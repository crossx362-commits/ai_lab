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
from _shared import growth

load_env()


def growth_report() -> str:
    """[예원 시스템 점검] — 프로세스 상태 + 성장기록 점검 + 승인대기 개선안 (헌장 예원 형식)."""
    status = agent_status()
    normal = [k for k, v in status.items() if v != "down"]
    down = [k for k, v in status.items() if v == "down"]
    summ = growth.summary()
    # 반복 문제: 같은 부족점이 2회 이상, 또는 평균 총점 60 미만
    repeated = [f"{a}: 평균 {d['avg_total']}점({d['count']}건)"
                for a, d in summ.items() if d["count"] >= 2 and d["avg_total"] < 60]
    no_log = [a for a in ("somi_monitor", "somi_advisor", "somi_position", "somi_screener",
                          "somi_reporter", "marketdesk") if a not in summ]
    proposals = growth.list_proposals()
    lines = ["[예원 시스템 점검]"]
    lines.append(f"- 정상 에이전트: {len(normal)}종 ({', '.join(normal) or '-'})")
    lines.append(f"- 이상 에이전트: {', '.join(down) if down else '없음'}")
    lines.append(f"- 성장기록 누락: {', '.join(no_log) if no_log else '없음'}")
    lines.append(f"- 반복 문제: {'; '.join(repeated) if repeated else '없음'}")
    if proposals:
        lines.append(f"- 사용자 승인 필요 개선안: {len(proposals)}건")
        for p in proposals[:5]:
            lines.append(f"  · [{p.get('agent')}] {p.get('fix','')[:60]}")
    else:
        lines.append("- 사용자 승인 필요 개선안: 없음")
    return "\n".join(lines)

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
    # 워치독 자신(yewon)은 재시작 대상에서 제외 — 자기를 죽이고 되살리는 재귀/스팸 방지.
    # (자신이 실제로 죽으면 스스로 재시작 불가; launchd/수동이 담당)
    down_bots = [k for k, v in status.items() if v == "down" and k != "yewon"]
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

    last_growth_date = None
    last_issue_sig = ""
    with ProcessLock("yewon_monitor"):
        try:
            while True:
                print(f"\n--- [{datetime.now().strftime('%H:%M:%S')}] 하네스 체크 ---")

                # 하네스 실행 (리포트 갱신 + 로그)
                output = run_harness()
                # WARN/FAIL은 '상태가 바뀔 때만' 텔레그램 보고(동일 이슈 5분마다 반복 스팸 방지).
                # 해소되면 회복 알림 1회. (run_harness는 SUPPRESS_TELEGRAM이라 자체 발송 없음)
                issues = [ln.strip() for ln in output.splitlines()
                          if ln.startswith("[WARN]") or ln.startswith("[FAIL]")]
                sig = "\n".join(issues)
                if sig != last_issue_sig:
                    if issues:
                        send("⚠️ [예원 하네스] 이슈 감지\n" + "\n".join(issues[:8]))
                    elif last_issue_sig:
                        send("✅ [예원 하네스] 이슈 해소 — 전 항목 정상")
                    last_issue_sig = sig
                if issues:
                    print("⚠️  이슈 감지")

                # 봇 상태는 항상 직접 확인 (하네스 stdout 파싱에 의존하지 않음)
                if check_and_restart_bots():
                    time.sleep(10)  # 재시작 대기

                # 성장 점검: 하루 1회(17시 이후) 발송 — 스팸 방지
                now = datetime.now()
                if now.hour >= 17 and last_growth_date != now.date():
                    try:
                        send(growth_report())
                        last_growth_date = now.date()
                    except Exception as e:
                        print(f"성장 점검 발송 오류: {e}")

                time.sleep(300)  # 5분

        except KeyboardInterrupt:
            print("\n[Yewon Monitor] stopped")

if __name__ == "__main__":
    main()
