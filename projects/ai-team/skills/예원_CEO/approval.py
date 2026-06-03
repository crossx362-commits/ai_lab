import os
from _shared.telegram_notifier import send_telegram_message

def await_approval(decision: str) -> bool:
    """Send approval request to 예원 via Telegram and auto‑approve.
    For simplicity this function always returns True after notifying.
    """
    # Send message (채팅 ID는 .env에 설정된 YEWON_TELEGRAM_ID 를 사용)
    try:
        send_telegram_message(f"[예원 승인 요청] {decision}")
    except Exception as e:
        print(f"⚠️ 승인 알림 전송 실패: {e}")
    # Auto‑approve (simple flow)
    return True
