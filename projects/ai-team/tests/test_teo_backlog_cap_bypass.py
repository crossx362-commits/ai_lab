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


class GenerateTestInfraFailureTests(unittest.TestCase):
    """2026-07-11 2차 감사에서 발견: 인프라 실패(CLI 부재·타임아웃 등)를 진짜 실패처럼
    attempts에 넣으면, claude CLI가 잠깐 안 보이기만 해도 멀쩡한 배정 과제가 몇 번 안에
    '보류'로 오탈락한다 — 수리 _improve_cycle과 같은 원칙으로 인프라 실패는 미차감."""

    def setUp(self):
        self.teo = load()

    def test_infra_failure_does_not_count_against_attempts(self):
        task = {"id": "x", "title": "배정 과제", "detail": "..."}
        with mock.patch.object(self.teo, "list_tests", return_value=[]), \
             mock.patch.object(self.teo, "_backlog_task", return_value=task), \
             mock.patch.object(self.teo, "run_claude",
                              return_value=(False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)")), \
             mock.patch.object(self.teo, "_backlog_task_failed") as failed:
            result = self.teo.generate_test(do_send=False)
            self.assertFalse(result)
            failed.assert_not_called()

    def test_real_content_failure_still_counts_against_attempts(self):
        """인프라 사유가 아닌 진짜 생성 실패(빈 응답 등)는 여전히 attempts에 반영돼야 한다."""
        task = {"id": "x", "title": "배정 과제", "detail": "..."}
        with mock.patch.object(self.teo, "list_tests", return_value=[]), \
             mock.patch.object(self.teo, "_backlog_task", return_value=task), \
             mock.patch.object(self.teo, "run_claude", return_value=(True, "이해가 안 갑니다")), \
             mock.patch.object(self.teo, "_backlog_task_failed") as failed:
            self.teo.generate_test(do_send=False)
            failed.assert_called_once_with("x")


class GenerateTestCommitFailureTests(unittest.TestCase):
    """2026-07-11 2차 감사에서 발견: git commit 실패를 확인 안 하면 과제가 '완료'로
    닫히고 테스트 파일은 스테이징된 채 미커밋으로 남는다."""

    def setUp(self):
        self.teo = load()
        import tempfile
        self.tmpdir = Path(tempfile.mkdtemp())
        self.new_path = self.tmpdir / "test_fake_new.py"
        self.new_path.write_text("NAME = 'x'\ndef run(page, base_url): pass\n", encoding="utf-8")

    def test_commit_failure_does_not_close_task_and_cleans_up_file(self):
        task = {"id": "x", "title": "배정 과제", "detail": "..."}
        existing = []
        # list_tests()는 세 번 호출된다: existing 계산 → covered_flows() 내부 → new 계산
        with mock.patch.object(self.teo, "list_tests",
                               side_effect=[existing, existing, [self.new_path]]), \
             mock.patch.object(self.teo, "_backlog_task", return_value=task), \
             mock.patch.object(self.teo, "run_claude", return_value=(True, "생성 완료")), \
             mock.patch.object(self.teo, "run_suite", return_value={"x": {"ok": True}}), \
             mock.patch.object(self.teo, "_backlog_done") as done, \
             mock.patch.object(self.teo, "_backlog_task_failed") as failed, \
             mock.patch.object(self.teo.subprocess, "run") as fake_run:
            # git add → 성공(returncode 0 기본), git commit → 실패(returncode 1)
            fake_run.side_effect = [
                mock.Mock(returncode=0),  # git add
                mock.Mock(returncode=1, stderr="pre-commit hook failed"),  # git commit
                mock.Mock(returncode=0),  # git reset
            ]
            result = self.teo.generate_test(do_send=False)
            self.assertFalse(result)
            done.assert_not_called()
            self.assertFalse(self.new_path.exists(), "커밋 실패한 파일은 폐기(unlink)돼야 한다")
            # 2차 감사 발견(2026-07-11): flaky 폐기 경로와 대칭 — 커밋 실패도 attempts에
            # 반영돼야 pre-commit 훅이 계속 거부하는 구조적 문제에서 상한·보류 전환이 작동한다.
            failed.assert_called_once_with("x")


if __name__ == "__main__":
    unittest.main()
