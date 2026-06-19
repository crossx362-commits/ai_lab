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


def dispatch_command(text: str) -> str:
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools"))
        import yewon_dispatcher

        result = yewon_dispatcher.dispatch_and_execute(text)
        return result or "CEO가 처리할 작업을 찾지 못했습니다."
    except Exception as exc:
        return f"작업 실행 실패: {exc}"


def llm_answer(text: str) -> str:
    try:
        from _shared.llm import text as llm_text
        answer = llm_text(text, system=PSYCHOLOGY_SYSTEM, max_tokens=400, temperature=0.7, lm_first=True)
        if answer:
            return str(answer).strip()
    except Exception as exc:
        log(f"llm_answer failed: {exc}")
    return "잠깐만요, 지금은 답하기 어려워요. 거래 현황, 일정, 에이전트 상태는 물어보실 수 있어요!"


def handle_message(text: str) -> str:
    clean = normalize(text)
    log(f"message: {text[:120]}")

    if clean in {"/start", "/help", "help", "도움말"}:
        return "영숙 준비됨. 예: 현황, 거래 현황, 일정, 데이브 상태, 레오 시작, 심리학 최신 자료 검색해봐"

    if is_search_request(clean):
        return web_search(text)

    if any(k in clean for k in ["현황", "상태", "다들뭐해", "에이전트상태", "agentstatus"]):
        if any(k in clean for k in ["투자", "거래", "주식", "코인", "매매", "데이브", "레오", "시그널", "펄스"]):
            return trading_status()
        return agent_status()

    if any(k in clean for k in ["투자현황", "거래현황", "주식현황", "코인현황", "매매현황", "트레이딩현황"]):
        return trading_status()

    if any(k in clean for k in ["일정", "스케줄", "calendar", "schedule"]):
        return schedule_report()

    agent_words = ["시그널", "펄스", "데이브", "레오", "영숙", "signal", "pulse", "dave", "leo", "youngsuk"]
    action_words = ["시작", "켜", "켜줘", "종료", "꺼", "꺼줘", "재시작", "상태", "start", "stop", "restart", "status"]
    if any(k in clean for k in agent_words) and any(k in clean for k in action_words):
        try:
            import agent_controller

            return agent_controller.handle_agent_command(text)
        except Exception as exc:
            return f"에이전트 명령 실패: {exc}"

    dispatch_words = ["실행해", "구동", "작업시켜", "처리해", "해줘"]
    if any(k in clean for k in dispatch_words):
        return dispatch_command(text)

    return llm_answer(text)


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
