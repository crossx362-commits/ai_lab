#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""전체 에이전트 스크립트 연결 상태 검증"""

import sys
import os
import importlib.util

sys.stdout.reconfigure(encoding='utf-8')

AGENTS = {
    "예원_CEO": [
        "evaluate_feedback.py",
        "daily_feedback_scheduler.py",
        "skill_auditor.py",
        "upload_manager.py",
        "yewon_dispatcher.py"
    ],
    "영숙_비서": [
        "telegram_receiver.py",
        "upload_manager.py"
    ],
    "루나_디렉터": [
        "music_video_pipeline.py",
        "shorts_pipeline.py"
    ],
    "아린_관리자": [
        "auto_pipeline.py",
        "shorts_pipeline.py",
        "uploader.py",
        "prompt_crafter.py"
    ],
    "가희_검수관": [
        "content_inspector.py",
        "scanner.py"
    ],
    "코다리_개발자": [
        "health_check.py"
    ],
    "케빈_인프라": [
        "monitor_petnna.py"
    ],
    "티모_디자이너": [
        "review_petnna.py"
    ],
    "현빈_전략가": [
        "market_research.py"
    ],
    "경수_수사관": [
        "detect_malicious.py"
    ],
    "로율_변호사": [
        "legal_review.py"
    ]
}

PROJECT_ROOT = r"d:\ai_lab"
SKILLS_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills")

def check_file_exists(agent_name: str, script_name: str) -> tuple[bool, str]:
    """파일 존재 여부 확인"""
    agent_path = os.path.join(SKILLS_ROOT, agent_name, "tools", script_name)
    exists = os.path.exists(agent_path)
    return exists, agent_path

def check_script_imports(agent_name: str, script_name: str) -> tuple[bool, str]:
    """스크립트 import 가능 여부 확인"""
    agent_path = os.path.join(SKILLS_ROOT, agent_name, "tools", script_name)

    if not os.path.exists(agent_path):
        return False, "파일 없음"

    try:
        # sys.path 설정
        tools_path = os.path.dirname(agent_path)
        if tools_path not in sys.path:
            sys.path.insert(0, tools_path)
        if PROJECT_ROOT not in sys.path:
            sys.path.insert(0, PROJECT_ROOT)

        # import 시도 (실행은 안 함)
        spec = importlib.util.spec_from_file_location(
            script_name.replace('.py', ''),
            agent_path
        )
        if spec and spec.loader:
            module = importlib.util.module_from_spec(spec)
            # exec_module은 실제 실행하므로 생략
            return True, "Import 가능"
        else:
            return False, "spec 생성 실패"
    except Exception as e:
        return False, str(e)[:100]

def main():
    print("=" * 70)
    print("  🔍 전체 에이전트 스크립트 연결 상태 검증")
    print("=" * 70)
    print()

    total_scripts = 0
    total_exists = 0
    total_ok = 0

    results = {}

    for agent_name, scripts in AGENTS.items():
        print(f"\n📋 {agent_name}")
        print("-" * 70)

        agent_results = []

        for script in scripts:
            total_scripts += 1
            exists, path = check_file_exists(agent_name, script)

            if exists:
                total_exists += 1
                status = "✅"
                msg = "존재"
            else:
                status = "❌"
                msg = "파일 없음"

            agent_results.append({
                "script": script,
                "exists": exists,
                "status": status,
                "message": msg,
                "path": path
            })

            print(f"  {status} {script:30s} | {msg}")

        results[agent_name] = agent_results

        # 에이전트별 통계
        exists_count = sum(1 for r in agent_results if r["exists"])
        print(f"  📊 {exists_count}/{len(scripts)} 스크립트 존재")

    # 전체 통계
    print()
    print("=" * 70)
    print("📊 전체 통계")
    print("=" * 70)
    print(f"  총 에이전트: {len(AGENTS)}개")
    print(f"  총 스크립트: {total_scripts}개")
    print(f"  존재하는 스크립트: {total_exists}개 ({total_exists/total_scripts*100:.1f}%)")
    print(f"  누락된 스크립트: {total_scripts - total_exists}개")

    # 누락된 스크립트 목록
    missing = []
    for agent_name, agent_results in results.items():
        for r in agent_results:
            if not r["exists"]:
                missing.append((agent_name, r["script"]))

    if missing:
        print()
        print("=" * 70)
        print("❌ 누락된 스크립트")
        print("=" * 70)
        for agent, script in missing:
            print(f"  • {agent}/{script}")
    else:
        print()
        print("✅ 모든 스크립트 정상 존재!")

    print()
    print("=" * 70)
    print("완료")
    print("=" * 70)

if __name__ == "__main__":
    main()
