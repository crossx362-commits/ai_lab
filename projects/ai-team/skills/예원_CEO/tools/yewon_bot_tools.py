#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 CEO — 텔레그램 봇 툴. 영숙 게이트웨이가 BOT_TOOLS를 수집해 등록한다.

오케스트레이션 성격 툴(에이전트 현황·작업 위임·신규 에이전트 생성)의 주인은 예원이다.
영숙(telegram_receiver)은 이 모듈을 import해 자기 툴 레지스트리에 병합만 한다."""

from __future__ import annotations

import sys
from pathlib import Path

_HERE = Path(__file__).resolve()
_AI_TEAM_ROOT = _HERE.parents[3]
_YS_TOOLS = _AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools"
for _p in (str(_AI_TEAM_ROOT), str(_YS_TOOLS)):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import bot_common as bc
from _shared.notify import status_report


def get_agent_status(_: str = "전체") -> str:
    """현재 AI 팀 에이전트 현황을 조회합니다."""
    return status_report()


def dispatch_to_yewon(text: str) -> str:
    """예원 CEO에게 작업을 요청합니다 (플랜 기반 멀티 에이전트 오케스트레이션)."""
    script = _AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_orchestrator.py"
    return bc.run_python(script, text, timeout=180)


def agent_factory_action(fn: str) -> str:
    """예원 agent_factory의 승인/거절 호출 (신규 에이전트 생성 게이트)."""
    try:
        sys.path.insert(0, str(_AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools"))
        import agent_factory
        return getattr(agent_factory, fn)()
    except Exception as exc:
        return f"에이전트 생성 처리 실패: {exc}"


BOT_TOOLS = [
    {
        "handler": get_agent_status,
        "schema": {
            "type": "function",
            "function": {
                "name": "get_agent_status",
                "description": "현재 실행 중인 AI 팀 에이전트 현황 조회",
                "parameters": {"type": "object", "properties": {}, "required": []},
            },
        },
    },
    {
        "handler": dispatch_to_yewon,
        "schema": {
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
    },
]
