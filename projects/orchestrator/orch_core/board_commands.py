"""보드의 영속 명령함과 단일 소비자. 기존 문서 명령은 자동 재생하지 않는다."""
from __future__ import annotations

import fcntl
from contextlib import contextmanager
import json
import os
import sqlite3
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'state'
LABELS = {'QUEUED': '접수', 'RUNNING': 'Codex 처리 중', 'REPORTED': '처리 보고',
          'NEEDS_REVIEW': '결과 확인 필요', 'FAILED': '실패', 'BLOCKED': '막힘',
          'INTERRUPTED': '중단 · 재실행 확인 필요'}

class Queue:
    def __init__(self, path=None):
        self.path = Path(path or STATE / 'board_commands.sqlite3')
        self.path.parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as c:
            c.executescript('''
                CREATE TABLE IF NOT EXISTS commands (
                    id INTEGER PRIMARY KEY, request_id TEXT NOT NULL UNIQUE,
                    command TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'QUEUED',
                    created REAL NOT NULL, started REAL, ended REAL,
                    summary TEXT NOT NULL DEFAULT '', report TEXT, pid INTEGER);
                CREATE TABLE IF NOT EXISTS worker (
                    id INTEGER PRIMARY KEY CHECK(id=1), heartbeat REAL, pid INTEGER,
                    detail TEXT);
            ''')

    @contextmanager
    def connect(self):
        c = sqlite3.connect(self.path, timeout=15)
        c.row_factory = sqlite3.Row
        try:
            with c:
                yield c
        finally:
            c.close()

    def submit(self, command, request_id):
        command = command.strip()
        if not command or len(command) > 20000 or not request_id or len(request_id) > 100:
            raise ValueError('명령은 1~20000자, 접수 식별자는 1~100자여야 합니다')
        with self.connect() as c:
            c.execute('BEGIN IMMEDIATE')
            old = c.execute('SELECT * FROM commands WHERE request_id=?', (request_id,)).fetchone()
            if old:
                if old['command'] != command:
                    raise ValueError('같은 접수 식별자에 다른 명령이 있습니다')
                return old['id']
            return c.execute('INSERT INTO commands(request_id,command,created) VALUES(?,?,?)',
                             (request_id, command, time.time())).lastrowid

    def recent(self, before=None):
        with self.connect() as c:
            if before is not None:
                return [dict(r) for r in c.execute('SELECT * FROM commands WHERE id<? ORDER BY id DESC LIMIT 30', (before,))]
            return [dict(r) for r in c.execute('SELECT * FROM commands ORDER BY id DESC LIMIT 30')]

    def claim(self):
        with self.connect() as c:
            c.execute('BEGIN IMMEDIATE')
            if c.execute("SELECT 1 FROM commands WHERE status='RUNNING'").fetchone():
                return None
            r = c.execute("SELECT * FROM commands WHERE status='QUEUED' ORDER BY id LIMIT 1").fetchone()
            if r is None:
                return None
            c.execute("UPDATE commands SET status='RUNNING',started=? WHERE id=?", (time.time(), r['id']))
            return dict(r)

    def finish(self, ident, status, summary, report=None):
        with self.connect() as c:
            c.execute('UPDATE commands SET status=?,summary=?,report=?,ended=? WHERE id=?',
                      (status, summary, json.dumps(report, ensure_ascii=False), time.time(), ident))

    def interrupt_running(self):
        with self.connect() as c:
            c.execute("UPDATE commands SET status='INTERRUPTED',summary='실행기 재시작: 결과 미확인, 자동 재실행 안 함',ended=? WHERE status='RUNNING'", (time.time(),))

    def beat(self, detail):
        with self.connect() as c:
            c.execute('INSERT OR REPLACE INTO worker VALUES(1,?,?,?)', (time.time(), os.getpid(), detail))

    def snapshot(self):
        with self.connect() as c:
            r = c.execute('SELECT * FROM worker WHERE id=1').fetchone()
            active = [dict(x) for x in c.execute("SELECT * FROM commands WHERE status='RUNNING'")]
            counts = {x['status']:x['n'] for x in c.execute('SELECT status,COUNT(*) n FROM commands GROUP BY status')}
        w = dict(r) if r else {}
        w['online'] = bool(w and time.time() - w['heartbeat'] < 20)
        return {'items': self.recent(), 'active': active, 'counts': counts, 'worker': w, 'labels': LABELS}


def assess_report(report, exit_code):
    if exit_code != 0:
        return 'FAILED', f'실행기 종료코드 {exit_code} — 로그 확인 필요'
    if not isinstance(report, dict):
        return 'NEEDS_REVIEW', '구조화된 결과 보고 없음'
    summary = report.get('summary')
    if not isinstance(summary, str) or not summary.strip():
        return 'NEEDS_REVIEW', '결과 요약 없음'
    if report.get('status') == 'BLOCKED':
        return 'BLOCKED', summary
    evidence = report.get('evidence')
    if report.get('status') != 'DONE' or not isinstance(evidence, list) or not evidence or not all(isinstance(x, str) and x.strip() for x in evidence):
        return 'NEEDS_REVIEW', summary
    # 에이전트의 성공 자기보고를 독립 검증 완료로 둔갑시키지 않는다.
    return 'REPORTED', summary


SCHEMA = {'type': 'object', 'properties': {
    'status': {'type': 'string', 'enum': ['DONE', 'BLOCKED']},
    'summary': {'type': 'string'},
    'evidence': {'type': 'array', 'items': {'type': 'string'}}},
    'required': ['status', 'summary', 'evidence'], 'additionalProperties': False}


def prompt_for(row):
    return f'''너는 오케스트레이터 보드의 명령 담당 Codex다. 비대화형 실행이다.
오너가 보드에 직접 접수한 아래 명령 한 건을 실제 처리하고 검증 근거를 보고하라.
작업 전 /Users/junholee/ai_lab/AGENTS.md 및 DIRECTIVES.md와 관련 프로젝트 지침을 따른다.
현재 작업 디렉터리는 {ROOT}이다. BOARD.md의 기존 오너 지시는 맥락으로 확인하되 과거 명령을 다시 실행하지 마라. 기존 변경과 다른 실행 중 작업을 보존하라.
게임/Blender 구현은 config.json의 적합한 target과 기존 orch_core.cli plan/run-plan/run을 사용하라.
진행 중 작업을 중복 생성하지 말고, 일반 운영/조회/보드 개선 명령은 해당 범위에서 직접 처리한다.
작업 대상이 결정 불가능하면 임의 프로젝트에 적용하지 말고 BLOCKED와 필요한 정보를 보고하라.
명령함 DB·접수 상태·실행 서비스는 수정하지 말고, 보드의 다른 명령을 재실행하지 마라.
종료코드 0이나 파일 생성만으로 완료라 하지 말고 실제 테스트/조회/산출물을 검증하라.
검증하지 못했거나 요청을 전부 처리하지 못하면 BLOCKED로 보고하라.
증거에는 실행한 검증과 실제 결과, 관련 작업 번호/파일 경로를 넣는다. 외부 메시지 발송은 별도 명시 지시가 있을 때만 한다.
[오너 명령 #{row['id']} 시작]\n{row['command']}\n[오너 명령 끝]
'''


def execute(q, row, lock_fd):
    from . import agents, config, proc
    cfg = config.load()
    agent = agents.build(cfg.agent('codex'))
    prefix = ROOT / 'logs' / f"command-{row['id']:06d}"
    prefix.parent.mkdir(exist_ok=True)
    schema = Path(str(prefix) + '.schema.json')
    report_path = Path(str(prefix) + '.result.json')
    schema.write_text(json.dumps(SCHEMA), encoding='utf-8')
    report_path.unlink(missing_ok=True)
    cmd = agent.argv(ROOT)
    cmd[-1:-1] = ['-c', 'sandbox_workspace_write.network_access=true', '--output-schema', str(schema), '--output-last-message', str(report_path)]
    with Path(str(prefix) + '.stdout.log').open('w') as out, Path(str(prefix) + '.stderr.log').open('w') as err:
        p = subprocess.Popen(cmd, cwd=ROOT, stdin=subprocess.PIPE, stdout=out, stderr=err,
                             text=True, encoding='utf-8', env=agent.env(), start_new_session=True,
                             pass_fds=(lock_fd,))
        try:
            with q.connect() as c:
                c.execute('UPDATE commands SET pid=? WHERE id=?', (p.pid, row['id']))
            deadline = time.monotonic() + agent.cfg.timeout_sec
            first = True
            while True:
                q.beat(f"명령 #{row['id']} 처리 중")
                if (STATE / 'STOP').exists() or time.monotonic() >= deadline:
                    proc._kill_group(p.pid)
                    p.wait(timeout=10)
                    q.finish(row['id'], 'INTERRUPTED', '세우기 요청 또는 실행 시간 초과 — 자동 재실행 안 함')
                    return
                try:
                    p.communicate(input=prompt_for(row) if first else None, timeout=2)
                    break
                except subprocess.TimeoutExpired:
                    first = False
            try:
                report = json.loads(report_path.read_text(encoding='utf-8'))
            except (OSError, ValueError):
                report = None
            status, summary = assess_report(report, p.returncode)
            q.finish(row['id'], status, summary, report)
        finally:
            if p.stdin and not p.stdin.closed:
                p.stdin.close()
            if p.poll() is None:
                proc._kill_group(p.pid)
                p.wait(timeout=10)



def main():
    sys.stdout.reconfigure(encoding='utf-8')
    q = Queue()
    with (STATE / 'board_commands.lock').open('a') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return  # 살아 있는 소비자 또는 그 자식이 잠금을 갖고 있다.
        q.interrupt_running()
        while True:
            q.beat('세워짐' if (STATE / 'STOP').exists() else '명령 대기')
            if not (STATE / 'STOP').exists():
                row = q.claim()
                if row:
                    try:
                        execute(q, row, lock.fileno())
                    except Exception as e:
                        q.finish(row['id'], 'FAILED', f'{type(e).__name__}: {e}')
                    continue
            time.sleep(2)

if __name__ == '__main__':
    main()
