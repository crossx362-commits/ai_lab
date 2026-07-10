#!/usr/bin/env python3
"""외부 하트비트 — 함대 밖에서 도는 독립 감시선.

2026-07-10 함대 전멸 사고: 9종이 통째로 죽었는데 아무 신호도 없었다.
침묵을 감지할 유일한 장치(봄이 신선도 감사)가 감시 대상과 같은 함대에 있어서
함대가 죽으면 감시선도 같이 죽는다. 워치독(예원)도 자기 자신은 못 살린다
(harness_monitor.check_and_restart_bots가 'yewon'을 재시작 목록에서 제외).

그래서 이 스크립트는 데몬이 아니다. Windows 작업 스케줄러가 5분마다 새 프로세스로
띄우며, 함대가 전멸해도 독립적으로 살아남아 경보한다.

  python fleet_heartbeat.py            # 1회 점검(스케줄러가 부르는 기본 모드)
  python fleet_heartbeat.py --install  # 5분 주기 작업 스케줄러 등록
  python fleet_heartbeat.py --status   # 마지막 점검 결과만 출력
"""
import argparse, json, os, subprocess, sys
from datetime import datetime, timezone

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))
from _shared.env import load_env
from _shared.notify import agent_status
from _shared.telegram import send
from _shared.utils import find_root

load_env()

TASK_NAME = "AiLabFleetHeartbeat"
STATE = find_root() / "output" / "cache" / "fleet_heartbeat.json"
ALERT_COOLDOWN_SEC = 1800  # 같은 문제로 30분 내 재알림 금지 — 경보 피로 방지
_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


def _load_state() -> dict:
    try:
        return json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        return {}


def _save_state(state: dict) -> None:
    STATE.parent.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def _revive_watchdog() -> str:
    """예원(워치독)은 스스로를 못 살린다 — 함대 밖의 우리만 살릴 수 있다."""
    ctl = (find_root() / "projects" / "ai-team" / "skills" / "영숙_비서"
           / "tools" / "agent_controller.py")
    try:
        r = subprocess.run([sys.executable, str(ctl), "예원", "시작"],
                           capture_output=True, text=True, encoding="utf-8",
                           errors="replace", timeout=90, **_NOWIN)
        return (r.stdout or r.stderr or "").strip()[:120]
    except Exception as e:
        return f"재시작 실패: {e}"


def check() -> int:
    status = agent_status()
    down = sorted(k for k, v in status.items() if v == "down")
    misconfig = sorted(k for k, v in status.items() if v == "misconfig")

    now = datetime.now(timezone.utc)
    state = _load_state()
    state["last_check"] = now.isoformat()
    state["status"] = status

    if not down and not misconfig:
        state["last_ok"] = now.isoformat()
        state["down_streak"] = {}
        _save_state(state)
        print(f"[하트비트] 정상 — {len(status)}종 가동")
        return 0

    revived = ""
    if "yewon" in down:
        revived = _revive_watchdog()

    # 에스컬레이션 정책(HANDBOOK §7): 기준은 심각도가 아니라 '자동으로 낫는가'다.
    # 즉시 경보 — 워치독이 못 고치거나(misconfig), 워치독 자신이 죽었거나,
    # 단일 봇 사고로 보기 어려운 동시 다운(≥3종).
    urgent = bool(misconfig) or "yewon" in down or len(down) >= 3

    # 그 외 단일·이중 다운은 워치독에게 한 주기(5분) 기회를 준다.
    # 다음 점검에도 같은 봇이 죽어 있으면 = 자동복구가 졌다 → 경보.
    key = f"down={','.join(down)}|misconfig={','.join(misconfig)}"
    streak = state.get("down_streak") or {}
    streak = {"key": key, "count": streak.get("count", 0) + 1} if streak.get("key") == key \
        else {"key": key, "count": 1}
    state["down_streak"] = streak

    if not urgent and streak["count"] < 2:
        _save_state(state)
        print(f"[하트비트] {', '.join(down)} 다운 — 워치독 복구 대기(1주기 유예)")
        return 0
    prev_key, prev_ts = state.get("alert_key"), state.get("alert_ts")
    cooled = True
    if prev_key == key and prev_ts:
        try:
            elapsed = (now - datetime.fromisoformat(prev_ts)).total_seconds()
            cooled = elapsed >= ALERT_COOLDOWN_SEC
        except Exception:
            cooled = True

    head = "🚨 [하트비트] 함대 이상" if urgent else "🔴 [하트비트] 자동복구 실패(2주기 연속)"
    lines = [head]
    if misconfig:
        lines.append(f"🟠 설정 유실({len(misconfig)}): {', '.join(misconfig)}")
        lines.append("   → PETNNA_AGENTS_ON_WINDOWS 부재. 재시작으로 안 낫는다.")
        lines.append("   → git checkout HEAD -- .env.encrypted (키 diff 먼저 확인)")
    if down:
        lines.append(f"🔴 중지({len(down)}): {', '.join(down)}")
    if len(down) >= 3:
        lines.append("   → 동시 다운. 단일 봇 사고가 아니다 — 공통 원인(.env·기계·전원) 의심")
    if revived:
        lines.append(f"🔄 워치독 예원 재기동 시도: {revived}")
    msg = "\n".join(lines)

    if cooled:
        send(msg)
        state["alert_key"], state["alert_ts"] = key, now.isoformat()
    else:
        print("[하트비트] 쿨다운 중 — 알림 생략")
    _save_state(state)
    print(msg)
    return 1


def install() -> int:
    if sys.platform != "win32":
        print("Windows 전용. macOS는 launchd(com.ailab.*)로 등록하라.")
        return 2
    script = os.path.abspath(__file__)
    # /f 덮어쓰기, /sc minute /mo 5 = 5분 주기. 관리자 권한 불필요(현재 사용자 컨텍스트).
    cmd = ["schtasks", "/create", "/tn", TASK_NAME, "/f", "/sc", "minute", "/mo", "5",
           "/tr", f'"{sys.executable}" "{script}"']
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    print((r.stdout or r.stderr).strip())
    if r.returncode == 0:
        print(f"등록 완료: {TASK_NAME} (5분 주기, 인터프리터 {sys.executable})")
    return r.returncode


def main() -> int:
    ap = argparse.ArgumentParser(description="함대 외부 하트비트")
    ap.add_argument("--install", action="store_true", help="작업 스케줄러에 5분 주기 등록")
    ap.add_argument("--status", action="store_true", help="마지막 점검 결과 출력")
    args = ap.parse_args()
    if args.install:
        return install()
    if args.status:
        print(json.dumps(_load_state(), ensure_ascii=False, indent=2))
        return 0
    return check()


if __name__ == "__main__":
    sys.exit(main())
