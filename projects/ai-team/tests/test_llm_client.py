import importlib.util
import json
import os
import pathlib
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "_shared" / "llm.py"


def load_llm():
    spec = importlib.util.spec_from_file_location("llm_client_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class LLMClientTests(unittest.TestCase):
    def test_openai_gpt_model_is_locked_to_4o_mini(self):
        llm = load_llm()

        self.assertEqual(llm.OPENAI_GPT_MODEL, "gpt-4o-mini")

    def test_ollama_availability_helper_exists_for_agent_tools(self):
        llm = load_llm()

        self.assertTrue(callable(llm.is_available))

    def test_gpt_request_uses_only_gpt_4o_mini(self):
        llm = load_llm()
        captured = {}

        class FakeResponse:
            status = 200

            def __enter__(self):
                return self

            def __exit__(self, *args):
                return False

            def read(self):
                return json.dumps({
                    "choices": [{"message": {"content": "ok"}}],
                }).encode()

        def fake_request(url, data=None, headers=None):
            captured["payload"] = json.loads(data.decode())
            return mock.Mock()

        with mock.patch.dict(os.environ, {"OPENAI_API_KEY": "test-key"}):
            with mock.patch.object(llm.urllib.request, "Request", side_effect=fake_request):
                with mock.patch.object(llm.urllib.request, "urlopen", return_value=FakeResponse()):
                    self.assertEqual(llm.gpt("ping", max_tokens=1), "ok")

        self.assertEqual(captured["payload"]["model"], "gpt-4o-mini")


if __name__ == "__main__":
    unittest.main()
