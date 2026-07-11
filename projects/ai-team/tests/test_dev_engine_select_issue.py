"""수리 select_issue() 회귀 테스트 — 2026-07-11 '재발 무시' 버그를 테스트로 굳힌다.

발견 경위: "완료" 이슈가 재발하면 재도전해야 하는데, attempts 필드가 첫 라운드
(예: 2회 실패 + 3회째 성공 = attempts 3)에 이미 MAX_ATTEMPTS(3)에 도달해 있어,
재발 판정 바로 다음 줄의 attempts 필터에 걸려 조용히 탈락했다. 재발이 감지됐는데도
수리가 영원히 무시하고 재시도 자체를 안 하니 3회 실패 알림·회의 소집도 안 뜨는
방치 상태가 됐다(dev_state.json에서 실제 사례 확인: 'H1 3개(중복)' 이슈).
"""
import importlib.util
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def load_engine():
    spec = importlib.util.spec_from_file_location("dev_engine_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SelectIssueReoccurrenceTests(unittest.TestCase):
    def setUp(self):
        self.eng = load_engine()

    def test_reoccurred_issue_is_retried_despite_old_attempt_count(self):
        state = {"issues": {"a": {"status": "완료", "attempts": 3,
                                  "fixed_at": "2026-07-01T00:00:00"}}}
        findings = {"a": {"priority": "P2", "type": "접근성", "title": "재발"}}
        result = self.eng.select_issue(findings, state, "2026-07-11T12:00:00")
        self.assertIsNotNone(result, "재발한 이슈가 옛 attempts 때문에 무시되면 안 된다")
        self.assertEqual(result[0], "a")

    def test_reoccurrence_resets_attempt_count(self):
        state = {"issues": {"a": {"status": "완료", "attempts": 3,
                                  "fixed_at": "2026-07-01T00:00:00"}}}
        findings = {"a": {"priority": "P2", "type": "접근성", "title": "재발"}}
        self.eng.select_issue(findings, state, "2026-07-11T12:00:00")
        self.assertEqual(state["issues"]["a"]["attempts"], 0,
                         "재발은 새 라운드이므로 시도 횟수가 리셋돼야 한다")

    def test_freshly_completed_issue_is_not_retried(self):
        # fixed_at이 qa_last_run보다 최신 = 아직 재발이 아니다(정상 완료 유지).
        state = {"issues": {"b": {"status": "완료", "attempts": 1,
                                  "fixed_at": "2026-07-11T23:00:00"}}}
        findings = {"b": {"priority": "P2", "type": "접근성", "title": "정상완료"}}
        result = self.eng.select_issue(findings, state, "2026-07-11T12:00:00")
        self.assertIsNone(result)

    def test_held_issue_is_never_picked(self):
        state = {"issues": {"c": {"status": "보류", "attempts": 3}}}
        findings = {"c": {"priority": "P1", "type": "기능", "title": "보류중"}}
        self.assertIsNone(self.eng.select_issue(findings, state, "2026-07-11T12:00:00"))

    def test_fresh_issue_still_respects_max_attempts(self):
        # 완료 이력이 없는(재발 판정 대상이 아닌) 이슈는 여전히 시도 상한을 지켜야 한다.
        state = {"issues": {"d": {"status": "대기", "attempts": 3}}}
        findings = {"d": {"priority": "P1", "type": "기능", "title": "3회실패중"}}
        self.assertIsNone(self.eng.select_issue(findings, state, "2026-07-11T12:00:00"))


if __name__ == "__main__":
    unittest.main()
