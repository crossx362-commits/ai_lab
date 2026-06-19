import importlib.util
import pathlib
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


if __name__ == "__main__":
    unittest.main()
