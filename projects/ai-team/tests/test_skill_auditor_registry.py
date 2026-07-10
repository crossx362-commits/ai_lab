import importlib.util
import os
import pathlib
import subprocess
import sys
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "skills" / "예원_CEO" / "tools" / "skill_auditor.py"
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))


def load_auditor():
    spec = importlib.util.spec_from_file_location("skill_auditor_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SkillAuditorRegistryTests(unittest.TestCase):
    def test_auditor_targets_every_registered_agent_with_skill_file(self):
        auditor = load_auditor()

        from _shared.registry import SKILLS_DIR, active_agents

        expected_agents = []
        for meta in active_agents().values():
            folder = meta.get("folder")
            if folder and (SKILLS_DIR / folder / "SKILL.md").exists():
                expected_agents.append(meta["display"])

        missing = sorted(set(expected_agents) - set(auditor.AGENTS))

        self.assertEqual(
            missing,
            [],
            f"skill_auditor.py is not auditing registered agents: {missing}",
        )

    def test_check_mode_can_start_as_script(self):
        env = os.environ.copy()
        env["OLLAMA_URL"] = "http://127.0.0.1:9/v1/chat/completions"
        env["AI_TEAM_ALLOW_CLOUD_LLM"] = "0"

        result = subprocess.run(
            [sys.executable, str(SCRIPT), "--check"],
            cwd=str(ROOT.parents[1]),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=env,
            # 실측 10.3s(2026-07-10) — 옛 10s 제한은 사실상 동률이라 상시 flaky였다.
            timeout=60,
        )

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("ModuleNotFoundError", result.stderr)


if __name__ == "__main__":
    unittest.main()
