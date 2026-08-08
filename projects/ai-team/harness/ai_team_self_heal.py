#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ai-team 코드 자기수정 — 문제가 생기면 사람에게 보고하지 않고 스스로 고친다.

오너 지시 2026-08-08: "코드 자기수정은 문제가 생길경우 나에게 보고하지 않고
알아서 수정하는거 ... 그래 그렇게해."

범위와 안전선
--------------
`harness/run_regression.py`가 회귀 스위트 실패를 발견하면 이 모듈이 고치기를
시도한다. petnna 앱 코드(수리 담당)와 달리 `projects/ai-team`은 E2E·QA 순찰·
백엔드 계약 감사 같은 다층 검증이 없다 — 여기가 바로 그 검증 장치들이 사는
곳이기 때문이다. 그래서 두 겹의 안전선을 둔다:

  1. **격리 워크트리**에서만 고친다. 실패한 채로 끝나도 본 작업트리는 항상 깨끗하다
     (여러 데몬이 동시에 같은 저장소를 쓰므로 이건 선택이 아니라 필수).
  2. **안전망 자체 파일(SAFETY_NET_FILES)은 건드리면 무조건 검토 트랙으로 보낸다.**
     자기수정이 하필 게이트·락·보고 로직을 잘못 고치면, 그 실패를 감지해 알릴
     메커니즘 자체가 고장 나서 "고쳤다고 착각한 채 조용히 전멸"할 수 있다
     (2026-07-12 유휴 디스패치 사고와 같은 계열 — 그때는 안전장치 도입 자체가
     새 사고를 냈다). 이 경로는 그 실패 모드가 발생할 수 없게 원천 차단한다.

병합 조건: ①안전망 미접촉 ②projects/ai-team 범위 내 ③격리 워크트리에서 회귀
스위트 100% 통과. 셋 다 만족해야 master에 병합 + push. 하나라도 아니면
워크트리를 버리고(merge 안 함) 실패로 반환 — 호출자(run_regression)가 기존처럼
텔레그램 보고로 넘어간다. **성공한 자기수정은 조용하다** — 오너 지시 §1-5.
"""

from __future__ import annotations

import subprocess
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.cc import run_claude  # noqa: E402

WT_BASE = PROJECT_ROOT / "output" / "cache" / "ai_team_self_heal"
CLAUDE_TIMEOUT = 600
SUITE_TIMEOUT = 900

# 안전망 자체 — 게이트·락·백로그 판별·복구 오케스트레이션·이 스위트를 실행하는
# 코드 자신. 자기수정이 이 파일들을 건드리면 무조건 사람 검토로 보낸다.
# 명시 목록(정규식 아님) — "이미 있는 것을 다시 만들지 마라" 유형과 달리 여기선
# 오탐보다 누락이 훨씬 위험하므로, 넓은 패턴 대신 감사 가능한 고정 목록을 쓴다.
SAFETY_NET_FILES = {
    "projects/ai-team/_shared/backlog.py",
    "projects/ai-team/_shared/process.py",
    "projects/ai-team/_shared/env.py",
    "projects/ai-team/skills/예원_CEO/tools/harness_monitor.py",
    "projects/ai-team/skills/예원_CEO/tools/petnna_backlog_unblocker.py",
    "projects/ai-team/skills/예원_CEO/tools/petnna_pr_reviewer.py",
    "projects/ai-team/skills/수리_개발자/tools/petnna_dev_engine.py",
    "projects/ai-team/skills/영숙_비서/tools/agent_controller.py",
    "projects/ai-team/skills/영숙_비서/tools/schedule_sync.py",
    "projects/ai-team/harness/check_all.py",
    "projects/ai-team/harness/run_regression.py",
    "projects/ai-team/harness/ai_team_self_heal.py",
}


def _git(args: list[str], cwd: Path) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=str(cwd), capture_output=True, text=True, timeout=120)


def touches_safety_net(files: list[str]) -> list[str]:
    """건드린 파일 중 안전망에 속하는 것만 돌려준다(없으면 빈 목록)."""
    return sorted(set(files) & SAFETY_NET_FILES)


def out_of_scope(files: list[str]) -> list[str]:
    """projects/ai-team 밖을 건드렸는가."""
    return sorted(f for f in files if not f.startswith("projects/ai-team/"))


def _changed_files(worktree: Path, base: str) -> list[str]:
    """수정된 파일 목록 — 신규 파일 포함.

    `git diff base --name-only`만 쓰면 **추적되지 않은 새 파일**은 안 잡힌다(git diff는
    스테이징 전 신규 파일을 비교 대상으로 안 본다). 안전망 판정이 새 파일을 놓치면
    검사 자체가 무력화되므로, 먼저 add -A로 스테이징한 뒤 비교한다.
    """
    _git(["add", "-A"], worktree)
    r = _git(["diff", "--cached", base, "--name-only"], worktree)
    return [l for l in r.stdout.strip().splitlines() if l]


def _run_suite(cwd: Path) -> tuple[bool, str]:
    """그 워크트리 안의 tests/를 discover로 전부 돌린다(run_regression.py와 동일 원칙 —
    파일 단위로 쪼개면 전역 상태 오염형 회귀를 놓친다)."""
    tests_dir = cwd / "projects" / "ai-team" / "tests"
    try:
        p = subprocess.run(
            [sys.executable, "-m", "unittest", "discover", "-s", str(tests_dir), "-p", "test_*.py"],
            cwd=str(cwd), capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=SUITE_TIMEOUT)
    except subprocess.TimeoutExpired:
        return False, "회귀 스위트 타임아웃"
    out = ((p.stdout or "") + (p.stderr or "")).strip()
    return p.returncode == 0, out.splitlines()[-1] if out else ""


def _merge_and_push(branch: str) -> tuple[bool, str]:
    """master로 병합 + push. 충돌 시 즉시 abort(2026-07-19 가드레일 — mid-merge 방치가
    충돌 마커를 배포 라인에 실은 적이 있다)."""
    m = _git(["merge", "--no-ff", "-m", f"fix(ai-team): 자기수정 {branch}", branch], PROJECT_ROOT)
    if m.returncode != 0:
        _git(["merge", "--abort"], PROJECT_ROOT)
        return False, f"병합 충돌 — abort함: {m.stderr.strip()[:150]}"
    p = _git(["push"], PROJECT_ROOT)
    if p.returncode != 0:
        return True, f"병합은 됐으나 push 실패(로컬엔 유지): {p.stderr.strip()[:150]}"
    return True, "병합 + push 완료"


def attempt(failure_summary: str, run_claude_fn=run_claude) -> dict:
    """실패 요약을 받아 격리 워크트리에서 고치기를 시도한다.

    반환: {healed: bool, committed: bool, reason: str}
    run_claude_fn을 주입 가능하게 둔 것은 테스트에서 실제 클로드 CLI 없이
    이 함수의 분기(안전망 접촉·범위 밖·회귀 재실패)를 검증하기 위해서다.
    """
    WT_BASE.mkdir(parents=True, exist_ok=True)
    branch = f"self-heal-{datetime.now():%Y%m%d%H%M%S}"
    wt = WT_BASE / branch
    base = _git(["rev-parse", "HEAD"], PROJECT_ROOT).stdout.strip()

    r = _git(["worktree", "add", "-b", branch, str(wt), "master"], PROJECT_ROOT)
    if r.returncode != 0:
        return {"healed": False, "committed": False,
                "reason": f"워크트리 생성 실패: {r.stderr.strip()[:150]}"}

    try:
        prompt = (
            "너는 이 저장소(ai_lab)의 자동화 코드를 고치는 헤드리스 세션이다. 회귀 스위트가 "
            f"실패하고 있다:\n\n{failure_summary}\n\n"
            "규칙:\n"
            "1. projects/ai-team/ 안의 파일만 고쳐라. projects/petnna/는 절대 건드리지 마라"
            "(수리 전용 영역이다).\n"
            "2. 다음 파일은 절대 수정하지 마라 — 이건 안전망 자체 코드다, 여기 문제면 코드를 "
            "고치지 말고 아무것도 하지 말고 종료하라:\n   " + "\n   ".join(sorted(SAFETY_NET_FILES)) + "\n"
            "3. 실패를 없애는 최소한의 수정만 하라. 리팩터링·개선·주석 정리 금지.\n"
            "4. 원인을 모르겠으면 아무것도 고치지 말고 그대로 종료하라 — 추측으로 고치지 마라.\n"
            "5. 커밋하지 마라. 파일만 고치고 끝내라.\n"
        )
        ok, out = run_claude_fn(prompt, wt, timeout=CLAUDE_TIMEOUT,
                                allowed_tools="Read,Edit,Grep,Glob", permission_mode="acceptEdits")

        changed = _changed_files(wt, base)
        if not changed:
            return {"healed": False, "committed": False,
                    "reason": f"수정 없음(클로드 응답: {out[:200]})" if not ok else "수정 없음"}

        blocked = touches_safety_net(changed)
        if blocked:
            return {"healed": False, "committed": False,
                    "reason": f"안전망 코드 접촉 — 검토 필요: {', '.join(blocked)}"}
        scope_violation = out_of_scope(changed)
        if scope_violation:
            return {"healed": False, "committed": False,
                    "reason": f"범위 밖(projects/ai-team 밖) 접촉 — 검토 필요: {', '.join(scope_violation)}"}

        suite_ok, tail = _run_suite(wt)
        if not suite_ok:
            return {"healed": False, "committed": False,
                    "reason": f"수정 후에도 회귀 실패: {tail[:200]}"}

        _git(["add", "-A"], wt)
        _git(["commit", "-qm", f"fix(ai-team): 회귀 자동 복구 — {failure_summary[:60]}"], wt)
        merged, msg = _merge_and_push(branch)
        return {"healed": True, "committed": merged, "reason": msg}
    finally:
        _git(["worktree", "remove", "--force", str(wt)], PROJECT_ROOT)
        _git(["branch", "-D", branch], PROJECT_ROOT)
