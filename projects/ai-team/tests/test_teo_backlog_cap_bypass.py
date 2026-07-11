"""테오 generate_test() MAX_TESTS 상한 우회 회귀 테스트 — 2026-07-11.

발견 경위: 테스트 스위트가 8개(TEO_MAX 기본값)에 도달한 뒤로, 백로그에 배정된
테스트 과제 2건이 generate_test() 진입 즉시(`len(existing) >= MAX_TESTS`) 반환돼
영원히 생성되지 않았다 — 백로그엔 '대기'로 남아 있는데 아무도 못 집는 조용한
방치 상태. MAX_TESTS는 '자유탐색 커버리지 확장'의 무한증식 방지용이지 명시
배정 과제까지 막아서는 안 된다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "테오_테스트" / "tools" / "petnna_test_engineer.py"


def load():
    spec = importlib.util.spec_from_file_location("teo_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class GenerateTestCapBypassTests(unittest.TestCase):
    def setUp(self):
        self.teo = load()

    def test_assigned_task_bypasses_cap(self):
        """상한 도달 상태에서도 배정 과제가 있으면 run_claude까지 진행해야 한다."""
        fake_existing = [Path(f"test_fake_{i}.py") for i in range(self.teo.MAX_TESTS)]  # 상한과 같은 개수
        task = {"id": "x", "title": "배정 과제", "detail": "..."}
        with mock.patch.object(self.teo, "list_tests", return_value=fake_existing), \
             mock.patch.object(self.teo, "_backlog_task", return_value=task), \
             mock.patch.object(self.teo, "run_claude", return_value=(False, "")) as rc:
            self.teo.generate_test(do_send=False)
            rc.assert_called_once()

    def test_no_task_still_respects_cap(self):
        """배정 과제가 없으면 기존처럼 상한에서 즉시 반환(run_claude 미호출)."""
        fake_existing = [Path(f"test_fake_{i}.py") for i in range(self.teo.MAX_TESTS)]
        with mock.patch.object(self.teo, "list_tests", return_value=fake_existing), \
             mock.patch.object(self.teo, "_backlog_task", return_value=None), \
             mock.patch.object(self.teo, "run_claude") as rc:
            result = self.teo.generate_test(do_send=False)
            self.assertFalse(result)
            rc.assert_not_called()

    def test_under_cap_without_task_still_generates(self):
        """상한 밑이면 배정 과제 없이도 기존처럼 자유탐색 생성을 시도한다."""
        fake_existing = [Path(f"test_fake_{i}.py") for i in range(self.teo.MAX_TESTS - 1)]
        with mock.patch.object(self.teo, "list_tests", return_value=fake_existing), \
             mock.patch.object(self.teo, "_backlog_task", return_value=None), \
             mock.patch.object(self.teo, "run_claude", return_value=(False, "")) as rc:
            self.teo.generate_test(do_send=False)
            rc.assert_called_once()


if __name__ == "__main__":
    unittest.main()
