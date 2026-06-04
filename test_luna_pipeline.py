#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
루나 파이프라인 테스트 스크립트
"""
import sys
import os

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

# 경로 설정
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'projects', 'ai-team', 'skills', '루나_디렉터', 'tools'))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'projects', 'ai-team'))

print("=" * 60)
print("  루나 파이프라인 진단 테스트")
print("=" * 60)

# 1. 환경 변수 확인
print("\n1️⃣ 환경 변수 확인...")
try:
    from _shared.env_loader import load_env
    load_env()

    critical_vars = ['GEMINI_API_KEY', 'YOUTUBE_API_KEY', 'TELEGRAM_BOT_TOKEN', 'TELEGRAM_CHAT_ID']
    all_set = True
    for var in critical_vars:
        val = os.getenv(var, '')
        status = '✅' if val else '❌'
        print(f"  {status} {var}: {'SET' if val else 'NOT SET'}")
        if not val:
            all_set = False
    if all_set:
        print("  ✅ 모든 환경 변수 설정됨")
    else:
        print("  ⚠️  일부 환경 변수 누락")
except Exception as e:
    print(f"  ❌ 환경 변수 로드 실패: {e}")

# 2. 필수 모듈 import 확인
print("\n2️⃣ 필수 모듈 import 확인...")
modules_to_test = [
    ("src.lyria_music_generator", "LyriaMusicGenerator"),
    ("src.trend_analyzer", "TrendAnalyzer"),
    ("src.video_generator", "VideoGenerator"),
    ("src.youtube_uploader", "YouTubeUploader"),
    ("_shared.telegram_notifier", "send_telegram_message"),
    ("_shared.ollama_client", "chat"),
    ("_shared.ffmpeg_utils", "get_ffmpeg_path"),
]

all_imports_ok = True
for module_name, obj_name in modules_to_test:
    try:
        module = __import__(module_name, fromlist=[obj_name])
        obj = getattr(module, obj_name)
        print(f"  ✅ {module_name}.{obj_name}")
    except Exception as e:
        print(f"  ❌ {module_name}.{obj_name}: {e}")
        all_imports_ok = False

if all_imports_ok:
    print("  ✅ 모든 모듈 import 성공")
else:
    print("  ⚠️  일부 모듈 import 실패")

# 3. FFmpeg 확인
print("\n3️⃣ FFmpeg 확인...")
try:
    from _shared.ffmpeg_utils import get_ffmpeg_path, get_ffprobe_path
    ffmpeg = get_ffmpeg_path()
    ffprobe = get_ffprobe_path()
    print(f"  FFmpeg: {ffmpeg}")
    print(f"  FFprobe: {ffprobe}")

    import subprocess
    result = subprocess.run([ffmpeg, "-version"], capture_output=True, text=True, timeout=5)
    if result.returncode == 0:
        version_line = result.stdout.split('\n')[0]
        print(f"  ✅ {version_line}")
    else:
        print(f"  ⚠️  FFmpeg 실행 실패")
except Exception as e:
    print(f"  ❌ FFmpeg 확인 실패: {e}")

# 4. Ollama 연결 확인
print("\n4️⃣ Ollama 연결 확인...")
try:
    from _shared.ollama_client import is_available, chat
    if is_available():
        print("  ✅ Ollama 서버 연결 성공")
        # 간단한 테스트
        response = chat("Say 'OK' only", task="test", max_tokens=10)
        if response:
            print(f"  ✅ Ollama 응답: {response[:50]}")
        else:
            print("  ⚠️  Ollama 응답 없음")
    else:
        print("  ⚠️  Ollama 서버 연결 실패")
except Exception as e:
    print(f"  ❌ Ollama 확인 실패: {e}")

# 5. 체크포인트 파일 확인
print("\n5️⃣ 체크포인트 상태 확인...")
try:
    checkpoint_path = os.path.join(os.path.dirname(__file__), 'reports', 'uploads', 'luna', 'music_video_checkpoint.json')
    if os.path.exists(checkpoint_path):
        import json
        import datetime
        with open(checkpoint_path, 'r', encoding='utf-8') as f:
            cp = json.load(f)
        print(f"  ⚠️  체크포인트 존재: {checkpoint_path}")
        print(f"  - 단계: {cp.get('step', 'unknown')}")
        print(f"  - 저장 시간: {cp.get('saved_at', 'unknown')}")

        # 체크포인트 나이 확인
        saved_at = datetime.datetime.fromisoformat(cp.get('saved_at', '2000-01-01'))
        age_hours = (datetime.datetime.now() - saved_at).total_seconds() / 3600
        print(f"  - 경과 시간: {age_hours:.1f}시간")

        if age_hours > 36:
            print("  ⚠️  체크포인트가 36시간 이상 경과 (자동 삭제 대상)")
        else:
            print("  ✅ 체크포인트가 유효함 (파이프라인 재개 가능)")
    else:
        print("  ✅ 체크포인트 없음 (새로운 실행 가능)")
except Exception as e:
    print(f"  ❌ 체크포인트 확인 실패: {e}")

# 6. 요약
print("\n" + "=" * 60)
print("  진단 완료")
print("=" * 60)
print("\n다음 단계:")
print("  - 모든 체크가 ✅ 이면: 파이프라인 실행 가능")
print("  - ⚠️  경고가 있으면: 해당 항목 확인 필요")
print("  - ❌ 오류가 있으면: 해당 모듈/설정 수정 필요")
print()
