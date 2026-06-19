import importlib.util
import sys
import tempfile
import unittest
from unittest import mock
import plistlib
from pathlib import Path


AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = AI_TEAM_ROOT / "scripts" / "start_trading_team.py"
LAUNCHD_DIR = AI_TEAM_ROOT / "scripts" / "launchd"


def load_start_trading_team_module():
    sys.path.insert(0, str(AI_TEAM_ROOT))
    spec = importlib.util.spec_from_file_location("start_trading_team_under_test", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class TradingProcessStartupTests(unittest.TestCase):
    def test_start_trading_team_imports_without_legacy_lock_api(self):
        module = load_start_trading_team_module()

        self.assertTrue(hasattr(module, "start_process"))

    def test_start_process_skips_when_same_script_is_already_running(self):
        module = load_start_trading_team_module()
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            script = tmp_path / "market_signal.py"
            script.write_text("print('running')\n", encoding="utf-8")

            module._LOG_DIR = str(tmp_path)
            popen_calls = []

            class FakePopen:
                def __init__(self, *args, **kwargs):
                    popen_calls.append((args, kwargs))

            with mock.patch.object(module, "is_script_already_running", return_value=True):
                with mock.patch.object(module.subprocess, "Popen", FakePopen):
                    process = module.start_process("시그널 (정보 수집)", str(script), ["--daemon"])

            self.assertIsNone(process)
            self.assertEqual(popen_calls, [])

    def test_launchd_installs_signal_agent(self):
        signal_plist = LAUNCHD_DIR / "com.ailab.signal.plist"
        install_script = (LAUNCHD_DIR / "install.sh").read_text(encoding="utf-8-sig")

        self.assertTrue(signal_plist.exists())
        config = plistlib.loads(signal_plist.read_bytes())
        self.assertEqual(config["Label"], "com.ailab.signal")
        self.assertIn("market_signal.py", " ".join(config["ProgramArguments"]))
        self.assertIn("com.ailab.signal", install_script)


if __name__ == "__main__":
    unittest.main()
