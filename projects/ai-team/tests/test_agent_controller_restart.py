"""restart가 '아무것도 안 남는' 상태로 끝나지 않는지 — 2026-08-04 전멸 사고.

관측: 예원이 코드 갱신으로 펫나 6종을 재시작한 뒤 6개가 전부 down이 됐다.
원인은 restart_agent의 순서였다. `kill` → `sleep(1)` → `start_agent`인데,
start_agent는 맨 먼저 find_agent_process로 "이미 실행 중"을 확인하고 그러면
**새 인스턴스를 안 띄운다**. SIGTERM을 받은 데몬이 사이클 중이라 1초 안에 못 죽으면
start는 기동을 건너뛰고, 그 직후 옛 프로세스가 죽어 아무것도 안 남는다.

여기서 굳히는 것: stop이 '정말 사라졌다'를 확인하고 돌아오므로, restart 뒤에는
반드시 새 프로세스가 뜬다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_PATH = AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "agent_controller.py"


def _load():
    spec = importlib.util.spec_from_file_location("agent_controller_restart_t", _PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class RestartAlwaysLeavesSomethingRunningTests(unittest.TestCase):
    def setUp(self):
        self.mod = _load()
        self.killed = []
        self.started = []
        # 느리게 죽는 데몬: kill 직후에도 두 번은 살아 있는 것으로 보인다.
        self.alive_reports = [[4242], [4242], []]
        self.mod.find_agent_process = lambda name: (
            self.alive_reports.pop(0) if self.alive_reports else [])
        self.mod.subprocess.run = lambda *a, **k: self.killed.append(a[0]) or type(
            "R", (), {"returncode": 0, "stdout": "", "stderr": ""})()
        self.mod.time.sleep = lambda s: None
        self.mod.STOP_WAIT_SEC = 0.3   # 실제 대기까지 재현할 필요는 없다
        self.mod.KILL_WAIT_SEC = 0.1

    def test_start_runs_after_process_actually_exits(self):
        self.mod.start_agent = lambda name: self.started.append(name) or "시작 완료"
        out = self.mod.restart_agent("봄이")
        self.assertTrue(self.started,
                        "느리게 죽는 데몬을 재시작했더니 새 인스턴스를 안 띄웠다 — 결국 아무것도 안 남는다")
        self.assertIn("시작 완료", out)

    def test_escalates_to_sigkill_when_it_never_exits(self):
        """영영 안 죽으면 SIGKILL까지 간다 — 안 그러면 restart가 조용히 무효가 된다."""
        self.mod.find_agent_process = lambda name: [4242]   # 절대 안 죽음
        self.mod.start_agent = lambda name: "시작 완료"
        out = self.mod.stop_agent("봄이")
        self.assertIn("강제 종료", out)
        self.assertTrue(any("-9" in c or "/F" in c for c in self.killed),
                        f"SIGKILL로 승격하지 않았다: {self.killed}")


if __name__ == "__main__":
    unittest.main()
