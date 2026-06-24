#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
시그널 학습 엔진
Dave/Leo 백테스트 결과를 분석해 최적 진입 조건을 도출하고
market_signal.json에 learning_insights를 추가한다.
"""
from __future__ import annotations

import json
from collections import defaultdict
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Any

KST = timezone(timedelta(hours=9))
MIN_SAMPLES = 20  # 통계적으로 유의한 최소 샘플 수


def _load_backtest(path: Path) -> list[dict]:
    if not path.exists():
        return []
    records = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            r = json.loads(line)
            if r.get("outcome_return_pct") is not None and r.get("decision") == "BUY":
                records.append(r)
        except Exception:
            continue
    return records


def _best_threshold(records: list[dict], candidates: list[int]) -> int:
    """기대값(EV) 기준 최적 진입 임계값 탐색"""
    best_thresh, best_ev = candidates[0], -999.0
    for thresh in candidates:
        subset = [r for r in records if r.get("entry_score", 0) >= thresh]
        if len(subset) < MIN_SAMPLES:
            continue
        ev = sum(r["outcome_return_pct"] for r in subset) / len(subset)
        win_rate = sum(1 for r in subset if r["outcome_return_pct"] > 0) / len(subset)
        # 기대값과 승률 가중 조합
        combined = ev * 0.6 + (win_rate - 0.5) * 0.4
        if combined > best_ev:
            best_ev, best_thresh = combined, thresh
    return best_thresh


def _coin_stats(records: list[dict]) -> dict[str, dict]:
    by_coin: dict[str, list[float]] = defaultdict(list)
    for r in records:
        ticker = r.get("ticker")
        if ticker:
            by_coin[ticker].append(r["outcome_return_pct"])
    stats = {}
    for ticker, returns in by_coin.items():
        n = len(returns)
        if n < 5:
            continue
        wins = sum(1 for x in returns if x > 0)
        ev = sum(returns) / n
        stats[ticker] = {
            "samples": n,
            "win_rate": round(wins / n, 3),
            "ev_pct": round(ev, 3),
        }
    return stats


def _win_rate_by_range(records: list[dict]) -> dict[str, dict]:
    buckets: dict[int, list[float]] = defaultdict(list)
    for r in records:
        score = r.get("entry_score", 0)
        bucket = int(score // 10) * 10
        buckets[bucket].append(r["outcome_return_pct"])
    result = {}
    for b in sorted(buckets):
        returns = buckets[b]
        wins = sum(1 for x in returns if x > 0)
        result[f"{b}-{b+10}"] = {
            "samples": len(returns),
            "win_rate": round(wins / len(returns), 3),
            "ev_pct": round(sum(returns) / len(returns), 3),
        }
    return result


def analyze(workspace_root: Path) -> dict[str, Any]:
    """
    Dave/Leo 백테스트 → learning_insights 생성
    반환값은 market_signal.json에 통째로 삽입된다.
    """
    backtest_dir = workspace_root / "reports" / "trading" / "backtests"
    insights: dict[str, Any] = {
        "updated_at": datetime.now(KST).isoformat(),
        "note": "백테스트 기반 자동 학습 인사이트 (Signal → Dave/Leo)",
    }

    for agent, thresh_candidates in [
        ("dave", [70, 72, 75, 78, 80]),
        ("leo",  [62, 70, 75, 80, 85, 88, 90]),
    ]:
        records = _load_backtest(backtest_dir / f"{agent}.jsonl")
        if not records:
            insights[agent] = {"error": "백테스트 데이터 없음"}
            continue

        coin_stats = _coin_stats(records)
        top_coins = sorted(
            [t for t, s in coin_stats.items() if s["samples"] >= 10 and s["ev_pct"] > 0],
            key=lambda t: coin_stats[t]["ev_pct"], reverse=True
        )[:6]
        avoid_coins = sorted(
            [t for t, s in coin_stats.items() if s["samples"] >= 10 and s["ev_pct"] < -0.1],
            key=lambda t: coin_stats[t]["ev_pct"]
        )[:4]

        optimal_thresh = _best_threshold(records, thresh_candidates)
        filtered = [r for r in records if r.get("entry_score", 0) >= optimal_thresh]
        ev = sum(r["outcome_return_pct"] for r in filtered) / len(filtered) if filtered else 0
        win_rate = sum(1 for r in filtered if r["outcome_return_pct"] > 0) / len(filtered) if filtered else 0

        insights[agent] = {
            "recommended_buy_threshold": optimal_thresh,
            "expected_ev_pct": round(ev, 3),
            "expected_win_rate": round(win_rate, 3),
            "sample_size": len(filtered),
            "top_coins": top_coins,
            "avoid_coins": avoid_coins,
            "coin_stats": coin_stats,
            "win_rate_by_score_range": _win_rate_by_range(records),
        }

    return insights
