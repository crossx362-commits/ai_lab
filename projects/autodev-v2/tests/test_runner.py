import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("autodev_v2_runner_test", ROOT / "runner.py")
R = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(R)

ESPEC = importlib.util.spec_from_file_location("autodev_v2_loop_ext_test", ROOT / "loop_ext.py")
E = importlib.util.module_from_spec(ESPEC)
assert ESPEC and ESPEC.loader
ESPEC.loader.exec_module(E)


class RunnerTests(unittest.TestCase):
    def test_runner_is_grok_director_only(self):
        source = (ROOT / "runner.py").read_text(encoding="utf-8")
        self.assertIn("계획은 Grok Director만 담당", source)
        self.assertIn("AUTODEV.grok_call", source)
        self.assertNotIn("AUTODEV_LOCAL_DIRECTOR", source)
        self.assertNotIn("AI_TEAM_ALLOW_CLOUD_LLM", source)

    def test_duplicate_director_tasks_are_removed_locally(self):
        cfg = {
            "max_tasks_per_director_batch": 6,
            "duplicate_task_similarity": 0.88,
            "dedupe_history_limit": 40,
            "max_same_area_per_director_batch": 2,
        }
        st = R.AUTODEV.new_state()
        st["completed"].append({
            "id": "T0001",
            "title": "보스 승리 처리",
            "goal": "마지막 보스 사망 시 승리 상태 연결",
            "area": "combat",
        })
        raw = [{
            "title": "보스 승리 처리",
            "goal": "마지막 보스 사망 시 승리 상태 연결",
            "done_when": ["Victory 상태가 된다"],
            "priority": 100,
            "depends_on": [],
            "verify_mode": "compile",
            "milestone": False,
        }]
        self.assertEqual(R.guard_director_raw(cfg, st, raw), [])

    def test_same_area_batch_is_capped(self):
        cfg = {
            "max_tasks_per_director_batch": 6,
            "duplicate_task_similarity": 0.99,
            "dedupe_history_limit": 40,
            "max_same_area_per_director_batch": 2,
        }
        st = R.AUTODEV.new_state()
        raw = [
            {"title": f"전투 기능 {i}", "goal": f"전투 공격 기능 {i} 구현", "done_when": ["동작"], "depends_on": []}
            for i in range(4)
        ]
        guarded = R.guard_director_raw(cfg, st, raw)
        self.assertLessEqual(sum(1 for x in guarded if x.get("area") == "combat"), 2)

    def test_provider_quota_is_temporary_not_task_failure(self):
        self.assertEqual(R._provider_state(88, "quota exhausted"), "temporary")
        self.assertEqual(R._provider_state(127, "cli를 찾을 수 없습니다"), "permanent")

    def test_hourly_cloud_cap(self):
        cfg = {"max_cloud_calls_per_hour": 2}
        now = __import__("time").time()
        st = {"cloud_call_times": [now - 10, now - 5]}
        self.assertFalse(R.cloud_slot_available(cfg, st))

    def test_prompt_version_is_v2(self):
        source = (ROOT / "runner.py").read_text(encoding="utf-8")
        self.assertIn("[AutoDev v2 추가 안전 규칙]", source)
        self.assertNotIn("[AutoDev v3 추가 안전 규칙]", source)

    def test_estate_text_is_not_generic_systems(self):
        self.assertEqual(R.infer_area({"title": "영지 건물 4종 동작", "goal": "EstateScreen에서 성장"}), "estate")
        self.assertEqual(R.infer_area({"title": "출전 편성", "goal": "W3Party 파티 편성"}), "formation")
        self.assertEqual(R.infer_area({"title": "보스전 쪹 소환", "goal": "BossBattle 레이드"}), "raid")

    def test_loop_ext_has_status_compact_and_seed(self):
        source = (ROOT / "loop_ext.py").read_text(encoding="utf-8")
        self.assertIn("compact_status_next", source)
        self.assertIn("seed_play_loop_if_empty", source)
        self.assertIn("STATUS 전체와 아트/ox-alpha/폴리싱 로그는 작업이 아니다", source)

    def test_project_profile_loads_seed_tasks(self):
        profile = E.load_project_profile({"active_project": "ashes-to-stars"})
        self.assertTrue(profile.get("seed_tasks"))

    def test_seed_play_loop_fills_empty_queue_after_install(self):
        E.install()
        cfg = {
            "active_project": "ashes-to-stars",
            "max_tasks_per_director_batch": 6,
            "duplicate_task_similarity": 0.88,
            "dedupe_history_limit": 40,
            "max_same_area_per_director_batch": 2,
        }
        st = R.AUTODEV.new_state()
        self.assertTrue(R.seed_play_loop_if_empty(cfg, st))
        self.assertGreaterEqual(len(st["tasks"]), 1)
        self.assertFalse(R.seed_play_loop_if_empty(cfg, st))


if __name__ == "__main__":
    unittest.main()
