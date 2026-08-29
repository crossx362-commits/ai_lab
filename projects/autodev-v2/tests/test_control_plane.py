import importlib.util
import os
import tempfile
import time
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(mod)
    return mod


ENGINE = load("autodev_engine_test", ROOT / "engine.py")
WEB = load("autodev_web_test", ROOT / "webview_app.py")


class FakeProc:
    def __init__(self, pid=43210, rc=None):
        self.pid = pid
        self._rc = rc
        self.returncode = rc

    def poll(self):
        return self._rc


class ControlPlaneTests(unittest.TestCase):
    def test_dashboard_and_engine_share_protocol_and_fingerprint(self):
        self.assertEqual(WEB.CONTROL_PROTOCOL, ENGINE.CONTROL_PROTOCOL)
        self.assertEqual(tuple(WEB.CONTROL_FILES), tuple(ENGINE.CONTROL_FILES))
        self.assertEqual(WEB.control_fingerprint(), ENGINE.control_fingerprint())

    def test_dashboard_starts_only_engine_entrypoint(self):
        source = (ROOT / "webview_app.py").read_text(encoding="utf-8")
        self.assertIn('str(ENGINE_FILE)', source)
        # start/runner_entry may be mentioned for legacy cleanup only, never as Popen target.
        self.assertNotIn('Popen([sys.executable, str(HERE / "start.py")]', source)
        self.assertNotIn('Popen([sys.executable, str(HERE / "runner_entry.py")]', source)

    def test_engine_runs_runner_in_same_process(self):
        source = (ROOT / "engine.py").read_text(encoding="utf-8")
        self.assertIn("import runner_entry", source)
        self.assertIn("runner_entry.main()", source)
        self.assertNotIn('subprocess.run([sys.executable, str(HERE / "runner_entry.py")]', source)

    def test_update_scope_does_not_replace_game_project(self):
        self.assertIn("projects/autodev-v2", WEB.INFRA_PATHS)
        self.assertNotIn("projects/ashes-to-stars/unity", WEB.INFRA_PATHS)
        self.assertIn(
            "projects/ashes-to-stars/unity/Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs",
            WEB.INFRA_PATHS,
        )

    def test_stale_detection_uses_control_fingerprint_not_git_head(self):
        current = WEB.control_fingerprint()
        state = {
            "pid": 1234,
            "control_protocol": WEB.CONTROL_PROTOCOL,
            "control_fingerprint": current,
            "started_at": time.time(),
        }
        with mock.patch.object(WEB, "read_json", return_value=state), \
             mock.patch.object(WEB, "pid_alive", return_value=True), \
             mock.patch.object(WEB, "legacy_pids", return_value=[]):
            info = WEB.engine_info()
        self.assertTrue(info["running"])
        self.assertFalse(info["stale"])

    def test_stale_detection_rejects_old_engine(self):
        state = {
            "pid": 1234,
            "control_protocol": WEB.CONTROL_PROTOCOL - 1,
            "control_fingerprint": "old",
            "started_at": time.time(),
        }
        with mock.patch.object(WEB, "read_json", return_value=state), \
             mock.patch.object(WEB, "pid_alive", return_value=True), \
             mock.patch.object(WEB, "legacy_pids", return_value=[]):
            info = WEB.engine_info()
        self.assertTrue(info["stale"])

    def test_start_waits_for_same_pid_state_and_heartbeat(self):
        ctl = WEB.Controller()
        fake = FakeProc(pid=43210)
        calls = {"n": 0}

        def fake_read(path):
            if path == WEB.ENGINE_STATE:
                return {"pid": fake.pid}
            if path == WEB.HEARTBEAT:
                calls["n"] += 1
                return {"pid": fake.pid}
            return {}

        with mock.patch.object(WEB, "engine_info", return_value={"running": False, "stale": False, "legacy_pids": []}), \
             mock.patch.object(WEB.subprocess, "Popen", return_value=fake), \
             mock.patch.object(WEB, "read_json", side_effect=fake_read), \
             mock.patch.object(WEB, "tail_lines", return_value=[]), \
             mock.patch.object(WEB.Controller, "log", return_value=None), \
             mock.patch.object(WEB.ENGINE_LOG, "open", mock.mock_open()):
            result = ctl.start()
        self.assertTrue(result["ok"])
        self.assertIn("43210", result["message"])
        self.assertGreater(calls["n"], 0)

    def test_recover_is_stop_then_start(self):
        ctl = WEB.Controller()
        with mock.patch.object(ctl, "stop", return_value={"ok": True}), \
             mock.patch.object(ctl, "start", return_value={"ok": True, "message": "started"}) as start:
            result = ctl.recover()
        self.assertTrue(result["ok"])
        start.assert_called_once()

    def test_heartbeat_has_single_engine_pid_and_protocol(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "hb.json"
            with mock.patch.object(ENGINE, "HEARTBEAT", p):
                ENGINE.heartbeat("test", "alive")
            data = __import__("json").loads(p.read_text(encoding="utf-8"))
        self.assertEqual(data["pid"], os.getpid())
        self.assertEqual(data["engine_protocol"], ENGINE.CONTROL_PROTOCOL)
        self.assertEqual(data["stage"], "test")


if __name__ == "__main__":
    unittest.main()
