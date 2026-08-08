"""보류로 **적재**할 때도 사유를 남긴다 (2026-08-08 실측).

무슨 일이 있었나
----------------
2026-08-06에 "수리 엔진의 3회 실패 → 보류 전환이 사유를 안 남긴다"를 고쳤다. 그런데
같은 계열의 결함이 **다른 지점**에 남아 있었다 — 회의(petnna_council)가 적재 시점에
보류로 넣을 때는 이랬다:

    **({"gate": "사람 판단 트랙"} if _owner == HUMAN_OWNER else {}),

즉 `owner == '사람'`인 경우에만 gate를 남기고, 나머지 사유(승인필요·DB/인증·에이전트
코드·owner 불일치·type 불일치)는 **아무 기록도 안 남겼다**. 그 결과 보류 10건이
`attempts=None`(수리가 집은 적도 없음) + 사유 공란으로 쌓였고, 예원의 정체 해소기는
근거가 없어 "판정 불가"로 보존만 반복했다.

여기서 굳히는 것:
  1. `block_reason()`이 구조적 보류 사유를 **문자열로** 돌려준다(bool이 아니라).
  2. `[승인필요]`는 `structurally_blocked`에 **들어가지 않는다** — 그건 승인되면 풀려야
     하는 사유이고, promote_approved_holds가 그 함수로 승격을 판단하기 때문이다.
     넣으면 승인받아도 영원히 승격되지 않는다(이 테스트를 쓰며 실제로 그 회귀를 냈다).
  3. 적재 사유는 `hold_reason()`이 `[승인필요]`까지 포함해 만든다.
  4. 회의 적재 코드가 그 사유를 gate에 기록한다 — owner 조건부가 아니라.
"""
import ast
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.backlog import (  # noqa: E402
    block_reason, hold_reason, structurally_blocked, needs_human,
)

_COUNCIL = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"


class BlockReasonTests(unittest.TestCase):
    def test_reasons_are_specific_strings(self):
        cases = [
            ("소비자 없는 owner", dict(owner="나무", item_type="기획", title="뭔가", detail=""), "owner"),
            ("owner+type 불일치", dict(owner="테오", item_type="디자인", title="뭔가", detail=""), "불일치"),
            ("DB/인증",          dict(owner="수리", item_type="기능", title="신규 테이블 추가", detail=""), "DB"),
            ("에이전트 코드",     dict(owner="수리", item_type="기능", title="diff_gate 개선", detail=""), "에이전트"),
            ("미오 구현 요구",    dict(owner="미오", item_type="디자인", title="지금 구현해라", detail=""), "미오"),
        ]
        for label, kw, needle in cases:
            r = block_reason(kw["owner"], kw["item_type"], kw["title"], kw["detail"])
            self.assertIsNotNone(r, f"{label}: 막혀야 하는데 None")
            self.assertIn(needle, r, f"{label}: 사유가 구체적이지 않다 → {r!r}")

    def test_normal_task_has_no_reason(self):
        self.assertIsNone(
            block_reason("수리", "디자인", "건강 탭 카드 여백 정리", "templates/health.js 수정"),
            "정상 과제를 막았다 — 자동 루프가 굶는다")

    def test_approval_tag_is_not_structural(self):
        """승인받으면 풀려야 하는 사유를 structurally_blocked에 넣으면 영구 정지한다."""
        t = "[승인필요] 무언가 위험한 작업"
        self.assertFalse(structurally_blocked("수리", "기능", t, ""),
                         "[승인필요]가 구조적 차단으로 분류됐다 — 승인해도 승격되지 않는다")
        self.assertTrue(needs_human(t, "수리", "", "기능"),
                        "[승인필요]인데 사람 트랙으로 안 보낸다")
        self.assertIn("승인필요", hold_reason(t, "수리", "", "기능") or "",
                      "적재 사유에 승인필요가 안 담긴다")

    def test_hold_reason_matches_needs_human(self):
        """두 판정이 어긋나면 '보류인데 사유 없음' 또는 그 반대가 생긴다."""
        samples = [
            ("정상", "수리", "디자인", "카드 여백 정리", ""),
            ("승인필요", "수리", "기능", "[승인필요] 위험 작업", ""),
            ("DB", "수리", "기능", "RLS 정책 변경", ""),
            ("소비자없음", "나무", "기획", "아무거나", ""),
        ]
        for label, owner, itype, title, detail in samples:
            human = needs_human(title, owner, detail, itype)
            reason = hold_reason(title, owner, detail, itype)
            self.assertEqual(human, reason is not None,
                             f"{label}: needs_human={human} 인데 hold_reason={reason!r}")


class CouncilRecordsReasonTests(unittest.TestCase):
    """회의 적재가 사유를 조건 없이 남기는가 — owner=='사람'일 때만 남기던 게 원인이었다."""

    def test_gate_is_written_from_hold_reason(self):
        src = _COUNCIL.read_text(encoding="utf-8")
        self.assertIn("hold_reason(", src, "회의가 hold_reason을 쓰지 않는다")
        self.assertNotIn('"gate": "사람 판단 트랙"', src,
                         "owner 조건부로만 gate를 남기는 옛 코드가 남아 있다 — "
                         "나머지 보류 사유가 기록 없이 쌓인다")
        self.assertIn('"gate": _hold', src, "적재 시 gate에 사유를 싣지 않는다")

    def test_council_still_imports_single_source(self):
        """사유 판정은 _shared/backlog.py 하나여야 한다(두 곳에 나열하면 어긋난다)."""
        tree = ast.parse(_COUNCIL.read_text(encoding="utf-8"))
        imported = {a.name for n in ast.walk(tree) if isinstance(n, ast.ImportFrom)
                    and n.module and "backlog" in n.module for a in n.names}
        self.assertIn("hold_reason", imported)
        self.assertIn("needs_human", imported)


if __name__ == "__main__":
    unittest.main()
