# -*- coding: utf-8 -*-
"""
데이브 업비트 다중 코인 자동 매매 봇 (실시간 감시 및 랭킹 선택형 진입)
"""
import os
import sys
import time
import datetime
import re

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()

import pyupbit
import upbit_analyzer

# 감시 대상 8대 메이저 코인
TICKERS = ["KRW-BTC", "KRW-ETH", "KRW-SOL", "KRW-XRP", "KRW-DOGE", "KRW-ADA", "KRW-AVAX", "KRW-DOT"]

def parse_decision_from_report(report: str) -> str:
    """리포트 텍스트에서 최종 결정을 파싱합니다."""
    for line in report.split("\n"):
        if "최종 결정" in line or "Decision" in line:
            if "매수" in line or "Long" in line:
                if "관망" not in line and "HOLD" not in line:
                    return "BUY"
            if "매도" in line or "Short" in line:
                if "관망" not in line and "HOLD" not in line:
                    return "SELL"
    return "HOLD"

def calculate_confluence_score(ticker: str) -> dict:
    """Ollama 호출 없이 기술 지표만으로 빠르게 정합성 점수를 계산합니다. (만점: 15점)"""
    try:
        df = pyupbit.get_ohlcv(ticker, interval="day", count=300)
        if df is None or df.empty:
            return {"ticker": ticker, "score": 0, "error": "데이터 없음"}
            
        df = df.reset_index().rename(columns={
            "index": "Date",
            "open": "Open",
            "high": "High",
            "low": "Low",
            "close": "Close",
            "volume": "Volume"
        })
        
        indicators = upbit_analyzer.calc_indicators(df)
        df_supertrend = upbit_analyzer.calculate_supertrend(df.copy())
        
        current_price = df_supertrend['Close'].iloc[-1]
        current_trend = df_supertrend['Trend'].iloc[-1]
        records = df_supertrend.tail(30).to_dict(orient='records')
        atr = upbit_analyzer._calc_atr(records)
        
        score = 0
        reasons = []
        
        # 1. EMA 200 대추세 (롱 국면 여부)
        if indicators["대추세_EMA200"] == "상승 국면(LONG 전용)":
            score += 4
            reasons.append("EMA200 위")
            
        # 2. Supertrend 추세
        if current_trend == "상승":
            score += 3
            reasons.append("Supertrend 상승")
            
        # 3. 스토캐스틱 RSI
        stoch_status = indicators["StochRSI_상태"]
        if stoch_status == "과매도 골든크로스":
            score += 3
            reasons.append("StochRSI 과매도 골크")
        elif stoch_status == "과매도":
            score += 1.5
            reasons.append("StochRSI 과매도")
        elif stoch_status == "과매수":
            score -= 1
            reasons.append("StochRSI 과매수(과열)")
            
        # 4. 하이킨 애쉬
        ha_status = indicators["HeikinAshi_상태"]
        if "아래꼬리 없는 장대양봉" in ha_status:
            score += 3
            reasons.append("HA 아래꼬리없는 장대양봉")
        elif ha_status == "양봉":
            score += 1
            reasons.append("HA 양봉")
            
        # 5. 거래량 급증
        if indicators["VolumeSpike"] == "✅ 급증":
            score += 2
            reasons.append(f"거래량 급증 ({indicators['Volume_배율']}배)")
            
        return {
            "ticker": ticker,
            "score": score,
            "current_price": current_price,
            "atr": atr,
            "reasons": reasons,
            "indicators": indicators
        }
    except Exception as e:
        return {"ticker": ticker, "score": 0, "error": str(e)}

def run_auto_trade_cycle(sim_mode=False, should_analyze=False):
    print(f"\n--- [{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 다중 코인 자동 매매 모니터링 시작 ---")
    
    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        print("[AutoTrader] API 키가 설정되지 않았습니다. 시뮬레이션 모드로 작동합니다.")
        sim_mode = True
        
    held_positions = []
    krw_balance = 1000000.0 if sim_mode else 0.0
    
    if not sim_mode:
        try:
            krw_balance = float(upbit_client.get_balance("KRW"))
        except Exception as e:
            print(f"[AutoTrader] KRW 잔고 조회 실패: {e}")
            return False

    # 1. 보유 중인 모든 코인에 대한 실시간 시세 감시 (10초 주기)
    for ticker in TICKERS:
        try:
            if sim_mode:
                btc_balance = 0.0
                avg_buy_price = 0.0
            else:
                btc_balance = float(upbit_client.get_balance(ticker))
                avg_buy_price = float(upbit_client.get_avg_buy_price(ticker))
                
            # 시세 조회
            current_price = float(pyupbit.get_current_price(ticker))
            
            # 최소 주문 단위(5,000원) 이상의 포지션 보유 확인
            if btc_balance * current_price >= 5000.0:
                # 30일 데이터로 ATR 구하기
                df = pyupbit.get_ohlcv(ticker, interval="day", count=30)
                df_supertrend = upbit_analyzer.calculate_supertrend(df.reset_index().rename(columns={"index": "Date", "open": "Open", "high": "High", "low": "Low", "close": "Close", "volume": "Volume"}))
                atr = upbit_analyzer._calc_atr(df_supertrend.to_dict(orient='records'))
                
                held_positions.append({
                    "ticker": ticker,
                    "balance": btc_balance,
                    "avg_buy_price": avg_buy_price,
                    "current_price": current_price,
                    "atr": atr
                })
        except Exception as e:
            print(f"[AutoTrader] {ticker} 보유 현황 조회 중 오류: {e}")

    # 2. 보유 포지션 실시간 TP/SL 감시 및 집행
    if held_positions:
        print(f"[AutoTrader] 현재 보유 중인 포지션 수: {len(held_positions)}개")
        for pos in held_positions:
            ticker = pos["ticker"]
            current_price = pos["current_price"]
            avg_buy_price = pos["avg_buy_price"]
            btc_balance = pos["balance"]
            atr = pos["atr"]
            
            profit_ratio = (current_price - avg_buy_price) / avg_buy_price
            tp_price = avg_buy_price * 1.025
            sl_atr = avg_buy_price - 2 * atr
            sl_fixed = avg_buy_price * 0.985
            sl_price = max(sl_atr, sl_fixed)
            
            print(f"  [{ticker}] 수익률: {profit_ratio*100:.2f}% | 익절가: {tp_price:,.0f}원 | 손절가: {sl_price:,.0f}원 (현재가: {current_price:,.0f}원)")
            
            if current_price >= tp_price:
                msg = f"🎉 [데이브] 실시간 목표 익절가 도달! (+2.5% 이상)\n📌 대상: {ticker}\n💰 매도가: {current_price:,}원 (평단: {avg_buy_price:,}원)\n📈 수익률: {profit_ratio*100:.2f}%\n🚨 전량 시장가 매도를 집행합니다."
                print(msg)
                send_telegram_message(msg)
                if not sim_mode:
                    res = upbit_analyzer.execute_sell_all(ticker)
                    print(res)
            elif current_price <= sl_price:
                msg = f"🚨 [데이브] 실시간 손절가 도달! (-1.5% 이하 또는 ATR 기준 이탈)\n📌 대상: {ticker}\n💰 매도가: {current_price:,}원 (평단: {avg_buy_price:,}원)\n📉 수익률: {profit_ratio*100:.2f}%\n🚨 손실 방지를 위해 전량 시장가 매도를 집행합니다."
                print(msg)
                send_telegram_message(msg)
                if not sim_mode:
                    res = upbit_analyzer.execute_sell_all(ticker)
                    print(res)
        return False # 분석은 안 돌렸으므로 False

    # 3. 포지션 미보유 시 신규 진입 분석 (1시간 주기)
    else:
        if not should_analyze:
            print("[AutoTrader] 포지션 미보유 상태. 신규 진입 분석 대기 중 (1시간 주기)")
            return False
            
        print("[AutoTrader] 포지션 미보유 상태 & 분석 주기 도달 -> 전체 코인 퀀트 스캔 시작...")
        scanned = []
        for ticker in TICKERS:
            res = calculate_confluence_score(ticker)
            if "error" not in res:
                scanned.append(res)
                
        # 점수 순 정렬
        scanned.sort(key=lambda x: x["score"], reverse=True)
        
        # 스캔 스코어 보드 출력
        print("\n=== [퀀트 스코어 랭킹] ===")
        for item in scanned[:5]:
            print(f"  - {item['ticker']}: {item['score']}점 | {', '.join(item['reasons'])}")
        print("=========================\n")
        
        if not scanned:
            print("[AutoTrader] 스캔 가능한 코인 데이터가 없습니다.")
            return True
            
        best = scanned[0]
        # 최소 진입 점수 문턱값 (11점 이상)
        if best["score"] >= 11:
            print(f"[AutoTrader] 최우수 코인 포착: {best['ticker']} ({best['score']}점) -> LLM 최종 Confluence 검증 실행...")
            report = upbit_analyzer.run_analysis(f"퀀트 스캔 점수 {best['score']}점으로 1위 달성. 신규 진입 검증 수행.", best["ticker"])
            decision = parse_decision_from_report(report)
            
            print(f"[AutoTrader] LLM 최종 결정: {decision}")
            if decision == "BUY":
                buy_amount = krw_balance * 0.3
                if buy_amount < 5000.0:
                    print(f"[AutoTrader] 매수 가능 금액이 최소 주문금액(5,000원) 미만입니다. (계산액: {buy_amount:.0f}원)")
                    return True
                    
                msg = f"⚡ [데이브] 다중 종목 스캔 1위 + Confluence 합치 완료!\n📌 대상: {best['ticker']} (스캔 점수: {best['score']}점)\n💰 투입 금액: {buy_amount:,.0f}원 (예수금의 30%)\n📊 현재가: {best['current_price']:,}원\n🚨 시장가 매수를 집행합니다."
                print(msg)
                send_telegram_message(msg)
                if not sim_mode:
                    res = upbit_analyzer.execute_buy(best["ticker"], buy_amount)
                    print(res)
            else:
                print(f"[AutoTrader] {best['ticker']} 분석 결과가 HOLD로 결정되어 진입하지 않습니다.")
        else:
            print(f"[AutoTrader] 현재 최소 진입 점수(11점)를 만족하는 코인이 없습니다. (최고 점수: {best['ticker']} {best['score']}점)")
            
        return True

if __name__ == "__main__":
    args = sys.argv[1:]
    sim = "--sim" in args
    
    if "--once" in args:
        run_auto_trade_cycle(sim_mode=sim, should_analyze=True)
    else:
        print("🤖 데이브 업비트 다중 코인 자동 매매 데몬 시작 (시세 감시: 10초, 신규 분석: 1시간)")
        send_telegram_message("🤖 데이브 업비트 다중 코인 자동 매매 데몬 가동을 시작합니다 (실시간 10초 시세 감시 및 1시간 주기 다중 스캔).")
        
        last_analysis_time = datetime.datetime.now() - datetime.timedelta(hours=2)
        
        while True:
            try:
                now = datetime.datetime.now()
                should_analyze = (now - last_analysis_time) >= datetime.timedelta(hours=1)
                analysis_executed = run_auto_trade_cycle(sim_mode=sim, should_analyze=should_analyze)
                if analysis_executed:
                    last_analysis_time = now
            except Exception as e:
                print(f"[Daemon Error] {e}")
            
            time.sleep(10)
