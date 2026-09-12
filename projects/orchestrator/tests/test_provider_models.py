from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from orch_core import agents, cli, config, providers


class ProviderModelPolicyTest(unittest.TestCase):
    def setUp(self):
        self.cfg = config.load()
        self.worktree = Path('/tmp/orch-model-policy-test')

    def test_every_codex_execution_profile_passes_sol_to_cli(self):
        profiles = [self.cfg.agent(name) for name in self.cfg.ladder]
        self.assertTrue(profiles)
        self.assertTrue(all(profile.type == 'codex' for profile in profiles))
        for profile in profiles:
            argv = agents.build(profile).argv(self.worktree)
            self.assertEqual(argv[argv.index('-m') + 1], 'gpt-5.6-sol')

    def test_claude_roles_pass_opus_to_cli(self):
        for role in (self.cfg.planner, self.cfg.reviewer):
            profile = self.cfg.agent(role)
            self.assertEqual(profile.type, 'claude')
            self.assertTrue(profile.enabled)
            argv = agents.build(profile).argv(self.worktree)
            self.assertEqual(argv[argv.index('--model') + 1], 'opus')

    def test_planning_falls_back_to_sol_when_claude_is_unavailable(self):
        status = {
            'claude': providers.Status('claude', providers.AUTH_REQUIRED),
            'codex': providers.Status('codex', providers.AVAILABLE),
            'astra': providers.Status('astra', providers.AVAILABLE),
        }
        names = cli._usable(self.cfg, status, prefer=[self.cfg.planner],
                            capability=providers.PLANNING)
        self.assertEqual(names[:2], ['codex', 'astra'])
        for name in names:
            self.assertEqual(self.cfg.agent(name).model, 'gpt-5.6-sol')


if __name__ == '__main__':
    unittest.main()
