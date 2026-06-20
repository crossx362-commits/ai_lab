import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class KimchiPremiumNotTradingGateTest(unittest.TestCase):
    def test_runtime_traders_do_not_gate_or_score_with_kimchi_premium(self):
        runtime_files = [
            ROOT / "skills" / "데이브_주식" / "tools" / "upbit_auto_trader.py",
            ROOT / "skills" / "데이브_주식" / "tools" / "upbit_analyzer.py",
            ROOT / "skills" / "레오_트레이더" / "tools" / "leo_aggressive_trader.py",
        ]
        combined = "\n".join(path.read_text(encoding="utf-8") for path in runtime_files)

        forbidden = [
            "김프:",
            "kimchi_pct",
            "kp >=",
            "kp <=",
            "kimchi_premium\", {}).get(\"value\"",
            "김프 +",
            "김치프리미엄 높음",
            "과열(김프",
            "역프리미엄",
        ]
        for needle in forbidden:
            self.assertNotIn(needle, combined)

    def test_prompts_and_skill_docs_do_not_use_kimchi_as_hard_rule(self):
        doc_files = [
            ROOT / "skills" / "데이브_주식" / "SKILL.md",
            ROOT / "skills" / "데이브_주식" / "AI_PROMPT_STRUCTURE.md",
            ROOT / "skills" / "데이브_주식" / "indicators_knowledge.md",
            ROOT / "skills" / "레오_트레이더" / "SKILL.md",
            ROOT / "skills" / "레오_트레이더" / "AI_PROMPT_STRUCTURE.md",
            ROOT / "skills" / "공용스킬" / "AI_토큰_최적화_스킬.md",
        ]
        combined = "\n".join(path.read_text(encoding="utf-8") for path in doc_files)

        forbidden = [
            "김프 15%+",
            "김프15%+",
            "김프:",
            "김치 프리미엄 ≥",
            "김치 프리미엄 <",
            "김치 프리미엄 >",
            "김치 프리미엄 ≤",
            "김치 프리미엄 정량 기준",
            "즉시 자산 대피",
            "김프 3~10%",
        ]
        for needle in forbidden:
            self.assertNotIn(needle, combined)


if __name__ == "__main__":
    unittest.main()
