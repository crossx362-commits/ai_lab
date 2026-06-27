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
    "영숙(비서). 규칙: 짧게 핵심만 답한다. "
    "운영/주식/검색 요청은 가능한 한 로컬 도구 결과를 우선한다."
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
    return _run_python(script, "list", timeout=30)


def dispatch_to_yewon(text: str) -> str:
    """예원 CEO에게 작업을 요청합니다."""
    script = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_dispatcher.py"
    return _run_python(script, text, timeout=90)


def dispatch_screener(_: str = "") -> str:
    """소미 점수 기반 유망종목 발굴 (거래량 상위 → 소미 점수 채점 상위)."""
    script = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_screener.py"
    return _run_python(script, "--top", "5", "--candidates", "20", timeout=240)


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


def handle_message(text: str) -> str:
    """Telegram 자연어 메시지를 처리합니다. 운영성 요청은 LLM 전에 직접 처리합니다."""
    text = (text or "").strip()
    if not text:
        return "메시지가 비어 있어요."

    if _is_trading_status_request(text):
        return _tool_get_trading_status() + "\n\n" + _tool_get_agent_status()

    if _is_stock_price_request(text):
        return _tool_get_stock_price(text)

    if _is_screener_request(text):
        return dispatch_screener(text)

    if _is_stock_analysis_request(text):
        return dispatch_to_somi(text)

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
