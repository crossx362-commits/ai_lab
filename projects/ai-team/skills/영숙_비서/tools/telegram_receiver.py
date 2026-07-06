#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram bot — 게이트웨이.

역할: 텔레그램 폴링·핸들러·LLM 함수호출 디스패치 + 주문/신호 승인 흐름(봇 상태와 강결합).
도메인 툴은 각 주인 에이전트 모듈이 소유하고, 여기서는 BOT_TOOLS를 수집해 병합만 한다:
  - bot_common(bc)      : 종목 해석·의도 판별·서브프로세스 (공유 헬퍼)
  - bot_tools_info(info): 날씨·일정 (영숙 본연)
  - somi_bot_tools(somi): 종목/투자 (소미)
  - yewon_bot_tools(yewon): 오케스트레이션 (예원)"""

from __future__ import annotations

import asyncio
import json
import os
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
for _p in (str(AI_TEAM_ROOT),
           str(HERE.parent),
           str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"),
           str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

from _shared.env import load_env
from _shared.process import ProcessLock
from _shared import growth

import bot_common as bc
import bot_tools_info as info
import somi_bot_tools as somi
import yewon_bot_tools as yewon


load_env(str(PROJECT_ROOT))

try:
    from telegram import Update
    from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes
except ImportError as exc:
    print(f"Dependencies not installed: {exc}")
    print("Run: pip install python-telegram-bot")
    sys.exit(1)


SYSTEM = (
    "너는 영숙이야. 사장님(준호)의 비서이자 친구. "
    "친한 친구처럼 편하고 따뜻하게, 반말 섞어 짧고 자연스럽게 답해. "
    "딱딱한 안내체 금지. 단 사실·숫자는 정확히. "
    "운영/주식/검색은 로컬 도구 결과를 우선한다."
)

PSYCHOLOGY_SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 팀 비서이자 대화 상대다.
- 어떤 말에도 먼저 감정과 의도를 읽고 짧게 받아준다.
- 불안/분노/무기력/외로움은 심리학 관점으로 풀되, 전문 진단처럼 단정하지 않는다.
- 위기나 자해 위험이 보이면 즉시 주변 사람/응급 서비스/988 같은 도움을 권한다.
- 최신 자료가 필요하면 검색 도구를 먼저 사용한다.
- 텔레그램답게 짧고 자연스럽게 답한다."""


# ── LLM 툴 레지스트리 — 각 도메인 모듈의 BOT_TOOLS를 병합(새 툴 추가는 주인 모듈 한 곳만) ──
_ALL_BOT_TOOLS = info.BOT_TOOLS + somi.BOT_TOOLS + yewon.BOT_TOOLS
TOOLS = [t["schema"] for t in _ALL_BOT_TOOLS]
AVAILABLE_FUNCTIONS = {t["schema"]["function"]["name"]: t["handler"] for t in _ALL_BOT_TOOLS}


def web_search(query: str) -> str:
    """검색 요청 폴백. 실제 검색 도구가 없으면 GPT에 넘기지 않고 안내만 반환합니다."""
    return f"검색 기능이 아직 연결되지 않았습니다. 요청: {query}"


def _call_llm(prompt: str, system: str = SYSTEM) -> str:
    """클라우드 우선 폴백 — llm.text(lm_first=False)가 GPT→Gemini→클로드→로컬(승급) 전체
    체인을 담당(2026-07-03)."""
    from _shared import llm
    try:
        result = llm.text(prompt, system=system, max_tokens=800, temperature=0.8, lm_first=False)
    except Exception as exc:
        bc.log(f"_call_llm 실패: {exc}")
        result = None
    return result or "지금은 답변을 만들기 어려워요. 잠시 후 다시 시도해줘요."


# ── 수동 주식 주문 (사용자가 명시적으로 지시한 매수/매도만 실행, 확인 1단계) ──
_PENDING_ORDER: dict = {}
_BUY_WORDS = ("매수", "매입", "사줘", "사세요", "사라", "삽니다", "사기", "매수해", "매수주문")
_SELL_WORDS = ("매도", "매각", "팔아", "파세요", "팔자", "팔기", "판다", "매도해", "매도주문")

# ── 거래 모드(모의/실거래) — 텔레그램 명령으로 즉시 전환. 체결은 여전히 'ㅇㅋ' 승인 때만. ──
TRADE_MODE_FILE = PROJECT_ROOT / "output" / "cache" / "trade_mode.json"


def _get_trade_mode() -> str:
    """현재 거래 모드: 'paper'(기본) 또는 'live'."""
    try:
        return json.loads(TRADE_MODE_FILE.read_text(encoding="utf-8")).get("mode", "paper")
    except Exception:
        return "paper"


def _set_trade_mode(mode: str) -> None:
    TRADE_MODE_FILE.parent.mkdir(parents=True, exist_ok=True)
    TRADE_MODE_FILE.write_text(json.dumps({"mode": mode}, ensure_ascii=False), encoding="utf-8")


def _apply_trade_mode() -> str:
    """주문 실행 직전 호출 — trade_mode.json이 단일 소스이나, 실거래는 실서버·실 tr_id를
    보장하도록 KIS_REAL_MODE도 함께 세팅(모의서버로 새지 않게)."""
    mode = _get_trade_mode()
    if mode == "live":
        os.environ["KIS_PAPER"] = "false"
        os.environ["KIS_REAL_MODE"] = "true"
    else:
        os.environ["KIS_PAPER"] = "true"
    return mode


def _parse_trade_mode_command(text: str) -> str | None:
    """모드 전환/조회 명령. 반환=응답 문자열 또는 None(명령 아님)."""
    n = bc.normalize_text(text)
    if n in ("실거래", "실거래모드", "실거래전환", "라이브", "실전", "실거래켜", "실거래on"):
        _set_trade_mode("live")
        return ("🔴 실거래 모드로 전환했습니다.\n"
                "이제 매수신호에 'ㅇㅋ' 승인하면 **실제 계좌·실제 돈**으로 체결됩니다.\n"
                "되돌리려면 '모의 모드'.")
    if n in ("모의", "모의모드", "페이퍼", "모의투자", "모의전환", "실거래끄기", "실거래off"):
        _set_trade_mode("paper")
        return "🧪 모의(페이퍼) 모드로 전환했습니다. 체결은 가상으로만 처리됩니다."
    if n in ("거래모드", "모드확인", "지금모드", "현재모드", "모드"):
        cur = _get_trade_mode()
        label = "🔴 실거래(실제 돈)" if cur == "live" else "🧪 모의(페이퍼)"
        return f"현재 거래 모드: {label}\n전환: '실거래 모드' / '모의 모드'"
    return None


def _parse_order(text: str) -> dict | None:
    """'종목 N주 [가격원] 매수/매도' 형식 파싱. 명시적 수량+매매어가 있어야만 인식."""
    if any(w in text for w in _BUY_WORDS):
        side = "buy"
    elif any(w in text for w in _SELL_WORDS):
        side = "sell"
    else:
        return None
    mqty = re.search(r"(\d[\d,]*)\s*주", text)
    if not mqty:
        return None
    qty = int(mqty.group(1).replace(",", ""))
    mprice = re.search(r"(\d[\d,]*)\s*원", text)
    price = int(mprice.group(1).replace(",", "")) if mprice else 0
    name_part = re.sub(r"\d[\d,]*\s*(주|원)", " ", text)
    for w in _BUY_WORDS + _SELL_WORDS + ("지정가", "시장가", "주문", "해줘", "좀"):
        name_part = name_part.replace(w, " ")
    stock = bc.stock_from_text(name_part.strip()) or bc.stock_from_text(text)
    if not stock:
        return None
    symbol, name = stock
    return {"symbol": symbol, "name": name, "qty": qty, "side": side, "price": price}


SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))


def _parse_somi_approve(text: str) -> dict | None:
    """'소미 승인 <종목명> [N주]' → 제안 기반 매수 주문(손절/목표 포함)."""
    n = bc.normalize_text(text)
    if "소미" not in n or "승인" not in n:
        return None
    try:
        from somi_trade_advisor import get_proposal
    except Exception:
        return None
    rest = text.replace("소미", " ").replace("승인", " ")
    mqty = re.search(r"(\d[\d,]*)\s*주", rest)
    qty = int(mqty.group(1).replace(",", "")) if mqty else 0
    key = re.sub(r"\d[\d,]*\s*주", " ", rest).strip()
    prop = get_proposal(key) if key else None
    if not prop:
        return None
    if qty <= 0:
        entry = prop.get("entry") or 1
        qty = max(1, int(SOMI_BUDGET // entry))
    return {
        "symbol": prop["symbol"], "name": prop["name"], "qty": qty,
        "side": "buy", "price": 0,
        "entry": prop["entry"], "stop": prop["stop"], "target": prop["target"],
    }


def _parse_somi_sell(text: str) -> dict | None:
    """'소미 매도 <종목>' → 보유 수량 전량(또는 지정 수량) 매도."""
    n = bc.normalize_text(text)
    if "소미" not in n or "매도" not in n:
        return None
    try:
        from somi_trade_advisor import load_positions
    except Exception:
        return None
    rest = text.replace("소미", " ").replace("매도", " ")
    mqty = re.search(r"(\d[\d,]*)\s*주", rest)
    key = re.sub(r"\d[\d,]*\s*주", " ", rest).strip()
    stock = bc.stock_from_text(key) if key else None
    if not stock:
        return None
    symbol, name = stock
    positions = load_positions()
    held = int(positions.get(symbol, {}).get("qty", 0))
    qty = int(mqty.group(1).replace(",", "")) if mqty else held
    if qty <= 0:
        return None
    return {"symbol": symbol, "name": name, "qty": qty, "side": "sell", "price": 0}


def _order_confirm_prompt(order: dict) -> str:
    sidetxt = "매수" if order["side"] == "buy" else "매도"
    pricetxt = f"{order['price']:,}원 지정가" if order["price"] else "시장가"
    mode = "🧪 모의(페이퍼) 주문 확인" if _get_trade_mode() != "live" else "🔴 실거래 주문 확인(실제 돈)"
    return (
        f"{mode}\n"
        f"{order['name']}({order['symbol']}) {order['qty']:,}주 {pricetxt} {sidetxt}\n\n"
        f"체결하려면 '확인', 취소하려면 '취소'라고 답해줘요."
    )


def _execute_pending_order() -> str:
    order = _PENDING_ORDER.pop("order", None)
    if not order:
        return "대기 중인 주문이 없어요."
    _apply_trade_mode()
    try:
        from kis_trader import KISTrader
        result = KISTrader().order(order["symbol"], order["qty"], order["side"], order["price"])
    except Exception as exc:
        return f"❌ 주문 실패: {exc}"
    try:
        from somi_trade_advisor import record_position, remove_position
        if order["side"] == "buy" and order.get("target"):
            record_position(order["symbol"], order["name"], order.get("entry", 0),
                            order.get("stop", 0), order.get("target", 0), order["qty"])
        elif order["side"] == "sell":
            remove_position(order["symbol"])
    except Exception:
        pass
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "한별_퀀트" / "tools"))
        import quant_analyzer
        if order["side"] == "buy":
            px = order.get("entry") or 0
        else:
            px = 0
            try:
                from kis_trader import KISTrader as _KT
                from somi_kis_reporter import num as _num
                px = _num(_KT().kis.quote(order["symbol"]).get("stck_prpr"))
            except Exception:
                px = order.get("price") or 0
        quant_analyzer.append(order["side"], order["symbol"], order["name"], order["qty"],
                              px, order.get("score"), order.get("entry"),
                              order.get("stop"), order.get("target"))
    except Exception:
        pass
    sidetxt = "매수" if order["side"] == "buy" else "매도"
    extra = ""
    if order.get("target"):
        extra = f"\n손절 {int(order['stop']):,} / 목표 {int(order['target']):,} 감시 시작"
    return (
        f"✅ {order['name']}({order['symbol']}) {order['qty']:,}주 {sidetxt} 주문 접수\n"
        f"주문번호: {result.get('order_no')} / {result.get('time')}\n{result.get('msg','')}{extra}"
    )


# ── 푸시된 매수신호 원터치 승인 (신호 → 원터치 체결) ──
PENDING_SIGNALS_FILE = PROJECT_ROOT / "output" / "cache" / "pending_signals.json"
_SIGNAL_APPROVE_RE = re.compile(
    r"^(ㅇㅋ|오케이|오키|ok|okay|승인|콜|ㄱㄱ|고고|가자|간다|사자|매수승인)\s*(\d+)?\s*(번)?$"
)


def _signals_load() -> list[dict]:
    """만료되지 않은 대기 신호 목록."""
    try:
        data = json.loads(PENDING_SIGNALS_FILE.read_text(encoding="utf-8"))
    except Exception:
        return []
    out, now = [], datetime.now()
    for s in data.get("signals", []):
        exp = s.get("expires")
        if exp:
            try:
                if datetime.fromisoformat(exp) < now:
                    continue
            except Exception:
                pass
        out.append(s)
    return out


def _signals_clear() -> None:
    try:
        PENDING_SIGNALS_FILE.write_text(
            json.dumps({"signals": []}, ensure_ascii=False), encoding="utf-8")
    except Exception:
        pass


def _signals_remove(sig_id: int) -> None:
    try:
        data = json.loads(PENDING_SIGNALS_FILE.read_text(encoding="utf-8"))
    except Exception:
        return
    data["signals"] = [s for s in data.get("signals", []) if s.get("id") != sig_id]
    PENDING_SIGNALS_FILE.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def _parse_signal_approve(text: str):
    """푸시된 매수신호 원터치 승인. 승인어(+선택 번호)만 인식.
    반환: (order, signal_id) 또는 None. 신호가 없으면 None."""
    signals = _signals_load()
    if not signals:
        return None
    m = _SIGNAL_APPROVE_RE.match(bc.normalize_text(text))
    if not m:
        return None
    idx = m.group(2)
    sig = next((s for s in signals if s.get("id") == int(idx)), None) if idx else signals[0]
    if not sig:
        return None
    order = {
        "symbol": sig["symbol"], "name": sig["name"], "qty": int(sig["qty"]),
        "side": "buy", "price": 0,
        "entry": sig.get("entry", 0), "stop": sig.get("stop", 0), "target": sig.get("target", 0),
        "score": sig.get("score"),
    }
    return order, sig["id"]


def handle_with_gpt(text: str) -> str:
    """자연어 → 도구 선택·실행 (구독/로컬 LLM 체인 수동 tool use — 크레딧 불필요, 2026-07-05).
    구 클로드 API tool use는 크레딧0에서 마비 → llm.text(구독 클로드 우선) 기반으로 전환.
    ①도구 선택(JSON) ②실행 ③결과를 영숙 말투로 요약. 딱 맞는 도구 없으면 일반 대화.
    (함수명은 handle_message 호환성 위해 유지)"""
    from _shared import llm
    tool_lines = "\n".join(
        f"- {t['schema']['function']['name']}: {t['schema']['function']['description'][:70]}"
        for t in _ALL_BOT_TOOLS)
    sel_prompt = (
        f'사용자 메시지: "{text}"\n\n사용 가능한 도구:\n{tool_lines}\n\n'
        '메시지에 딱 맞는 도구가 있으면 그 도구명을, 없으면(일반 대화·잡담·인사·질문) null.\n'
        'JSON만 출력: {"tool": "도구명 또는 null", "args": {필요한 인자 객체}}'
    )
    try:
        raw = llm.text(sel_prompt, json_mode=True, max_tokens=500, lm_first=False)
        sel = json.loads(raw) if raw else {}
    except Exception:
        sel = {}
    tool = sel.get("tool")
    fn = AVAILABLE_FUNCTIONS.get(tool) if (tool and tool != "null") else None
    if fn:
        bc.log(f"Calling {tool} with {sel.get('args')}")
        try:
            result = fn(**(sel.get("args") or {}))
        except Exception as e:
            result = f"도구 실행 실패: {e}"
        ans = llm.text(
            f'사용자가 "{text}"라고 물었어. 도구 결과:\n{result}\n\n'
            '이 결과를 영숙이답게 친근하게(반말 섞어 짧게) 전해줘. 숫자·사실은 정확히.',
            system=SYSTEM, max_tokens=800, lm_first=False)
        return ans or str(result)
    # 딱 맞는 도구 없음 → 일반 대화(심리 프롬프트)
    return _call_llm(text, PSYCHOLOGY_SYSTEM)


def _trading_status_reply(text: str, is_live: bool) -> str:
    """거래/손익 현황 — 결정적 팩트(get_trading_status)를 뽑아, 원문 질문 의미에 맞춰
    LLM이 동적으로 재구성. '손익'이면 손익 금액 중심, '거래현황'이면 활동 중심 등.
    숫자·종목·금액은 도구 결과에 있는 값만 사용(지어내지 않음). 실패 시 원문 팩트 그대로."""
    facts = somi.get_trading_status(is_live)
    from _shared import llm
    try:
        ans = llm.text(
            f'사용자가 "{text}"라고 물었어. 아래는 실시간 보유·손익 데이터야:\n{facts}\n\n'
            '질문 의도에 딱 맞춰 영숙이답게 친근하게(반말 섞어 짧게) 답해줘. '
            '숫자·종목명·금액·수익률은 데이터에 있는 값만 그대로 쓰고 절대 새로 만들지 마.',
            system=SYSTEM, max_tokens=800, lm_first=False)
    except Exception:
        ans = None
    return ans or facts


def _classify_intent(text: str, has_order: bool, has_signals: bool) -> dict | None:
    """LLM이 메시지의 '의미'를 분석해 운영 명령 의도를 분류. 정해진 단어가 아니어도 같은 뜻이면 매칭.
    반환: {"intent":..., "index":n|None} 또는 None(해당 없음/casual)."""
    from _shared import llm
    opts = ["mode_live=실거래(실제 돈)로 전환", "mode_paper=모의(가상)로 전환",
            "mode_status=현재 거래 모드 조회",
            "status=거래/투자 현황 조회(보유 종목·손익·오늘 거래 — 오타 포함 '거래현황' 류)",
            "none=거래 운영 명령이 아님(일반 대화/질문)"]
    if has_order:
        opts = ["order_confirm=대기 중인 주문 체결 승인", "order_cancel=대기 주문 취소"] + opts
    if has_signals:
        opts = ["signal_approve=푸시된 매수신호 승인(index=종목 번호, 없으면 1)",
                "pass=신호 보류/넘김"] + opts
    prompt = (
        "주식 봇 사용자의 메시지 의도를 분류하세요. 미리 정한 단어가 아니라 '의미'로 판단합니다.\n"
        "예) '이제 진짜로 사자'·'실전으로 돌려'→mode_live, '가상으로만'·'연습모드'→mode_paper, "
        "'지금 뭘로 돼있어?'→mode_status, 'ㄱㅓ래현황'·'포지션 어때'→status, "
        "'그래 사', '오케이 2번'→signal_approve, '넘겨'→pass.\n"
        f"가능한 의도: {'; '.join(opts)}\n"
        f'메시지: "{text}"\n'
        '애매하거나 단순 대화면 none. JSON만: {"intent":"...","index":번호 또는 null}'
    )
    try:
        raw = llm.text(prompt, json_mode=True, max_tokens=1500, temperature=0, lm_first=False)
    except Exception:
        raw = None
    if not raw:
        return None
    a, b = raw.find("{"), raw.rfind("}")
    if a < 0 or b <= a:
        return None
    try:
        d = json.loads(raw[a:b + 1])
    except Exception:
        return None
    it = str(d.get("intent", "")).strip()
    if not it or it == "none":
        return None
    out = {"intent": it}
    try:
        if d.get("index") is not None:
            out["index"] = int(d["index"])
    except Exception:
        pass
    return out


def _approve_signal(sig: dict) -> str:
    """대기 신호 1건을 주문으로 만들어 즉시 체결."""
    order = {
        "symbol": sig["symbol"], "name": sig["name"], "qty": int(sig["qty"]),
        "side": "buy", "price": 0,
        "entry": sig.get("entry", 0), "stop": sig.get("stop", 0),
        "target": sig.get("target", 0), "score": sig.get("score"),
    }
    _PENDING_ORDER["order"] = order
    _signals_remove(sig["id"])
    return _execute_pending_order()


def _dispatch_intent(intent: dict, has_order: bool, has_signals: bool, text: str = "") -> str | None:
    """분류된 의도를 실제 명령으로 실행. 처리 못 하면 None."""
    it = intent.get("intent")
    if it == "order_confirm" and has_order:
        return _execute_pending_order()
    if it == "order_cancel" and has_order:
        _PENDING_ORDER.pop("order", None)
        return "주문을 취소했어요."
    if it == "mode_live":
        _set_trade_mode("live")
        return ("🔴 실거래 모드로 전환했습니다.\n이제 신호 승인 시 실제 계좌·실제 돈으로 체결됩니다. "
                "되돌리려면 '모의로'.")
    if it == "mode_paper":
        _set_trade_mode("paper")
        return "🧪 모의(페이퍼) 모드로 전환했습니다. 체결은 가상으로만 처리됩니다."
    if it == "mode_status":
        cur = _get_trade_mode()
        return f"현재 거래 모드: {'🔴 실거래(실제 돈)' if cur == 'live' else '🧪 모의(페이퍼)'}"
    if it == "status":
        return _trading_status_reply(text, _get_trade_mode() == "live")
    if it == "signal_approve" and has_signals:
        signals = _signals_load()
        idx = intent.get("index")
        s = next((x for x in signals if x.get("id") == idx), None) if idx else signals[0]
        if s:
            return _approve_signal(s)
    if it == "pass" and has_signals:
        _signals_clear()
        return "넘어갈게요. 계속 감시하겠습니다."
    return None


DEV_RUNNER = Path(__file__).resolve().parent / "tg_dev_runner.py"


def _launch_dev_task(request: str) -> str:
    """'개발 <요청>' → 격리 worktree에서 헤드리스 claude 실행(백그라운드). 끝나면 결과를 별도 보고."""
    if not request:
        return "개발 요청 내용을 적어줘. 예: '개발 소미 리포트에 RSI 지표 추가'"
    try:
        wt = subprocess.run(["git", "worktree", "list"], cwd=str(PROJECT_ROOT),
                            capture_output=True, text=True, timeout=10).stdout
        if "ailab-dev-" in wt:
            return "🛠️ 이미 진행 중인 개발 작업이 있어. 끝나면 다시 요청해줘."
    except Exception:
        pass
    branch = "tg-dev-" + datetime.now().strftime("%m%d-%H%M%S")
    try:
        subprocess.Popen(
            [sys.executable, str(DEV_RUNNER), branch, request],
            cwd=str(PROJECT_ROOT),
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            start_new_session=True,
        )
    except Exception as exc:
        return f"개발 작업 시작 실패: {exc}"
    return (f"🛠️ 개발 작업 시작했어 (브랜치 {branch}).\n요청: {request}\n"
            f"격리 환경에서 claude가 작업하고, 끝나면 변경 요약을 보고할게. (보통 1~3분, master는 안 건드려)")


def handle_message(text: str) -> str:
    """Telegram 자연어 메시지를 처리합니다. 운영 명령은 '의미'로 인식합니다(정해진 단어 불필요)."""
    text = (text or "").strip()
    if not text:
        return "메시지가 비어 있어요."

    if text.startswith("개발 ") or text.startswith("기능요청 "):
        return _launch_dev_task(text.split(" ", 1)[1].strip() if " " in text else "")

    has_order = bool(_PENDING_ORDER.get("order"))
    has_signals = bool(_signals_load())

    # 1) 빠른 정확매칭 (LLM 없이 즉답)
    if has_order:
        n = bc.normalize_text(text)
        if n in ("확인", "ㅇㅇ", "응", "ok", "yes", "체결"):
            return _execute_pending_order()
        if n in ("취소", "ㄴㄴ", "아니", "no", "cancel"):
            _PENDING_ORDER.pop("order", None)
            return "주문을 취소했어요."
    mode_reply = _parse_trade_mode_command(text)
    if mode_reply:
        return mode_reply
    sig = _parse_signal_approve(text)
    if sig:
        order, sig_id = sig
        _PENDING_ORDER["order"] = order
        _signals_remove(sig_id)
        return _execute_pending_order()
    if bc.is_pass(text):
        _signals_clear()
        return "넘어갈게요. 계속 감시하겠습니다."

    # 거래/투자 현황(보유 포지션·손익) — LLM 분류기보다 먼저
    if bc.is_trading_status_request(text):
        return _trading_status_reply(text, _get_trade_mode() == "live")

    # 2) 의미 기반 분류 — 정확매칭이 안 됐고, 운영 맥락이거나 짧은 지시일 때만 LLM 호출
    if has_order or has_signals or len(text) <= 30:
        intent = _classify_intent(text, has_order, has_signals)
        if intent:
            resolved = _dispatch_intent(intent, has_order, has_signals, text)
            if resolved is not None:
                return resolved

    # 3) 위에서 안 잡힌 대기 주문은 만료
    if has_order:
        _PENDING_ORDER.pop("order", None)

    # 소미 매수 제안 승인 / 매도 — 모의는 즉시 체결, 실거래는 '확인' 단계
    somi_order = _parse_somi_approve(text) or _parse_somi_sell(text)
    if somi_order:
        _PENDING_ORDER["order"] = somi_order
        if _get_trade_mode() != "live":
            return _execute_pending_order()
        return _order_confirm_prompt(somi_order)

    # 수동 매수/매도 지시 — 모의는 즉시 체결, 실거래는 '확인' 단계
    order = _parse_order(text)
    if order:
        _PENDING_ORDER["order"] = order
        if _get_trade_mode() != "live":
            return _execute_pending_order()
        return _order_confirm_prompt(order)

    if bc.is_balance_request(text):
        return somi.balance()

    # 신규 에이전트 생성 승인/거절 (예원 오케스트레이터 제안에 대한 응답)
    _n = bc.normalize_text(text)
    if _n in ("에이전트승인", "새에이전트승인", "에이전트생성승인"):
        return yewon.agent_factory_action("approve_pending")
    if _n in ("에이전트거절", "에이전트취소", "새에이전트거절"):
        return yewon.agent_factory_action("reject_pending")

    if bc.is_stock_price_request(text):
        return somi.get_stock_price(text)

    if bc.is_screener_request(text):
        return somi.dispatch_screener(text)

    if bc.is_stock_analysis_request(text):
        return somi.dispatch_to_somi(text)

    if bc.is_weather_request(text):
        return info.get_weather(info.parse_weather_city(text), info.parse_weather_day(text))

    if bc.is_search_request(text):
        return web_search(text)

    gpt_response = handle_with_gpt(text)
    if not gpt_response.startswith("요청 처리 중 오류가 발생했습니다:"):
        return gpt_response
    return _call_llm(text, PSYCHOLOGY_SYSTEM)


async def _start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text("영숙 비서 대기 중입니다.")


async def _status(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text(yewon.get_agent_status())


_ALLOWED_CHAT_IDS = {c.strip() for c in (os.getenv("TELEGRAM_CHAT_ID", "")).split(",") if c.strip()}


async def _message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    # 발신자 잠금 — fail-closed: 허용 목록이 비면 전부 거부. user.id·chat.id 모두 대조(그룹 권한상승 차단)
    uid = str(update.effective_user.id) if update.effective_user else ""
    cid = str(update.effective_chat.id) if update.effective_chat else ""
    if not _ALLOWED_CHAT_IDS or (uid not in _ALLOWED_CHAT_IDS and cid not in _ALLOWED_CHAT_IDS):
        bc.log(f"차단된 발신자 user={uid} chat={cid}: {(update.effective_message.text or '')[:50]}")
        return

    text = update.effective_message.text or ""
    bc.log(f"Message: {text[:100]}")
    try:
        response = await asyncio.to_thread(handle_message, text)
    except Exception as exc:
        bc.log(f"Handler error: {exc}")
        response = f"요청 처리 중 오류가 발생했습니다: {exc}"

    chunks = [response[i : i + 4000] for i in range(0, len(response), 4000)]
    for chunk in chunks:
        await update.effective_message.reply_text(chunk)

    growth.record(
        "youngsuk", role="명령 수신·전달·승인 확인", data=text[:60],
        judgment="명령 처리·응답", result="응답 전송",
        good="발신자 잠금(fail-closed) 적용", bad="명령 분류 라벨링 정식화 여지",
        scores={"fit": 20, "evidence": 17, "efficiency": 19, "risk": 18, "brevity": 9},
    )


def main() -> int:
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    if not token:
        print("TELEGRAM_BOT_TOKEN is not configured.")
        return 1

    # 단일 인스턴스 보장 — 중복 폴러가 getUpdates Conflict를 일으키지 않도록
    with ProcessLock("youngsuk_telegram"):
        app = Application.builder().token(token).build()
        app.add_handler(CommandHandler("start", _start))
        app.add_handler(CommandHandler("status", _status))
        app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, _message))

        bc.log("Youngsuk Telegram bot started (polling mode)")
        app.run_polling(allowed_updates=["message"], drop_pending_updates=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
