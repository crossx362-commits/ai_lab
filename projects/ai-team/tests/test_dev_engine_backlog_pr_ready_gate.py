"""백로그(기획) 항목은 '지표 비악화' 게이트 없이 PR대기 직행 (회의_202608042225_1 구현).

배경: '시니어 케어' 단일 허브 탭 재조립(feat/petnna-회의_202607251120_4)이 target
해결·P0/P1/P2 무신규·E2E 29개 전부 통과였는데도, 신규 P3 1건(1→2)이 qa_not_worse를
'악화'로 판정해 PR대기에 못 이르고 3회 재시도 끝에 보류 + 긴급회의 소집까지 갔다
(output/qa/petnna/council/minutes_20260804_2225.md). 6인 중 5인이 "이건 실패가 아니라
게이트 오분류"라고 판단, 결정: 백로그 항목은 '지표 비악화'(qa_not_worse) 게이트를
PR대기 진입 조건에서 뺀다 — 어차피 자동 병합 대상이 아니라 항상 사람(예원 PR 리뷰어)이
diff_gate·E2E·LLM 품질판단으로 병합 직전에 독립적으로 재검증하므로 안전망은 유지된다.
gate_ok(금지경로·크기)·tests_ok(E2E 회귀)는 기술적 정합성 문제라 계속 게이트로 남긴다.

QA 이슈 자동수정(is_backlog=False)은 자동 병합 경로라 기존 엄격 규칙(not_worse 포함)을
그대로 유지한다 — 이번 완화는 백로그에만 적용된다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def _load():
    spec = importlib.util.spec_from_file_location("dev_engine_pr_ready_t", _ENGINE)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_MOD = _load()


class PrReadyGateTests(unittest.TestCase):
    """엔진의 실제 판정 함수(pr_ready)를 직접 호출한다 — 로직 복사본이 아니다."""

    def test_senior_care_hub_scenario_now_reaches_pr_ready(self):
        """실제 사고 재현: target 해결·P0~P2 무신규·E2E 그린인데 qa_not_worse만 False."""
        self.assertTrue(
            _MOD.pr_ready(True, gate_ok=True, resolved=True, not_worse=False, tests_ok=True),
            "P3만 늘어 qa_not_worse가 False여도 백로그는 PR대기에 도달해야 한다 "
            "— 회의_202608042225_1이 고치려던 바로 그 3회-보류 재발 시나리오")

    def test_backlog_still_blocked_by_gate_ok(self):
        """금지경로·크기 초과(diff_gate)는 여전히 기술적 하드 게이트다."""
        self.assertFalse(
            _MOD.pr_ready(True, gate_ok=False, resolved=True, not_worse=True, tests_ok=True),
            "diff_gate 거부인데도 PR대기로 보냈다 — 금지경로/과대 diff 안전망이 뚫렸다")

    def test_backlog_still_blocked_by_failing_e2e(self):
        """새로 깨진 E2E는 여전히 하드 게이트다 — '지표 비악화'와 별개 관심사."""
        self.assertFalse(
            _MOD.pr_ready(True, gate_ok=True, resolved=True, not_worse=True, tests_ok=False),
            "신규 E2E 실패인데도 PR대기로 보냈다 — 회귀 게이트가 뚫렸다")

    def test_qa_fix_path_unaffected_by_not_worse(self):
        """QA 이슈 자동수정(is_backlog=False)은 완화 대상이 아니다 — 기존 엄격 규칙 유지."""
        self.assertFalse(
            _MOD.pr_ready(False, gate_ok=True, resolved=True, not_worse=False, tests_ok=True),
            "자동 병합 경로의 '지표 비악화' 규칙까지 풀렸다 — 완화 범위가 넘쳤다")

    def test_qa_fix_path_pr_ready_when_all_pass(self):
        self.assertTrue(
            _MOD.pr_ready(False, gate_ok=True, resolved=True, not_worse=True, tests_ok=True))

    def test_qa_fix_path_blocked_when_unresolved(self):
        self.assertFalse(
            _MOD.pr_ready(False, gate_ok=True, resolved=False, not_worse=True, tests_ok=True))


class ReasonMessageOrderingTests(unittest.TestCase):
    """백로그는 safe_priority가 항상 False라, gate_ok 체크가 그 뒤에 있으면 diff_gate가
    실제로 거부했어도 로그엔 늘 '고위험 분류'만 찍혀 진짜 사유가 가려진다(회의_202608042225_1
    구현 중 발견). gate_ok 분기가 safe_priority 분기보다 먼저 와야 한다."""

    def test_gate_ok_reason_checked_before_safe_priority(self):
        src = _ENGINE.read_text(encoding="utf-8")
        idx_gate = src.index('"게이트 거부" if not gate_ok else')
        idx_safe = src.index('"고위험 분류" if not safe_priority else')
        self.assertLess(idx_gate, idx_safe,
                        "reason 삼항식에서 '고위험 분류'가 '게이트 거부'보다 먼저 평가된다 "
                        "— 백로그는 safe_priority가 항상 False라 실제 게이트 거부 사유가 "
                        "로그에서 영원히 가려진다")

    def test_pr_ready_used_in_status_assignment(self):
        """PR대기 판정이 인라인 불리언식으로 되돌아가면 회귀를 다시 만든다."""
        src = _ENGINE.read_text(encoding="utf-8")
        self.assertIn('pr_ready(is_backlog, gate_ok, resolved, not_worse, tests_ok)', src,
                      "PR대기 상태 배정이 공용 함수 pr_ready를 쓰지 않는다")


if __name__ == "__main__":
    unittest.main()
