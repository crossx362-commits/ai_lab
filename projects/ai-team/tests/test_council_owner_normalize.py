"""회의 액션아이템 owner 근본 수리 회귀 — 2026-08-04.

사고: 회의 프롬프트가 owner 선택지를 "수리|테오|백호|미오|나무|사람"으로 **하드코딩**해
뒀는데, 실제 소비자 목록인 AUTO_OWNERS엔 나무·사람이 없다. 회의가 나무나 사람을 고를
때마다 아무도 읽지 않는 항목이 하나씩 생겼고, 2026-08-04 시점에 10건이 쌓여 있었다
(사람 5·봄이 3·나무 2).

2026-07-10에 같은 사고를 이미 한 번 겪고 structurally_blocked()로 '걸러내기'는 했지만
**애초에 안 만들게** 하지 않아서 계속 재생산됐다 — 이 저장소에서 반복되는
"한쪽만 고친 비대칭".

여기서 굳히는 것:
  1. 프롬프트의 owner 목록이 AUTO_OWNERS에서 유도된다(하드코딩 금지). 목록이 어긋나는
     상태가 구조적으로 불가능해야 한다.
  2. LLM이 그래도 목록 밖 owner를 뱉으면 적재 시점에 정규화된다.
  3. '사람'은 유효한 트랙이라 살아남되, gate로 명시돼 '소비자 없음'과 구분된다.
"""
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.backlog import (  # noqa: E402
    AUTO_OWNERS, OWNER_CHOICES, HUMAN_OWNER, normalize_owner, structurally_blocked,
)

COUNCIL = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"


class NormalizeOwnerTests(unittest.TestCase):
    def test_consumer_owners_pass_through(self):
        for o in AUTO_OWNERS:
            got, note = normalize_owner(o)
            self.assertEqual(got, o, f"소비자 owner {o!r}가 바뀌었다")
            self.assertEqual(note, "")

    def test_human_owner_survives(self):
        """'사람'은 오너 판단 트랙 — 소비자가 없는 게 정상이므로 지우면 안 된다."""
        got, note = normalize_owner(HUMAN_OWNER)
        self.assertEqual(got, HUMAN_OWNER)
        self.assertEqual(note, "")

    def test_non_consumer_owner_is_reset(self):
        """이번 사고의 본체 — 나무·봄이는 백로그를 읽지 않는다."""
        for o in ("나무", "봄이", "영숙"):
            got, note = normalize_owner(o)
            self.assertEqual(got, "", f"{o!r}가 그대로 남아 유령이 된다")
            self.assertIn(o, note, "무엇이 왜 바뀌었는지 사유에 남아야 한다")

    def test_whitespace_tolerated(self):
        self.assertEqual(normalize_owner("  테오 ")[0], "테오")

    def test_normalized_owner_is_never_structurally_blocked_for_owner_reason(self):
        """정규화를 거친 owner는 'owner 소비자 없음' 사유로는 막히지 않아야 한다."""
        for o in ("나무", "봄이", "수리", ""):
            norm, _ = normalize_owner(o)
            if norm == HUMAN_OWNER:
                continue
            self.assertFalse(
                structurally_blocked(norm, "기능", "일반 UI 개선", "버튼 정렬"),
                f"{o!r} → {norm!r} 인데 여전히 구조적으로 막힌다")


class CouncilPromptTests(unittest.TestCase):
    """프롬프트가 목록을 하드코딩하지 않는지 — 어긋남이 재발하는 유일한 경로."""

    def setUp(self):
        self.src = COUNCIL.read_text(encoding="utf-8")

    def test_prompt_derives_owner_list(self):
        self.assertIn("'|'.join(OWNER_CHOICES)", self.src,
                      "owner 선택지를 OWNER_CHOICES에서 유도해야 한다")

    def test_prompt_has_no_hardcoded_owner_list(self):
        self.assertNotIn("수리|테오|백호|미오|나무|사람", self.src,
                         "하드코딩된 owner 목록이 남아 있다 — AUTO_OWNERS와 다시 어긋난다")

    def test_ingest_normalizes(self):
        self.assertIn("normalize_owner(", self.src,
                      "적재 시점 정규화가 없으면 LLM이 목록 밖 owner를 뱉을 때 그대로 들어간다")

    def test_human_track_is_labeled(self):
        """'사람' 배정은 '소비자 없는 owner'와 구분되는 사유로 기록돼야 한다.

        예전엔 이 문자열이 petnna_council.py 소스에 있는지로 검사했는데, 그러면
        판정 로직을 _shared/backlog.py 단일 소스로 옮기는 정당한 리팩터링이 막힌다
        (2026-08-08에 실제로 막혔다). 문자열의 **위치**가 아니라 **판정 결과**를 본다.
        """
        import sys
        sys.path.insert(0, str(AI_TEAM_ROOT))
        from _shared.backlog import block_reason

        human = block_reason("사람", "기획", "오너가 결정할 무언가", "")
        orphan = block_reason("나무", "기획", "아무거나", "")
        self.assertIsNotNone(human, "'사람' 배정이 사람 트랙으로 안 간다")
        self.assertIn("사람 판단 트랙", human,
                      f"'사람' 배정의 사유가 구분되지 않는다 → {human!r}")
        self.assertNotEqual(human, orphan,
                            "'사람'(의도적 배정)과 '소비자 없는 owner'(잘못된 배정)의 "
                            "사유가 같다 — 나중에 재판단할 때 유령으로 오해한다")


if __name__ == "__main__":
    unittest.main()
