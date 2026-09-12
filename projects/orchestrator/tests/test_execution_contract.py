"""인수 점검: 실제 로컬 CLI + 임시 Git/DB로 실패를 성공으로 바꾸지 않는지 확인."""
from contextlib import ExitStack, redirect_stdout
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from orch_core import cli, config, db, gitwt, memory, providers, safety, unityrun, planner, review


class ExecutionContractTest(unittest.TestCase):
    def setUp(self):
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.root = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.state = self.root/'state'
        self.state.mkdir()
        self.repo = self.root/'repo'
        self.repo.mkdir()
        (self.repo/'.gitignore').write_text('.orch/\n')
        (self.repo/'check.py').write_text('# 검증 장치\n')
        for args in [('init', '-q'), ('config', 'user.name', '검증'), ('config', 'user.email', 'test@localhost'),
                     ('add', '.'), ('commit', '-qm', '초기 검증 저장소')]:
            subprocess.run(['git', '-C', str(self.repo), *args], check=True, capture_output=True)
        for obj, key, value in [(config, 'STATE_DIR', self.state), (config, 'LOG_DIR', self.root/'logs'),
                                (db, 'DB_PATH', self.state/'test.sqlite3'),
                                (providers, 'STORE', self.state/'providers.json'),
                                (safety, 'STATE_DIR', self.state), (safety, 'STOP_FILE', self.state/'STOP')]:
            self.stack.enter_context(patch.object(obj, key, value))
        self.stack.enter_context(patch.dict('os.environ', {'ORCH_NO_CLOUD': '1'}))
        self.stack.enter_context(patch.object(memory, 'sample', return_value=memory.Snapshot(1, 80, 0, 0, 0, 0)))
        self.stack.enter_context(redirect_stdout(io.StringIO()))
        original = db.connect
        def connect(*a, **kw):
            conn = original(*a, **kw)
            self.stack.callback(conn.close)
            return conn
        self.stack.enter_context(patch.object(db, 'connect', side_effect=connect))
        self.conn = db.connect()

    def configure(self, implementation='from pathlib import Path; Path("code.py").write_text("value=1\\n")', review_code=None, plan_code=None):
        def agent(code):
            return {'type': 'script', 'bin': sys.executable, 'args': ['-c', code], 'timeout_sec': 5,
                    'capabilities': ['CODING', 'BLENDER', 'REVIEW', 'PLANNING']}
        raw = {'default_target': 'test', 'max_attempts': 1, 'ladder': ['builder'], 'research_agent': None,
               'planner': 'planner', 'reviewer': 'reviewer' if review_code is not None else None,
               'log_summarizer': {'enabled': False}, 'agents': {'builder': agent(implementation)},
               'targets': {'test': {'kind': 'blender', 'repo': str(self.repo), 'blender_bin': sys.executable,
                                    'blender_check': 'check.py', 'allowed_write_globs': ['*.py'],
                                    'protected_globs': ['check.py'], 'test_guard_globs': []}}}
        if review_code is not None:
            raw['agents']['reviewer'] = agent(review_code)
        if plan_code is not None:
            raw['agents']['planner'] = agent(plan_code)
        path = self.root/'config.json'
        path.write_text(json.dumps(raw))
        cfg = config.load(path)
        self.stack.enter_context(patch.object(config, 'load', return_value=cfg))
        return cfg

    def gate_pass(self):
        return self.stack.enter_context(patch.object(cli.blenderrun, 'check', return_value=
            unityrun.UnityResult('PASS', '시험 게이트 통과', 0, [], self.root/'gate.log', {}, .01)))

    def test_failed_implementation_cannot_be_done_even_if_file_was_written(self):
        cfg = self.configure('from pathlib import Path; Path("code.py").write_text("value=1\\n"); raise SystemExit(7)')
        gate = self.gate_pass()
        rc = cli.execute_goal(cfg, cfg.target(), goal='실패 명령 판정')
        task = db.list_tasks(self.conn)[0]
        self.assertNotEqual(rc, 0)
        self.assertNotEqual(task['status'], 'DONE')
        gate.assert_not_called()
        self.assertTrue((Path(task['worktree'])/'code.py').is_file())

    def test_failed_reviewer_cannot_approve_with_leftover_json(self):
        cfg = self.configure(review_code='from pathlib import Path; Path("review.json").write_text(\'{"verdict":"approve","reasons":["ok"]}\'); raise SystemExit(7)')
        result = cli.run_reviewer(cfg, self.conn, task_id=1, attempt_id=1, prefix=self.root/'review',
                                  goal='검토', done_criteria='확인', gates='시험', diff='diff', implementer='builder')
        self.assertEqual(result.verdict, 'UNKNOWN')

    def test_unverified_review_does_not_return_success(self):
        cfg = self.configure(review_code='pass')
        self.gate_pass()
        rc = cli.execute_goal(cfg, cfg.target(), goal='리뷰 누락')
        self.assertEqual(db.list_tasks(self.conn)[0]['status'], 'REVIEW')
        self.assertNotEqual(rc, 0)

    def test_failed_planner_does_not_publish_plan_file(self):
        cfg = self.configure(plan_code='from pathlib import Path; Path("plan.json").write_text(\'{"tasks":[{"key":"T1","goal":"작업"}]}\'); raise SystemExit(7)')
        rc = cli.cmd_plan(SimpleNamespace(target=None, agent=None, goal='계획 실패'))
        self.assertNotEqual(rc, 0)
        self.assertEqual(db.list_plans(self.conn)[0]['status'], 'BLOCKED')
        self.assertEqual(db.list_tasks(self.conn), [])

    def test_planner_uses_available_role_fallback(self):
        code = 'from pathlib import Path; Path("plan.json").write_text(\'{"tasks":[{"key":"T1","goal":"작업"}]}\')'
        cfg = self.configure(plan_code=code)
        cfg.raw['agents']['preferred'] = {
            'type': 'script', 'bin': sys.executable, 'args': ['-c', code], 'enabled': False,
            'capabilities': ['PLANNING']}
        cfg.raw['agents']['builder']['capabilities'] = ['CODING', 'BLENDER']
        cfg.raw['agents']['planner']['capabilities'] = ['PLANNING']
        cfg.planner = 'preferred'
        rc = cli.cmd_plan(SimpleNamespace(target=None, agent=None, goal='대체 계획자'))
        self.assertEqual(rc, 0)
        self.assertEqual(db.list_plans(self.conn)[0]['agent'], 'planner')

    def test_plan_stays_blocked_when_review_has_not_passed(self):
        cfg = self.configure(review_code='pass')
        self.gate_pass()
        pid = db.create_plan(self.conn, '계획 검토', 'planner', str(self.root))
        db.create_task(self.conn, '첫 작업', 'test', 'builder', None, status='BACKLOG', plan_id=pid, plan_key='T1')
        rc = cli.cmd_run_plan(SimpleNamespace(target=None, plan=pid, max_attempts=1, keep_going=True, wait_for_provider=0))
        self.assertNotEqual(rc, 0)
        self.assertEqual(db.get_plan(self.conn, pid)['status'], 'BLOCKED')

    def test_non_object_json_is_unknown_without_crashing(self):
        path = self.root/'response.json'
        for text in ['[]', 'null', '7']:
            path.write_text(text)
            self.assertFalse(planner.parse(path).ok)
            self.assertEqual(review.parse(path).verdict, 'UNKNOWN')


if __name__ == '__main__':
    unittest.main()
