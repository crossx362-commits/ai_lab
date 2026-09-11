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


if __name__ == "__main__":
    unittest.main(verbosity=2)
