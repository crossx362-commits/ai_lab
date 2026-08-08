#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 백로그 정체 해소기 (오너 지시 2026-08-06 "앞으로 그런일 발생하면 예원이가 해결해").

무엇을 푸는가
-------------
자동 개선 루프는 과제를 3회 시도해 실패하면 `보류`로 옮긴다. 이 상태는 **아무도 다시
꺼내주지 않는다** — 그래서 게이트가 잘못돼 반려된 것과 진짜로 못 하는 것이 같은 무덤에
쌓인다. 2026-08-06 실측으로 보류 67건이 쌓였고, 그중 절반 이상이 **게이트 쪽 문제**였다:

  · 시크릿 스캐너가 `password: "비밀번호를 입력해 주세요"` 같은 UI 문구를 시크릿으로 오판 (9건)
  · 크기 상한(200줄)이 기능 과제에 구조적으로 안 맞음 — 쪼개줄 담당자가 없음 (12건)
  · E2E 1회 실패로 반려 — 변경과 무관한 테스트가 지목됐고 재현되지 않음 (15건)

핵심 통찰: **같은 사유가 여러 건에서 반복되면 그건 과제의 문제가 아니라 게이트의 문제다.**
사람이 매번 이걸 발견해 주는 대신 예원이 스스로 판정하고 되돌린다.

무엇을 하는가
-------------
1. 보류 항목을 반려 사유별로 묶는다.
2. **자동 해소 가능**: 지금의 게이트로 다시 판정했을 때 더 이상 걸리지 않는 사유는
   `대기`로 되돌리고 시도 횟수를 초기화한다(게이트가 고쳐졌다는 뜻).
3. **재시도 가치 있음**: E2E 1회 실패로 반려된 것은 그 테스트가 지금 master에서
   통과하는지 확인해, 통과하면 flaky로 보고 되돌린다.
4. **구조적 정체**: 같은 사유가 THRESHOLD회 이상 반복되면 게이트 결함 후보로 보고한다
   (자동으로 게이트를 고치지는 않는다 — 그건 코드 변경이라 사람 몫).
5. 되돌린 것은 조용히, **못 되돌린 구조적 정체만** 텔레그램으로 보고한다(오너 지시 §1-5).

안전선
------
· 코드를 고치지 않는다. 백로그 상태만 되돌린다 — 되돌려도 자동 병합은 여전히 없고
  수리가 다시 구현해 PR대기로 갈 뿐이다.
· 사람이 명시적으로 판단한 보류(gate="사람 판단 트랙", owner="사람", 오너 결정 사유)는
  절대 건드리지 않는다.
· 한 번에 되돌리는 수를 제한한다 — 60건을 한꺼번에 풀면 수리가 몇 주치 큐에 묻힌다.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import Counter
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.backlog import backlog_lock, already_exists_evidence  # noqa: E402

load_env(str(PROJECT_ROOT))

QA = PROJECT_ROOT / "output" / "qa" / "petnna"
BACKLOG = QA / "backlog.json"
DEV_STATE = QA / "dev" / "dev_state.json"
E2E_RESULTS = QA / "tests" / "results.json"

# 같은 사유가 이만큼 반복되면 "과제가 아니라 게이트가 문제"로 본다.
PATTERN_THRESHOLD = int(os.getenv("YEWON_UNBLOCK_THRESHOLD", "3"))
# 한 번에 되돌릴 최대 건수 — 전부 풀면 수리 큐가 몇 주치로 묻힌다.
MAX_RELEASE = int(os.getenv("YEWON_UNBLOCK_MAX", "8"))

# 사람이 판단한 보류 — 절대 자동으로 되돌리지 않는다.
HUMAN_MARKERS = ("오너 결정", "오너 지시", "사람 판단", "사람 검토", "승인필요",
                 "당분간 나만", "공개 계획", "오너 우선순위", "우선순위 판단",
                 "에이전트 코드", "재판단할 것")


def _reason(rec: dict, item: dict) -> str:
    return " ".join(str(x) for x in (
        rec.get("review_reason", ""), item.get("gate", ""),
        item.get("resolution", ""), item.get("note", "")) if x)


def classify(reason: str, predates_facts: bool = False) -> str:
    """반려 사유를 자동 판정 가능한 범주로 접는다.

    predates_facts: 이 판단이 PETNNA_FACTS 주입 이전이면 사실 착오 가능성을 본다.
    """
    if not reason.strip():
        return "사유 미기록"
    if any(m in reason for m in HUMAN_MARKERS):
        return "사람 판단(보존)"
    if "시크릿" in reason or "인증 의심" in reason:
        return "시크릿 스캐너"
    if "변경 과대" in reason:
        return "크기 게이트"
    if "금지 경로" in reason:
        return "금지 경로"
    if "E2E 신규 실패" in reason:
        return "E2E 신규 실패"
    if _FACT_ERROR.search(reason) and predates_facts:
        return "리뷰어 사실 오류"
    if "Supabase 신규 계약" in reason:
        return "DB 계약"
    return "품질 판단"


# 리뷰어가 프로젝트 사실(앱 이름·brand 팔레트)을 몰라 낸 반려 — 재시도 대상이다.
#
# **텍스트만으로 판정하면 안 된다**: 사실을 주입한 뒤의 색상 지적은 정당한 판단인데,
# "브랜드 컬러" 같은 낱말로 잡으면 그것까지 뒤집는다(회귀 테스트가 이 오류를 잡았다).
# 그래서 **시점**으로 가른다 — 사실 주입 이전에 내려진 판단만 근거가 부족했던 것이다.
_FACTS_INJECTED_AT = "2026-08-06T23:00:00"   # _shared/petnna_facts.py 도입 시각
_FACT_ERROR = re.compile(r"'펫나'|브랜드 정체성|브랜드 컬러|코랄|coral", re.IGNORECASE)


def _predates_facts(reviewed_at: str) -> bool:
    """이 판단이 프로젝트 사실 주입 이전에 내려졌는가. 시각을 모르면 False(보존)."""
    return bool(reviewed_at) and str(reviewed_at) < _FACTS_INJECTED_AT


def _current_e2e_pass() -> set[str]:
    """지금 master 스위트에서 통과하는 테스트 이름 — flaky 판정 근거."""
    try:
        d = json.loads(E2E_RESULTS.read_text(encoding="utf-8"))
        return {k for k, v in d.get("results", {}).items() if v.get("ok")}
    except Exception:
        return set()


def _gate_still_blocks(category: str, reason: str, passing: set[str]) -> bool:
    """지금의 게이트/상태로 다시 봐도 여전히 막히는가.

    막히지 않는다면 그 보류는 이미 유효기간이 지난 것이므로 되돌린다.
    """
    if category == "리뷰어 사실 오류":
        # 프롬프트에 PETNNA_FACTS를 주입했으므로 같은 사실 착오는 재현되지 않는다.
        # 진짜 색상 위반이면 리뷰어가 정확한 근거(brand-500)로 다시 반려한다.
        return False
    if category == "시크릿 스캐너":
        # 스캐너를 값 모양 기준으로 좁혔다(2026-08-06). 옛 오탐은 더 이상 재현되지 않는다.
        # 진짜 시크릿이면 수리가 다시 구현할 때 그대로 다시 걸린다 — 되돌려도 안전하다.
        return False
    if category == "크기 게이트":
        # 백로그 상한을 400줄로 완화했다. 그 안에 드는 건만 되돌린다.
        m = re.search(r"(\d+)줄", reason)
        return bool(m) and int(m.group(1)) > 400
    if category == "E2E 신규 실패":
        named = re.findall(r"'(test_\w+)'", reason)
        if not named or not passing:
            return True                      # 판정 불가 — 보존(빈 응답을 부재로 읽지 않는다)
        return not all(t in passing for t in named)   # 지금 다 통과하면 그때 실패는 flaky
    return True                              # 금지 경로·DB 계약·품질 판단은 예원이 못 푼다


# ── 유령 과제 탐지 (2026-08-08) ────────────────────────────────────────────
#
# 왜 필요한가: 회의·미오·나무가 "X를 구현하라"고 적재할 때 **X가 이미 있는지 확인하지
# 않는다**. 2026-08-08 전수 조사에서 보류 53건 중 5건이 그 경로였고, 그중 3건은 회의가
# 요구한 회귀 테스트까지 이미 존재했다(예: "structurally_blocked에 엔진 자기수정 판별
# 추가 + 회귀 테스트 동반" → touches_agent_ops()와 test_backlog_agent_ops_routing.py가
# 이미 있었다). 이런 항목은 자동 루프를 돌면 반드시 3회 실패하고, 사람이 전수 조사를
# 해야만 발견된다.
#
# **절대 자동 종결하지 않는다 — 보고만 한다.** 같은 조사에서 반례를 봤다:
# 미오_20260716231002_0(placeholder 대비 강화)은 "이미 완료"로 적혀 있었지만 실측하니
# 5.3:1로 목표 7:1에 미달이었다. "있긴 한데 목표 미달"을 유령으로 닫으면 진짜 남은
# 일이 사라진다. 기계는 실재만 말하고, 충분한지는 사람이 판단한다.
#
# analyze()와 달리 **백로그를 직접 순회한다** — analyze()는 dev_state를 돌기 때문에
# 수리가 한 번도 집지 않은 항목(2026-08-08 기준 53건 중 23건)이 아예 안 보인다.

# 판별 자체는 `_shared/backlog.already_exists_evidence()`가 한다 — 적재 시점(미오·나무)과
# 여기(사후 감사)가 같은 판별을 써야 한 쪽만 갱신돼 어긋나지 않는다.


def ghost_candidates() -> list[dict]:
    """보류 항목 중 '요구한 산출물이 이미 실재하는' 것 — 종결 후보로 보고만 한다."""
    try:
        backlog = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        return []
    out = []
    for it in backlog.get("items", []):
        if it.get("status") != "보류":
            continue
        evidence = already_exists_evidence(it.get("title", ""), str(it.get("detail", "")))
        if evidence:
            out.append({"id": it.get("id"), "title": it.get("title", ""),
                        "owner": it.get("owner", ""), "evidence": evidence,
                        "detail": str(it.get("detail", ""))[:600]})
    return out


def _llm_second_opinion(cands: list[dict]) -> dict[str, str]:
    """기계 신호가 붙은 후보에만 로컬 모델(Ollama 우선) 소견을 덧붙인다.

    판정 권한은 없다 — 사람이 읽을 근거를 한 줄 더 얹을 뿐이다. 로컬이 죽어 있으면
    조용히 건너뛴다(이 기능 때문에 정체 해소 본편이 실패하면 안 된다).
    """
    if not cands:
        return {}
    try:
        from _shared.llm import text as llm_text
    except Exception:
        return {}
    notes = {}
    for c in cands[:5]:                      # 보고용이라 상위 몇 건이면 충분하다
        try:
            ans = llm_text(
                "아래는 소프트웨어 백로그의 보류 과제와, 그 과제가 요구한 산출물이 "
                "저장소에 이미 존재한다는 기계적 증거다. 이 과제가 '이미 구현돼 종결해도 되는 것'인지, "
                "아니면 '일부만 구현돼 남은 일이 있는 것'인지 한 줄로만 답하라. 추측하지 말고 "
                "증거가 부족하면 '판단 불가'라고 하라.\n\n"
                f"[과제] {c['title']}\n[과제 상세] {c.get('detail', '')}\n"
                "[증거] " + " / ".join(c["evidence"]),
                lm_first=True, task="coding", max_tokens=200)
            if ans:
                notes[c["id"]] = ans.strip().splitlines()[0][:160]
        except Exception:
            continue
    return notes


def analyze() -> dict:
    """보류 항목을 분석한다.

    2026-08-08 수정: dev_state.issues만 순회하면 **수리가 한 번도 집지 않은 항목이
    아예 안 보인다** — 실측 2026-08-08, 보류 50건 중 28건이 dev_state에 없었다
    (회의·미오·나무가 적재 시점에 곧바로 gate="사람 판단 트랙"/"DB/인증" 등으로 보류
    시킨 것들이라 수리 루프에 진입한 적이 없다). analyze()가 "구조적 정체 몇 건"을
    보고할 때 실제로는 절반 넘게 안 보고 있었던 것.

    dev_state에 없는 항목은 수리가 낸 반려 사유(review_reason)가 없으므로, 적재한
    쪽이 이미 item 자체에 남긴 gate/resolution을 사유로 쓴다 — `_reason()`이 원래도
    이 필드들을 함께 보므로 rec={}로 넘기면 그대로 동작한다. release()는 이미
    dev_state에 항목이 없으면 그 부분만 건너뛰도록 짜여 있어(`rec = issues.get(fp);
    if rec: ...`) 여기서 추가 처리가 필요 없다.
    """
    try:
        backlog = json.loads(BACKLOG.read_text(encoding="utf-8"))
        dev = json.loads(DEV_STATE.read_text(encoding="utf-8"))
    except Exception as e:
        return {"error": str(e)}
    items = {i["id"]: i for i in backlog.get("items", [])}
    issues = dev.get("issues", {})
    passing = _current_e2e_pass()

    rows = []
    for fp, rec in issues.items():
        if rec.get("status") != "보류":
            continue
        item = items.get(fp, {})
        # dev_state가 보류로 남아 있어도 백로그가 이미 다른 상태(예: 오늘 유령 과제로
        # 완료 종결됨)면 뒤진 기록이다 — 계속 넣으면 release()가 사유 문구 매칭에만
        # 기대어 우연히 안전한 것이지, 사유가 사람판단 마커와 안 겹치는 순간 방금
        # 닫은 항목을 대기로 되돌려버린다(2026-08-08 실측: 이 세션이 오늘 종결한
        # 유령 2건이 바로 이 상태였다). 백로그가 진실이므로 백로그 상태를 우선한다.
        if fp in items and item.get("status") != "보류":
            continue
        reason = _reason(rec, item)
        cat = classify(reason, _predates_facts(rec.get("reviewed_at", "")))
        rows.append({
            "id": fp, "title": rec.get("title") or item.get("title", ""),
            "category": cat, "reason": reason,
            "releasable": cat not in ("사람 판단(보존)", "사유 미기록")
                          and not _gate_still_blocks(cat, reason, passing),
        })

    seen = set(issues)
    for it in backlog.get("items", []):
        fp = it.get("id")
        if it.get("status") != "보류" or fp in seen:
            continue
        reason = _reason({}, it)
        cat = classify(reason, False)   # dev_state가 없어 리뷰 시각 자체가 없다 — 보존 취급
        rows.append({
            "id": fp, "title": it.get("title", ""),
            "category": cat, "reason": reason,
            "releasable": cat not in ("사람 판단(보존)", "사유 미기록")
                          and not _gate_still_blocks(cat, reason, passing),
        })

    return {"rows": rows, "counts": Counter(r["category"] for r in rows),
            "passing_tests": len(passing)}


def release(rows: list[dict], limit: int) -> list[dict]:
    """되돌릴 항목을 `대기`로 옮기고 시도 횟수를 초기화한다.

    시도 초기화가 핵심이다 — 상태만 대기로 바꾸고 attempts를 남기면 수리가 집자마자
    MAX_ATTEMPTS 필터에 다시 걸려 조용히 탈락한다(2026-07-11에 같은 함정을 겪었다).
    """
    picked = [r for r in rows if r["releasable"]][:limit]
    if not picked:
        return []
    ids = {r["id"] for r in picked}
    with backlog_lock():
        backlog = json.loads(BACKLOG.read_text(encoding="utf-8"))
        for it in backlog.get("items", []):
            if it.get("id") in ids:
                it["status"] = "대기"
                it["resolution"] = (
                    f"[예원 자동 해소 {datetime.now():%Y-%m-%d}] 반려 사유가 게이트 결함이었고 "
                    f"그 게이트가 수정돼 더 이상 적용되지 않는다 — 재시도로 되돌림.")
                it.pop("gate", None)
        BACKLOG.write_text(json.dumps(backlog, ensure_ascii=False, indent=1), encoding="utf-8")

    dev = json.loads(DEV_STATE.read_text(encoding="utf-8"))
    for fp in ids:
        rec = dev["issues"].get(fp)
        if rec:
            rec["status"] = "대기"
            rec["attempts"] = 0            # ← 이게 없으면 집자마자 다시 탈락한다
            rec["unblocked_at"] = datetime.now().isoformat()
    DEV_STATE.write_text(json.dumps(dev, ensure_ascii=False, indent=1), encoding="utf-8")
    return picked


def run(do_send: bool, dry: bool = False) -> int:
    res = analyze()
    if "error" in res:
        print(f"[정체해소] 읽기 실패: {res['error']}")
        return 0
    rows, counts = res["rows"], res["counts"]
    print(f"[{datetime.now():%Y-%m-%d %H:%M}] 🧯 예원 백로그 정체 해소 "
          f"(보류 {len(rows)}건, master 통과 테스트 {res['passing_tests']}개)")
    for c, n in counts.most_common():
        rel = sum(1 for r in rows if r["category"] == c and r["releasable"])
        print(f"  {n:3}건  {c}" + (f"  → 해소 가능 {rel}건" if rel else ""))

    freed = [] if dry else release(rows, MAX_RELEASE)
    for r in freed:
        print(f"  ✅ 해소: [{r['category']}] {r['title'][:52]}")

    # 못 푼 구조적 정체만 보고한다 — 같은 사유가 임계 이상 반복되면 게이트 결함 후보.
    stuck = Counter(r["category"] for r in rows
                    if not r["releasable"] and r["category"] not in ("사람 판단(보존)",))
    report = [f"· {c} {n}건" for c, n in stuck.most_common() if n >= PATTERN_THRESHOLD]
    if report:
        print("  ⚠️ 구조적 정체(예원이 못 푼 것):")
        for line in report:
            print(f"    {line}")
        if do_send:
            send("🧯 [예원] 백로그 구조적 정체 — 게이트 결함 후보\n\n"
                 + "\n".join(report)
                 + f"\n\n같은 사유가 {PATTERN_THRESHOLD}건 이상 반복되면 과제가 아니라 "
                   "게이트 쪽을 의심해야 합니다."
                 + (f"\n이번에 자동 해소한 것: {len(freed)}건" if freed else ""))

    # 유령 과제 — 요구한 산출물이 이미 실재하는 보류. 보고만 하고 절대 자동 종결하지 않는다.
    ghosts = ghost_candidates()
    if ghosts:
        notes = _llm_second_opinion(ghosts)
        print(f"  👻 유령 과제 후보 {len(ghosts)}건 (요구 산출물이 이미 실재 — 사람 확인 필요):")
        for g in ghosts:
            print(f"    · [{g['id']}] {g['title'][:50]}")
            for e in g["evidence"][:2]:
                print(f"        {e}")
            if notes.get(g["id"]):
                print(f"        소견(로컬): {notes[g['id']]}")
        if do_send:
            lines = [f"· [{g['id']}] {g['title'][:44]}\n  {g['evidence'][0]}"
                     + (f"\n  소견: {notes[g['id']]}" if notes.get(g["id"]) else "")
                     for g in ghosts[:6]]
            send(f"👻 [예원] 유령 과제 후보 {len(ghosts)}건 — 요구한 산출물이 이미 있습니다\n\n"
                 + "\n".join(lines)
                 + "\n\n자동 종결하지 않았습니다. '있긴 한데 목표 미달'일 수 있어 "
                   "사람이 확인해야 합니다(2026-08-08 placeholder 대비 사례).")
    return len(freed)


def main() -> None:
    ap = argparse.ArgumentParser(description="예원 — 백로그 정체 해소")
    ap.add_argument("--once", action="store_true")
    ap.add_argument("--dry", action="store_true", help="분석만, 상태 변경 없음")
    ap.add_argument("--send", action="store_true", help="구조적 정체를 텔레그램 보고")
    args = ap.parse_args()
    run(do_send=args.send, dry=args.dry)


if __name__ == "__main__":
    main()
