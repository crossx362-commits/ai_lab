import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import functional_verify as F  # noqa: E402
import runner_entry as E  # noqa: E402


class FunctionalVerifyTests(unittest.TestCase):
    def make_cfg(self, root: Path):
        project = root / "projects" / "ashes-to-stars" / "unity"
        runner_path = project / F.RUNNER_REL
        runner_path.parent.mkdir(parents=True, exist_ok=True)
        runner_path.write_text("public static class AutoDevAcceptanceRunner {}", encoding="utf-8")
        return {
            "_repo_root": str(root),
            "project_root": str(project),
            "functional_verify_enabled": True,
            "functional_verify_areas": ["combat", "character", "systems"],
            "functional_verify_min_assertions": 1,
            "functional_verify_wait_seconds": 120,
            "functional_verify_timeout_seconds": 900,
        }

    def task(self):
        return {
            "id": "T0042",
            "title": "적 피격 시 체력 감소",
            "goal": "실제 적 컴포넌트가 피해를 받으면 체력이 감소한다",
            "area": "combat",
            "done_when": ["피해 10을 받으면 체력이 10 감소한다"],
            "status": "pending",
            "attempts_grok": 0,
            "attempts_codex": 0,
            "depends_on": [],
            "priority": 50,
            "created_at": "1",
        }

    def write_acceptance(self, cfg, task, body):
        path = Path(cfg["project_root"]) / F.acceptance_rel(task)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(body, encoding="utf-8")
        return path

    def good_source(self):
        return """
// AUTODEV_TASK:T0042
public static class AutoDev_T0042_Acceptance
{
    public static void Run()
    {
        var hpBefore = 100;
        var hpAfter = hpBefore - 10;
        AutoDevAssert.Equal(90, hpAfter, "damage must reduce hp");
    }
}
"""

    def test_gameplay_area_requires_functional_verification(self):
        with tempfile.TemporaryDirectory() as td:
            cfg = self.make_cfg(Path(td))
            self.assertTrue(F.requires_functional(cfg, self.task()))
            q = self.task(); q["area"] = "qa"
            self.assertFalse(F.requires_functional(cfg, q))

    def test_missing_acceptance_file_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            cfg = self.make_cfg(Path(td))
            problem = F.source_problem(cfg, self.task(), {"projects/ashes-to-stars/unity/Assets/Game.cs"})
            self.assertIn("검증 파일이 없습니다", problem)

    def test_fake_constant_assert_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            cfg = self.make_cfg(Path(td)); task = self.task()
            self.write_acceptance(cfg, task, """
// AUTODEV_TASK:T0042
public static class AutoDev_T0042_Acceptance { public static void Run() { AutoDevAssert.True(true); } }
""")
            problem = F.source_problem(cfg, task, {"projects/ashes-to-stars/unity/Assets/Game.cs"})
            self.assertIn("가짜", problem)

    def test_test_only_change_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td); cfg = self.make_cfg(root); task = self.task()
            path = self.write_acceptance(cfg, task, self.good_source())
            rel = path.resolve().relative_to(root).as_posix()
            problem = F.source_problem(cfg, task, {rel})
            self.assertIn("실제 게임 코드는 바뀌지 않았습니다", problem)

    def test_real_code_plus_acceptance_is_allowed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td); cfg = self.make_cfg(root); task = self.task()
            path = self.write_acceptance(cfg, task, self.good_source())
            rel = path.resolve().relative_to(root).as_posix()
            problem = F.source_problem(
                cfg,
                task,
                {rel, "projects/ashes-to-stars/unity/Assets/Scripts/EnemyHealth.cs"},
            )
            self.assertEqual("", problem)

    def test_environment_lock_waits_without_calling_ai(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td); cfg = self.make_cfg(root); task = self.task()
            st = {"tasks": [task], "completed": [], "blocked": [], "stats": {"grok_calls": 0, "codex_calls": 0}}
            run_stats = {"cloud_calls": 0, "tasks": 0}
            with mock.patch.object(E.FV, "environment_ready", return_value=(False, "Unity editor open")), \
                 mock.patch.object(E, "ORIGINAL_SAFE_EXECUTE") as execute, \
                 mock.patch.object(E.AUTODEV, "save_state"):
                outcome = E.safe_execute_one(cfg, st, task, run_stats)
            self.assertEqual("waiting_verification", outcome)
            self.assertEqual("waiting_verification", task["status"])
            self.assertGreater(task["verification_retry_at"], time.time())
            self.assertEqual(0, run_stats["cloud_calls"])
            execute.assert_not_called()

    def test_waiting_task_becomes_ready_after_retry_time(self):
        task = self.task()
        task["status"] = "waiting_verification"
        task["verification_retry_at"] = time.time() - 1
        st = {"tasks": [task], "completed": [], "blocked": []}
        ready = E.next_ready(st)
        self.assertIs(ready, task)
        self.assertEqual("pending", task["status"])

    def test_director_does_not_make_more_work_while_only_verification_waits(self):
        task = self.task(); task["status"] = "waiting_verification"
        st = {"tasks": [task], "completed": [], "blocked": []}
        with mock.patch.object(E, "ORIGINAL_DIRECTOR_FILL") as director:
            ok = E.director_fill({}, st)
        self.assertFalse(ok)
        director.assert_not_called()


if __name__ == "__main__":
    unittest.main()
