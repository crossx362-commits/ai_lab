# -*- coding: utf-8 -*-
"""
프로세스 모니터링 데몬
10분마다 중복 프로세스 체크 및 자동 정리
"""
import os
import sys
import io
import time
import subprocess

# UTF-8 강제 (Windows)
if sys.platform == "win32" and hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env

load_env()


def main(daemon=False):
    """메인 모니터링 루프"""
    if not daemon:
        print("🤖 프로세스 모니터링 시작 (10분 주기)")
        print("  - 중복 프로세스 감지 시 자동 정리")
        print("  - Ctrl+C로 중지\n")

    cleanup_script = os.path.join(_here, "cleanup_duplicate_processes.py")

    while True:
        try:
            # 중복 프로세스 체크 및 정리 스크립트 실행
            result = subprocess.run(
                [sys.executable, cleanup_script],
                capture_output=True,
                text=True,
                encoding='utf-8',
                errors='replace',
                timeout=120
            )

            if result.returncode == 0:
                if not daemon:
                    print(f"[{time.strftime('%H:%M:%S')}] 모니터링 완료")
                # "트레이딩 팀 재시작"이 포함되면 정리가 발생한 것
                if "트레이딩 팀 재시작" in result.stdout or "정리 완료" in result.stdout:
                    print(f"\n[{time.strftime('%H:%M:%S')}] 중복 프로세스 자동 정리 발생!")
                    print(result.stdout)
            else:
                print(f"[{time.strftime('%H:%M:%S')}] 오류 발생")
                if result.stderr:
                    print(result.stderr)

        except Exception as e:
            print(f"[{time.strftime('%H:%M:%S')}] 모니터링 오류: {e}")

        # 10분 대기
        time.sleep(600)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--daemon', action='store_true', help='데몬 모드 (조용한 백그라운드 실행)')
    args = parser.parse_args()

    # 중복 실행 방지 (PID 파일 기반)
    from _shared.process_lock import acquire_lock, release_lock
    if not acquire_lock("monitor"):
        sys.exit(0)

    try:
        main(daemon=args.daemon)
    except KeyboardInterrupt:
        print("\n\n프로세스 모니터링 중지")
    finally:
        release_lock("monitor")
