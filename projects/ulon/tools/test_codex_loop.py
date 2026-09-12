"""실제 자식 프로세스로 Codex 루프 수명·정지·실패 제한을 검사한다."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

RUNNER = Path(__file__).resolve().parents[1] / 'loop/codex_loop.py'


class LoopTests(unittest.TestCase):
    def run_case(self, body, *, loops=1, timeout=3, stop=False):
        self.assertTrue(RUNNER.exists(), '그록 방식의 독립 Codex 프로세스 루프가 아직 없습니다')
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            fake = root / 'fake-codex'
            fake.write_text('#!/usr/bin/env python3\n' + body)
            fake.chmod(0o755)
            prompt = root / 'prompt.md'; prompt.write_text('한 바퀴만 실행')
            if stop: (root / 'STOP').touch()
            cmd = [sys.executable, str(RUNNER), '--cli', str(fake), '--worktree', str(root),
                   '--prompt', str(prompt), '--run-dir', str(root / 'run'), '--stop-file', str(root/'STOP'),
                   '--pause', '0', '--timeout', str(timeout), '--max-loops', str(loops)]
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=15)
            state = json.loads((root/'run/runtime_state.json').read_text())
            hist = root/'run/history.jsonl'
            rows = [json.loads(x) for x in hist.read_text().splitlines()] if hist.exists() else []
            return result, state, rows

    def test_fresh_session_per_iteration(self):
        result, state, rows = self.run_case('import os,sys\nprint("prompt:",sys.stdin.read())\n', loops=2)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(rows), 2)
        self.assertNotEqual(rows[0]['child_pid'], rows[1]['child_pid'])
        self.assertEqual(state['status'], 'max_loops')
        self.assertEqual(rows[0]['result'], 'session_finished')

    def test_existing_stop_starts_no_session(self):
        result, state, rows = self.run_case('raise RuntimeError("실행되면 안 됨")\n', stop=True)
        self.assertEqual(result.returncode, 0)
        self.assertEqual(state['status'], 'owner_stopped')
        self.assertEqual(rows, [])

    def test_three_failures_stop_loop(self):
        result, state, rows = self.run_case('import sys\nsys.exit(7)\n', loops=0)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(len(rows), 3)
        self.assertEqual(state['status'], 'stopped_fail')
        self.assertEqual(state['consec_fail'], 3)

    def test_timeout_is_not_success(self):
        result, state, rows = self.run_case('import time\ntime.sleep(10)\n', timeout=.15)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]['result'], 'timeout')
        self.assertEqual(rows[0]['rc'], 124)

    def test_stop_after_session_prevents_next(self):
        body = 'from pathlib import Path\nPath("STOP").touch()\n'
        result, state, rows = self.run_case(body, loops=0)
        self.assertEqual(result.returncode, 0)
        self.assertEqual(len(rows), 1)
        self.assertEqual(state['status'], 'owner_stopped')

    def test_live_lock_rejects_duplicate(self):
        import fcntl
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            with (root/'loop.lock').open('w') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                result = subprocess.run([sys.executable,str(RUNNER),'--run-dir',str(root)],
                                        text=True,capture_output=True,timeout=5)
                self.assertEqual(result.returncode, 0)
                self.assertIn('이미 Codex 루프', result.stdout)
                self.assertFalse((root/'runtime_state.json').exists())


if __name__ == '__main__':
    unittest.main()
