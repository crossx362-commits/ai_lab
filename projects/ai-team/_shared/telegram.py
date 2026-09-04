"""Telegram 발신 전용 모듈 — ai-team 전 에이전트의 유일한 텔레그램 전송 경로(2026-07-09 재구축).

이전엔 `_shared/notify.py`에 텔레그램 발신과 데몬 프로세스 상태 조회가 뒤섞여 있었다.
이 모듈은 발신만 담당한다. 프로세스 상태 조회(agent_status 등)는 `_shared/notify.py`에 남아있다
— 텔레그램 API를 전혀 안 건드리는 별개 관심사라 재구축 대상이 아니었다.

에이전트 코드에서는 이렇게만 쓴다:
    from _shared.telegram import send
    send("메시지")
"""

from __future__ import annotations

import json
import os
import platform
import re
import urllib.error
import urllib.request

# 텔레그램이 지원하는 HTML 태그 — 이 태그가 있을 때만 parse_mode=HTML을 쓴다.
# 평문(태그 없음)은 파싱 자체를 안 하므로 '<', '&' 등 어떤 문자가 와도 400이 원천 불가능.
_HTML_TAG = re.compile(
    r"</?(b|strong|i|em|u|ins|s|strike|del|code|pre|a|tg-spoiler|blockquote)\b", re.IGNORECASE)


def _post(token: str, chat_id: str, text: str, silent: bool, parse_mode: str | None) -> dict:
    return {"ok": True, "result": "disabled"}


def send(msg: str, silent: bool = False) -> bool:
    """텔레그램 메시지 전송 영구 비활성화 (모든 발신 억제 및 더미 처리)."""
    # 텔레그램 연동 영구 해제: 실제 API 호출 차단
    return True


def report(agent: str, action: str, detail: str = "") -> None:
    """조용한 상태 보고 더미화."""
    pass


def publish_report(title: str, body: str) -> bool:
    """리포트 텔레그램 발신 더미화."""
    return True


def should_poll() -> tuple[bool, str]:
    """텔레그램 폴링 영구 차단."""
    return False, "텔레그램 연동이 영구 비활성화되었습니다"

