# -*- coding: utf-8 -*-
"""
프로세스 모니터링 데몬
10분마다 중복 프로세스 체크 및 자동 정리
"""
import os
import sys
import time
import subprocess

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env

load_env()


def main():
    """메인 모니터링 루프"""
    print("🤖 프로세스 모니터링 시작 (10분 주기)")
    print("  - 중복 프로세스 감지 시 자동 정리")
    print("  - Ctrl+C로 중지\n")

    cleanup_script = os.path.join(_here, "cleanup_duplicate_processes.py")

    while True:
        try:
            # 중복 프로세스 체크 및 정리 스크립트 실행
            result = subprocess.run(
                ["python", cleanup_script],
                capture_output=True,
                text=True,
                timeout=120
            )

            if result.returncode == 0:
                print(f"[{time.strftime('%H:%M:%S')}] 모니터링 완료")
                if "중복 프로세스 감지" in result.stdout:
                    print(result.stdout)
            else:
                print(f"[{time.strftime('%H:%M:%S')}] 오류 발생")
                print(result.stderr)

        except Exception as e:
            print(f"[{time.strftime('%H:%M:%S')}] 모니터링 오류: {e}")

        # 10분 대기
        time.sleep(600)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n프로세스 모니터링 중지")
