"""임시 DB·로컬 프로세스로 실패/중단/동시접수/재시작을 검증한다. 운영 서비스는 건드리지 않는다."""
import concurrent.futures
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from orch_core import board_commands as m, agents, config


class FunctionalTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'state').mkdir()
        self.q = m.Queue(self.root / 'state/board_commands.sqlite3')

    def execute_fixture(self, source, timeout=10):
        fixture = self.root / 'fixture.py'
        fixture.write_text(source, encoding='utf-8')
        fake = SimpleNamespace(argv=lambda cwd: [sys.executable, str(fixture), '-'], env=lambda: None,
                               cfg=SimpleNamespace(timeout_sec=timeout))
        self.q.submit('시험 프로세스만 실행', 'execution')
        row = self.q.claim()
        with (self.root / 'lock').open('w') as lock, patch.object(m, 'ROOT', self.root), patch.object(m, 'STATE', self.root / 'state'), patch.object(config, 'load', return_value=SimpleNamespace(agent=lambda name: None)), patch.object(agents, 'build', return_value=fake):
            m.execute(self.q, row, lock.fileno())
        return self.q.recent()[0]

    def test_nonzero_exit_with_success_json_is_failed(self):
        r = self.execute_fixture("""import sys,json,pathlib
sys.stdin.read()
p=pathlib.Path(sys.argv[sys.argv.index('--output-last-message')+1])
p.write_text(json.dumps({'status':'DONE','summary':'성공이라고 주장','evidence':['주장']}))
sys.exit(7)
""")
        self.assertEqual(r['status'], 'FAILED')
        self.assertIn('7', r['summary'])

    def test_missing_result_needs_review(self):
        r = self.execute_fixture('import sys; sys.stdin.read()')
        self.assertEqual(r['status'], 'NEEDS_REVIEW')

    def test_malformed_result_needs_review(self):
        r = self.execute_fixture("""import sys,pathlib
sys.stdin.read()
pathlib.Path(sys.argv[sys.argv.index('--output-last-message')+1]).write_text('{broken')
""")
        self.assertEqual(r['status'], 'NEEDS_REVIEW')

    def test_blocked_result_is_preserved(self):
        r = self.execute_fixture("""import sys,json,pathlib
sys.stdin.read()
pathlib.Path(sys.argv[sys.argv.index('--output-last-message')+1]).write_text(json.dumps({'status':'BLOCKED','summary':'시험용 권한 부재','evidence':[]}))
""")
        self.assertEqual(r['status'], 'BLOCKED')
        self.assertEqual(r['summary'], '시험용 권한 부재')

    def test_timeout_terminates_process(self):
        r = self.execute_fixture('import sys,time; sys.stdin.read(); time.sleep(30)', timeout=0.2)
        self.assertEqual(r['status'], 'INTERRUPTED')
        with self.assertRaises(ProcessLookupError):
            os.kill(r['pid'], 0)

    def test_concurrent_retry_creates_only_one_command(self):
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            ids = list(pool.map(lambda _: self.q.submit('같은 명령', 'concurrent'), range(16)))
        self.assertEqual(len(set(ids)), 1)
        self.assertEqual(len(self.q.recent()), 1)

    def test_concurrent_consumers_claim_only_one(self):
        self.q.submit('한 번 실행', 'a')
        barrier = threading.Barrier(8)
        def claim(_):
            barrier.wait()
            return self.q.claim()
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            claims = list(pool.map(claim, range(8)))
        self.assertEqual(sum(x is not None for x in claims), 1)

    def test_stale_heartbeat_shows_offline(self):
        self.q.beat('테스트')
        self.assertTrue(self.q.snapshot()['worker']['online'])
        with self.q.connect() as c:
            c.execute('UPDATE worker SET heartbeat=?', (time.time()-25,))
        self.assertFalse(self.q.snapshot()['worker']['online'])

    def test_pause_resume_and_worker_restart_use_only_temp_state(self):
        # 실제 main 루프를 별도 Python 프로세스에서 실행하되 에이전트는 로컬 시험 처리기로 대체.
        source_root = Path(m.__file__).resolve().parents[1]
        boot = self.root / 'worker.py'
        boot.write_text(f'''import sys,time
from pathlib import Path
sys.path.insert(0, {str(source_root)!r})
from orch_core import board_commands as m
m.ROOT=Path({str(self.root)!r})
m.STATE=m.ROOT/'state'
def execute(q,row,fd):
    with (m.ROOT/'executions.txt').open('a') as f: f.write(str(row['id'])+'\\n')
    q.finish(row['id'],'REPORTED','임시 소비자 처리',{{'evidence':['로컬 시험']}})
m.execute=execute
m.main()
''')
        stop = self.root / 'state/STOP'
        stop.touch()
        self.q.submit('다음에 실행', 'a')
        procs = []
        def spawn():
            p = subprocess.Popen([sys.executable, str(boot)], stdout=subprocess.DEVNULL, stderr=subprocess.PIPE, text=True)
            procs.append(p)
            return p
        def wait_until(check):
            end = time.monotonic()+8
            while time.monotonic()<end:
                if check(): return
                time.sleep(0.05)
            self.fail('임시 소비자 상태 전이 시간 초과')
        try:
            first = spawn()
            wait_until(lambda: self.q.snapshot()['worker']['online'])
            self.assertEqual(self.q.recent()[0]['status'], 'QUEUED')
            self.assertFalse((self.root / 'executions.txt').exists())
            duplicate = spawn()
            self.assertEqual(duplicate.wait(timeout=3), 0)
            stop.unlink()  # 임시 STOP만 해제한다.
            wait_until(lambda: self.q.recent()[0]['status']=='REPORTED')
            first.terminate()
            first.wait(timeout=3)
            self.q.submit('재시작 뒤 실행', 'b')
            second = spawn()
            wait_until(lambda: self.q.recent()[0]['status']=='REPORTED')
            self.assertEqual((self.root/'executions.txt').read_text().splitlines(), ['1','2'])
            self.assertEqual(self.q.snapshot()['worker']['pid'], second.pid)
        finally:
            for p in procs:
                if p.poll() is None:
                    p.terminate()
                p.communicate(timeout=3)

if __name__ == '__main__':
    unittest.main()
