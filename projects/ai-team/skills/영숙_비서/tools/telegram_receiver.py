#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram receiver - 최신 python-telegram-bot 22.8 사용.

Single source of truth for Telegram bot.
"""

from __future__ import annotations

import os
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

# Telegram bot 22.8 최신 API
from telegram import Update
from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
LOG_PATH = SCRIPT_DIR / "telegram_receiver.log"


def _ensure_stdio() -> None:
    """로그 파일 설정"""
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

STATE_DIR = Path.home() / ".ai-team-brain"
STATE_DIR.mkdir(parents=True, exist_ok=True)

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
    """로그 출력"""
    print(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}", flush=True)


# =====================================================================
# 명령어 핸들러
# =====================================================================

async def start_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    """시작 명령"""
    await update.message.reply_text(
        "안녕하세요! 영숙이에요.\n\n"
        "사용 가능한 명령어:\n"
        "/start - 도움말\n"
        "/status - 에이전트 상태\n"
        "/balance - 잔고 확인\n"
        "/holdings - 보유 현황\n"
        "\n일반 메시지는 자연어로 처리됩니다."
    )
    log(f"start 명령: {update.effective_user.username}")


async def status_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    """에이전트 상태 확인"""
    try:
        import subprocess
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "unified_control.py"), "agent", "status"],
            capture_output=True,
            text=True,
            timeout=10,
            encoding='utf-8',
            errors='replace'
        )

        if result.returncode == 0:
            status_text = result.stdout or "상태 정보 없음"
            # Remove duplicate title if present in the first line
            lines = status_text.splitlines()
            if lines and "에이전트 상태" in lines[0]:
                status_text = "\n".join(lines[1:]).strip()
        else:
            status_text = f"상태 확인 실패:\n{result.stderr}"

        await update.message.reply_text(f"📊 에이전트 상태:\n\n{status_text}")
        log(f"status 명령: {update.effective_user.username}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 상태 확인 중 오류:\n{e}")
        log(f"status 오류: {e}")


async def balance_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    """잔고 확인"""
    try:
        import subprocess
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "daily_balance_check.py")],
            capture_output=True,
            text=True,
            timeout=30,
            encoding='utf-8',
            errors='replace'
        )

        if result.returncode == 0:
            balance_text = result.stdout or "잔고 정보 없음"
        else:
            balance_text = f"잔고 확인 실패:\n{result.stderr}"

        await update.message.reply_text(f"💰 잔고:\n\n{balance_text}")
        log(f"balance 명령: {update.effective_user.username}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 잔고 확인 중 오류:\n{e}")
        log(f"balance 오류: {e}")


async def holdings_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    """보유 현황"""
    try:
        import subprocess
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "check_holdings.py")],
            capture_output=True,
            text=True,
            timeout=30,
            encoding='utf-8',
            errors='replace'
        )

        if result.returncode == 0:
            holdings_text = result.stdout or "보유 정보 없음"
        else:
            holdings_text = f"보유 현황 확인 실패:\n{result.stderr}"

        await update.message.reply_text(f"📈 보유 현황:\n\n{holdings_text}")
        log(f"holdings 명령: {update.effective_user.username}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 보유 현황 확인 중 오류:\n{e}")
        log(f"holdings 오류: {e}")


# =====================================================================
# 자연어 메시지 핸들러
# =====================================================================

def _detect_intent(text: str) -> dict[str, Any]:
    """룰 기반 의도 감지 (Gemini 없이)"""
    clean = "".join(text.lower().split())

    # 상태 확인
    if any(k in clean for k in ["상태", "현황", "뭐해", "상황", "체크"]):
        return {"intent": "status", "confidence": 0.9}

    # 잔고 확인
    if any(k in clean for k in ["잔고", "잔액", "돈", "자산", "balance"]):
        return {"intent": "balance", "confidence": 0.9}

    # 보유 현황
    if any(k in clean for k in ["보유", "포지션", "코인", "주식", "holdings"]):
        return {"intent": "holdings", "confidence": 0.9}

    # 검색 요청
    if any(k in clean for k in ["검색", "찾아봐", "찾아줘", "인터넷", "웹에서"]):
        return {"intent": "search", "query": text, "confidence": 0.8}

    # 기본: 대화
    return {"intent": "chat", "confidence": 0.5}


async def handle_message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    """자연어 메시지 처리"""
    user_text = update.message.text or ""
    log(f"메시지: {user_text[:50]}...")

    # 의도 감지
    intent_result = _detect_intent(user_text)
    intent = intent_result.get("intent", "chat")

    try:
        if intent == "status":
            await status_command(update, context)
        elif intent == "balance":
            await balance_command(update, context)
        elif intent == "holdings":
            await holdings_command(update, context)
        elif intent == "search":
            query = intent_result.get("query", user_text)
            await update.message.reply_text(f"🔍 검색 중: {query}")
            # TODO: 웹 검색 통합
            await update.message.reply_text("검색 기능은 곧 추가될 예정이에요.")
        else:
            # 심리 상담 모드 (LLM 사용)
            await update.message.reply_text("생각 중...")

            try:
                response = llm_text(
                    user_text,
                    system=PSYCHOLOGY_SYSTEM,
                    max_tokens=500,
                    temperature=0.8,
                    lm_first=True  # Ollama → GPT → Gemini
                )

                if response:
                    await update.message.reply_text(response)
                else:
                    await update.message.reply_text("지금은 답변하기 어려워요. 잠시 후 다시 말해주세요.")
            except Exception as e:
                log(f"LLM 오류: {e}")
                await update.message.reply_text("답변 생성 중 문제가 생겼어요. 다시 시도해주세요.")

    except Exception as e:
        log(f"메시지 처리 오류: {e}")
        await update.message.reply_text(f"⚠️ 오류 발생:\n{e}")


async def error_handler(update: object, context: ContextTypes.DEFAULT_TYPE) -> None:
    """에러 핸들러"""
    log(f"업데이트 오류: {context.error}")

    if isinstance(update, Update) and update.effective_message:
        try:
            await update.effective_message.reply_text(
                "⚠️ 요청 처리 중 문제가 발생했어요. 잠시 후 다시 시도해주세요."
            )
        except Exception:
            pass


# =====================================================================
# 메인 봇 실행
# =====================================================================

def main() -> None:
    """봇 메인 루프"""
    if not TOKEN:
        log("❌ TELEGRAM_BOT_TOKEN 없음")
        return

    log("🚀 영숙 텔레그램 봇 시작...")
    log(f"Token: {TOKEN[:20]}...")
    log(f"Chat ID: {CHAT_ID}")

    # Application 생성 (최신 API)
    application = Application.builder().token(TOKEN).build()

    # 명령어 핸들러 등록
    application.add_handler(CommandHandler("start", start_command))
    application.add_handler(CommandHandler("status", status_command))
    application.add_handler(CommandHandler("balance", balance_command))
    application.add_handler(CommandHandler("holdings", holdings_command))

    # 메시지 핸들러 등록
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, handle_message))

    # 에러 핸들러 등록
    application.add_error_handler(error_handler)

    # 봇 실행 (polling) - run_polling()은 자체적으로 event loop 관리
    log("✅ 봇 준비 완료 - 메시지 대기 중...")
    application.run_polling(allowed_updates=Update.ALL_TYPES)


def daemon() -> None:
    """데몬 모드 진입점"""
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
        # 일회성 실행
        try:
            main()
        except KeyboardInterrupt:
            log("봇 중지됨")
