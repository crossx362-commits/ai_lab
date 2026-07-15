"""반려 피드백 환류(크리틱 루프) 회귀 테스트 — 2026-07-15 신설.

예전엔 예원이 품질 사유로 반려한 브랜치가 즉시 '보류'(영구 정지)돼, 반려 사유가
기록만 되고 누구에게도 재사용되지 않았다 — 리뷰어의 노트를 작성자(수리)에게 돌려줘
재작업시키는 루프의 부재. 이 테스트는 그 환류가 계속 작동하는지 굳힌다:

  예원 품질 반려 → (시도 한도 내) 상태 '대기' + 사유를 review_feedback에 적재
  → 수리 재시도 시 claude_fix 프롬프트에 그 사유가 주입됨.

하드 게이트 반려(금지경로·E2E 신규실패·빈 diff)와 시도 한도 소진은 기존대로 '보류'.
"""
import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

ENG_PATH = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"
REV_PATH = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_pr_reviewer.py"


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


# pr_reviewer가 `import petnna_dev_engine`을 하므로 같은 이름으로 먼저 적재해 공유시킨다
eng = load("petnna_dev_engine", ENG_PATH)
rev = load("pr_reviewer_under_test", REV_PATH)


class RejectRouteTests(unittest.TestCase):
    """예원 _reject_route — 품질 반려만, 한도 내에서만 환류한다."""

    def test_quality_reject_under_limit_goes_back_to_wait(self):
        rec = {"attempts": 1}
        status = rev._reject_route(rec, "버튼 색이 디자인 시스템과 어긋남", quality=True)
        self.assertEqual(status, "대기")
        self.assertEqual(rec["review_feedback"], ["버튼 색이 디자인 시스템과 어긋남"])

    def test_quality_reject_at_limit_holds(self):
        rec = {"attempts": eng.MAX_ATTEMPTS}
        status = rev._reject_route(rec, "여전히 미흡", quality=True)
        self.assertEqual(status, "보류")
        self.assertNotIn("review_feedback", rec, "재시도가 없는데 피드백을 쌓으면 안 된다")

    def test_hard_gate_reject_always_holds(self):
        rec = {"attempts": 0}
        status = rev._reject_route(rec, "안전 게이트 거부: 금지 경로", quality=False)
        self.assertEqual(status, "보류")
        self.assertNotIn("review_feedback", rec)

    def test_feedback_list_is_capped(self):
        rec = {"attempts": 0}
        for i in range(7):
            rev._reject_route(rec, f"사유{i}", quality=True)
        self.assertLessEqual(len(rec["review_feedback"]), 5)
        self.assertEqual(rec["review_feedback"][-1], "사유6", "최신 사유가 보존돼야 한다")


class FeedbackIntoPromptTests(unittest.TestCase):
    """수리 쪽 절반 — 피드백 부착과 프롬프트 주입."""

    def test_attach_review_feedback_takes_last_two(self):
        finding = {}
        eng.attach_review_feedback(finding, {"review_feedback": ["a", "b", "c"]})
        self.assertEqual(finding["feedback"], ["b", "c"])

    def test_attach_review_feedback_noop_without_feedback(self):
        finding = {}
        eng.attach_review_feedback(finding, {})
        self.assertNotIn("feedback", finding)

    def _run_claude_fix(self, finding: dict) -> str:
        with mock.patch.object(eng, "_find_claude", return_value="claude"), \
             mock.patch.object(eng.subprocess, "run") as m:
            m.return_value = mock.Mock(returncode=0, stdout="ok", stderr="")
            ok, _ = eng.claude_fix(Path("."), finding)
            self.assertTrue(ok)
            return m.call_args.kwargs["input"]

    def test_prompt_contains_feedback_when_present(self):
        prompt = self._run_claude_fix({
            "env": "-", "title": "카드 여백 개선", "priority": "P3", "type": "디자인",
            "feedback": ["버튼 색이 디자인 시스템(코랄)과 어긋남"],
        })
        self.assertIn("반려 피드백", prompt)
        self.assertIn("버튼 색이 디자인 시스템(코랄)과 어긋남", prompt)

    def test_prompt_has_no_feedback_block_by_default(self):
        prompt = self._run_claude_fix({
            "env": "-", "title": "카드 여백 개선", "priority": "P3", "type": "디자인",
        })
        self.assertNotIn("반려 피드백", prompt)


if __name__ == "__main__":
    unittest.main()
