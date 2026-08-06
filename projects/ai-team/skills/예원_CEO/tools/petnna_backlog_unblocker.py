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
from _shared.backlog import backlog_lock  # noqa: E402

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


def analyze() -> dict:
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
        reason = _reason(rec, item)
        cat = classify(reason, _predates_facts(rec.get("reviewed_at", "")))
        rows.append({
            "id": fp, "title": rec.get("title") or item.get("title", ""),
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
