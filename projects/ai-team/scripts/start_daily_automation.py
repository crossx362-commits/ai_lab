"""
start_daily_automation.py — AI 팀 일일 자동화 시작 스크립트

Windows Task Scheduler 없이 Python 스케줄러로 매일 자동 실행합니다.
컴퓨터를 바꿔도 동일하게 작동합니다.
"""
import sys
import os
import time
import subprocess
import datetime
import schedule

_here = os.path.dirname(os.path.abspath(__file__))
if _here not in sys.path:
    sys.path.insert(0, _here)

# UTF-8 인코딩
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def run_daily_task():
    """일일 작업 실행 wrapper."""
    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 일일 작업 시작")
    print(f"{'='*60}\n")
    try:
        # daily_ai_team_runner 실행
        sys.path.insert(0, os.path.join(_here, "projects", "ai-team", "skills"))
        from daily_ai_team_runner import run_daily_automation
        run_daily_automation()
    except Exception as e:
        print(f"\n[ERROR] 작업 실행 실패: {e}")
        import traceback
        traceback.print_exc()
    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 작업 완료")
    print(f"{'='*60}\n")

def run_fix_issues():
    """YouTube/Instagram 자동 수정 스크립트 실행 wrapper."""
    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] fix_issues 실행 시작")
    print(f"{'='*60}\n")
    try:
        subprocess.run([sys.executable, os.path.join(_here, "projects", "ai-team", "skills", "가희_검수관", "tools", "fix_issues.py")], check=True)
    except Exception as e:
        print(f"\n[ERROR] fix_issues 실행 실패: {e}")
        import traceback
        traceback.print_exc()
    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] fix_issues 실행 완료")
    print(f"{'='*60}\n")


def run_inspection_task(time_slot: str):
    """가희 정기 검수 실행 wrapper."""
    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 가희 정기 검수 시작 ({time_slot})")
    print(f"{'='*60}\n")
    try:
        import subprocess
        inspector_path = os.path.join(_here, "projects", "ai-team", "skills", "가희_검수관", "tools", "content_inspector.py")
        print(f"  검수기 실행: {inspector_path} --schedule {time_slot}")
        result = subprocess.run(
            [sys.executable, inspector_path, "--schedule", time_slot],
            capture_output=True,
            text=True,
            timeout=1800
        )
        print(result.stdout)
        if result.returncode != 0:
            print(f"[ERROR] 검수기 실패: {result.stderr}")
    except Exception as e:
        print(f"[ERROR] 검수 실행 오류: {e}")

    print(f"\n{'='*60}")
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 검수 완료")
    print(f"{'='*60}\n")


def main():
    """스케줄러 메인 루프."""
    print("="*60)
    print("  AI 팀 일일 자동화 및 정기 검수 스케줄러 시작")
    print("="*60)
    print(f"  시작 시각: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"  스케줄: 매일 09:00 (일일 자동화)")
    print(f"          매일 07:00 / 13:00 / 21:00 (가희 정기 검수)")
    print("="*60)
    print("\n⚠️  이 창을 닫지 마세요. 백그라운드에서 실행됩니다.\n")

    # 일일 자동화 스케줄 등록: 매일 오전 9시
    schedule.every().day.at("09:00").do(run_daily_task)
    # fix_issues 스크립트 실행 스케줄 (일일 자동화 직후)
    schedule.every().day.at("09:05").do(run_fix_issues)

    # 가희 정기 검수 스케줄 등록
    schedule.every().day.at("07:00").do(run_inspection_task, time_slot="morning")
    schedule.every().day.at("13:00").do(run_inspection_task, time_slot="afternoon")
    schedule.every().day.at("21:00").do(run_inspection_task, time_slot="night")

    # 시작 시 한 번 실행 (선택사항)
    # run_daily_task()

    try:
        while True:
            schedule.run_pending()
            time.sleep(60)  # 1분마다 확인

    except KeyboardInterrupt:
        print("\n\n사용자가 중단했습니다.")
    except Exception as e:
        print(f"\n[ERROR] 스케줄러 오류: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    # schedule 라이브러리 확인
    try:
        import schedule
    except ImportError:
        print("[ERROR] schedule 라이브러리가 설치되지 않았습니다.")
        print("\n설치 명령:")
        print("  pip install schedule")
        sys.exit(1)

    main()
