#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""전체 에이전트 스크립트 연결 상태 검증 (실제 파일명)"""

import sys
import os

sys.stdout.reconfigure(encoding='utf-8')

# 실제 존재하는 스크립트 기반으로 업데이트
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
        "yeongsuk_telegram_bot.py",
        "posting_scheduler.py",
        "register_upload_schedule.py",
        "reports_manager.py",
        "google_calendar.py",
        "google_calendar_write.py",
        "notion_summarizer.py",
        "youtube_recommender.py",
        "telegram_setup.py"
    ],
    "루나_디렉터": [
        "music_video_pipeline.py",
        "shorts_pipeline.py"
    ],
    "아린_관리자": [
        "auto_pipeline.py",
        "uploader.py",
        "prompt_crafter.py",
        "image_research.py"
    ],
    "가희_검수관": [
        "content_inspector.py",
        "fix_issues.py"
    ],
    "코다리_개발자": [
        "agent_health_check.py",
        "ollama_health_check.py",
        "telegram_health_check.py",
        "instagram_token_refresher.py",
        "mermaid_generator.py",
        "lint_test.py",
        "pack_apply.py",
        "pwa_setup.py",
        "web_init.py",
        "web_preview.py"
    ],
    "케빈_인프라": [
        "petnna_monitor.py",
        "vercel_manager.py",
        "supabase_manager.py",
        "sync_env_to_vercel.py",
        "test_env_vars.py",
        "test_env_loader_direct.py",
        "debug_env.py",
        "parse_env_test.py"
    ],
    "티모_디자이너": [
        "petnna_reviewer.py"
    ],
    "현빈_전략가": [
        "business_research.py",
        "deep_search_6h.py",
        "paypal_revenue.py"
    ],
    "경수_수사관": [
        "comment_forensics.py"
    ],
    "로율_변호사": [
        "tax_simulator.py"
    ]
}

PROJECT_ROOT = r"d:\ai_lab"
SKILLS_ROOT = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills")

def check_file_exists(agent_name: str, script_name: str) -> tuple[bool, str]:
    """파일 존재 여부 확인"""
    agent_path = os.path.join(SKILLS_ROOT, agent_name, "tools", script_name)
    exists = os.path.exists(agent_path)
    return exists, agent_path

def main():
    print("=" * 70)
    print("  🔍 전체 에이전트 스크립트 연결 상태 검증 (v2)")
    print("=" * 70)
    print()

    total_scripts = 0
    total_exists = 0

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
            else:
                status = "❌"

            agent_results.append({
                "script": script,
                "exists": exists,
                "status": status
            })

            print(f"  {status} {script}")

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

    # 에이전트별 요약
    print()
    print("=" * 70)
    print("📈 에이전트별 요약")
    print("=" * 70)
    for agent_name, agent_results in results.items():
        exists_count = sum(1 for r in agent_results if r["exists"])
        total_count = len(agent_results)
        status = "✅" if exists_count == total_count else "⚠️"
        print(f"  {status} {agent_name:15s}: {exists_count:2d}/{total_count:2d} ({exists_count/total_count*100:5.1f}%)")

    print()
    print("=" * 70)

if __name__ == "__main__":
    main()
