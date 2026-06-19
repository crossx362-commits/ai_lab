import importlib.util
import json
import pathlib
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "_shared" / "trading_entry_evaluator.py"


def load_module():
    spec = importlib.util.spec_from_file_location("trading_entry_evaluator_under_test", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class TradingEntryEvaluatorTests(unittest.TestCase):
    def test_entry_score_normalizes_raw_score_and_uses_win_rate_rr(self):
        evaluator = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            history = root / "reports" / "trading" / "backtests" / "dave.jsonl"
            history.parent.mkdir(parents=True)
            history.write_text(
                "\n".join(
                    [
                        json.dumps({"ticker": "KRW-BTC", "decision": "BUY", "outcome_return_pct": 2.0}),
                        json.dumps({"ticker": "KRW-BTC", "decision": "BUY", "outcome_return_pct": 1.0}),
                        json.dumps({"ticker": "KRW-BTC", "decision": "BUY", "outcome_return_pct": -0.5}),
                    ]
                )
                + "\n",
                encoding="utf-8",
            )

            result = evaluator.evaluate_entry(
                agent="dave",
                ticker="KRW-BTC",
                raw_score=8,
                max_raw_score=10,
                reasons=["trend"],
                workspace_root=root,
            )

        self.assertGreaterEqual(result["entry_score"], 80)
        self.assertGreater(result["expected_win_rate"], 0.6)
        self.assertGreater(result["risk_reward"], 1.5)
        self.assertEqual(result["decision"], "BUY")

    def test_hold_streak_lowers_hold_threshold_without_overriding_hard_hold(self):
        evaluator = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            history = root / "reports" / "trading" / "backtests" / "leo.jsonl"
            history.parent.mkdir(parents=True)
            for _ in range(5):
                evaluator.record_decision(
                    agent="leo",
                    ticker="KRW-SUI",
                    decision="HOLD",
                    evaluation={"entry_score": 50, "reasons": ["weak"]},
                    workspace_root=root,
                )

            soft = evaluator.evaluate_entry(
                agent="leo",
                ticker="KRW-SUI",
                raw_score=5,
                max_raw_score=10,
                reasons=["volume"],
                workspace_root=root,
            )
            hard = evaluator.evaluate_entry(
                agent="leo",
                ticker="KRW-SUI",
                raw_score=9,
                max_raw_score=10,
                reasons=["volume"],
                hard_hold_reasons=["daily loss limit"],
                workspace_root=root,
            )

        self.assertGreaterEqual(soft["hold_pressure"], 10)
        self.assertIn(soft["decision"], {"BUY", "WATCH"})
        self.assertEqual(hard["decision"], "HOLD")
        self.assertIn("daily loss limit", hard["hard_hold_reasons"])

    def test_record_decision_writes_jsonl(self):
        evaluator = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            path = evaluator.record_decision(
                agent="dave",
                ticker="KRW-ETH",
                decision="BUY",
                evaluation={"entry_score": 77, "expected_win_rate": 0.58, "risk_reward": 1.4},
                reason="score gate",
                workspace_root=root,
            )
            rows = [json.loads(line) for line in pathlib.Path(path).read_text(encoding="utf-8").splitlines()]

        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["agent"], "dave")
        self.assertEqual(rows[0]["ticker"], "KRW-ETH")
        self.assertEqual(rows[0]["decision"], "BUY")
        self.assertEqual(rows[0]["entry_score"], 77)

    def test_backfill_pending_outcomes_records_later_return(self):
        evaluator = load_module()
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            evaluator.record_decision(
                agent="leo",
                ticker="KRW-SOL",
                decision="BUY",
                evaluation={"entry_score": 80},
                workspace_root=root,
                extra={"observed_price": 100.0, "timestamp": "2026-06-19T00:00:00+00:00"},
            )

            updated = evaluator.backfill_pending_outcomes(
                agent="leo",
                ticker="KRW-SOL",
                current_price=106.0,
                workspace_root=root,
                min_age_seconds=0,
            )
            rows = [
                json.loads(line)
                for line in (root / "reports" / "trading" / "backtests" / "leo.jsonl").read_text(encoding="utf-8").splitlines()
            ]

        self.assertEqual(updated, 1)
        self.assertEqual(rows[0]["outcome_return_pct"], 6.0)
        self.assertEqual(rows[0]["outcome_price"], 106.0)


if __name__ == "__main__":
    unittest.main()
