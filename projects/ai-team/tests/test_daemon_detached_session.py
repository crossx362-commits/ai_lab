"""상시 데몬은 자기를 띄운 프로세스의 수명과 무관해야 한다 (2026-08-04 실측 사고).

무슨 일이 있었나: 코드 갱신마다 워치독 로그에
`Down: bomi_qa, suri_dev, teo_test, baekho_backend, mio_design, namu_pm`이 반복됐다.
원인은 `agent_controller.start_agent`의 Popen이 `start_new_session` 없이 떠서
데몬이 **자기를 띄운 쪽의 프로세스 그룹에 그대로 묶인** 것이다.

예원 워치독은 launchd 잡(com.ailab.yewon_monitor)이고, 코드 갱신을 감지하면
데몬들을 재시작한 **직후 자가교체로 sys.exit(0)** 한다. launchd가 잡을 내리며 그
프로세스 그룹을 정리하면, 방금 띄운 데몬 6개가 통째로 같이 죽었다.

여기서 굳히는 것:
  1. start_agent가 POSIX에서 start_new_session=True로 띄운다.
  2. 실제로 띄운 프로세스의 PGID가 자기 PID와 같다(= 자기 그룹으로 분리됨).
     이 단언이 깨지면 부모가 죽을 때 데몬이 연좌로 죽는다.
"""
import ast
import os
import signal
import subprocess
import sys
import time
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_CTRL = AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "agent_controller.py"


class StartNewSessionTests(unittest.TestCase):
    def test_start_agent_passes_start_new_session(self):
        tree = ast.parse(_CTRL.read_text(encoding="utf-8"))
        fn = next(n for n in ast.walk(tree)
                  if isinstance(n, ast.FunctionDef) and n.name == "start_agent")
        self.assertIn("start_new_session", ast.dump(fn),
                      "start_agent의 Popen이 세션을 분리하지 않는다 — "
                      "띄운 프로세스가 죽을 때 데몬이 연좌로 죽는다")


class DetachedChildSurvivesParentTests(unittest.TestCase):
    """같은 Popen 설정으로 띄운 자식이 부모 그룹에서 분리되는지 실제로 확인한다."""

    def test_child_gets_own_process_group(self):
        child = subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(30)"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            start_new_session=True,
        )
        try:
            time.sleep(0.4)
            self.assertEqual(os.getpgid(child.pid), child.pid,
                             "자식이 자기 프로세스 그룹을 갖지 않는다 — "
                             "부모 그룹에 SIGTERM이 가면 같이 죽는다")
            self.assertNotEqual(os.getpgid(child.pid), os.getpgid(0),
                                "자식이 이 테스트 프로세스와 같은 그룹에 있다")
        finally:
            child.kill()
            child.wait(10)

    def test_group_signal_does_not_reach_detached_child(self):
        """분리하지 않았다면 죽었을 시나리오 — 그룹 시그널이 자식에 닿지 않는다."""
        child = subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(30)"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            start_new_session=True,
        )
        try:
            time.sleep(0.4)
            os.killpg(os.getpgid(0), 0)          # 우리 그룹이 유효한지만 확인(시그널 0)
            os.kill(child.pid, 0)                 # 자식 생존
            # 우리 그룹에 SIGTERM을 실제로 쏘면 테스트 러너가 죽으므로,
            # 그룹이 서로 다르다는 사실로 대체 검증한다.
            self.assertNotEqual(os.getpgid(child.pid), os.getpgid(0))
        finally:
            child.kill()
            child.wait(10)


if __name__ == "__main__":
    unittest.main()
