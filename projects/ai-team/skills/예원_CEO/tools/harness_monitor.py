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
    env = {**os.environ, "PYTHONUTF8": "1", "SUPPRESS_TELEGRAM": "true"}
    result = subprocess.run(
        [sys.executable, "harness/check_all.py"],
        cwd=os.path.join(os.path.dirname(__file__), "..", "..", ".."),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=env
    )
    return result.stdout or result.stderr or ""

def _restart_bot(name: str) -> None:
    """봇 재시작 — Windows는 agent_controller, macOS는 launchctl kickstart."""
    if sys.platform == "win32":
        controller = os.path.join(
            os.path.dirname(__file__), "..", "..", "영숙_비서", "tools", "agent_controller.py"
        )
        subprocess.run(
            [sys.executable, controller, name, "restart"],
            capture_output=True, timeout=30,
        )
    else:
        domain = f"gui/{os.getuid()}"
        subprocess.run(
            ["launchctl", "kickstart", "-k", f"{domain}/com.ailab.{name}"],
            capture_output=True, timeout=10,
        )


def check_and_restart_bots():
    """봇 상태 확인 및 재시작 (best-effort)"""
    status = agent_status()
    down_bots = [k for k, v in status.items() if v == "down"]
    if not down_bots:
        return False

    print(f"⚠️  Down: {', '.join(down_bots)}")
    for name in down_bots:
        try:
            _restart_bot(name)
        except Exception:
            pass  # 실패해도 다음 주기에 재시도

    send(f"🔄 [예원] 봇 다운 감지 → 재시작 시도\nDown: {', '.join(down_bots)}")
    return True

def main():
    """메인 루프"""
    print("🤖 [예원] 하네스 자동 감시 시작 (5분 주기)")

    with ProcessLock("yewon_monitor"):
        try:
            while True:
                print(f"\n--- [{datetime.now().strftime('%H:%M:%S')}] 하네스 체크 ---")

                # 하네스 실행 (리포트 갱신 + 로그)
                output = run_harness()
                if "WARN" in output or "FAIL" in output:
                    print("⚠️  이슈 감지")

                # 봇 상태는 항상 직접 확인 (하네스 stdout 파싱에 의존하지 않음)
                if check_and_restart_bots():
                    time.sleep(10)  # 재시작 대기

                time.sleep(300)  # 5분

        except KeyboardInterrupt:
            print("\n[Yewon Monitor] stopped")

if __name__ == "__main__":
    main()
