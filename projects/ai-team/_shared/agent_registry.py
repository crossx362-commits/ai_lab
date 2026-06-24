#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Agent discovery and registry.

The registry is intentionally conservative: legacy compatibility wrappers can
exist on disk without being exposed as runnable agents.
"""

from __future__ import annotations

from pathlib import Path


def find_project_root() -> Path:
    current = Path(__file__).resolve()
    for parent in current.parents:
        if (parent / "projects" / "ai-team").exists():
            return parent
    return current.parents[4]


PROJECT_ROOT = find_project_root()
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
SKILLS_DIR = AI_TEAM_ROOT / "skills"

PREFERRED_SCRIPTS = {
    "시그널_분석가": "market_signal.py",
    "데이브_주식": "upbit_auto_trader.py",
    "레오_트레이더": "leo_aggressive_trader.py",
    "영숙_비서": "telegram_receiver.py",
    "예원_CEO": "yewon_dispatcher.py",
}

SLUGS = {
    "예원_CEO": "yewon",
    "영숙_비서": "youngsuk",
    "코다리_개발자": "kodari",
    "케빈_인프라": "kevin",
    "티모_디자이너": "timo",
    "시그널_분석가": "signal",
    "데이브_주식": "dave",
    "레오_트레이더": "leo",
    "경수_수사관": "kyungsu",
    "로율_변호사": "royul",
}

LEGACY_AGENT_DIRS = {
    "펄스_애널리스트",
    "펄스_전략가",
    "루나_디렉터",
    "아린_비주얼",
}


def _generate_slug(folder_name: str) -> str:
    if folder_name in SLUGS:
        return SLUGS[folder_name]
    return folder_name.split("_", 1)[0].lower()


def _detect_agent_type(filename: str) -> str:
    lower = filename.lower()
    if any(token in lower for token in ["trader", "trading", "receiver", "bot", "market", "intelligence"]):
        return "daemon"
    return "on-demand"


def _choose_main_script(agent_name: str, tools_dir: Path) -> Path | None:
    preferred = PREFERRED_SCRIPTS.get(agent_name)
    if preferred and (tools_dir / preferred).exists():
        return tools_dir / preferred

    candidates: list[Path] = []
    for pattern in ["*_receiver.py", "*_dispatcher.py", "*_trader.py", "*_intelligence.py", "*_manager.py"]:
        candidates.extend(tools_dir.glob(pattern))
    if candidates:
        return max(candidates, key=lambda path: path.stat().st_size)

    py_files = [path for path in tools_dir.glob("*.py") if path.name != "__init__.py" and not path.name.startswith("_")]
    if py_files:
        return max(py_files, key=lambda path: path.stat().st_size)
    return None


def scan_agents() -> dict[str, dict[str, str]]:
    agents: dict[str, dict[str, str]] = {}
    if not SKILLS_DIR.exists():
        return agents

    for agent_dir in sorted(SKILLS_DIR.iterdir(), key=lambda path: path.name):
        if not agent_dir.is_dir() or agent_dir.name in LEGACY_AGENT_DIRS:
            continue

        tools_dir = agent_dir / "tools"
        if not tools_dir.exists():
            continue

        main_script = _choose_main_script(agent_dir.name, tools_dir)
        if not main_script:
            continue

        relative_path = main_script.relative_to(SKILLS_DIR)
        slug = _generate_slug(agent_dir.name)
        agents[slug] = {
            "name": agent_dir.name,
            "script": str(relative_path).replace("\\", "/"),
            "path": str(main_script),
            "type": _detect_agent_type(main_script.name),
        }

    return agents


def get_agents() -> dict[str, dict[str, str]]:
    if not hasattr(get_agents, "_cache"):
        get_agents._cache = scan_agents()
    return get_agents._cache


def reload_agents() -> dict[str, dict[str, str]]:
    if hasattr(get_agents, "_cache"):
        delattr(get_agents, "_cache")
    return get_agents()


def get_daemon_agents() -> dict[str, dict[str, str]]:
    return {slug: info for slug, info in get_agents().items() if info["type"] == "daemon"}


if __name__ == "__main__":
    found = scan_agents()
    print(f"발견된 에이전트: {len(found)}개\n")
    for slug, info in found.items():
        print(f"[{slug}] {info['name']}")
        print(f"  스크립트: {info['script']}")
        print(f"  타입: {info['type']}")
