#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
영숙의 스케줄 관리 시스템
- 모든 에이전트 스케줄 중앙 관리
- 스케줄 시간 도래 시 CEO에게 보고 후 에이전트 지시
"""

import os
import sys
import json
import time
import datetime
import subprocess
from typing import Dict, List, Optional
from croniter import croniter

# UTF-8 인코딩
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.stderr.reconfigure(encoding='utf-8', errors='replace')

# 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.notify import send
from _shared.llm import is_available as lm_available

SCHEDULES_FILE = os.path.join(_here, "schedules.json")
LAST_RUN_FILE = os.path.join(_here, "last_run.json")


def load_schedules() -> List[Dict]:
    """스케줄 목록 로드"""
    if not os.path.exists(SCHEDULES_FILE):
        print(f"❌ 스케줄 파일이 없습니다: {SCHEDULES_FILE}")
        return []

    with open(SCHEDULES_FILE, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)
        return data.get('schedules', [])


def load_last_run() -> Dict:
    """마지막 실행 시간 로드"""
    if not os.path.exists(LAST_RUN_FILE):
        return {}

    with open(LAST_RUN_FILE, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def save_last_run(last_run: Dict):
    """마지막 실행 시간 저장"""
    with open(LAST_RUN_FILE, 'w', encoding='utf-8') as f:
        json.dump(last_run, f, ensure_ascii=False, indent=2)


def should_run(schedule: Dict, last_run_time: Optional[str]) -> bool:
    """스케줄 실행 여부 확인"""
    if not schedule.get('enabled', True):
        return False

    cron_expr = schedule['cron']
    now = datetime.datetime.now()

    # croniter로 다음 실행 시간 계산
    try:
        cron = croniter(cron_expr, now)
        next_run = cron.get_prev(datetime.datetime)

        # 마지막 실행 시간이 없으면 실행
        if not last_run_time:
            return True

        last_run_dt = datetime.datetime.fromisoformat(last_run_time)

        # 마지막 실행 후 다음 스케줄 시간이 도래했으면 실행
        if next_run > last_run_dt:
            return True

    except Exception as e:
        print(f"  ⚠️ Cron 파싱 실패 ({schedule['id']}): {e}")
        return False

    return False


def execute_schedule(schedule: Dict):
    """스케줄 실행 - CEO에게 보고 후 지시"""
    schedule_id = schedule['id']
    agent = schedule['agent']
    task = schedule['task']
    command = schedule['command']
    priority = schedule.get('priority', 'medium')

    now_kst = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    print(f"\n{'='*70}")
    print(f"⏰ [영숙 스케줄러] {now_kst}")
    print(f"{'='*70}")
    print(f"  스케줄 ID: {schedule_id}")
    print(f"  담당 에이전트: {agent}")
    print(f"  작업: {task}")
    print(f"  우선순위: {priority}")
    print(f"  명령: {command}")
    print(f"{'='*70}\n")

    # 스케줄 시간 도래 알림을 텔레그램으로 전송
    notify_msg = (
        f"⏰ **[영숙 스케줄러]** 정기 작업 알림\n\n"
        f"**에이전트**: {agent}\n"
        f"**작업**: {task}\n"
        f"**우선순위**: {priority}\n"
        f"**명령**: {command}\n"
        f"**시간**: {now_kst}"
    )
    send(notify_msg)
    print(f"  [영숙 → 사장님] 스케줄 알림 전송 완료")

    # v2.90 — python 스크립트 command는 실제 실행한다. 자체 데몬이 보고를
    # 전담하는 항목(소미 등)은 schedules.json에 "run": false 로 두어 중복 방지.
    if schedule.get("run", True) and command.strip().startswith("python"):
        try:
            subprocess.Popen(command, shell=True, cwd=PROJECT_ROOT)
            print(f"  [영숙] 명령 실행 시작: {command}")
        except Exception as exc:
            print(f"  [영숙] 명령 실행 실패: {exc}")
            send(f"⚠️ 스케줄 실행 실패 ({schedule_id}): {exc}")


def check_and_run_schedules():
    """모든 스케줄 확인 및 실행"""
    schedules = load_schedules()
    last_run = load_last_run()

    if not schedules:
        print("스케줄이 없습니다.")
        return

    now = datetime.datetime.now()
    now_str = now.isoformat()

    executed_count = 0

    for schedule in schedules:
        schedule_id = schedule['id']
        last_run_time = last_run.get(schedule_id)

        if should_run(schedule, last_run_time):
            try:
                execute_schedule(schedule)
                last_run[schedule_id] = now_str
                executed_count += 1

            except Exception as e:
                print(f"❌ 스케줄 실행 실패 ({schedule_id}): {e}")

    # 마지막 실행 시간 저장
    if executed_count > 0:
        save_last_run(last_run)
        print(f"\n✅ 총 {executed_count}개 스케줄 실행 완료")


def schedule_loop(interval_seconds: int = 60):
    """스케줄 루프 (백그라운드 실행)"""
    print("=" * 70)
    print("  🗓️  영숙의 스케줄 관리 시스템 시작")
    print("=" * 70)
    print(f"  체크 주기: {interval_seconds}초")
    print(f"  스케줄 파일: {SCHEDULES_FILE}")
    print("=" * 70)
    print()

    # 시작 메시지
    start_msg = (
        f"🗓️ **[영숙 스케줄 관리 시스템]**\n\n"
        f"모든 에이전트 스케줄 관리를 시작합니다.\n"
        f"스케줄 시간이 되면 CEO님께 보고하고 에이전트에게 지시하겠습니다.\n\n"
        f"**체크 주기**: {interval_seconds}초\n"
        f"**총 스케줄**: {len(load_schedules())}개"
    )
    send(start_msg)

    while True:
        try:
            check_and_run_schedules()
            time.sleep(interval_seconds)

        except KeyboardInterrupt:
            print("\n\n⏹️  영숙 스케줄러 종료")
            break

        except Exception as e:
            print(f"\n❌ 루프 오류: {e}")
            time.sleep(interval_seconds)


def list_schedules():
    """스케줄 목록 출력"""
    schedules = load_schedules()

    print("=" * 70)
    print("  📋 영숙의 스케줄 목록")
    print("=" * 70)
    print()

    if not schedules:
        print("스케줄이 없습니다.")
        return

    for idx, schedule in enumerate(schedules, 1):
        enabled = "✅" if schedule.get('enabled', True) else "❌"
        priority = schedule.get('priority', 'medium')

        print(f"{idx}. {enabled} [{priority.upper()}] {schedule['agent']} - {schedule['task']}")
        print(f"   Cron: {schedule['cron']}")
        print(f"   명령: {schedule['command']}")
        print()

    print(f"총 {len(schedules)}개 스케줄")
    print("=" * 70)


def main():
    """메인 함수"""
    import argparse

    parser = argparse.ArgumentParser(description="영숙의 스케줄 관리 시스템")
    parser.add_argument('--daemon', action='store_true', help='백그라운드 데몬 모드 (기본 60초 주기)')
    parser.add_argument('--interval', type=int, default=60, help='체크 주기 (초)')
    parser.add_argument('--list', action='store_true', help='스케줄 목록 출력')
    parser.add_argument('--check', action='store_true', help='스케줄 1회 체크 및 실행')

    args = parser.parse_args()

    if args.list:
        list_schedules()
    elif args.check:
        check_and_run_schedules()
    elif args.daemon:
        schedule_loop(args.interval)
    else:
        print("사용법:")
        print("  --daemon        백그라운드 실행")
        print("  --list          스케줄 목록")
        print("  --check         1회 체크")
        print("  --interval N    체크 주기 (초)")


if __name__ == "__main__":
    main()
