#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 텔레그램 봇 — Gemini Function Calling + python-telegram-bot 22.8"""

from __future__ import annotations

import os
import sys
import subprocess
from datetime import datetime, timezone, timedelta
from pathlib import Path

from telegram import Update
from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes

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

from _shared.env import load_env  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
API_KEY = os.getenv("GEMINI_API_KEY", "").strip()

# Gemini client 초기화
try:
    from google import genai
    from google.genai import types as gtypes
    _gemini_client = genai.Client(api_key=API_KEY) if API_KEY else None
except Exception:
    _gemini_client = None

# 대화 히스토리 (최근 3턴 유지)
_HISTORY: list = []

SYSTEM_PROMPT = (
    "영숙(비서). 규칙: 짧게 핵심만 2줄 이내. "
    "필요시 도구(get_agent_status, list_calendar, dispatch, get_stock_price) 즉시 호출."
)

PSYCHOLOGY_SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 트레이딩팀 비서이자 대화 상대다.
- 어떤 말에도 먼저 감정과 의도를 읽고 짧게 받아준다.
- 불안/분노/무기력/외로움 등은 심리학 관점으로 풀되 전문 용어 남발 금지.
- 보통 1) 감정 반영 2) 핵심 패턴 3) 지금 할 수 있는 작은 행동 1개 순서로 답한다.
- 텔레그램답게 짧고 자연스럽게."""


def log(message: str) -> None:
    print(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}", flush=True)


# =====================================================================
# 도구 함수 (Gemini Function Calling용)
# =====================================================================

def get_agent_status(agent: str = "전체") -> str:
    """에이전트 현황 조회. Args: agent - 에이전트명 또는 '전체'"""
    log(f"에이전트 현황 조회: {agent}")
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "unified_control.py"), "agent", "status"],
            capture_output=True, text=True, timeout=10, encoding="utf-8", errors="replace"
        )
        return (result.stdout or "상태 정보 없음")[:1000]
    except Exception as e:
        return f"❌ 상태 조회 실패: {e}"


def list_calendar(days: int = 7) -> str:
    """캘린더 일정 조회. Args: days - 조회 일수"""
    log(f"캘린더 조회: {days}일")
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "_shared"))
        from calendar_client import get_service, list_events as _list_events  # noqa: PLC0415
        svc = get_service()
        if not svc:
            raise RuntimeError("Google Calendar 인증 필요")
        events = _list_events(svc, days_ahead=days)
        if not events:
            return f"📅 향후 {days}일 일정 없음"
        lines = []
        for ev in events[:15]:
            start = ev.get("start", {})
            dt = start.get("dateTime") or start.get("date", "")
            if "T" in dt:
                from datetime import datetime as _dt  # noqa: PLC0415
                d = _dt.fromisoformat(dt)
                time_str = f"{d.month}/{d.day} {d.hour:02d}:{d.minute:02d}"
            else:
                time_str = dt[5:]  # MM-DD
            lines.append(f"• {time_str} {ev.get('summary','(제목없음)')}")
        return f"📅 향후 {days}일 일정 ({len(events)}건):\n" + "\n".join(lines)
    except Exception as e:
        # 폴백: 캐시 파일
        cache = AI_TEAM_ROOT / "_shared" / "calendar_cache.md"
        if cache.exists():
            return f"📅 일정 (캐시):\n{cache.read_text(encoding='utf-8')[:800]}"
        return f"📅 캘린더 조회 실패: {e}"


def dispatch(cmd: str) -> str:
    """에이전트에게 작업 지시. Args: cmd - 명령"""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools"))
        import yewon_dispatcher  # noqa: PLC0415
        result = yewon_dispatcher.dispatch_and_execute(cmd)
        if not result:
            return "⚠️ CEO 대기 중"
        # 긴 결과는 Gemini로 요약
        if len(result) > 400 and _gemini_client:
            try:
                s = _gemini_client.models.generate_content(
                    model="gemini-2.5-flash",
                    contents=f"2줄 요약:\n{result[:600]}",
                    config=gtypes.GenerateContentConfig(max_output_tokens=100)
                )
                if s.text:
                    return f"✅ {s.text.strip()}"
            except Exception:
                pass
        return result[:400] + ("..." if len(result) > 400 else "")
    except Exception as e:
        return f"❌ {str(e)[:100]}"


def get_stock_price(stock_code: str, stock_name: str = "") -> str:
    """주식 현재가 조회. Args: stock_code - 6자리 종목코드 (예: '005930' -> 삼성전자, '035720' -> 카카오, '000660' -> SK하이닉스), stock_name - 종목명 (옵션)"""
    log(f"주식 현재가 조회: {stock_name} ({stock_code})")
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "데이브_주식" / "tools"))
        from kis_client import KISClient  # noqa: PLC0415
        client = KISClient()
        price_data = client.get_current_price(stock_code)
        if "output" not in price_data:
            return f"❌ {stock_name or stock_code} 조회 실패: {price_data.get('msg1', '알 수 없는 오류')}"
        
        output = price_data["output"]
        current_price = int(output.get("stck_prpr", 0))
        change = int(output.get("prdy_vrss", 0))
        change_rate = float(output.get("prdy_ctrt", 0.0))
        sign = output.get("prdy_vrss_sign", "3")
        
        sign_emoji = "➕"
        if sign in ["1", "2"]:
            sign_emoji = "📈"
        elif sign in ["4", "5"]:
            sign_emoji = "📉"
            change = -change
        else:
            sign_emoji = "➖"
            
        name_str = f"{stock_name}" if stock_name else f"종목코드 {stock_code}"
        return f"📊 {name_str} 현재가: {current_price:,}원 ({sign_emoji} 전일대비 {change:+,}원 | {change_rate:+.2f}%)"
    except Exception as e:
        return f"❌ 주가 조회 중 오류 발생: {e}"


_TOOL_MAP = {
    "get_agent_status": get_agent_status,
    "list_calendar": list_calendar,
    "dispatch": dispatch,
    "get_stock_price": get_stock_price,
}
_TOOLS = [get_agent_status, list_calendar, dispatch, get_stock_price]


# =====================================================================
# Gemini Function Calling 처리
# =====================================================================

def _process_with_gemini(user_text: str) -> str | None:
    """Gemini Function Calling으로 메시지 처리. 실패 시 None 반환."""
    global _HISTORY

    if not _gemini_client:
        return None

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    _HISTORY.append(gtypes.Content(role="user", parts=[gtypes.Part.from_text(text=user_text)]))
    if len(_HISTORY) > 6:
        _HISTORY = _HISTORY[-6:]

    model = "gemini-2.5-flash"
    try:
        resp = _gemini_client.models.generate_content(
            model=model,
            contents=_HISTORY,
            config=gtypes.GenerateContentConfig(
                system_instruction=SYSTEM_PROMPT + time_ctx,
                tools=_TOOLS,
                max_output_tokens=200,
                temperature=0.7,
            ),
        )
    except Exception as e:
        err = str(e)
        if any(code in err for code in ["429", "503", "RESOURCE_EXHAUSTED"]) or any(k in err.lower() for k in ["quota", "demand", "unavailable"]):
            log("Gemini Flash 오류 (429/503/Quota) → Pro 전환")
            model = "gemini-2.5-pro"
            try:
                resp = _gemini_client.models.generate_content(
                    model=model,
                    contents=_HISTORY,
                    config=gtypes.GenerateContentConfig(
                        system_instruction=SYSTEM_PROMPT + time_ctx,
                        tools=_TOOLS,
                        max_output_tokens=200,
                        temperature=0.7,
                    ),
                )
            except Exception as e2:
                log(f"Gemini Pro 실패: {e2}")
                return None
        else:
            log(f"Gemini 오류: {e}")
            return None

    answer = ""
    tool_results = []

    if resp.candidates and resp.candidates[0].content.parts:
        for part in resp.candidates[0].content.parts:
            if part.text:
                answer += part.text
            elif part.function_call:
                fn = part.function_call.name
                args = dict(part.function_call.args) if part.function_call.args else {}
                log(f"Tool: {fn}({args})")
                if fn in _TOOL_MAP:
                    try:
                        res = _TOOL_MAP[fn](**args)
                        tool_results.append({"name": fn, "result": res})
                    except Exception as e:
                        tool_results.append({"name": fn, "result": f"❌ {e}"})

    # 도구 결과가 있으면 최종 답변 생성
    if tool_results:
        fn_parts = [
            gtypes.Part.from_function_response(name=tr["name"], response={"result": tr["result"]})
            for tr in tool_results
        ]
        _HISTORY.append(gtypes.Content(
            role="model",
            parts=[gtypes.Part.from_function_call(name=tool_results[0]["name"], args={})],
        ))
        _HISTORY.append(gtypes.Content(role="user", parts=fn_parts))

        is_status = any(tr["name"] == "get_agent_status" for tr in tool_results)
        max_tok = 1000 if is_status else 150
        sys_inst = SYSTEM_PROMPT + time_ctx
        if is_status:
            sys_inst += "\n현황 보고는 모든 에이전트 내용을 줄이지 말고 상세하게 보고하세요."

        try:
            final = _gemini_client.models.generate_content(
                model=model,
                contents=_HISTORY,
                config=gtypes.GenerateContentConfig(
                    system_instruction=sys_inst,
                    max_output_tokens=max_tok,
                    temperature=0.7,
                ),
            )
            answer = final.text.strip() if final.text else "\n\n".join(tr["result"] for tr in tool_results)
        except Exception as e:
            log(f"Gemini 최종 답변 실패: {e}")
            answer = "\n\n".join(tr["result"] for tr in tool_results)

    if not answer:
        answer = "네"

    _HISTORY.append(gtypes.Content(role="model", parts=[gtypes.Part.from_text(text=answer)]))
    if len(_HISTORY) > 6:
        _HISTORY = _HISTORY[-6:]

    return answer


# =====================================================================
# 명령어 핸들러
# =====================================================================

async def start_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.message.reply_text(
        "안녕하세요! 영숙이에요.\n\n"
        "/status - 에이전트 상태\n"
        "/balance - 잔고\n"
        "/holdings - 보유 현황\n\n"
        "자연어로 뭐든 말해주세요."
    )
    log(f"start: {update.effective_user.username}")


async def status_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    text = get_agent_status()
    await update.message.reply_text(f"📊 에이전트 상태:\n\n{text}")
    log("status 명령")


async def balance_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "daily_balance_check.py")],
            capture_output=True, text=True, timeout=30, encoding="utf-8", errors="replace"
        )
        text = result.stdout if result.returncode == 0 else f"실패:\n{result.stderr}"
        await update.message.reply_text(f"💰 잔고:\n\n{text}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 잔고 확인 오류:\n{e}")
    log("balance 명령")


async def holdings_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "check_holdings.py")],
            capture_output=True, text=True, timeout=30, encoding="utf-8", errors="replace"
        )
        text = result.stdout if result.returncode == 0 else f"실패:\n{result.stderr}"
        await update.message.reply_text(f"📈 보유 현황:\n\n{text}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 보유 현황 오류:\n{e}")
    log("holdings 명령")


# =====================================================================
# 자연어 메시지 핸들러
# =====================================================================

async def handle_message(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    user_text = update.message.text or ""
    log(f"메시지: {user_text[:50]}")

    await update.message.reply_text("생각 중...")

    try:
        _u = user_text
        _coin_kw = ["코인", "보유", "포트폴리오", "잔고", "수익률", "holdings", "balance",
                    "비트코인", "이더리움", "업비트", "매매현황", "거래현황", "btc", "eth", "주식현황"]

        # 0차: 코인/잔고 키워드 → check_holdings.py 직접 실행 (Gemini 우회)
        if any(k in _u.lower() for k in _coin_kw):
            log("키워드 직접처리: check_holdings.py 실행")
            try:
                r = subprocess.run(
                    [sys.executable, str(AI_TEAM_ROOT / "scripts" / "check_holdings.py")],
                    capture_output=True, text=True, timeout=20,
                    encoding="utf-8", errors="replace"
                )
                response = (r.stdout or r.stderr or "잔고 조회 실패")[:1200]
            except Exception as _e:
                response = f"❌ 잔고 조회 오류: {_e}"
        else:
            response = None

        # 1차: Gemini Function Calling
        if not response:
            response = _process_with_gemini(user_text)

        # 2차 폴백: 키워드 직접 처리 (Gemini 실패 시)
        if not response:
            _status_kw = ["에이전트", "상태", "뭐해", "다들", "작동", "실행", "팀"]
            _cal_kw = ["일정", "캘린더", "스케줄", "calendar"]
            if any(k in _u for k in _status_kw):
                log("키워드 폴백: 에이전트 상태")
                response = get_agent_status()
            elif any(k in _u for k in _cal_kw):
                log("키워드 폴백: 캘린더")
                response = list_calendar()

        # 3차 폴백: Ollama → GPT
        if not response:
            log("Gemini 실패 → llm_text 폴백")
            response = llm_text(
                user_text,
                system=PSYCHOLOGY_SYSTEM,
                max_tokens=500,
                temperature=0.8,
                lm_first=False,  # GPT → Gemini → Ollama 순서
            )

        await update.message.reply_text(response or "지금은 답변하기 어려워요. 잠시 후 다시 말해주세요.")

    except Exception as e:
        log(f"메시지 처리 오류: {e}")
        await update.message.reply_text(f"⚠️ 오류:\n{e}")


async def error_handler(update: object, context: ContextTypes.DEFAULT_TYPE) -> None:
    log(f"업데이트 오류: {context.error}")
    if isinstance(update, Update) and update.effective_message:
        try:
            await update.effective_message.reply_text("⚠️ 요청 처리 중 문제가 발생했어요.")
        except Exception:
            pass


# =====================================================================
# 메인
# =====================================================================

def main() -> None:
    if not TOKEN:
        log("❌ TELEGRAM_BOT_TOKEN 없음")
        return

    log("🚀 영숙 텔레그램 봇 시작...")
    log(f"Token: {TOKEN[:20]}...")
    log(f"Gemini: {'✅' if _gemini_client else '❌ (폴백 모드)'}")

    application = Application.builder().token(TOKEN).build()
    application.add_handler(CommandHandler("start", start_command))
    application.add_handler(CommandHandler("status", status_command))
    application.add_handler(CommandHandler("balance", balance_command))
    application.add_handler(CommandHandler("holdings", holdings_command))
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, handle_message))
    application.add_error_handler(error_handler)

    log("✅ 봇 준비 완료 - 메시지 대기 중...")
    application.run_polling(allowed_updates=Update.ALL_TYPES)


def daemon() -> None:
    with ProcessLock("youngsuk"):
        try:
            main()
        except KeyboardInterrupt:
            log("봇 중지됨")
        except Exception as e:
            log(f"봇 실행 오류: {e}")
            import traceback
            traceback.print_exc()


if __name__ == "__main__":
    if "--daemon" in sys.argv:
        daemon()
    else:
        try:
            main()
        except KeyboardInterrupt:
            log("봇 중지됨")
