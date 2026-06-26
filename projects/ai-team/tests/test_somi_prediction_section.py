import importlib.util
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "skills" / "소미_분석가" / "tools" / "short_covering_analyzer.py"


def load_analyzer():
    spec = importlib.util.spec_from_file_location("short_covering_prediction_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SomiPredictionSectionTests(unittest.TestCase):
    def test_report_includes_bearish_short_term_prediction(self):
        analyzer = load_analyzer()
        raw = {
            "stock_name": "삼성전자",
            "stock_code": "005930",
            "theme": "전기전자",
            "open": "354,000",
            "high": "356,500",
            "low": "321,500",
            "close": "339,500",
            "change_pct": "-5.30%",
            "volume": "39,735,951",
            "avg_volume_20d": "33,351,639",
            "trading_value": "13,408,352,495,497",
            "recent_price_flow": "353,500 -> 310,000 -> 340,500 -> 358,500 -> 339,500 (-3.96%)",
            "recent_volume_flow": "최근 5일 평균 33,000,000주, 오늘 39,735,951주",
            "buy_indiv": "9,298,204",
            "buy_foreigner": "-5,975,701",
            "buy_institution": "-3,593,889",
            "buy_indiv_5d": "13,305,226",
            "buy_foreigner_5d": "-11,863,761",
            "buy_institution_5d": "-1,901,432",
            "foreign_holding_rate": "47.24",
            "loan_balance_rate": "0.39",
            "short_volume": "2,982,620",
            "short_ratio": "7.51",
            "support_line": "352,832",
            "resistance_line": "363,332",
            "market_warning": "없음",
        }
        score, grade, pos, neg = analyzer.calculate_score(raw)

        report = analyzer.generate_report(raw, score, grade, pos, neg)

        self.assertIn("## 3. 단기 예측", report)
        self.assertIn("방향성: 하락 우세", report)
        self.assertIn("상방 전환 조건", report)
        self.assertIn("하방 위험", report)
        self.assertIn("무효화 조건", report)
        self.assertIn("본 예측은", report)

    def test_report_includes_bullish_short_term_prediction(self):
        analyzer = load_analyzer()
        raw = {
            "stock_name": "테스트전자",
            "stock_code": "123456",
            "theme": "반도체",
            "open": "10,000",
            "high": "11,100",
            "low": "9,900",
            "close": "11,000",
            "change_pct": "+6.00%",
            "volume": "3,000,000",
            "avg_volume_20d": "1,000,000",
            "trading_value": "80,000,000,000",
            "recent_price_flow": "10,000 -> 10,200 -> 10,500 -> 10,800 -> 11,000 (+10.00%)",
            "buy_indiv": "-100,000",
            "buy_foreigner": "500,000",
            "buy_institution": "300,000",
            "buy_indiv_5d": "-200,000",
            "buy_foreigner_5d": "1,200,000",
            "buy_institution_5d": "900,000",
            "foreign_holding_rate": "12.0",
            "loan_balance_rate": "3.5",
            "short_volume": "400,000",
            "support_line": "10,500",
            "resistance_line": "10,900",
            "market_warning": "없음",
        }
        score, grade, pos, neg = analyzer.calculate_score(raw)

        report = analyzer.generate_report(raw, score, grade, pos, neg)

        self.assertIn("## 3. 단기 예측", report)
        self.assertIn("방향성: 상승 우세", report)
        self.assertIn("상방 목표", report)


if __name__ == "__main__":
    unittest.main()
