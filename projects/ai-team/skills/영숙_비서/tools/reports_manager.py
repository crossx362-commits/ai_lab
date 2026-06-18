#!/usr/bin/env python3
"""
Reports 폴더 관리 도구 (reports_manager.py)
영숙 비서가 에이전트들의 리서치 보고서, 학습 로그, 작업 히스토리를 관리하고 Notion에 보고서를 전송합니다.
"""

import os
import sys
import json
import shutil
import glob
from datetime import datetime, timedelta
from pathlib import Path
from typing import List, Dict

# 프로젝트 루트 경로
ROOT_DIR = Path(__file__).parent.parent.parent.parent.parent
sys.path.insert(0, str(ROOT_DIR))
sys.path.insert(0, str(ROOT_DIR / "projects" / "ai-team"))

from _shared.knowledge_base import get_kb_dir
from _shared.notion_client import create_notion_page
from _shared.llm import ollama as ollama_chat

REPORTS_DIR = ROOT_DIR / "reports"
ARCHIVE_DIR = REPORTS_DIR / "archive"

def ensure_directories():
    """필요한 디렉토리 생성"""
    (REPORTS_DIR / "research").mkdir(parents=True, exist_ok=True)
    (REPORTS_DIR / "learning").mkdir(parents=True, exist_ok=True)
    (REPORTS_DIR / "history").mkdir(parents=True, exist_ok=True)
    ARCHIVE_DIR.mkdir(parents=True, exist_ok=True)


def get_file_age(file_path: Path) -> int:
    """파일 생성일로부터 경과 일수 계산"""
    mod_time = datetime.fromtimestamp(file_path.stat().st_mtime)
    age = (datetime.now() - mod_time).days
    return age


def archive_old_logs(days_threshold: int = 30) -> List[str]:
    """오래된 학습 로그를 아카이브"""
    archived = []
    learning_dir = REPORTS_DIR / "learning"

    if not learning_dir.exists():
        return archived

    for log_file in learning_dir.glob("*.jsonl"):
        age = get_file_age(log_file)

        if age > days_threshold:
            # 아카이브 폴더로 이동
            archive_name = f"{log_file.stem}_{datetime.now().strftime('%Y%m%d')}.jsonl"
            archive_path = ARCHIVE_DIR / archive_name

            shutil.move(str(log_file), str(archive_path))
            archived.append(f"{log_file.name} → archive/{archive_name}")

    return archived


def clean_duplicate_research() -> List[str]:
    """중복된 리서치 보고서 정리 (같은 에이전트의 오래된 버전 삭제)"""
    removed = []
    research_dir = REPORTS_DIR / "research"

    if not research_dir.exists():
        return removed

    # 에이전트별로 그룹화
    agent_files: Dict[str, List[Path]] = {}

    for research_file in research_dir.glob("*_research*.json"):
        agent_name = research_file.name.split("_research")[0]

        if agent_name not in agent_files:
            agent_files[agent_name] = []
        agent_files[agent_name].append(research_file)

    # 각 에이전트별로 최신 파일만 유지
    for agent, files in agent_files.items():
        if len(files) > 1:
            # 수정 시간 기준 정렬 (최신 파일이 마지막)
            files.sort(key=lambda f: f.stat().st_mtime)

            # 최신 파일을 제외한 나머지 삭제
            for old_file in files[:-1]:
                old_file.unlink()
                removed.append(f"{old_file.name} (오래된 버전)")

    return removed


def trim_history(max_entries: int = 100) -> Dict[str, int]:
    """히스토리 파일에서 최근 N개 항목만 유지"""
    trimmed = {}
    history_dir = REPORTS_DIR / "history"

    if not history_dir.exists():
        return trimmed

    for history_file in history_dir.glob("*.json"):
        try:
            with open(history_file, 'r', encoding='utf-8') as f:
                data = json.load(f)

            if isinstance(data, list) and len(data) > max_entries:
                original_count = len(data)
                trimmed_data = data[-max_entries:]

                with open(history_file, 'w', encoding='utf-8') as f:
                    json.dump(trimmed_data, f, ensure_ascii=False, indent=2)

                trimmed[history_file.name] = original_count - max_entries

        except Exception as e:
            print(f"⚠️ {history_file.name} 처리 실패: {e}")

    return trimmed


def generate_status_report() -> str:
    """Reports 폴더 현황 보고서 생성"""
    ensure_directories()

    report = []
    report.append("📊 **Reports 폴더 현황**\n")

    research_dir = REPORTS_DIR / "research"
    research_count = len(list(research_dir.glob("*.json"))) + len(list(research_dir.glob("*.md")))
    report.append(f"📝 리서치 보고서: {research_count}개")

    agent_research = {}
    for research_file in research_dir.glob("*_research*.json"):
        agent_name = research_file.name.split("_research")[0]
        mod_time = datetime.fromtimestamp(research_file.stat().st_mtime)
        agent_research[agent_name] = mod_time.strftime("%Y-%m-%d")

    if agent_research:
        report.append("\n**에이전트별 최근 리서치:**")
        for agent, date in sorted(agent_research.items()):
            report.append(f"  - {agent}: {date}")

    learning_dir = REPORTS_DIR / "learning"
    success_log = learning_dir / "success_log.jsonl"
    fail_log = learning_dir / "fail_log.jsonl"

    success_count = 0
    fail_count = 0

    if success_log.exists():
        with open(success_log, 'r', encoding='utf-8') as f:
            success_count = sum(1 for _ in f)

    if fail_log.exists():
        with open(fail_log, 'r', encoding='utf-8') as f:
            fail_count = sum(1 for _ in f)

    report.append(f"\n📚 학습 로그:")
    report.append(f"  - ✅ 성공: {success_count}건")
    report.append(f"  - ❌ 실패: {fail_count}건")

    archive_count = len(list(ARCHIVE_DIR.glob("*"))) if ARCHIVE_DIR.exists() else 0
    report.append(f"\n📦 아카이브: {archive_count}개")

    return "\n".join(report)


def cleanup_all(verbose: bool = True) -> str:
    """전체 정리 작업 실행"""
    ensure_directories()

    results = []
    results.append("🧹 **Reports 폴더 자동 정리 시작**\n")

    archived = archive_old_logs(days_threshold=30)
    if archived:
        results.append(f"📦 학습 로그 아카이브: {len(archived)}개")
        if verbose:
            for item in archived:
                results.append(f"  - {item}")
    else:
        results.append("📦 아카이브할 로그 없음")

    removed = clean_duplicate_research()
    if removed:
        results.append(f"\n🗑️ 중복 리서치 삭제: {len(removed)}개")
        if verbose:
            for item in removed:
                results.append(f"  - {item}")
    else:
        results.append("\n🗑️ 중복 파일 없음")

    trimmed = trim_history(max_entries=100)
    if trimmed:
        results.append(f"\n✂️ 히스토리 정리:")
        for filename, removed_count in trimmed.items():
            results.append(f"  - {filename}: {removed_count}개 항목 제거")
    else:
        results.append("\n✂️ 히스토리 정리 불필요")

    results.append("\n✅ 정리 완료!")

    return "\n".join(results)


def run_notion_report():
    """지식베이스 리서치를 취합하여 Notion에 리포트 발행"""
    kb_path = get_kb_dir()
    md_files = glob.glob(os.path.join(kb_path, "*.md"))
    
    if not md_files:
        return "영숙이에요! 아직 지식 베이스(Knowledge Base)에 수집된 리서치 결과가 없네요."
        
    md_files.sort(key=os.path.getmtime, reverse=True)
    recent_files = md_files[:5]
    
    combined_text = ""
    for mf in recent_files:
        with open(mf, "r", encoding="utf-8") as f:
            combined_text += f.read()[:1000] + "\n\n---\n\n"
            
    prompt = (
        "당신은 스마트 비서 '영숙'입니다. 다음은 여러 에이전트들이 방금까지 수집한 최신 지식 리서치 자료입니다.\n"
        "이 자료들을 CEO가 한눈에 파악하기 쉽게 '핵심 인사이트 요약 보고서'로 작성해 주세요.\n"
        "말투는 전문적이면서도 깔끔하게, 마크다운(글머리기호 등)을 적극 사용하세요.\n\n"
        f"{combined_text}"
    )
    
    print("  [영숙] 지식 통합 분석 중...")
    summary = ollama_chat(prompt, task="", max_tokens=1000)

    if not summary:
        return "❌ 리서치 자료를 분석하는 데 실패했습니다 (AI 응답 오류)."
        
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M")
    title = f"🧠 AI 팀 통합 리서치 리포트 ({now_str})"
    
    print("  [영숙] 노션(Notion) 슈퍼파워 툴 가동 중...")
    result = create_notion_page(title, summary)
    return result


def main():
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python reports_manager.py status    - 현황 보고")
        print("  python reports_manager.py cleanup   - 자동 정리 실행")
        print("  python reports_manager.py archive   - 오래된 로그만 아카이브")
        print("  python reports_manager.py notion    - Notion 리포트 발행")
        return

    command = sys.argv[1].lower()

    if command == "status":
        print(generate_status_report())
    elif command == "cleanup":
        print(cleanup_all(verbose=True))
        print("\n" + generate_status_report())
    elif command == "archive":
        ensure_directories()
        archived = archive_old_logs(days_threshold=30)
        if archived:
            print(f"📦 {len(archived)}개 로그 파일 아카이브 완료:")
            for item in archived:
                print(f"  - {item}")
        else:
            print("📦 아카이브할 파일 없음")
    elif command == "notion":
        print(run_notion_report())
    else:
        print(f"❌ 알 수 없는 명령어: {command}")


if __name__ == "__main__":
    main()
