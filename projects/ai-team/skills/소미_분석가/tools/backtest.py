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


def _high52_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """52주 신고가 돌파(웹 연구 2026-07-05, 검증 후보): 종가가 최근 250거래일 고가 98%+ 돌파일 때만
    소미 점수 통과 — 기관 상승추세·신고가 편승(웹: historically best-performing). 소미 수급확인과 시너지 기대.
    ⚠️ 검증 전 실채택 금지 — backtest --compare로 기존 전략 대비 우위 확인 후에만 도입."""
    score, e, s, tg = _score_levels(bars, t)
    if score <= 0:
        return 0, e, s, tg
    win = bars[max(0, t - 250):t + 1]
    if len(win) < 60:  # 52주 데이터 부족 시 진입 안 함
        return 0, e, s, tg
    hi52 = max(b["h"] for b in win)
    if bars[t]["c"] >= hi52 * 0.98:   # 52주 고가 98%+ 돌파만 통과
        return score, e, s, tg
    return 0, e, s, tg


def _breakout_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """거래량 동반 20일 고가 돌파(turtle/Donchian, 웹 연구 2026-07-05, 검증 후보): 종가가 직전 20거래일
    고가 상향 돌파 + 당일 거래량 ≥ 직전 20일 평균 1.5배일 때만 소미 점수 통과 — 검증 돌파(웹: 승률 60~70%).
    52주(장기)와 다른 단기 돌파+거래량 확인 축. ⚠️ 검증 결과(2026-07-05): 약·기간의존 — 12mo -24.2%(기존
    +20.7%보다 열위), 24mo +43.5%. 52주 신고가(12mo +74.6%/24mo +203%)에 크게 못 미침 → 실거래 승격 금지."""
    score, e, s, tg = _score_levels(bars, t)
    if score <= 0:
        return 0, e, s, tg
    prior = bars[max(0, t - 20):t]   # 직전 20거래일(당일 t 제외, 무미래참조)
    if len(prior) < 20:
        return 0, e, s, tg
    prior_high = max(b["h"] for b in prior)
    vols = [b["v"] for b in prior if b["v"]]
    avg_vol = sum(vols) / len(vols) if vols else 0
    if avg_vol and bars[t]["c"] > prior_high and bars[t]["v"] >= avg_vol * 1.5:
        return score, e, s, tg
    return 0, e, s, tg


def _breakout_rs_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """거래량 돌파 + 상대강도(주도주) — 돌파를 시장 초과 강세 종목으로 한정(개선 실험 2026-07-05)."""
    score, e, s, tg = _breakout_levels(bars, t)
    if score <= 0:
        return 0, e, s, tg
    return (score if _rel_strength_ok(bars, t) else 0), e, s, tg


def _breakout_combo_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """거래량 돌파 + 상대강도 + RSI 과매수(>70) 회피 — 주도주 중 블로우오프 고점만 제외(개선 실험 2026-07-05).
    ⚠️ 재검증 결과: 완화된 임계값(40)에선 최우수(24mo 샤프2.89)였으나, 실제 라이브 게이트(60)에서 재검증하니
    과필터로 거래 4~21건까지 급감 — 표본부족(`--validate` MIN_TRADES=30 미달)으로 판단 불가. 라이브 미채택
    (단순 `_breakout_levels`가 실게이트에서 유일하게 검증 통과 — advisor._volume_breakout 참고)."""
    score, e, s, tg = _breakout_levels(bars, t)
    if score <= 0 or _rsi(bars, t) > 70:
        return 0, e, s, tg
    return (score if _rel_strength_ok(bars, t) else 0), e, s, tg


def _breakout_52w_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """거래량 돌파 + 52주 신고가 근처 — 단기 돌파를 장기 신고가 국면으로 한정(개선 실험 2026-07-05)."""
    score, e, s, tg = _breakout_levels(bars, t)
    if score <= 0:
        return 0, e, s, tg
    win = bars[max(0, t - 250):t + 1]
    if len(win) < 60:
        return 0, e, s, tg
    hi52 = max(b["h"] for b in win)
    return (score if bars[t]["c"] >= hi52 * 0.98 else 0), e, s, tg


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


def _meanrev_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """역추세(과매도 반등): '떨어졌으니 오른다' 가설 — RSI<30 과매도면 진입, 평균회귀 목표."""
    c = [b["c"] for b in bars[:t + 1]]
    if len(c) < 25:
        return 0, bars[t]["c"], 0, 0
    cur = c[-1]
    ma20 = sum(c[-20:]) / 20
    recent_low = min(c[-5:])
    score = 70 if _rsi(bars, t) < 30 else 0      # 과매도 진입
    entry = cur
    stop = round(recent_low * 0.95)              # 더 빠지면(칼날) 손절
    target = round(max(ma20, cur * 1.08))        # 평균(MA20) 회귀 or +8%
    return score, entry, stop, target


# ── 과거 수급(외국인·기관 순매매) 반영 — 네이버 히스토리 병합 ──────────────
_SOOMGEUP: dict = {}  # 현재 백테스트 중인 종목의 date(YYYYMMDD)->{"inst","frgn"} (compare_soomgeup에서 종목별 설정)


def _accum_net(bars: list[dict], t: int, lookback: int = 5):
    """최근 lookback 거래일 기관+외국인 누적 순매매(주). 수급 데이터 없으면 None."""
    if not _SOOMGEUP:
        return None
    tot, hit = 0, 0
    for k in range(max(0, t - lookback + 1), t + 1):
        s = _SOOMGEUP.get(bars[k]["date"])
        if s:
            tot += s["inst"] + s["frgn"]
            hit += 1
    return tot if hit else None


def _smartmoney_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """모멘텀 + 수급확인: 기관+외국인 5일 누적 순매수>0(스마트머니 매집)일 때만 모멘텀 진입."""
    score, e, s, tg = _score_levels(bars, t)
    net = _accum_net(bars, t, 5)
    return (score if (net is not None and net > 0) else 0), e, s, tg


def _accum_levels(bars: list[dict], t: int) -> tuple[int, float, float, float]:
    """조용한 매집(선취형): 5일 순매수 강세 + 당일 등락 과하지 않음(아직 안 튐) + 추세 위/근처.
    가격이 이미 급등하지 않았는데 수급이 들어오는 '오를 것 같은' 종목을 잡는 가설."""
    c = [b["c"] for b in bars[:t + 1]]
    if len(c) < 25:
        return 0, bars[t]["c"], 0, 0
    cur, prev = c[-1], c[-2]
    chg = ((cur - prev) / prev * 100) if prev else 0
    ma20 = sum(c[-20:]) / 20
    net = _accum_net(bars, t, 5)
    score = 0
    if net is not None and net > 0 and chg <= 3 and cur >= ma20 * 0.97:
        score = 60 + min(int(net / 5_000_000 * 5), 24)  # 순매수 규모로 가점(상한 84)
    entry = cur
    stop = round(min(ma20, cur) * 0.95)
    target = round(cur * 1.10)
    return score, entry, stop, target


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
                    use_target: bool = True, bear_relstr: bool = False,
                    bear_threshold: int | None = None) -> list[dict]:
    """한 종목 walk-forward 백테스트 → 체결 리스트. levels_fn으로 전략 교체, regime_ok로 국면 게이트.
    trail_pct: 고점 대비 trail_pct 하락 시 청산(트레일링 스톱). use_target=False면 고정목표 미사용(추세 추종).
    bear_relstr=True면 하락국면에 '전량 차단' 대신 역행 강세(상대강도>지수) + bear_threshold 이상만 통과."""
    trades = []
    i = 21
    n = len(bars)
    while i < n - 1:
        is_bear = regime_ok is not None and not regime_ok.get(bars[i]["date"], True)
        if is_bear and not bear_relstr:
            i += 1                                    # 현행: 하락국면 전량 차단
            continue
        score, _entry, stop, target = levels_fn(bars, i)
        eff_threshold = threshold
        if is_bear:                                   # 제안: 하락국면엔 역행 강세 + 고점수만
            if not _rel_strength_ok(bars, i):
                i += 1
                continue
            eff_threshold = bear_threshold or threshold
        if score < eff_threshold or not (stop < bars[i]["c"] < target):
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
                       "score": score, "risk": risk,
                       "entry": round(ep), "exit": round(exit_price),
                       "ts_open": bars[i + 1]["date"], "ts_close": bars[exit_idx]["date"]})
        i = exit_idx + 1                          # 청산 후 재진입
    return trades


MIN_TRADES_SIGNIFICANT = 30  # 최소 표본(웹 연구: Bailey&López de Prado Deflated Sharpe — 표본 부족·다중검정은 성과를 부풀린다)


def _metrics(trades: list[dict], months: float | None = None) -> dict:
    """승률·손익비·누적·MDD·샤프에 소르티노(하방편차 기준)·칼마(연환산수익/MDD)·표본유의성 플래그 추가
    (웹 연구 2026-07-05: Sortino/Calmar가 Sharpe 단독보다 하방리스크를 더 정확히 포착, 최소 30건 미만은
    통계적 유의성 부족 — Deflated Sharpe Ratio 취지). months 주어지면 칼마용 연환산수익 계산."""
    if not trades:
        return {"trades": 0}
    import numpy as np
    r = np.array([t["ret"] for t in trades])
    wins = r[r > 0]; losses = r[r <= 0]
    eq = np.cumprod(1 + r)
    peak = np.maximum.accumulate(eq)
    mdd = float(((eq - peak) / peak).min() * 100)
    total_return = (float(eq[-1]) - 1) * 100
    downside = r[r < 0]
    downside_std = float(downside.std()) if len(downside) > 1 else 0.0
    sortino = round(float(r.mean() / downside_std * (252 ** 0.5)), 2) if downside_std else 0.0
    ann_return = (float(eq[-1]) ** (12 / months) - 1) * 100 if months else total_return
    calmar = round(ann_return / abs(mdd), 2) if mdd else 0.0
    return {
        "trades": len(r),
        "win_rate": round(float((r > 0).mean()) * 100, 1),
        "avg_win": round(float(wins.mean()) * 100, 2) if len(wins) else 0,
        "avg_loss": round(float(losses.mean()) * 100, 2) if len(losses) else 0,
        "profit_factor": round(float(wins.sum() / -losses.sum()), 2) if len(losses) and losses.sum() < 0 else 0,
        "total_return": round(total_return, 1),
        "mdd": round(mdd, 1),
        "sharpe": round(float(r.mean() / r.std() * (252 ** 0.5)) if r.std() else 0, 2),
        "sortino": sortino,
        "calmar": calmar,
        "significant": len(r) >= MIN_TRADES_SIGNIFICANT,
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


_STRATEGY_VARIANTS = [
    ("모멘텀+국면", _score_levels),
    ("+52주신고가+국면", _high52_levels),        # 웹 연구 후보(2026-07-05) — 검증 전용
    ("+거래량돌파+국면", _breakout_levels),        # 웹 연구 후보(2026-07-05) — 검증 전용(단순 돌파, 열위)
    ("+돌파+상대강도", _breakout_rs_levels),       # 개선 실험(2026-07-05)
    ("+돌파+상대강도+RSI", _breakout_combo_levels),  # 개선 실험(2026-07-05) — 채택(라이브 반영)
    ("+돌파+52주신고가", _breakout_52w_levels),    # 개선 실험(2026-07-05)
    ("+RSI회피+국면", _rsi_levels),
    ("+상대강도+국면", _rs_levels),
    ("+RSI+상대강도+국면", _combo_levels),
]


def _collect_variants(months: int, threshold: int, holds: tuple[int, ...]) -> dict[tuple[str, int], dict]:
    """한 기간에 대해 전 전략×보유기간 조합의 지표를 계산 — compare_strategies·validate_strategies 공유 코어."""
    global _MARKET
    kis = KISClient()
    data = _load_all(months)
    regime = market_regime_map(kis, months)
    mbars = _history(kis, "069500", months)        # 지수(KODEX200) → 상대강도 기준
    _MARKET = {b["date"]: b["c"] for b in mbars}
    out: dict[tuple[str, int], dict] = {}
    for label, fn in _STRATEGY_VARIANTS:
        for hd in holds:
            trades = []
            for bars in data.values():
                trades += backtest_symbol(bars, threshold, hd, fn, regime)
            out[(label, hd)] = _metrics(trades, months=months)
    return out, len(data), regime


def compare_strategies(months: int, threshold: int = 60, holds=(7, 10)) -> None:
    """전략·게이트 비교 — 모멘텀 / +수급(OBV) / +국면(HMM대용) / 눌림목. 동일 데이터·기준.
    소르티노(하방편차)·칼마(연환산/MDD)·표본유의성(≥30건) 병기(웹 연구 2026-07-05, 다기간 검증은
    --validate 참고 — 단일기간 결과만으로 채택 금지, MIN_TRADES_SIGNIFICANT 미달은 '표본부족' 표시)."""
    results, n_symbols, regime = _collect_variants(months, threshold, holds)
    bull_days = sum(1 for v in regime.values() if v)
    print(f"[전략 비교] {n_symbols}종목 / {months}개월 / 기준 {threshold} "
          f"(상승국면 {bull_days}/{len(regime)}일)\n")
    print(f"{'전략':>18} {'보유':>4} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} "
          f"{'샤프':>5} {'소르티노':>7} {'칼마':>5} {'표본':>5}")
    for label, _fn in _STRATEGY_VARIANTS:
        for hd in holds:
            m = results[(label, hd)]
            if m.get("trades"):
                sig = "OK" if m["significant"] else "부족"
                print(f"{label:>18} {hd:>4} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                      f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5} "
                      f"{m['sortino']:>7} {m['calmar']:>5} {sig:>5}")
            else:
                print(f"{label:>18} {hd:>4}  거래 없음")


def validate_strategies(periods: tuple[int, ...] = (12, 24), threshold: int = 60,
                         holds=(7, 10)) -> None:
    """다기간 검증 프로토콜(웹 연구 2026-07-05: walk-forward 다구간 일관성 확인이 단일 백테스트
    과최적화·선택편향의 핵심 방어 — Bailey&López de Prado 「Deflated Sharpe Ratio」, QuantInsti WFO 가이드).
    여러 기간(기본 12·24개월)에서 독립 실행 후 3단계 판정:
      ✅채택   — 전 기간 표본≥30(MIN_TRADES_SIGNIFICANT) & 흑자 & 샤프>0 모두 충족
      ❌기각   — 표본은 충분하나(≥30) 적자·샤프≤0인 기간이 있음 (성과로 증명된 열위)
      🔸보류(표본부족) — 거래 자체가 부족(<30)해 판단 불가 (성과가 아니라 데이터 부족 — 기각과 구분)
    표본부족을 기각과 뭉뚱그리면 실제로는 '증명 안 됨'인 전략을 '나쁨'으로 오판한다(DSR 취지)."""
    per_period = {}
    for months in periods:
        per_period[months] = _collect_variants(months, threshold, holds)[0]
    print(f"[다기간 검증] 기간={periods}개월 / 기준 {threshold}\n")
    header = f"{'전략':>18} {'보유':>4}" + "".join(f" {'%d개월누적%%' % p:>12}" for p in periods) + f" {'판정':>14}"
    print(header)
    for label, _fn in _STRATEGY_VARIANTS:
        for hd in holds:
            cells = []
            any_trades = False
            insufficient = False
            underperform = False
            for p in periods:
                m = per_period[p].get((label, hd), {})
                if not m.get("trades"):
                    cells.append("거래없음")
                    insufficient = True
                    continue
                any_trades = True
                cells.append(f"{m['total_return']:+.1f}%(샤프{m['sharpe']})")
                if not m["significant"]:
                    insufficient = True
                elif not (m["total_return"] > 0 and m["sharpe"] > 0):
                    underperform = True
            if not any_trades or (insufficient and not underperform):
                verdict = "🔸보류(표본부족)"
            elif underperform:
                verdict = "❌기각(성과미달)"
            else:
                verdict = "✅채택"
            row = f"{label:>18} {hd:>4}" + "".join(f" {c:>12}" for c in cells) + f" {verdict:>14}"
            print(row)


def compare_bear_gate(months: int, threshold: int = 60, hold: int = 10, bear_threshold: int = 70) -> None:
    """하락국면 게이트 비교 — 전량차단(현행) vs 역행강세 선별통과(제안) vs 게이트없음. 동일 데이터·기준."""
    global _MARKET
    kis = KISClient()
    data = _load_all(months)
    regime = market_regime_map(kis, months)
    mbars = _history(kis, "069500", months)            # 지수(KODEX200) → 상대강도 기준
    _MARKET = {b["date"]: b["c"] for b in mbars}
    bear_days = sum(1 for v in regime.values() if not v)
    print(f"[하락국면 게이트 비교] {len(data)}종목 / {months}개월 / 기준 {threshold} / 보유 {hold}일 "
          f"(하락국면 {bear_days}/{len(regime)}일, bear선별 기준 {bear_threshold})\n")
    print(f"{'전략':>26} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} {'샤프':>5}")
    configs = [
        ("게이트없음(하락장도 매수)", dict(regime_ok=None)),
        ("현행: 하락장 전량차단", dict(regime_ok=regime)),
        (f"제안: 하락장 역행강세선별", dict(regime_ok=regime, bear_relstr=True, bear_threshold=bear_threshold)),
    ]
    for label, kw in configs:
        trades = []
        for bars in data.values():
            trades += backtest_symbol(bars, threshold, hold, _score_levels, **kw)
        m = _metrics(trades)
        if m.get("trades"):
            print(f"{label:>26} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                  f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5}")
        else:
            print(f"{label:>26}  거래 없음")


def compare_soomgeup(months: int, threshold: int = 60, hold: int = 10, pages: int = 14) -> None:
    """과거 수급(네이버 외국인·기관 순매매) 병합 — 모멘텀 vs 모멘텀+수급확인 vs 조용한매집 비교."""
    global _SOOMGEUP, _MARKET
    import soomgeup_history
    kis = KISClient()
    data = _load_all(months)
    regime = market_regime_map(kis, months)
    mbars = _history(kis, "069500", months)
    _MARKET = {b["date"]: b["c"] for b in mbars}
    soom = {}
    for code in data:
        soom[code] = soomgeup_history.fetch(code, pages)
    covered = sum(1 for s in soom.values() if s)
    avg_days = (sum(len(s) for s in soom.values()) / max(1, covered)) if covered else 0
    print(f"[수급 반영 비교] {len(data)}종목 / {months}개월 / 기준 {threshold} / 보유 {hold}일 "
          f"(수급 수집 {covered}/{len(data)}종목·평균 {avg_days:.0f}일)\n")
    print(f"{'전략':>22} {'거래':>5} {'승률':>6} {'손익비':>6} {'누적%':>8} {'MDD%':>7} {'샤프':>5}")
    variants = [
        ("모멘텀(현행)", _score_levels),
        ("모멘텀+수급확인", _smartmoney_levels),
        ("조용한매집(선취)", _accum_levels),
    ]
    for label, fn in variants:
        trades = []
        for code, bars in data.items():
            _SOOMGEUP = soom.get(code, {})
            trades += backtest_symbol(bars, threshold, hold, fn, regime)
        _SOOMGEUP = {}
        m = _metrics(trades)
        if m.get("trades"):
            print(f"{label:>22} {m['trades']:>5} {m['win_rate']:>5}% {m['profit_factor']:>6} "
                  f"{m['total_return']:>7}% {m['mdd']:>6}% {m['sharpe']:>5}")
        else:
            print(f"{label:>22}  거래 없음")


def seed_sample(months: int = 9, pages: int = 12, threshold: int = 60, hold: int = 20) -> None:
    """검증된 '모멘텀+수급확인' 전략을 과거 자료에 돌려 청산거래를 생성하고
    somi_closed_trades.json 에 주입 — 성과추적 표본을 즉시 확보(과거 자료 기반)."""
    global _SOOMGEUP, _MARKET
    import json
    import soomgeup_history
    kis = KISClient()
    regime = market_regime_map(kis, months)
    mbars = _history(kis, "069500", months)
    _MARKET = {b["date"]: b["c"] for b in mbars}

    def _fmt(s: str) -> str:
        return f"{s[:4]}-{s[4:6]}-{s[6:]}" if len(s) == 8 else s

    records = []
    for code, name in UNIVERSE.items():
        bars = _history(kis, code, months)
        if len(bars) < 30:
            continue
        _SOOMGEUP = soomgeup_history.fetch(code, pages)
        for t in backtest_symbol(bars, threshold, hold, _smartmoney_levels, regime):
            records.append({
                "symbol": code, "name": name, "entry": t["entry"], "exit": t["exit"],
                "qty": 1, "ret_pct": round(t["ret"] * 100, 2), "reason": t["reason"],
                "score": t["score"], "ts_open": _fmt(t["ts_open"]), "ts_close": _fmt(t["ts_close"]),
                "source": "backtest_seed",  # 과거자료 주입분 표시(라이브 실거래 아님)
            })
    _SOOMGEUP = {}
    records.sort(key=lambda r: r["ts_close"])
    out = PROJECT_ROOT / "output" / "cache" / "somi_closed_trades.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    wins = sum(1 for r in records if r["ret_pct"] > 0)
    print(f"표본 주입 완료: {len(records)}건 (승 {wins} / 패 {len(records)-wins}) → {out}")


def main() -> None:
    ap = argparse.ArgumentParser(description="소미 전략 백테스트")
    ap.add_argument("--threshold", type=int, default=40, help="진입 점수 기준")
    ap.add_argument("--hold", type=int, default=10, help="최대 보유 거래일")
    ap.add_argument("--months", type=int, default=12, help="검증 기간(개월)")
    ap.add_argument("--scan", action="store_true", help="여러 임계값 비교")
    ap.add_argument("--grid", action="store_true", help="기준×보유기간 그리드 (데이터 1회 로드)")
    ap.add_argument("--compare", action="store_true", help="모멘텀 vs 눌림목 전략 비교")
    ap.add_argument("--validate", action="store_true",
                     help="다기간(기본 12·24개월) 자동 검증 — 전 기간 표본≥30·흑자·샤프>0 일치해야 채택 판정")
    ap.add_argument("--beargate", action="store_true", help="하락국면 전량차단 vs 역행강세 선별통과 비교")
    ap.add_argument("--bear-threshold", type=int, default=70, help="하락장 선별통과 점수 기준")
    ap.add_argument("--soomgeup", action="store_true", help="과거 수급 병합 — 모멘텀 vs 수급확인 vs 조용한매집 비교")
    ap.add_argument("--pages", type=int, default=14, help="네이버 수급 수집 페이지(≈20일/페이지)")
    ap.add_argument("--seed-sample", action="store_true", help="검증전략을 과거자료에 돌려 성과추적 표본 주입")
    args = ap.parse_args()

    if args.seed_sample:
        seed_sample(args.months, args.pages, args.threshold if args.threshold != 40 else 60, args.hold)
    elif args.soomgeup:
        compare_soomgeup(args.months, args.threshold if args.threshold != 40 else 60, args.hold, args.pages)
    elif args.beargate:
        compare_bear_gate(args.months, args.threshold if args.threshold != 40 else 60,
                          args.hold, args.bear_threshold)
    elif args.compare:
        # 기존 버그: args.threshold 기본값(40)을 그대로 넘겨 실제 라이브 게이트(60)와 다른 임계값으로
        # 검증되던 불일치를 발견(2026-07-05, --validate 신설 중). 다른 서브커맨드와 동일하게 40→60 치환.
        compare_strategies(args.months, args.threshold if args.threshold != 40 else 60)
    elif args.validate:
        validate_strategies(threshold=args.threshold if args.threshold != 40 else 60)
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
