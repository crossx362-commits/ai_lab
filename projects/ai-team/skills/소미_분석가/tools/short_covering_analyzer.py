#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
소미 - 국내주식 수급·세력상황·큰 수익·매수판단 분석 도구
"""
import os
import sys
import re
import argparse
from datetime import datetime

# UTF-8 출력 보장
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))

# 수치 정화 헬퍼
def to_num(val_str: str) -> float:
    if not val_str:
        return 0.0
    # 콤마, 공백, %, 원, 주 등 문자 제거
    cleaned = re.sub(r"[^\d\.\-]", "", val_str)
    try:
        return float(cleaned) if "." in cleaned else int(cleaned)
    except ValueError:
        return 0.0

def parse_input_text(text: str) -> dict:
    """사용자가 제공한 자연어 텍스트 데이터를 정규식으로 파싱"""
    data = {}
    gap = r"[ \t]*"
    
    # 텍스트에 포함된 다양한 키워드 대응 정규식
    patterns = {
        "date": rf"날짜:{gap}([^\n]*)",
        "stock_name": rf"종목명:{gap}([^\n]*)",
        "stock_code": rf"종목코드:{gap}([^\n]*)",
        "open": rf"시가:{gap}([^\n]*)",
        "high": rf"고가:{gap}([^\n]*)",
        "low": rf"저가:{gap}([^\n]*)",
        "close": rf"종가:{gap}([^\n]*)",
        "change_pct": rf"등락률:{gap}([^\n]*)",
        "volume": rf"거래량:{gap}([^\n]*)",
        "avg_volume_20d": rf"(?:20일 평균 거래량|20일 평균거래량):{gap}([^\n]*)",
        "trading_value": rf"거래대금:{gap}([^\n]*)",
        "recent_price_flow": rf"최근 5일 주가 흐름:{gap}([^\n]*)",
        "recent_volume_flow": rf"최근 5일 거래량 흐름:{gap}([^\n]*)",
        "recent_investor_flow": rf"최근 5일 외국인·기관 수급:{gap}([^\n]*)",
        "buy_indiv": rf"개인 순매수:{gap}([^\n]*)",
        "buy_foreigner": rf"외국인 순매수:{gap}([^\n]*)",
        "buy_institution": rf"기관 순매수:{gap}([^\n]*)",
        "program_trading": rf"프로그램 매매:{gap}([^\n]*)",
        "loan_balance": rf"대차잔고수량:{gap}([^\n]*)",
        "prev_loan_balance": rf"전일 대차잔고수량:{gap}([^\n]*)",
        "loan_repay": rf"대차상환수량:{gap}([^\n]*)",
        "loan_agree": rf"대차체결수량:{gap}([^\n]*)",
        "short_volume": rf"공매도 거래량:{gap}([^\n]*)",
        "short_ratio": rf"공매도 비중:{gap}([^\n]*)",
        "short_avg_price": rf"공매도 평균가:{gap}([^\n]*)",
        "kospi_change": rf"코스피 등락률:{gap}([^\n]*)",
        "kosdaq_change": rf"코스닥 등락률:{gap}([^\n]*)",
        "market_warning": rf"시장경보 여부:{gap}([^\n]*)",
        "theme": rf"테마:{gap}([^\n]*)",
        "news_sentiment": rf"(?:관련 뉴스|원전 관련 뉴스 분위기):{gap}([^\n]*)",
        "cb_notice": rf"(?:CB/BW/유상증자/보호예수 이슈|CB 관련 공시):{gap}([^\n]*)",
        "conversion_price": rf"전환가:{gap}([^\n]*)",
        "refixing_possible": rf"리픽싱 가능 여부:{gap}([^\n]*)",
        "support_line": rf"주요 지지선(?:[ \t]*가격)?:{gap}([^\n]*)",
        "resistance_line": rf"주요 저항선:{gap}([^\n]*)",
        "investor_avg_price": rf"(?:투자자 평단|내 평단):{gap}([^\n]*)",
        "holding_ratio": rf"(?:보유 비중|내 보유 비중):{gap}([^\n]*)",
        "cash": rf"(?:추가 예수금|예수금):{gap}([^\n]*)",
        "intraday_feature": rf"장중 특징:{gap}([^\n]*)"
    }
    
    for key, regex in patterns.items():
        match = re.search(regex, text)
        if match:
            data[key] = match.group(1).strip()
        else:
            data[key] = ""
            
    return data

def calculate_score(raw_data: dict) -> tuple[int, str, list[str], list[str]]:
    """파싱된 데이터를 기반으로 큰 수익 가능성 점수 계산"""
    
    # 텍스트 정보 파싱
    date_str = raw_data.get("date", datetime.now().strftime("%Y-%m-%d"))
    news_sentiment = raw_data.get("news_sentiment", "중립")
    intraday_feature = raw_data.get("intraday_feature", "")
    
    # 수치 정보 변환
    close = to_num(raw_data.get("close"))
    change_pct = to_num(raw_data.get("change_pct"))
    volume = to_num(raw_data.get("volume"))
    avg_volume_20d = to_num(raw_data.get("avg_volume_20d"))
    buy_indiv = to_num(raw_data.get("buy_indiv"))
    buy_foreigner = to_num(raw_data.get("buy_foreigner"))
    buy_institution = to_num(raw_data.get("buy_institution"))
    loan_balance = to_num(raw_data.get("loan_balance"))
    prev_loan_balance = to_num(raw_data.get("prev_loan_balance"))
    loan_repay = to_num(raw_data.get("loan_repay"))
    loan_agree = to_num(raw_data.get("loan_agree"))
    short_volume = to_num(raw_data.get("short_volume"))
    short_ratio = to_num(raw_data.get("short_ratio"))
    kosdaq_change = to_num(raw_data.get("kosdaq_change"))
    support_line = to_num(raw_data.get("support_line"))
    high = to_num(raw_data.get("high"))
    open_p = to_num(raw_data.get("open"))
    low = to_num(raw_data.get("low"))
    
    pos_signals = []
    neg_signals = []
    
    # [1] 대차잔고/공매도 수급 전환 점수
    score_1 = 0
    loan_diff = prev_loan_balance - loan_balance
    
    if loan_diff >= 300000:
        score_1 += 10
        pos_signals.append(f"대차잔고 전일 대비 {loan_diff:,.0f}주 감소 (+10점)")
    if loan_diff >= 1000000:
        score_1 += 20  # 합산하여 30점이 됨 (30만주 이상 10점 + 100만주 이상 20점)
        pos_signals.append(f"대차잔고 전일 대비 100만 주 이상 대량 감소 (+20점)")
    if loan_diff >= 2000000:
        score_1 += 10  # 합산하여 40점이 됨
        pos_signals.append(f"대차잔고 전일 대비 200만 주 이상 초대량 감소 (+30점 누적)")
        
    # 대차상환수량과 체결수량 비교
    if loan_repay > loan_agree and (loan_repay - loan_agree) > 100000:
        score_1 += 10
        pos_signals.append(f"대차상환수량({loan_repay:,.0f}주)이 체결수량({loan_agree:,.0f}주)보다 뚜렷하게 많음 (+10점)")
        
    # 복합 지표
    if loan_diff > 0 and change_pct > 0:
        score_1 += 10
        pos_signals.append("대차잔고 감소와 주가 상승 동시 발생 (+10점)")
    if loan_diff > 0 and volume > avg_volume_20d:
        score_1 += 10
        pos_signals.append("대차잔고 감소와 거래량 증가 동시 발생 (+10점)")
    if loan_diff > 0 and (buy_foreigner > 0 or buy_institution > 0):
        score_1 += 10
        pos_signals.append("대차잔고 감소와 외국인/기관 순매수 동시 발생 (+10점)")
        
    # 연속 감소는 임시로 당일 정보만으로는 판단하기 어려우나, 장중 특징 등에 "대차 감소 연속" 단어가 있거나 별도 입력이 없으므로, 
    # 일단 당일 차이만 계산하여 처리하되 한계를 40점으로 제한
    score_1 = min(score_1, 40)
    
    # [2] 거래량 폭발 + 장대양봉 점수 (최대 30점)
    score_2 = 0
    if avg_volume_20d > 0:
        vol_ratio = volume / avg_volume_20d
        if vol_ratio >= 10.0:
            score_2 += 30
            pos_signals.append(f"거래량이 20일 평균 대비 10배 이상 폭발({vol_ratio:.1f}배) (+30점)")
        elif vol_ratio >= 5.0:
            score_2 += 20
            pos_signals.append(f"거래량이 20일 평균 대비 5배 이상 폭발({vol_ratio:.1f}배) (+20점)")
        elif vol_ratio >= 3.0:
            score_2 += 10
            pos_signals.append(f"거래량이 20일 평균 대비 3배 이상 폭발({vol_ratio:.1f}배) (+10점)")
        elif vol_ratio >= 2.0:
            score_2 += 5
            pos_signals.append(f"거래량이 20일 평균 대비 2배 이상 폭발({vol_ratio:.1f}배) (+5점)")
            
    if change_pct >= 10.0:
        score_2 += 20
        pos_signals.append(f"종가 상승률 +10% 이상 급등({change_pct:+.2f}%) (+20점)")
    elif change_pct >= 7.0:
        score_2 += 15
        pos_signals.append(f"종가 상승률 +7% 이상 강세({change_pct:+.2f}%) (+15점)")
    elif change_pct >= 5.0:
        score_2 += 10
        pos_signals.append(f"종가 상승률 +5% 이상 강세({change_pct:+.2f}%) (+10점)")
    elif change_pct >= 3.0:
        score_2 += 5
        pos_signals.append(f"종가 상승률 +3% 이상 상승({change_pct:+.2f}%) (+5점)")
        
    # 종가가 고가 부근 마감 (고가 대비 낙폭 1% 이내)
    if high > 0 and (high - close) / high <= 0.01 and change_pct > 0:
        score_2 += 10
        pos_signals.append("종가가 오늘 고가 부근에서 마감 (+10점)")
        
    if "매도벽 돌파" in intraday_feature or "매도벽" in intraday_feature:
        score_2 += 10
        pos_signals.append("장중 대량 매도벽을 시장가로 강하게 돌파한 흔적 감지 (+10점)")
        
    if "장막판 대량매수" in intraday_feature or "장막판 대량 매수" in intraday_feature or "장막판 매수" in intraday_feature:
        score_2 += 10
        pos_signals.append("장막판 대량 매수 유입으로 종가 견인 (+10점)")
        
    score_2 = min(score_2, 30)
    
    # [3] 외국인·기관 수급 점수 (최대 25점)
    score_3 = 0
    if buy_foreigner > 0 and buy_institution > 0:
        score_3 += 20
        pos_signals.append("외국인과 기관의 양대 수급 주체 동반 순매수 (+20점)")
    else:
        if buy_foreigner > 0:
            score_3 += 10
            pos_signals.append("외국인 순매수 유입 (+10점)")
        if buy_institution > 0:
            score_3 += 10
            pos_signals.append("기관 순매수 유입 (+10점)")
            
    if buy_indiv < -1000000 and (buy_foreigner > 0 or buy_institution > 0):
        score_3 += 10
        pos_signals.append("개인 100만 주 이상 대량 순매도하고 외인/기관이 물량 수취 (+10점)")
        
    # 연속 매수 정황이 특징에 명시된 경우 반영
    if "연속 매수" in intraday_feature or "연속매수" in intraday_feature:
        score_3 += 15
        pos_signals.append("외국인 또는 기관의 다일 연속 순매수세 지속 (+15점)")
        
    score_3 = min(score_3, 25)
    
    # [4] 악재에도 안 빠지는 바닥 신호 점수 (최대 20점)
    score_4 = 0
    if kosdaq_change < 0 and change_pct >= 0:
        score_4 += 10
        pos_signals.append("코스닥 하락 압력 속에서도 보합 이상 주가 방어 (+10점)")
    elif kosdaq_change < -1.5 and change_pct > kosdaq_change:
        score_4 += 10
        pos_signals.append("시장 급락 대비 선방하며 낙폭 제한 (+10점)")
        
    if news_sentiment == "악재" and close >= support_line and support_line > 0:
        score_4 += 10
        pos_signals.append("원전 뉴스 악재 출현에도 불구하고 주요 지지선 방어 (+10점)")
        
    if "지지선 반등" in intraday_feature or "지지반등" in intraday_feature:
        score_4 += 10
        pos_signals.append("장중 하락세를 보였으나 주요 지지선에서 강한 반등 성공 (+10점)")
        
    if "지지선 고수" in intraday_feature or "지지선 방어" in intraday_feature:
        score_4 += 10
        pos_signals.append("3일 이상 동일한 지지 가격대를 깨지 않고 버팀 (+10점)")
        
    score_4 = min(score_4, 20)
    
    # [5] 시장 대비 상대강도 점수 (최대 15점)
    score_5 = 0
    rel_strength = change_pct - kosdaq_change
    if rel_strength >= 5.0:
        score_5 += 10
        pos_signals.append(f"코스닥 대비 상대 강도 매우 강함 ({rel_strength:.1f}%p 상회) (+10점)")
    elif rel_strength >= 3.0:
        score_5 += 5
        pos_signals.append(f"코스닥 대비 상대 강도 우위 ({rel_strength:.1f}%p 상회) (+5점)")
        
    if kosdaq_change < 0 and change_pct > 0 and (close - open_p) > 0:
        score_5 += 10
        pos_signals.append("지수 하락 장세에서 양봉 마감 (+10점)")
        
    if "테마 대장" in intraday_feature or "테마 상대강세" in intraday_feature:
        score_5 += 10
        pos_signals.append("원전 테마 섹터 내 타 종목 대비 독보적 상대 강세 (+10점)")
        
    score_5 = min(score_5, 15)
    
    # 가점 합산
    total_pos = score_1 + score_2 + score_3 + score_4 + score_5
    
    # [6] 위험 신호 감점
    deduction = 0
    if loan_diff < 0:
        deduction += 10
        neg_signals.append("대차잔고 전일 대비 증가 (-10점)")
        if "대차 증가 연속" in intraday_feature:
            deduction += 5
            neg_signals.append("대차잔고가 수일 연속 증가세 지속 (-15점)")
            
    if change_pct > 0 and volume < avg_volume_20d * 0.7:
        deduction += 10
        neg_signals.append("거래량 결여된 주가 상승 (단기 신뢰도 낮음) (-10점)")
        
    if buy_foreigner < 0 and buy_institution < 0:
        deduction += 15
        neg_signals.append("외국인과 기관의 양대 매매 주체 동반 순매도 이탈 (-15점)")
        
    if buy_indiv > 500000 and buy_foreigner < 0 and buy_institution < 0:
        deduction += 10
        neg_signals.append("개인 주도의 추격 매수 및 외인/기관 물량 넘기기 의심 (-10점)")
        
    if support_line > 0 and close < support_line:
        deduction += 20
        neg_signals.append("핵심 지지선 가격 이탈 후 하방 지지 실패 (-20점)")
        
    if change_pct <= -5.0 and volume > avg_volume_20d:
        deduction += 25
        neg_signals.append("거래량 실린 장대음봉 발생 (매도 압력 우세) (-25점)")
        
    if news_sentiment == "악재" and change_pct < -4.0:
        deduction += 15
        neg_signals.append("원전 악재 뉴스에 민감하게 하락 반응 (-15점)")
        
    if "윗꼬리" in intraday_feature or (high - close) / (high - low or 1) >= 0.5 and change_pct > 0:
        deduction += 10
        neg_signals.append("장중 상승폭을 대거 반납하며 긴 윗꼬리 저항 캔들 형성 (-10점)")
        
    if loan_diff <= 0 and volume > avg_volume_20d * 2:
        deduction += 5
        neg_signals.append("대차잔고 감소 수반 없는 단순 거래량 폭발 (-5점)")
        
    if short_ratio > 15.0: # 공매도 비중이 15% 이상으로 높은 수준
        deduction += 10
        neg_signals.append(f"공매도 거래 비중 증가 ({short_ratio}%) (-10점)")
        
    # 최종 점수 계산 (최대 100점, 최소 0점 제한)
    final_score = max(0, min(100, total_pos - deduction))
    
    # 등급 분류
    if final_score >= 90:
        grade = "매우 강함"
    elif final_score >= 75:
        grade = "강함"
    elif final_score >= 60:
        grade = "중간"
    elif final_score >= 40:
        grade = "초기 의심"
    else:
        grade = "약함"
        
    return final_score, grade, pos_signals, neg_signals

def _value_or_check(raw_data: dict, key: str, suffix: str = "") -> str:
    value = str(raw_data.get(key, "")).strip()
    return f"{value}{suffix}" if value else "확인 필요"


def _risk_line(condition: bool, text: str) -> str:
    return text if condition else "특이 신호 없음"


def generate_report(raw_data: dict, score: int, grade: str, pos: list, neg: list) -> str:
    """최종 분석 리포트 마크다운 생성"""
    stock_name = raw_data.get("stock_name", "").strip() or "확인 필요"
    stock_code = raw_data.get("stock_code", "").strip()
    stock_label = f"{stock_name}({stock_code})" if stock_code else stock_name
    theme = _value_or_check(raw_data, "theme")
    close = to_num(raw_data.get("close"))
    open_p = to_num(raw_data.get("open"))
    high = to_num(raw_data.get("high"))
    low = to_num(raw_data.get("low"))
    change_pct = to_num(raw_data.get("change_pct"))
    vol = to_num(raw_data.get("volume"))
    avg_vol = to_num(raw_data.get("avg_volume_20d"))
    trading_value = to_num(raw_data.get("trading_value"))
    buy_i = to_num(raw_data.get("buy_indiv"))
    buy_f = to_num(raw_data.get("buy_foreigner"))
    buy_inst = to_num(raw_data.get("buy_institution"))
    loan_balance = to_num(raw_data.get("loan_balance"))
    prev_loan = to_num(raw_data.get("prev_loan_balance"))
    loan_repay = to_num(raw_data.get("loan_repay"))
    loan_agree = to_num(raw_data.get("loan_agree"))
    short_ratio = to_num(raw_data.get("short_ratio"))
    conversion_price = to_num(raw_data.get("conversion_price"))
    support_line = to_num(raw_data.get("support_line"))
    resistance_line = to_num(raw_data.get("resistance_line"))
    avg_price = to_num(raw_data.get("investor_avg_price"))
    cash = to_num(raw_data.get("cash"))
    intraday = raw_data.get("intraday_feature", "")

    has_loan_data = bool(raw_data.get("loan_balance") and raw_data.get("prev_loan_balance"))
    loan_diff = prev_loan - loan_balance
    loan_change_text = (
        f"{loan_diff:,.0f}주 감소" if loan_diff >= 0 else f"{abs(loan_diff):,.0f}주 증가"
    ) if has_loan_data else "확인 필요"
    vol_ratio = vol / avg_vol if avg_vol else 0
    close_near_high = high > 0 and (high - close) / high <= 0.01
    upper_tail = bool(high and low and (high - close) / (high - low or 1) >= 0.5 and change_pct > 0)
    below_support = bool(support_line and close and close < support_line)
    above_resistance = bool(resistance_line and close and close >= resistance_line)
    cb_active = bool(raw_data.get("cb_notice") or conversion_price or raw_data.get("refixing_possible"))
    refixing_text = raw_data.get("refixing_possible", "").strip()

    if score >= 75:
        short_judgment = "강한 수급 전환 또는 숏커버링 가능성을 의심할 수 있는 정황입니다. 단, 확정이 아니라 대차·거래량·수급이 맞물린 관찰 신호입니다."
    elif score >= 60:
        short_judgment = "중간 강도의 수급 전환 의심 구간입니다. 다음 날 대차잔고와 외국인·기관 수급 연속성이 핵심입니다."
    elif score >= 40:
        short_judgment = "초기 의심 신호는 있으나 아직 대차잔고·거래량·수급이 완전히 정렬되지는 않았습니다."
    else:
        short_judgment = "상환성 매수 정황이 약합니다. 단기 기술적 반등 또는 수급 착시 가능성을 먼저 봐야 합니다."

    if below_support:
        scenario = "시나리오 D: 지지선 이탈. 바닥 논리는 무효에 가깝고, 물타기보다 손실 확대 방어와 예수금 보존이 우선입니다."
    elif score >= 75:
        scenario = "시나리오 C: 강한 수급 전환 의심. 신규는 추격보다 눌림 확인, 보유자는 저항선 부근 비중 조절 계획이 필요합니다."
    elif score >= 40:
        scenario = "시나리오 B: 중간 반등. 일부 수급 개선은 있으나 확신하기 이르며, 반등은 비중 축소 기회로 보는 편이 현실적입니다."
    else:
        scenario = "시나리오 A: 약한 반등. 추가 매수보다 예수금 보존과 리스크 관리가 우선입니다."

    cb_risk = []
    if cb_active:
        if conversion_price and close > conversion_price:
            cb_risk.append(f"현재가가 전환가({conversion_price:,.0f}원)보다 높아 전환 물량 출회 가능성 점검 필요")
        elif conversion_price and close < conversion_price:
            cb_risk.append(f"현재가가 전환가({conversion_price:,.0f}원)보다 낮아 리픽싱 또는 하락 방치 리스크 확인 필요")
        if "가능" in refixing_text:
            cb_risk.append("리픽싱 가능성이 남아 있어 성급한 물타기 금지")
        if upper_tail or "윗꼬리" in intraday:
            cb_risk.append("거래량 동반 윗꼬리는 CB 또는 기존 물량 출회 가능성 점검")
    if not cb_risk:
        cb_risk.append("CB 관련 공시·전환가·리픽싱 여부 확인 필요")

    execution_trap = "체결강도 수치 입력 없음. 체결강도만으로 판단하지 말고 거래량·거래대금·종가 위치를 우선 확인해야 합니다."
    if vol_ratio and vol_ratio < 2 and change_pct > 0:
        execution_trap = "상승했지만 거래량 배수가 낮아 체결강도 착시 또는 얇은 호가 반등 가능성을 주의해야 합니다."
    elif vol_ratio >= 5 and close_near_high:
        execution_trap = "거래량과 종가 위치가 함께 강해 체결강도 착시보다는 실제 수급 유입 가능성이 더 높아진 정황입니다."

    pos_lines = "\n".join([f"* {s}" for s in pos]) if pos else "* 확인된 긍정 신호 없음"
    neg_lines = "\n".join([f"* {s}" for s in neg]) if neg else "* 확인된 위험 신호 없음"
    loss_context = "확인 필요"
    if avg_price and close:
        gap = (close - avg_price) / avg_price * 100
        loss_context = f"현재가 기준 평단 대비 {gap:+.2f}% 위치"
    cash_text = _value_or_check(raw_data, "cash")
    cash_clause = f"추가 예수금 {cash_text}이 있어도" if cash_text != "확인 필요" else "추가 예수금이 확인되지 않아도"
    resistance_text = _value_or_check(raw_data, "resistance_line")
    resistance_clause = f"주요 저항선 {resistance_text}" if resistance_text != "확인 필요" else "확인된 주요 저항선"
    support_text = _value_or_check(raw_data, "support_line")
    support_clause = f"지지선 {support_text} 이탈 후" if support_text != "확인 필요" else "주요 지지선 이탈 후"

    if below_support or (buy_i > 0 and buy_f < 0 and buy_inst < 0) or (change_pct > 0 and vol_ratio < 2):
        buy_decision = "매수 금지"
    elif score >= 75 and above_resistance and vol_ratio >= 3:
        buy_decision = "공격 검토"
    elif score >= 60:
        buy_decision = "분할 접근 가능"
    elif score >= 40:
        buy_decision = "관찰"
    else:
        buy_decision = "보유자는 유지, 신규는 대기"

    power_class = "확인 부족"
    if score >= 75:
        power_class = "숏커버링 또는 강한 수급 전환 의심"
    elif upper_tail and vol_ratio >= 2:
        power_class = "물량 넘기기 의심"
    elif buy_i > 0 and buy_f < 0 and buy_inst < 0:
        power_class = "털림 또는 개인 추격 의심"
    elif buy_f > 0 or buy_inst > 0:
        power_class = "매집 의심"
    elif "경고" in raw_data.get("market_warning", "") or "주의" in raw_data.get("market_warning", ""):
        power_class = "과열 위험"

    return f"""[종목 간단 분석 리포트]

종목: {stock_label}
테마: {theme}

## 1. 현재 판세 진단

* 주가 흐름: 시가 {_value_or_check(raw_data, "open")} / 고가 {_value_or_check(raw_data, "high")} / 저가 {_value_or_check(raw_data, "low")} / 종가 {_value_or_check(raw_data, "close")} / 등락률 {_value_or_check(raw_data, "change_pct")}
* 최근 5일 흐름: 주가 {_value_or_check(raw_data, "recent_price_flow")} / 거래량 {_value_or_check(raw_data, "recent_volume_flow")} / 외국인·기관 {_value_or_check(raw_data, "recent_investor_flow")}
* 거래량과 거래대금: 거래량 {_value_or_check(raw_data, "volume")} / 20일 평균 {_value_or_check(raw_data, "avg_volume_20d")} / 평균 대비 {vol_ratio:.1f}배 / 거래대금 {_value_or_check(raw_data, "trading_value")}
* 대차잔고: 현재 {_value_or_check(raw_data, "loan_balance")} / 전일 {_value_or_check(raw_data, "prev_loan_balance")} / 변화 {loan_change_text} / 상환 {loan_repay:,.0f}주 / 체결 {loan_agree:,.0f}주
* 공매도: 거래량 {_value_or_check(raw_data, "short_volume")} / 비중 {_value_or_check(raw_data, "short_ratio")} / 평균가 {_value_or_check(raw_data, "short_avg_price")}
* 외국인·기관·개인 수급: 개인 {buy_i:+,.0f}주 / 외국인 {buy_f:+,.0f}주 / 기관 {buy_inst:+,.0f}주 / 프로그램 {_value_or_check(raw_data, "program_trading")}
* 지지선과 저항선: 지지선 {_value_or_check(raw_data, "support_line")} / 저항선 {_value_or_check(raw_data, "resistance_line")} / 평단 점검 {loss_context}
* 현재 결론: {short_judgment}

## 2. 매수 판단

* 결론: {buy_decision}
* 큰 수익 가능성 점수: {score}점 / 100점
* 등급: {grade}
* 대차잔고 변화: {loan_change_text}. 감소면 상환성 매수 정황, 증가면 하방 압력 리스크로 봅니다.
* 거래량과 캔들: 평균 대비 {vol_ratio:.1f}배, 종가 고가권 여부 {'예' if close_near_high else '아니오'}, 장중 특징: {_value_or_check(raw_data, "intraday_feature")}
* 외국인·기관 수급: 외국인 {buy_f:+,.0f}주 / 기관 {buy_inst:+,.0f}주. 개인만 받는 구조인지 반드시 구분해야 합니다.
* 공매도 비중: {_value_or_check(raw_data, "short_ratio")}. 비중 증가 여부는 다음 날 반드시 재확인해야 합니다.
* 판단 근거: {short_judgment}

## 3. 세력 움직임 해석

* 분류: {power_class}
* 공매도 세력: 대차잔고 변화와 공매도 비중을 함께 봐야 합니다. 대차 감소 없이 주가만 오르면 단기 반등 또는 물량 넘기기 가능성을 우선 점검합니다.
* 대차 상환 여부: 상환수량 {loan_repay:,.0f}주 / 체결수량 {loan_agree:,.0f}주. 상환 우위가 이어질 때 강한 수급 전환 정황 신뢰도가 올라갑니다.
* 물량 리스크: {_value_or_check(raw_data, "cb_notice")}
* 리픽싱 가능성: {_value_or_check(raw_data, "refixing_possible")}
* 물량 넘기기 가능성: {'윗꼬리 또는 저항선 부근 물량 출회 가능성 점검 필요' if upper_tail or above_resistance else '현재 입력만으로는 강한 물량 넘기기 신호 확인 제한'}
* 세력 해석 결론: {"; ".join(cb_risk)}

## 4. 리스크와 착시 경고

* 체결강도 착시: {execution_trap}
* 시장경보: {_value_or_check(raw_data, "market_warning")}
* 거래량 없는 반등: {_risk_line(change_pct > 0 and vol_ratio < 2, "거래량 배수가 낮은 상승은 신뢰도가 낮습니다.")}
* 개인 추격매수: {_risk_line(buy_i > 0 and buy_f < 0 and buy_inst < 0, "개인만 사고 외국인·기관이 파는 구조는 추격 위험이 큽니다.")}
* CB 물량: {"; ".join(cb_risk)}
* 지지선 이탈: {_risk_line(below_support, "주요 지지선 이탈. 기존 바닥 논리는 무효로 봐야 합니다.")}
* 희망회로 차단: 지금 필요한 것은 희망이 아니라 탈출 계획입니다. 원금 회복보다 손실 최소화가 우선입니다.

## 5. 대응 가이드

* 신규 매수: {buy_decision}
* 예수금 사용: 예수금은 마지막 방패입니다. {cash_clause} 확실한 수급 전환 전까지 함부로 쓰면 안 됩니다.
* 물타기 여부: 지지선 붕괴, 대차잔고 증가, 외국인·기관 순매도, 거래량 없는 하락 구간에서는 물타기 금지입니다.
* 반등 시 비중 축소: 반등은 축제가 아니라 탈출문입니다. {resistance_clause} 또는 매물대에서는 분할 비중 축소 시나리오를 우선 검토합니다.
* 지지선 이탈 시 대응: {support_clause} 회복 실패 시 손실 확대 방어와 예수금 보존을 우선합니다.
* 강한 수급 전환 의심 시 대응: {scenario}
* 내일 확인할 데이터: 대차잔고 추가 감소, 대차상환 우위 지속, 거래량/거래대금 유지, 외국인·기관 연속 순매수, 공매도 비중 감소, CB 공시와 전환가, 지지선 유지 여부.
* 최종 전략: 큰 수익 가능성은 강한 수급 전환이 확인될 때만 봅니다. 손실 방어 조건이 약하면 매수보다 관찰과 예수금 보존이 우선입니다.

### 점수 근거
긍정 신호:
{pos_lines}

위험 신호:
{neg_lines}

본 리포트는 데이터 기반 관찰과 대응 시나리오이며, 매수·매도 지시가 아닙니다.
"""

def main():
    parser = argparse.ArgumentParser(description="국내주식 수급·세력상황·큰 수익·매수판단 에이전트 소미")
    parser.add_argument("--file", type=str, help="분석할 데이터 텍스트 파일 경로")
    parser.add_argument("--text", type=str, help="분석할 데이터 원본 텍스트")
    parser.add_argument("--output", type=str, help="출력 리포트 파일 경로")
    
    args = parser.parse_args()
    
    raw_text = ""
    if args.file:
        if os.path.exists(args.file):
            with open(args.file, "r", encoding="utf-8", errors="replace") as f:
                raw_text = f.read()
        else:
            print(f"❌ 파일을 찾을 수 없습니다: {args.file}")
            sys.exit(1)
    elif args.text:
        raw_text = args.text
    else:
        # stdin에서 읽기
        if not sys.stdin.isatty():
            raw_text = sys.stdin.read()
        else:
            print("ℹ️ 데이터 입력이 없습니다. 파일 경로나 텍스트를 인자로 전달해주세요.")
            print("사용법: python short_covering_analyzer.py --file data.txt")
            sys.exit(0)
            
    if not raw_text.strip():
        print("❌ 분석할 데이터 텍스트가 비어있습니다.")
        sys.exit(1)
        
    # 데이터 파싱
    parsed_data = parse_input_text(raw_text)
    
    # 분석 및 채점
    score, grade, pos, neg = calculate_score(parsed_data)
    
    # 리포트 생성
    report_content = generate_report(parsed_data, score, grade, pos, neg)
    
    # 결과 출력
    print(report_content)
    
    # 파일 저장
    out_file = args.output
    if not out_file:
        # 기본 저장 경로 설정
        reports_dir = os.path.join(PROJECT_ROOT, "reports", "research")
        os.makedirs(reports_dir, exist_ok=True)
        out_file = os.path.join(reports_dir, "somi_stock_latest.md")
        
    try:
        with open(out_file, "w", encoding="utf-8") as f:
            f.write(report_content)
        print(f"\n[OK] 리포트가 성공적으로 저장되었습니다: {out_file}")
    except Exception as e:
        print(f"\n❌ 리포트 저장 실패: {e}")

if __name__ == "__main__":
    main()
