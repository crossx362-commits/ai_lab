#!/usr/bin/env python3
"""예원 — 펫나 PR대기 브랜치 자동 검토·병합 결정.

수리가 만든 고위험(디자인·기능) 백로그 브랜치는 자동 병합이 없어 PR대기로 사람 검토를
기다린다. 오너 지시(2026-07-09 "알아서 예원이가 하라고")로 CEO 예원이 리뷰어가 되어
각 브랜치를 안전 게이트 + 클로드 품질판단으로 검토해 병합/반려를 스스로 결정한다.
오너를 검토 병목에서 제거 → 수리 적체 상한(SURI_MAX_PENDING)이 자동으로 풀린다.

안전장치는 수리 게이트를 그대로 재사용한다(중복 구현 없음):
  - petnna/ 경로 한정, 금지경로(DB·API·결제·env·배포·시크릿) 접촉 시 반려
  - diff 크기 제한
  - E2E 신규 실패 차단(master 기준 새로 깨진 테스트가 있으면 병합 금지)
이 하드 게이트를 통과한 것만 예원의 품질판단(디자인 시스템 일치·과제 부합·회귀 위험)
대상이 되고, 예원이 approve한 것만 master --no-ff 병합.
"""
import os
import sys
import subprocess
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
SURI_TOOLS = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SURI_TOOLS))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402

import petnna_dev_engine as eng  # noqa: E402  (게이트·git·상태 재사용)

load_env()

MAX_DIFF_CHARS = 12000


def _pending_branches(state: dict) -> list[tuple[str, dict, str]]:
    """실재하는 브랜치를 가진 PR대기 항목만. (없는 브랜치는 sync가 완료 처리)"""
    out = []
    for fp, rec in state.get("issues", {}).items():
        if rec.get("status") != "PR대기":
            continue
        br = rec.get("branch")
        if not br:
            continue
        if eng._git(["rev-parse", "--verify", "--quiet", f"refs/heads/{br}"],
                    eng.PROJECT_ROOT).returncode == 0:
            out.append((fp, rec, br))
    return out


def _backlog_detail(fp: str) -> dict:
    for it in eng._load_backlog().get("items", []):
        if it.get("id") == fp:
            return it
    return {}


def _ask_yewon(title: str, detail: str, diff: str, files: list[str]) -> tuple[str, str]:
    """예원(클로드)에게 diff 리뷰를 맡겨 approve/reject 결정을 받는다."""
    prompt = (
        "너는 펫나(반려동물 웹앱) 개발팀 CEO '예원'이다. 수리가 자동 구현한 아래 UI/UX "
        "브랜치를 master(운영)에 병합할지 최종 결정하라. 안전 게이트(경로·크기·E2E)는 이미 "
        "통과했다. 너는 품질만 본다.\n\n"
        f"[과제] {title}\n[상세] {detail or '(없음)'}\n"
        f"[변경 파일] {', '.join(files[:8])}\n\n"
        "[판단 기준]\n"
        "- 과제 의도에 부합하는가, 디자인 시스템(코랄 brand-500 계열·기존 톤)과 어긋나지 않는가\n"
        "- 명백한 회귀·레이아웃 파손·접근성 후퇴 위험이 없는가\n"
        "- 운영에 바로 나가도 부끄럽지 않은 완성도인가\n"
        "애매하면 reject(사람 검토로 남김)가 안전하다. 과하게 관대하지 마라.\n\n"
        "[diff]\n```diff\n" + diff[:MAX_DIFF_CHARS] +
        ("\n...(생략)" if len(diff) > MAX_DIFF_CHARS else "") + "\n```\n\n"
        '반드시 JSON만 출력: {"decision":"approve"|"reject","reason":"<한 줄 근거(한국어)>"}'
    )
    ok, out = run_claude(prompt, eng.PROJECT_ROOT, timeout=300, allowed_tools="")
    if not ok:
        return "defer", f"예원 판단 실패: {out[:120]}"
    data = extract_json(out) or {}
    decision = str(data.get("decision", "")).lower()
    reason = str(data.get("reason", "")).strip()[:200]
    if decision not in ("approve", "reject"):
        return "defer", f"판단 형식 오류: {out[:120]}"
    return decision, reason or "(근거 없음)"


def _merge(branch: str) -> tuple[bool, str]:
    dirty = eng._git(["status", "--porcelain", "--", "projects/petnna"],
                     eng.PROJECT_ROOT).stdout.strip()
    if dirty:
        return False, "main 워킹트리 petnna 경로에 미커밋 변경 존재 — 병합 보류"
    m = eng._git(["merge", "--no-ff", branch, "-m",
                  f"merge: {branch} (예원 자동 검토 승인)"], eng.PROJECT_ROOT)
    if m.returncode != 0:
        return False, f"병합 실패: {m.stderr.strip()[:150]}"
    return True, "master 병합 완료"


def review_all(do_send: bool = True) -> str:
    state = eng.load_dev_state()
    eng.sync_merged_branches(state)  # 수동 병합 정리 먼저
    pend = _pending_branches(state)
    if not pend:
        return "검토 대기 브랜치 없음"

    # master E2E 기준선(신규 실패 판정용) — 1회만
    master_tests = eng.run_e2e(eng.PROJECT_ROOT / "projects" / "petnna")

    lines = []
    for fp, rec, branch in pend:
        it = _backlog_detail(fp)
        title = rec.get("title") or it.get("title", fp)
        wt = eng.WT_BASE / f"review-{fp}"
        eng._git(["worktree", "remove", "--force", str(wt)], eng.PROJECT_ROOT)
        add = eng._git(["worktree", "add", str(wt), branch], eng.PROJECT_ROOT)
        if add.returncode != 0:
            lines.append(f"⚠️ {title[:40]} — worktree 실패, 건너뜀")
            continue

        applied = False
        try:
            gate_ok, gate_note, files = eng.diff_gate(wt)
            after = eng.run_e2e(wt / "projects" / "petnna")
            new_fail = sorted(k for k, ok in after.items()
                              if not ok and master_tests.get(k, True))
            diff = eng._git(["diff", f"master...{branch}", "--", "projects/petnna"],
                            eng.PROJECT_ROOT).stdout

            if not gate_ok:
                decision, reason = "reject", f"안전 게이트 거부: {gate_note}"
            elif new_fail:
                decision, reason = "reject", f"E2E 신규 실패: {new_fail[:3]}"
            elif not diff.strip():
                decision, reason = "reject", "빈 diff(변경 없음)"
            else:
                decision, reason = _ask_yewon(title, it.get("detail", ""), diff, files)

            if decision == "approve":
                merged, mnote = _merge(branch)
                if merged:
                    rec["status"] = "완료"
                    rec["reviewed_by"] = "예원"
                    rec["reviewed_at"] = datetime.now().isoformat()
                    eng._update_backlog(fp, "완료")
                    applied = True
                    lines.append(f"✅ 병합 — {title[:40]}\n   예원: {reason}")
                else:
                    lines.append(f"⏸️ 승인했으나 {mnote} — {title[:40]}")
            elif decision == "reject":
                rec["status"] = "보류"
                rec["reviewed_by"] = "예원"
                rec["reviewed_at"] = datetime.now().isoformat()
                rec["review_reason"] = reason
                eng._update_backlog(fp, "보류")
                applied = True
                lines.append(f"🛑 반려 — {title[:40]}\n   예원: {reason}")
            else:  # defer — 이번엔 판단 못 함, PR대기 유지
                lines.append(f"↩️ 보류(판단 실패) — {title[:40]}: {reason}")
        finally:
            eng._git(["worktree", "remove", "--force", str(wt)], eng.PROJECT_ROOT)
            if applied:  # 완료/보류로 확정된 것만 브랜치 정리
                eng._git(["branch", "-D", branch], eng.PROJECT_ROOT)
        eng.save_dev_state(state)

    body = "\n".join(lines) if lines else "처리 없음"
    merged_n = sum(1 for x in lines if x.startswith("✅"))
    rejected_n = sum(1 for x in lines if x.startswith("🛑"))
    if do_send and lines:
        send(f"🧭 예원 — 펫나 PR 검토 {len(pend)}건 (병합 {merged_n}·반려 {rejected_n})\n\n{body}",
             silent=(merged_n == 0))
    return body


def main() -> None:
    with ProcessLock("yewon_petnna_pr_reviewer"):
        print(review_all(do_send="--no-send" not in sys.argv))


if __name__ == "__main__":
    main()
