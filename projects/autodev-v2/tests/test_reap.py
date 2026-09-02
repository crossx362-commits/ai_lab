import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("reap", ROOT / "reap.py")
R = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(R)


class ReapTests(unittest.TestCase):
    def test_keeps_unity(self):
        self.assertTrue(R.is_unity("/Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity"))
        self.assertFalse(R.is_target("/Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity -projectPath /Users/x/ai_lab/projects/ashes-to-stars/unity"))

    def test_keeps_dashboard(self):
        self.assertTrue(R.is_keep("python3 /Users/x/ai_lab/projects/autodev-v2/webview_app.py", 9, {9}))
        self.assertFalse(R.is_target("python3 /Users/x/ai_lab/projects/autodev-v2/webview_app.py"))

    def test_kills_stale_engine_and_runner(self):
        self.assertTrue(R.is_target("python3 /Users/junholee/ai_lab/projects/autodev-v2/engine.py"))
        self.assertTrue(R.is_target("python3 /Users/junholee/ai_lab/projects/autodev-v2/runner_entry.py --continuous"))
        self.assertTrue(R.is_target("python3 /Users/junholee/ai_lab/projects/autodev-v2/start.py"))

    def test_kills_wrapper_cli(self):
        self.assertTrue(R.is_target("/Users/junholee/ai_lab/output/autodev_v2/runtime_bin/grok --single x"))

    def test_ignores_unrelated_python(self):
        self.assertFalse(R.is_target("python3 /Users/junholee/other/app.py"))


if __name__ == "__main__":
    unittest.main()
