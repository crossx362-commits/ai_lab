"""_json_ok 회귀 테스트 — 2026-07-28 '다원소 배열이 항상 탈락' 사고를 굳힌다.

사고: `_json_ok`가 `{`…`}` 구간만 잘라 검사했다. 배열 응답 `[{...},{...}]`에서
첫 `{`부터 마지막 `}`까지를 자르면 `{...},{...}` 라는 JSON이 아닌 문자열이 나와
검증이 실패한다. 원소가 1개일 때(`[{...}]`)만 우연히 통과했다.

피해: 예원 오케스트레이터의 계획은 **정의상 JSON 배열**이라(_plan_prompt), 2단계
이상 계획이면 구독 클로드·구독 GPT가 매번 'invalid json'으로 버려지고 로컬 모델로
조용히 강등됐다 — 오너 지시를 해석하는 가장 중요한 호출이 가장 약한 모델로 처리됐다.
실측으로 확인: 같은 프롬프트를 CLI에 직접 넣으면 유효한 JSON이 나오는데
`_claude_code`는 3회 연속 'invalid json'을 찍었다.

동시에 지켜야 할 것: **절단 응답은 계속 걸러내야 한다**. max_tokens를 넘겨 잘린
`[{"a":1},` 는 그 안에 온전한 객체가 하나 있으므로, 괄호 종류를 둘 다 시도하면
통과해 버린다(2026-07-02 'json 잘림' 가드레일이 무력화된다). 그래서 여는 괄호는
먼저 나온 것 하나만 인정한다.
"""
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.llm import _json_ok  # noqa: E402


class JsonOkTests(unittest.TestCase):
    def test_multi_element_array_accepted(self):
        """이번 사고의 본체 — 2개 이상 원소를 가진 배열."""
        self.assertTrue(_json_ok('[{"step":1,"agent":"bomi"},{"step":2,"agent":"mio"}]'))

    def test_single_element_array_accepted(self):
        """예전에도 통과하던 케이스 — 회귀 방지용."""
        self.assertTrue(_json_ok('[{"step":1,"agent":"bomi"}]'))

    def test_object_accepted(self):
        self.assertTrue(_json_ok('{"verdict":"buy","score":3}'))
        self.assertTrue(_json_ok('{"a":[1,2],"b":{"c":3}}'))

    def test_code_fence_and_preamble_tolerated(self):
        """관대한 판정이라는 원래 성격은 유지한다."""
        self.assertTrue(_json_ok('```json\n[{"a":1},{"b":2}]\n```'))
        self.assertTrue(_json_ok('설명입니다.\n[{"a":1},{"b":2}]'))

    def test_primitive_array_accepted(self):
        self.assertTrue(_json_ok("[1,2,3]"))

    def test_truncated_array_rejected(self):
        """max_tokens 초과로 잘린 응답 — 안에 온전한 객체가 있어도 통과하면 안 된다."""
        self.assertFalse(_json_ok('[{"a":1},'))
        self.assertFalse(_json_ok('[{"step":1,"agent":"bomi"},{"step":2,'))

    def test_truncated_object_rejected(self):
        self.assertFalse(_json_ok('{"a":1'))

    def test_non_json_rejected(self):
        self.assertFalse(_json_ok("그냥 잡문입니다"))
        self.assertFalse(_json_ok(""))


class YewonPlanPromptTests(unittest.TestCase):
    """예원 계획 호출이 위 가드레일을 실제로 지키는지 — 사고의 발생 지점."""

    def _source(self):
        p = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "yewon_orchestrator.py"
        return p.read_text(encoding="utf-8")

    def test_plan_call_uses_large_max_tokens(self):
        """json_mode는 max_tokens 1500↑ (CLAUDE.md 가드레일). 400이면 계획이 잘린다."""
        src = self._source()
        self.assertIn("json_mode=True", src)
        self.assertNotIn("max_tokens=400", src)

    def test_plan_prompt_includes_recent_context(self):
        """짧은 지시("검토해")를 CEO가 스스로 풀려면 '방금 무슨 일이 있었나'가 필요하다.
        오너에게 되묻는 건 위임 구조의 실패다."""
        src = self._source()
        self.assertIn("_recent_context", src)


if __name__ == "__main__":
    unittest.main()
