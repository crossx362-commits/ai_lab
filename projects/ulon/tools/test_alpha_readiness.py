#!/usr/bin/env python3
"""운영 서비스를 실행하지 않고 Closed Alpha 준비 판정의 실패 전파를 검사한다."""
import os
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.stdout.reconfigure(encoding="utf-8")
SOURCE = Path(__file__).with_name("closed_alpha_smoke.sh")
sys.path.insert(0, str(Path(__file__).resolve().parent))
import alpha_ready  # noqa: E402


class AlphaReadinessTests(unittest.TestCase):
    def test_unready_or_invalid_service_is_not_reported_ready(self):
        for mode in ("unavailable", "invalid", "false", "ready"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                for name in ("tools", "server", "bin"):
                    (root / name).mkdir()
                shutil.copyfile(SOURCE, root / "tools" / SOURCE.name)
                for name in ("start_postgres.sh", "start_persist.sh"):
                    p = root / "server" / name
                    p.write_text("#!/bin/sh\nexit 0\n")
                    p.chmod(0o755)
                curl = root / "bin" / "curl"
                curl.write_text('''#!/bin/sh
case "$*" in
  */health*) echo '{"ok":true}'; exit 0 ;;
esac
case "$READINESS_CASE" in
  unavailable) exit 22 ;;
  invalid) echo 'not-json' ;;
  false) echo '{"ok":false}' ;;
  ready) echo '{"ok":true}' ;;
esac
''')
                curl.chmod(0o755)
                env = dict(os.environ, PATH=str(root / "bin") + ":" + os.environ["PATH"], READINESS_CASE=mode)
                result = subprocess.run(["zsh", str(root / "tools" / SOURCE.name)],
                                        env=env, capture_output=True, text=True, timeout=10)
                if mode == "ready":
                    self.assertEqual(result.returncode, 0, result.stderr)
                    status = json.loads((root / "data" / "alpha_status.json").read_text())
                    self.assertTrue(status["persist"]["ok"])
                    continue
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse((root / "data" / "alpha_status.json").exists())
                self.assertIn("persist /ready", result.stderr)

    def test_smoke_failure_deletes_stale_success_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ("tools", "server", "bin", "data"):
                (root / name).mkdir()
            shutil.copyfile(SOURCE, root / "tools" / SOURCE.name)
            for name in ("start_postgres.sh", "start_persist.sh"):
                p = root / "server" / name
                p.write_text("#!/bin/sh\nexit 0\n")
                p.chmod(0o755)
            curl = root / "bin" / "curl"
            curl.write_text("#!/bin/sh\nexit 22\n")
            curl.chmod(0o755)
            stale = root / "data" / "alpha_status.json"
            stale.write_text(json.dumps({"ok": True, "persist": {"ok": True}}), encoding="utf-8")
            env = dict(os.environ, PATH=str(root / "bin") + ":" + os.environ["PATH"])
            result = subprocess.run(
                ["zsh", str(root / "tools" / SOURCE.name)],
                env=env,
                capture_output=True,
                text=True,
                timeout=10,
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse(stale.exists(), "실패 스모크가 옛 ok:true 파일을 남겨 두면 안 된다")


class AlphaReadyJudgeTests(unittest.TestCase):
    def test_stale_file_is_not_current_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "alpha_status.json"
            path.write_text(json.dumps({"ok": True, "persist": {"ok": True}}), encoding="utf-8")
            got = alpha_ready.judge(path, probe=lambda: {"ok": False, "http": 0, "body": {}, "error": "unavailable"})
            self.assertFalse(got["ok"])
            self.assertTrue(got["stale_file"])
            self.assertIn("옛 성공", got["reason"])

    def test_live_ready_even_if_file_missing(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "alpha_status.json"
            got = alpha_ready.judge(path, probe=lambda: {"ok": True, "http": 200, "body": {"ok": True}, "error": ""})
            self.assertTrue(got["ok"])
            self.assertFalse(got["stale_file"])
            self.assertFalse(got["file"]["present"])

    def test_file_ok_false_does_not_override_live_ok(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "alpha_status.json"
            path.write_text(json.dumps({"ok": False}), encoding="utf-8")
            got = alpha_ready.judge(path, probe=lambda: {"ok": True, "http": 200, "body": {"ok": True}, "error": ""})
            self.assertTrue(got["ok"])
            self.assertFalse(got["stale_file"])

    def test_non_json_live_is_not_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "alpha_status.json"
            path.write_text(json.dumps({"ok": True}), encoding="utf-8")
            got = alpha_ready.judge(
                path,
                probe=lambda: alpha_ready._parse_live("not-json", http=200, error=""),
            )
            self.assertFalse(got["ok"])
            self.assertTrue(got["stale_file"])

    def test_invalidate_removes_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "alpha_status.json"
            path.write_text("{}", encoding="utf-8")
            self.assertTrue(alpha_ready.invalidate(path))
            self.assertFalse(path.exists())
            self.assertTrue(alpha_ready.invalidate(path))


if __name__ == "__main__":
    unittest.main(verbosity=2)
