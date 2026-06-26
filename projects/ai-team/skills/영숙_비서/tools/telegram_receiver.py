#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram receiver for the current ai-team roster."""

from __future__ import annotations

import asyncio
import os
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
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
from _shared.notify import status_report


load_env(str(PROJECT_ROOT))


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


def get_agent_status(_: str = "전체") -> str:
    return status_report()


def list_calendar() -> str:
    script = HERE.with_name("schedule_manager.py")
    return _run_python(script, "list", timeout=30)


def dispatch_to_yewon(text: str) -> str:
    script = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_dispatcher.py"
    return _run_python(script, text, timeout=90)


def dispatch_to_somi(text: str) -> str:
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "short_covering_analyzer.py"
    return _run_python(script, "--text", text, timeout=90)


def handle_text(text: str) -> str:
    stripped = text.strip()
    lowered = stripped.lower()
    if not stripped:
        return "말씀을 못 알아들었어요. 다시 보내 주세요."

    if any(word in stripped for word in ["현황", "상태", "다들", "에이전트"]):
        return get_agent_status()
    if any(word in stripped for word in ["일정", "스케줄", "캘린더"]):
        return list_calendar()
    if any(word in stripped for word in ["점수", "투자", "종목", "숏커버링", "시장", "매수", "매도"]):
        return dispatch_to_somi(stripped)
    if any(word in stripped for word in ["하네스", "스킬", "경로", "정리", "분배", "ceo"]) or "yewon" in lowered:
        return dispatch_to_yewon(stripped)

    return dispatch_to_yewon(stripped)


async def _send_chunks(reply, text: str) -> None:
    chunks = [text[i : i + 3900] for i in range(0, len(text), 3900)] or ["완료했습니다."]
    for chunk in chunks:
        await reply(chunk)


async def _start(update, context) -> None:
    await update.effective_message.reply_text("영숙 비서 대기 중입니다.")


async def _status(update, context) -> None:
    await update.effective_message.reply_text(get_agent_status())


async def _message(update, context) -> None:
    text = update.effective_message.text or ""
    log(f"message: {text[:120]}")
    try:
        response = await asyncio.to_thread(handle_text, text)
    except Exception as exc:
        log(f"handler error: {exc}")
        response = f"요청 처리 중 오류가 발생했습니다: {exc}"
    await _send_chunks(update.effective_message.reply_text, response)


def main() -> int:
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    if not token:
        print("TELEGRAM_BOT_TOKEN is not configured.")
        return 1

    try:
        from telegram.ext import Application, CommandHandler, MessageHandler, filters
    except Exception as exc:
        print(f"python-telegram-bot import failed: {exc}")
        return 1

    app = Application.builder().token(token).build()
    app.add_handler(CommandHandler("start", _start))
    app.add_handler(CommandHandler("status", _status))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, _message))

    log("Youngsuk Telegram receiver started.")
    app.run_polling(allowed_updates=["message"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
