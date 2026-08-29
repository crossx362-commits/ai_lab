import importlib.util
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]

RSPEC = importlib.util.spec_from_file_location("autodev_v2_runner", ROOT / "runner.py")
R = importlib.util.module_from_spec(RSPEC)
RSPEC.loader.exec_module(R)


class AutonomousLoopTests(unittest.TestCase):
    def base_cfg(self):
        return {
            "max_tasks_per_run": 2,
            "max_cloud_calls_per_run": 10,
            "max_tasks_per_director_batch": 6,
            "stop_on_blocked": False,
        }

    def state_with_blocked_and_two_ready(self):
        return {
            "goal": "test",
            "tasks": [
                {"id": "T2", "title": "독립 작업 1", "status": "pending", "priority": 90, "depends_on": [], "created_at": "1"},
                {"id": "T3", "title": "독립 작업 2", "status": "pending", "priority": 80, "depends_on": [], "created_at": "2"},
            ],
            "completed": [],
            "blocked": [
                {"id": "T1", "title": "기존 막힌 작업", "status": "blocked", "last_error": "boom"},
            ],
            "stats": {"grok_calls": 0, "codex_calls": 0},
        }

    def test_blocked_history_does_not_stop_independent_tasks(self):
        cfg = self.base_cfg()
        st = self.state_with_blocked_and_two_ready()
        executed = []

        def fake_execute(cfg_arg, st_arg, task, run_stats):
            executed.append(task["id"])
            st_arg["tasks"] = [x for x in st_arg["tasks"] if x["id"] != task["id"]]
            st_arg["completed"].append(dict(task))
            return True

        with mock.patch.object(R.AUTODEV, "load_state", return_value=st), \
             mock.patch.object(R.AUTODEV, "save_state"), \
             mock.patch.object(R.AUTODEV, "print_status"), \
             mock.patch.object(R.AUTODEV, "execute_one", side_effect=fake_execute):
            rc = R.run_loop(cfg, continuous=True)

        self.assertEqual(rc, 0)
        self.assertEqual(executed, ["T2", "T3"])
        self.assertEqual([x["id"] for x in st["blocked"]], ["T1"])

    def test_dependency_on_blocked_task_is_cascaded_to_blocked(self):
        st = {
            "tasks": [
                {"id": "T2", "title": "후속", "status": "pending", "depends_on": ["T1"]},
                {"id": "T3", "title": "독립", "status": "pending", "depends_on": []},
            ],
            "completed": [],
            "blocked": [{"id": "T1", "title": "선행", "status": "blocked"}],
            "stats": {"tasks_blocked": 0},
        }
        count = R._cascade_blocked_dependencies(st)
        self.assertEqual(count, 1)
        self.assertEqual([x["id"] for x in st["tasks"]], ["T3"])
        self.assertEqual([x["id"] for x in st["blocked"]], ["T1", "T2"])
        self.assertIn("T1", st["blocked"][-1]["last_error"])

    def test_local_director_costs_zero_cloud_budget(self):
        cfg = self.base_cfg()
        cfg["max_tasks_per_run"] = 1
        st = {
            "goal": "",
            "tasks": [],
            "completed": [],
            "blocked": [],
            "stats": {"grok_calls": 0, "codex_calls": 0},
        }
        seen_cloud = []

        def fake_director(cfg_arg, st_arg):
            st_arg["last_director_provider"] = "ollama"
            st_arg["tasks"].append({
                "id": "T1", "title": "로컬 기획 작업", "status": "pending",
                "priority": 10, "depends_on": [], "created_at": "1",
            })
            return True

        def fake_execute(cfg_arg, st_arg, task, run_stats):
            seen_cloud.append(run_stats["cloud_calls"])
            st_arg["tasks"].clear()
            st_arg["completed"].append(dict(task))
            return True

        with mock.patch.object(R.AUTODEV, "load_state", return_value=st), \
             mock.patch.object(R.AUTODEV, "save_state"), \
             mock.patch.object(R.AUTODEV, "print_status"), \
             mock.patch.object(R, "director_fill", side_effect=fake_director), \
             mock.patch.object(R.AUTODEV, "execute_one", side_effect=fake_execute):
            rc = R.run_loop(cfg, continuous=True)

        self.assertEqual(rc, 0)
        self.assertEqual(seen_cloud, [0])


if __name__ == "__main__":
    unittest.main()
