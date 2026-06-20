import importlib.util
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "skills" / "시그널_분석가" / "tools" / "market_signal.py"


def load_market_signal_module():
    spec = importlib.util.spec_from_file_location("market_signal_for_test", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class MarketSignalKoreanNotificationTest(unittest.TestCase):
    def test_signal_summary_is_korean(self):
        module = load_market_signal_module()
        summary = module.summarize(
            {
                "crypto": {
                    "fear_greed": {"value": 82, "signal": "SELL"},
                    "kimchi_premium": {"value": 6.2, "signal": "SELL"},
                    "top_coins": [{"ticker": "KRW-DOGE", "score": 4}, {"ticker": "KRW-XRP", "score": 3}],
                }
            }
        )

        self.assertIn("공포탐욕", summary)
        self.assertIn("김치프리미엄", summary)
        self.assertIn("상위 코인", summary)
        self.assertIn("주의", summary)
        self.assertNotIn("Fear/Greed", summary)
        self.assertNotIn("kimchi", summary.lower())

    def test_signal_change_notification_uses_korean_header(self):
        module = load_market_signal_module()
        sent = []
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            module.STATE_FILE = temp_path / "market_signal_state.json"
            module.COMPAT_STATE_FILE = temp_path / "pulse_state.json"
            module.send = lambda message, silent=False: sent.append((message, silent))

            module.notify_on_change(
                {
                    "crypto": {
                        "fear_greed": {"value": 80, "signal": "SELL"},
                        "kimchi_premium": {"value": 1.2, "signal": "NEUTRAL"},
                        "top_coins": [],
                    }
                }
            )

        self.assertTrue(sent)
        self.assertTrue(sent[0][0].startswith("📡 [시그널] 시장 신호가 바뀌었어요"))
        self.assertNotIn("[Signal] Market signal changed", sent[0][0])


if __name__ == "__main__":
    unittest.main()
