import importlib.util
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

class MetricsTest(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(importlib.util.find_spec('orch_core.board_metrics'), '보드 진행률/AI 상태 집계가 없다')
        from orch_core import board_metrics
        self.m=board_metrics

    def test_progress_counts_verified_done_and_keeps_failed_in_denominator(self):
        tasks=[{'status':'DONE','verdict':'PASS','target':'game'}, {'status':'DONE','verdict':'UNKNOWN','target':'game'}, {'status':'BLOCKED','target':'game'}, {'status':'RUNNING','target':'game'}]
        p=self.m.progress(tasks)
        self.assertEqual((p['done'],p['total'],p['percent']), (1,4,25))
        self.assertEqual(p['running'],1)

    def test_empty_progress_is_unknown(self):
        self.assertIsNone(self.m.progress([])['percent'])

    def test_provider_enabled_missing_cache_is_unknown_not_off(self):
        r=self.m.ai_state({'codex':{}},{},[],now=1000)
        self.assertEqual(r[0]['power'],'UNKNOWN')

    def test_probe_error_is_unknown_not_confirmed_off(self):
        r=self.m.ai_state({'codex':{}},{'codex':{'state':'ERROR','checked_at':999}},[],now=1000)
        self.assertEqual(r[0]['power'],'UNKNOWN')

    def test_disabled_ai_is_visible_off(self):
        r=self.m.ai_state({'codex':{'enabled':False}},{},[],now=1000)
        self.assertEqual(r[0]['power'],'OFF')

    def test_stale_auth_is_not_green(self):
        r=self.m.ai_state({'codex':{}},{'codex':{'state':'AVAILABLE','checked_at':1}},[],now=1000)
        self.assertEqual(r[0]['power'],'UNKNOWN')

    def test_fresh_available_idle_is_on(self):
        r=self.m.ai_state({'codex':{'model':'gpt-5.6-sol'}},{'codex':{'state':'AVAILABLE','checked_at':999}},[],now=1000)
        self.assertEqual((r[0]['power'],r[0]['activity']),('ON','대기'))
        self.assertEqual(r[0]['model'],'gpt-5.6-sol')

    def test_cooldown_overrides_available(self):
        r=self.m.ai_state({'codex':{}},{'codex':{'state':'AVAILABLE','checked_at':999,'cooldown_until':1100}},[],now=1000)
        self.assertEqual(r[0]['power'],'OFF')

    def test_verified_active_reviewer_is_astra_not_implementer(self):
        proc={'kind':'agent','stdout_path':'task.review-astra.stdout.log','cmd':'["codex","exec"]','attempt_id':7}
        self.assertEqual(self.m.actor(proc,{7:'codex'}),('astra','리뷰'))

    def test_implementation_uses_attempt_agent(self):
        proc={'kind':'agent','stdout_path':'task.stdout.log','cmd':'["codex","exec"]','attempt_id':7}
        self.assertEqual(self.m.actor(proc,{7:'astra'}),('astra','구현'))

    def test_running_activity_survives_stale_auth(self):
        r=self.m.ai_state({'astra':{}},{},[{'ai':'astra','label':'작업 #8 리뷰'}],now=1000)
        self.assertEqual((r[0]['power'],r[0]['activity']),('ON','작업 중'))
        self.assertEqual(r[0]['jobs'],['작업 #8 리뷰'])

    def test_node_wrapped_codex_is_recognized_without_matching_unrelated_scripts(self):
        p={'cmd':'["codex","exec"]','started_at':995}
        self.assertTrue(self.m.matches_process(p,'00:05 node /opt/homebrew/bin/codex exec -C /tmp',1000))
        self.assertFalse(self.m.matches_process(p,'00:05 node /tmp/unrelated.js codex exec',1000))

    def test_reused_pid_different_process_is_rejected(self):
        p={'cmd':'["codex", "exec"]','started_at':995}
        self.assertFalse(self.m.matches_process(p,'00:05 /usr/bin/python server.py',1000))
        self.assertTrue(self.m.matches_process(p,'00:05 codex exec -C /tmp',1000))
        self.assertFalse(self.m.matches_process(p,'04:00 codex exec -C /tmp',1000))

if __name__=='__main__': unittest.main()
