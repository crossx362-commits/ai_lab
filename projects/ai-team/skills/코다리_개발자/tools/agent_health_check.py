"""
agent_health_check.py — 코다리: 전체 에이전트 상태 및 시스템 자원 통합 점검
에이전트별 리서치 메모리 갱신 여부 및 시스템 부하(CPU/RAM)를 체크하여 보고합니다.
"""
import os
import sys
import datetime
import json

# 프로젝트 루트 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
from _shared.env_loader import load_env as _load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.resource_utils import get_resource_report_html, get_heavy_processes_report

def _check_file_freshness(file_path, max_hours=24):
    """파일의 마지막 수정 시간을 확인하여 신선도 판단"""
    if not os.path.exists(file_path):
        return False, "데이터 없음"
    
    mtime = datetime.datetime.fromtimestamp(os.path.getmtime(file_path))
    age = (datetime.datetime.now() - mtime).total_seconds() / 3600
    
    if age > max_hours:
        return False, f"지연 ({age:.1f}h 전)"
    return True, "정상"

def run_check():
    """시스템 리소스 및 에이전트 활동 상태 점검 실행"""
    _load_env()
    
    # 1. 시스템 리소스 리포트 (CPU/RAM 사용률)
    resource_report = get_resource_report_html()
    
    # 리소스가 '위험(🔴)' 상태일 때만 무거운 프로세스 목록을 추가 수집
    if "🔴" in resource_report:
        resource_report += "\n" + get_heavy_processes_report()

    # 2. 에이전트별 주요 메모리 파일 점검
    agents_to_check = {
        "루나 (리서치)": os.path.join(_root, "reports", "research", "luna_research.json"),
        "현빈 (비즈니스)": os.path.join(_root, "reports", "research", "hyunbin_research.json"),
        "아린 (인스타)": os.path.join(_root, "reports", "research", "arin_research.json"),
    }
    
    status_lines = []
    has_issues = False
    
    for name, path in agents_to_check.items():
        ok, msg = _check_file_freshness(path)
        icon = "🟢" if ok else "🟡"
        if not ok: has_issues = True
        status_lines.append(f"{icon} <b>{name}</b>: {msg}")

    # 3. 통합 리포트 생성 및 전송
    full_report = (
        f"🛠️ <b>[코다리] 에이전트 종합 헬스체크</b>\n\n"
        f"{resource_report}\n\n"
        f"👥 <b>에이전트 활동 현황:</b>\n" + "\n".join(status_lines)
    )

    print(full_report.replace("<b>", "").replace("</b>", "")) # 터미널용
    
    # 리소스가 위험하거나 에이전트 지연 시 텔레그램 발송
    if "🔴" in resource_report or has_issues:
        send_telegram_message(full_report)

if __name__ == "__main__":
    run_check()
