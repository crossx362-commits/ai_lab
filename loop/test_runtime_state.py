#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


HERE = Path(__file__).resolve().parent
MODULE_PATH = HERE / "runtime_state.py"


def load_module():
    spec = importlib.util.spec_from_file_location("runtime_state", MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError("runtime_state.py를 불러올 수 없다")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RuntimeStateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / "runtime_state.json"

    def tearDown(self) -> None:
        self.tmp.cleanup()

    def test_missing_file_reads_owner_stopped_defaults(self) -> None:
        state = load_module()

        got = state.read_state(self.path)

        self.assertEqual(got["phase"], "owner_stopped")
        self.assertEqual(got["provider"], "")
        self.assertEqual(got["recovery_claims"], [])

    def test_update_is_atomic_and_preserves_recovery_claims(self) -> None:
        state = load_module()
        state.update_state(self.path, phase="quota_wait", provider="claude")

        self.assertTrue(state.claim_recovery(self.path, "loop:abc"))
        self.assertFalse(state.claim_recovery(self.path, "loop:abc"))
        got = state.read_state(self.path)

        self.assertEqual(got["phase"], "quota_wait")
        self.assertEqual(got["provider"], "claude")
        self.assertEqual(got["recovery_claims"], ["loop:abc"])
        self.assertEqual(list(self.path.parent.glob("*.tmp")), [])

    def test_claim_keeps_only_the_latest_32_fingerprints(self) -> None:
        state = load_module()

        for index in range(35):
            self.assertTrue(state.claim_recovery(self.path, f"board:{index}"))

        claims = state.read_state(self.path)["recovery_claims"]
        self.assertEqual(len(claims), 32)
        self.assertEqual(claims[0], "board:3")
        self.assertEqual(claims[-1], "board:34")

    def test_usage_limit_is_quota_but_access_denial_is_error(self) -> None:
        state = load_module()

        self.assertEqual(state.classify_failure("usage limit reached", 1), "quota")
        self.assertEqual(state.classify_failure("할당량 초과", 1), "quota")
        self.assertEqual(
            state.classify_failure(
                "Your organization has disabled Claude subscription access", 1
            ),
            "error",
        )
        self.assertEqual(state.classify_failure("authentication required", 1), "error")
        self.assertEqual(state.classify_failure("", 0), "ok")

    def test_error_fingerprint_is_stable_and_changes_with_context(self) -> None:
        state = load_module()

        first = state.error_fingerprint("claude", 1, "same error\n", "head-a")
        same = state.error_fingerprint("claude", 1, "same error\n", "head-a")
        changed = state.error_fingerprint("claude", 1, "same error\n", "head-b")

        self.assertEqual(first, same)
        self.assertNotEqual(first, changed)
        self.assertEqual(len(first), 64)

    def test_error_fingerprint_ignores_volatile_lap_wrapper_lines(self) -> None:
        state = load_module()
        first = state.error_fingerprint(
            "claude",
            1,
            "바퀴 시작: 20260828-010101-1 agent=claude\nsame provider error\n"
            "바퀴 종료: 20260828-010101-1 code=1\n",
            "head-a",
        )
        second = state.error_fingerprint(
            "claude",
            1,
            "바퀴 시작: 20260828-020202-2 agent=claude\nsame provider error\n"
            "바퀴 종료: 20260828-020202-2 code=1\n",
            "head-a",
        )

        self.assertEqual(first, second)

    def test_cli_set_get_and_heartbeat(self) -> None:
        set_result = subprocess.run(
            [
                sys.executable,
                str(MODULE_PATH),
                "--path",
                str(self.path),
                "set",
                "running",
                "--provider",
                "codex",
                "--reason",
                "started",
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        self.assertEqual(set_result.returncode, 0, set_result.stderr)

        before = json.loads(set_result.stdout)["heartbeat_at"]
        heartbeat_result = subprocess.run(
            [sys.executable, str(MODULE_PATH), "--path", str(self.path), "heartbeat"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        get_result = subprocess.run(
            [
                sys.executable,
                str(MODULE_PATH),
                "--path",
                str(self.path),
                "get",
                "provider",
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )

        self.assertEqual(heartbeat_result.returncode, 0, heartbeat_result.stderr)
        self.assertGreaterEqual(json.loads(heartbeat_result.stdout)["heartbeat_at"], before)
        self.assertEqual(get_result.returncode, 0, get_result.stderr)
        self.assertEqual(get_result.stdout.strip(), "codex")


if __name__ == "__main__":
    unittest.main()
