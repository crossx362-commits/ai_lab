"""Ollama 클라우드 모델 제외 회귀 테스트 — 2026-08-06.

`ollama list`에 `deepseek-v4-flash:0731-cloud`가 등록돼 있으면 `_pick_ollama`가
task="coding"에서 이름의 'deepseek' 때문에 이걸 1순위로 집는데, 인증이 없어 매번
403 Forbidden을 맞고 gemma4:12b로 폴백했다(미오·나무 로그에서 사이클마다 관측).

llm.py의 Ollama 티어는 '무료·로컬 최후선'이 존재 이유다 — 실제로는 원격에서 도는
클라우드 태그 모델을 이 티어에 두면 매 호출이 실패 왕복 1회를 낭비한다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

spec = importlib.util.spec_from_file_location(
    "llm_under_test", AI_TEAM_ROOT / "_shared" / "llm.py")
llm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(llm)


class FakeResponse:
    """urlopen 컨텍스트 매니저 흉내 — /v1/models 응답만 돌려준다."""

    def __init__(self, payload: bytes):
        self._payload = payload

    def read(self):
        return self._payload

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False


LIVE_LISTING = b"""{"data": [
    {"id": "deepseek-v4-flash:0731-cloud"},
    {"id": "gpt-oss:120b-cloud"},
    {"id": "gemma4:12b"},
    {"id": "nomic-embed-text:latest"}
]}"""


def list_with(env=None):
    with mock.patch.object(llm.urllib.request, "urlopen",
                           return_value=FakeResponse(LIVE_LISTING)), \
            mock.patch.dict(llm.os.environ, env or {}, clear=False):
        return llm._list_ollama()


class CloudTagDetectionTests(unittest.TestCase):
    def test_detects_cloud_variants(self):
        for name in ("deepseek-v4-flash:0731-cloud", "gpt-oss:120b-cloud", "foo:cloud"):
            self.assertTrue(llm._is_ollama_cloud(name), name)

    def test_local_models_are_not_cloud(self):
        for name in ("gemma4:12b", "deepseek-coder:latest", "qwen2.5:7b", "gemma4"):
            self.assertFalse(llm._is_ollama_cloud(name), name)


class ListOllamaTests(unittest.TestCase):
    def test_cloud_models_excluded_by_default(self):
        models = list_with({"AI_TEAM_ALLOW_OLLAMA_CLOUD": ""})
        self.assertEqual(models, ["gemma4:12b"])

    def test_embeddings_still_excluded(self):
        self.assertNotIn("nomic-embed-text:latest", list_with({"AI_TEAM_ALLOW_OLLAMA_CLOUD": ""}))

    def test_opt_in_env_restores_cloud_models(self):
        """인증을 붙였다면 되살릴 수 있어야 한다 — 영구 배제가 아니라 기본값 전환이다."""
        models = list_with({"AI_TEAM_ALLOW_OLLAMA_CLOUD": "true"})
        self.assertIn("deepseek-v4-flash:0731-cloud", models)
        self.assertIn("gemma4:12b", models)


class PickOllamaTests(unittest.TestCase):
    def test_coding_task_no_longer_picks_cloud_model(self):
        """이 테스트가 원래 사고를 직접 재현한다 — 제외 전이면 클라우드 모델이 뽑혔다."""
        available = list_with({"AI_TEAM_ALLOW_OLLAMA_CLOUD": ""})
        self.assertEqual(llm._pick_ollama(available, "coding"), "gemma4:12b")

    def test_negative_control_unfiltered_list_would_pick_cloud(self):
        """네거티브 컨트롤: 필터를 끄면 실제로 클라우드 모델이 1순위로 뽑힌다."""
        unfiltered = list_with({"AI_TEAM_ALLOW_OLLAMA_CLOUD": "true"})
        self.assertEqual(llm._pick_ollama(unfiltered, "coding"), "deepseek-v4-flash:0731-cloud")


if __name__ == "__main__":
    unittest.main()
