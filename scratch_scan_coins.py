import os
import sys
import pandas as pd
import numpy as np

# Set python path
_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "projects", "ai-team"))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
load_env()

# Import functions from upbit_analyzer
sys.path.insert(0, os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools"))
import upbit_analyzer

import pyupbit

def scan_coins():
    tickers = ["KRW-ETH", "KRW-SOL", "KRW-XRP", "KRW-DOGE", "KRW-ADA", "KRW-AVAX", "KRW-DOT"]
    results = []
    
    print("[Dave Scan] 시작합니다. 분석 대상 코인:", tickers)
    
    for ticker in tickers:
        try:
            # 300일 데이터 가져오기
            df = pyupbit.get_ohlcv(ticker, interval="day", count=300)
            if df is None or df.empty:
                continue
                
            df = df.reset_index().rename(columns={
                "index": "Date",
                "open": "Open",
                "high": "High",
                "low": "Low",
                "close": "Close",
                "volume": "Volume"
            })
            
            # 지표 계산
            indicators = upbit_analyzer.calc_indicators(df)
            df_supertrend = upbit_analyzer.calculate_supertrend(df.copy())
            
            current_price = df_supertrend['Close'].iloc[-1]
            current_trend = df_supertrend['Trend'].iloc[-1]
            
            # 점수 산정
            score = 0
            reasons = []
            
            # 1. EMA 200 대추세 (롱 국면 여부)
            is_above_ema = indicators["대추세_EMA200"] == "상승 국면(LONG 전용)"
            if is_above_ema:
                score += 4
                reasons.append("EMA 200 위 (상승 국면)")
            else:
                reasons.append("EMA 200 아래 (하락 국면)")
                
            # 2. Supertrend 추세
            if current_trend == "상승":
                score += 3
                reasons.append("Supertrend 상승")
                
            # 3. 스토캐스틱 RSI
            stoch_status = indicators["StochRSI_상태"]
            if stoch_status == "과매도 골든크로스":
                score += 3
                reasons.append("StochRSI 과매도 골든크로스")
            elif stoch_status == "과매도":
                score += 1.5
                reasons.append("StochRSI 과매도")
            elif stoch_status == "과매수":
                score -= 1
                reasons.append("StochRSI 과매수 (과열)")
                
            # 4. 하이킨 애쉬
            ha_status = indicators["HeikinAshi_상태"]
            if "아래꼬리 없는 장대양봉" in ha_status:
                score += 3
                reasons.append("하이킨애쉬 아래꼬리 없는 장대양봉")
            elif ha_status == "양봉":
                score += 1
                reasons.append("하이킨애쉬 양봉")
                
            # 5. 거래량 급증
            if indicators["VolumeSpike"] == "✅ 급증":
                score += 2
                reasons.append(f"거래량 급증 ({indicators['Volume_배율']}배)")
                
            results.append({
                "ticker": ticker,
                "score": score,
                "current_price": current_price,
                "reasons": reasons,
                "indicators": indicators
            })
            print(f"  - {ticker}: {score}점 | {', '.join(reasons[:3])}")
            
        except Exception as e:
            print(f"  - {ticker} 분석 실패: {e}")
            
    # 점수 기준 내림차순 정렬
    results.sort(key=lambda x: x["score"], reverse=True)
    
    if not results:
        print("❌ 분석 가능한 코인 데이터가 없습니다.")
        return
        
    best = results[0]
    print(f"\n[Dave Scan 완료] 가장 전망이 우수한 코인은 {best['ticker']} ({best['score']}점) 입니다.")
    print(f"상세 근거: {', '.join(best['reasons'])}")
    
    # 최고 코인에 대해 전체 분석 보고서 생성 실행
    print(f"\n[Dave] {best['ticker']}에 대한 마스터 리포트 분석을 실행합니다...")
    report = upbit_analyzer.run_analysis("퀀트 스캔 결과 가장 전망이 우수함. 마스터 리포트 작성 요청.", ticker=best["ticker"])
    print(report)

if __name__ == "__main__":
    scan_coins()
