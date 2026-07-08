#!/usr/bin/env python3
"""yewon_self_heal.py — 예원(CEO): 정기 자가 점검 → 안전 자동 복구 → 결과만 보고.

점검: 하네스 구조 / 레지스트리 무결성 / 데몬·서비스 상태
자동 복구(안전 범위만): __pycache__ 정리, 중지된 상시 데몬 재시작
사람 판단이 필요한 문제는 '확인 필요'로만 보고. 결과 1건만 텔레그램 전송.

실행:
  python yewon_self_heal.py            # 점검·복구·보고(텔레그램)
  python yewon_self_heal.py --dry      # 보고 억제(콘솔만), 복구는 수행
  python yewon_self_heal.py --check    # 점검만(복구 안 함)
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.env import load_env            # noqa: E402
from _shared.notify import send, agent_status, _AGENT_LABELS, CONTINUOUS_DAEMONS  # noqa: E402
from _shared.registry import load_registry  # noqa: E402

load_env()

AC = AI_TEAM / "skills" / "영숙_비서" / "tools" / "agent_controller.py"
HARNESS = AI_TEAM / "harness" / "check_all.py"
# 상시 데몬 → 직접 재실행 스크립트(컨트롤러 비대상, 뮤텍스로 중복 방지)
DAEMON_SCRIPT = {}

DRY = "--dry" in sys.argv
CHECK_ONLY = "--check" in sys.argv


def _run(args: list[str], timeout: int = 60) -> tuple[int, str]:
    try:
        r = subprocess.run(args, cwd=str(PROJECT_ROOT), capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=timeout,
                           env={**os.environ, "PYTHONUTF8": "1", "SUPPRESS_TELEGRAM": "true"})
        return r.returncode, (r.stdout or r.stderr or "").strip()
    except Exception as e:
        return 1, str(e)


# ── 점검 ─────────────────────────────────────────────────────────
# 매일 떠도 조치 대상이 아닌 잡음(레포 위생/중복) — 보고 제외
_BENIGN_WARN = ("runtime:", "classification_layout", "unclassified_files", "tracked ignored")


def check_harness() -> list[str]:
    if not HARNESS.exists():
        return ["하네스 스크립트 없음"]
    code, out = _run([sys.executable, str(HARNESS)], timeout=90)
    issues = []
    for line in out.splitlines():
        s = line.strip()
        if s.startswith("[Telegram suppressed]") or "suppressed]" in s:
            continue
        if "FAIL" in s:
            issues.append(s[:120])
        elif "WARN" in s and not any(b in s for b in _BENIGN_WARN):
            issues.append(s[:120])
    if code != 0 and not issues:
        issues.append(f"하네스 비정상 종료(code={code})")
    return issues


def check_registry() -> list[str]:
    issues = []
    reg = load_registry(include_inactive=True)
    for aid, m in reg.items():
        if m.get("status") == "active" and not m.get("_folder_exists"):
            issues.append(f"에이전트 '{m.get('display', aid)}' 폴더 없음(active인데 백엔드 없음)")
        for t in m.get("tools", []):
            script = AI_TEAM / t.get("script", "")
            if t.get("script") and not script.exists():
                issues.append(f"'{m.get('display', aid)}' 도구 스크립트 없음: {t['script']}")
    return issues


# ── 복구(안전 범위) ──────────────────────────────────────────────
def heal_pycache() -> int:
    n = 0
    for pc in AI_TEAM.rglob("__pycache__"):
        try:
            shutil.rmtree(pc)
            n += 1
        except OSError:
            pass
    return n


def heal_daemons(status: dict) -> tuple[list[str], list[str]]:
    fixed, flagged = [], []
    for name, state in status.items():
        if state != "down":
            continue
        label = _AGENT_LABELS.get(name, name)
        if name in DAEMON_SCRIPT and DAEMON_SCRIPT[name].exists():
            try:
                subprocess.Popen([sys.executable, str(DAEMON_SCRIPT[name])],
                                 cwd=str(PROJECT_ROOT),
                                 env={**os.environ, "PYTHONUTF8": "1"},
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                 start_new_session=True)
                fixed.append(label + " 재기동")
            except Exception:
                flagged.append(label + " 재기동 실패")
        elif name in CONTINUOUS_DAEMONS:
            # 상시 데몬은 agent_controller(ALIASES 영어 키)로 재시작 — 재부팅 후 미기동 자가복구.
            # 컨트롤러는 항상 exit 0이므로 출력의 '시작 완료'로 성공 판정.
            code, out = _run([sys.executable, str(AC), name, "restart"], timeout=40)
            ok = code == 0 and "시작 완료" in out
            (fixed if ok else flagged).append(label + (" 재시작" if ok else " 중지(재시작 실패 — 확인 필요)"))
        else:
            # 예약(launchd) 서비스 down은 프로세스 기동으로 못 고침 — 보고만.
            flagged.append(label + " 중지(확인 필요)")
    return fixed, flagged


# ── 보고(결과만) ─────────────────────────────────────────────────
def build_report(harness_issues, reg_issues, pyc_n, fixed, flagged) -> str:
    from datetime import datetime, timezone, timedelta
    ts = datetime.now(timezone(timedelta(hours=9))).strftime("%m-%d %H:%M")
    problems = harness_issues + reg_issues
    if not problems and not flagged and not fixed:
        return f"✅ [예원] 시스템 점검 완료 ({ts}) — 이상 없음"

    lines = [f"🧭 [예원] 시스템 점검 결과 ({ts})"]
    if fixed:
        lines.append("🔧 자동 해결: " + ", ".join(fixed))
    if pyc_n:
        lines.append(f"🧹 캐시 정리 {pyc_n}건")
    need = []
    if harness_issues:
        need += [f"구조: {x}" for x in harness_issues[:3]]
    if reg_issues:
        need += reg_issues[:3]
    if flagged:
        need += flagged
    if need:
        lines.append("⚠️ 확인 필요:\n  - " + "\n  - ".join(need))
    elif fixed or pyc_n:
        lines.append("→ 나머지 정상")
    return "\n".join(lines)


def main() -> None:
    harness_issues = check_harness()
    reg_issues = check_registry()
    status = agent_status()

    pyc_n, fixed, flagged = 0, [], []
    if not CHECK_ONLY and not DRY:
        pyc_n = heal_pycache()
        fixed, flagged = heal_daemons(status)
    else:
        # 점검/드라이런: 부작용 없이 down 데몬만 표기 (캐시 삭제·재시작 안 함)
        flagged = [f"{_AGENT_LABELS.get(n, n)} 중지" for n, s in status.items() if s == "down"]

    report = build_report(harness_issues, reg_issues, pyc_n, fixed, flagged)
    print(report)
    if not DRY and not CHECK_ONLY:
        send(report)


if __name__ == "__main__":
    main()
