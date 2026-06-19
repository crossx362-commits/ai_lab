#!/usr/bin/env python3
"""에이전트 자동 검색 및 등록 시스템 (하드코딩 제거)"""
import os
import glob
from pathlib import Path

def find_project_root():
    """프로젝트 루트 찾기"""
    current = Path(__file__).resolve()
    for parent in current.parents:
        if (parent / "projects" / "ai-team").exists():
            return parent
    return current.parents[4]  # fallback

PROJECT_ROOT = find_project_root()
SKILLS_DIR = PROJECT_ROOT / "projects" / "ai-team" / "skills"

def scan_agents():
    """skills 폴더를 자동 스캔해서 모든 에이전트 검색"""
    agents = {}

    if not SKILLS_DIR.exists():
        return agents

    # skills 폴더의 모든 하위 폴더 검색
    for agent_dir in SKILLS_DIR.iterdir():
        if not agent_dir.is_dir():
            continue

        agent_name = agent_dir.name
        tools_dir = agent_dir / "tools"

        if not tools_dir.exists():
            continue

        # tools 폴더에서 주요 실행 파일 찾기
        main_scripts = []

        # 패턴 1: *_receiver.py, *_dispatcher.py, *_trader.py 등
        for pattern in ["*_receiver.py", "*_dispatcher.py", "*_trader.py",
                       "*_intelligence.py", "*_manager.py"]:
            main_scripts.extend(tools_dir.glob(pattern))

        # 패턴 2: 폴더명과 관련된 메인 스크립트
        if not main_scripts:
            # 모든 .py 파일 중 __init__.py가 아닌 것
            py_files = [f for f in tools_dir.glob("*.py")
                       if f.name != "__init__.py" and not f.name.startswith("_")]
            if py_files:
                # 가장 큰 파일을 메인으로 간주
                main_scripts = [max(py_files, key=lambda f: f.stat().st_size)]

        if main_scripts:
            # 프로젝트 루트 기준 상대 경로 (항상 / 사용)
            relative_path = main_scripts[0].relative_to(PROJECT_ROOT / "projects" / "ai-team" / "skills")
            script_path = str(relative_path).replace("\\", "/")  # Windows 경로 정규화

            # 영어 slug 생성 (폴더명 기반)
            slug = _generate_slug(agent_name)

            agents[slug] = {
                "name": agent_name,
                "script": script_path,
                "path": str(main_scripts[0]),
                "type": _detect_agent_type(main_scripts[0].name)
            }

    return agents

def _generate_slug(folder_name: str) -> str:
    """폴더명에서 영어 slug 생성"""
    # 한글명 → 영어명 매핑
    name_map = {
        "예원_CEO": "yewon",
        "영숙_비서": "youngsuk",
        "코다리_개발자": "kodari",
        "케빈_인프라": "kevin",
        "티모_디자이너": "timo",
        "현빈_전략가": "hyunbin",
        "데이브_주식": "dave",
        "레오_트레이더": "leo",
        "경수_수사관": "kyungsu",
        "로율_변호사": "royul",
    }

    # 매핑에 있으면 사용
    if folder_name in name_map:
        return name_map[folder_name]

    # 없으면 첫 번째 단어만 소문자로
    parts = folder_name.split("_")
    return parts[0].lower()

def _detect_agent_type(filename: str) -> str:
    """파일명으로 에이전트 타입 감지"""
    if "trader" in filename or "trading" in filename:
        return "daemon"
    elif "receiver" in filename or "bot" in filename:
        return "daemon"
    elif "intelligence" in filename or "market" in filename:
        return "daemon"
    else:
        return "on-demand"

def get_agents():
    """캐시된 에이전트 목록 반환"""
    if not hasattr(get_agents, "_cache"):
        get_agents._cache = scan_agents()
    return get_agents._cache

def reload_agents():
    """에이전트 목록 재스캔"""
    if hasattr(get_agents, "_cache"):
        delattr(get_agents, "_cache")
    return get_agents()

def get_daemon_agents():
    """daemon으로 실행되는 에이전트만 반환"""
    all_agents = get_agents()
    return {k: v for k, v in all_agents.items() if v["type"] == "daemon"}

if __name__ == "__main__":
    agents = scan_agents()
    print(f"발견된 에이전트: {len(agents)}개\n")
    for slug, info in agents.items():
        print(f"[{slug}] {info['name']}")
        print(f"  스크립트: {info['script']}")
        print(f"  타입: {info['type']}")
        print()
