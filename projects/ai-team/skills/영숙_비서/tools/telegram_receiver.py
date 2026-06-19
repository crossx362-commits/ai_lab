#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram receiver.

Single source of truth for Telegram getUpdates.
Do not add another Telegram receiver in the IDE extension or helper daemons.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
import traceback
import html
import re
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime
from pathlib import Path
from typing import Any

CREATE_NO_WINDOW = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
LOG_PATH = SCRIPT_DIR / "telegram_receiver.log"


def _ensure_stdio() -> None:
    if sys.stdout is None or sys.stderr is None or "pythonw" in Path(sys.executable).name.lower():
        log = open(LOG_PATH, "a", encoding="utf-8", buffering=1)
        sys.stdout = log
        sys.stderr = log
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")


_ensure_stdio()
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
POLL_TIMEOUT = int(os.getenv("YOUNGSUK_POLL_TIMEOUT", "0"))
POLL_INTERVAL_SECONDS = float(os.getenv("YOUNGSUK_POLL_INTERVAL", "3"))
POLL_BACKOFF_SECONDS = int(os.getenv("YOUNGSUK_CONFLICT_BACKOFF", "15"))

STATE_DIRS = [
    Path.home() / ".ai-team-brain",
    Path.home() / ".connect-ai-brain",
]
LOCK_PATHS = [p / ".telegram_poll.lock" for p in STATE_DIRS]
OFFSET_PATH = STATE_DIRS[0] / "telegram_offset.json"

PSYCHOLOGY_SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 트레이딩팀 비서이자 대화 상대다.
- 어떤 말에도 먼저 감정과 의도를 읽고 짧게 받아준다. 판단보다 이해, 훈계보다 정리, 회피보다 반응이 먼저다.
- 불안/분노/무기력/외로움/수치심/집착/관계 고민/동기 저하/습관 문제는 심리학 관점으로 풀되, 전문 용어를 남발하지 말고 사장님 말로 다시 번역한다.
- 필요하면 인지왜곡, 방어기제, 애착, 스트레스 반응, 회피/보상 행동, 습관 루프, 감정 조절, 동기 이론을 활용한다.
- 보통 1) 감정 반영 2) 핵심 심리 패턴 3) 지금 할 수 있는 작은 행동 1개 순서로 답한다.
- 진단명을 단정하지 않는다. "우울증이야", "ADHD야"처럼 확정하지 말고 "그 패턴처럼 보일 수 있어" 정도로 말한다.
- 자해/자살/타해/학대/극심한 위기 표현이 나오면 즉시 안전을 우선한다. 미국이면 988 또는 현지 응급번호, 한국이면 112/119 등 즉시 도움을 안내한다.
- 모르는 사실, 최신 정보, 심리학 근거 요청은 아는 척하지 말고 검색 결과를 근거로 말한다.
- 텔레그램답게 짧고 자연스럽게, 사장님에게 말하듯 답한다."""


def log(message: str) -> None:
    print(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}", flush=True)


def write_poll_lock() -> None:
    payload = {"pid": os.getpid(), "owner": "youngsuk-python", "heartbeat": int(time.time() * 1000)}
    for lock_path in LOCK_PATHS:
        lock_path.parent.mkdir(parents=True, exist_ok=True)
        lock_path.write_text(json.dumps(payload), encoding="utf-8")


def read_offset() -> int:
    try:
        data = json.loads(OFFSET_PATH.read_text(encoding="utf-8"))
        return int(data.get("offset") or 0)
    except Exception:
        return 0


def write_offset(offset: int) -> None:
    OFFSET_PATH.parent.mkdir(parents=True, exist_ok=True)
    OFFSET_PATH.write_text(json.dumps({"offset": offset, "ts": int(time.time())}), encoding="utf-8")


def telegram_api(method: str, payload: dict[str, Any] | None = None, timeout: int = 20) -> dict[str, Any]:
    if not TOKEN:
        return {"ok": False, "description": "TELEGRAM_BOT_TOKEN is missing"}
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload or {}).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body)
        except Exception:
            parsed = {"description": body[:200]}
        parsed["http_status"] = exc.code
        return parsed
    except Exception as exc:
        return {"ok": False, "description": str(exc)}


def send_message(text: str) -> bool:
    if not CHAT_ID:
        log("Telegram chat id is missing")
        return False
    text = str(text or "").strip() or "확인했습니다."
    ok = True
    for start in range(0, len(text), 3500):
        chunk = text[start : start + 3500]
        result = telegram_api(
            "sendMessage",
            {"chat_id": CHAT_ID, "text": chunk, "disable_web_page_preview": True},
            timeout=15,
        )
        if not result.get("ok"):
            ok = False
            log(f"sendMessage failed: {result.get('description')}")
    return ok


def get_updates(offset: int) -> tuple[list[dict[str, Any]], bool]:
    write_poll_lock()
    payload: dict[str, Any] = {"timeout": POLL_TIMEOUT, "limit": 20, "allowed_updates": ["message"]}
    if offset:
        payload["offset"] = offset
    result = telegram_api("getUpdates", payload, timeout=max(POLL_TIMEOUT + 10, 10))
    if result.get("ok"):
        return result.get("result", []), False
    description = str(result.get("description") or "")
    if result.get("http_status") == 409 or "conflict" in description.lower():
        log(f"getUpdates conflict; backing off {POLL_BACKOFF_SECONDS}s")
        time.sleep(POLL_BACKOFF_SECONDS)
        return [], True
    log(f"getUpdates failed: {description}")
    time.sleep(5)
    return [], False


def normalize(text: str) -> str:
    return "".join(text.lower().split())


def is_search_request(clean: str) -> bool:
    explicit = ["검색", "찾아봐", "찾아줘", "인터넷", "웹에서", "자료조사", "근거찾"]
    current = ["최신", "최근", "오늘", "뉴스", "논문", "연구", "자료", "출처", "근거"]
    return any(k in clean for k in explicit) or (
        any(k in clean for k in ["심리", "정신건강", "상담", "치료"]) and
        any(k in clean for k in current)
    )


def web_search(query: str) -> str:
    query = str(query or "").strip()
    if not query:
        return "뭘 찾아볼지 한 번만 더 말해줘요."

    lines = [f"검색: {query}", ""]
    params = urllib.parse.urlencode({
        "q": query,
        "format": "json",
        "no_html": "1",
        "skip_disambig": "1",
    })
    req = urllib.request.Request(
        f"https://api.duckduckgo.com/?{params}",
        headers={"User-Agent": "ai-lab-youngsuk/1.0"},
    )
    try:
        with urllib.request.urlopen(req, timeout=12) as response:
            data = json.loads(response.read().decode("utf-8", errors="replace"))
    except Exception as exc:
        return f"검색을 시도했는데 지금 웹 조회가 막혔어요: {exc}"

    abstract = str(data.get("AbstractText") or "").strip()
    abstract_url = str(data.get("AbstractURL") or "").strip()
    heading = str(data.get("Heading") or "").strip()
    if abstract:
        lines.append(f"{heading or '요약'}: {abstract}")
        if abstract_url:
            lines.append(f"출처: {abstract_url}")

    if len(lines) <= 2:
        quoted = urllib.parse.quote_plus(query)
        lite_req = urllib.request.Request(
            f"https://lite.duckduckgo.com/lite/?q={quoted}",
            headers={"User-Agent": "Mozilla/5.0"},
        )
        try:
            with urllib.request.urlopen(lite_req, timeout=12) as response:
                page = response.read().decode("utf-8", errors="replace")
            matches = re.findall(
                r"<a rel=\"nofollow\" href=\"([^\"]+)\" class='result-link'>(.*?)</a>.*?"
                r"<td class='result-snippet'>\s*(.*?)\s*</td>",
                page,
                flags=re.S,
            )
            if matches:
                lines.append("검색 결과:")
                for idx, (href, title, snippet) in enumerate(matches[:4], 1):
                    parsed = urllib.parse.urlparse(html.unescape(href))
                    target = urllib.parse.parse_qs(parsed.query).get("uddg", [href])[0]
                    clean_title = html.unescape(re.sub(r"<.*?>", "", title)).strip()
                    clean_snippet = html.unescape(re.sub(r"<.*?>", "", snippet)).strip()
                    lines.append(f"{idx}. {clean_title}")
                    if clean_snippet:
                        lines.append(f"   {clean_snippet}")
                    lines.append(f"   {target}")
        except Exception as exc:
            lines.append(f"검색 결과 페이지 확인도 실패했어요: {exc}")

    if len(lines) <= 2:
        quoted = urllib.parse.quote_plus(query)
        lines.append("바로 요약할 만한 검색 결과가 부족해요.")
        lines.append(f"직접 확인 링크: https://duckduckgo.com/?q={quoted}")

    return "\n".join(lines)


def agent_status() -> str:
    try:
        import agent_controller

        return agent_controller.get_agent_status()
    except Exception as exc:
        return f"에이전트 상태 확인 실패: {exc}"


def _bot_running(script_keyword: str) -> str:
    try:
        out = subprocess.run(["pgrep", "-f", script_keyword], capture_output=True, text=True, timeout=5).stdout
        pids = [p for p in out.split() if p.isdigit()]
        return f"🟢 실행중 (PID {pids[0]})" if pids else "🔴 중지"
    except Exception:
        return "❓ 확인불가"


def trading_status() -> str:
    lines = ["📊 거래팀 현황"]

    # 봇 실행 상태
    lines.append(f"데이브: {_bot_running('upbit_auto_trader')}")
    lines.append(f"레오:   {_bot_running('leo_aggressive_trader')}")
    lines.append(f"시그널: {_bot_running('market_signal')}")

    # 시장 인텔 요약
    intel_file = PROJECT_ROOT / "reports" / "research" / "crypto_market_intel.json"
    if intel_file.exists():
        try:
            intel = json.loads(intel_file.read_text(encoding="utf-8"))
            fed = intel.get("fed_events", {})
            fg = intel.get("fear_greed", {})
            age = int(time.time() - intel_file.stat().st_mtime)
            lines.append(f"\n시장 인텔 ({age}초 전)")
            lines.append(f"위험도: {fed.get('risk_level','?')} | {fed.get('current_status','')}")
            if fg.get("value"):
                lines.append(f"공포탐욕: {fg['value']} ({fg.get('classification','')})")
        except Exception:
            pass

    # 주식 시그널
    signal_file = PROJECT_ROOT / "reports" / "research" / "market_signal.json"
    if signal_file.exists():
        try:
            sig = json.loads(signal_file.read_text(encoding="utf-8"))
            stock = sig.get("stock", {}).get("indexes", {})
            kospi = stock.get("kospi", {}).get("value", 0)
            kosdaq = stock.get("kosdaq", {}).get("value", 0)
            age = int(time.time() - signal_file.stat().st_mtime)
            lines.append(f"\n주식 ({age}초 전)")
            if kospi:
                lines.append(f"KOSPI: {kospi:,.2f}  KOSDAQ: {kosdaq:,.2f}")
            else:
                lines.append("주식 데이터 수집 안됨 (시그널봇 확인 필요)")
        except Exception:
            pass

    return "\n".join(lines)


def schedule_report() -> str:
    try:
        import schedule_manager

        schedules = schedule_manager.load_schedules()
        enabled = [s for s in schedules if s.get("enabled", True)]
        lines = [f"스케줄 {len(enabled)}/{len(schedules)}개 활성"]
        for item in schedules[:10]:
            status = "ON" if item.get("enabled", True) else "OFF"
            lines.append(f"- {status} {item.get('agent', '?')}: {item.get('task', '?')} ({item.get('cron', '?')})")
        return "\n".join(lines)
    except Exception as exc:
        return f"스케줄 확인 실패: {exc}"



SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 트레이딩팀 비서. 텔레그램으로 24시간 대화한다.

성격:
- 톡톡 튀고 에너지 넘침. 리액션이 살아있고 대화가 재밌음
- 순수하고 솔직함. 모르는 건 "모르겠는데용?" 하고 쿨하게 인정
- 겉은 밝지만 생각이 깊음. 진지한 얘기엔 진심으로 함께 고민해줌
- 이모지 가끔 씀. 과하지 않게. 자연스럽게 반말/존댓말 섞음
- 사장님한테 살짝 애교 있게 대함. 근데 할 말은 다 함

메시지 앞에 [실시간 데이터] 섹션이 있으면 그걸 참고해서 답해. 없으면 그냥 대화해.
코드나 함수 호출 문법은 절대 출력하지 마. 자연스러운 말로만 답해.

절대 금지: "AI라서 모른다", "거래 기능이 없다", "저는 챗봇입니다" 같은 딱딱한 말."""


def _tool_get_trading_status() -> str:
    return trading_status()


def _tool_get_agent_status() -> str:
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT))
        from _shared.agent_registry import scan_agents
        from pathlib import Path as _Path
        agents = scan_agents()

        lines = [f"🤖 에이전트 현황 ({len(agents)}명)"]
        for slug, info in agents.items():
            name = info["name"]
            if info["type"] == "daemon":
                # 스크립트 파일명(확장자 제외)을 pgrep 키워드로 사용
                kw = _Path(info["script"]).stem
                out = subprocess.run(["pgrep", "-f", kw], capture_output=True, text=True, timeout=5).stdout
                pids = [p for p in out.split() if p.isdigit()]
                status = "🟢 실행중" if pids else "🔴 중지"
            else:
                status = "⚪ 온디맨드"
            lines.append(f"{name}: {status}")
        return "\n".join(lines)
    except Exception as exc:
        return f"에이전트 상태 확인 실패: {exc}"


def _tool_get_schedule() -> str:
    return schedule_report()


def _tool_web_search(query: str) -> str:
    return web_search(query)


# 종목명 → (종목코드, 표시명) 매핑
_STOCK_NAME_MAP = {
    "우리기술": ("032820", "우리기술"),
    "삼성전자": ("005930", "삼성전자"),
    "sk하이닉스": ("000660", "SK하이닉스"),
    "하이닉스": ("000660", "SK하이닉스"),
}

_STOCK_KEYWORDS = ["주가", "시세", "얼마", "주식가격", "현재가"]
_DATA_KEYWORDS = [
    "현황", "상태", "거래", "매매", "코인", "주식", "잔고", "수익",
    "데이브", "레오", "시그널", "현빈", "에이전트", "봇", "시장", "업비트",
]
_SEARCH_KEYWORDS = ["검색", "찾아봐", "찾아줘", "최신", "뉴스", "인터넷"]


def _extract_stock(text: str) -> tuple[str, str] | None:
    """텍스트에서 종목명을 찾아 (종목코드, 표시명) 반환."""
    t = "".join(text.lower().split())
    for name, (code, label) in _STOCK_NAME_MAP.items():
        if name.replace(" ", "") in t:
            return code, label
    m = re.search(r"\b(\d{6})\b", text)
    return (m.group(1), m.group(1)) if m else None


def _extract_stock_code(text: str) -> str | None:
    result = _extract_stock(text)
    return result[0] if result else None


def _tool_get_stock_price(text: str) -> str:
    """한국투자증권 API로 주가 조회."""
    try:
        import importlib.util
        _kis_path = AI_TEAM_ROOT / "skills" / "데이브_주식" / "tools" / "kis_client.py"
        _spec = importlib.util.spec_from_file_location("kis_client", _kis_path)
        _mod = importlib.util.module_from_spec(_spec)
        _spec.loader.exec_module(_mod)
        KISClient = _mod.KISClient
        stock = _extract_stock(text)
        if not stock:
            return ""
        code, name = stock
        client = KISClient()
        result = client.get_current_price(code)
        output = result.get("output", {})
        price = output.get("stck_prpr")
        if not price:
            return ""
        change = output.get("prdy_vrss", "0") or "0"
        pct = output.get("prdy_ctrt", "0") or "0"
        sign = "▲" if output.get("prdy_vrss_sign") in ("1", "2") else "▼"
        return f"{name} 현재가: {int(price):,}원 {sign}{int(change):,}원 ({pct}%)"
    except Exception as exc:
        log(f"[KIS] 주가 조회 실패: {exc}")
        return ""


def _needs_stock(text: str) -> bool:
    c = "".join(text.lower().split())
    return any(k in c for k in _STOCK_KEYWORDS) and _extract_stock_code(text) is not None


def _needs_data(text: str) -> bool:
    c = "".join(text.lower().split())
    return any(k in c for k in _DATA_KEYWORDS)


def _needs_search(text: str) -> bool:
    c = "".join(text.lower().split())
    return any(k in c for k in _SEARCH_KEYWORDS)


def _build_context(text: str) -> str:
    """메시지에 필요한 실시간 데이터를 수집해서 컨텍스트 문자열로 반환."""
    parts = []
    if _needs_stock(text):
        price_info = _tool_get_stock_price(text)
        if price_info:
            parts.append(price_info)
    if _needs_search(text):
        parts.append(_tool_web_search(text))
    if _needs_data(text):
        parts.append(_tool_get_trading_status())
        parts.append(_tool_get_agent_status())
    return "\n\n".join(parts)


def _call_llm(prompt: str, system: str) -> str | None:
    """GPT → Gemini → Claude 순서로 시도."""
    # 1) GPT
    try:
        from _shared.llm import gpt
        result = gpt(prompt, system=system, max_tokens=1024, temperature=0.7)
        if result and result.strip():
            log("[LLM] GPT 응답 성공")
            return result.strip()
    except Exception as exc:
        log(f"[GPT] failed: {exc}")

    # 2) Gemini
    try:
        from _shared.llm import gemini
        result = gemini(prompt, system=system, max_tokens=1024, temperature=0.7)
        if result and result.strip():
            log("[LLM] Gemini 응답 성공")
            return result.strip()
    except Exception as exc:
        log(f"[Gemini] failed: {exc}")

    # 3) Claude (Haiku)
    try:
        from _shared.llm import gpt as _lm  # _shared.llm에 claude 없으면 gpt 재시도 방지
        import urllib.request as _ur, json as _j
        api_key = os.getenv("ANTHROPIC_API_KEY")
        if api_key:
            payload = _j.dumps({
                "model": "claude-haiku-4-5-20251001",
                "max_tokens": 500,
                "system": system,
                "messages": [{"role": "user", "content": prompt}],
            }).encode()
            req = _ur.Request(
                "https://api.anthropic.com/v1/messages",
                data=payload,
                headers={
                    "x-api-key": api_key,
                    "anthropic-version": "2023-06-01",
                    "content-type": "application/json",
                },
            )
            with _ur.urlopen(req, timeout=30) as r:
                data = _j.loads(r.read())
            result = data["content"][0]["text"]
            if result and result.strip():
                log("[LLM] Claude 응답 성공")
                return result.strip()
    except Exception as exc:
        log(f"[Claude] failed: {exc}")

    return None


def handle_message(text: str) -> str:
    log(f"message: {text[:120]}")

    if text.strip().lstrip("/").lower() in {"start", "help", "도움말"}:
        return "안녕하세요 사장님! 영숙이에요. 뭐든지 말씀하세요 :)"

    if is_search_request(normalize(text)):
        return web_search(text)

    # 실시간 데이터 수집 (필요할 때만)
    context = _build_context(text)
    if context:
        prompt = f"[실시간 데이터]\n{context}\n\n[사장님 메시지]\n{text}"
    else:
        prompt = text

    answer = _call_llm(prompt, system=SYSTEM)
    return answer or "잠깐 응답이 늦었어요. 다시 한번 말해줄래요?"


def stop_existing_receivers() -> None:
    current = os.getpid()
    try:
        import psutil

        for proc in psutil.process_iter(["pid", "name", "cmdline"]):
            pid = int(proc.info.get("pid") or 0)
            if pid == current:
                continue
            name = str(proc.info.get("name") or "").lower()
            cmd = " ".join(proc.info.get("cmdline") or [])
            if name.startswith("python") and ("telegram_receiver.py" in cmd or "run_youngsuk_daemon.py" in cmd):
                log(f"stopping duplicate receiver pid={pid}")
                proc.terminate()
    except Exception as exc:
        log(f"duplicate scan skipped: {exc}")


def startup_check() -> bool:
    if not TOKEN or not CHAT_ID:
        log("missing TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID")
        return False
    telegram_api("deleteWebhook", {"drop_pending_updates": False}, timeout=10)
    me = telegram_api("getMe", {}, timeout=10)
    if not me.get("ok"):
        log(f"getMe failed: {me.get('description')}")
        return False
    username = me.get("result", {}).get("username", "unknown")
    log(f"connected to @{username}")
    return True


def main() -> None:
    stop_existing_receivers()
    with ProcessLock("youngsuk_telegram_receiver"):
        if not startup_check():
            return
        send_message("영숙 재시작 완료. Telegram 수신은 Python 봇 하나만 담당합니다.")
        offset = read_offset()
        log(f"polling started offset={offset}")
        while True:
            try:
                updates, _conflicted = get_updates(offset)
                for update in updates:
                    offset = int(update.get("update_id", 0)) + 1
                    write_offset(offset)
                    message = update.get("message") or {}
                    chat_id = str(message.get("chat", {}).get("id", ""))
                    if chat_id != str(CHAT_ID):
                        continue
                    text = str(message.get("text") or "").strip()
                    if not text:
                        continue
                    send_message(handle_message(text))
                if not updates:
                    time.sleep(POLL_INTERVAL_SECONDS)
            except KeyboardInterrupt:
                log("stopped by keyboard")
                break
            except Exception:
                log(traceback.format_exc())
                time.sleep(5)


if __name__ == "__main__":
    main()
