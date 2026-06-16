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
        if os.path.isfile(os.path.join(root, "ENV_MANIFEST.json")) or os.path.isfile(os.path.join(root, ".env.encrypted")):
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
    from .gemini_client import text as gemini_text
except (ImportError, ValueError):
    try:
        from gemini_client import text as gemini_text
    except ImportError:
        import sys
        import os
        sys.path.insert(0, os.path.dirname(__file__))
        from gemini_client import text as gemini_text

def _call_ai_for_yeongsuk(original_message: str) -> str:
    """영숙 페르소나를 사용하여 메시지를 친근한 한글 톤으로 변환 (Gemini 2.5 Flash)"""
    system_prompt = (
        "당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 비서입니다. "
        "말투는 자연스럽고 친근하며 이모지를 적절히 사용합니다. "
        "에이전트들의 거래/작업 알림을 대표님께 간결하게 보고해주세요. "
        "핵심만 전달하고, HTML 태그는 절대 사용하지 마세요. "
        "1-2줄로 간결하게 작성하세요."
    )
    prompt = f"다음 알림을 간결하게 변환해줘:\n\n{original_message}"

    try:
        res = gemini_text(
            prompt=prompt,
            system=system_prompt,
            max_tokens=200,
            temperature=0.7
        )
        if res and len(res.strip()) > 0:
            return res.strip()
    except Exception as e:
        print(f"  [Gemini] 변환 실패, 원본 전송: {e}")

    return original_message


# ─── 공개 API ────────────────────────────────────────────────────────────────

def send_telegram_message(message: str, parse_mode: str = "HTML") -> bool:
    """텔레그램 메시지 전송. 실패 시 False 반환 (예외 미전파).

    ⚠️ 모든 에이전트는 직접 전송하지 않고 영숙에게 보고만 함.
    """
    # 영숙 중심 아키텍처: 다른 에이전트들은 로그만 남기고 전송 안 함
    if os.getenv("SUPPRESS_TELEGRAM") == "1":
        print(f"  [영숙에게 보고 예정] {message[:50]}")
        return True

    # 중복 메시지 및 도배 방지 필터 (파일 기반 영구 캐시)
    try:
        import time
        import re
        import json
        
        cache_file = os.path.join(_PROJECT_ROOT, ".telegram_sent_cache.json")
        now = time.time()
        
        # 캐시 파일 로드 및 오래된 데이터 정리
        cache = {}
        if os.path.exists(cache_file):
            try:
                with open(cache_file, "r", encoding="utf-8") as f:
                    cache = json.load(f)
            except Exception:
                pass
        
        # 10분(600초) 이상 지난 항목 정리
        cache = {k: v for k, v in cache.items() if now - v < 600}
        
        # 에이전트명, 코인명(티커), 액션(매수/매도/실패/에러 등) 추출
        agent_match = re.search(r'\[([^\]]+)\]', message)
        ticker_match = re.search(r'(KRW-[A-Z0-9]+)', message)
        
        agent = agent_match.group(1) if agent_match else "unknown"
        ticker = ticker_match.group(1) if ticker_match else "general"
        
        # 메시지 내 핵심 상태 단어 추출
        action = "info"
        for word in ["매도 실패", "매수 실패", "손절", "익절", "매도", "매수", "에러", "오류", "실패", "실시간 스캔"]:
            if word in message:
                action = word
                break
        
        is_duplicate = False
        if action != "info" or ticker != "general":
            key = f"{agent}_{ticker}_{action}"
            last_time = cache.get(key, 0)
            if now - last_time < 600:  # 10분
                is_duplicate = True
            else:
                cache[key] = now
        else:
            # 일반 메시지의 경우 완전히 동일한 텍스트는 5분간 차단
            key = f"exact_{hash(message)}"
            last_time = cache.get(key, 0)
            if now - last_time < 300:  # 5분
                is_duplicate = True
            else:
                cache[key] = now
                
        # 캐시 업데이트 저장
        try:
            with open(cache_file, "w", encoding="utf-8") as f:
                json.dump(cache, f, ensure_ascii=False)
        except Exception:
            pass
            
        if is_duplicate:
            print(f"  [Telegram] 중복 메시지 전송 차단 (Key: {key})")
            return True
    except Exception as fe:
        print(f"중복 방지 필터 오류: {fe}")

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
