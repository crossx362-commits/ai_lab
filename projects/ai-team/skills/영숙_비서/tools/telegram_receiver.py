#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Youngsuk Telegram bot — 게이트웨이.

역할: 텔레그램 폴링·핸들러·LLM 함수호출 디스패치.
도메인 툴은 각 주인 에이전트 모듈이 소유하고, 여기서는 BOT_TOOLS를 수집해 병합만 한다:
  - bot_common(bc)      : 의도 판별·서브프로세스 (공유 헬퍼)
  - bot_tools_info(info): 날씨·일정 (영숙 본연)
  - yewon_bot_tools(yewon): 오케스트레이션 (예원)"""

from __future__ import annotations

import asyncio
import json
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
for _p in (str(AI_TEAM_ROOT),
           str(HERE.parent),
           str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

from _shared.env import load_env
from _shared.notify import send as tg_send
from _shared.process import ProcessLock
from _shared import growth

import bot_common as bc
import bot_tools_info as info
import yewon_bot_tools as yewon


load_env(str(PROJECT_ROOT))

try:
    from telegram import Update
    from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes
    from telegram.error import Conflict as TgConflict
except ImportError as exc:
    print(f"Dependencies not installed: {exc}")
    print("Run: pip install python-telegram-bot")
    sys.exit(1)


SYSTEM = (
    "너는 영숙이야. 사장님(준호)의 비서이자 친구. "
    "친한 친구처럼 편하고 따뜻하게, 반말 섞어 짧고 자연스럽게 답해. "
    "딱딱한 안내체 금지. 단 사실·숫자는 정확히. "
    "운영/검색은 로컬 도구 결과를 우선한다."
)

PSYCHOLOGY_SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 팀 비서이자 대화 상대다.
- 어떤 말에도 먼저 감정과 의도를 읽고 짧게 받아준다.
- 불안/분노/무기력/외로움은 심리학 관점으로 풀되, 전문 진단처럼 단정하지 않는다.
- 위기나 자해 위험이 보이면 즉시 주변 사람/응급 서비스/988 같은 도움을 권한다.
- 최신 자료가 필요하면 검색 도구를 먼저 사용한다.
- 텔레그램답게 짧고 자연스럽게 답한다."""


# ── LLM 툴 레지스트리 — 각 도메인 모듈의 BOT_TOOLS를 병합(새 툴 추가는 주인 모듈 한 곳만) ──
_ALL_BOT_TOOLS = info.BOT_TOOLS + yewon.BOT_TOOLS
TOOLS = [t["schema"] for t in _ALL_BOT_TOOLS]
AVAILABLE_FUNCTIONS = {t["schema"]["function"]["name"]: t["handler"] for t in _ALL_BOT_TOOLS}


def web_search(query: str) -> str:
    """검색 요청 폴백. 실제 검색 도구가 없으면 GPT에 넘기지 않고 안내만 반환합니다."""
    return f"검색 기능이 아직 연결되지 않았습니다. 요청: {query}"


def _call_llm(prompt: str, system: str = SYSTEM) -> str:
    """클라우드 우선 폴백 — llm.text(lm_first=False)가 GPT→Gemini→클로드→로컬(승급) 전체
    체인을 담당(2026-07-03)."""
    from _shared import llm
    try:
        result = llm.text(prompt, system=system, max_tokens=800, temperature=0.8, lm_first=False)
    except Exception as exc:
        bc.log(f"_call_llm 실패: {exc}")
        result = None
    return result or "지금은 답변을 만들기 어려워요. 잠시 후 다시 시도해줘요."


def handle_with_gpt(text: str) -> str:
    """자연어 → 도구 선택·실행 (구독/로컬 LLM 체인 수동 tool use — 크레딧 불필요, 2026-07-05).
    구 클로드 API tool use는 크레딧0에서 마비 → llm.text(구독 클로드 우선) 기반으로 전환.
    ①도구 선택(JSON) ②실행 ③결과를 영숙 말투로 요약. 딱 맞는 도구 없으면 일반 대화.
    (함수명은 handle_message 호환성 위해 유지)"""
    from _shared import llm
    tool_lines = "\n".join(
        f"- {t['schema']['function']['name']}: {t['schema']['function']['description'][:70]}"
        for t in _ALL_BOT_TOOLS)
    sel_prompt = (
        f'사용자 메시지: "{text}"\n\n사용 가능한 도구:\n{tool_lines}\n\n'
        '메시지에 딱 맞는 도구가 있으면 그 도구명을, 없으면(일반 대화·잡담·인사·질문) null.\n'
        'JSON만 출력: {"tool": "도구명 또는 null", "args": {필요한 인자 객체}}'
    )
    try:
        raw = llm.text(sel_prompt, json_mode=True, max_tokens=500, lm_first=False)
        sel = json.loads(raw) if raw else {}
    except Exception:
        sel = {}
    tool = sel.get("tool")
    fn = AVAILABLE_FUNCTIONS.get(tool) if (tool and tool != "null") else None
    if fn:
        bc.log(f"Calling {tool} with {sel.get('args')}")
        try:
            result = fn(**(sel.get("args") or {}))
        except Exception as e:
            result = f"도구 실행 실패: {e}"
        ans = llm.text(
            f'사용자가 "{text}"라고 물었어. 도구 결과:\n{result}\n\n'
            '이 결과를 영숙이답게 친근하게(반말 섞어 짧게) 전해줘. 숫자·사실은 정확히.',
            system=SYSTEM, max_tokens=800, lm_first=False)
        return ans or str(result)
    # 딱 맞는 도구 없음 → 일반 대화(심리 프롬프트)
    return _call_llm(text, PSYCHOLOGY_SYSTEM)


DEV_RUNNER = Path(__file__).resolve().parent / "tg_dev_runner.py"


def _launch_dev_task(request: str) -> str:
    """'개발 <요청>' → 격리 worktree에서 헤드리스 claude 실행(백그라운드). 끝나면 결과를 별도 보고."""
    if not request:
        return "개발 요청 내용을 적어줘. 예: '개발 예원 하네스 알림에 이모지 추가'"
    try:
        wt = subprocess.run(["git", "worktree", "list"], cwd=str(PROJECT_ROOT),
                            capture_output=True, text=True, timeout=10).stdout
        if "ailab-dev-" in wt:
            return "🛠️ 이미 진행 중인 개발 작업이 있어. 끝나면 다시 요청해줘."
    except Exception:
        pass
    branch = "tg-dev-" + datetime.now().strftime("%m%d-%H%M%S")
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {"start_new_session": True}
    try:
        subprocess.Popen(
            [sys.executable, str(DEV_RUNNER), branch, request],
            cwd=str(PROJECT_ROOT),
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            **nowin,
        )
    except Exception as exc:
        return f"개발 작업 시작 실패: {exc}"
    return (f"🛠️ 개발 작업 시작했어 (브랜치 {branch}).\n요청: {request}\n"
            f"격리 환경에서 claude가 작업하고, 끝나면 변경 요약을 보고할게. (보통 1~3분, master는 안 건드려)")


def handle_message(text: str) -> str:
    """Telegram 자연어 메시지를 처리합니다. 운영 명령은 '의미'로 인식합니다(정해진 단어 불필요)."""
    text = (text or "").strip()
    if not text:
        return "메시지가 비어 있어요."

    if text.startswith("개발 ") or text.startswith("기능요청 "):
        return _launch_dev_task(text.split(" ", 1)[1].strip() if " " in text else "")

    # 맥 봇 전체 원격 종료/기동 — LLM 분류보다 먼저(결정적 즉답). 영숙·워치독은 유지.
    if bc.is_bots_on_request(text):
        from agent_controller import start_all_bots
        return start_all_bots()
    if bc.is_bots_off_request(text):
        from agent_controller import stop_all_bots
        return stop_all_bots()

    # 신규 에이전트 생성 승인/거절 (예원 오케스트레이터 제안에 대한 응답)
    _n = bc.normalize_text(text)
    if _n in ("에이전트승인", "새에이전트승인", "에이전트생성승인"):
        return yewon.agent_factory_action("approve_pending")
    if _n in ("에이전트거절", "에이전트취소", "새에이전트거절"):
        return yewon.agent_factory_action("reject_pending")

    if bc.is_weather_request(text):
        return info.get_weather(info.parse_weather_city(text), info.parse_weather_day(text))

    if bc.is_search_request(text):
        return web_search(text)

    gpt_response = handle_with_gpt(text)
    if not gpt_response.startswith("요청 처리 중 오류가 발생했습니다:"):
        return gpt_response
    return _call_llm(text, PSYCHOLOGY_SYSTEM)


async def _start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text("영숙 비서 대기 중입니다.")


async def _status(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.effective_message.reply_text(yewon.get_agent_status())


_ALLOWED_CHAT_IDS = {c.strip() for c in (os.getenv("TELEGRAM_CHAT_ID", "")).split(",") if c.strip()}

_CONFLICT_STATE_FILE = PROJECT_ROOT / "output" / "cache" / "youngsuk_conflict_alert.json"
_CONFLICT_ALERT_COOLDOWN_SEC = 1800  # 30분 — 다른 기기 폴링 충돌은 반복 스팸 대신 주기적 1회만


async def _error_handler(update: object, context: ContextTypes.DEFAULT_TYPE) -> None:
    """폴링 루프 에러 핸들러 — 등록 안 하면 PTB가 매 재시도마다 전체 스택트레이스를
    로그에 찍는다(Conflict는 재시도가 잦아 순식간에 로그가 수십MB로 불어남, 2026-07-09 확인).
    Conflict(다른 기기가 동시에 getUpdates 폴링 중 — 대개 맥)는 한 줄 로그 + 쿨다운 텔레그램 알림만,
    나머지 에러는 한 줄 로그만 남기고 폴링은 계속(run_polling이 알아서 재시도)."""
    err = context.error
    if isinstance(err, TgConflict):
        bc.log("getUpdates Conflict — 다른 기기(맥 등)에서 동시 폴링 중으로 추정")
        try:
            now = time.time()
            last = 0.0
            if _CONFLICT_STATE_FILE.exists():
                last = json.loads(_CONFLICT_STATE_FILE.read_text(encoding="utf-8")).get("ts", 0.0)
            if now - last > _CONFLICT_ALERT_COOLDOWN_SEC:
                _CONFLICT_STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
                _CONFLICT_STATE_FILE.write_text(json.dumps({"ts": now}), encoding="utf-8")
                tg_send("⚠️ 영숙 텔레그램 폴링 충돌 — 다른 기기(맥 등)에서 같은 봇을 동시에 실행 중인 것 같아요. "
                        "한쪽만 남기고 꺼주세요 (agent_controller.py 영숙 stop).")
        except Exception:
            pass
        return
    bc.log(f"Polling error: {err}")


async def _message(update: Update, context: ContextTypes.DEFAULT_TYPE) -> None:
    # 발신자 잠금 — fail-closed: 허용 목록이 비면 전부 거부. user.id·chat.id 모두 대조(그룹 권한상승 차단)
    uid = str(update.effective_user.id) if update.effective_user else ""
    cid = str(update.effective_chat.id) if update.effective_chat else ""
    if not _ALLOWED_CHAT_IDS or (uid not in _ALLOWED_CHAT_IDS and cid not in _ALLOWED_CHAT_IDS):
        bc.log(f"차단된 발신자 user={uid} chat={cid}: {(update.effective_message.text or '')[:50]}")
        return

    text = update.effective_message.text or ""
    bc.log(f"Message: {text[:100]}")
    try:
        response = await asyncio.to_thread(handle_message, text)
    except Exception as exc:
        bc.log(f"Handler error: {exc}")
        response = f"요청 처리 중 오류가 발생했습니다: {exc}"

    chunks = [response[i : i + 4000] for i in range(0, len(response), 4000)]
    for chunk in chunks:
        await update.effective_message.reply_text(chunk)

    growth.record(
        "youngsuk", role="명령 수신·전달·승인 확인", data=text[:60],
        judgment="명령 처리·응답", result="응답 전송",
        good="발신자 잠금(fail-closed) 적용", bad="명령 분류 라벨링 정식화 여지",
        scores={"fit": 20, "evidence": 17, "efficiency": 19, "risk": 18, "brevity": 9},
    )


def main() -> int:
    token = os.getenv("TELEGRAM_BOT_TOKEN")
    if not token:
        print("TELEGRAM_BOT_TOKEN is not configured.")
        return 1

    # 단일 인스턴스 보장 — 중복 폴러가 getUpdates Conflict를 일으키지 않도록
    with ProcessLock("youngsuk_telegram"):
        app = Application.builder().token(token).build()
        app.add_handler(CommandHandler("start", _start))
        app.add_handler(CommandHandler("status", _status))
        app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, _message))
        app.add_error_handler(_error_handler)

        bc.log("Youngsuk Telegram bot started (polling mode)")
        app.run_polling(allowed_updates=["message"], drop_pending_updates=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
