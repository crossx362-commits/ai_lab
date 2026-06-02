#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""주요 에이전트 스크립트 import 검증"""

import sys
import os
import subprocess

sys.stdout.reconfigure(encoding='utf-8')

PROJECT_ROOT = r"d:\ai_lab"
SKILLS_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills")

# 주요 파이프라인 스크립트만 테스트 (실행 시 문제될 수 있는 것 제외)
CRITICAL_SCRIPTS = {
    "예원_CEO": ["yewon_dispatcher.py"],
    "영숙_비서": ["telegram_receiver.py"],
    "루나_디렉터": ["music_video_pipeline.py"],
    "아린_관리자": ["auto_pipeline.py", "uploader.py", "prompt_crafter.py"],
    "가희_검수관": ["content_inspector.py"],
    "코다리_개발자": ["agent_health_check.py", "ollama_health_check.py"],
}

def test_import(agent_name: str, script_name: str) -> tuple[bool, str]:
    """스크립트 import 테스트 (syntax check만)"""
    script_path = os.path.join(SKILLS_ROOT, agent_name, "tools", script_name)

    try:
        # Python -m py_compile로 syntax만 체크
        result = subprocess.run(
            ["python", "-m", "py_compile", script_path],
            capture_output=True,
            text=True,
            timeout=5,
            encoding='utf-8',
            errors='replace'
        )

        if result.returncode == 0:
            return True, "✅ Syntax OK"
        else:
            error = result.stderr[:200] if result.stderr else "Unknown error"
            return False, f"❌ Syntax Error: {error}"

    except subprocess.TimeoutExpired:
        return False, "❌ Timeout"
    except Exception as e:
        return False, f"❌ {str(e)[:100]}"

def main():
    print("=" * 70)
    print("  🔍 주요 에이전트 스크립트 Import 검증")
    print("=" * 70)
    print()

    total = 0
    passed = 0

    for agent_name, scripts in CRITICAL_SCRIPTS.items():
        print(f"\n📋 {agent_name}")
        print("-" * 70)

        for script in scripts:
            total += 1
            success, msg = test_import(agent_name, script)

            if success:
                passed += 1

            print(f"  {msg:50s} | {script}")

    print()
    print("=" * 70)
    print("📊 최종 결과")
    print("=" * 70)
    print(f"  총 테스트: {total}개")
    print(f"  통과: {passed}개 ({passed/total*100:.1f}%)")
    print(f"  실패: {total - passed}개")

    if passed == total:
        print()
        print("✅ 모든 주요 스크립트 정상!")
    else:
        print()
        print("⚠️  일부 스크립트에 문제가 있습니다.")

    print("=" * 70)

if __name__ == "__main__":
    main()
