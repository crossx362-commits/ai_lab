"""
텔레그램 봇 테스트
"""
import sys
import os

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

from google import genai
from google.genai import types

print("=" * 70)
print("텔레그램 봇 설정 테스트")
print("=" * 70)

# API 키 확인
gemini_key = os.getenv("GEMINI_API_KEY", "")
anthropic_key = os.getenv("ANTHROPIC_API_KEY", "")
telegram_token = os.getenv("TELEGRAM_BOT_TOKEN", "")
chat_id = os.getenv("TELEGRAM_CHAT_ID", "")

print(f"\n[OK] Gemini API Key: {gemini_key[:20]}...{gemini_key[-10:] if len(gemini_key) > 30 else ''}")
print(f"  Length: {len(gemini_key)} chars")

print(f"\n[OK] Anthropic API Key: {anthropic_key[:20]}...{anthropic_key[-10:] if len(anthropic_key) > 30 else ''}")
print(f"  Length: {len(anthropic_key)} chars")

print(f"\n[OK] Telegram Token: {telegram_token[:10]}...")
print(f"[OK] Chat ID: {chat_id}")

# Gemini 테스트
print("\n" + "=" * 70)
print("Gemini API 테스트")
print("=" * 70)

try:
    client = genai.Client(api_key=gemini_key)
    print("[OK] Gemini 클라이언트 초기화 성공")

    response = client.models.generate_content(
        model="gemini-2.5-flash",
        contents="Say 'OK' in one word",
        config=types.GenerateContentConfig(max_output_tokens=10)
    )

    print(f"[OK] Gemini 응답: {response.text}")
    print("\n[SUCCESS] Gemini API 정상 작동!")

except Exception as e:
    print(f"\n[ERROR] Gemini 오류: {e}")

# Claude 폴백 테스트
print("\n" + "=" * 70)
print("Claude API 폴백 테스트")
print("=" * 70)

try:
    from _shared.claude_client import chat as claude_chat

    claude_response = claude_chat(
        prompt="Say 'OK' in one word",
        system="답변은 한 단어로만 하세요",
        max_tokens=10
    )

    print(f"[OK] Claude 응답: {claude_response}")
    print("\n[SUCCESS] Claude API 정상 작동!")

except Exception as e:
    print(f"\n[ERROR] Claude 오류: {e}")

print("\n" + "=" * 70)
print("테스트 완료")
print("=" * 70)
print("\n이제 텔레그램 봇을 실행할 수 있습니다:")
print("  python projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py")
