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

        from _shared.agent_registry import scan_agents

        expected_agents = []
        for info in scan_agents().values():
            agent_dir = pathlib.Path(info["path"]).parents[1]
            if (agent_dir / "SKILL.md").exists():
                expected_agents.append(info["name"].split("_", 1)[0])

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
            env=env,
            timeout=10,
        )

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("ModuleNotFoundError", result.stderr)


if __name__ == "__main__":
    unittest.main()
