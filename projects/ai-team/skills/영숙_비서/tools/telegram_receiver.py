#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram bot — polling mode with GPT-4o-mini function calling."""

from __future__ import annotations

import asyncio
import json
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

try:
    from telegram import Update
    from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes
    from openai import OpenAI
except ImportError as exc:
    print(f"Dependencies not installed: {exc}")
    print("Run: pip install python-telegram-bot openai")
    sys.exit(1)


client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))


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
    """현재 AI 팀 에이전트 현황을 조회합니다."""
    return status_report()


def list_calendar() -> str:
    """일정 및 스케줄을 조회합니다."""
    script = HERE.with_name("schedule_manager.py")
    return _run_python(script, "list", timeout=30)


def dispatch_to_yewon(text: str) -> str:
    """예원 CEO에게 작업을 요청합니다."""
    script = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_dispatcher.py"
    return _run_python(script, text, timeout=90)


def dispatch_to_somi(text: str) -> str:
    """소미 분석가에게 종목 분석을 요청합니다."""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_kis_reporter.py"
    return _run_python(script, "--print", timeout=90)


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
    "dispatch_to_yewon": dispatch_to_yewon,
    "add_watchlist": add_watchlist,
    "remove_watchlist": remove_watchlist,
    "list_watchlist": list_watchlist,
    "search_stock": search_stock,
}


def handle_with_gpt(text: str) -> str:
    """GPT-4o-mini function calling으로 사용자 메시지 처리"""
    try:
        messages = [
            {
                "role": "system",
                "content": """당신은 영숙 비서입니다. 사용자의 모든 질문에 친절하고 정확하게 답변합니다.

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


async def _start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text("영숙 비서 대기 중입니다.")


async def _status(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text(get_agent_status())


async def _message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    text = update.effective_message.text or ""
    log(f"Message: {text[:100]}")
    try:
        response = await asyncio.to_thread(handle_with_gpt, text)
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
