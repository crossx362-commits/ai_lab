import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("autodev_v2", ROOT / "autodev.py")
M = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(M)

MSPEC = importlib.util.spec_from_file_location("migrate_v1", ROOT / "migrate_v1.py")
MG = importlib.util.module_from_spec(MSPEC)
MSPEC.loader.exec_module(MG)


class CoreTests(unittest.TestCase):
    def test_extract_json_with_noise(self):
        x = M.extract_json("설명\n```json\n{\"tasks\":[1]}\n```")
        self.assertEqual(x["tasks"], [1])

    def test_next_ready_respects_dependencies(self):
        st = M.new_state()
        st["tasks"] = [
            {"id": "T1", "status": "pending", "priority": 50, "depends_on": ["T0"], "created_at": "2"},
            {"id": "T2", "status": "pending", "priority": 40, "depends_on": [], "created_at": "1"},
        ]
        self.assertEqual(M.next_ready(st)["id"], "T2")
        st["completed"].append({"id": "T0"})
        self.assertEqual(M.next_ready(st)["id"], "T1")

    def test_schedule_patch_disables_only_game_ai_jobs(self):
        data = {"schedules": [
            {"id": "game_council", "enabled": True, "run": True},
            {"id": "game_agent_bomi", "enabled": True, "run": True},
            {"id": "harness_regression", "enabled": True, "run": True},
        ]}
        changed = MG.patch_schedule(data)
        self.assertEqual(set(changed), {"game_council", "game_agent_bomi"})
        self.assertFalse(data["schedules"][0]["enabled"])
        self.assertFalse(data["schedules"][1]["run"])
        self.assertTrue(data["schedules"][2]["enabled"])

    def test_normalize_tasks_assigns_dependencies(self):
        cfg = {"max_tasks_per_director_batch": 6}
        st = M.new_state()
        raw = [
            {"title": "A", "goal": "a", "done_when": ["x"], "priority": 90},
            {"title": "B", "goal": "b", "done_when": ["y"], "priority": 80, "depends_on": [1]},
        ]
        out = M.normalize_director_tasks(cfg, st, raw)
        self.assertEqual(out[0]["id"], "T0001")
        self.assertEqual(out[1]["depends_on"], ["T0001"])


if __name__ == "__main__":
    unittest.main()
