import importlib.util
import sys
import unittest
from pathlib import Path


AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = AI_TEAM_ROOT / "scripts" / "cleanup_duplicate_processes.py"


def load_cleanup_module():
    sys.path.insert(0, str(AI_TEAM_ROOT))
    spec = importlib.util.spec_from_file_location("cleanup_duplicate_processes_under_test", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class CleanupDuplicateNotificationTests(unittest.TestCase):
    def test_signal_duplicate_cleanup_is_not_notified(self):
        module = load_cleanup_module()

        message = module.format_removed_message([
            ("시그널 시장정보", 12345, "terminated"),
        ])

        self.assertIsNone(message)

    def test_non_hyunbin_duplicate_cleanup_is_notified(self):
        module = load_cleanup_module()

        message = module.format_removed_message([
            ("데이브 트레이더", 12345, "terminated"),
        ])

        self.assertIsNotNone(message)
        self.assertIn("데이브 트레이더", message)


if __name__ == "__main__":
    unittest.main()
