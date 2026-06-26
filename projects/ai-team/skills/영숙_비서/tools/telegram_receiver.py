#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram receiver — Flask webhook with GPT-4o-mini function calling."""

from __future__ import annotations

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
    from flask import Flask, request
    from openai import OpenAI
except ImportError as exc:
    print(f"Dependencies not installed: {exc}")
    print("Run: pip install flask openai")
    sys.exit(1)


app = Flask(__name__)
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


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "get_agent_status",
            "description": "현재 실행 중인 AI 팀 에이전트 현황 조회 (영숙, 소미, 예원 등)",
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
            "description": "소미 분석가에게 주식 종목 분석 요청 (우리기술 종목)",
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
            "description": "예원 CEO에게 작업 요청 (하네스 관리, 스킬 정리 등)",
            "parameters": {
                "type": "object",
                "properties": {"text": {"type": "string", "description": "작업 요청 메시지"}},
                "required": ["text"],
            },
        },
    },
]


AVAILABLE_FUNCTIONS = {
    "get_agent_status": get_agent_status,
    "list_calendar": list_calendar,
    "dispatch_to_somi": dispatch_to_somi,
    "dispatch_to_yewon": dispatch_to_yewon,
}


def handle_with_gpt(text: str) -> str:
    """GPT-4o-mini function calling으로 사용자 메시지 처리"""
    try:
        messages = [
            {
                "role": "system",
                "content": """당신은 영숙 비서입니다. 사용자의 모든 질문에 친절하고 정확하게 답변합니다.

**역할**:
- AI 팀 에이전트 현황 조회 (영숙, 소미, 예원, 데이브, 레오 등)
- 일정 및 스케줄 관리
- 주식 종목 분석 (우리기술 종목)
- 작업 요청 및 지시사항 전달
- 일반적인 질문에 대한 답변 (장마감 시간, 시장 정보 등)

**중요**:
- 사용자가 에이전트 현황, 상태, 다들 뭐해? 등을 물으면 get_agent_status() 사용
- 일정, 스케줄, 캘린더 관련 질문은 list_calendar() 사용
- 종목 분석, 주식, 투자 관련 질문은 dispatch_to_somi() 사용
- 하네스, 스킬, 작업 요청은 dispatch_to_yewon() 사용
- **그 외 모든 질문은 당신이 직접 답변하세요** (tool 사용 불필요)

예시:
- "장마감 몇시야?" → 한국 주식시장은 오후 3시 30분에 마감됩니다.
- "코스피 거래시간은?" → 오전 9시 ~ 오후 3시 30분입니다.
- "현황 보고해줘" → get_agent_status() 호출""",
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


def send_telegram_message(chat_id: str, text: str) -> None:
    """텔레그램 메시지 전송"""
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    if not token:
        return

    import urllib.request

    chunks = [text[i : i + 4000] for i in range(0, len(text), 4000)]
    for chunk in chunks:
        payload = json.dumps({"chat_id": chat_id, "text": chunk, "parse_mode": "HTML"}).encode()
        req = urllib.request.Request(
            f"https://api.telegram.org/bot{token}/sendMessage",
            data=payload,
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=10):
                pass
        except Exception as exc:
            log(f"send_telegram_message failed: {exc}")


@app.route("/webhook", methods=["POST"])
def webhook():
    """Telegram webhook endpoint"""
    try:
        data = request.get_json()
        log(f"Webhook received: {json.dumps(data, ensure_ascii=False)[:200]}")

        if "message" not in data:
            return "ok"

        message = data["message"]
        chat_id = str(message["chat"]["id"])
        text = message.get("text", "").strip()

        if not text:
            return "ok"

        log(f"Message from {chat_id}: {text[:100]}")

        response = handle_with_gpt(text)
        send_telegram_message(chat_id, response)

        return "ok"

    except Exception as exc:
        log(f"Webhook error: {exc}")
        return "error", 500


@app.route("/health", methods=["GET"])
def health():
    return "ok"


if __name__ == "__main__":
    port = int(os.getenv("YOUNGSUK_PORT", "5000"))
    log(f"Starting Youngsuk webhook server on port {port}")
    app.run(host="0.0.0.0", port=port, debug=False)
