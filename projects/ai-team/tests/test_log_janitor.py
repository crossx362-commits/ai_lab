"""log_janitor QA 사각지대 회귀 테스트 — 2026-07-11.

output/qa/petnna/(스크린샷·리포트·loop 로그)가 log_janitor 관리 밖이라 무한
증식하던 것을 회귀 테스트로 굳힌다: 오래된 산출물은 지워지되, backlog.json 같은
지속 상태 파일과 최신 산출물은 절대 지워지면 안 된다.
"""
import importlib.util
import os
import sys
import time
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "log_janitor.py"


def load():
    spec = importlib.util.spec_from_file_location("log_janitor_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class QaPetnnaSweepTests(unittest.TestCase):
    def setUp(self):
        self.jan = load()

    def _touch(self, path: Path, age_days: float):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("x", encoding="utf-8")
        old = time.time() - age_days * 86400
        os.utime(path, (old, old))

    def test_ephemeral_name_matching(self):
        self.assertTrue(self.jan._is_qa_ephemeral("/x/output/qa/petnna", "report_20260101_0000.md"))
        self.assertTrue(self.jan._is_qa_ephemeral("/x/output/qa/petnna/dev", "loop_20260101_0000.md"))
        self.assertTrue(self.jan._is_qa_ephemeral("/x/output/qa/petnna/council", "minutes_20260101_0000.md"))
        self.assertTrue(self.jan._is_qa_ephemeral("/x/output/qa/petnna/design/shots", "anything.png"))

    def test_state_files_never_match(self):
        for name in ("backlog.json", "dev_state.json", "qa_state.json", "state.json", "results.json"):
            self.assertFalse(self.jan._is_qa_ephemeral("/x/output/qa/petnna", name),
                             f"{name}는 지속 상태 파일이라 삭제 대상이면 안 된다")

    def test_sweep_deletes_stale_but_keeps_fresh_and_state(self):
        import tempfile
        tmp = Path(tempfile.mkdtemp())
        self.jan.QA_SWEEP_DIR = str(tmp)
        stale_report = tmp / "report_20260101_0000.md"
        fresh_report = tmp / "report_20260710_0000.md"
        state_file = tmp / "backlog.json"
        stale_shot = tmp / "design" / "shots" / "old.png"
        fresh_shot = tmp / "design" / "shots" / "new.png"

        self._touch(stale_report, age_days=100)
        self._touch(fresh_report, age_days=1)
        self._touch(state_file, age_days=100)
        self._touch(stale_shot, age_days=100)
        self._touch(fresh_shot, age_days=1)

        acts = self.jan._sweep_qa_petnna(time.time())

        self.assertFalse(stale_report.exists(), "오래된 리포트는 지워져야 한다")
        self.assertFalse(stale_shot.exists(), "오래된 스크린샷은 지워져야 한다")
        self.assertTrue(fresh_report.exists(), "최신 리포트는 보존돼야 한다")
        self.assertTrue(fresh_shot.exists(), "최신 스크린샷은 보존돼야 한다")
        self.assertTrue(state_file.exists(), "지속 상태 파일은 나이와 무관하게 보존돼야 한다")
        self.assertEqual(len(acts), 2)

    def test_screenshots_expire_sooner_than_reports(self):
        """스크린샷은 QA_SHOTS_STALE_DAYS, 리포트는 STALE_DAYS — 2026-08-04."""
        self.assertLess(self.jan.QA_SHOTS_STALE_DAYS, self.jan.STALE_DAYS)
        self.assertEqual(self.jan._qa_stale_days("/x/qa/petnna/dev/qa_base/shots", "a.png"),
                         self.jan.QA_SHOTS_STALE_DAYS)
        self.assertEqual(self.jan._qa_stale_days("/x/qa/petnna/design", "review_20260101.png"),
                         self.jan.QA_SHOTS_STALE_DAYS)
        self.assertEqual(self.jan._qa_stale_days("/x/qa/petnna", "report_20260101_0000.md"),
                         self.jan.STALE_DAYS)

    def test_sweep_deletes_midaged_shot_but_keeps_midaged_report(self):
        """스크린샷만 지워지고 같은 나이의 리포트는 남는 구간이 실제로 존재한다."""
        import tempfile
        tmp = Path(tempfile.mkdtemp())
        self.jan.QA_SWEEP_DIR = str(tmp)
        mid = (self.jan.QA_SHOTS_STALE_DAYS + self.jan.STALE_DAYS) / 2
        shot = tmp / "shots" / "mid.png"
        report = tmp / "report_mid.md"
        self._touch(shot, age_days=mid)
        self._touch(report, age_days=mid)

        self.jan._sweep_qa_petnna(time.time())

        self.assertFalse(shot.exists(), f"{mid}일 지난 스크린샷은 지워져야 한다")
        self.assertTrue(report.exists(), f"{mid}일 지난 리포트는 아직 보존돼야 한다")


if __name__ == "__main__":
    unittest.main()
