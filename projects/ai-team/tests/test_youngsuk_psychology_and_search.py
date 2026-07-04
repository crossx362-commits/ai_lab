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
    # LLM 의도 분류기 우회 — 라우팅만 검증(크레딧 429 등 환경 무관). 짧은 메시지가
    # _classify_intent(LLM)로 새는 것을 막아 순수 규칙 라우팅을 시험한다.
    module._classify_intent = lambda text, has_order, has_signals: None
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
        # 종목/투자 툴은 소미 모듈(somi_bot_tools) 소유 — 게이트웨이는 위임만 한다.
        receiver.somi.get_stock_price = lambda text: "SK하이닉스 현재가: 300,000원 ▲1,000원 (0.33%)"
        receiver._call_llm = lambda prompt, system: self.fail("stock price should not call LLM")

        result = receiver.handle_message("하이닉스 주가 알려줘")

        self.assertIn("SK하이닉스 현재가", result)
        self.assertIn("300,000원", result)

    def test_samsung_analysis_routes_to_somi_without_llm(self):
        receiver = load_receiver()
        calls = []

        receiver.somi.dispatch_to_somi = lambda text: calls.append(text) or "삼성전자 분석 완료"
        receiver._call_llm = lambda prompt, system: self.fail("stock analysis should not call LLM")

        result = receiver.handle_message("삼전 분석해줘")

        self.assertEqual(calls, ["삼전 분석해줘"])
        self.assertIn("삼성전자 분석 완료", result)

    def test_dispatch_to_somi_passes_samsung_symbol_and_name(self):
        # 위임 대상 자체는 소미 모듈을 직접 시험한다(게이트웨이 우회).
        sys.path.insert(0, str(ROOT / "skills" / "영숙_비서" / "tools"))
        sys.path.insert(0, str(ROOT / "skills" / "소미_분석가" / "tools"))
        import somi_bot_tools
        calls = []

        def fake_run_python(script, *args, timeout=60):
            calls.append((script.name, args, timeout))
            return "삼성전자 리포트"

        original = somi_bot_tools.bc.run_python
        somi_bot_tools.bc.run_python = fake_run_python
        try:
            result = somi_bot_tools.dispatch_to_somi("삼전 분석")
        finally:
            somi_bot_tools.bc.run_python = original

        self.assertEqual(result, "삼성전자 리포트")
        self.assertEqual(calls[0][0], "somi_kis_reporter.py")
        self.assertIn("--symbol", calls[0][1])
        self.assertIn("005930", calls[0][1])
        self.assertIn("--name", calls[0][1])
        self.assertIn("삼성전자", calls[0][1])

    def test_trading_status_answer_does_not_call_llm(self):
        receiver = load_receiver()
        receiver.somi.get_trading_status = lambda is_live=False: "거래현황: 보유 2종목 +3.1%"
        receiver._call_llm = lambda prompt, system: self.fail("trading status should not call LLM")

        result = receiver.handle_message("거래 현황")

        self.assertIn("거래현황", result)

    def test_call_llm_delegates_to_llm_text_cloud_first(self):
        # _call_llm은 llm.text(lm_first=False)에 위임 — GPT→Gemini→클로드→로컬 체인 담당(2026-07-03).
        receiver = load_receiver()
        seen = {}

        def fake_text(prompt, system=None, max_tokens=None, temperature=None, lm_first=None):
            seen["prompt"] = prompt
            seen["lm_first"] = lm_first
            return "통합 LLM 답변"

        fake_llm = types.SimpleNamespace(text=fake_text)
        original_llm = sys.modules.get("_shared.llm")
        try:
            sys.modules["_shared.llm"] = fake_llm
            result = receiver._call_llm("안녕", receiver.SYSTEM)
            self.assertEqual(result, "통합 LLM 답변")
            self.assertEqual(seen["prompt"], "안녕")
            self.assertFalse(seen["lm_first"])  # 클라우드 우선
        finally:
            if original_llm is None:
                sys.modules.pop("_shared.llm", None)
            else:
                sys.modules["_shared.llm"] = original_llm

    def test_registry_merges_all_domain_tools(self):
        # 툴 소유권 이관 후에도 17개 LLM 함수가 게이트웨이에 병합되는지.
        receiver = load_receiver()
        self.assertEqual(len(receiver.TOOLS), 17)
        self.assertEqual(len(receiver.AVAILABLE_FUNCTIONS), 17)
        for name in ("get_agent_status", "dispatch_to_somi", "get_weather", "invest_scout"):
            self.assertIn(name, receiver.AVAILABLE_FUNCTIONS)


if __name__ == "__main__":
    unittest.main()
