#!/usr/bin/env python3
"""
텔레그램 봇 중복 프로세스 정리 스크립트
영숙(비서) 텔레그램 봇이 여러 개 실행될 경우 오래된 프로세스 종료
"""

import os
import sys
import psutil
from datetime import datetime

def find_telegram_bot_processes():
    """텔레그램 봇 프로세스 찾기"""
    telegram_processes = []

    for proc in psutil.process_iter(['pid', 'name', 'cmdline', 'create_time']):
        try:
            cmdline = proc.info['cmdline']
            if cmdline and any('telegram_receiver.py' in arg for arg in cmdline):
                telegram_processes.append({
                    'pid': proc.info['pid'],
                    'name': proc.info['name'],
                    'cmdline': ' '.join(cmdline),
                    'create_time': proc.info['create_time'],
                    'started_at': datetime.fromtimestamp(proc.info['create_time']).strftime('%Y-%m-%d %H:%M:%S')
                })
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue

    return telegram_processes

def cleanup_duplicate_bots(dry_run=False):
    """중복된 텔레그램 봇 정리"""
    processes = find_telegram_bot_processes()

    if not processes:
        print("✅ 실행 중인 텔레그램 봇이 없습니다.")
        return

    print(f"📊 발견된 텔레그램 봇: {len(processes)}개\n")

    if len(processes) == 1:
        proc = processes[0]
        print(f"✅ 텔레그램 봇 1개만 실행 중 (정상)")
        print(f"   PID: {proc['pid']}")
        print(f"   시작 시간: {proc['started_at']}")
        return

    # 시작 시간 기준으로 정렬 (최신 -> 오래된 순)
    processes.sort(key=lambda x: x['create_time'], reverse=True)

    # 가장 최신 프로세스만 유지, 나머지는 종료
    keep_process = processes[0]
    terminate_processes = processes[1:]

    print(f"✅ 유지할 프로세스:")
    print(f"   PID: {keep_process['pid']}")
    print(f"   시작 시간: {keep_process['started_at']}\n")

    print(f"❌ 종료할 오래된 프로세스: {len(terminate_processes)}개\n")

    for proc in terminate_processes:
        print(f"   PID: {proc['pid']} | 시작: {proc['started_at']}")

        if not dry_run:
            try:
                process = psutil.Process(proc['pid'])
                process.terminate()
                process.wait(timeout=5)
                print(f"   → 종료 완료")
            except psutil.TimeoutExpired:
                process.kill()
                print(f"   → 강제 종료")
            except Exception as e:
                print(f"   → 종료 실패: {e}")
        else:
            print(f"   → [DRY RUN] 종료 예정")

    print("\n✅ 정리 완료")

def main():
    import argparse

    parser = argparse.ArgumentParser(description="텔레그램 봇 중복 프로세스 정리")
    parser.add_argument("--dry-run", action="store_true", help="실제 종료하지 않고 시뮬레이션만 수행")
    parser.add_argument("--list", action="store_true", help="실행 중인 프로세스만 표시")

    args = parser.parse_args()

    if args.list:
        processes = find_telegram_bot_processes()
        if not processes:
            print("실행 중인 텔레그램 봇이 없습니다.")
        else:
            print(f"📊 텔레그램 봇 프로세스: {len(processes)}개\n")
            for proc in processes:
                print(f"PID: {proc['pid']}")
                print(f"시작: {proc['started_at']}")
                print(f"명령: {proc['cmdline'][:100]}...")
                print()
    else:
        cleanup_duplicate_bots(dry_run=args.dry_run)

if __name__ == "__main__":
    # UTF-8 설정
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    main()
