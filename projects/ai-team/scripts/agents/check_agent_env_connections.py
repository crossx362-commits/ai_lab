#!/usr/bin/env python3
"""
AI 에이전트 환경변수 연결 체크 스크립트
모든 에이전트가 필요한 환경변수를 올바르게 로드하는지 검증합니다.
"""

import os
import sys
import json

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

# 에이전트 정의
AGENTS = {
    "루나_디렉터": {
        "path": "projects/ai-team/skills/루나_디렉터/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "YOUTUBE_API_KEY",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [
            "GEMINI_MUSIC_KEY",
            "OLLAMA_URL",
        ],
        "files": [
            "client_secret.json",  # 최상위 폴더
            "youtube_token.pickle",  # tools 폴더
        ]
    },
    "아린_관리자": {
        "path": "projects/ai-team/skills/아린_관리자/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "INSTAGRAM_APP_ID",
            "INSTAGRAM_APP_SECRET",
            "INSTAGRAM_ACCESS_TOKEN",
            "INSTAGRAM_ACCOUNT_ID",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [
            "OLLAMA_URL",
        ],
        "files": []
    },
    "가희_검수관": {
        "path": "projects/ai-team/skills/가희_검수관/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "YOUTUBE_API_KEY",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [],
        "files": []
    },
    "경수_수사관": {
        "path": "projects/ai-team/skills/경수_수사관/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "YOUTUBE_API_KEY",
        ],
        "optional_env": [],
        "files": []
    },
    "현빈_실장": {
        "path": "projects/ai-team/skills/현빈_실장/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "YOUTUBE_API_KEY",
        ],
        "optional_env": [],
        "files": []
    },
    "예원_CEO": {
        "path": "projects/ai-team/skills/예원_CEO/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [
            "OLLAMA_URL",
        ],
        "files": []
    },
    "영숙_비서": {
        "path": "projects/ai-team/skills/영숙_비서/tools",
        "required_env": [
            "GEMINI_API_KEY",
            "NOTION_API_KEY",
            "NOTION_DATABASE_ID",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [],
        "files": []
    },
}


def check_env_var(var_name: str) -> tuple[bool, str]:
    """환경 변수 체크"""
    value = os.getenv(var_name)
    if not value:
        return False, "NOT SET"
    if len(value) < 10:
        return True, f"SET (short: {len(value)} chars)"
    return True, f"SET ({value[:20]}...)"


def check_file(file_path: str, agent_path: str = None) -> bool:
    """파일 존재 체크"""
    workspace_root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
    # 최상위 폴더 파일 (client_secret.json)
    if file_path == "client_secret.json":
        root_path = os.path.join(workspace_root, file_path)
        return os.path.exists(root_path)

    # 에이전트 폴더 파일
    if agent_path:
        full_path = os.path.join(workspace_root, agent_path, file_path)
        return os.path.exists(full_path)

    return False


def main():
    print("=" * 70)
    print("  AI 에이전트 환경변수 연결 체크")
    print("=" * 70)
    print()

    # 전체 통계
    total_agents = len(AGENTS)
    total_checks = 0
    passed_checks = 0
    failed_checks = 0

    # 에이전트별 체크
    agent_status = {}

    for agent_name, config in AGENTS.items():
        print(f"🤖 {agent_name}")
        print("-" * 70)

        agent_passed = 0
        agent_failed = 0
        issues = []

        # 필수 환경 변수 체크
        print(f"   📋 필수 환경 변수 ({len(config['required_env'])}개):")
        for env_var in config["required_env"]:
            exists, status = check_env_var(env_var)
            total_checks += 1

            if exists:
                print(f"      ✅ {env_var}: {status}")
                agent_passed += 1
                passed_checks += 1
            else:
                print(f"      ❌ {env_var}: {status}")
                agent_failed += 1
                failed_checks += 1
                issues.append(f"Missing: {env_var}")

        # 선택 환경 변수 체크
        if config["optional_env"]:
            print(f"   📋 선택 환경 변수 ({len(config['optional_env'])}개):")
            for env_var in config["optional_env"]:
                exists, status = check_env_var(env_var)
                if exists:
                    print(f"      ✅ {env_var}: {status}")
                else:
                    print(f"      ⚠️  {env_var}: {status} (선택사항)")

        # 파일 체크
        if config["files"]:
            print(f"   📁 필수 파일 ({len(config['files'])}개):")
            for file_name in config["files"]:
                total_checks += 1
                exists = check_file(file_name, config["path"])

                if exists:
                    print(f"      ✅ {file_name}: 존재")
                    agent_passed += 1
                    passed_checks += 1
                else:
                    print(f"      ❌ {file_name}: 없음")
                    agent_failed += 1
                    failed_checks += 1
                    issues.append(f"Missing file: {file_name}")

        # 에이전트 상태 요약
        if agent_failed == 0:
            status_icon = "✅"
            status_text = "모두 정상"
        elif agent_failed < len(config["required_env"]):
            status_icon = "⚠️"
            status_text = f"{agent_failed}개 문제"
        else:
            status_icon = "❌"
            status_text = "심각한 문제"

        agent_status[agent_name] = {
            "icon": status_icon,
            "status": status_text,
            "passed": agent_passed,
            "failed": agent_failed,
            "issues": issues
        }

        print(f"   {status_icon} 상태: {status_text} (통과: {agent_passed}, 실패: {agent_failed})")
        print()

    # 전체 요약
    print("=" * 70)
    print("  전체 요약")
    print("=" * 70)
    print()

    # 에이전트별 상태
    for agent_name, status in agent_status.items():
        print(f"{status['icon']} {agent_name}: {status['status']}")
        if status['issues']:
            for issue in status['issues']:
                print(f"   - {issue}")

    print()
    print(f"📊 통계:")
    print(f"   - 총 에이전트: {total_agents}개")
    print(f"   - 총 체크: {total_checks}개")
    print(f"   - 통과: {passed_checks}개 ({passed_checks/total_checks*100:.1f}%)")
    print(f"   - 실패: {failed_checks}개 ({failed_checks/total_checks*100:.1f}%)")
    print()

    # 전체 상태 판정
    if failed_checks == 0:
        print("🎉 모든 에이전트가 정상적으로 연결되었습니다!")
        return 0
    elif failed_checks < total_checks * 0.2:
        print("⚠️  일부 에이전트에 문제가 있습니다. 확인이 필요합니다.")
        return 1
    else:
        print("❌ 심각한 문제가 있습니다. 환경 변수를 확인하세요.")
        return 2


if __name__ == "__main__":
    exit_code = main()
    sys.exit(exit_code)
