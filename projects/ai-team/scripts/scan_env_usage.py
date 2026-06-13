#!/usr/bin/env python3
"""
환경변수 사용 현황 스캔 도구
전체 코드베이스에서 환경변수 사용을 자동으로 스캔하고 분석합니다.
"""

import os
import re
import json
from pathlib import Path
from collections import defaultdict
from typing import Dict, List, Set


def scan_python_file(file_path: str) -> Set[str]:
    """Python 파일에서 사용되는 환경변수 추출"""
    env_vars = set()

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # os.getenv("VAR_NAME") 패턴
        pattern1 = r'os\.getenv\(["\']([A-Z_]+)["\']\)'
        matches1 = re.findall(pattern1, content)
        env_vars.update(matches1)

        # os.environ["VAR_NAME"] 또는 os.environ.get("VAR_NAME") 패턴
        pattern2 = r'os\.environ(?:\.get)?\[["\']([A-Z_]+)["\']\]'
        matches2 = re.findall(pattern2, content)
        env_vars.update(matches2)

        # os.environ.get("VAR_NAME") 패턴
        pattern3 = r'os\.environ\.get\(["\']([A-Z_]+)["\']\)'
        matches3 = re.findall(pattern3, content)
        env_vars.update(matches3)

    except Exception as e:
        print(f"경고: {file_path} 읽기 실패 - {e}")

    return env_vars


def scan_directory(root_dir: str) -> Dict[str, List[str]]:
    """디렉토리 전체 스캔"""
    env_usage = defaultdict(list)  # {env_var: [file_paths]}
    file_usage = {}  # {file_path: [env_vars]}

    root_path = Path(root_dir)

    # Python 파일만 스캔
    for py_file in root_path.rglob("*.py"):
        # 제외 디렉토리
        if any(part in py_file.parts for part in ['.venv', 'node_modules', '__pycache__', '.git', 'venv']):
            continue

        file_path_str = str(py_file.relative_to(root_path))
        env_vars = scan_python_file(str(py_file))

        if env_vars:
            file_usage[file_path_str] = sorted(list(env_vars))
            for var in env_vars:
                env_usage[var].append(file_path_str)

    return dict(env_usage), file_usage


def extract_agent_name(file_path: str) -> str:
    """파일 경로에서 에이전트 이름 추출"""
    if "skills" in file_path:
        parts = file_path.split(os.sep)
        for i, part in enumerate(parts):
            if part == "skills" and i + 1 < len(parts):
                return parts[i + 1]

    if "_shared" in file_path:
        return "_shared (공용)"

    return "기타"


def generate_manifest(env_usage: Dict[str, List[str]], file_usage: Dict[str, List[str]]) -> Dict:
    """ENV_MANIFEST.json 생성"""

    # 에이전트별 환경변수 의존성
    agent_deps = defaultdict(set)
    for file_path, vars in file_usage.items():
        agent = extract_agent_name(file_path)
        agent_deps[agent].update(vars)

    # 환경변수별 상세 정보
    var_details = {}
    for var, files in env_usage.items():
        agents = set()
        for file_path in files:
            agents.add(extract_agent_name(file_path))

        var_details[var] = {
            "usage_count": len(files),
            "used_by_agents": sorted(list(agents)),
            "files": files
        }

    manifest = {
        "generated_at": "2026-06-02",
        "total_env_vars": len(env_usage),
        "total_files": len(file_usage),
        "environment_variables": var_details,
        "agent_dependencies": {agent: sorted(list(vars)) for agent, vars in agent_deps.items()}
    }

    return manifest


def generate_summary_report(manifest: Dict) -> str:
    """요약 보고서 생성"""
    lines = []
    lines.append("=" * 70)
    lines.append("환경변수 사용 현황 보고서")
    lines.append("=" * 70)
    lines.append("")

    lines.append(f"생성 일시: {manifest['generated_at']}")
    lines.append(f"총 환경변수: {manifest['total_env_vars']}개")
    lines.append(f"사용 파일 수: {manifest['total_files']}개")
    lines.append("")

    lines.append("-" * 70)
    lines.append("환경변수 사용 빈도 (상위 15개)")
    lines.append("-" * 70)

    # 사용 빈도순 정렬
    sorted_vars = sorted(
        manifest['environment_variables'].items(),
        key=lambda x: x[1]['usage_count'],
        reverse=True
    )

    for i, (var, info) in enumerate(sorted_vars[:15], 1):
        agents_str = ", ".join(info['used_by_agents'][:3])
        if len(info['used_by_agents']) > 3:
            agents_str += f" 외 {len(info['used_by_agents']) - 3}개"
        lines.append(f"{i:2}. {var:30} - {info['usage_count']:2}회 ({agents_str})")

    lines.append("")
    lines.append("-" * 70)
    lines.append("에이전트별 환경변수 의존성")
    lines.append("-" * 70)

    for agent, vars in sorted(manifest['agent_dependencies'].items()):
        lines.append(f"\n{agent}:")
        for var in vars:
            lines.append(f"  - {var}")

    lines.append("")
    lines.append("=" * 70)

    return "\n".join(lines)


def check_load_env_usage(root_dir: str, file_usage: Dict[str, List[str]]) -> Dict:
    """load_env() 사용 여부 확인"""
    results = {
        "files_using_env": [],
        "files_with_load_env": [],
        "files_without_load_env": []
    }

    root_path = Path(root_dir)

    for file_path, env_vars in file_usage.items():
        full_path = root_path / file_path

        try:
            with open(full_path, 'r', encoding='utf-8') as f:
                content = f.read()

            results["files_using_env"].append(file_path)

            # load_env 호출 확인
            if "load_env()" in content or "from _shared.env_loader import load_env" in content:
                results["files_with_load_env"].append(file_path)
            else:
                results["files_without_load_env"].append({
                    "file": file_path,
                    "env_vars": env_vars
                })
        except Exception as e:
            print(f"경고: {file_path} 확인 실패 - {e}")

    return results


if __name__ == "__main__":
    print("환경변수 사용 현황 스캔 시작...")
    print()

    # 프로젝트 루트
    root_dir = os.path.dirname(os.path.abspath(__file__))

    # 스캔 실행
    env_usage, file_usage = scan_directory(root_dir)

    print(f"스캔 완료: {len(env_usage)}개 환경변수, {len(file_usage)}개 파일")
    print()

    # Manifest 생성
    manifest = generate_manifest(env_usage, file_usage)

    # JSON 저장
    manifest_path = os.path.join(root_dir, "ENV_MANIFEST.json")
    with open(manifest_path, 'w', encoding='utf-8') as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    print(f"ENV_MANIFEST.json 생성 완료: {manifest_path}")

    # 요약 보고서
    summary = generate_summary_report(manifest)
    print()
    print(summary)

    # load_env() 사용 확인
    print()
    print("=" * 70)
    print("load_env() 사용 현황 분석")
    print("=" * 70)

    load_env_check = check_load_env_usage(root_dir, file_usage)

    print(f"\n환경변수 사용 파일: {len(load_env_check['files_using_env'])}개")
    print(f"load_env() 적용: {len(load_env_check['files_with_load_env'])}개")
    print(f"load_env() 미적용: {len(load_env_check['files_without_load_env'])}개")

    if load_env_check['files_without_load_env']:
        print("\nload_env() 미적용 파일 목록:")
        for item in load_env_check['files_without_load_env']:
            print(f"  - {item['file']}")
            print(f"    사용 환경변수: {', '.join(item['env_vars'])}")

    print()
    print("=" * 70)
    print("스캔 완료!")
