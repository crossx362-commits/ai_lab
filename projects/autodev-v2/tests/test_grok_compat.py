import importlib.util
import json
import os
import tempfile
import time
from pathlib import Path
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("grok_compat_test", ROOT / "grok_compat.py")
GC = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GC)


class GrokCompatTests(unittest.TestCase):
    def test_detects_real_quota_error(self):
        text = 'API error (status 402 Payment Required): Grok Build usage balance exhausted'
        self.assertTrue(GC.looks_like_quota_error(text))
        self.assertFalse(GC.looks_like_quota_error('500 internal server error'))

    def test_optional_flags_are_dropped_only_when_unsupported(self):
        args = ['--single', 'x', '--no-plan', '--no-memory', '--cwd', '/tmp']
        out, dropped = GC.filter_args(args, 'Usage --single --cwd --no-plan')
        self.assertIn('--no-plan', out)
        self.assertNotIn('--no-memory', out)
        self.assertEqual(dropped, ['--no-memory'])

    def test_quota_cooldown_blocks_repeated_real_calls(self):
        with tempfile.TemporaryDirectory() as td:
            state = Path(td) / 'quota.json'
            with mock.patch.dict(os.environ, {
                'AUTODEV_GROK_QUOTA_STATE': str(state),
                'AUTODEV_GROK_QUOTA_COOLDOWN_SECONDS': '3600',
            }, clear=False):
                GC.mark_quota_exhausted('402 usage balance exhausted')
                self.assertTrue(GC.quota_cooldown_active())
                data = json.loads(state.read_text(encoding='utf-8'))
                data['detected_at'] = time.time() - 7200
                state.write_text(json.dumps(data), encoding='utf-8')
                self.assertFalse(GC.quota_cooldown_active())

    def test_non_402_does_not_trigger_quota_guard(self):
        self.assertFalse(GC.looks_like_quota_error('429 rate limit exceeded'))
        self.assertFalse(GC.looks_like_quota_error('402 unrelated response'))


if __name__ == '__main__':
    unittest.main()
