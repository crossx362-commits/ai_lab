from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock


MODULE_PATH = Path(__file__).with_name("agent_runner.py")
SPEC = importlib.util.spec_from_file_location("agent_runner", MODULE_PATH)
assert SPEC and SPEC.loader
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class SchedulerTests(unittest.TestCase):
    def task(self, task_id: str, *paths: str) -> dict[str, object]:
        return {
            "id": task_id,
            "goal": f"goal-{task_id}",
            "write_paths": list(paths),
            "tests": [],
        }

    def test_parallel_selection_rejects_overlapping_paths(self) -> None:
        tasks = [
            self.task("hud", "game/ui/hud"),
            self.task("hud-copy", "game/ui/hud/panel.json"),
            self.task("enemy", "game/enemies"),
        ]
        selected, deferred = runner.select_independent_tasks(tasks, limit=3)
        self.assertEqual([task["id"] for task in selected], ["enemy", "hud"])
        self.assertEqual([task["id"] for task in deferred], ["hud-copy"])

    def test_provider_assignment_always_cross_reviews(self) -> None:
        tasks = [self.task("a", "a"), self.task("b", "b"), self.task("c", "c")]
        assignments = runner.assign_providers(tasks, ["claude", "codex", "grok"])
        self.assertEqual(len(assignments), 3)
        self.assertTrue(all(item.worker != item.reviewer for item in assignments))
        self.assertEqual([item.worker for item in assignments], ["claude", "codex", "grok"])

    def test_single_task_prefers_claude_and_codex_review(self) -> None:
        assignment = runner.assign_providers([self.task("a", "a")], ["claude", "codex", "grok"])[0]
        self.assertEqual((assignment.worker, assignment.reviewer), ("claude", "codex"))


class ProviderCommandTests(unittest.TestCase):
    def test_claude_uses_fable_then_only_opus5(self) -> None:
        command = runner.build_provider_command("claude", "prompt", 12, "worker")
        self.assertEqual(command[command.index("--model") + 1], "fable")
        self.assertEqual(command[command.index("--fallback-model") + 1], "opus5")
        self.assertIn("--no-session-persistence", command)
        self.assertNotIn("--resume", command)

    def test_codex_uses_strong_model_without_full_access(self) -> None:
        command = runner.build_provider_command("codex", "prompt", 12, "worker")
        self.assertIn("gpt-5.6-sol", command)
        self.assertIn("workspace-write", command)
        self.assertIn('model_reasoning_effort="xhigh"', command)
        self.assertNotIn("danger-full-access", command)
        self.assertNotIn("resume", command)

    def test_grok_uses_strong_stateless_contract(self) -> None:
        command = runner.build_provider_command("grok", "prompt", 12, "worker")
        self.assertIn("grok-4.6", command)
        self.assertIn("--no-memory", command)
        self.assertIn("--no-subagents", command)
        self.assertIn("auto", command)
        self.assertNotIn("bypassPermissions", command)


class ManifestTests(unittest.TestCase):
    def test_manifest_is_strict_and_deduplicated(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "tasks.json"
            path.write_text(json.dumps({"tasks": [
                {"id": "same", "goal": "one", "write_paths": ["a"], "tests": []},
                {"id": "same", "goal": "two", "write_paths": ["b"], "tests": []},
            ]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "duplicate task id"):
                runner.load_task_manifest(path)

    def test_changed_files_must_stay_inside_write_paths(self) -> None:
        runner.validate_changed_paths(["game/ui/a.json"], ["game/ui"])
        with self.assertRaisesRegex(ValueError, "outside write_paths"):
            runner.validate_changed_paths(["server/secret.py"], ["game/ui"])

    def test_json_payload_accepts_fenced_result(self) -> None:
        payload = runner.parse_json_payload('text\n```json\n{"approved": true}\n```')
        self.assertEqual(payload, {"approved": True})

    def test_json_payload_finds_review_inside_stream_event(self) -> None:
        event = json.dumps({"type": "item.completed", "item": {"text": '{"approved": true, "score": 90}'}})
        payload = runner.parse_json_payload(event)
        self.assertEqual(payload, {"approved": True, "score": 90})


class GitIntegrationTests(unittest.TestCase):
    def git(self, root: Path, *args: str) -> str:
        return subprocess.run(
            ["git", *args], cwd=root, check=True, capture_output=True,
            text=True, encoding="utf-8",
        ).stdout.strip()

    def test_approved_branches_merge_only_into_integration(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.git(root, "init", "-b", "master")
            self.git(root, "config", "user.name", "Loop Test")
            self.git(root, "config", "user.email", "loop@example.test")
            (root / "docs").mkdir()
            (root / "docs/STATUS.md").write_text("# 상태\n", encoding="utf-8")
            (root / "base.txt").write_text("base\n", encoding="utf-8")
            self.git(root, "add", "--", "docs/STATUS.md", "base.txt")
            self.git(root, "commit", "-m", "base")
            base = self.git(root, "rev-parse", "HEAD")

            reviewed = []
            for task_id in ("a", "b"):
                branch = f"autonomous/loop-test-{task_id}"
                self.git(root, "checkout", "-b", branch, base)
                (root / f"{task_id}.txt").write_text(task_id + "\n", encoding="utf-8")
                self.git(root, "add", "--", f"{task_id}.txt")
                self.git(root, "commit", "-m", task_id)
                head = self.git(root, "rev-parse", "HEAD")
                task = {"id": task_id, "goal": task_id, "write_paths": [f"{task_id}.txt"], "tests": []}
                assignment = runner.Assignment(task, "claude", "codex")
                candidate = runner.Candidate(assignment, branch, root, base, head, (f"{task_id}.txt",), True, "")
                reviewed.append(runner.ReviewedCandidate(candidate, True, {"approved": True}, ""))
            self.git(root, "checkout", "master")

            run_root = root.parent / (root.name + "-run")
            run_root.mkdir()
            try:
                with mock.patch.dict(os.environ, {"LOOP_PUSH": "0"}):
                    merged = runner.integrate_candidates(root, run_root, "test-run", reviewed)
                self.assertEqual(len(merged), 2)
                master_file = subprocess.run(
                    ["git", "show", "master:a.txt"], cwd=root,
                    capture_output=True, text=True, encoding="utf-8",
                )
                self.assertNotEqual(master_file.returncode, 0)
                self.assertEqual(self.git(root, "show", "autonomous/integration:a.txt"), "a")
                self.assertEqual(self.git(root, "show", "autonomous/integration:b.txt"), "b")
                status = self.git(root, "show", "autonomous/integration:docs/STATUS.md")
                self.assertIn("worker claude, reviewer codex", status)
                self.assertNotEqual(self.git(root, "rev-parse", "master"), self.git(root, "rev-parse", "autonomous/integration"))
            finally:
                run_root.rmdir()


if __name__ == "__main__":
    unittest.main(verbosity=2)
