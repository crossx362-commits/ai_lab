import os
import subprocess
import tempfile
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]

import importlib.util
RSPEC = importlib.util.spec_from_file_location("autodev_v2_runner", ROOT / "runner.py")
R = importlib.util.module_from_spec(RSPEC)
RSPEC.loader.exec_module(R)


class AutonomousLoopTests(unittest.TestCase):
    def base_cfg(self):
        return {
            "max_tasks_per_run": 2,
            "max_cloud_calls_per_run": 10,
            "max_cloud_calls_per_hour": 12,
            "max_tasks_per_director_batch": 6,
            "duplicate_task_similarity": 0.88,
            "dedupe_history_limit": 40,
            "max_same_area_per_director_batch": 2,
            "blocked_replan_cooldown_seconds": 1800,
            "supervisor_enabled": False,
            "stop_on_blocked": False,
        }

    def state_with_blocked_and_two_ready(self):
        return {
            "goal": "test",
            "tasks": [
                {"id": "T2", "title": "UI 체력바 구현", "goal": "체력바 표시", "area": "ui", "status": "pending", "priority": 90, "depends_on": [], "created_at": "1"},
                {"id": "T3", "title": "아이템 드롭 구현", "goal": "적 처치 시 아이템 드롭", "area": "items", "status": "pending", "priority": 80, "depends_on": [], "created_at": "2"},
            ],
            "completed": [],
            "blocked": [
                {"id": "T1", "title": "기존 막힌 전투 작업", "goal": "전투", "area": "combat", "status": "blocked", "last_error": "boom"},
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
            done = dict(task)
            done["status"] = "done"
            st_arg["completed"].append(done)
            return "done"

        with mock.patch.object(R.AUTODEV, "load_state", return_value=st), \
             mock.patch.object(R.AUTODEV, "save_state"), \
             mock.patch.object(R.AUTODEV, "print_status"), \
             mock.patch.object(R, "safe_execute_one", side_effect=fake_execute):
            rc = R.run_loop(cfg, continuous=True)

        self.assertEqual(rc, 0)
        self.assertEqual(executed, ["T2", "T3"])
        self.assertEqual([x["id"] for x in st["blocked"]], ["T1"])

    def test_dependency_on_blocked_task_waits_instead_of_becoming_blocked(self):
        st = {
            "tasks": [
                {"id": "T2", "title": "후속", "goal": "후속", "status": "pending", "depends_on": ["T1"]},
                {"id": "T3", "title": "독립", "goal": "독립", "status": "pending", "depends_on": []},
            ],
            "completed": [],
            "blocked": [{"id": "T1", "title": "선행", "goal": "선행", "status": "blocked"}],
            "stats": {"tasks_blocked": 0},
        }
        R.refresh_dependency_states(st)
        t2 = next(x for x in st["tasks"] if x["id"] == "T2")
        self.assertEqual(t2["status"], "waiting_dependency")
        self.assertIn("T1", t2["wait_reason"])
        self.assertEqual([x["id"] for x in st["blocked"]], ["T1"])
        self.assertEqual(R.next_ready(st)["id"], "T3")

    def test_duplicate_director_tasks_are_rejected_locally(self):
        cfg = self.base_cfg()
        st = {
            "tasks": [],
            "completed": [{"id": "T1", "title": "플레이어 체력바 UI 구현", "goal": "전투 HUD에 플레이어 체력바를 표시", "area": "ui"}],
            "blocked": [],
        }
        raw = [
            {"title": "플레이어 체력바 UI 구현", "goal": "전투 HUD에 플레이어 체력바를 표시", "area": "ui", "done_when": ["보인다"]},
            {"title": "몬스터 드롭 아이템 생성", "goal": "적 처치 시 아이템을 생성한다", "area": "items", "done_when": ["드롭된다"]},
        ]
        guarded = R.guard_director_raw(cfg, st, raw)
        self.assertEqual(len(guarded), 1)
        self.assertEqual(guarded[0]["area"], "items")

    def test_same_area_batch_is_capped(self):
        cfg = self.base_cfg()
        st = {"tasks": [], "completed": [], "blocked": []}
        raw = [
            {"title": "전투 공격 A", "goal": "공격 A 구현", "area": "combat", "done_when": ["A"]},
            {"title": "전투 공격 B", "goal": "공격 B 구현", "area": "combat", "done_when": ["B"]},
            {"title": "전투 공격 C", "goal": "공격 C 구현", "area": "combat", "done_when": ["C"]},
            {"title": "인벤토리 화면", "goal": "인벤토리 UI 구현", "area": "ui", "done_when": ["UI"]},
        ]
        guarded = R.guard_director_raw(cfg, st, raw)
        self.assertLessEqual(sum(1 for x in guarded if x["area"] == "combat"), 2)
        self.assertTrue(any(x["area"] == "ui" for x in guarded))

    def test_recent_same_area_yields_to_other_ready_area(self):
        st = {
            "completed": [
                {"id": "D1", "title": "전투1", "goal": "공격", "area": "combat"},
                {"id": "D2", "title": "전투2", "goal": "보스", "area": "combat"},
            ],
            "blocked": [],
            "tasks": [
                {"id": "T1", "title": "전투3", "goal": "스킬", "area": "combat", "status": "pending", "priority": 100, "depends_on": [], "created_at": "1"},
                {"id": "T2", "title": "UI", "goal": "메뉴", "area": "ui", "status": "pending", "priority": 10, "depends_on": [], "created_at": "2"},
            ],
        }
        self.assertEqual(R.next_ready(st)["id"], "T2")

    def test_quota_wait_does_not_consume_attempt_or_cloud_budget(self):
        cfg = {
            **self.base_cfg(),
            "_repo_root": "/tmp/fake-repo",
            "project_root": "/tmp/fake-repo/projects/ashes-to-stars/unity",
            "state_file": "/tmp/fake-state.json",
            "max_grok_attempts_per_task": 2,
            "max_codex_attempts_per_task": 1,
            "grok_worker_max_turns": 6,
        }
        task = {
            "id": "T1", "title": "전투 작업", "goal": "공격 구현", "area": "combat",
            "done_when": ["공격"], "status": "pending", "attempts_grok": 0,
            "attempts_codex": 0, "last_error": "", "last_verify_fingerprint": "",
        }
        st = {"tasks": [task], "completed": [], "blocked": [], "stats": {"grok_calls": 0, "codex_calls": 0}}
        run_stats = {"cloud_calls": 0, "tasks": 0}
        dummy_cp = {"dirty": set(), "untracked": set(), "staged": set(), "snapshots": {}}

        with mock.patch.object(R, "checkpoint", return_value=dummy_cp), \
             mock.patch.object(R, "rollback_checkpoint", return_value=[]), \
             mock.patch.object(R, "cloud_slot_available", return_value=True), \
             mock.patch.object(R.AUTODEV, "save_state"), \
             mock.patch.object(R.AUTODEV, "grok_call", return_value=(88, "usage limit")), \
             mock.patch.object(R.AUTODEV, "codex_call", return_value=(88, "weekly limit")):
            outcome = R.safe_execute_one(cfg, st, task, run_stats)

        self.assertEqual(outcome, "waiting_provider")
        self.assertEqual(task["attempts_grok"], 0)
        self.assertEqual(task["attempts_codex"], 0)
        self.assertEqual(run_stats["cloud_calls"], 0)
        self.assertEqual(task["status"], "pending")

    def test_failed_task_rollback_preserves_preexisting_dirty_files(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True)
            subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
            subprocess.run(["git", "config", "user.name", "AutoDev Test"], cwd=root, check=True)
            (root / "clean.txt").write_text("clean-base", encoding="utf-8")
            (root / "dirty.txt").write_text("dirty-base", encoding="utf-8")
            subprocess.run(["git", "add", "clean.txt", "dirty.txt"], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "base"], cwd=root, check=True, capture_output=True)

            (root / "dirty.txt").write_text("user-change", encoding="utf-8")
            (root / "keep.txt").write_text("user-untracked", encoding="utf-8")
            cp = R.checkpoint(root)

            (root / "clean.txt").write_text("task-broke-clean", encoding="utf-8")
            (root / "dirty.txt").write_text("task-overwrote-user", encoding="utf-8")
            (root / "keep.txt").write_text("task-overwrote-untracked", encoding="utf-8")
            (root / "new-by-task.txt").write_text("new", encoding="utf-8")

            restored = R.rollback_checkpoint(root, cp)
            self.assertIn("clean.txt", restored)
            self.assertEqual((root / "clean.txt").read_text(encoding="utf-8"), "clean-base")
            self.assertEqual((root / "dirty.txt").read_text(encoding="utf-8"), "user-change")
            self.assertEqual((root / "keep.txt").read_text(encoding="utf-8"), "user-untracked")
            self.assertFalse((root / "new-by-task.txt").exists())

    def test_ollama_is_not_used_as_director(self):
        source = (ROOT / "runner.py").read_text(encoding="utf-8")
        self.assertNotIn("LLM.text", source)
        self.assertNotIn("AI_TEAM_LLM_PRIMARY", source)
        self.assertIn("DIRECTOR:GROK", source)


if __name__ == "__main__":
    unittest.main()
