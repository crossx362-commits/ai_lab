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
from _shared.cc import extract_json  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402  (병합 품질 판단 — 올라마 우선, 실패 시 구독 폴백)
from _shared.backlog import recent_reviewed_items, format_recent_decisions  # noqa: E402

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


def _ask_yewon(title: str, detail: str, diff: str, files: list[str],
                item_type: str = "", item_id: str = "") -> tuple[str, str]:
    """예원(클로드)에게 diff 리뷰를 맡겨 approve/reject 결정을 받는다."""
    recent = recent_reviewed_items(eng.BACKLOG, limit=8, item_type=item_type, exclude_id=item_id)
    recent_block = format_recent_decisions(recent)
    prompt = (
        "너는 펫나(반려동물 웹앱) 개발팀 CEO '예원'이다. 수리가 자동 구현한 아래 UI/UX "
        "브랜치를 master(운영)에 병합할지 최종 결정하라. 안전 게이트(경로·크기·E2E)는 이미 "
        "통과했다. 너는 품질만 본다.\n\n"
        f"[과제] {title}\n[상세] {detail or '(없음)'}\n"
        f"[변경 파일] {', '.join(files[:8])}\n\n"
        + (recent_block + "\n\n" if recent_block else "") +
        "[판단 기준]\n"
        "- 과제 의도에 부합하는가, 디자인 시스템(코랄 brand-500 계열·기존 톤)과 어긋나지 않는가\n"
        "- 명백한 회귀·레이아웃 파손·접근성 후퇴 위험이 없는가\n"
        "- 운영에 바로 나가도 부끄럽지 않은 완성도인가\n"
        "- 위 [최근 검토된 관련 결정]과 모순되는 변경(예: 최근 승인·통합한 걸 다시 분리·되돌림)은 "
        "아니었는가 — 모순되면 실질 피해가 적어 보여도 reject하라(디자인이 매번 진자처럼 왔다갔다 하는 것 방지)\n"
        "애매하면 reject(사람 검토로 남김)가 안전하다. 과하게 관대하지 마라.\n\n"
        "[diff]\n```diff\n" + diff[:MAX_DIFF_CHARS] +
        ("\n...(생략)" if len(diff) > MAX_DIFF_CHARS else "") + "\n```\n\n"
        '반드시 JSON만 출력: {"decision":"approve"|"reject","reason":"<한 줄 근거(한국어)>"}'
    )
    # 도구 불필요(diff는 프롬프트에 포함) → 올라마 우선. 하드 안전게이트(경로·크기·E2E)는
    # 이미 통과했고, 애매하면 reject/defer가 안전한 판단이라 로컬 품질로도 리스크 제한적.
    out = llm_text(prompt, json_mode=True, lm_first=True, max_tokens=1500)
    if not out:
        return "defer", "예원 판단 실패: LLM 응답 없음"
    data = extract_json(out) or {}
    decision = str(data.get("decision", "")).lower()
    reason = str(data.get("reason", "")).strip()[:200]
    if decision not in ("approve", "reject"):
        return "defer", f"판단 형식 오류: {out[:120]}"
    return decision, reason or "(근거 없음)"


def _reject_route(rec: dict, reason: str, quality: bool) -> str:
    """반려 라우팅 — 품질 반려는 시도 한도 내에서 피드백과 함께 수리에게 되돌린다.

    크리틱 환류 루프(2026-07-15): 예전엔 모든 반려가 즉시 '보류'(사람 검토 대기)라
    예원의 반려 사유가 기록만 되고 재사용되지 않았다 — 리뷰어의 노트를 작성자에게
    돌려줘 재작업시키는 루프가 없었던 것. 품질 반려(_ask_yewon 판단)는 MAX_ATTEMPTS
    내라면 '대기'로 되돌리고 사유를 rec["review_feedback"]에 쌓는다 — 수리가 재시도
    프롬프트에 이 피드백을 주입해(claude_fix) 같은 실수를 반복하지 않게 한다.
    하드 게이트 반려(금지경로·E2E 신규실패·빈 diff)와 한도 소진은 기존대로 '보류'
    (구조적 문제 — 같은 프롬프트로 재시도해도 같은 결과라 환류 무의미).
    재시도 상한은 수리 쪽 attempts/MAX_ATTEMPTS가 그대로 문다 — 무한 핑퐁 없음.
    """
    if quality and rec.get("attempts", 0) < eng.MAX_ATTEMPTS:
        rec["review_feedback"] = (rec.get("review_feedback") or [])[-4:] + [reason]
        return "대기"
    return "보류"


def _merge(branch: str) -> tuple[bool, str]:
    dirty = eng._git(["status", "--porcelain", "--", "projects/petnna"],
                     eng.PROJECT_ROOT).stdout.strip()
    if dirty:
        return False, "main 워킹트리 petnna 경로에 미커밋 변경 존재 — 병합 보류"
    m = eng._git(["merge", "--no-ff", branch, "-m",
                  f"merge: {branch} (예원 자동 검토 승인)"], eng.PROJECT_ROOT)
    if m.returncode != 0:
        # 충돌 시 mid-merge 상태를 방치하면 충돌 마커가 박힌 작업트리가 그대로 남아
        # 다음 커밋·배포를 오염시킨다(2026-07-19 실사고: 나무_20260717110713_1 병합
        # 충돌이 index.html에 마커 박힌 채 방치 → 문서 두 벌이 통째로 섞여 서빙됨).
        # 즉시 원상복구 — abort 실패(MERGE_HEAD 없음 등)는 무해하므로 결과 무시.
        eng._git(["merge", "--abort"], eng.PROJECT_ROOT)
        return False, f"병합 실패(원상복구됨): {m.stderr.strip()[:150]}"
    return True, "master 병합 완료"


def _push_master() -> str:
    """병합 후 원격 push — 이게 없으면 로컬 master에만 쌓이고 GitHub·배포는
    영원히 안 바뀐다(2026-07-12 오너 발견: 18커밋이 push 안 된 채 로컬에만 쌓여
    있었음 — 수리·예원 둘 다 merge만 하고 push하는 코드가 아예 없었다)."""
    p = eng._git(["push"], eng.PROJECT_ROOT)
    if p.returncode == 0:
        return "✅ push 완료"
    return f"⚠️ push 실패(로컬 병합은 유지됨): {p.stderr.strip()[:150]}"


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

            quality = False  # 하드 게이트 반려 vs 품질 반려 구분(_reject_route 환류 판정용)
            if not gate_ok:
                decision, reason = "reject", f"안전 게이트 거부: {gate_note}"
            elif new_fail:
                decision, reason = "reject", f"E2E 신규 실패: {new_fail[:3]}"
            elif not diff.strip():
                decision, reason = "reject", "빈 diff(변경 없음)"
            else:
                quality = True
                decision, reason = _ask_yewon(title, it.get("detail", ""), diff, files,
                                               item_type=it.get("type", ""), item_id=fp)

            if decision == "approve":
                merged, mnote = _merge(branch)
                if merged:
                    rec["status"] = "완료"
                    rec["reviewed_by"] = "예원"
                    rec["reviewed_at"] = datetime.now().isoformat()
                    eng._update_backlog(fp, "완료", reason)
                    applied = True
                    lines.append(f"✅ 병합 — {title[:40]}\n   예원: {reason}")
                else:
                    lines.append(f"⏸️ 승인했으나 {mnote} — {title[:40]}")
            elif decision == "reject":
                new_status = _reject_route(rec, reason, quality)
                rec["status"] = new_status
                rec["reviewed_by"] = "예원"
                rec["reviewed_at"] = datetime.now().isoformat()
                rec["review_reason"] = reason
                eng._update_backlog(fp, new_status, reason)
                applied = True
                if new_status == "대기":
                    lines.append(f"🔁 반려→피드백 재시도 — {title[:40]}\n   예원: {reason}")
                else:
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
    rejected_n = sum(1 for x in lines if x.startswith(("🛑", "🔁")))
    if merged_n:
        body += f"\n{_push_master()}"
    if do_send and lines:
        send(f"🧭 예원 — 펫나 PR 검토 {len(pend)}건 (병합 {merged_n}·반려 {rejected_n})\n\n{body}",
             silent=(merged_n == 0))
    return body


def main() -> None:
    with ProcessLock("yewon_petnna_pr_reviewer"):
        print(review_all(do_send="--no-send" not in sys.argv))


if __name__ == "__main__":
    main()
