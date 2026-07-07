import importlib.util
import os
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class LlmCostControlTest(unittest.TestCase):
    def test_shared_llm_defaults_to_local_first(self):
        module = load_module("shared_llm_cost_test", ROOT / "_shared" / "llm.py")
        calls = []
        module._ollama = lambda *args, **kwargs: calls.append("ollama") or "local-ok"
        module._claude_code = lambda *args, **kwargs: calls.append("claude_code") or "sub-ok"
        module._gemini = lambda *args, **kwargs: calls.append("gemini") or "gemini-ok"

        result = module.text("hello")

        self.assertEqual(result, "local-ok")
        self.assertEqual(calls, ["ollama"])

    def test_shared_llm_can_disable_cloud_fallback(self):
        module = load_module("shared_llm_no_cloud_test", ROOT / "_shared" / "llm.py")
        calls = []
        module._ollama = lambda *args, **kwargs: calls.append("ollama") or None
        module._claude_code = lambda *args, **kwargs: calls.append("claude_code") or "sub-ok"
        module._gemini = lambda *args, **kwargs: calls.append("gemini") or "gemini-ok"

        old_value = os.environ.get("AI_TEAM_ALLOW_CLOUD_LLM")
        os.environ["AI_TEAM_ALLOW_CLOUD_LLM"] = "0"
        try:
            result = module.text("hello")
        finally:
            if old_value is None:
                os.environ.pop("AI_TEAM_ALLOW_CLOUD_LLM", None)
            else:
                os.environ["AI_TEAM_ALLOW_CLOUD_LLM"] = old_value

        self.assertIsNone(result)
        self.assertEqual(calls, ["ollama"])

    def test_direct_cloud_aliases_obey_global_cost_gate(self):
        module = load_module("shared_llm_alias_gate_test", ROOT / "_shared" / "llm.py")
        old_allow = os.environ.get("AI_TEAM_ALLOW_CLOUD_LLM")
        old_openai = os.environ.get("OPENAI_API_KEY")
        old_gemini = os.environ.get("GEMINI_API_KEY")
        os.environ["AI_TEAM_ALLOW_CLOUD_LLM"] = "0"
        os.environ["OPENAI_API_KEY"] = "test-openai-key"
        os.environ["GEMINI_API_KEY"] = "test-gemini-key"
        try:
            cloud_attempts = []
            def blocked_urlopen(*args, **kwargs):
                cloud_attempts.append(args[0])
                raise AssertionError("cloud request should be blocked")
            module.urllib.request.urlopen = blocked_urlopen
            # 유료 별칭(gpt/claude)은 제거됨 — 존재 자체가 없어야 한다(오너 지시: 유료 API 미사용)
            self.assertFalse(hasattr(module, "gpt"))
            self.assertFalse(hasattr(module, "claude"))
            # 남은 urllib 경로(gemini)는 비용 게이트를 준수해 네트워크를 때리지 않아야 한다
            self.assertIsNone(module.gemini("hello"))
            self.assertEqual(cloud_attempts, [])
        finally:
            for key, value in {
                "AI_TEAM_ALLOW_CLOUD_LLM": old_allow,
                "OPENAI_API_KEY": old_openai,
                "GEMINI_API_KEY": old_gemini,
            }.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value

    def test_youngsuk_bot_uses_unified_subscription_chain(self):
        """영숙 봇 함수호출은 통합 llm.text(구독→Gemini→로컬)를 쓰고, 유료 API 별칭·엔드포인트를
        직접 참조하지 않는다(오너 지시: 유료 API 미사용). 옛 데이브 테스트는 에이전트 제거로 폐기."""
        source = (ROOT / "skills" / "영숙_비서" / "tools" / "telegram_receiver.py").read_text(encoding="utf-8")
        self.assertIn("llm.text(", source)
        self.assertNotIn("import gpt", source)
        self.assertNotIn("api.openai.com", source)
        self.assertNotIn("api.anthropic.com", source)

    def test_petnna_ai_health_has_request_size_guard(self):
        source = (ROOT.parents[0] / "petnna" / "api" / "ai-health.js").read_text(encoding="utf-8")
        self.assertIn("AI_HEALTH_MAX_IMAGE_BASE64_CHARS", source)
        self.assertIn("imageBase64.length", source)


if __name__ == "__main__":
    unittest.main()
