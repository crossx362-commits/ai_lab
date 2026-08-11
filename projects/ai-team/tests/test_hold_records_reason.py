"""보류로 옮길 때는 반드시 사유를 남긴다 (2026-08-06).

배경: 수리 엔진의 `attempts >= MAX_ATTEMPTS → 보류` 전환이 status만 바꾸고 사유를
기록하지 않았다. 그 결과 보류 68건 중 **9건이 "왜 막혔는지 모르는" 상태**로 쌓였다.
사유가 없으면 예원의 정체 해소기(petnna_backlog_unblocker)도 판정할 수 없고 —
게이트 결함인지 진짜 불가인지 구분이 안 되므로 — 사람도 처음부터 다시 조사해야 한다.
사유 없는 보류는 영구 무덤이다.

여기서 굳히는 것:
  1. 보류 전환 코드가 review_reason을 채운다(루프 로그의 실패 줄에서 뽑는다).
  2. 이미 사유가 있으면 덮어쓰지 않는다 — 예원 리뷰어의 구체적 판단이 더 정확하다.
  3. 해소기가 '사유 미기록'을 자동 해소 대상으로 삼지 않는다(판정 불가는 보존).
"""
import ast
import importlib.util
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"
_UNBLOCK = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_backlog_unblocker.py"


class HoldTransitionRecordsReasonTests(unittest.TestCase):
    def _hold_block(self):
        """`attempts >= MAX_ATTEMPTS` → status='보류'로 **대입하는** 블록의 AST 덤프.

        낱말만 보면 안 된다 — select_backlog의 '보류→대기 재개' 로직도 같은 낱말을
        전부 갖고 있어 엉뚱한 블록을 잡는다(이 테스트를 처음 쓸 때 실제로 그랬다).
        본문이 rec["status"]에 '보류'를 대입하는지로 특정한다.
        """
        tree = ast.parse(_ENGINE.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if not isinstance(node, ast.If) or "MAX_ATTEMPTS" not in ast.dump(node.test):
                continue
            for stmt in node.body:
                if (isinstance(stmt, ast.Assign)
                        and isinstance(stmt.value, ast.Constant) and stmt.value.value == "보류"):
                    return ast.dump(node)
        return ""

    def test_hold_sets_review_reason(self):
        dump = self._hold_block()
        self.assertTrue(dump, "3회 실패 → 보류 전환 블록을 찾지 못했다")
        self.assertIn("review_reason", dump,
                      "보류로 옮기면서 사유를 안 남긴다 — 예원도 사람도 재판단할 수 없다")

    def test_existing_reason_is_not_overwritten(self):
        """예원 리뷰어가 남긴 구체적 사유가 뭉뚱그린 문구로 덮이면 안 된다."""
        # AST 덤프에는 소스 문자열이 아니라 노드 구조가 담긴다 —
        # `if not rec.get("review_reason")` 은 UnaryOp(Not) + Constant("review_reason")로 나타난다.
        tree = ast.parse(_ENGINE.read_text(encoding="utf-8"))
        guarded = False
        for node in ast.walk(tree):
            if (isinstance(node, ast.If) and isinstance(node.test, ast.UnaryOp)
                    and isinstance(node.test.op, ast.Not)
                    and "review_reason" in ast.dump(node.test)):
                guarded = True
                break
        self.assertTrue(guarded,
                        "기존 사유를 보존하는 가드가 없다 — 예원 리뷰어의 구체 판단이 "
                        "뭉뚱그린 문구로 덮인다")

    def test_reason_is_derived_from_failure_lines(self):
        src = _ENGINE.read_text(encoding="utf-8")
        self.assertIn("fail_lines", src, "루프 로그에서 실패 줄을 뽑지 않는다")
        for kw in ("거부", "실패", "오류"):
            self.assertIn(f'"{kw}"', src, f"실패 신호 '{kw}'를 안 본다")


class UnblockerKeepsUnknownReasonTests(unittest.TestCase):
    """사유를 모르면 '판정 불가'다 — 자동 해소 대상으로 삼으면 안 된다."""

    def test_missing_reason_is_not_releasable(self):
        spec = importlib.util.spec_from_file_location("ub_reason_t", _UNBLOCK)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        self.assertEqual(mod.classify(""), "사유 미기록")
        self.assertEqual(mod.classify("   "), "사유 미기록")
        # 소스 문자열이 아니라 **실제 판정**을 본다 — 인라인 식을 문자열로 못 박으면
        # 판정을 공용 함수(_releasable)로 모으는 정당한 리팩터링이 막힌다
        # (2026-08-11에 실제로 막혔다).
        spec = importlib.util.spec_from_file_location("ub_rel_t", _UNBLOCK)
        m = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(m)
        self.assertFalse(m._releasable({}, "사유 미기록", "", set()),
                         "사유를 모르는 항목을 자동 해소 대상에 넣었다 — 근거 없이 되돌린다")
        self.assertFalse(m._releasable({}, "사람 판단(보존)", "오너 결정", set()),
                         "사람이 판단한 보류를 자동으로 되돌린다")


if __name__ == "__main__":
    unittest.main()
