"""
_shared/telegram_notifier.py — 전체 에이전트 공용 텔레그램 알림 모듈.

사용법:
    import sys, os
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '_shared'))
    from telegram_notifier import send_telegram_message

    send_telegram_message("✅ 루나: 업로드 완료")
"""
import os
import json
import urllib.request

# ─── 프로젝트 루트 자동 탐색 ──────────────────────────────────────────────────
def _find_project_root() -> str:
    here = os.path.dirname(os.path.abspath(__file__))
    root = here
    for _ in range(10):
        if os.path.isdir(os.path.join(root, ".agent")):
            return root
        parent = os.path.dirname(root)
        if parent == root:
            break
        root = parent
    return here


_PROJECT_ROOT = _find_project_root()

try:
    from .env_loader import load_env as _load_env
except (ImportError, ValueError):
    from env_loader import load_env as _load_env

_load_env(_PROJECT_ROOT)


# ─── 영숙 페르소나 가공 ────────────────────────────────────────────────────────
try:
    from .ollama_client import chat as lm_chat, is_available as lm_available
except (ImportError, ValueError):
    try:
        from ollama_client import chat as lm_chat, is_available as lm_available
    except ImportError:
        import sys
        import os
        sys.path.insert(0, os.path.dirname(__file__))
        from ollama_client import chat as lm_chat, is_available as lm_available

def _call_ai_for_yeongsuk(original_message: str) -> str:
    """영숙 페르소나를 사용하여 메시지를 친근한 한글 톤으로 변환."""
    yeongsuk_persona = (
        "당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 동료입니다. "
        "말투는 자연스럽고 친근하며 이모지를 적절히 사용합니다. "
        "전달받은 에이전트들의 작업 진행 상황이나 수사 결과, 리서치 보고 등의 메시지를 바탕으로, "
        "대표님(CEO)께 보내는 애교 있고 싹싹하며 명랑한 한글 메시지를 작성해주세요. "
        "가독성을 높이기 위해 줄바꿈과 적절한 이모지를 활용하여 정돈해 주시고, "
        "절대로 <b>, <i>, <code> 등 HTML 태그는 작성하지 마세요."
    )
    prompt = f"{yeongsuk_persona}\n\n다음 에이전트의 원본 알림 메시지를 바탕으로 대표님께 보낼 예쁜 보고 메시지를 작성해줘. 절대로 HTML 태그는 쓰지 마:\n\n{original_message}"

    if lm_available():
        try:
            res = lm_chat(prompt, max_tokens=1000, temperature=0.8)
            if res:
                return res.strip()
        except Exception:
            pass

    return original_message


# ─── 공개 API ────────────────────────────────────────────────────────────────

def send_telegram_message(message: str, parse_mode: str = "HTML") -> bool:
    """텔레그램 메시지 전송. 실패 시 False 반환 (예외 미전파)."""
    # 영숙이 톤으로 일괄 변환
    message = _call_ai_for_yeongsuk(message)

    token = os.getenv("TELEGRAM_BOT_TOKEN", "")
    chat_id = os.getenv("TELEGRAM_CHAT_ID", "")
    if not token or not chat_id:
        print("  [Telegram] 토큰 또는 CHAT_ID 미설정 — 전송 생략")
        return False
    try:
        url = f"https://api.telegram.org/bot{token}/sendMessage"
        data = json.dumps({
            "chat_id": chat_id,
            "text": message,
            "parse_mode": parse_mode,
        }).encode("utf-8")
        req = urllib.request.Request(
            url, data=data, headers={"Content-Type": "application/json"}
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            res = json.loads(r.read())
            return res.get("ok", False)
    except Exception as e:
        print(f"  [Telegram] 전송 실패: {e}")
        return False
