import importlib.util
import json
import os
import tempfile
import time
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("codex_compat_test", ROOT / "codex_compat.py")
CC = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CC)


class CodexCompatTests(unittest.TestCase):
    def test_detects_usage_limit_text(self):
        self.assertTrue(CC.looks_like_quota_error("You've hit your weekly usage limit"))
        self.assertTrue(CC.looks_like_quota_error("quota exceeded"))
        self.assertFalse(CC.looks_like_quota_error("compile failed"))

    def test_cooldown_state_expires(self):
        with tempfile.TemporaryDirectory() as td:
            state = Path(td) / "codex_quota.json"
            with mock.patch.dict(os.environ, {
                "AUTODEV_CODEX_QUOTA_STATE": str(state),
                "AUTODEV_CODEX_QUOTA_COOLDOWN_SECONDS": "3600",
            }, clear=False):
                CC.mark_exhausted("usage limit")
                self.assertTrue(CC.cooldown_active())
                data = json.loads(state.read_text(encoding="utf-8"))
                data["detected_at"] = time.time() - 7200
                state.write_text(json.dumps(data), encoding="utf-8")
                self.assertFalse(CC.cooldown_active())


if __name__ == "__main__":
    unittest.main()
