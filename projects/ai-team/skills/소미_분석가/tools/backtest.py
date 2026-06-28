#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 전략 백테스트 엔진 — 노트북에서 과거 일봉으로 수익성 검증.

소미의 '가격·거래량' 점수(short_covering_analyzer.calculate_score)와 진입/손절/목표 로직을
과거 일봉에 무미래참조(walk-forward)로 적용하고, 실제 비용(수수료·거래세·슬리피지)을 반영해
승률·손익비·누적수익·MDD·샤프를 측정한다.

한계(정직): 과거 수급(외국인/기관)·공매도·뉴스는 히스토리 확보가 어려워 0으로 둔다.
즉 '기술적(가격·거래량·지지/저항)' 코어의 수익성만 검증한다. 수급·뉴스 가점은 실거래에서 추가됨.
"""

from __future__ import annotations

import argparse
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from somi_kis_reporter import KISClient, num  # noqa: E402
from short_covering_analyzer import calculate_score  # noqa: E402

load_env(str(PROJECT_ROOT))

# 검증용 유동성 종목 바스켓 (과거 거래대금 순위 히스토리가 없어 고정 바스켓 사용)
UNIVERSE = {
    # 대형주
    "005930": "삼성전자", "000660": "SK하이닉스", "373220": "LG에너지솔루션",
    "207940": "삼성바이오로직스", "005380": "현대차", "000270": "기아",
    "005490": "POSCO홀딩스", "035420": "NAVER", "035720": "카카오",
    "068270": "셀트리온", "006400": "삼성SDI", "051910": "LG화학",
    "012330": "현대모비스", "105560": "KB금융", "055550": "신한지주",
    "402340": "SK스퀘어", "009150": "삼성전기", "011070": "LG이노텍",
    "259960": "크래프톤", "086520": "에코프로",
    # 대형·중형 확대 (표본↑)
    "012450": "한화에어로스페이스", "042700": "한미반도체", "247540": "에코프로비엠",
    "028260": "삼성물산", "323410": "카카오뱅크", "034020": "두산에너빌리티",
    "003670": "포스코퓨처엠", "011200": "HMM", "015760": "한국전력",
    "032830": "삼성생명", "138040": "메리츠금융지주", "086280": "현대글로비스",
    "036570": "엔씨소프트", "352820": "하이브", "090430": "아모레퍼시픽",
    "024110": "기업은행", "003550": "LG", "017670": "SK텔레콤",
    "030200": "KT", "196170": "알테오젠",
}

# 비용 (왕복) — 매수/매도 수수료 + 거래세 + 슬리피지
FEE = 0.00015          # 편도 위탁수수료
TAX = 0.0018           # 매도 거래세(코스피 기준 근사)
SLIP = 0.001           # 편도 슬리피지(시장가)


def _history(kis: KISClient, symbol: str, months: int = 12) -> list[dict]:
    """KIS 일봉을 페이지네이션으로 ~months 만큼 받아 chronological(오래된→최신) 반환."""
    bars: dict[str, dict] = {}
    end = datetime.now()
    for _ in range(months // 3 + 1):  # 호출당 ~100행(약 5개월) → 여유로 반복
        start = end - timedelta(days=150)
        try:
            d = kis.get(
                "uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice", "FHKST03010100",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol,
                 "FID_INPUT_DATE_1": start.strftime("%Y%m%d"), "FID_INPUT_DATE_2": end.strftime("%Y%m%d"),
                 "FID_PERIOD_DIV_CODE": "D", "FID_ORG_ADJ_PRC": "0"},
            )
        except Exception:
            break
        rows = d.get("output2") or []
        if not rows:
            break
        for r in rows:
            dt = r.get("stck_bsop_date")
            if dt and r.get("stck_clpr"):
                bars[dt] = {
                    "date": dt, "o": num(r.get("stck_oprc")), "h": num(r.get("stck_hgpr")),
                    "l": num(r.get("stck_lwpr")), "c": num(r.get("stck_clpr")),
                    "v": num(r.get("acml_vol")), "val": num(r.get("acml_tr_pbmn")),
                }
        oldest = min(bars)
        end = datetime.strptime(oldest, "%Y%m%d") - timedelta(days=1)
        time.sleep(0.15)
    return [bars[d] for d in sorted(bars)]


def _score_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """t일 종가 기준 가격·거래량 점수 + 진입/손절/목표 (무미래참조: bars[:t+1]만 사용)."""
    cur, prev = bars[t], bars[t - 1]
    win = bars[t - 20:t]
    avg_vol = sum(b["v"] for b in win) / len(win) if win else 0
    chg = ((cur["c"] - prev["c"]) / prev["c"] * 100) if prev["c"] else 0
    # 피벗 지지/저항 (전일 H/L/C 표준식)
    p = (prev["h"] + prev["l"] + prev["c"]) / 3
    s1, r1 = 2 * p - prev["h"], 2 * p - prev["l"]
    parsed = {
        "close": str(cur["c"]), "open": str(cur["o"]), "high": str(cur["h"]), "low": str(cur["l"]),
        "change_pct": str(chg), "volume": str(cur["v"]), "avg_volume_20d": str(avg_vol),
        "trading_value": str(cur["val"]), "support_line": str(s1), "resistance_line": str(r1),
    }
    score, _, _, _ = calculate_score(parsed)
    entry = cur["c"]
    stop = s1 if (s1 and s1 < entry) else round(entry * 0.95)
    target = r1 if (r1 and r1 > entry) else round(entry * 1.10)
    return score, entry, stop, target


def _pullback_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """눌림목 진입: 상승추세 중 단기 눌림에서 매수 (모멘텀 추격 대안).
    추세(종가>MA20·MA20 상승) + 눌림(종가≤MA5) + 고점대비 2~12% 되돌림 → 점수화."""
    c = [b["c"] for b in bars[:t + 1]]
    if len(c) < 25:
        return 0, bars[t]["c"], 0, 0
    cur = c[-1]
    ma5 = sum(c[-5:]) / 5
    ma20 = sum(c[-20:]) / 20
    ma20_prev = sum(c[-25:-5]) / 20
    recent_high = max(c[-10:])
    uptrend = cur > ma20 and ma20 > ma20_prev
    pullback = cur <= ma5
    dip = (recent_high - cur) / recent_high if recent_high else 0
    score = 0
    if uptrend and pullback and 0.02 <= dip <= 0.12:
        score = 60 + int(min(dip, 0.12) * 200)  # 되돌림 깊을수록 가점(최대 ~84)
    entry = cur
    stop = round(min(ma20, cur) * 0.97)          # MA20 약간 아래 손절
    target = round(max(recent_high, cur * 1.08))  # 전고 회복 or +8%
    return score, entry, stop, target


def _obv_rising(bars: list[dict], t: int, lookback: int = 10) -> bool:
    """수급 프록시: OBV(누적 방향성 거래량) 추세가 상승인가 = 매집 흐름."""
    if t < lookback + 1:
        return False
    obv = 0.0
    series = []
    for k in range(t - lookback, t + 1):
        if k == 0:
            continue
        sign = 1 if bars[k]["c"] > bars[k - 1]["c"] else (-1 if bars[k]["c"] < bars[k - 1]["c"] else 0)
        obv += sign * bars[k]["v"]
        series.append(obv)
    # OBV 시작 대비 끝이 상승 + 후반부가 전반부보다 높으면 매집
    return len(series) >= 4 and series[-1] > series[0] and series[-1] > series[len(series) // 2]


def _momentum_obv_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """모멘텀 점수 + 수급(OBV) 필터 — OBV 매집 아니면 진입 제외(score 0)."""
    score, entry, stop, target = _score_levels(bars, t)
    if not _obv_rising(bars, t):
        return 0, entry, stop, target
    return score, entry, stop, target


# ── 진입 신호 강화(절대수익): RSI(과매수 회피) · 상대강도(지수 초과) ──────────
_MARKET: dict = {}  # date -> 지수(KODEX200) 종가. compare_strategies에서 채움.


def _rsi(bars: list[dict], t: int, period: int = 14) -> float:
    """Wilder 단순화 RSI. 데이터 부족 시 중립값 50."""
    if t < period:
        return 50.0
    gains = losses = 0.0
    for k in range(t - period + 1, t + 1):
        d = bars[k]["c"] - bars[k - 1]["c"]
        if d >= 0:
            gains += d
        else:
            losses -= d
    if losses == 0:
        return 100.0
    rs = (gains / period) / (losses / period)
    return 100 - 100 / (1 + rs)


def _rel_strength_ok(bars: list[dict], t: int, lookback: int = 20) -> bool:
    """상대강도: 종목의 lookback 수익률이 지수보다 높은가(시장 주도주). 데이터 없으면 중립 통과."""
    if not _MARKET or t < lookback or not bars[t - lookback]["c"]:
        return True
    stock_ret = bars[t]["c"] / bars[t - lookback]["c"] - 1
    m_now = _MARKET.get(bars[t]["date"])
    m_then = _MARKET.get(bars[t - lookback]["date"])
    if m_now and m_then:
        return stock_ret > (m_now / m_then - 1)
    return True


def _rsi_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """모멘텀 + RSI 과매수(>70) 진입 제외 — 블로우오프 고점 추격 회피."""
    score, e, s, tg = _score_levels(bars, t)
    return (0 if _rsi(bars, t) > 70 else score), e, s, tg


def _rs_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """모멘텀 + 상대강도(지수 초과) 필터 — 시장 주도주만 매수."""
    score, e, s, tg = _score_levels(bars, t)
    return (score if _rel_strength_ok(bars, t) else 0), e, s, tg


def _combo_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """모멘텀 + 상대강도 + RSI과매수회피 동시 적용."""
    score, e, s, tg = _score_levels(bars, t)
    ok = _rel_strength_ok(bars, t) and _rsi(bars, t) <= 70
    return (score if ok else 0), e, s, tg


def market_regime_map(kis: KISClient, months: int) -> dict:
    """시장 국면(라이브 HMM 게이트의 백테스트 대용) — KODEX200 종가>MA20 이면 상승국면(진입 허용)."""
    bars = _history(kis, "069500", months)  # KODEX 200 = KOSPI200 대용
    closes = [b["c"] for b in bars]
    ok = {}
    for i, b in enumerate(bars):
        if i < 20:
            ok[b["date"]] = True
            continue
        ma20 = sum(closes[i - 20:i]) / 20
        ok[b["date"]] = b["c"] > ma20  # 지수 20일선 위 = 상승국면
    return ok


def backtest_symbol(bars: list[dict], threshold: int, hold: int, levels_fn=_score_levels,
                    regime_ok: dict | None = None, trail_pct: float | None = None,
                    use_target: bool = True) -> list[dict]:
    """한 종목 walk-forward 백테스트 → 체결 리스트. levels_fn으로 전략 교체, regime_ok로 국면 게이트.
    trail_pct: 고점 대비 trail_pct 하락 시 청산(트레일링 스톱). use_target=False면 고정목표 미사용(추세 추종)."""
    trades = []
    i = 21
    n = len(bars)
    while i < n - 1:
        # 시장 하락국면이면 진입 스킵 (라이브 HMM 게이트와 동일 취지)
        if regime_ok is not None and not regime_ok.get(bars[i]["date"], True):
            i += 1
            continue
        score, _entry, stop, target = levels_fn(bars, i)
        if score < threshold or not (stop < bars[i]["c"] < target):
            i += 1
            continue
        # 다음날 시가 진입 (무미래참조)
        ep = bars[i + 1]["o"] * (1 + SLIP)
        exit_price, exit_reason, exit_idx = None, "timeout", min(i + hold, n - 1)
        peak = ep
        for j in range(i + 1, min(i + 1 + hold, n)):
            peak = max(peak, bars[j]["h"])           # 보유 중 최고가 갱신
            eff_stop = stop
            if trail_pct:                            # 트레일링: 고점 대비 trail_pct 하락선
                eff_stop = max(stop, peak * (1 - trail_pct))
            if bars[j]["l"] <= eff_stop:             # 손절/트레일링 우선(보수적)
                reason = "trail" if (trail_pct and eff_stop > stop) else "stop"
                exit_price, exit_reason, exit_idx = eff_stop * (1 - SLIP), reason, j
                break
            if use_target and bars[j]["h"] >= target:
                exit_price, exit_reason, exit_idx = target * (1 - SLIP), "target", j
                break
        if exit_price is None:
            exit_price = bars[exit_idx]["c"] * (1 - SLIP)
        gross = exit_price / ep - 1
        net = gross - (FEE * 2 + TAX)            # 왕복 수수료 + 매도세
        risk = (ep - stop) / ep if ep else 0     # 손절까지 거리(=1주당 위험) — 사이징용
        trades.append({"ret": net, "reason": exit_reason, "days": exit_idx - i,
                       "score": score, "risk": risk})
        i = exit_idx + 1                          # 청산 후 재진입
    return trades


def _metrics(trades: list[dict]) -> dict:
    if not trades:
        return {"trades": 0}
    import numpy as np
    r = np.array([t["ret"] for t in trades])
    wins = r[r > 0]; losses = r[r <= 0]
    eq = np.cumprod(1 + r)
    peak = np.maximum.accumulate(eq)
    mdd = float(((eq - peak) / peak).min() * 100)
    return {
        "trades": len(r),
        "win_rate": round(float((r > 0).mean()) * 100, 1),
        "avg_win": round(float(wins.mean()) * 100, 2) if len(wins) else 0,
        "avg_loss": round(float(losses.mean()) * 100, 2) if len(losses) else 0,
        "profit_factor": round(float(wins.sum() / -losses.sum()), 2) if len(losses) and losses.sum() < 0 else 0,
        "total_return": round((float(eq[-1]) - 1) * 100, 1),
        "mdd": round(mdd, 1),
        "sharpe": round(float(r.mean() / r.std() * (252 ** 0.5)) if r.std() else 0, 2),
        "stop/target/timeout": f"{sum(t['reason']=='stop' for t in trades)}/{sum(t['reason']=='target' for t in trades)}/{sum(t['reason']=='timeout' for t in trades)}",
    }


def run(threshold: int, hold: int, months: int) -> dict:
    kis = KISClient()
    all_trades = []
    for code, name in UNIVERSE.items():
        bars = _history(kis, code, months)
        if len(bars) < 30:
            continue
        all_trades += backtest_symbol(bars, threshold, hold)
    return {"params": {"threshold": threshold, "hold": hold, "months": months,
                       "universe": len(UNIVERSE)}, **_metrics(all_trades)}


def _load_all(months: int) -> dict:
    """전 종목 일봉을 1회만 받아 캐시 (그리드 스캔용)."""
    kis = KISClient()
    out = {}
    for code in UNIVERSE:
        bars = _history(kis, code, months)
        if len(bars) >= 30:
            out[code] = bars
    return out


def grid(months: int, thresholds=(50, 55, 60, 65, 70), holds=(5, 7, 10, 15)) -> None:
    """데이터 1회 로드 후 기준×보유기간 그리드 비교 (최적 파라미터 탐색)."""
    data = _load_all(months)
    print(f"[그리드] {len(data)}종목 / {months}개월 로드 완료\n")
    print(f"{'기준':>4} {'보유':>4} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} {'샤프':>5}")
    best = None
    for th in thresholds:
        for hd in holds:
            trades = []
            for bars in data.values():
                trades += backtest_symbol(bars, th, hd)
            m = _metrics(trades)
            if not m.get("trades"):
                continue
            print(f"{th:>4} {hd:>4} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                  f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5}")
            # 최적: 샤프 우선, 거래 30건 이상(통계 신뢰) + MDD -40% 이내 — 과적합 소표본 배제
            if m["trades"] >= 30 and m["mdd"] > -40:
                key = m["sharpe"]
                if best is None or key > best[0]:
                    best = (key, th, hd, m)
    if best:
        _, th, hd, m = best
        print(f"\n✅ 최적(샤프 기준, 거래≥30·MDD≤40%): 기준={th} 보유={hd}일 "
              f"→ 승률 {m['win_rate']}% 손익비 {m['profit_factor']} 누적 {m['total_return']}% "
              f"MDD {m['mdd']}% 샤프 {m['sharpe']}")
    else:
        print("\n⚠️ 거래≥30·MDD≤40% 조건을 만족하는 견고한 파라미터 없음 (엣지 약함)")


def compare_strategies(months: int, threshold: int = 60, holds=(7, 10)) -> None:
    """전략·게이트 비교 — 모멘텀 / +수급(OBV) / +국면(HMM대용) / 눌림목. 동일 데이터·기준."""
    global _MARKET
    kis = KISClient()
    data = _load_all(months)
    regime = market_regime_map(kis, months)
    mbars = _history(kis, "069500", months)        # 지수(KODEX200) → 상대강도 기준
    _MARKET = {b["date"]: b["c"] for b in mbars}
    bull_days = sum(1 for v in regime.values() if v)
    print(f"[전략 비교] {len(data)}종목 / {months}개월 / 기준 {threshold} "
          f"(상승국면 {bull_days}/{len(regime)}일)\n")
    print(f"{'전략':>18} {'보유':>4} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} {'샤프':>5}")
    variants = [
        ("모멘텀+국면", _score_levels, regime),
        ("+RSI회피+국면", _rsi_levels, regime),
        ("+상대강도+국면", _rs_levels, regime),
        ("+RSI+상대강도+국면", _combo_levels, regime),
    ]
    for label, fn, reg in variants:
        for hd in holds:
            trades = []
            for bars in data.values():
                trades += backtest_symbol(bars, threshold, hd, fn, reg)
            m = _metrics(trades)
            if m.get("trades"):
                print(f"{label:>18} {hd:>4} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                      f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5}")
            else:
                print(f"{label:>18} {hd:>4}  거래 없음")


def main() -> None:
    ap = argparse.ArgumentParser(description="소미 전략 백테스트")
    ap.add_argument("--threshold", type=int, default=40, help="진입 점수 기준")
    ap.add_argument("--hold", type=int, default=10, help="최대 보유 거래일")
    ap.add_argument("--months", type=int, default=12, help="검증 기간(개월)")
    ap.add_argument("--scan", action="store_true", help="여러 임계값 비교")
    ap.add_argument("--grid", action="store_true", help="기준×보유기간 그리드 (데이터 1회 로드)")
    ap.add_argument("--compare", action="store_true", help="모멘텀 vs 눌림목 전략 비교")
    args = ap.parse_args()

    if args.compare:
        compare_strategies(args.months, args.threshold)
    elif args.grid:
        grid(args.months)
    elif args.scan:
        print(f"[백테스트 스캔] {len(UNIVERSE)}종목 / {args.months}개월 / 보유 {args.hold}일")
        print(f"{'기준':>4} {'거래':>5} {'승률':>6} {'평균익':>7} {'평균손':>7} {'손익비':>6} {'누적%':>7} {'MDD%':>7} {'샤프':>5}")
        for th in (30, 40, 50, 60):
            m = run(th, args.hold, args.months)
            if m.get("trades"):
                print(f"{th:>4} {m['trades']:>5} {m['win_rate']:>5}% {m['avg_win']:>6}% {m['avg_loss']:>6}% "
                      f"{m['profit_factor']:>6} {m['total_return']:>6}% {m['mdd']:>6}% {m['sharpe']:>5}")
            else:
                print(f"{th:>4}  거래 없음")
    else:
        import json
        print(json.dumps(run(args.threshold, args.hold, args.months), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
