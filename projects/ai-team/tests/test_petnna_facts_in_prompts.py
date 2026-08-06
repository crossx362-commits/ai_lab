"""구현·리뷰 프롬프트가 같은 '프로젝트 사실'을 본다 (2026-08-06 실측 사고).

무슨 일이 있었나 — 반려 사유가 "구현이 나빠서"가 아니라 **양쪽이 사실을 몰라서**였다:

  · 예원 리뷰어 프롬프트는 앱을 "펫나"라고 소개했다. 그래서 워드마크를 **실제 이름인
    '펫과나'로 정확히 구현한** 브랜치를 "브랜드 정체성 훼손"이라며 반려했다.
  · 수리 구현 프롬프트에는 "기존 디자인 시스템을 따르라"만 있고 실제 팔레트가 없었다.
    그래서 클로드가 violet/purple을 골랐고, 예원이 "브랜드 컬러 불일치"로 반려했다 —
    이 경로로 3건이 죽었다.

여기서 굳히는 것:
  1. 사실이 `_shared/petnna_facts.py` 한 곳에만 있다(두 곳에 복붙하면 한쪽만 갱신된다).
  2. 구현(수리)·리뷰(예원) 프롬프트가 **둘 다** 그걸 주입한다 — 한쪽만 알면
     구현과 판정 기준이 어긋나 또 오반려가 난다.
  3. 사실이 실제 코드와 일치한다(앱 이름·brand 팔레트) — 사실 블록 자체가 틀리면
     양쪽이 같이 틀린다.
"""
import re
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.petnna_facts import PETNNA_FACTS  # noqa: E402

_INDEX = PROJECT_ROOT / "projects" / "petnna" / "index.html"
_REVIEWER = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_pr_reviewer.py"
_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


class BothPromptsInjectFactsTests(unittest.TestCase):
    def test_reviewer_injects_facts(self):
        src = _REVIEWER.read_text(encoding="utf-8")
        self.assertIn("from _shared.petnna_facts import PETNNA_FACTS", src)
        self.assertIn("+ PETNNA_FACTS +", src,
                      "리뷰어 프롬프트가 프로젝트 사실을 주입하지 않는다 — 이름·색을 지어낸다")

    def test_engine_injects_facts(self):
        src = _ENGINE.read_text(encoding="utf-8")
        self.assertIn("from _shared.petnna_facts import PETNNA_FACTS", src)
        self.assertIn("+ PETNNA_FACTS +", src,
                      "구현 프롬프트가 팔레트를 안 알려준다 — 클로드가 앱에 없는 색을 고른다")

    def test_facts_are_not_duplicated_in_tools(self):
        """사실 문구를 도구에 복붙하면 한쪽만 갱신돼 어긋난다."""
        for path in (_REVIEWER, _ENGINE):
            src = path.read_text(encoding="utf-8")
            self.assertNotIn("brand-500=#cc785c", src,
                             f"{path.name}에 사실이 복붙돼 있다 — _shared/petnna_facts.py만 쓸 것")


class FactsMatchRealityTests(unittest.TestCase):
    """사실 블록이 틀리면 양쪽이 같이 틀린다 — 실제 코드와 대조한다."""

    def test_app_name_matches_index_html(self):
        html = _INDEX.read_text(encoding="utf-8")
        title = re.search(r"<title>([^<]+)</title>", html)
        self.assertIsNotNone(title)
        self.assertIn("펫과나", title.group(1),
                      "index.html의 앱 이름이 바뀌었다 — petnna_facts.py를 같이 고쳐라")
        self.assertIn("펫과나", PETNNA_FACTS)

    def test_brand_palette_matches_tailwind_config(self):
        html = _INDEX.read_text(encoding="utf-8")
        m = re.search(r"brand:\s*\{(.*?)\}", html, re.S)
        self.assertIsNotNone(m, "index.html에 brand 팔레트 정의가 없다")
        shade500 = re.search(r"500:\s*'(#[0-9a-fA-F]{6})'", m.group(1))
        self.assertIsNotNone(shade500)
        self.assertIn(shade500.group(1), PETNNA_FACTS,
                      f"brand-500이 {shade500.group(1)}로 바뀌었다 — petnna_facts.py를 같이 고쳐라")

    def test_facts_do_not_claim_unused_colors(self):
        """앱이 실제로 안 쓰는 색을 '브랜드 색'이라고 적으면 안 된다."""
        for banned in ("violet", "purple", "indigo"):
            self.assertNotIn(f"{banned}를 쓴다", PETNNA_FACTS)
        self.assertIn("violet/purple/indigo는 이 앱의 색이 아니다", PETNNA_FACTS)


if __name__ == "__main__":
    unittest.main()
