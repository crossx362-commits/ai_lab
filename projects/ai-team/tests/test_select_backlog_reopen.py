"""수리 select_backlog 회귀 — 보류→대기 승격이 조용히 무시되던 것(2026-08-03).

증상: 수리 로그에 "처리 가능한 이슈/과제 없음 — 대기"만 반복되는데 backlog.json에는
'대기' 항목이 멀쩡히 있다. 원인은 dev_state에 남은 낡은 상한이었다 —

  backlog.json:  status=대기      (사람 또는 promote_approved_holds가 되돌림)
  dev_state:     status=보류, attempts=3(MAX)   ← 아무도 안 건드림

select_backlog의 필터가 `attempts < MAX_ATTEMPTS`만 보므로 이 항목은 영원히 탈락하고,
로그는 '과제 없음'이라 원인이 드러나지 않는다. 실제로 미오 과제 2건이 이 상태였고,
그 3회 실패조차 2026-07-28 구독 클로드 토큰 만료로 함대가 죽어 있던 구간에 소진된
것이라 과제 탓도 아니었다.

select_issue()에는 이미 같은 원칙(재발 감지 시 attempts 리셋)이 있었는데 백로그
경로에만 없었다 — 이 저장소에서 반복되는 '한쪽만 고친 비대칭'.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

ENGINE_PATH = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def _load_engine():
    sys.path.insert(0, str(ENGINE_PATH.parent))
    spec = importlib.util.spec_from_file_location("petnna_dev_engine_t", ENGINE_PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class SelectBacklogReopenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.eng = _load_engine()

    def _run(self, backlog_status, dev_status, attempts):
        item = {"id": "미오_test_1", "title": "카드 헤더 타이포 정리",
                "detail": "d", "priority": "P3", "type": "디자인",
                "status": backlog_status, "source": "미오", "created": "2026-07-26"}
        state = {"issues": {}}
        if dev_status is not None:
            state["issues"]["미오_test_1"] = {
                "attempts": attempts, "status": dev_status, "title": item["title"]}
        with mock.patch.object(self.eng, "_load_backlog", return_value={"items": [item]}):
            picked = self.eng.select_backlog(state)
        return picked, state

    def test_stale_hold_is_reopened(self):
        """이번 사고의 본체 — 백로그는 대기인데 dev_state가 보류+상한이면 재개해야 한다."""
        picked, state = self._run("대기", "보류", self.eng.MAX_ATTEMPTS)
        self.assertIsNotNone(
            picked,
            "백로그가 '대기'인데 dev_state의 낡은 보류 때문에 과제를 못 집었다 — "
            "수리가 '처리 가능한 과제 없음'만 반복하게 된다")
        rec = state["issues"]["미오_test_1"]
        self.assertEqual(rec["attempts"], 0, "재개 시 attempts를 리셋해야 한다")
        self.assertEqual(rec["status"], "대기")

    def test_normal_waiting_item_still_picked(self):
        """정상 경로 회귀 — dev_state 기록이 아예 없는 신규 과제."""
        picked, _ = self._run("대기", None, 0)
        self.assertIsNotNone(picked, "신규 대기 과제를 못 집었다")

    def test_backlog_hold_is_not_reopened(self):
        """백로그 자체가 보류면 건드리지 않는다 — 재개는 '대기'로 되돌린 것에만 적용."""
        picked, state = self._run("보류", "보류", self.eng.MAX_ATTEMPTS)
        self.assertIsNone(picked, "백로그가 보류인데 집었다")
        self.assertEqual(state["issues"]["미오_test_1"]["attempts"], self.eng.MAX_ATTEMPTS,
                         "백로그 보류 항목의 attempts를 건드리면 안 된다")

    def test_in_progress_attempts_not_reset(self):
        """아직 상한에 안 닿은 진행 중 과제의 시도 횟수를 리셋하면 안 된다."""
        picked, state = self._run("대기", "대기", 1)
        self.assertIsNotNone(picked)
        self.assertEqual(state["issues"]["미오_test_1"]["attempts"], 1,
                         "진행 중 과제의 attempts가 초기화됐다 — 무한 재시도 위험")


if __name__ == "__main__":
    unittest.main()
