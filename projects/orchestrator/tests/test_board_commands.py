import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

class CommandsTest(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(importlib.util.find_spec('orch_core.board_commands'), '영속 명령 대기열이 없다')
        from orch_core import board_commands
        self.m = board_commands
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.q = self.m.Queue(Path(self.tmp.name) / 'commands.sqlite3')

    def test_durable_idempotent_submission(self):
        a = self.q.submit('한글\n명령', 'request-a')
        self.assertEqual(a, self.q.submit('한글\n명령', 'request-a'))
        q2 = self.m.Queue(self.q.path)
        self.assertEqual(q2.recent()[0]['command'], '한글\n명령')
        with self.assertRaises(ValueError):
            q2.submit('다른 명령', 'request-a')

    def test_history_pagination_and_active_command_survive_newer_entries(self):
        self.q.submit('처리 중인 첫 명령','first')
        self.q.claim()
        for n in range(35):
            self.q.submit(f'대기 {n}',f'queued-{n}')
        snap = self.q.snapshot()
        self.assertEqual(len(snap['items']),30)
        self.assertEqual(snap['active'][0]['id'],1)
        self.assertEqual(snap['counts']['QUEUED'],35)
        older = self.q.recent(before=snap['items'][-1]['id'])
        self.assertEqual([r['id'] for r in older],[6,5,4,3,2,1])

    def test_atomic_claim(self):
        self.q.submit('확인', 'a')
        other = self.m.Queue(self.q.path)
        self.assertIsNotNone(self.q.claim())
        self.assertIsNone(other.claim())

    def test_empty_rejected(self):
        with self.assertRaises(ValueError):
            self.q.submit('  ', 'a')

    def test_report_without_evidence_not_complete(self):
        status, _ = self.m.assess_report({'status':'DONE','summary':'완료','evidence':[]}, 0)
        self.assertEqual(status, 'NEEDS_REVIEW')

    def test_failed_process_never_complete(self):
        status, _ = self.m.assess_report({'status':'DONE','summary':'완료','evidence':['검증']}, 1)
        self.assertEqual(status, 'FAILED')

    def test_agent_report_is_distinct_from_verified_completion(self):
        status, _ = self.m.assess_report({'status':'DONE','summary':'완료','evidence':['검증']}, 0)
        self.assertEqual(status, 'REPORTED')

    def test_interrupted_not_requeued(self):
        self.q.submit('수정', 'a')
        self.q.claim()
        self.q.interrupt_running()
        self.assertEqual(self.q.recent()[0]['status'], 'INTERRUPTED')
        self.assertIsNone(self.q.claim())


class HttpTest(unittest.TestCase):
    def setUp(self):
        import threading
        from http.server import ThreadingHTTPServer
        from unittest.mock import patch
        from orch_core.board_commands import Queue
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.q = Queue(Path(self.tmp.name) / 'q.db')
        spec = importlib.util.spec_from_file_location('test_board_http', Path(__file__).resolve().parents[1] / 'tools/board.py')
        self.board = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.board)
        patcher = patch.object(self.board, 'Queue', lambda: self.q)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), self.board.Handler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.addCleanup(self.server.server_close)
        self.addCleanup(self.server.shutdown)

    def post(self, data, origin=None):
        import http.client
        import json
        conn = http.client.HTTPConnection('127.0.0.1', self.server.server_port)
        headers = {'Content-Type': 'application/json'}
        if origin:
            headers['Origin'] = origin
        conn.request('POST', '/command', json.dumps(data), headers)
        res = conn.getresponse()
        result = res.status, json.loads(res.read())
        conn.close()
        return result

    def test_http_ack_is_durable_and_retry_is_same_id(self):
        body = {'command': '연결 점검', 'request_id': 'http-a'}
        status, r = self.post(body)
        self.assertEqual(status, 202)
        self.assertEqual(self.post(body)[1]['id'], r['id'])
        self.assertEqual(self.q.claim()['command'], '연결 점검')

    def test_cross_site_command_rejected(self):
        self.assertEqual(self.post({'command':'실행','request_id':'b'}, 'https://untrusted.example')[0], 400)
        self.assertEqual(self.q.recent(), [])

    def test_bad_json_shape_rejected(self):
        self.assertEqual(self.post({'command': []})[0], 400)
        self.assertEqual(self.q.recent(), [])

class ExecutionTest(unittest.TestCase):
    def test_real_subprocess_receives_full_prompt_and_report_persists(self):
        import json
        from types import SimpleNamespace
        from unittest.mock import patch
        from orch_core import board_commands as m, agents, config
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / 'state').mkdir()
            fixture = root / 'fixture.py'
            fixture.write_text('''import json,sys,pathlib
prompt=sys.stdin.read()
pathlib.Path('received.txt').write_text(prompt)
out=sys.argv[sys.argv.index('--output-last-message')+1]
pathlib.Path(out).write_text(json.dumps({'status':'DONE','summary':'접수 검증','evidence':['입력 전달 확인']}))
''')
            fake = SimpleNamespace(argv=lambda cwd: [sys.executable, str(fixture), '-'], env=lambda: None, cfg=SimpleNamespace(timeout_sec=10))
            q = m.Queue(root / 'q.db')
            q.submit('첫줄\n두번째 한글 $() `명령`', 'fixture')
            row = q.claim()
            with (root / 'lock').open('w') as lock, patch.object(m, 'ROOT', root), patch.object(m, 'STATE', root / 'state'), patch.object(config, 'load', return_value=SimpleNamespace(agent=lambda name: None)), patch.object(agents, 'build', return_value=fake):
                m.execute(q, row, lock.fileno())
            self.assertIn('첫줄\n두번째 한글 $() `명령`', (root / 'received.txt').read_text())
            self.assertEqual(q.recent()[0]['status'], 'REPORTED')
            self.assertEqual(json.loads(q.recent()[0]['report'])['summary'], '접수 검증')

class StopTest(unittest.TestCase):
    def test_stop_terminates_child_without_success_report(self):
        import os
        from types import SimpleNamespace
        from unittest.mock import patch
        from orch_core import board_commands as m, agents, config
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / 'state').mkdir()
            (root / 'state/STOP').touch()
            q = m.Queue(root / 'q.db')
            q.submit('중단 시험', 'stop')
            row = q.claim()
            fake = SimpleNamespace(argv=lambda cwd: [sys.executable, '-c', 'import time;time.sleep(30)', '-'], env=lambda: None, cfg=SimpleNamespace(timeout_sec=10))
            with (root / 'lock').open('w') as lock, patch.object(m, 'ROOT', root), patch.object(m, 'STATE', root / 'state'), patch.object(config, 'load', return_value=SimpleNamespace(agent=lambda name: None)), patch.object(agents, 'build', return_value=fake):
                m.execute(q, row, lock.fileno())
            r = q.recent()[0]
            self.assertEqual(r['status'], 'INTERRUPTED')
            with self.assertRaises(ProcessLookupError):
                os.kill(r['pid'], 0)

if __name__ == '__main__':
    unittest.main()
