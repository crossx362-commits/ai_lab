import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("autodev_v2_runner_test", ROOT / "runner.py")
R = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(R)


class RunnerTests(unittest.TestCase):
    def test_local_director_result_does_not_increment_grok_stats(self):
        cfg = {"max_tasks_per_director_batch": 6}
        st = R.AUTODEV.new_state()
        out = json.dumps({
            "goal": "전투 수직 슬라이스",
            "tasks": [
                {
                    "title": "승리 처리",
                    "goal": "마지막 적 사망 시 승리 상태 연결",
                    "done_when": ["Victory 상태가 된다"],
                    "priority": 100,
                    "depends_on": [],
                    "verify_mode": "compile",
                    "milestone": False,
                }
            ],
        }, ensure_ascii=False)
        self.assertTrue(R.apply_director_result(cfg, st, out))
        self.assertEqual(st["stats"].get("grok_calls", 0), 0)
        self.assertEqual(st["stats"].get("director_calls", 0), 0)
        self.assertEqual(st["stats"].get("director_local_calls", 0), 1)
        self.assertEqual(st.get("last_director_provider"), "ollama")

    def test_invalid_local_director_json_is_rejected(self):
        cfg = {"max_tasks_per_director_batch": 6}
        st = R.AUTODEV.new_state()
        self.assertFalse(R.apply_director_result(cfg, st, '{"tasks": []}'))
        self.assertEqual(len(st["tasks"]), 0)

    def test_director_prompt_requires_json_tasks(self):
        cfg = {"max_tasks_per_director_batch": 6}
        st = R.AUTODEV.new_state()
        # compact helpers need file paths, so only verify the source contract here.
        source = (ROOT / "runner.py").read_text(encoding="utf-8")
        self.assertIn("JSON만 출력", source)
        self.assertIn("AUTODEV_LOCAL_DIRECTOR", source)
        self.assertIn("AI_TEAM_ALLOW_CLOUD_LLM", source)


if __name__ == '__main__':
    unittest.main()
