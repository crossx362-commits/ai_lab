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
import urllib.error
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


def agent_status() -> str:
    try:
        import agent_controller

        return agent_controller.get_agent_status()
    except Exception as exc:
        return f"에이전트 상태 확인 실패: {exc}"


def trading_status() -> str:
    lines = ["거래팀 상태"]
    try:
        import agent_controller

        for name in ["시그널", "데이브", "레오"]:
            lines.append(agent_controller.get_agent_status(name))
    except Exception as exc:
        lines.append(f"상태 확인 실패: {exc}")
    signal_file = PROJECT_ROOT / "reports" / "research" / "market_signal.json"
    legacy_file = PROJECT_ROOT / "reports" / "research" / "market_pulse.json"
    report_file = signal_file if signal_file.exists() else legacy_file
    if report_file.exists():
        age = int(time.time() - report_file.stat().st_mtime)
        lines.append(f"시그널 리포트: {age}초 전 갱신")
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
        from _shared.ollama_client import chat

        answer = chat(
            [{"role": "system", "content": "너는 영숙 비서다. 짧고 사실적으로 한국어로 답한다."}, {"role": "user", "content": text}],
            task="general",
        )
        if answer:
            return str(answer).strip()
    except Exception:
        pass
    return "확인했습니다. 지금은 상태/일정/거래팀/에이전트 시작·종료 명령을 처리할 수 있습니다."


def handle_message(text: str) -> str:
    clean = normalize(text)
    log(f"message: {text[:120]}")

    if clean in {"/start", "/help", "help", "도움말"}:
        return "영숙 준비됨. 예: 현황, 거래 현황, 일정, 데이브 상태, 레오 시작, 시그널 시작"

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
