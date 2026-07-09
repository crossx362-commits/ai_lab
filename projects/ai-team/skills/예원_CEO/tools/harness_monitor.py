#!/usr/bin/env python3
"""예원 - 하네스 자동 감시 및 봇 관리"""
import json, os, sys, time, subprocess
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))
from _shared.env import load_env
from _shared.notify import send, agent_status, _LAUNCHD_FALLBACK, CONTINUOUS_DAEMONS
from _shared.process import ProcessLock
from _shared import growth

load_env()

# 콘솔 없는 데몬(agent_controller가 CREATE_NO_WINDOW로 기동)이 git/python 등 콘솔 서브시스템
# 자식을 창 숨김 없이 spawn하면 Windows가 매번 새 콘솔 창을 띄운다 — 5분 주기 run_harness/_git_out가
# 그 주범이었다(2026-07-09, "창 자꾸 켜졌다 꺼진다" 신고로 발견). 모든 win32 subprocess 호출에 적용.
_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


def growth_report() -> str:
    """[예원 시스템 점검] — 프로세스 상태 + 성장기록 점검 + 승인대기 개선안 (헌장 예원 형식)."""
    status = agent_status()
    normal = [k for k, v in status.items() if v != "down"]
    down = [k for k, v in status.items() if v == "down"]
    summ = growth.summary()
    # 반복 문제: 같은 부족점이 2회 이상, 또는 평균 총점 60 미만
    repeated = [f"{a}: 평균 {d['avg_total']}점({d['count']}건)"
                for a, d in summ.items() if d["count"] >= 2 and d["avg_total"] < 60]
    no_log = []  # 주식 에이전트 삭제(2026-07-08)로 성장기록 누락 점검 대상 없음
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
        env=env,
        **_NOWIN,
    )
    return result.stdout or result.stderr or ""

def _restart_bot(name: str) -> None:
    """봇 재시작 — Windows는 agent_controller, macOS는 launchctl kickstart.
    macOS 라벨은 _LAUNCHD_FALLBACK으로 해석(현재 비어 있음 — 주식 에이전트 삭제).
    launchd 비관리 데몬(추세알림·모닝노트·성장엔진 등)은 kickstart가 실패하므로
    agent_controller로 폴백 — 재부팅 후 미기동 데몬도 이 경로로 복구된다."""
    controller = os.path.join(
        os.path.dirname(__file__), "..", "..", "영숙_비서", "tools", "agent_controller.py"
    )
    if sys.platform == "win32":
        subprocess.run(
            [sys.executable, controller, name, "restart"],
            capture_output=True, timeout=30, **_NOWIN,
        )
    else:
        domain = f"gui/{os.getuid()}"
        label = _LAUNCHD_FALLBACK.get(name, f"com.ailab.{name}")
        r = subprocess.run(
            ["launchctl", "kickstart", "-k", f"{domain}/{label}"],
            capture_output=True, timeout=10,
        )
        if r.returncode != 0:
            subprocess.run(
                [sys.executable, controller, name, "restart"],
                capture_output=True, timeout=30,
            )


ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))
HEAD_STATE = os.path.join(ROOT_DIR, "output", "cache", "watchdog_git_head.json")
# 텔레그램 "봇 다 꺼"가 세우는 플래그 — 존재하면 워치독이 다운 봇을 되살리지 않는다(원격 종료 유지).
BOTS_OFF_FLAG = os.path.join(ROOT_DIR, "output", "cache", "BOTS_OFF")


def _bots_off() -> bool:
    return os.path.exists(BOTS_OFF_FLAG)


def _git_out(*args: str) -> str:
    try:
        r = subprocess.run(["git", "-c", "core.quotepath=false", *args], cwd=ROOT_DIR,
                           capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=20,
                           **_NOWIN)
        return r.stdout.strip()
    except Exception:
        return ""


def _daemon_dirs() -> dict:
    """데몬 키 → 리포 상대 tools 디렉터리. agent_controller의 스크립트 경로가 단일 소스."""
    try:
        ctrl_dir = os.path.join(os.path.dirname(__file__), "..", "..", "영숙_비서", "tools")
        if ctrl_dir not in sys.path:
            sys.path.insert(0, ctrl_dir)
        import agent_controller as _ac
        out = {}
        for key in CONTINUOUS_DAEMONS:   # 상시 데몬만 — 예약 서비스 키를 넣으면 정시 잡이 오발사된다
            info = _ac.AGENTS.get(_ac.get_agent_name(key))
            if info:
                out[key] = os.path.relpath(str(info["script"].parent), ROOT_DIR).replace("\\", "/")
        return out
    except Exception:
        return {}


def restart_on_code_update() -> None:
    """git pull 감지 → 변경 코드에 해당하는 데몬 자동 재시작 — '깃 풀만 하면 되게'(2026-07-02).
    _shared 변경은 전 데몬, tools 폴더 변경은 그 폴더 소속 데몬만. 자신(yewon)은
    분리 실행한 컨트롤러 restart로 최후 교체(뮤텍스 경합 없는 유일한 자가재시작 경로)."""
    if _bots_off():
        return   # 원격 종료 상태 — 코드 변경돼도 데몬 되살리지 않음(HEAD 갱신도 보류)
    head = _git_out("rev-parse", "HEAD")
    if not head:
        return
    try:
        with open(HEAD_STATE, encoding="utf-8") as f:
            last = json.load(f).get("head", "")
    except Exception:
        last = ""
    if head == last:
        return
    os.makedirs(os.path.dirname(HEAD_STATE), exist_ok=True)
    with open(HEAD_STATE, "w", encoding="utf-8") as f:   # 재시작 루프 방지 — 상태 먼저 기록
        json.dump({"head": head, "ts": datetime.now().isoformat(timespec="seconds")}, f)
    if not last:
        return  # 첫 기동 — 기준점만 기록
    changed = [p for p in _git_out("diff", "--name-only", f"{last}..{head}").splitlines()
               if p.endswith(".py")]
    if not changed:
        return
    dirs = _daemon_dirs()
    shared = any(p.startswith("projects/ai-team/_shared/") for p in changed)
    targets = [key for key, rel_dir in dirs.items()
               if shared or any(os.path.dirname(p) == rel_dir for p in changed)]
    self_restart = "yewon" in targets
    targets = [t for t in targets if t != "yewon"]
    if not (targets or self_restart):
        return
    send("🔄 [예원] 코드 갱신 감지(git pull) → 새 코드로 데몬 재시작\n"
         + ", ".join(targets + (["yewon(자가교체)"] if self_restart else [])))
    for name in targets:
        try:
            _restart_bot(name)
        except Exception:
            pass  # 실패해도 다음 주기 워치독이 down 감지 후 재시도
    if self_restart:
        controller = os.path.join(os.path.dirname(__file__), "..", "..", "영숙_비서", "tools",
                                  "agent_controller.py")
        kwargs = {"creationflags": 0x08000008} if sys.platform == "win32" else {"start_new_session": True}
        subprocess.Popen([sys.executable, controller, "yewon", "restart"], **kwargs)


# 하네스 이슈 → 원인 에이전트 자동 복구 매핑: (검사명, 메시지 부분문자열, 재시작 대상)
# 대상은 agent_controller ALIASES에 있는 영어 키. 재시작으로 해결 가능한 이슈만 등록
# (파일 파손 등 재시작으로 안 낫는 건 알림만 — 데이터 삭제류 복구는 자동화 금지).
REMEDY_MAP = [
    ("schedule", "schedule_manager", "scheduler"),
]
REMEDY_COOLDOWN_SEC = 3600   # 같은 대상 재시작 최소 간격(재시작 루프 방지)
REMEDY_MAX_PER_DAY = 3       # 초과 시 자동복구 포기 → 수동 개입 에스컬레이션


def auto_remediate(state: dict) -> list[str]:
    """직전 하네스 결과(harness_latest.json)의 WARN/FAIL을 원인 에이전트 재시작으로 자가치유.
    state: {대상: {"ts": [epoch...], "date": "YYYY-MM-DD", "escalated": bool}} (데몬 메모리 상주)."""
    if _bots_off():
        return []   # 원격 종료 상태 — 자가치유 재시작도 억제

    report_file = os.path.join(
        os.path.dirname(__file__), "..", "..", "..", "..", "..",
        "reports", "status", "harness_latest.json")
    try:
        with open(report_file, encoding="utf-8") as f:
            checks = json.load(f).get("checks", [])
    except Exception:
        return []
    actions = []
    now, today = time.time(), datetime.now().strftime("%Y-%m-%d")
    for chk in checks:
        if chk.get("status") == "OK":
            continue
        for name, pat, target in REMEDY_MAP:
            if chk.get("name") != name or pat not in chk.get("message", ""):
                continue
            s = state.setdefault(target, {"ts": [], "date": today, "escalated": False})
            if s["date"] != today:   # 날짜 바뀌면 카운터 리셋
                s.update({"ts": [], "date": today, "escalated": False})
            if s["ts"] and now - s["ts"][-1] < REMEDY_COOLDOWN_SEC:
                break                # 쿨다운 중 — 직전 복구 효과를 기다린다
            if len(s["ts"]) >= REMEDY_MAX_PER_DAY:
                if not s["escalated"]:
                    s["escalated"] = True
                    actions.append(f"🚨 {target} 자동복구 {REMEDY_MAX_PER_DAY}회 실패 — 수동 개입 필요: {chk['message'][:60]}")
                break
            try:
                _restart_bot(target)
                s["ts"].append(now)
                actions.append(f"🔧 {target} 재시작 ({len(s['ts'])}/{REMEDY_MAX_PER_DAY}) ← {chk['message'][:60]}")
            except Exception as e:
                actions.append(f"⚠️ {target} 재시작 실패: {e}")
            break                    # 이슈당 첫 매칭 대상 하나만
    return actions


def check_and_restart_bots():
    """봇 상태 확인 및 재시작 (best-effort)"""
    if _bots_off():
        return False   # 텔레그램 원격 종료 상태 — 부활 억제('봇 다 켜'로 해제)
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
    remedy_state: dict = {}
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
                    # 자동 복구: 원인 에이전트 재시작(쿨다운·일일상한) — 결과는 텔레그램 보고
                    acts = auto_remediate(remedy_state)
                    if acts:
                        send("🔧 [예원] 하네스 이슈 자동 복구\n" + "\n".join(acts))
                        last_issue_sig = ""  # 복구 후 상태 재평가 — 다음 사이클 결과를 다시 보고

                # git pull 감지 → 변경 데몬 새 코드로 자동 교체 (풀만 하면 반영)
                try:
                    restart_on_code_update()
                except Exception as e:
                    print(f"코드 갱신 감지 오류: {e}")

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
