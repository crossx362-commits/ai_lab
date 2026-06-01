#!/usr/bin/env python3
"""
환경 변수 유효성 검증 스크립트
모든 API 키가 제대로 작동하는지 테스트합니다.
"""
import os
import sys
import json
import urllib.request
import urllib.error

# .env 파일 로드
env_path = os.path.join(os.path.dirname(__file__), ".env")
if os.path.exists(env_path):
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                value = value.strip('"').strip("'")
                os.environ[key] = value

def test_vercel():
    """Vercel API 테스트"""
    print("\n[1/6] 🔍 Vercel API 테스트...")
    token = os.getenv("VERCEL_TOKEN", "")
    if not token:
        print("  ❌ VERCEL_TOKEN 없음")
        return False

    try:
        req = urllib.request.Request(
            "https://api.vercel.com/v9/projects",
            headers={"Authorization": f"Bearer {token}"}
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            data = json.loads(r.read())
            print(f"  ✅ Vercel API 정상 - 프로젝트 {len(data.get('projects', []))}개 확인")
            return True
    except Exception as e:
        print(f"  ❌ Vercel API 실패: {e}")
        return False

def test_supabase():
    """Supabase 연결 테스트"""
    print("\n[2/6] 🔍 Supabase API 테스트...")
    url = os.getenv("SUPABASE_URL", "")
    key = os.getenv("SUPABASE_ANON_KEY", "")
    if not url or not key:
        print("  ❌ SUPABASE_URL 또는 SUPABASE_ANON_KEY 없음")
        return False

    try:
        req = urllib.request.Request(
            f"{url}/auth/v1/health",
            headers={"apikey": key}
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            data = json.loads(r.read())
            if "version" in data:
                print(f"  ✅ Supabase API 정상 - {data.get('name', 'GoTrue')} {data.get('version', '')}")
                return True
            return False
    except Exception as e:
        print(f"  ❌ Supabase API 실패: {e}")
        return False

def test_gemini():
    """Gemini API 테스트"""
    print("\n[3/6] 🔍 Gemini API 테스트...")
    key = os.getenv("GEMINI_API_KEY", "")
    if not key:
        print("  ❌ GEMINI_API_KEY 없음")
        return False

    try:
        url = f"https://generativelanguage.googleapis.com/v1beta/models?key={key}"
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
            models = [m["name"] for m in data.get("models", [])[:3]]
            print(f"  ✅ Gemini API 정상 - 모델 {len(data.get('models', []))}개 사용 가능")
            return True
    except Exception as e:
        print(f"  ❌ Gemini API 실패: {e}")
        return False

def test_youtube():
    """YouTube Data API 테스트"""
    print("\n[4/6] 🔍 YouTube Data API 테스트...")
    key = os.getenv("YOUTUBE_API_KEY", "")
    if not key:
        print("  ❌ YOUTUBE_API_KEY 없음")
        return False

    try:
        url = f"https://www.googleapis.com/youtube/v3/search?part=snippet&q=test&maxResults=1&key={key}"
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
            print(f"  ✅ YouTube Data API 정상 - 검색 기능 작동")
            return True
    except Exception as e:
        print(f"  ❌ YouTube Data API 실패: {e}")
        return False

def test_instagram():
    """Instagram Graph API 테스트"""
    print("\n[5/6] 🔍 Instagram Graph API 테스트...")
    token = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    if not token:
        print("  ❌ INSTAGRAM_ACCESS_TOKEN 없음")
        return False

    # Facebook Graph API로 테스트 (제공된 토큰이 Facebook 토큰)
    try:
        url = f"https://graph.facebook.com/v21.0/me?access_token={token}"
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
            if "id" in data:
                print(f"  ⚠️  Facebook User Access Token - ID: {data.get('id')}")
                print(f"     Instagram Business 계정 연결 필요 (SETUP_INSTAGRAM.md 참고)")
                return True
            else:
                print(f"  ❌ 토큰 무효")
                return False
    except Exception as e:
        print(f"  ❌ Instagram/Facebook API 실패: {e}")
        return False

def test_telegram():
    """Telegram Bot API 테스트"""
    print("\n[6/6] 🔍 Telegram Bot API 테스트...")
    token = os.getenv("TELEGRAM_BOT_TOKEN", "")
    if not token:
        print("  ❌ TELEGRAM_BOT_TOKEN 없음")
        return False

    try:
        url = f"https://api.telegram.org/bot{token}/getMe"
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
            if data.get("ok"):
                bot_info = data.get("result", {})
                print(f"  ✅ Telegram Bot API 정상 - @{bot_info.get('username', 'unknown')}")
                return True
            else:
                print(f"  ❌ Telegram Bot API 응답 실패")
                return False
    except Exception as e:
        print(f"  ❌ Telegram Bot API 실패: {e}")
        return False

def main():
    print("=" * 60)
    print("🔐 환경 변수 유효성 검증")
    print("=" * 60)

    results = {
        "Vercel": test_vercel(),
        "Supabase": test_supabase(),
        "Gemini": test_gemini(),
        "YouTube": test_youtube(),
        "Instagram/Facebook": test_instagram(),
        "Telegram": test_telegram(),
    }

    print("\n" + "=" * 60)
    print("📊 테스트 결과 요약")
    print("=" * 60)

    passed = sum(results.values())
    total = len(results)

    for name, result in results.items():
        status = "✅ PASS" if result else "❌ FAIL"
        print(f"  {status}  {name}")

    print("\n" + "=" * 60)
    print(f"총 {passed}/{total}개 API 정상 작동")
    print("=" * 60)

    return 0 if passed == total else 1

if __name__ == "__main__":
    sys.exit(main())
