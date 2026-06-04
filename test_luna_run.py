#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
루나 파이프라인 실행 테스트 (resource_utils 포커스)
"""
import sys
import os

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

# 경로 설정
_here = os.path.join(os.getcwd(), 'projects', 'ai-team', 'skills', '루나_디렉터', 'tools')
_ai_team_root = os.path.abspath(os.path.join(_here, '..', '..', '..'))
sys.path.insert(0, _ai_team_root)
sys.path.insert(0, _here)

print("=" * 60)
print("  루나 파이프라인 실행 테스트 (resource_utils 중점)")
print("=" * 60)

try:
    # 1. 모든 import
    print("\n1️⃣ 모듈 import...")
    from _shared.env_loader import load_env, find_project_root
    from src.trend_analyzer import generate_music_prompt_from_title
    from src.lyria_music_generator import LyriaMusicGenerator
    from _shared.resource_utils import wait_for_resources
    from _shared.telegram_notifier import send_telegram_message
    print("   ✅ 모든 모듈 import 성공")

    # 2. 환경 로드
    print("\n2️⃣ 환경 설정 로드...")
    load_env()
    _root = find_project_root(_here)
    print(f"   ✅ 프로젝트 루트: {_root}")

    # 3. 체크포인트 로드
    print("\n3️⃣ 체크포인트 복원...")
    import json
    checkpoint_path = os.path.join(_root, 'reports', 'uploads', 'luna', 'music_video_checkpoint.json')
    with open(checkpoint_path, 'r', encoding='utf-8') as f:
        cp = json.load(f)

    theme = cp['theme']
    title = theme.get('title', '').strip('"')
    keyword = theme.get('keyword', '')

    print(f"   단계: {cp.get('step')}")
    print(f"   제목: {title}")
    print(f"   키워드: {keyword}")
    print(f"   ✅ 체크포인트 로드 성공")

    # 4. resource_utils 테스트
    print("\n4️⃣ resource_utils.wait_for_resources() 테스트...")
    try:
        wait_for_resources(task_name="테스트 작업", cpu_limit=85, ram_limit=85, check_interval=1)
        print("   ✅ wait_for_resources 정상 작동")
    except Exception as e:
        print(f"   ❌ wait_for_resources 실패: {e}")
        raise

    # 5. 음악 프롬프트 생성 (파이프라인의 다음 단계)
    print("\n5️⃣ 음악 프롬프트 생성...")
    try:
        music_prompt = generate_music_prompt_from_title(title, keyword)
        print(f"   ✅ 프롬프트: {music_prompt[:100]}...")
    except Exception as e:
        print(f"   ❌ 프롬프트 생성 실패: {e}")
        raise

    # 6. Lyria 객체 생성 테스트
    print("\n6️⃣ LyriaMusicGenerator 초기화...")
    try:
        music_gen = LyriaMusicGenerator()
        print("   ✅ LyriaMusicGenerator 초기화 성공")
    except Exception as e:
        print(f"   ❌ LyriaMusicGenerator 초기화 실패: {e}")
        raise

    print("\n" + "=" * 60)
    print("  ✅ 모든 테스트 통과!")
    print("=" * 60)
    print("\n결론: resource_utils 및 관련 모듈 정상 작동")
    print("파이프라인이 'theme' 단계에서 멈춘 이유:")
    print("  - 수동 중단되었거나")
    print("  - 외부 API (Lyria) 호출 실패")
    print("  - 시스템 리소스 부족으로 대기 중")

except Exception as e:
    print(f"\n❌ 테스트 실패: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
