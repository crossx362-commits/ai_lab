"""실제 자식 프로세스로 Codex 루프 수명·정지·실패 제한을 검사한다."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import importlib.util

spec = importlib.util.spec_from_file_location('codex_loop', Path(__file__).resolve().parents[1]/'loop/codex_loop.py')
loop = importlib.util.module_from_spec(spec)
spec.loader.exec_module(loop)

RUNNER = Path(__file__).resolve().parents[1] / 'loop/codex_loop.py'


class LoopTests(unittest.TestCase):
    def run_case(self, body, *, loops=1, timeout=3, stop=False):
        self.assertTrue(RUNNER.exists(), '그록 방식의 독립 Codex 프로세스 루프가 아직 없습니다')
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            subprocess.run(['git','init','-q',str(root)],check=True)
            subprocess.run(['git','-c','user.name=Loop Test','-c','user.email=loop@example.invalid',
                            'commit','--allow-empty','-qm','baseline'],cwd=root,check=True)
            fake = root / 'fake-codex'
            fake.write_text('#!/usr/bin/env python3\n' + body)
            fake.chmod(0o755)
            prompt = root / 'prompt.md'; prompt.write_text('한 바퀴만 실행')
            board = root/'board.json'
            board.write_text(json.dumps({'cards':[{'id':str(i),'owner':'codex','status':'대기'} for i in range(3)]}))
            if stop: (root / 'STOP').touch()
            cmd = [sys.executable, str(RUNNER), '--cli', str(fake), '--worktree', str(root),
                   '--prompt', str(prompt), '--run-dir', str(root / 'run'), '--stop-file', str(root/'STOP'),
                   '--pause', '0', '--timeout', str(timeout), '--max-loops', str(loops), '--board', str(board)]
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
        self.assertEqual(rows[0]['result'], 'no_progress')
        self.assertNotEqual(rows[0]['card_id'], rows[1]['card_id'])

    def test_progress_does_not_monopolize_first_card(self):
        body = 'from pathlib import Path\np=Path("projects/ulon/art/progress.txt")\np.parent.mkdir(parents=True,exist_ok=True)\np.write_text(p.read_text()+"x" if p.exists() else "x")\n'
        result, state, rows = self.run_case(body, loops=4)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual([r['card_id'] for r in rows], ['0','1','2','0'])
        self.assertTrue(all(r['content_changed'] for r in rows))

    def test_oldest_execution_wins_over_board_and_status_order(self):
        cards = [{'id':'world','owner':'codex','status':'진행 중'},
                 {'id':'hud','owner':'codex','status':'검증 중'}]
        self.assertEqual(loop.select_card(cards, {}, {'world':82,'hud':75})['id'], 'hud')

    def test_history_restores_order_and_ignores_partial_line(self):
        with tempfile.TemporaryDirectory() as folder:
            p = Path(folder)/'history.jsonl'
            p.write_text('{"card_id":"hud"}\n{"card_id":"world"}\n{"card_id":')
            self.assertEqual(loop.execution_order(p), {'hud':1,'world':2})
            cards=[{'id':x,'owner':'codex','status':'검증 중'} for x in ['world','hud']]
            self.assertEqual(loop.select_card(cards,{},loop.execution_order(p))['id'],'hud')

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

    def test_selection_skips_blocked_foreign_and_unchanged_deferred(self):
        self.assertTrue(hasattr(loop,'select_card'), '실행 가능한 담당 카드 선택이 없습니다')
        cards=[{'id':'grok','owner':'grok','model':'gpt','status':'대기'},
               {'id':'blocked','owner':'codex','status':'막힘'},
               {'id':'stale','owner':'codex','status':'검증 중'},
               {'id':'hud','owner':'codex','status':'대기'}]
        deferred={'stale':loop.card_signature(cards[2])}
        self.assertEqual(loop.select_card(cards,deferred)['id'],'hud')
        self.assertIsNone(loop.select_card(cards[:3],deferred))
        cards[2]['resume_token']='조건이 바뀜'
        self.assertEqual(loop.select_card(cards[:3],deferred)['id'],'stale')

    def test_git_error_is_not_no_progress(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(subprocess.CalledProcessError):
                loop.content_fingerprint(Path(folder))

    def test_resume_token_changed_during_session_is_not_deferred(self):
        body='import json\nfrom pathlib import Path\np=Path("board.json")\nb=json.loads(p.read_text())\nb["cards"][0]["resume_token"]="new"\np.write_text(json.dumps(b))\n'
        result,state,rows=self.run_case(body,loops=2)
        self.assertEqual(result.returncode,0,result.stderr)
        self.assertNotIn(rows[0]['card_id'],state['deferred_cards'])
        self.assertNotEqual(rows[0]['card_id'],rows[1]['card_id'])

    def test_no_ready_card_does_not_start_cli(self):
        import time
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder); board=root/'board.json'
            board.write_text(json.dumps({'cards':[{'id':'x','owner':'codex','status':'막힘'}]}))
            proc=subprocess.Popen([sys.executable,str(RUNNER),'--run-dir',str(root/'run'),
                                   '--board',str(board),'--stop-file',str(root/'STOP'),
                                   '--cli','/does-not-exist'])
            try:
                state_path=root/'run/runtime_state.json'
                end=time.monotonic()+5
                while time.monotonic()<end:
                    if state_path.exists() and json.loads(state_path.read_text()).get('status')=='waiting_work': break
                    time.sleep(.05)
                self.assertEqual(json.loads(state_path.read_text())['status'],'waiting_work')
                self.assertFalse(list((root/'run').glob('loop_*.jsonl')))
                (root/'STOP').touch()
                self.assertEqual(proc.wait(timeout=3),0)
            finally:
                if proc.poll() is None:proc.terminate();proc.wait(timeout=3)


if __name__ == '__main__':
    unittest.main()
