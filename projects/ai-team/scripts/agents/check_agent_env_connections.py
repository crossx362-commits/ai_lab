#!/usr/bin/env python3
"""
AI 에이전트 로컬 연결 체크 스크립트.

외부 API를 호출하지 않고, 에이전트 폴더/도구 파일/필수 환경변수 존재 여부만 확인합니다.
실제 API 연결 테스트는 같은 폴더의 test_agent_api_connections.py를 사용하세요.
"""

import os
import sys
import importlib.util
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[1]
WORKSPACE_ROOT = AI_TEAM_ROOT.parent.parent

if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env

load_env()


AGENTS = {
    "예원_CEO": {
        "path": "projects/ai-team/skills/예원_CEO",
        "required_env": ["GEMINI_API_KEY", "TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"],
        "optional_env": ["OLLAMA_URL"],
        "files": ["SKILL.md", "tools/yewon_dispatcher.py"],
    },
    "영숙_비서": {
        "path": "projects/ai-team/skills/영숙_비서",
        "required_env": [
            "GEMINI_API_KEY",
            "NOTION_API_KEY",
            "NOTION_DATABASE_ID",
            "TELEGRAM_BOT_TOKEN",
            "TELEGRAM_CHAT_ID",
        ],
        "optional_env": [],
        "files": ["SKILL.md", "tools/telegram_receiver.py", "tools/reports_manager.py"],
    },
    "경수_수사관": {
        "path": "projects/ai-team/skills/경수_수사관",
        "required_env": ["GEMINI_API_KEY", "YOUTUBE_API_KEY"],
        "optional_env": [],
        "files": ["SKILL.md", "tools/comment_forensics.py", "tools/content_inspector.py"],
    },
    "코다리_개발자": {
        "path": "projects/ai-team/skills/코다리_개발자",
        "required_env": ["TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"],
        "optional_env": ["OLLAMA_URL", "GEMINI_API_KEY"],
        "files": ["SKILL.md", "tools/agent_health_check.py", "tools/ollama_health_check.py"],
    },
    "티모_디자이너": {
        "path": "projects/ai-team/skills/티모_디자이너",
        "required_env": ["GEMINI_API_KEY"],
        "optional_env": ["OLLAMA_URL"],
        "files": ["SKILL.md", "tools/petnna_reviewer.py"],
    },
    "케빈_인프라": {
        "path": "projects/ai-team/skills/케빈_인프라",
        "required_env": ["VERCEL_TOKEN", "SUPABASE_URL", "SUPABASE_ANON_KEY", "TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"],
        "optional_env": ["VERCEL_TEAM_ID", "BLOB_READ_WRITE_TOKEN", "CRON_SECRET"],
        "files": ["SKILL.md", "tools/vercel_manager.py", "tools/petnna_monitor.py", "tools/supabase_manager.py"],
    },
    "현빈_전략가": {
        "path": "projects/ai-team/skills/현빈_전략가",
        "required_env": ["GEMINI_API_KEY"],
        "optional_env": ["YOUTUBE_API_KEY", "PAYPAL_CLIENT_ID", "PAYPAL_CLIENT_SECRET"],
        "files": ["SKILL.md", "tools/business_research.py", "tools/crypto_market_intelligence.py"],
    },
    "로율_변호사": {
        "path": "projects/ai-team/skills/로율_변호사",
        "required_env": ["GEMINI_API_KEY"],
        "optional_env": [],
        "files": ["SKILL.md", "tools/tax_simulator.py"],
    },
    "데이브_주식": {
        "path": "projects/ai-team/skills/데이브_주식",
        "required_env": ["GEMINI_API_KEY"],
        "optional_env": ["UPBIT_ACCESS_KEY", "UPBIT_SECRET_KEY", "NOTION_API_KEY", "NOTION_DATABASE_ID"],
        "files": ["SKILL.md", "tools/stock_analyzer.py", "tools/upbit_analyzer.py", "tools/upbit_auto_trader.py"],
        "python_modules": ["pyupbit"],
    },
    "레오_트레이더": {
        "path": "projects/ai-team/skills/레오_트레이더",
        "required_env": [],
        "optional_env": ["UPBIT_ACCESS_KEY", "UPBIT_SECRET_KEY", "TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"],
        "files": ["SKILL.md", "tools/leo_aggressive_trader.py", "tools/leo_learning_system.py"],
        "python_modules": ["pyupbit"],
    },
}


def check_env_var(var_name: str) -> tuple[bool, str]:
    value = os.getenv(var_name)
    if not value:
        return False, "NOT SET"
    return True, f"SET ({len(value)} chars)"


def check_path(path: str) -> bool:
    return (WORKSPACE_ROOT / path).exists()


def check_module(module_name: str) -> bool:
    return importlib.util.find_spec(module_name) is not None


def print_env_group(title: str, env_vars: list[str], required: bool) -> tuple[int, int, list[str]]:
    passed = 0
    failed = 0
    issues = []

    if not env_vars:
        return passed, failed, issues

    print(f"   {title} ({len(env_vars)}개):")
    for env_var in env_vars:
        exists, status = check_env_var(env_var)
        if exists:
            print(f"      OK  {env_var}: {status}")
            passed += 1
        elif required:
            print(f"      MISS {env_var}: {status}")
            failed += 1
            issues.append(f"Missing env: {env_var}")
        else:
            print(f"      OPT {env_var}: {status}")

    return passed, failed, issues


def main() -> int:
    print("=" * 70)
    print("  AI 에이전트 로컬 연결 체크")
    print("=" * 70)
    print(f"Workspace: {WORKSPACE_ROOT}")
    print()

    total_required_checks = 0
    passed_checks = 0
    failed_checks = 0
    agent_status = {}

    for agent_name, config in AGENTS.items():
        print(f"[{agent_name}]")
        print("-" * 70)

        agent_passed = 0
        agent_failed = 0
        issues = []

        if check_path(config["path"]):
            print(f"   OK  folder: {config['path']}")
            agent_passed += 1
            passed_checks += 1
        else:
            print(f"   MISS folder: {config['path']}")
            agent_failed += 1
            failed_checks += 1
            issues.append(f"Missing folder: {config['path']}")
        total_required_checks += 1

        env_passed, env_failed, env_issues = print_env_group("required env", config["required_env"], True)
        agent_passed += env_passed
        agent_failed += env_failed
        issues.extend(env_issues)
        total_required_checks += len(config["required_env"])
        passed_checks += env_passed
        failed_checks += env_failed

        print_env_group("optional env", config["optional_env"], False)

        required_files = config.get("files", [])
        if required_files:
            print(f"   files ({len(required_files)}개):")
        for relative_file in required_files:
            path = f"{config['path']}/{relative_file}"
            total_required_checks += 1
            if check_path(path):
                print(f"      OK  {relative_file}")
                agent_passed += 1
                passed_checks += 1
            else:
                print(f"      MISS {relative_file}")
                agent_failed += 1
                failed_checks += 1
                issues.append(f"Missing file: {path}")

        workspace_files = config.get("workspace_files", [])
        if workspace_files:
            print(f"   workspace files ({len(workspace_files)}개):")
        for path in workspace_files:
            total_required_checks += 1
            if check_path(path):
                print(f"      OK  {path}")
                agent_passed += 1
                passed_checks += 1
            else:
                print(f"      MISS {path}")
                agent_failed += 1
                failed_checks += 1
                issues.append(f"Missing workspace file: {path}")

        python_modules = config.get("python_modules", [])
        if python_modules:
            print(f"   python modules ({len(python_modules)}개):")
        for module_name in python_modules:
            total_required_checks += 1
            if check_module(module_name):
                print(f"      OK  {module_name}")
                agent_passed += 1
                passed_checks += 1
            else:
                print(f"      MISS {module_name}")
                agent_failed += 1
                failed_checks += 1
                issues.append(f"Missing python module: {module_name}")

        status_text = "정상" if agent_failed == 0 else f"{agent_failed}개 확인 필요"
        agent_status[agent_name] = (agent_failed == 0, status_text, issues)
        print(f"   => {status_text} (통과 {agent_passed}, 실패 {agent_failed})")
        print()

    print("=" * 70)
    print("  전체 요약")
    print("=" * 70)
    for agent_name, (ok, status_text, issues) in agent_status.items():
        marker = "OK" if ok else "CHECK"
        print(f"{marker:5} {agent_name}: {status_text}")
        for issue in issues:
            print(f"      - {issue}")

    pass_rate = (passed_checks / total_required_checks * 100) if total_required_checks else 100
    print()
    print(f"총 에이전트: {len(AGENTS)}개")
    print(f"필수 체크: {total_required_checks}개")
    print(f"통과: {passed_checks}개 ({pass_rate:.1f}%)")
    print(f"실패: {failed_checks}개")

    return 0 if failed_checks == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
