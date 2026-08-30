import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("autodev_new_loop", ROOT / "loop.py")
L = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(L)


class LoopTests(unittest.TestCase):
    def test_extract_json(self):
        data = L.extract_json('note\n```json\n{"tasks":[{"title":"a"}]}\n```')
        self.assertEqual(data["tasks"][0]["title"], "a")

    def test_profile_has_seed_tasks(self):
        cfg = {"active_project": "ashes-to-stars"}
        profile = L.load_profile(cfg)
        self.assertTrue(profile.get("seed_tasks"))

    def test_seed_fills_empty_queue(self):
        cfg = {
            "active_project": "ashes-to-stars",
            "state_file": str(Path(tempfile.gettempdir()) / "autodev-test-state.json"),
        }
        st = L.new_state()
        self.assertTrue(L.seed_if_empty(cfg, st))
        self.assertGreaterEqual(len(st["tasks"]), 1)
        self.assertFalse(L.seed_if_empty(cfg, st))

    def test_next_ready_picks_highest_priority(self):
        st = L.new_state()
        st["tasks"] = [
            {"id": "T1", "status": "pending", "priority": 10},
            {"id": "T2", "status": "pending", "priority": 90},
        ]
        self.assertEqual(L.next_ready(st)["id"], "T2")


if __name__ == "__main__":
    unittest.main()
