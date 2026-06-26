import importlib.util
import pathlib
import sys
import types
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "skills" / "영숙_비서" / "tools" / "telegram_receiver.py"


def load_receiver():
    spec = importlib.util.spec_from_file_location("telegram_receiver_psychology_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class YoungsukPsychologyAndSearchTests(unittest.TestCase):
    def test_system_prompt_has_psychology_and_safety_rules(self):
        receiver = load_receiver()

        self.assertIn("심리학", receiver.PSYCHOLOGY_SYSTEM)
        self.assertIn("감정", receiver.PSYCHOLOGY_SYSTEM)
        self.assertIn("진단", receiver.PSYCHOLOGY_SYSTEM)
        self.assertIn("988", receiver.PSYCHOLOGY_SYSTEM)
        self.assertIn("검색", receiver.PSYCHOLOGY_SYSTEM)

    def test_search_request_uses_web_search_before_llm(self):
        receiver = load_receiver()
        queries = []

        receiver.web_search = lambda query: queries.append(query) or "검색 결과 요약"

        result = receiver.handle_message("심리학 최신 자료 검색해봐")

        self.assertEqual(queries, ["심리학 최신 자료 검색해봐"])
        self.assertEqual(result, "검색 결과 요약")

    def test_stock_price_answer_does_not_call_llm(self):
        receiver = load_receiver()
        receiver._tool_get_stock_price = lambda text: "SK하이닉스 현재가: 300,000원 ▲1,000원 (0.33%)"
        receiver._call_llm = lambda prompt, system: self.fail("stock price should not call LLM")

        result = receiver.handle_message("하이닉스 주가 알려줘")

        self.assertIn("SK하이닉스 현재가", result)
        self.assertIn("300,000원", result)

    def test_samsung_analysis_routes_to_somi_without_llm(self):
        receiver = load_receiver()
        calls = []

        receiver.dispatch_to_somi = lambda text: calls.append(text) or "삼성전자 분석 완료"
        receiver._call_llm = lambda prompt, system: self.fail("stock analysis should not call LLM")

        result = receiver.handle_message("삼전 분석해줘")

        self.assertEqual(calls, ["삼전 분석해줘"])
        self.assertIn("삼성전자 분석 완료", result)

    def test_dispatch_to_somi_passes_samsung_symbol_and_name(self):
        receiver = load_receiver()
        calls = []

        def fake_run_python(script, *args, timeout=60):
            calls.append((script.name, args, timeout))
            return "삼성전자 리포트"

        receiver._run_python = fake_run_python

        result = receiver.dispatch_to_somi("삼전 분석")

        self.assertEqual(result, "삼성전자 리포트")
        self.assertEqual(calls[0][0], "somi_kis_reporter.py")
        self.assertIn("--symbol", calls[0][1])
        self.assertIn("005930", calls[0][1])
        self.assertIn("--name", calls[0][1])
        self.assertIn("삼성전자", calls[0][1])

    def test_trading_status_answer_does_not_call_llm(self):
        receiver = load_receiver()
        receiver._tool_get_trading_status = lambda: "거래현황: 데이브 실행중"
        receiver._tool_get_agent_status = lambda: "에이전트 현황: 레오 실행중"
        receiver._call_llm = lambda prompt, system: self.fail("trading status should not call LLM")

        result = receiver.handle_message("거래 현황")

        self.assertIn("거래현황", result)
        self.assertIn("에이전트 현황", result)

    def test_llm_uses_local_before_cloud_by_default(self):
        receiver = load_receiver()
        receiver.ALLOW_CLOUD_LLM = True
        receiver.LLM_PRIMARY = "gpt"
        calls = []
        fake_llm = types.SimpleNamespace(
            ollama=lambda *args, **kwargs: calls.append("ollama") or "로컬 답변",
            gpt=lambda *args, **kwargs: calls.append("gpt") or "GPT 답변",
            gemini=lambda *args, **kwargs: calls.append("gemini") or "Gemini 답변",
        )
        original_llm = sys.modules.get("_shared.llm")
        try:
            sys.modules["_shared.llm"] = fake_llm

            result = receiver._call_llm("안녕", receiver.SYSTEM)

            self.assertEqual(result, "로컬 답변")
            self.assertEqual(calls, ["ollama"])
        finally:
            if original_llm is None:
                sys.modules.pop("_shared.llm", None)
            else:
                sys.modules["_shared.llm"] = original_llm

    def test_llm_falls_back_to_gemini_when_local_is_empty_and_cloud_is_explicit(self):
        receiver = load_receiver()
        receiver.ALLOW_CLOUD_LLM = True
        receiver.LLM_PRIMARY = "gpt"
        calls = []
        fake_llm = types.SimpleNamespace(
            ollama=lambda *args, **kwargs: calls.append("ollama") or None,
            gpt=lambda *args, **kwargs: calls.append("gpt") or None,
            gemini=lambda *args, **kwargs: calls.append("gemini") or "Gemini 답변",
        )
        original_llm = sys.modules.get("_shared.llm")
        try:
            sys.modules["_shared.llm"] = fake_llm

            result = receiver._call_llm("지피티 정밀모드로 답해줘", receiver.SYSTEM)

            self.assertEqual(result, "Gemini 답변")
            self.assertEqual(calls, ["ollama", "gpt", "gemini"])
        finally:
            if original_llm is None:
                sys.modules.pop("_shared.llm", None)
            else:
                sys.modules["_shared.llm"] = original_llm

    def test_cloud_llm_requires_explicit_mode_by_default(self):
        receiver = load_receiver()

        self.assertTrue(receiver.ALLOW_CLOUD_LLM)
        self.assertEqual(receiver.CLOUD_MODE, "explicit")
        self.assertEqual(receiver.LLM_PRIMARY, "ollama")


if __name__ == "__main__":
    unittest.main()
