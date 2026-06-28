#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Somi stock squeeze / momentum scoring tool."""

from __future__ import annotations

import argparse
import os
import re
import sys
from datetime import datetime
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


PROJECT_ROOT = Path(__file__).resolve().parents[4]


def to_num(value: object) -> float:
    if value is None:
        return 0.0
    cleaned = re.sub(r"[^\d.\-]", "", str(value))
    if not cleaned or cleaned in {"-", ".", "-."}:
        return 0.0
    try:
        return float(cleaned)
    except ValueError:
        return 0.0


def parse_input_text(text: str) -> dict[str, str]:
    data: dict[str, str] = {}
    aliases = {
        "date": ["날짜"],
        "stock_name": ["종목명"],
        "stock_code": ["종목코드"],
        "open": ["시가"],
        "high": ["고가"],
        "low": ["저가"],
        "close": ["종가", "현재가"],
        "change_pct": ["등락률"],
        "volume": ["거래량"],
        "avg_volume_20d": ["20일 평균 거래량"],
        "trading_value": ["거래대금"],
        "recent_price_flow": ["최근 5일 주가 흐름"],
        "recent_volume_flow": ["최근 5일 거래량 흐름"],
        "recent_investor_flow": ["최근 5일 외국인기관 수급"],
        "buy_indiv": ["개인 순매수"],
        "buy_foreigner": ["외국인 순매수"],
        "buy_institution": ["기관 순매수"],
        "buy_indiv_5d": ["최근 5일 개인 누적"],
        "buy_foreigner_5d": ["최근 5일 외국인 누적"],
        "buy_institution_5d": ["최근 5일 기관 누적"],
        "foreign_holding": ["외국인 보유수량"],
        "foreign_holding_rate": ["외국인 보유율"],
        "program_trading": ["프로그램 매매"],
        "loan_balance": ["대차잔고"],
        "loan_balance_rate": ["대차잔고율"],
        "prev_loan_balance": ["전일 대차잔고"],
        "loan_repay": ["대차상환수량"],
        "loan_agree": ["대차체결수량"],
        "short_volume": ["공매도 거래량", "직전 공매도 체결수량"],
        "short_ratio": ["공매도 비중"],
        "short_avg_price": ["공매도 평균가"],
        "kospi_change": ["코스피 등락률"],
        "kosdaq_change": ["코스닥 등락률"],
        "market_warning": ["시장경보 여부", "시장경보"],
        "theme": ["테마"],
        "news_sentiment": ["관련 뉴스", "뉴스 분위기"],
        "cb_notice": ["CB/BW/유상증자/보호예수 이슈", "CB 공시"],
        "conversion_price": ["전환가"],
        "refixing_possible": ["리픽싱 가능 여부"],
        "support_line": ["주요 지지선"],
        "resistance_line": ["주요 저항선"],
        "investor_avg_price": ["내 평단", "평단"],
        "holding_ratio": ["내 보유 비중", "보유 비중"],
        "cash": ["추가 예수금", "예수금"],
        "intraday_feature": ["장중 특징"],
    }
    for key, labels in aliases.items():
        value = ""
        for label in labels:
            match = re.search(rf"^{re.escape(label)}[ \t]*:[ \t]*(.*)$", text, re.MULTILINE)
            if match:
                value = match.group(1).strip()
                break
        data[key] = value
    return data


def calculate_score(raw: dict[str, str]) -> tuple[int, str, list[str], list[str]]:
    close = to_num(raw.get("close"))
    open_p = to_num(raw.get("open"))
    high = to_num(raw.get("high"))
    low = to_num(raw.get("low"))
    change_pct = to_num(raw.get("change_pct"))
    volume = to_num(raw.get("volume"))
    avg_volume = to_num(raw.get("avg_volume_20d"))
    trading_value = to_num(raw.get("trading_value"))
    foreigner = to_num(raw.get("buy_foreigner"))
    institution = to_num(raw.get("buy_institution"))
    individual = to_num(raw.get("buy_indiv"))
    foreigner_5d = to_num(raw.get("buy_foreigner_5d"))
    institution_5d = to_num(raw.get("buy_institution_5d"))
    individual_5d = to_num(raw.get("buy_indiv_5d"))
    foreign_rate = to_num(raw.get("foreign_holding_rate"))
    loan_rate = to_num(raw.get("loan_balance_rate"))
    short_volume = to_num(raw.get("short_volume"))
    support = to_num(raw.get("support_line"))
    resistance = to_num(raw.get("resistance_line"))
    intraday = raw.get("intraday_feature", "")
    warning = raw.get("market_warning", "")

    score = 0
    pos: list[str] = []
    neg: list[str] = []
    vol_ratio = volume / avg_volume if avg_volume else 0

    if vol_ratio >= 5:
        score += 25
        pos.append(f"거래량이 20일 평균 대비 {vol_ratio:.1f}배로 강하게 증가")
    elif vol_ratio >= 3:
        score += 18
        pos.append(f"거래량이 20일 평균 대비 {vol_ratio:.1f}배")
    elif vol_ratio >= 1.5:
        score += 8
        pos.append(f"거래량이 평균 대비 {vol_ratio:.1f}배")
    elif volume and avg_volume and vol_ratio < 0.7:
        score -= 8
        neg.append(f"거래량이 20일 평균의 {vol_ratio:.1f}배로 약함")

    if change_pct >= 10:
        score += 22
        pos.append(f"등락률 {change_pct:+.2f}% 급등")
    elif change_pct >= 5:
        score += 14
        pos.append(f"등락률 {change_pct:+.2f}% 강세")
    elif change_pct >= 2:
        score += 6
        pos.append(f"등락률 {change_pct:+.2f}% 상승")
    elif change_pct <= -7:
        score -= 18
        neg.append(f"등락률 {change_pct:+.2f}% 급락")
    elif change_pct <= -3:
        score -= 8
        neg.append(f"등락률 {change_pct:+.2f}% 약세")

    if high and close and change_pct > 0 and (high - close) / high <= 0.01:
        score += 8
        pos.append("종가가 고가권에 붙어 마감")
    if high and low and close and change_pct > 0 and (high - close) / max(1, high - low) >= 0.5:
        score -= 8
        neg.append("윗꼬리가 길어 물량 출회 가능성")

    if foreigner > 0:
        score += 8
        pos.append(f"외국인 순매수 {foreigner:,.0f}주")
    if institution > 0:
        score += 8
        pos.append(f"기관 순매수 {institution:,.0f}주")
    if individual > 0 and foreigner <= 0 and institution <= 0:
        score -= 6
        neg.append("개인 중심 수급으로 추격 매수 위험")

    if foreigner_5d > 0:
        score += 12
        pos.append(f"최근 5일 외국인 누적 순매수 {foreigner_5d:,.0f}주")
    elif foreigner_5d < 0:
        score -= 6
        neg.append(f"최근 5일 외국인 누적 순매도 {abs(foreigner_5d):,.0f}주")

    if institution_5d > 0:
        score += 10
        pos.append(f"최근 5일 기관 누적 순매수 {institution_5d:,.0f}주")
    elif institution_5d < 0:
        score -= 5
        neg.append(f"최근 5일 기관 누적 순매도 {abs(institution_5d):,.0f}주")

    if individual_5d > 0 and foreigner_5d <= 0 and institution_5d <= 0:
        score -= 8
        neg.append(f"최근 5일 개인만 순매수 {individual_5d:,.0f}주 - 수급 약세 신호")

    if foreign_rate >= 5:
        score += 4
        pos.append(f"외국인 보유율 {foreign_rate:.2f}%")

    if loan_rate >= 3:
        score += 8
        pos.append(f"대차잔고율 {loan_rate:.2f}%로 숏커버링 관찰 가치 있음")
    elif loan_rate > 0:
        score += 3
        pos.append(f"대차잔고율 {loan_rate:.2f}% 확인")
    if short_volume > 0:
        score += 5
        pos.append(f"직전 공매도 체결수량 {short_volume:,.0f}주")

    if support and close and close < support:
        score -= 15
        neg.append(f"주가가 주요 지지선 {support:,.0f}원 아래")
    elif support and low and low <= support <= close:
        score += 6
        pos.append(f"주요 지지선 {support:,.0f}원 방어")
    if resistance and close and close >= resistance:
        score += 8
        pos.append(f"주요 저항선 {resistance:,.0f}원 돌파/근접")

    if trading_value >= 50_000_000_000:
        score += 8
        pos.append(f"거래대금 {trading_value:,.0f}원으로 유동성 충분")
    elif trading_value and trading_value < 5_000_000_000:
        score -= 5
        neg.append("거래대금이 작아 신뢰도 낮음")

    if "투자주의" in warning or "경고" in warning:
        score -= 10
        neg.append(f"시장경보: {warning}")
    if any(word in intraday for word in ["대차 감소", "상환 우위", "외국인 연속", "기관 연속"]):
        score += 8
        pos.append(f"장중 특징: {intraday}")

    final = max(0, min(100, int(round(score))))
    return final, grade_of(final), pos, neg


def grade_of(score: int) -> str:
    if score >= 80:
        return "강함"
    if score >= 60:
        return "중간 이상"
    if score >= 40:
        return "관찰"
    if score >= 20:
        return "약함"
    return "매우 약함"


def _v(raw: dict[str, str], key: str, default: str = "확인 필요") -> str:
    return raw.get(key, "").strip() or default


def generate_prediction(raw: dict[str, str], score: int) -> str:
    close = to_num(raw.get("close"))
    change_pct = to_num(raw.get("change_pct"))
    volume = to_num(raw.get("volume"))
    avg_volume = to_num(raw.get("avg_volume_20d"))
    foreigner = to_num(raw.get("buy_foreigner"))
    institution = to_num(raw.get("buy_institution"))
    foreigner_5d = to_num(raw.get("buy_foreigner_5d"))
    institution_5d = to_num(raw.get("buy_institution_5d"))
    support = to_num(raw.get("support_line"))
    resistance = to_num(raw.get("resistance_line"))
    vol_ratio = volume / avg_volume if avg_volume else 0

    flow_score = 0
    if close and support and close < support:
        flow_score -= 3
    elif close and support and close >= support:
        flow_score += 1
    if close and resistance and close >= resistance:
        flow_score += 2
    if change_pct >= 3:
        flow_score += 2
    elif change_pct <= -3:
        flow_score -= 2
    if vol_ratio >= 1.5:
        flow_score += 1
    elif volume and avg_volume and vol_ratio < 0.7:
        flow_score -= 1
    if foreigner > 0:
        flow_score += 1
    elif foreigner < 0:
        flow_score -= 1
    if institution > 0:
        flow_score += 1
    elif institution < 0:
        flow_score -= 1
    if foreigner_5d > 0:
        flow_score += 1
    elif foreigner_5d < 0:
        flow_score -= 1
    if institution_5d > 0:
        flow_score += 1
    elif institution_5d < 0:
        flow_score -= 1
    if score >= 60:
        flow_score += 1
    elif score < 30:
        flow_score -= 1

    if flow_score >= 4:
        direction = "상승 우세"
    elif flow_score <= -3:
        direction = "하락 우세"
    else:
        direction = "중립"

    support_text = f"{support:,.0f}원" if support else "확인 필요"
    resistance_text = f"{resistance:,.0f}원" if resistance else "확인 필요"
    close_text = f"{close:,.0f}원" if close else "확인 필요"

    if direction == "상승 우세":
        range_text = f"지지선 {support_text} 방어 시 저항선 {resistance_text} 위 안착을 시도할 가능성"
        upside = f"상방 목표: {resistance_text} 위에서 거래량이 20일 평균 이상 유지되는지 확인"
        downside = f"하방 위험: {support_text} 이탈 또는 외국인/기관 동반 매도 전환"
        invalidation = f"무효화 조건: 종가가 {support_text} 아래로 밀리며 거래량이 증가"
    elif direction == "하락 우세":
        range_text = f"현재가 {close_text} 기준 지지선 {support_text} 회복 전까지 약세 지속 가능성"
        upside = f"상방 전환 조건: {support_text} 회복 후 거래량이 20일 평균 이상 유지"
        downside = "하방 위험: 외국인/기관 순매도 지속 시 최근 저점 재확인 가능"
        invalidation = f"무효화 조건: 외국인 순매수 전환과 {resistance_text} 돌파"
    else:
        range_text = f"{support_text}~{resistance_text} 박스권 확인 구간"
        upside = f"상방 전환 조건: {resistance_text} 돌파와 거래량 증가"
        downside = f"하방 위험: {support_text} 이탈과 수급 악화"
        invalidation = "무효화 조건: 방향성 신호가 약해 장 마감 수급 재확인 필요"

    return f"""## 3. 단기 예측

* 기간: 다음 거래일 ~ 5거래일
* 방향성: {direction}
* 예상 구간: {range_text}
* {upside}
* {downside}
* {invalidation}
* 본 예측은 현재 KIS 가격·거래량·수급·지지/저항 데이터 기반 조건부 시나리오이며, 매수·매도 지시가 아닙니다.
"""


def generate_smart_money(raw: dict[str, str]) -> str:
    """5일 수급 주체·거래량·프로그램매매를 종합한 세력 동향 분석."""
    foreigner_5d = to_num(raw.get("buy_foreigner_5d"))
    institution_5d = to_num(raw.get("buy_institution_5d"))
    indiv_5d = to_num(raw.get("buy_indiv_5d"))
    program = to_num(raw.get("program_trading"))
    vol = to_num(raw.get("volume"))
    avg_vol = to_num(raw.get("avg_volume_20d"))
    vol_ratio = vol / avg_vol if avg_vol else 0
    smart_5d = foreigner_5d + institution_5d

    if smart_5d > 0 and indiv_5d < 0:
        verdict = "매집 우위 (외국인·기관 순매수 / 개인 순매도)"
    elif smart_5d > 0:
        verdict = "순매수 우위 (외국인·기관 자금 유입)"
    elif smart_5d < 0 and indiv_5d > 0:
        verdict = "이탈 우위 (외국인·기관 순매도 / 개인 순매수)"
    elif smart_5d < 0:
        verdict = "순매도 우위 (외국인·기관 자금 이탈)"
    else:
        verdict = "중립 (뚜렷한 세력 수급 신호 없음)"

    if vol_ratio >= 2:
        vol_note = f"거래량 평균 대비 {vol_ratio:.1f}배 급증 → 세력 개입 정황 강함"
    elif vol_ratio >= 1.5:
        vol_note = f"거래량 평균 대비 {vol_ratio:.1f}배 증가 → 관심 유입"
    elif avg_vol:
        vol_note = f"거래량 평균 대비 {vol_ratio:.1f}배 → 특이 동향 없음"
    else:
        vol_note = "거래량 비교 데이터 부족"

    if program > 0:
        prog_note = f"프로그램 순매수 {program:,.0f} (기관성 매수 유입)"
    elif program < 0:
        prog_note = f"프로그램 순매도 {program:,.0f} (기관성 매도 출회)"
    else:
        prog_note = "프로그램 매매 영향 미미"

    def _qty(value: float) -> str:
        return f"{value:+,.0f}주" if value else "0주"

    return f"""## 1-1. 세력 동향

* 세력 판단: {verdict}
* 5일 누적 수급: 외국인 {_qty(foreigner_5d)} / 기관 {_qty(institution_5d)} / 개인 {_qty(indiv_5d)}
* 거래량 신호: {vol_note}
* 프로그램 매매: {prog_note}
* 보조 지표: 외국인 보유율 {_v(raw, "foreign_holding_rate")} / 공매도 비중 {_v(raw, "short_ratio")}
* 해석: 외국인·기관 합산 5일 순매수는 {_qty(smart_5d)}. 단 5일 누적만으론 고점 분산·숏커버를 놓칠 수 있으니 '1-2 공매도·대차 정밀 분석'을 함께 보세요.
"""


def flow_short_analysis(investor_daily, short_daily, support, close, loan_rate, prev_loan_rate=None):
    """일자별 수급·공매도·대차 추이 종합 — 고점분산/공매도추세/숏커버 판별 + 점수 보정.
    investor_daily, short_daily는 최신순 리스트. 반환: {text, delta, pos, neg, verdict, signals}."""
    support = to_num(support)
    close = to_num(close)
    lr = to_num(loan_rate) if loan_rate not in (None, "") else None
    lines: list[str] = []
    pos: list[str] = []
    neg: list[str] = []
    signals: list[str] = []
    delta = 0

    # 1) 고점 분산 — 외국인 매도·개인 매수가 고가권에 몰렸나
    rows = [r for r in (investor_daily or []) if to_num(r.get("stck_clpr"))]
    if len(rows) >= 5:
        closes = [to_num(r.get("stck_clpr")) for r in rows]
        hi, lo = max(closes), min(closes)
        if hi > lo:
            band = lo + (hi - lo) * 0.6  # 상위 40% 가격대 = 고가권
            hz = [r for r in rows if to_num(r.get("stck_clpr")) >= band]
            f_hi = sum(to_num(r.get("frgn_ntby_qty") or r.get("frgn_ntby_vol")) for r in hz)
            p_hi = sum(to_num(r.get("prsn_ntby_qty") or r.get("prsn_ntby_vol")) for r in hz)
            if f_hi < 0 and p_hi > 0:
                delta -= 10
                signals.append("고점분산")
                neg.append(f"고점 분산 — 고가권({band:,.0f}원↑)서 외국인 {f_hi:,.0f}주 매도·개인 {p_hi:,.0f}주 매수")
                lines.append(f"⚠️ 고점 분산 감지: 고가권({band:,.0f}원 이상)에서 외국인 {f_hi:,.0f}주 순매도, "
                             f"개인 {p_hi:,.0f}주 순매수 → 개인이 고점 물량을 받은 구조")

    # 2) 공매도 추세 — 누적 증가 + 평균 비중
    sd = [r for r in (short_daily or []) if to_num(r.get("acml_ssts_cntg_qty"))]
    if len(sd) >= 5:
        recent = sd[:5]
        avg_ratio = sum(to_num(r.get("ssts_vol_rlim")) for r in recent) / 5
        add5 = to_num(sd[0].get("acml_ssts_cntg_qty")) - to_num(sd[4].get("acml_ssts_cntg_qty"))
        rising = add5 > 0
        if rising and avg_ratio >= 5:
            delta -= 12
            signals.append("공매도증가")
            neg.append(f"공매도 압력 지속 — 5일 누적공매도 +{add5:,.0f}주(평균 비중 {avg_ratio:.1f}%)")
            lines.append(f"🔻 공매도 압력 지속: 최근 5일 누적공매도 +{add5:,.0f}주, 평균 비중 {avg_ratio:.1f}% "
                         f"→ 하락을 끌고 내려가는 중")
        elif avg_ratio < 3 or not rising:
            delta += 6
            signals.append("공매도둔화")
            pos.append(f"공매도 둔화 — 평균 비중 {avg_ratio:.1f}%, 누적 증가 둔화")
            lines.append(f"🟢 공매도 둔화: 평균 비중 {avg_ratio:.1f}%, 누적 증가 둔화 → 숏 압력 약화")
        else:
            lines.append(f"공매도 비중 평균 {avg_ratio:.1f}% (보통 수준)")

    # 3) 대차잔고 해석 + 전일 대비 추이
    if lr is not None:
        trend = ""
        if prev_loan_rate not in (None, ""):
            pr = to_num(prev_loan_rate)
            if lr > pr:
                trend = f" (전일 {pr:.2f}%→증가: 신규 공매도 유입)"
                signals.append("대차증가")
            elif lr < pr:
                trend = f" (전일 {pr:.2f}%→감소: 숏커버/상환 우위)"
                signals.append("대차감소")
        if lr < 1 and "공매도증가" in signals:
            signals.append("숏커버소진")
            lines.append(f"대차잔고율 {lr:.2f}% 낮음{trend} — 빌린 물량 대부분 상환됨. "
                         f"외국인 순매수가 진짜 매집이 아니라 '숏커버 되사기'일 수 있어 반등 동력 제한")
        elif lr >= 3:
            lines.append(f"대차잔고율 {lr:.2f}% 높음{trend} — 미상환 공매도 잔존, 숏커버 시 반등 트리거 가능")
        else:
            lines.append(f"대차잔고율 {lr:.2f}%{trend}")

    # 4) 종합 판세
    below = bool(support and close and close < support)
    above = bool(support and close and close >= support)
    bearish = ("공매도증가" in signals) or ("고점분산" in signals)
    if bearish and below:
        verdict = "공매도 우위 — 하락 지속 위험. 외국인이 받쳐도 아직 '지는 받아먹기'. 신규 매수 보류."
    elif "공매도둔화" in signals and above:
        verdict = "받아먹기 우위 가능성 — 공매도 둔화 + 지지선 위. 반등 시도 관찰."
    else:
        verdict = "혼조 — 방향 확정 전. 관망."

    # 바닥 전환 체크리스트
    chk = [("✅" if "공매도둔화" in signals else "❌") + " 공매도 비중 꺾임(3%↓)"]
    if "대차감소" in signals:
        chk.append("✅ 대차잔고 감소(숏커버 진행)")
    elif "숏커버소진" in signals:
        chk.append("➖ 대차 소진(되사기 한계)")
    else:
        chk.append("❌ 대차잔고 감소 신호")
    if support:
        chk.append(("✅" if above else "❌") + f" 지지선({support:,.0f}원) 회복")

    body = "\n".join(f"* {l}" for l in lines) if lines else "* 일자별 추이 데이터 부족"
    text = ("## 1-2. 공매도·대차 정밀 분석\n\n" + body +
            f"\n* 종합 판세: {verdict}\n* 바닥 전환 체크: " + " / ".join(chk))
    return {"text": text, "delta": delta, "pos": pos, "neg": neg, "verdict": verdict, "signals": signals}


def generate_report(raw: dict[str, str], score: int, grade: str, pos: list[str], neg: list[str]) -> str:
    stock_name = _v(raw, "stock_name")
    stock_code = _v(raw, "stock_code", "")
    label = f"{stock_name}({stock_code})" if stock_code else stock_name
    vol = to_num(raw.get("volume"))
    avg_vol = to_num(raw.get("avg_volume_20d"))
    vol_ratio = vol / avg_vol if avg_vol else 0
    close = to_num(raw.get("close"))
    support = to_num(raw.get("support_line"))
    resistance = to_num(raw.get("resistance_line"))

    if score >= 60:
        decision = "분할 관찰 가능"
    elif score >= 40:
        decision = "관찰 우선"
    else:
        decision = "신규 매수 보류"

    if support and close and close < support:
        scenario = "지지선 이탈 상태라 반등보다 손실 방어가 우선입니다."
    elif resistance and close and close >= resistance:
        scenario = "저항선 돌파/근접 구간입니다. 거래량 유지 여부를 다음 캔들에서 확인해야 합니다."
    elif score >= 60:
        scenario = "수급 전환 가능성은 있으나 대차/공매도 후속 데이터 확인이 필요합니다."
    else:
        scenario = "숏커버링으로 단정하기에는 재료가 부족합니다."

    pos_lines = "\n".join(f"* {item}" for item in pos) if pos else "* 확인된 긍정 신호 없음"
    neg_lines = "\n".join(f"* {item}" for item in neg) if neg else "* 확인된 위험 신호 없음"
    smart_money = generate_smart_money(raw)
    prediction = generate_prediction(raw, score)

    return f"""[종목 간단 분석 리포트]

종목: {label}
테마: {_v(raw, "theme")}

## 1. 현재 판세 진단

* 주가 흐름: 시가 {_v(raw, "open")} / 고가 {_v(raw, "high")} / 저가 {_v(raw, "low")} / 종가 {_v(raw, "close")} / 등락률 {_v(raw, "change_pct")}
* 최근 5일 흐름: {_v(raw, "recent_price_flow")}
* 거래량과 거래대금: 거래량 {_v(raw, "volume")} / 20일 평균 {_v(raw, "avg_volume_20d")} / 평균 대비 {vol_ratio:.1f}배 / 거래대금 {_v(raw, "trading_value")}
* 수급: 개인 {_v(raw, "buy_indiv")} / 외국인 {_v(raw, "buy_foreigner")} / 기관 {_v(raw, "buy_institution")} / 외국인 보유율 {_v(raw, "foreign_holding_rate")}
* 대차/공매도: 대차잔고율 {_v(raw, "loan_balance_rate")} / 직전 공매도 체결수량 {_v(raw, "short_volume")} / 공매도 비중 {_v(raw, "short_ratio")}
* 지지선과 저항선: 지지선 {_v(raw, "support_line")} / 저항선 {_v(raw, "resistance_line")}
* 시장경보: {_v(raw, "market_warning")}

{smart_money}
## 2. 매수 판단

* 결론: {decision}
* 큰 수익 가능성 점수: {score}점 / 100점
* 등급: {grade}
* 판단: {scenario}

{prediction}

## 4. 체크 포인트

* 대차잔고율과 직전 공매도 체결수량은 확인됐지만, 일자별 대차 감소/상환 우위는 별도 데이터가 필요합니다.
* 외국인/기관 오늘 순매수는 KIS 응답이 비어 있으면 0으로 처리합니다. 장 마감 후 재확인이 필요합니다.
* 지지선/저항선은 KIS 피벗 값을 사용했습니다.

### 점수 근거
긍정 신호:
{pos_lines}

위험 신호:
{neg_lines}

본 리포트는 데이터 기반 관찰과 대응 시나리오이며, 매수·매도 지시가 아닙니다.
"""


def main() -> None:
    parser = argparse.ArgumentParser(description="소미 종목 점수 분석")
    parser.add_argument("--file", type=str)
    parser.add_argument("--text", type=str)
    parser.add_argument("--output", type=str)
    args = parser.parse_args()

    if args.file:
        raw_text = Path(args.file).read_text(encoding="utf-8", errors="replace")
    elif args.text:
        raw_text = args.text
    elif not sys.stdin.isatty():
        raw_text = sys.stdin.read()
    else:
        print("분석할 텍스트가 없습니다.")
        return

    parsed = parse_input_text(raw_text)
    score, grade, pos, neg = calculate_score(parsed)
    report = generate_report(parsed, score, grade, pos, neg)
    print(report)

    out_file = args.output
    if not out_file:
        reports_dir = PROJECT_ROOT / "reports" / "research"
        reports_dir.mkdir(parents=True, exist_ok=True)
        out_file = str(reports_dir / "somi_stock_latest.md")
    Path(out_file).write_text(report, encoding="utf-8")
    print(f"\n[OK] 리포트 저장: {out_file}")


if __name__ == "__main__":
    main()
