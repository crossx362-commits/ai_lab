"""재와별 역할 점검의 구독 모델 폴백 회귀 테스트."""

import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "skills" / "마루_게임개발" / "tools" / "game_agents.py"


def load_module():
    spec = importlib.util.spec_from_file_location("game_agents_fallback_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class GameAgentsFallbackTest(unittest.TestCase):
    def test_role_audit_uses_gpt_subscription_when_claude_is_unavailable(self):
        module = load_module()
        response = ('## 발견\n- 확인됨: 파일:1\n\n## 과제\n'
                    '[{"title":"점검 과제","detail":"내용","priority":"P2",'
                    '"track":"개발","verify":"검증","needs_owner":false}]')

        with patch.object(module, "run_claude", return_value=(False, "구독 차단")), \
             patch.object(module, "gpt_codex", return_value=response, create=True) as fallback:
            lens, report, items = module._beat("정합성", "페르소나", "현재 상태")

        self.assertEqual(lens, "정합성")
        self.assertEqual(report, response)
        self.assertEqual(items[0]["title"], "점검 과제")
        fallback.assert_called_once()


if __name__ == "__main__":
    unittest.main()
