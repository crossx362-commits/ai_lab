"""Shared entry scoring, HOLD pressure, and backtest logging for traders."""
from __future__ import annotations

import json
import math
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]

AGENT_DEFAULTS = {
    "dave": {
        "buy_threshold": 70,
        "watch_threshold": 55,
        "default_win_rate": 0.52,
        "default_risk_reward": 1.25,
        "hold_pressure_step": 3,
        "hold_pressure_points": 4,
    },
    "leo": {
        "buy_threshold": 62,
        "watch_threshold": 48,
        "default_win_rate": 0.55,
        "default_risk_reward": 1.45,
        "hold_pressure_step": 2,
        "hold_pressure_points": 5,
    },
}


def _workspace_root(workspace_root: str | Path | None = None) -> Path:
    return Path(workspace_root).resolve() if workspace_root else DEFAULT_WORKSPACE_ROOT


def backtest_path(agent: str, workspace_root: str | Path | None = None) -> Path:
    safe_agent = "".join(ch for ch in str(agent).lower() if ch.isalnum() or ch in {"_", "-"}).strip() or "trader"
    return _workspace_root(workspace_root) / "reports" / "trading" / "backtests" / f"{safe_agent}.jsonl"


def load_backtest_records(
    agent: str,
    ticker: str | None = None,
    limit: int = 200,
    workspace_root: str | Path | None = None,
) -> list[dict[str, Any]]:
    path = backtest_path(agent, workspace_root)
    if not path.exists():
        return []
    rows: list[dict[str, Any]] = []
    try:
        for line in path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue
            if ticker and row.get("ticker") != ticker:
                continue
            rows.append(row)
    except OSError:
        return []
    return rows[-limit:]


def summarize_performance(records: list[dict[str, Any]], defaults: dict[str, float]) -> dict[str, float]:
    outcomes: list[float] = []
    for row in records:
        value = row.get("outcome_return_pct", row.get("profit_pct"))
        try:
            if value is not None:
                outcomes.append(float(value))
        except (TypeError, ValueError):
            continue

    if not outcomes:
        return {
            "sample_size": 0,
            "win_rate": float(defaults["default_win_rate"]),
            "risk_reward": float(defaults["default_risk_reward"]),
            "avg_win_pct": 0.0,
            "avg_loss_pct": 0.0,
            "expectancy_pct": 0.0,
        }

    wins = [v for v in outcomes if v > 0]
    losses = [-v for v in outcomes if v < 0]
    win_rate = len(wins) / len(outcomes)
    avg_win = sum(wins) / len(wins) if wins else 0.0
    avg_loss = sum(losses) / len(losses) if losses else max(avg_win / 1.5, 0.1)
    risk_reward = avg_win / avg_loss if avg_loss > 0 else float(defaults["default_risk_reward"])
    expectancy = win_rate * avg_win - (1 - win_rate) * avg_loss
    return {
        "sample_size": len(outcomes),
        "win_rate": round(win_rate, 4),
        "risk_reward": round(risk_reward, 4),
        "avg_win_pct": round(avg_win, 4),
        "avg_loss_pct": round(avg_loss, 4),
        "expectancy_pct": round(expectancy, 4),
    }


def recent_hold_streak(records: list[dict[str, Any]]) -> int:
    streak = 0
    for row in reversed(records):
        if str(row.get("decision", "")).upper() == "HOLD":
            streak += 1
        else:
            break
    return streak


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def normalize_raw_score(raw_score: float, max_raw_score: float) -> float:
    if max_raw_score <= 0:
        return 0.0
    return _clamp((float(raw_score) / float(max_raw_score)) * 100.0, 0.0, 100.0)


def evaluate_entry(
    agent: str,
    ticker: str,
    raw_score: float,
    max_raw_score: float,
    reasons: list[str] | None = None,
    metrics: dict[str, Any] | None = None,
    hard_hold_reasons: list[str] | None = None,
    workspace_root: str | Path | None = None,
) -> dict[str, Any]:
    agent_key = str(agent).lower()
    defaults = AGENT_DEFAULTS.get(agent_key, AGENT_DEFAULTS["dave"])
    records = load_backtest_records(agent_key, ticker=ticker, workspace_root=workspace_root)
    perf = summarize_performance(records, defaults)
    hold_streak = recent_hold_streak(records)

    base_score = normalize_raw_score(raw_score, max_raw_score)
    win_rate = perf["win_rate"]
    risk_reward = perf["risk_reward"]
    expectancy = perf["expectancy_pct"]

    performance_adjustment = 0.0
    performance_adjustment += _clamp((win_rate - 0.5) * 60.0, -9.0, 12.0)
    performance_adjustment += _clamp((risk_reward - 1.2) * 8.0, -8.0, 12.0)
    performance_adjustment += _clamp(expectancy * 2.0, -8.0, 10.0)

    hold_pressure = max(
        0.0,
        (hold_streak - float(defaults["hold_pressure_step"]) + 1.0) * float(defaults["hold_pressure_points"]),
    )

    metrics = metrics or {}
    metric_adjustment = 0.0
    try:
        volume_ratio = float(metrics.get("volume_ratio", 0) or 0)
        if volume_ratio >= 1.5:
            metric_adjustment += min((volume_ratio - 1.0) * 2.0, 6.0)
    except (TypeError, ValueError):
        pass
    try:
        momentum = float(metrics.get("momentum_1h", metrics.get("momentum_pct", 0)) or 0)
        metric_adjustment += _clamp(momentum, -5.0, 6.0)
    except (TypeError, ValueError):
        pass

    entry_score = _clamp(base_score + performance_adjustment + hold_pressure + metric_adjustment, 0.0, 100.0)
    hard_hold_reasons = [r for r in (hard_hold_reasons or []) if r]

    if hard_hold_reasons:
        decision = "HOLD"
    elif entry_score >= float(defaults["buy_threshold"]):
        decision = "BUY"
    elif entry_score >= float(defaults["watch_threshold"]):
        decision = "WATCH"
    else:
        decision = "HOLD"

    return {
        "agent": agent_key,
        "ticker": ticker,
        "decision": decision,
        "entry_score": round(entry_score, 2),
        "base_score": round(base_score, 2),
        "raw_score": raw_score,
        "max_raw_score": max_raw_score,
        "expected_win_rate": round(win_rate, 4),
        "risk_reward": round(risk_reward, 4),
        "expectancy_pct": round(expectancy, 4),
        "hold_streak": hold_streak,
        "hold_pressure": round(hold_pressure, 2),
        "performance_adjustment": round(performance_adjustment, 2),
        "metric_adjustment": round(metric_adjustment, 2),
        "hard_hold_reasons": hard_hold_reasons,
        "reasons": list(reasons or []),
        "sample_size": perf["sample_size"],
    }


def suggested_position_pct(agent: str, evaluation: dict[str, Any]) -> int:
    if evaluation.get("decision") == "HOLD":
        return 0
    score = float(evaluation.get("entry_score", 0) or 0)
    agent_key = str(agent).lower()
    if agent_key == "leo":
        if score >= 85:
            return 50
        if score >= 72:
            return 40
        if score >= 62:
            return 30
        return 10
    if score >= 85:
        return 40
    if score >= 75:
        return 25
    if score >= 70:
        return 15
    if score >= 55:
        return 5
    return 0


def record_decision(
    agent: str,
    ticker: str,
    decision: str,
    evaluation: dict[str, Any] | None = None,
    reason: str = "",
    workspace_root: str | Path | None = None,
    extra: dict[str, Any] | None = None,
) -> str:
    evaluation = evaluation or {}
    path = backtest_path(agent, workspace_root)
    path.parent.mkdir(parents=True, exist_ok=True)
    row = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "agent": str(agent).lower(),
        "ticker": ticker,
        "decision": str(decision).upper(),
        "reason": reason,
        "entry_score": evaluation.get("entry_score"),
        "base_score": evaluation.get("base_score"),
        "raw_score": evaluation.get("raw_score"),
        "expected_win_rate": evaluation.get("expected_win_rate"),
        "risk_reward": evaluation.get("risk_reward"),
        "expectancy_pct": evaluation.get("expectancy_pct"),
        "hold_streak": evaluation.get("hold_streak"),
        "hold_pressure": evaluation.get("hold_pressure"),
        "sample_size": evaluation.get("sample_size"),
        "reasons": evaluation.get("reasons", []),
        "hard_hold_reasons": evaluation.get("hard_hold_reasons", []),
    }
    if extra:
        row.update(extra)
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(row, ensure_ascii=False, sort_keys=True) + "\n")
    return str(path)


def _parse_ts(value: str | None) -> datetime | None:
    if not value:
        return None
    try:
        return datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    except ValueError:
        return None


def backfill_pending_outcomes(
    agent: str,
    ticker: str,
    current_price: float,
    workspace_root: str | Path | None = None,
    min_age_seconds: int = 1800,
) -> int:
    """Fill later return pct for prior recorded decisions when price is observed again."""
    path = backtest_path(agent, workspace_root)
    if not path.exists():
        return 0
    try:
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    except (OSError, json.JSONDecodeError):
        return 0

    now = datetime.now(timezone.utc)
    updated = 0
    for row in rows:
        if row.get("ticker") != ticker:
            continue
        if row.get("outcome_return_pct") is not None:
            continue
        observed = row.get("observed_price", row.get("entry_price"))
        try:
            observed_price = float(observed)
            current = float(current_price)
        except (TypeError, ValueError):
            continue
        if observed_price <= 0 or current <= 0:
            continue
        ts = _parse_ts(row.get("timestamp"))
        if ts and (now - ts).total_seconds() < min_age_seconds:
            continue
        outcome = (current - observed_price) / observed_price * 100.0
        row["outcome_price"] = current
        row["outcome_return_pct"] = round(outcome, 4)
        row["outcome_recorded_at"] = now.isoformat()
        updated += 1

    if not updated:
        return 0
    with path.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False, sort_keys=True) + "\n")
    return updated


def format_evaluation(evaluation: dict[str, Any]) -> str:
    rr = evaluation.get("risk_reward", 0)
    wr = float(evaluation.get("expected_win_rate", 0) or 0) * 100
    return (
        f"entry={evaluation.get('entry_score')} "
        f"win={wr:.1f}% rr={rr} "
        f"hold={evaluation.get('hold_streak')} "
        f"decision={evaluation.get('decision')}"
    )
