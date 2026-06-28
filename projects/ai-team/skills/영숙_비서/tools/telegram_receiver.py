#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram bot — polling mode with GPT-4o-mini function calling."""

from __future__ import annotations

import asyncio
import json
import os
import re
import subprocess
import sys
import urllib.parse
import urllib.request
from datetime import datetime
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
from _shared.notify import status_report


load_env(str(PROJECT_ROOT))

try:
    from telegram import Update
    from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes
    from openai import OpenAI
except ImportError as exc:
    print(f"Dependencies not installed: {exc}")
    print("Run: pip install python-telegram-bot openai")
    sys.exit(1)


client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

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


_STOCK_ALIASES: dict[str, tuple[str, str]] = {
    "삼전": ("005930", "삼성전자"),
    "삼성": ("005930", "삼성전자"),
    "삼성전자": ("005930", "삼성전자"),
    "하이닉스": ("000660", "SK하이닉스"),
    "sk하이닉스": ("000660", "SK하이닉스"),
    "sk하닉": ("000660", "SK하이닉스"),
    "하닉": ("000660", "SK하이닉스"),
    "카카오": ("035720", "카카오"),
    "네이버": ("035420", "NAVER"),
    "현대차": ("005380", "현대차"),
    "기아": ("000270", "기아"),
    "우리기술": ("032820", "우리기술"),
}


def log(message: str) -> None:
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{stamp}] {message}", flush=True)


def _run_python(script: Path, *args: str, timeout: int = 60) -> str:
    if not script.exists():
        return f"경로가 없습니다: {script}"
    env = os.environ.copy()
    env["PYTHONUTF8"] = "1"
    kwargs = {}
    if sys.platform == "win32":
        kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW
    result = subprocess.run(
        [sys.executable, str(script), *args],
        cwd=str(PROJECT_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=timeout,
        **kwargs,
    )
    output = (result.stdout or "").strip()
    error = (result.stderr or "").strip()
    if result.returncode != 0:
        return error or output or f"실행 실패: {result.returncode}"
    return output or "완료했습니다."


def _normalize_text(text: str) -> str:
    return re.sub(r"\s+", "", text or "").lower()


_STOCK_CMD_WORDS = [
    "분석해줘", "분석해", "분석", "리포트", "보고서", "보고", "전망", "주가", "현재가",
    "가격", "종목", "알려줘", "보여줘", "해줘", "봐줘", "어때", "체크", "확인", "해봐",
    "말해줘", "좀", "오늘", "지금",
]


def _extract_stock_query(text: str) -> str:
    """문장에서 명령어/조사를 제거해 종목명 후보만 추출."""
    t = text or ""
    for word in _STOCK_CMD_WORDS:
        t = t.replace(word, " ")
    t = re.sub(r"\d{6}", " ", t)
    t = re.sub(r"[^0-9A-Za-z가-힣 ]", " ", t)
    return re.sub(r"\s+", " ", t).strip()


def _resolve_from_search(text: str) -> tuple[str, str] | None:
    """본문에서 종목 해석. 로컬 주요 종목 맵 → 네이버 자동완성(임의 종목) 순."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from stock_search import MAJOR_STOCKS, STOCK_NAME_ALIASES, naver_search
    except Exception:
        return None
    normalized = _normalize_text(text)
    for alias, name in sorted(STOCK_NAME_ALIASES.items(), key=lambda i: len(i[0]), reverse=True):
        if _normalize_text(alias) in normalized and name in MAJOR_STOCKS:
            return MAJOR_STOCKS[name], name
    for name, code in sorted(MAJOR_STOCKS.items(), key=lambda i: len(i[0]), reverse=True):
        if _normalize_text(name) in normalized:
            return code, name
    # 별칭/주요종목에 없으면 네이버 자동완성으로 물어본 종목 해석
    candidate = _extract_stock_query(text)
    if candidate:
        hit = naver_search(candidate)
        if hit:
            return hit
        # 후보가 여러 토큰이면 가장 긴 토큰으로 재시도
        tokens = sorted(candidate.split(), key=len, reverse=True)
        if len(tokens) > 1:
            hit = naver_search(tokens[0])
            if hit:
                return hit
    return None


def _stock_from_text(text: str) -> tuple[str, str] | None:
    normalized = _normalize_text(text)
    for alias, stock in sorted(_STOCK_ALIASES.items(), key=lambda item: len(item[0]), reverse=True):
        if _normalize_text(alias) in normalized:
            return stock

    code_match = re.search(r"\b(\d{6})\b", text or "")
    if code_match:
        return code_match.group(1), code_match.group(1)

    return _resolve_from_search(text)


def _is_search_request(text: str) -> bool:
    normalized = _normalize_text(text)
    return any(word in normalized for word in ("검색", "찾아봐", "찾아줘", "최신자료", "자료찾"))


def _is_screener_request(text: str) -> bool:
    normalized = _normalize_text(text)
    return any(
        word in normalized
        for word in ("유망종목", "유망주", "종목발굴", "발굴", "종목추천", "추천종목", "급등주", "살만한", "뭐사")
    )


def _is_stock_analysis_request(text: str) -> bool:
    normalized = _normalize_text(text)
    if not any(word in normalized for word in ("분석", "리포트", "보고서", "전망")):
        return False
    return _stock_from_text(text) is not None or any(word in normalized for word in ("주식", "종목"))


def _is_stock_price_request(text: str) -> bool:
    normalized = _normalize_text(text)
    return any(word in normalized for word in ("주가", "현재가", "가격")) and _stock_from_text(text) is not None


def _is_trading_status_request(text: str) -> bool:
    normalized = _normalize_text(text)
    return any(word in normalized for word in ("거래현황", "봇현황", "자동매매현황", "매매현황"))


def _is_weather_request(text: str) -> bool:
    n = _normalize_text(text)
    return any(w in n for w in ("날씨", "기온", "더워", "추워", "비와", "비온", "눈와", "미세먼지", "weather"))


_WEATHER_DESC_KO = {
    "Sunny": "맑음", "Clear": "맑음", "Partly cloudy": "구름 조금", "Cloudy": "흐림",
    "Overcast": "흐림", "Mist": "안개", "Fog": "안개", "Patchy rain possible": "비 가능",
    "Patchy rain nearby": "비 약간", "Light rain": "가랑비", "Light drizzle": "이슬비",
    "Moderate rain": "비", "Heavy rain": "폭우", "Light snow": "눈 약간", "Snow": "눈",
    "Thundery outbreaks possible": "천둥 가능",
}


def _parse_weather_day(text: str) -> int:
    """0=오늘(현재), 1=내일, 2=모레."""
    n = _normalize_text(text)
    if "모레" in n:
        return 2
    if "내일" in n:
        return 1
    return 0


def _parse_weather_city(text: str) -> str:
    t = text
    # 시간 표현은 도시명이 아니므로 먼저 제거 (예: "내일 날씨" → 도시 없음 → 서울)
    for w in ("모레", "내일", "오늘", "지금", "현재", "이번주", "주말",
              "날씨", "예보", "어때", "어떄", "기온", "좀", "알려줘", "?", "？", "미세먼지"):
        t = t.replace(w, " ")
    t = re.sub(r"\s+", " ", t).strip()
    return t or "서울"


def _weather_desc(node: dict) -> str:
    desc = (node.get("lang_ko") or [{}])[0].get("value") or (node.get("weatherDesc") or [{}])[0].get("value", "")
    return _WEATHER_DESC_KO.get(desc.strip(), desc.strip())


def get_weather(city: str = "서울", day: int = 0) -> str:
    """도시의 현재 날씨 또는 내일/모레 예보를 조회합니다 (wttr.in, 키 불필요)."""
    city = (city or "서울").strip()
    try:
        day = max(0, min(2, int(day or 0)))
    except (TypeError, ValueError):
        day = 0
    try:
        url = f"https://wttr.in/{urllib.parse.quote(city)}?format=j1&lang=ko"
        req = urllib.request.Request(url, headers={"User-Agent": "curl/8"})
        with urllib.request.urlopen(req, timeout=10) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))

        if day == 0:
            c = d["current_condition"][0]
            return (
                f"🌤️ {city} 현재 날씨\n"
                f"기온 {c['temp_C']}°C (체감 {c['FeelsLikeC']}°C) / {_weather_desc(c)}\n"
                f"습도 {c['humidity']}% / 바람 {c['windspeedKmph']}km/h / 강수 {c['precipMM']}mm"
            )

        w = d["weather"][day]
        label = {1: "내일", 2: "모레"}.get(day, f"{day}일 후")
        hourly = w.get("hourly") or []
        mid = hourly[4] if len(hourly) > 4 else (hourly[len(hourly) // 2] if hourly else {})
        rain = mid.get("chanceofrain", "?")
        return (
            f"🌤️ {city} {label}({w.get('date', '')}) 날씨\n"
            f"최고 {w['maxtempC']}°C / 최저 {w['mintempC']}°C / {_weather_desc(mid)}\n"
            f"강수확률 {rain}%"
        )
    except Exception as exc:
        return f"날씨 조회 실패: {exc}"


def web_search(query: str) -> str:
    """검색 요청 폴백. 실제 검색 도구가 없으면 GPT에 넘기지 않고 안내만 반환합니다."""
    return f"검색 기능이 아직 연결되지 않았습니다. 요청: {query}"


def _call_llm(prompt: str, system: str = SYSTEM) -> str:
    """안정적인 클라우드 우선 폴백. Ollama는 연결 불안정이 잦아 최후수단으로만 시도."""
    from _shared import llm

    # 1순위: gpt-4o-mini (영숙 주 모델과 동일, 가장 안정)
    # 2순위: gemini-2.5-flash (무료 쿼터 여유 있음)
    # 3순위: Ollama (로컬, 불안정 — 위 둘 다 실패했을 때만)
    providers = [
        ("gpt", lambda: llm.gpt(prompt, system=system, max_tokens=500, temperature=0.8)),
        ("gemini", lambda: llm.gemini(prompt, system=system, max_tokens=500, temperature=0.8)),
        ("ollama", lambda: llm.ollama(prompt, system=system, max_tokens=500, temperature=0.8)),
    ]
    for name, call in providers:
        try:
            result = call()
        except Exception as exc:
            log(f"_call_llm {name} 실패: {exc}")
            continue
        if result:
            return result

    return "지금은 답변을 만들기 어려워요. 잠시 후 다시 시도해줘요."


def _tool_get_agent_status() -> str:
    return get_agent_status()


def _tool_get_trading_status() -> str:
    return status_report()


def _tool_get_stock_price(text: str) -> str:
    stock = _stock_from_text(text)
    if not stock:
        return "종목을 찾지 못했어요. 예: 삼전 주가, 하이닉스 현재가"

    symbol, name = stock
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from somi_kis_reporter import KISClient, fmt_pct, fmt_int, pick

        quote = KISClient().quote(symbol)
        price = fmt_int(pick(quote, "stck_prpr"))
        change = fmt_int(pick(quote, "prdy_vrss"))
        rate = fmt_pct(pick(quote, "prdy_ctrt"))
        return f"{name} 현재가: {price or '확인 필요'}원, 전일대비 {change or '0'}원 ({rate or '0.00%'})"
    except Exception as exc:
        return f"{name} 현재가 조회 실패: {exc}"


def get_agent_status(_: str = "전체") -> str:
    """현재 AI 팀 에이전트 현황을 조회합니다."""
    return status_report()


def list_calendar() -> str:
    """일정 및 스케줄을 조회합니다."""
    script = HERE.with_name("schedule_manager.py")
    return _run_python(script, "--list", timeout=30)


def dispatch_to_yewon(text: str) -> str:
    """예원 CEO에게 작업을 요청합니다 (플랜 기반 멀티 에이전트 오케스트레이션)."""
    script = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_orchestrator.py"
    return _run_python(script, text, timeout=180)


def dispatch_screener(_: str = "") -> str:
    """소미 매수 제안 (발굴+점수화 → 진입/손절/목표/이유/위험, 승인형)."""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_trade_advisor.py"
    return _run_python(script, "--propose", "--candidates", "20", timeout=300)


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
    """주문 실행 직전 호출 — 모드 파일을 KIS_PAPER 환경변수에 반영해 KISTrader가 따르게 한다."""
    mode = _get_trade_mode()
    os.environ["KIS_PAPER"] = "false" if mode == "live" else "true"
    return mode


def _parse_trade_mode_command(text: str) -> str | None:
    """모드 전환/조회 명령. 반환=응답 문자열 또는 None(명령 아님)."""
    n = _normalize_text(text)
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


def _is_balance_request(text: str) -> bool:
    n = _normalize_text(text)
    return any(w in n for w in ("잔고", "보유종목", "내주식", "내종목", "예수금", "내계좌"))


def _tool_balance() -> str:
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from kis_trader import KISTrader, _fmt_balance
        t = KISTrader()
        mode = "실거래" if t.real else "모의투자"
        return f"[{mode} 계좌 {t.cano}-{t.prod}]\n" + _fmt_balance(t.balance())
    except Exception as exc:
        return f"잔고 조회 실패: {exc}"


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
    # 종목명 분리: 수량/가격/매매어 제거 후 해석
    name_part = re.sub(r"\d[\d,]*\s*(주|원)", " ", text)
    for w in _BUY_WORDS + _SELL_WORDS + ("지정가", "시장가", "주문", "해줘", "좀"):
        name_part = name_part.replace(w, " ")
    stock = _stock_from_text(name_part.strip()) or _stock_from_text(text)
    if not stock:
        return None
    symbol, name = stock
    return {"symbol": symbol, "name": name, "qty": qty, "side": side, "price": price}


SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))  # 제안 승인 시 1종목 기본 예산


def _is_pass(text: str) -> bool:
    return _normalize_text(text) in ("패스", "pass", "넘겨", "스킵", "skip", "보류")


def _parse_somi_approve(text: str) -> dict | None:
    """'소미 승인 <종목명> [N주]' → 제안 기반 매수 주문(손절/목표 포함)."""
    n = _normalize_text(text)
    if "소미" not in n or "승인" not in n:
        return None
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
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
    if qty <= 0:  # 미지정 시 예산 기반 수량
        entry = prop.get("entry") or 1
        qty = max(1, int(SOMI_BUDGET // entry))
    return {
        "symbol": prop["symbol"], "name": prop["name"], "qty": qty,
        "side": "buy", "price": 0,
        "entry": prop["entry"], "stop": prop["stop"], "target": prop["target"],
    }


def _parse_somi_sell(text: str) -> dict | None:
    """'소미 매도 <종목>' → 보유 수량 전량(또는 지정 수량) 매도."""
    n = _normalize_text(text)
    if "소미" not in n or "매도" not in n:
        return None
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from somi_trade_advisor import load_positions
    except Exception:
        return None
    rest = text.replace("소미", " ").replace("매도", " ")
    mqty = re.search(r"(\d[\d,]*)\s*주", rest)
    key = re.sub(r"\d[\d,]*\s*주", " ", rest).strip()
    stock = _stock_from_text(key) if key else None
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
    _apply_trade_mode()   # 모드 파일(모의/실거래)을 KIS_PAPER 환경변수에 반영
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from kis_trader import KISTrader
        result = KISTrader().order(order["symbol"], order["qty"], order["side"], order["price"])
    except Exception as exc:
        return f"❌ 주문 실패: {exc}"
    # 포지션 기록(매수 제안 승인 시 손절/목표 저장) / 해제(매도 시)
    try:
        from somi_trade_advisor import record_position, remove_position
        if order["side"] == "buy" and order.get("target"):
            record_position(order["symbol"], order["name"], order.get("entry", 0),
                            order.get("stop", 0), order.get("target", 0), order["qty"])
        elif order["side"] == "sell":
            remove_position(order["symbol"])
    except Exception:
        pass
    # 한별(퀀트) 거래일지 기록 — 성과 복기·튜닝용
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "한별_퀀트" / "tools"))
        import quant_analyzer
        if order["side"] == "buy":
            px = order.get("entry") or 0
        else:  # 매도 체결가 best-effort 조회
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
    from datetime import datetime
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
    m = _SIGNAL_APPROVE_RE.match(_normalize_text(text))
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


def _agent_factory_action(fn: str) -> str:
    """예원 agent_factory의 승인/거절 호출 (신규 에이전트 생성 게이트)."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools"))
        import agent_factory
        return getattr(agent_factory, fn)()
    except Exception as exc:
        return f"에이전트 생성 처리 실패: {exc}"


def dispatch_to_somi(text: str) -> str:
    """소미 분석가에게 종목 분석을 요청합니다."""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_kis_reporter.py"
    stock = _stock_from_text(text)
    if not stock:
        return "어떤 종목을 분석할까요? 종목명이나 6자리 코드를 알려줘요. 예: SK스퀘어 분석, 005930 리포트"
    symbol, name = stock
    return _run_python(script, "--print", "--symbol", symbol, "--name", name, timeout=90)


def add_watchlist(symbol: str, name: str) -> str:
    """소미 감시 목록에 종목 추가"""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "watchlist_manager.py"
    return _run_python(script, "add", "--symbol", symbol, "--name", name, timeout=30)


def remove_watchlist(symbol: str) -> str:
    """소미 감시 목록에서 종목 제거"""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "watchlist_manager.py"
    return _run_python(script, "remove", "--symbol", symbol, timeout=30)


def list_watchlist() -> str:
    """소미 감시 종목 목록 조회"""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "watchlist_manager.py"
    return _run_python(script, "list", timeout=30)


def search_stock(query: str) -> str:
    """종목명으로 종목코드 검색"""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "stock_search.py"
    return _run_python(script, query, timeout=30)


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "get_agent_status",
            "description": "현재 실행 중인 AI 팀 에이전트 현황 조회",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "list_calendar",
            "description": "일정 및 스케줄 조회",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "dispatch_to_somi",
            "description": "소미 분석가에게 주식 종목 분석 요청",
            "parameters": {
                "type": "object",
                "properties": {"text": {"type": "string", "description": "분석 요청 메시지"}},
                "required": ["text"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "dispatch_screener",
            "description": "유망 종목 발굴. 사용자가 특정 종목을 지정하지 않고 '유망종목/추천종목/뭐 살까/발굴' 등을 물으면 호출. 거래량 상위 종목을 소미 점수로 채점해 상위 종목을 추천",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "dispatch_to_yewon",
            "description": "예원 CEO에게 작업 요청",
            "parameters": {
                "type": "object",
                "properties": {"text": {"type": "string", "description": "작업 요청 메시지"}},
                "required": ["text"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_weather",
            "description": "특정 도시의 현재 날씨 또는 내일/모레 예보 조회. 사용자가 날씨/기온을 물으면 호출",
            "parameters": {
                "type": "object",
                "properties": {
                    "city": {"type": "string", "description": "도시명 (예: 서울, 부산)"},
                    "day": {"type": "integer", "description": "0=오늘(현재), 1=내일, 2=모레"},
                },
                "required": [],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "search_stock",
            "description": "종목명으로 종목코드 검색. 사용자가 '삼전 감시' 같이 종목명만 말하면 먼저 이 함수로 검색",
            "parameters": {
                "type": "object",
                "properties": {"query": {"type": "string", "description": "검색어 (종목명)"}},
                "required": ["query"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "add_watchlist",
            "description": "소미 감시 목록에 종목 추가 (search_stock으로 종목코드 확인 후 사용)",
            "parameters": {
                "type": "object",
                "properties": {
                    "symbol": {"type": "string", "description": "종목코드"},
                    "name": {"type": "string", "description": "종목명"},
                },
                "required": ["symbol", "name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "remove_watchlist",
            "description": "소미 감시 목록에서 종목 제거",
            "parameters": {
                "type": "object",
                "properties": {"symbol": {"type": "string", "description": "종목코드"}},
                "required": ["symbol"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "list_watchlist",
            "description": "소미가 감시 중인 종목 목록 조회",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
]


AVAILABLE_FUNCTIONS = {
    "get_agent_status": get_agent_status,
    "list_calendar": list_calendar,
    "dispatch_to_somi": dispatch_to_somi,
    "dispatch_screener": dispatch_screener,
    "dispatch_to_yewon": dispatch_to_yewon,
    "add_watchlist": add_watchlist,
    "remove_watchlist": remove_watchlist,
    "list_watchlist": list_watchlist,
    "search_stock": search_stock,
    "get_weather": get_weather,
}


def handle_with_gpt(text: str) -> str:
    """GPT-4o-mini function calling으로 사용자 메시지 처리"""
    try:
        messages = [
            {
                "role": "system",
                "content": """너는 '영숙'이야. 사장님(준호)의 비서이자 친구야.
말투는 **친한 친구처럼 편하고 따뜻하게** — 반말 섞어 친근하게, 짧고 자연스럽게 답해.
딱딱한 존댓말·기계적인 안내체 금지. 이모지도 가끔 자연스럽게.
단, 사실/숫자(주가·일정·현황 등)는 정확하게 전한다.

**종목 감시 추가 절차**:
1. 사용자가 "삼전 감시" 같이 종목명만 말하면:
   → search_stock("삼전") 호출하여 종목코드 확인
   → 결과에서 종목코드/종목명 파싱
   → add_watchlist(종목코드, 종목명) 호출

2. 감시 중지/목록: remove_watchlist(), list_watchlist()

**Tool 사용 규칙**:
- 에이전트 현황 → get_agent_status()
- 일정 → list_calendar()
- 종목 검색 → search_stock()
- 감시 추가 → add_watchlist()
- 감시 제거 → remove_watchlist()
- 감시 목록 → list_watchlist()
- 종목 분석 → dispatch_to_somi()
- 일반 질문은 직접 답변""",
            },
            {"role": "user", "content": text},
        ]

        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=messages,
            tools=TOOLS,
            tool_choice="auto",
        )

        message = response.choices[0].message

        if message.tool_calls:
            for tool_call in message.tool_calls:
                function_name = tool_call.function.name
                function_args = json.loads(tool_call.function.arguments)
                function_to_call = AVAILABLE_FUNCTIONS.get(function_name)

                if function_to_call:
                    log(f"Calling {function_name} with {function_args}")
                    function_response = function_to_call(**function_args)
                    messages.append(message)
                    messages.append(
                        {
                            "role": "tool",
                            "tool_call_id": tool_call.id,
                            "content": function_response,
                        }
                    )

            second_response = client.chat.completions.create(model="gpt-4o-mini", messages=messages)
            return second_response.choices[0].message.content or "완료했습니다."

        return message.content or "알 수 없는 응답입니다."

    except Exception as exc:
        log(f"GPT error: {exc}")
        return f"요청 처리 중 오류가 발생했습니다: {exc}"


def _classify_intent(text: str, has_order: bool, has_signals: bool) -> dict | None:
    """LLM이 메시지의 '의미'를 분석해 운영 명령 의도를 분류. 정해진 단어가 아니어도 같은 뜻이면 매칭.
    반환: {"intent":..., "index":n|None} 또는 None(해당 없음/casual)."""
    from _shared import llm
    opts = ["mode_live=실거래(실제 돈)로 전환", "mode_paper=모의(가상)로 전환",
            "mode_status=현재 거래 모드 조회", "none=거래 운영 명령이 아님(일반 대화/질문)"]
    if has_order:
        opts = ["order_confirm=대기 중인 주문 체결 승인", "order_cancel=대기 주문 취소"] + opts
    if has_signals:
        opts = ["signal_approve=푸시된 매수신호 승인(index=종목 번호, 없으면 1)",
                "pass=신호 보류/넘김"] + opts
    prompt = (
        "주식 봇 사용자의 메시지 의도를 분류하세요. 미리 정한 단어가 아니라 '의미'로 판단합니다.\n"
        "예) '이제 진짜로 사자'·'실전으로 돌려'→mode_live, '가상으로만'·'연습모드'→mode_paper, "
        "'지금 뭘로 돼있어?'→mode_status, '그래 사', '오케이 2번'→signal_approve, '넘겨'→pass.\n"
        f"가능한 의도: {'; '.join(opts)}\n"
        f'메시지: "{text}"\n'
        '애매하거나 단순 대화면 none. JSON만: {"intent":"...","index":번호 또는 null}'
    )
    raw = None
    for fn in (lambda: llm.gpt(prompt, max_tokens=60, temperature=0),
               lambda: llm.gemini(prompt, max_tokens=60, temperature=0),
               lambda: llm.ollama(prompt, max_tokens=60, temperature=0)):
        try:
            raw = fn()
            if raw:
                break
        except Exception:
            continue
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


def _dispatch_intent(intent: dict, has_order: bool, has_signals: bool) -> str | None:
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


def handle_message(text: str) -> str:
    """Telegram 자연어 메시지를 처리합니다. 운영 명령은 '의미'로 인식합니다(정해진 단어 불필요)."""
    text = (text or "").strip()
    if not text:
        return "메시지가 비어 있어요."

    has_order = bool(_PENDING_ORDER.get("order"))
    has_signals = bool(_signals_load())

    # 1) 빠른 정확매칭 (LLM 없이 즉답)
    if has_order:
        n = _normalize_text(text)
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
    if _is_pass(text):
        _signals_clear()
        return "넘어갈게요. 계속 감시하겠습니다."

    # 2) 의미 기반 분류 — 정확매칭이 안 됐고, 운영 맥락이거나 짧은 지시일 때만 LLM 호출
    if has_order or has_signals or len(text) <= 30:
        intent = _classify_intent(text, has_order, has_signals)
        if intent:
            resolved = _dispatch_intent(intent, has_order, has_signals)
            if resolved is not None:
                return resolved

    # 3) 위에서 안 잡힌 대기 주문은 만료
    if has_order:
        _PENDING_ORDER.pop("order", None)

    # 소미 매수 제안 승인 / 매도
    somi_order = _parse_somi_approve(text) or _parse_somi_sell(text)
    if somi_order:
        _PENDING_ORDER["order"] = somi_order
        return _order_confirm_prompt(somi_order)

    # 수동 매수/매도 지시 → 확인 단계
    order = _parse_order(text)
    if order:
        _PENDING_ORDER["order"] = order
        return _order_confirm_prompt(order)

    if _is_balance_request(text):
        return _tool_balance()

    # 신규 에이전트 생성 승인/거절 (예원 오케스트레이터 제안에 대한 응답)
    _n = _normalize_text(text)
    if _n in ("에이전트승인", "새에이전트승인", "에이전트생성승인"):
        return _agent_factory_action("approve_pending")
    if _n in ("에이전트거절", "에이전트취소", "새에이전트거절"):
        return _agent_factory_action("reject_pending")

    if _is_trading_status_request(text):
        return _tool_get_agent_status()  # 거래/에이전트 현황 통합 — 한 번만

    if _is_stock_price_request(text):
        return _tool_get_stock_price(text)

    if _is_screener_request(text):
        return dispatch_screener(text)

    if _is_stock_analysis_request(text):
        return dispatch_to_somi(text)

    if _is_weather_request(text):
        return get_weather(_parse_weather_city(text), _parse_weather_day(text))

    if _is_search_request(text):
        return web_search(text)

    gpt_response = handle_with_gpt(text)
    if not gpt_response.startswith("요청 처리 중 오류가 발생했습니다:"):
        return gpt_response
    return _call_llm(text, PSYCHOLOGY_SYSTEM)


async def _start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text("영숙 비서 대기 중입니다.")


async def _status(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text(get_agent_status())


async def _message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    text = update.effective_message.text or ""
    log(f"Message: {text[:100]}")
    try:
        response = await asyncio.to_thread(handle_message, text)
    except Exception as exc:
        log(f"Handler error: {exc}")
        response = f"요청 처리 중 오류가 발생했습니다: {exc}"

    # 4096자 제한으로 분할 전송
    chunks = [response[i : i + 4000] for i in range(0, len(response), 4000)]
    for chunk in chunks:
        await update.effective_message.reply_text(chunk)


def main() -> int:
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    if not token:
        print("TELEGRAM_BOT_TOKEN is not configured.")
        return 1

    app = Application.builder().token(token).build()
    app.add_handler(CommandHandler("start", _start))
    app.add_handler(CommandHandler("status", _status))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, _message))

    log("Youngsuk Telegram bot started (polling mode)")
    app.run_polling(allowed_updates=["message"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
