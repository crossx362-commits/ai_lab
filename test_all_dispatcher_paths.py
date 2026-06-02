#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""전체 에이전트 dispatcher 경로 검증"""

import sys
import os

sys.stdout.reconfigure(encoding='utf-8')

# yewon_dispatcher.py와 동일한 PROJECT_ROOT 계산
_here = r"d:\ai_lab\projects\ai-team\skills\예원_CEO\tools"
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))

print("=" * 70)
print("  🔍 전체 에이전트 Dispatcher 경로 검증")
print("=" * 70)
print(f"\nPROJECT_ROOT: {PROJECT_ROOT}\n")

# yewon_dispatcher.py에서 사용하는 모든 경로
PATHS_TO_CHECK = {
    "영숙_비서/notion_summarizer.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools", "notion_summarizer.py"),
    "현빈_전략가/business_research.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "tools", "business_research.py"),
    "케빈_인프라/vercel_manager.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools", "vercel_manager.py"),
    "로율_변호사/tax_simulator.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "로율_변호사", "tools", "tax_simulator.py"),
    "루나_디렉터/music_video_pipeline.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "music_video_pipeline.py"),
    "아린_관리자/auto_pipeline.py": os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "아린_관리자", "tools", "auto_pipeline.py"),
}

print("📋 경로 검증 결과:")
print("-" * 70)

all_ok = True
for name, path in PATHS_TO_CHECK.items():
    exists = os.path.exists(path)
    status = "✅" if exists else "❌"

    if not exists:
        all_ok = False

    print(f"{status} {name}")
    if not exists:
        print(f"   경로: {path}")

print()
print("=" * 70)

if all_ok:
    print("✅ 모든 dispatcher 경로 정상!")
else:
    print("❌ 일부 경로에 문제가 있습니다.")
    print("\n권장 조치:")
    print("1. 파일이 실제로 존재하는지 확인")
    print("2. 파일명이 정확한지 확인")
    print("3. yewon_dispatcher.py의 경로 수정")

print("=" * 70)
