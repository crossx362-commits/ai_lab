"""Codex 계정 잔여 한도. 모델 실행 없이 app-server의 읽기 요청만 보낸다."""
from __future__ import annotations

import copy
import json
import math
import os
import select
import signal
import subprocess
import threading
import time


def read_codex(command=None, timeout=15):
    """설치된 CLI 프로토콜: initialize → initialized → account/rateLimits/read."""
    proc = subprocess.Popen(command or ['codex', 'app-server', '--stdio'],
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.DEVNULL, start_new_session=True)
    pending = b''
    deadline = time.monotonic() + timeout

    def send(request):
        proc.stdin.write((json.dumps(request) + '\n').encode('utf-8'))
        proc.stdin.flush()

    def request(ident, method, params=None):
        nonlocal pending
        send({'id': ident, 'method': method, 'params': params})
        while time.monotonic() < deadline:
            while b'\n' in pending:
                line, pending = pending.split(b'\n', 1)
                message = json.loads(line)
                if message.get('id') == ident:
                    if 'error' in message:
                        raise RuntimeError('계정 사용량 조회 실패')
                    return message['result']
            if not select.select([proc.stdout], [], [], max(0, deadline-time.monotonic()))[0]:
                break
            chunk = os.read(proc.stdout.fileno(), 65536)
            if not chunk:
                raise RuntimeError('사용량 조회 연결 종료')
            pending += chunk
            if len(pending) > 2_000_000:
                raise RuntimeError('사용량 응답 크기 초과')
        raise TimeoutError('사용량 조회 시간 초과')

    try:
        request(1, 'initialize', {'clientInfo': {'name': 'orchestrator_board', 'version': '1.0'}})
        send({'method': 'initialized'})
        return request(2, 'account/rateLimits/read')
    finally:
        # 이 조회가 만든 프로세스 그룹만 종료한다. 작업 실행기에는 손대지 않는다.
        try:
            os.killpg(proc.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            proc.wait(timeout=2)
        except subprocess.TimeoutExpired:
            os.killpg(proc.pid, signal.SIGKILL)
            proc.wait()
        proc.stdin.close()
        proc.stdout.close()


def number(value):
    return value if type(value) in (int, float) and math.isfinite(value) else None


def normalize(payload):
    """명시된 창만 집계한다. null은 0%나 무제한이 아니며 계정 식별자는 내보내지 않는다."""
    buckets = payload.get('rateLimitsByLimitId')
    if not isinstance(buckets, dict) or not buckets:
        legacy = payload.get('rateLimits')
        buckets = {(legacy.get('limitId') or 'codex'): legacy} if isinstance(legacy, dict) and legacy else {}
    output = []
    for ident, bucket in sorted(buckets.items(), key=lambda item: (item[0] != 'codex', item[0])):
        if not isinstance(bucket, dict):
            continue
        windows = []
        for slot in ('primary', 'secondary'):
            window = bucket.get(slot)
            if not isinstance(window, dict):
                continue
            used = number(window.get('usedPercent'))
            mins = number(window.get('windowDurationMins'))
            period = ('주간' if mins == 10080 else f'{mins/60:g}시간' if mins and mins % 60 == 0
                      else f'{mins:g}분' if mins else '기간 미확인')
            windows.append({'slot': slot, 'period': period, 'duration_mins': mins,
                            'remaining_percent': round(max(0, min(100, 100-used)), 1) if used is not None else None,
                            'resets_at': number(window.get('resetsAt'))})
        credits = bucket.get('credits')
        output.append({'id': ident, 'name': bucket.get('limitName') or ('Codex' if ident == 'codex' else ident),
                       'windows': windows,
                       'credit_balance': credits.get('balance') if isinstance(credits, dict) else None})
    reset = payload.get('rateLimitResetCredits')
    return {'buckets': output, 'reset_credits': number(reset.get('availableCount')) if isinstance(reset, dict) else None}


class QuotaCache:
    """HTTP 응답을 막지 않는 단일 조회. 화면을 보는 동안 60초마다 갱신."""
    def __init__(self, reader=read_codex, clock=time.time):
        self.reader, self.clock = reader, clock
        self.lock = threading.Lock()
        self.data = {'buckets': [], 'reset_credits': None}
        self.checked_at = None
        self.attempted_at = None
        self.refreshing = False
        self.error = ''

    def refresh(self, force=False):
        with self.lock:
            now = self.clock()
            if self.refreshing or (self.attempted_at is not None and now-self.attempted_at < (10 if force else 60)):
                return None
            self.refreshing, self.attempted_at = True, now
        thread = threading.Thread(target=self._read, daemon=True, name='board-quota')
        thread.start()
        return thread

    def _read(self):
        try:
            data = normalize(self.reader())
            if not data['buckets']:
                raise ValueError('계정 한도 미제공')
            with self.lock:
                self.data, self.checked_at, self.error = data, self.clock(), ''
        except Exception:
            # CLI 원문에는 계정/인증 정보가 포함될 수 있으므로 그대로 노출하지 않는다.
            with self.lock:
                self.error = '사용량 조회 실패 · 다음 갱신 때 다시 확인합니다'
        finally:
            with self.lock:
                self.refreshing = False

    def snapshot(self):
        with self.lock:
            result = copy.deepcopy(self.data)
            now = self.clock()
            age = max(0, now-self.checked_at) if self.checked_at is not None else None
            fresh = age is not None and age <= 120 and not self.error
            result.update(checked_at=self.checked_at, age_sec=age, fresh=fresh,
                          refreshing=self.refreshing, error=self.error,
                          source='Codex 계정 조회', refresh_interval_sec=60)
            for bucket in result['buckets']:
                for window in bucket['windows']:
                    expired = window['resets_at'] is not None and now >= window['resets_at']
                    window['previous_remaining_percent'] = window['remaining_percent']
                    if not fresh or expired:
                        window['remaining_percent'] = None
            return result
