import importlib.util
import sys
import tempfile
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

SPEC = importlib.util.spec_from_file_location("autodev_v2_runner_entry_test", ROOT / "runner_entry.py")
E = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(E)


class RunnerEntryTests(unittest.TestCase):
    def tearDown(self):
        E._ACTIVE_CHECKPOINTS.clear()
        E._ACTIVE_DELTA_OVERRIDES.clear()
        E._LAST_CFG = None

    def base_cfg(self, root: str) -> dict:
        return {
            "_repo_root": root,
            "project_root": root,
            "functional_verify_wait_seconds": 30,
            "implement_while_unity_locked": True,
            "max_waiting_verification_tasks": 2,
            "max_candidate_files": 5,
        }

    def test_unity_lock_after_implementation_holds_changes_without_rollback(self):
        cfg = self.base_cfg("/tmp")
        task = {"id": "T1000", "title": "전투", "area": "combat", "status": "pending"}
        st = {"tasks": [task], "completed": [], "blocked": [], "stats": {}}
        cp = {"dirty": set(), "untracked": set(), "staged": set(), "snapshots": {}}
        with mock.patch.object(E.FV, "requires_functional", return_value=True), \
             mock.patch.object(E.FV, "environment_ready", return_value=(False, "Unity locked")), \
             mock.patch.object(E.runner, "checkpoint", return_value=cp), \
             mock.patch.object(E.runner, "task_delta_paths", return_value={"projects/game/Foo.cs"}), \
             mock.patch.object(E, "ORIGINAL_SAFE_EXECUTE", side_effect=E.FV.FunctionalVerificationWait("Unity locked")), \
             mock.patch.object(E, "load_hold_checkpoint", return_value=None), \
             mock.patch.object(E, "save_hold_checkpoint") as save_hold, \
             mock.patch.object(E.runner, "rollback_checkpoint") as rollback, \
             mock.patch.object(E.AUTODEV, "save_state"):
            outcome = E.safe_execute_one(cfg, st, task, {"cloud_calls": 0, "tasks": 0})
        self.assertEqual(outcome, "waiting_verification")
        self.assertTrue(task["verification_only"])
        self.assertEqual(task["implementation_delta_files"], ["projects/game/Foo.cs"])
        save_hold.assert_called_once_with("T1000", cp)
        rollback.assert_not_called()

    def test_verification_only_retry_does_not_call_worker(self):
        cfg = self.base_cfg("/tmp")
        task = {
            "id": "T1001", "title": "영지", "area": "estate", "status": "pending",
            "verification_only": True, "implementation_delta_files": ["game/EstateScreen.cs"],
        }
        st = {"tasks": [task], "completed": [], "blocked": [], "stats": {}}
        with mock.patch.object(E.FV, "requires_functional", return_value=True), \
             mock.patch.object(E.FV, "environment_ready", return_value=(True, "ready")), \
             mock.patch.object(E, "verify_task", return_value=("pass", "acceptance pass")), \
             mock.patch.object(E.AUTODEV, "finish_task") as finish, \
             mock.patch.object(E, "ORIGINAL_SAFE_EXECUTE") as worker:
            outcome = E.safe_execute_one(cfg, st, task, {"cloud_calls": 0, "tasks": 0})
        self.assertEqual(outcome, "done")
        finish.assert_called_once()
        worker.assert_not_called()

    def test_waiting_verification_cap_prevents_third_implementation(self):
        cfg = self.base_cfg("/tmp")
        task = {"id": "T1002", "title": "레이드", "area": "raid", "status": "pending"}
        st = {
            "tasks": [
                {"id": "T1", "status": "waiting_verification"},
                {"id": "T2", "status": "waiting_verification"},
                task,
            ],
            "completed": [], "blocked": [], "stats": {},
        }
        with mock.patch.object(E.FV, "requires_functional", return_value=True), \
             mock.patch.object(E.FV, "environment_ready", return_value=(False, "Unity locked")), \
             mock.patch.object(E, "ORIGINAL_SAFE_EXECUTE") as worker, \
             mock.patch.object(E.AUTODEV, "save_state"):
            outcome = E.safe_execute_one(cfg, st, task, {"cloud_calls": 0, "tasks": 0})
        self.assertEqual(outcome, "waiting_verification")
        self.assertEqual(task["status"], "waiting_verification_capacity")
        worker.assert_not_called()

    def test_project_anchor_is_preferred_over_generic_candidate(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            project = root / "unity"
            target = project / "Assets" / "Scripts" / "EstateScreenController.cs"
            target.parent.mkdir(parents=True)
            target.write_text("class EstateScreenController {}", encoding="utf-8")
            cfg = {"_repo_root": str(root), "project_root": str(project), "max_candidate_files": 5}
            task = {"id": "T1003", "title": "영지 화면 연결", "goal": "EstateScreen 갱신", "area": "estate"}
            with mock.patch.object(E, "ORIGINAL_CANDIDATE_FILES", return_value=["generic.cs"]):
                files = E.candidate_files(cfg, task)
        self.assertTrue(files[0].endswith("EstateScreenController.cs"))
        self.assertIn("generic.cs", files)

    def test_finish_task_records_only_task_delta(self):
        cfg = self.base_cfg("/tmp")
        task = {"id": "T1004", "title": "편성"}
        st = {"tasks": [task], "completed": [], "stats": {}}
        cp = {"dirty": {"owner.cs"}, "untracked": set(), "staged": set(), "snapshots": {}}
        E._ACTIVE_CHECKPOINTS["T1004"] = cp

        def fake_finish(_cfg, state, item, _out):
            state["completed"].append({**item, "changed_files": ["owner.cs", "game/W3Party.cs"]})
            state["tasks"] = []

        with mock.patch.object(E, "ORIGINAL_FINISH_TASK", side_effect=fake_finish), \
             mock.patch.object(E, "load_hold_checkpoint", return_value=None), \
             mock.patch.object(E, "clear_hold_checkpoint"), \
             mock.patch.object(E.runner, "task_delta_paths", return_value={"game/W3Party.cs"}), \
             mock.patch.object(E.AUTODEV, "save_state"):
            E.finish_task(cfg, st, task, "pass")
        self.assertEqual(st["completed"][-1]["changed_files"], ["game/W3Party.cs"])

    def test_hold_checkpoint_roundtrip_preserves_owner_snapshot(self):
        cp = {
            "dirty": {"owner.cs"},
            "untracked": {"note.txt"},
            "staged": {"owner.cs"},
            "snapshots": {
                "owner.cs": (True, b"owner-before"),
                "note.txt": (True, b""),
                "new.cs": (False, None),
            },
        }
        with tempfile.TemporaryDirectory() as td, mock.patch.object(E, "HOLD_DIR", Path(td)):
            E.save_hold_checkpoint("T1005", cp)
            loaded = E.load_hold_checkpoint("T1005")
            self.assertEqual(loaded, cp)
            self.assertTrue(E._hold_path("T1005").exists())
            E.clear_hold_checkpoint("T1005")
            self.assertFalse(E._hold_path("T1005").exists())

    def test_final_block_rolls_back_persisted_hold_checkpoint(self):
        cfg = self.base_cfg("/tmp")
        task = {"id": "T1006", "title": "영지 수정", "area": "estate", "status": "pending"}
        st = {"tasks": [task], "completed": [], "blocked": [], "stats": {}}
        current_cp = {"dirty": {"held.cs"}, "untracked": set(), "staged": set(), "snapshots": {}}
        held_cp = {
            "dirty": {"owner.cs"}, "untracked": set(), "staged": set(),
            "snapshots": {"owner.cs": (True, b"owner-before")},
        }

        def fake_block(_cfg, state, item, _run_stats):
            item["status"] = "blocked"
            state["blocked"].append(dict(item))
            state["tasks"] = []
            return "blocked"

        with tempfile.TemporaryDirectory() as td, \
             mock.patch.object(E, "HOLD_DIR", Path(td)), \
             mock.patch.object(E.FV, "requires_functional", return_value=True), \
             mock.patch.object(E.FV, "environment_ready", return_value=(True, "ready")), \
             mock.patch.object(E.runner, "checkpoint", return_value=current_cp), \
             mock.patch.object(E, "ORIGINAL_SAFE_EXECUTE", side_effect=fake_block), \
             mock.patch.object(E.runner, "rollback_checkpoint", return_value=["held.cs"]) as rollback, \
             mock.patch.object(E.AUTODEV, "save_state"):
            E.save_hold_checkpoint("T1006", held_cp)
            outcome = E.safe_execute_one(cfg, st, task, {"cloud_calls": 0, "tasks": 0})
            self.assertFalse(E._hold_path("T1006").exists())
        self.assertEqual(outcome, "blocked")
        rollback.assert_called_once_with(Path("/tmp"), held_cp)
        self.assertEqual(st["blocked"][-1]["hold_rollback_files"], ["held.cs"])


if __name__ == "__main__":
    unittest.main()
