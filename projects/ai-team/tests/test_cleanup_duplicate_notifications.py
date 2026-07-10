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
    def test_no_removal_produces_no_message(self):
        module = load_cleanup_module()

        self.assertIsNone(module.format_removed_message([]))

    def test_removed_process_is_notified(self):
        module = load_cleanup_module()

        message = module.format_removed_message([
            ("봄이 QA 순찰", 12345, "terminated"),
        ])

        self.assertIsNotNone(message)
        self.assertIn("봄이 QA 순찰", message)
        self.assertIn("12345", message)


if __name__ == "__main__":
    unittest.main()
