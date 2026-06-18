#!/usr/bin/env python3
"""예원 - 하네스 자동 감시 및 봇 관리"""
import os, sys, time, subprocess
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))
from _shared.env import load_env
from _shared.notify import send, agent_status
from _shared.process import ProcessLock

load_env()

def run_harness():
    """하네스 실행"""
    result = subprocess.run(
        [sys.executable, "harness/check_all.py"],
        cwd=os.path.join(os.path.dirname(__file__), "..", ".."),
        capture_output=True,
        text=True
    )
    return result.stdout

def check_and_restart_bots():
    """봇 상태 확인 및 재시작"""
    status = agent_status()
    down_bots = [k for k, v in status.items() if v == "down"]

    if down_bots:
        print(f"⚠️  Down: {', '.join(down_bots)}")

        # 재시작 시도
        restart_script = os.path.join(os.path.dirname(__file__), "..", "..", "scripts", "start_trading_all.py")
        if os.path.exists(restart_script):
            subprocess.Popen([sys.executable, restart_script])
            send(f"🔄 [예원] 봇 재시작\nDown: {', '.join(down_bots)}")
            return True
    return False

def main():
    """메인 루프"""
    print("🤖 [예원] 하네스 자동 감시 시작 (5분 주기)")

    with ProcessLock("yewon_monitor"):
        try:
            while True:
                print(f"\n--- [{datetime.now().strftime('%H:%M:%S')}] 하네스 체크 ---")

                # 하네스 실행
                output = run_harness()

                # WARN/FAIL 체크
                has_warn = "WARN" in output or "FAIL" in output

                if has_warn:
                    print("⚠️  이슈 감지")

                    # 봇 상태 확인
                    if "runtime" in output and "down" in output:
                        restarted = check_and_restart_bots()
                        if restarted:
                            time.sleep(10)  # 재시작 대기

                time.sleep(300)  # 5분

        except KeyboardInterrupt:
            print("\n[Yewon Monitor] stopped")

if __name__ == "__main__":
    main()
