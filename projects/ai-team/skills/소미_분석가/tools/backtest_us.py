#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""backtest_us.py — 미장 확장 1단계: 소미 코어(가격·거래량 모멘텀+국면)의 미국 전이 검증.

국내 검증 엔진(backtest.py)의 backtest_symbol/_metrics/_score_levels를 그대로 재사용하고,
데이터만 야후 일봉(무료)으로 교체. 수급확인(외인·기관)은 미국에 없는 데이터라 제외 —
즉 '기술적 코어가 수급 없이 미국서 통하는가'를 검증한다(통과 못 하면 2·3단계 중단).

비용: KIS 해외주식 위탁수수료 편도 0.25%(보수) + 슬리피지 0.05%(초대형주 스프레드).
거래대금은 환율(1,350원) 환산 — calculate_score의 원화 절대 문턱(500억↑ 가점) 정합.

실행:
  python backtest_us.py --grid --months 12
  python backtest_us.py --grid --months 12 --fee 0.001   # 수수료 우대 시나리오
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.request
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
sys.path.insert(0, str(AI_TEAM))
sys.path.insert(0, str(_here))

import backtest as bt  # noqa: E402 — 검증 엔진 단일 소스

FX = 1350.0  # USD→KRW 근사 (calculate_score 원화 문턱 정합용)

UNIVERSE_US = {
    # 메가캡 테크
    "AAPL": "Apple", "MSFT": "Microsoft", "NVDA": "Nvidia", "GOOGL": "Alphabet",
    "AMZN": "Amazon", "META": "Meta", "TSLA": "Tesla", "AVGO": "Broadcom",
    # 반도체·하드웨어
    "AMD": "AMD", "QCOM": "Qualcomm", "MU": "Micron", "INTC": "Intel",
    "ARM": "Arm", "SMCI": "Supermicro", "TSM": "TSMC(ADR)",
    # 소프트웨어·플랫폼
    "CRM": "Salesforce", "ORCL": "Oracle", "ADBE": "Adobe", "NFLX": "Netflix",
    "UBER": "Uber", "PLTR": "Palantir", "SNOW": "Snowflake", "SHOP": "Shopify",
    "COIN": "Coinbase",
    # 소비·헬스·금융·에너지 (섹터 분산)
    "COST": "Costco", "WMT": "Walmart", "LLY": "EliLilly", "UNH": "UnitedHealth",
    "JNJ": "J&J", "MRK": "Merck", "JPM": "JPMorgan", "BAC": "BofA",
    "GS": "GoldmanSachs", "V": "Visa", "MA": "Mastercard", "XOM": "Exxon",
    "CVX": "Chevron", "BA": "Boeing", "CAT": "Caterpillar", "GE": "GE",
}


def _yahoo_bars(symbol: str, months: int = 12) -> list[dict]:
    """야후 일봉 → backtest 봉 형식. 마지막 봉은 당일 미확정 가능성이 있어 제거."""
    rng = "2y" if months > 12 else "1y"
    url = (f"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}"
           f"?range={rng}&interval=1d")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=15) as r:
        d = json.loads(r.read())
    res = d["chart"]["result"][0]
    ts = res.get("timestamp") or []
    q = res["indicators"]["quote"][0]
    bars = []
    for i, t in enumerate(ts):
        c, o, h, lo, v = (q["close"][i], q["open"][i], q["high"][i], q["low"][i], q["volume"][i])
        if not all(x is not None for x in (c, o, h, lo, v)):
            continue
        bars.append({"date": datetime.fromtimestamp(t).strftime("%Y%m%d"),
                     "o": o, "h": h, "l": lo, "c": c, "v": v, "val": c * v * FX})
    cutoff = (datetime.now().replace(month=1) if False else None)
    bars = bars[:-1]  # 당일 미확정 봉 제거
    need = int(months * 21.5)
    return bars[-need:] if len(bars) > need else bars


def us_regime_map(months: int) -> dict:
    """S&P500(SPY) 종가 > MA20 = 상승국면 — 국내 검증과 동일 정의."""
    bars = _yahoo_bars("SPY", months)
    closes = [b["c"] for b in bars]
    ok = {}
    for i, b in enumerate(bars):
        ok[b["date"]] = True if i < 20 else b["c"] > sum(closes[i - 20:i]) / 20
    return ok


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--months", type=int, default=12)
    ap.add_argument("--grid", action="store_true")
    ap.add_argument("--fee", type=float, default=0.0025, help="편도 수수료 (기본 KIS 해외 0.25%%)")
    ap.add_argument("--slip", type=float, default=0.0005)
    args = ap.parse_args()

    bt.FEE, bt.TAX, bt.SLIP = args.fee, 0.0, args.slip   # 미국: 거래세 없음
    regime = us_regime_map(args.months)
    data = {}
    for sym in UNIVERSE_US:
        try:
            bars = _yahoo_bars(sym, args.months)
            if len(bars) >= 30:
                data[sym] = bars
        except Exception as e:
            print(f"  [스킵] {sym}: {e}")
        time.sleep(0.3)
    print(f"[미국 전이검증] {len(data)}종목 / {args.months}개월 / 편도 수수료 {args.fee*100:.2f}% "
          f"(상승국면 {sum(regime.values())}/{len(regime)}일)\n")
    print(f"{'기준':>4} {'보유':>4} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} {'샤프':>5}")
    for th in (50, 55, 60, 65, 70):
        for hd in (5, 7, 10):
            trades = []
            for bars in data.values():
                trades += bt.backtest_symbol(bars, th, hd, bt._score_levels, regime)
            m = bt._metrics(trades)
            if m.get("trades"):
                print(f"{th:>4} {hd:>4} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                      f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5}")
            else:
                print(f"{th:>4} {hd:>4}  거래 없음")


if __name__ == "__main__":
    main()
