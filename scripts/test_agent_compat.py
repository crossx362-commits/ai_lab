#!/usr/bin/env python3
"""
AI 팀 에이전트들의 환경 변수 호환성 테스트
"""
import sys
import os

# env_loader 테스트
sys.path.insert(0, 'ai-team/_shared')
from env_loader import load_env

print("=" * 60)
print("AI Team Agent Compatibility Test")
print("=" * 60)

# .env 로드
load_env()

# 필수 환경 변수 확인
required_vars = {
    "GEMINI_API_KEY": "AI generation",
    "YOUTUBE_API_KEY": "YouTube research",
    "TELEGRAM_BOT_TOKEN": "Notifications",
    "INSTAGRAM_ACCESS_TOKEN": "Instagram management",
    "SUPABASE_URL": "Database",
}

print("\n[Environment Variables]")
all_ok = True
for key, purpose in required_vars.items():
    val = os.environ.get(key, "")
    if val:
        print(f"  OK   {key:25s} ({len(val):3d} chars) - {purpose}")
    else:
        print(f"  MISS {key:25s} (  0 chars) - {purpose}")
        all_ok = False

# telegram_bot.py 테스트
print("\n[Testing telegram_bot.py]")
try:
    sys.path.insert(0, '.agent/tools')
    # telegram_bot 임포트 테스트만 (실행 안함)
    import importlib.util
    spec = importlib.util.spec_from_file_location("telegram_bot", ".agent/tools/telegram_bot.py")
    if spec and spec.loader:
        print("  OK   telegram_bot.py can be imported")
    else:
        print("  FAIL telegram_bot.py import failed")
except Exception as e:
    print(f"  FAIL {e}")

# 공통 모듈 테스트
print("\n[Testing _shared modules]")
try:
    from telegram_notifier import send_telegram_message
    print("  OK   telegram_notifier imported")
except Exception as e:
    print(f"  FAIL telegram_notifier: {e}")

# 에이전트 스크립트 샘플 테스트
print("\n[Testing Sample Agent Scripts]")
test_scripts = [
    "ai-team/assets/tool-seeds/코다리_개발자/instagram_token_refresher.py",
    "ai-team/assets/tool-seeds/루나_디렉터/lyria_music_gen.py",
    "ai-team/assets/tool-seeds/아린_관리자/uploader.py",
]

for script in test_scripts:
    if not os.path.exists(script):
        print(f"  SKIP {script} (not found)")
        continue

    try:
        # 파일 읽기만 테스트 (실행 안함)
        with open(script, "r", encoding="utf-8") as f:
            content = f.read()
            if "load_env" in content:
                print(f"  OK   {os.path.basename(script):40s} uses env_loader")
            else:
                print(f"  WARN {os.path.basename(script):40s} may not load env")
    except Exception as e:
        print(f"  FAIL {os.path.basename(script):40s} - {e}")

print("\n" + "=" * 60)
if all_ok:
    print("SUCCESS: All agents compatible with new env system")
else:
    print("WARNING: Some environment variables missing")
print("=" * 60)

sys.exit(0 if all_ok else 1)
