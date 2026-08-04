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


class FixBeforeAlertOrderTests(unittest.TestCase):
    """오너 지시(2026-08-04): "경고 메시지는 알아서 해결하고 텔레그램으로 보내지 마라."

    워치독은 이슈를 보면 **먼저 auto_remediate로 고치고**, 고친 뒤에도 남은 것만 보고해야
    한다. 옛 순서(감지 즉시 발송 → 그 다음 복구)는 예원이 10초 뒤 스스로 고칠 일까지
    사람을 불렀다. 루프가 while True라 실행으로는 검증하기 어려워 호출 순서로 굳힌다.
    """

    def _loop_calls(self):
        import ast
        path = AI_TEAM / "skills" / "예원_CEO" / "tools" / "harness_monitor.py"
        tree = ast.parse(path.read_text(encoding="utf-8"))
        fn = next(n for n in ast.walk(tree)
                  if isinstance(n, ast.FunctionDef) and n.name == "main")
        return ast.walk(fn)

    def test_auto_remediate_runs_before_issue_alert(self):
        import ast
        first_remedy = first_alert = None
        for node in self._loop_calls():
            if not isinstance(node, ast.Call):
                continue
            name = getattr(node.func, "id", None)
            if name == "auto_remediate" and first_remedy is None:
                first_remedy = node.lineno
            if name == "send" and node.args:
                text = ast.dump(node.args[0])
                if "이슈" in text and first_alert is None:
                    first_alert = node.lineno
        self.assertIsNotNone(first_remedy, "main 루프에서 auto_remediate 호출을 못 찾았다")
        self.assertIsNotNone(first_alert, "main 루프에서 이슈 경보 send를 못 찾았다")
        self.assertLess(first_remedy, first_alert,
                        "이슈 경보가 자동 복구보다 먼저 나간다 — 예원이 고칠 일까지 사람을 부른다")

    def test_self_handled_events_are_not_telegrammed(self):
        """예원이 스스로 끝낸 일(코드 갱신 재시작·죽은 잡 정리)은 알림 대상이 아니다."""
        path = AI_TEAM / "skills" / "예원_CEO" / "tools" / "harness_monitor.py"
        src = path.read_text(encoding="utf-8")
        for phrase in ("코드 갱신 감지(git pull)", "백그라운드 작업 정리"):
            for line in src.splitlines():
                if phrase in line:
                    self.assertNotIn("send(", line,
                                     f"'{phrase}'는 예원이 처리한 일인데 텔레그램으로 보낸다")


if __name__ == "__main__":
    unittest.main()
