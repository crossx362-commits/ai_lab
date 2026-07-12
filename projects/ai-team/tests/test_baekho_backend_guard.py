"""백호 investigate_assigned_tasks() 재시도 상한 회귀 테스트 — 2026-07-11 2차 감사.

발견 경위: 배정 조사 과제가 실패하면(인프라 사유가 아니어도) attempts를 전혀
추적하지 않고 무조건 '다음 주기 재시도'만 해서, 구조적으로 실패하는 과제가
300초(데몬 주기)마다 영원히 재조사됐다 — 상한·보류 전환·에스컬레이션이 전무.
테오(_backlog_task_failed)와 동일한 계열의 결함이라 공용 헬퍼(_shared/backlog.py)로
통일해 고쳤다.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "백호_백엔드" / "tools" / "petnna_backend_guard.py"


def load():
    spec = importlib.util.spec_from_file_location("baekho_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class InvestigateAssignedTasksFailureTests(unittest.TestCase):
    def setUp(self):
        self.baekho = load()
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
        return mock.patch.object(self.baekho, "BACKLOG", self.tmp)

    def test_infra_failure_does_not_count_against_attempts(self):
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude",
                              return_value=(False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)")):
            self.baekho.investigate_assigned_tasks(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "대기")
        self.assertNotIn("attempts", item, "인프라 실패는 시도 횟수에 반영되면 안 된다")

    def test_real_failure_accumulates_attempts_and_eventually_holds(self):
        """상한 없이 무한 재시도되던 결함 회귀 — 3회 진짜 실패 후 '보류'로 전환돼야 한다."""
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude", return_value=(True, "")):
            # ok=True인데 out이 빈 문자열 → `not (ok and out)` 참 → 실패 경로
            for _ in range(self.baekho.TASK_MAX_ATTEMPTS):
                self.baekho.investigate_assigned_tasks(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["attempts"], self.baekho.TASK_MAX_ATTEMPTS)
        self.assertEqual(item["status"], "보류", "상한 도달 시 무한 재조사 대신 보류로 전환해야 한다")

    def test_success_marks_done_and_stores_finding(self):
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude", return_value=(True, "결론: 문제 없음")):
            n = self.baekho.investigate_assigned_tasks(do_send=False)
        self.assertEqual(n, 1)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "완료")
        self.assertIn("문제 없음", item["finding"])

    def test_only_own_waiting_tasks_are_picked(self):
        with self._write([
            {"id": "a", "title": "백호 과제", "owner": "백호", "status": "대기"},
            {"id": "b", "title": "다른 과제", "owner": "수리", "status": "대기"},
            {"id": "c", "title": "완료된 과제", "owner": "백호", "status": "완료"},
        ]), mock.patch.object(self.baekho, "run_claude", return_value=(True, "결론")) as rc:
            n = self.baekho.investigate_assigned_tasks(do_send=False)
        self.assertEqual(n, 1)
        rc.assert_called_once()

    def test_concurrent_write_during_processing_is_not_clobbered(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-12): 예전엔 시작 시점 스냅샷을
        run_claude(최대 600s) 동안 들고 있다가 맨 마지막에 통째로 덮어써, 그 사이
        다른 에이전트가 backlog.json에 적재한 내용을 유실할 수 있었다. 이제는 과제
        처리 직후 파일을 다시 읽어 해당 항목만 갱신하므로, run_claude 호출 도중(실제로는
        그 반환 직후) 다른 프로세스가 추가한 항목이 살아남아야 한다."""
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]):
            def fake_run_claude(*args, **kwargs):
                # run_claude가 오래 걸리는 동안 다른 에이전트(예: 미오)가 새 항목을
                # 적재했다고 시뮬레이션 — investigate_assigned_tasks가 이 반환 직후
                # 파일을 다시 읽으므로 이 변경을 봐야 한다.
                data = json.loads(self.tmp.read_text(encoding="utf-8"))
                data["items"].append({"id": "concurrent", "title": "동시 적재된 항목",
                                      "owner": "미오", "status": "대기", "type": "디자인"})
                self.tmp.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
                return (True, "결론: 문제 없음")

            with mock.patch.object(self.baekho, "run_claude", side_effect=fake_run_claude):
                self.baekho.investigate_assigned_tasks(do_send=False)

        items = {i["id"]: i for i in json.loads(self.tmp.read_text(encoding="utf-8"))["items"]}
        self.assertEqual(items["a"]["status"], "완료")
        self.assertIn("concurrent", items, "처리 도중 다른 에이전트가 적재한 항목이 유실되면 안 된다")


if __name__ == "__main__":
    unittest.main()
