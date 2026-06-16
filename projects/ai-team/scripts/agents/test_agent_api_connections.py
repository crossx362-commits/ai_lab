#!/usr/bin/env python3
"""
AI 에이전트 API 연결 실제 테스트
환경변수뿐만 아니라 실제 API 연결을 테스트합니다.
"""

import os
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

# 프로젝트 루트 설정
_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env

# 환경 변수 로드
load_env()


def test_gemini_api():
    """Gemini API 연결 테스트"""
    try:
        from _shared.gemini_client import text
        result = text("Say 'OK' only", max_tokens=10)
        return result is not None, result[:50] if result else "No response"
    except Exception as e:
        return False, str(e)[:100]


def test_ollama():
    """Ollama 연결 테스트"""
    try:
        from _shared.ollama_client import is_available, chat
        if not is_available():
            return False, "Ollama 서버 연결 불가"
        result = chat("Say 'OK' only", task="test", max_tokens=10)
        return result is not None, result[:50] if result else "No response"
    except Exception as e:
        return False, str(e)[:100]


def test_telegram():
    """Telegram Bot 연결 테스트 (메시지 전송 없이 확인만)"""
    try:
        import os
        bot_token = os.getenv("TELEGRAM_BOT_TOKEN")
        chat_id = os.getenv("TELEGRAM_CHAT_ID")

        if not bot_token or not chat_id:
            return False, "토큰 또는 채팅 ID 없음"

        # 간단한 getMe API 호출로 봇 연결만 확인
        import urllib.request
        import json
        url = f"https://api.telegram.org/bot{bot_token}/getMe"
        with urllib.request.urlopen(url, timeout=5) as response:
            data = json.loads(response.read())
            if data.get("ok"):
                bot_name = data.get("result", {}).get("username", "Unknown")
                return True, f"@{bot_name}"
            return False, "API 응답 실패"
    except Exception as e:
        return False, str(e)[:100]


def test_youtube_oauth():
    """YouTube OAuth 연결 테스트"""
    try:
        sys.path.insert(0, "projects/ai-team/skills/경수_수사관/tools")
        from src.youtube_uploader import YouTubeUploader

        uploader = YouTubeUploader()
        if uploader.authenticate():
            return True, "인증 성공"
        return False, "인증 실패"
    except Exception as e:
        return False, str(e)[:100]


def test_instagram():
    """Instagram API 연결 테스트 (토큰 확인만)"""
    try:
        access_token = os.getenv("INSTAGRAM_ACCESS_TOKEN")
        account_id = os.getenv("INSTAGRAM_ACCOUNT_ID")

        if not access_token or not account_id:
            return False, "토큰 또는 계정 ID 없음"

        # 간단한 API 호출로 연결 확인
        import urllib.request
        import json
        url = f"https://graph.instagram.com/{account_id}?fields=username&access_token={access_token}"
        with urllib.request.urlopen(url, timeout=5) as response:
            data = json.loads(response.read())
            if data.get("username"):
                return True, f"@{data['username']}"
            return False, "API 응답 실패"
    except Exception as e:
        return False, str(e)[:100]


def test_notion():
    """Notion API 연결 테스트"""
    try:
        api_key = os.getenv("NOTION_API_KEY")
        db_id = os.getenv("NOTION_DATABASE_ID")

        if not api_key or not db_id:
            return False, "API 키 또는 DB ID 없음"

        # 간단한 API 호출
        import urllib.request
        import json
        url = "https://api.notion.com/v1/users/me"
        req = urllib.request.Request(
            url,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Notion-Version": "2022-06-28"
            }
        )
        with urllib.request.urlopen(req, timeout=5) as response:
            data = json.loads(response.read())
            if data.get("object") == "user":
                return True, "연결 성공"
            return False, "API 응답 실패"
    except Exception as e:
        return False, str(e)[:100]


def main():
    print("=" * 70)
    print("  AI 에이전트 API 실제 연결 테스트")
    print("=" * 70)
    print()
    print("⚠️  주의: 이 테스트는 실제 API를 호출합니다.")
    print()

    tests = [
        ("Gemini API", test_gemini_api, "필수 - 모든 에이전트"),
        ("Ollama", test_ollama, "선택 - 로컬 LLM"),
        ("Telegram Bot", test_telegram, "필수 - 알림"),
        ("YouTube OAuth", test_youtube_oauth, "선택 - 경수"),
        ("Instagram API", test_instagram, "선택 - 경수"),
        ("Notion API", test_notion, "필수 - 영숙"),
    ]

    passed = 0
    failed = 0
    skipped = 0

    for name, test_func, description in tests:
        print(f"🔍 {name} ({description})")
        try:
            success, message = test_func()
            if success:
                print(f"   ✅ 성공: {message}")
                passed += 1
            else:
                print(f"   ❌ 실패: {message}")
                failed += 1
        except Exception as e:
            print(f"   ⚠️  테스트 오류: {str(e)[:100]}")
            skipped += 1
        print()

    # 요약
    print("=" * 70)
    print("  테스트 요약")
    print("=" * 70)
    print(f"   ✅ 통과: {passed}개")
    print(f"   ❌ 실패: {failed}개")
    print(f"   ⚠️  건너뜀: {skipped}개")
    print()

    if failed == 0 and skipped == 0:
        print("🎉 모든 API 연결이 정상입니다!")
        return 0
    elif failed > 0:
        print("⚠️  일부 API 연결에 문제가 있습니다. 확인이 필요합니다.")
        return 1
    else:
        print("⚠️  일부 테스트를 건너뛰었습니다.")
        return 1


if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)
