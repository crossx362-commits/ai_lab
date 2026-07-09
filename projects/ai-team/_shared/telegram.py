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
    body = {"chat_id": chat_id, "text": text[:4096], "disable_notification": silent}
    if parse_mode:
        body["parse_mode"] = parse_mode
    req = urllib.request.Request(
        f"https://api.telegram.org/bot{token}/sendMessage",
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=10) as response:
        return json.loads(response.read().decode("utf-8"))


def send(msg: str, silent: bool = False) -> bool:
    """텔레그램 메시지 전송. 자격증명 없거나 SUPPRESS_TELEGRAM=true면 콘솔에만 찍고 True 반환
    (억제는 실패가 아니라 의도된 무전송이라 상위 로직이 재시도하지 않게)."""
    if os.getenv("SUPPRESS_TELEGRAM") == "true":
        print(f"[Telegram suppressed] {msg[:100]}")
        return True

    token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_CHAT_ID")
    if not token or not chat_id:
        print("[Telegram] Missing TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID")
        return False

    wants_html = bool(_HTML_TAG.search(msg))
    try:
        try:
            result = _post(token, chat_id, msg, silent, "HTML" if wants_html else None)
        except urllib.error.HTTPError as he:
            if he.code != 400 or not wants_html:
                raise
            # HTML 태그가 있어도 본문 나머지가 파싱을 깨면 400 → 평문 폴백(서식은 잃어도 전달 보장)
            result = _post(token, chat_id, msg, silent, None)
        if result.get("ok"):
            print(f"[Telegram] Sent {len(msg)} chars")
            return True
        print(f"[Telegram] API error: {result}")
        return False
    except Exception as exc:
        print(f"[Telegram] {exc}")
        return False


def report(agent: str, action: str, detail: str = "") -> None:
    """조용한(silent) 상태 보고 한 줄 — 알림음 없이 로그성으로만 전송."""
    msg = f"[{agent}] {action}"
    if detail:
        msg += f"\n{detail}"
    send(msg, silent=True)


def publish_report(title: str, body: str) -> bool:
    """보고서·브리핑은 노션에 작성하고 텔레그램엔 '제목 + 노션 링크'만 보낸다.
    노션 불가(키 없음/실패) 시 본문을 텔레그램으로 폴백 전송(보고 유실 방지)."""
    url = ""
    try:
        from _shared import research
        url = research.notion_report(title, body)
    except Exception:
        url = ""
    if url:
        return send(f"📄 {title}\n{url}")
    ok = True
    for i in range(0, len(body), 3900):
        ok = send(body[i:i + 3900]) and ok
    return ok


# ==================== 폴링 소유권 (다기기 충돌 방지 스위치) ====================
#
# Windows·맥이 같은 봇 토큰으로 동시에 getUpdates 폴링하면 텔레그램이 서로를
# Conflict(409)로 끊어내며 응답이 불안정해진다(2026-07-09 실사고). 두 기기 간 공유
# 저장소가 없어(맥 SSH 미승인, Supabase는 anon 키뿐이라 DDL 불가) 자동 조정은 불가능 —
# 대신 `.env`의 TELEGRAM_POLL_HOST로 사람이 명시적으로 소유 기기를 지정할 수 있게 한다.
# 미설정(기본)이면 이 기기에서 그냥 폴링 허용 — 기존처럼 동작(무설정 시 회귀 없음).


def should_poll() -> tuple[bool, str]:
    """이 기기가 폴링해도 되는지 판정. (허용여부, 사유) 반환.
    TELEGRAM_POLL_HOST가 비어있으면 항상 허용(사용자가 아직 기기를 지정 안 함)."""
    designated = os.getenv("TELEGRAM_POLL_HOST", "").strip()
    if not designated:
        return True, "TELEGRAM_POLL_HOST 미설정 — 무조건 허용"
    here = platform.node().strip()
    if here.lower() == designated.lower():
        return True, f"이 기기({here})가 지정된 폴링 기기"
    return False, f"이 기기({here})는 지정 폴링 기기({designated})가 아님 — 폴링 안 함"
