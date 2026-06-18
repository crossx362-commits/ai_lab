"""
approval_kyungsoo.py — 경수(수사관) 승인 모듈
인스타그램 및 YouTube 콘텐츠 승인 요청을 텔레그램으로 발송합니다.
"""
import os
import sys

# _shared 경로 추가
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
if _root not in sys.path:
    sys.path.insert(0, _root)

from _shared.notify import send


def await_approval(decision: str, channel: str = "") -> bool:
    """
    경수에게 Telegram으로 검수 요청을 보내고 자동 승인합니다.
    Args:
        decision: 승인 요청 내용 문자열
        channel: 채널 구분 (예: "Instagram", "YouTube")
    Returns:
        True (자동 승인)
    """
    channel_tag = f"[{channel}] " if channel else ""
    msg = f"🔍 {channel_tag}경수 검수 요청\n{decision}"
    try:
        send(msg)
        print(f"  ✅ 경수 검수 요청 전송 완료 (채널: {channel or '미지정'})")
    except Exception as e:
        print(f"  ⚠️ 경수 검수 알림 전송 실패: {e}")
    # 자동 승인
    return True
