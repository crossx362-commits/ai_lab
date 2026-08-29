import importlib.util
import json
import os
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]

SPEC = importlib.util.spec_from_file_location("autodev_v2", ROOT / "autodev.py")
M = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(M)

MSPEC = importlib.util.spec_from_file_location("migrate_v1", ROOT / "migrate_v1.py")
MG = importlib.util.module_from_spec(MSPEC)
MSPEC.loader.exec_module(MG)

CSPEC = importlib.util.spec_from_file_location("shared_cc", REPO / "projects/ai-team/_shared/cc.py")
CC = importlib.util.module_from_spec(CSPEC)
CSPEC.loader.exec_module(CC)

LSPEC = importlib.util.spec_from_file_location("shared_llm", REPO / "projects/ai-team/_shared/llm.py")
LLM = importlib.util.module_from_spec(LSPEC)
LSPEC.loader.exec_module(LLM)


CURRENT_GROK_HELP = """
Usage: grok --single <PROMPT> --cwd <CWD> --output-format <OUTPUT_FORMAT>
  --no-auto-update
  -p, --single <PROMPT>
  --cwd <PATH>
  --output-format <FMT>
  --max-turns <N>
  --no-plan
  --no-subagents
  --no-memory
  --disable-web-search
  --always-approve
  --deny <RULE>
"""


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

    def test_repo_keeps_legacy_game_schedules_off(self):
        path = REPO / "projects/ai-team/skills/영숙_비서/tools/schedules.json"
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for job in data["schedules"]:
            jid = job.get("id", "")
            if jid in MG.HEAVY_IDS or jid.startswith("game_agent_"):
                self.assertFalse(job.get("enabled", True), jid)
                self.assertFalse(job.get("run", True), jid)

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

    def test_v2_uses_compact_knowledge_not_legacy_handoff(self):
        cfg = json.loads((ROOT / "config.json").read_text(encoding="utf-8"))
        self.assertEqual(cfg["handoff_file"], "projects/autodev-v2/KNOWLEDGE.md")
        self.assertLessEqual(cfg["max_candidate_files"], 5)
        self.assertLessEqual(cfg["max_context_chars"], 16000)
        self.assertLessEqual(cfg["max_cloud_calls_per_run"], 12)

    def test_core_rules_disable_claude_and_legacy_queue(self):
        rules = (ROOT / "CORE_RULES.md").read_text(encoding="utf-8")
        self.assertIn("Claude는 사용하지 않는다", rules)
        self.assertIn("ORDERS.md", rules)
        self.assertIn("state.json", rules)

    def test_codex_stop_autopilot_hook_is_removed(self):
        hooks = json.loads((REPO / ".codex/hooks.json").read_text(encoding="utf-8"))
        stop = (hooks.get("hooks") or {}).get("Stop") or []
        self.assertNotIn("autopilot_stop_hook.py", json.dumps(stop, ensure_ascii=False))

    def test_legacy_claude_helper_is_off_by_default(self):
        with mock.patch.dict(os.environ, {}, clear=False):
            os.environ.pop("AI_TEAM_ENABLE_CLAUDE", None)
            ok, msg = CC.run_claude("x", REPO)
        self.assertFalse(ok)
        self.assertIn("Claude 비활성", msg)

    def test_shared_llm_has_no_claude_fallback(self):
        self.assertIsNone(LLM.claude_code("테스트"))
        source = (REPO / "projects/ai-team/_shared/llm.py").read_text(encoding="utf-8")
        self.assertIn("Ollama(로컬) → Grok Build 구독 CLI → Codex 구독 CLI → Gemini", source)

    def test_grok_command_never_uses_removed_agent_profile(self):
        cmd = M.build_grok_command(
            "/usr/local/bin/grok", "x", REPO,
            max_turns=2, allow_edits=False, help_text=CURRENT_GROK_HELP,
        )
        self.assertNotIn("--agent-profile", cmd)
        self.assertIn("--single", cmd)
        self.assertIn("--no-plan", cmd)
        self.assertIn("--no-subagents", cmd)
        self.assertIn("--no-memory", cmd)
        self.assertIn("--disable-web-search", cmd)

    def test_grok_worker_command_has_approval_and_deny_guards(self):
        cmd = M.build_grok_command(
            "/usr/local/bin/grok", "x", REPO,
            max_turns=6, allow_edits=True, help_text=CURRENT_GROK_HELP,
        )
        self.assertIn("--always-approve", cmd)
        self.assertIn("--deny", cmd)
        self.assertIn("Bash(git push *)", cmd)
        self.assertIn("Bash(git reset --hard*)", cmd)

    def test_grok_command_fails_closed_when_saving_flags_are_missing(self):
        old_help = "Usage: grok --single <PROMPT> --cwd <CWD> --output-format <FMT> --max-turns <N> --always-approve"
        with self.assertRaises(RuntimeError) as cm:
            M.build_grok_command(
                "/usr/local/bin/grok", "x", REPO,
                max_turns=2, allow_edits=False, help_text=old_help,
            )
        self.assertIn("절약 옵션", str(cm.exception))

    def test_autodev_source_has_no_agent_profile_argv(self):
        source = (ROOT / "autodev.py").read_text(encoding="utf-8")
        self.assertNotIn('cmd += ["--agent-profile"', source)


if __name__ == "__main__":
    unittest.main()
