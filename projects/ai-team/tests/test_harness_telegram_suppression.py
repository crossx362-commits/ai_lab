import importlib.util
import unittest
from pathlib import Path
from types import SimpleNamespace


AI_TEAM = Path(__file__).resolve().parents[1]


def load_module(name, relative_path):
    path = AI_TEAM / relative_path
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class HarnessTelegramSuppressionTests(unittest.TestCase):
    def assert_harness_runs_silently(self, module, callback):
        captured = {}
        original_run = module.subprocess.run
        module.subprocess.run = lambda *args, **kwargs: (
            captured.update(kwargs) or SimpleNamespace(returncode=0, stdout="OK", stderr="")
        )
        try:
            callback()
        finally:
            module.subprocess.run = original_run

        self.assertEqual(captured["env"].get("SUPPRESS_TELEGRAM"), "true")

    def test_yewon_dispatcher_suppresses_harness_telegram_alerts(self):
        module = load_module("yewon_dispatcher_under_test", "skills/예원_CEO/tools/yewon_dispatcher.py")
        self.assert_harness_runs_silently(module, module._run_harness)

    def test_harness_manager_suppresses_harness_telegram_alerts(self):
        module = load_module("harness_manager_under_test", "skills/예원_CEO/tools/harness_manager.py")
        manager = module.HarnessManager()
        self.assert_harness_runs_silently(module, manager.run_harness)

    def test_harness_monitor_suppresses_harness_telegram_alerts(self):
        module = load_module("harness_monitor_under_test", "skills/예원_CEO/tools/harness_monitor.py")
        self.assert_harness_runs_silently(module, module.run_harness)

    def test_harness_manager_does_not_send_warning_summaries(self):
        module = load_module("harness_manager_summary_under_test", "skills/예원_CEO/tools/harness_manager.py")
        manager = module.HarnessManager()
        original_run_harness = manager.run_harness
        original_analyze_structure = manager.analyze_structure
        original_agent_status = module.agent_status
        original_send = module.send
        sent = []
        manager.run_harness = lambda: "[WARN] runtime: down"
        manager.analyze_structure = lambda: []
        module.agent_status = lambda: {"youngsuk": "down"}
        module.send = lambda *args, **kwargs: sent.append((args, kwargs))
        try:
            manager.generate_report()
        finally:
            manager.run_harness = original_run_harness
            manager.analyze_structure = original_analyze_structure
            module.agent_status = original_agent_status
            module.send = original_send

        self.assertEqual(sent, [])


if __name__ == "__main__":
    unittest.main()
