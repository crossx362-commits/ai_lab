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
    if final >= 80:
        grade = "강함"
    elif final >= 60:
        grade = "중간 이상"
    elif final >= 40:
        grade = "관찰"
    elif final >= 20:
        grade = "약함"
    else:
        grade = "매우 약함"
    return final, grade, pos, neg


def _v(raw: dict[str, str], key: str, default: str = "확인 필요") -> str:
    return raw.get(key, "").strip() or default


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

## 2. 매수 판단

* 결론: {decision}
* 큰 수익 가능성 점수: {score}점 / 100점
* 등급: {grade}
* 판단: {scenario}

## 3. 체크 포인트

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
