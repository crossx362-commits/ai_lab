import importlib.util
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "_shared" / "llm.py"


def load_llm():
    spec = importlib.util.spec_from_file_location("llm_client_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class LLMClientTests(unittest.TestCase):
    def test_ollama_availability_helper_exists_for_agent_tools(self):
        llm = load_llm()

        self.assertTrue(callable(llm.is_available))

    def test_paid_api_symbols_are_removed(self):
        """유료 API(GPT·Claude Messages) 함수·별칭은 제거돼야 한다(오너 지시: 유료 API 미사용).
        누가 실수로 되살려 놓으면 이 회귀 테스트가 잡는다."""
        llm = load_llm()
        for name in ("gpt", "claude", "_gpt", "_claude", "OPENAI_GPT_MODEL", "ANTHROPIC_MODEL"):
            self.assertFalse(hasattr(llm, name), f"유료 심볼 {name} 이 다시 생겼다")

    def test_source_has_no_paid_endpoints(self):
        """구독/무료 경로만 남아야 한다 — 유료 엔드포인트 문자열이 소스에 없어야 한다."""
        src = SCRIPT.read_text(encoding="utf-8")
        self.assertNotIn("api.openai.com", src)
        self.assertNotIn("api.anthropic.com", src)


if __name__ == "__main__":
    unittest.main()
