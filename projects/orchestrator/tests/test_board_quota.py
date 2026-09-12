import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


class QuotaTest(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(importlib.util.find_spec('orch_core.board_quota'), '잔여 한도 조회 모듈이 없다')
        from orch_core import board_quota
        self.q = board_quota

    def payload(self, used=12):
        return {'rateLimits': {'primary': {'usedPercent': 99}},
                'rateLimitsByLimitId': {'codex': {
                    'primary': {'usedPercent': used, 'windowDurationMins': 10080, 'resetsAt': 2000},
                    'secondary': None, 'credits': {'balance': '0', 'unlimited': False}}},
                'accountId': 'private-account'}

    def test_prefers_buckets_and_keeps_period_without_account_id(self):
        data = self.q.normalize(self.payload())
        w = data['buckets'][0]['windows'][0]
        self.assertEqual((w['remaining_percent'], w['period']), (88, '주간'))
        self.assertEqual(w['resets_at'], 2000)
        self.assertEqual(len(data['buckets'][0]['windows']), 1)
        self.assertNotIn('private-account', json.dumps(data))

    def test_missing_usage_is_unknown_and_values_are_clamped(self):
        for used, expected in [(None, None), (150, 0), (-4, 100), (float('nan'), None), (True, None)]:
            self.assertEqual(self.q.normalize(self.payload(used))['buckets'][0]['windows'][0]['remaining_percent'], expected)

    def test_legacy_response_and_five_hour_window(self):
        data = self.q.normalize({'rateLimits': {'primary': {'usedPercent': 27, 'windowDurationMins': 300}}})
        w = data['buckets'][0]['windows'][0]
        self.assertEqual((w['remaining_percent'], w['period'], w['resets_at']), (73, '5시간', None))

    def test_empty_response_does_not_claim_unlimited(self):
        self.assertEqual(self.q.normalize({})['buckets'], [])

    def test_cache_marks_failed_and_expired_numbers_as_previous(self):
        cache = self.q.QuotaCache(reader=self.payload, clock=lambda: 1000)
        cache.refresh().join(2)
        self.assertTrue(cache.snapshot()['fresh'])
        def fail():
            raise RuntimeError('secret upstream message')
        cache.reader = fail
        cache.clock = lambda: 1061
        cache.refresh().join(2)
        data = cache.snapshot()
        self.assertFalse(data['fresh'])
        self.assertNotIn('secret', json.dumps(data))
        w = data['buckets'][0]['windows'][0]
        self.assertEqual(w['previous_remaining_percent'], 88)
        self.assertIsNone(w['remaining_percent'])

    def test_reset_passed_is_unknown_even_with_recent_success(self):
        cache = self.q.QuotaCache(reader=self.payload, clock=lambda: 1990)
        cache.refresh().join(2)
        cache.clock = lambda: 2001
        w = cache.snapshot()['buckets'][0]['windows'][0]
        self.assertIsNone(w['remaining_percent'])
        self.assertEqual(w['previous_remaining_percent'], 88)

    def test_simultaneous_refresh_starts_only_one_reader(self):
        started, release = threading.Event(), threading.Event()
        calls = []
        def read():
            calls.append(1)
            started.set()
            release.wait(2)
            return self.payload()
        cache = self.q.QuotaCache(reader=read, clock=lambda: 1000)
        thread = cache.refresh()
        try:
            self.assertTrue(started.wait(1))
            self.assertIsNone(cache.refresh(force=True))
            self.assertTrue(cache.snapshot()['refreshing'])
        finally:
            release.set()
            thread.join(2)
        self.assertEqual(calls, [1])
        self.assertIsNone(cache.refresh())

    def test_protocol_ignores_notifications_and_never_starts_a_turn(self):
        with tempfile.TemporaryDirectory() as d:
            script = Path(d)/'server.py'
            script.write_text('''import json,sys
for line in sys.stdin:
 r=json.loads(line)
 if r['method']=='initialize': print(json.dumps({'id':r['id'],'result':{}}),flush=True)
 elif r['method']=='initialized': pass
 elif r['method']=='account/rateLimits/read':
  print(json.dumps({'method':'notice','params':{}}),flush=True)
  print(json.dumps({'id':r['id'],'result':{'rateLimits':{'primary':{'usedPercent':12}}}}),flush=True)
 else: raise RuntimeError('unexpected mutation')
''', encoding='utf-8')
            result = self.q.read_codex([sys.executable, str(script)], timeout=2)
            self.assertEqual(result['rateLimits']['primary']['usedPercent'], 12)

    def test_protocol_timeout_cleans_up_its_child(self):
        with tempfile.TemporaryDirectory() as d:
            script = Path(d)/'server.py'
            script.write_text('import time\ntime.sleep(30)\n', encoding='utf-8')
            with self.assertRaises(TimeoutError):
                self.q.read_codex([sys.executable, str(script)], timeout=.1)


if __name__ == '__main__':
    unittest.main()
