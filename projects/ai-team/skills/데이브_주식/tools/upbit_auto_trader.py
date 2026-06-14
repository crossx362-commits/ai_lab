# -*- coding: utf-8 -*-
"""
데이브 업비트 코인(BTC) 자동 매매 봇 (실시간 모니터링 고도화 버전)
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

# upbit_analyzer 기능 임포트
import upbit_analyzer

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

def run_auto_trade_cycle(ticker="KRW-BTC", sim_mode=False, should_analyze=False):
    print(f"\n--- [{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 자동 매매 모니터링 사이클 ---")
    
    # 1. 업비트 클라이언트 및 잔고 확인
    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        print("[AutoTrader] API 키가 설정되지 않았습니다. 시뮬레이션 모드로 작동합니다.")
        sim_mode = True
        
    if sim_mode:
        krw_balance = 1000000.0
        btc_balance = 0.0
        avg_buy_price = 0.0
    else:
        try:
            krw_balance = float(upbit_client.get_balance("KRW"))
            btc_balance = float(upbit_client.get_balance(ticker))
            avg_buy_price = float(upbit_client.get_avg_buy_price(ticker))
        except Exception as e:
            print(f"[AutoTrader] 잔고 조회 실패: {e}")
            return False

    # 2. 현재 시세 및 변동성(ATR) 조회
    data = upbit_analyzer.get_upbit_data(ticker)
    if "error" in data:
        print(f"[AutoTrader] 시세 조회 실패: {data['error']}")
        return False
        
    current_price = data["현재가"]
    records = data["최근 30일 슈퍼트렌드 분석"]
    atr = upbit_analyzer._calc_atr(records)
    
    print(f"[AutoTrader] 현재가: {current_price:,}원 | 평단가: {avg_buy_price:,}원 | 보유수량: {btc_balance} BTC | 예수금: {krw_balance:,}원")

    # 3. 매매 감수 분기 (포지션 유무 기준)
    # 5,000원 이상의 가치가 있을 때 보유 중으로 판단
    if btc_balance * current_price >= 5000.0:
        # 포지션 보유 중 -> 익절/손절 감시 (10초마다 실시간 작동)
        profit_ratio = (current_price - avg_buy_price) / avg_buy_price
        
        # 기계적 익절선 (+2.5%)
        tp_price = avg_buy_price * 1.025
        
        # 손절선: -1.5% 또는 2.0 * ATR 중 타이트한 값 또는 기본 -1.5% 적용
        sl_atr = avg_buy_price - 2 * atr
        sl_fixed = avg_buy_price * 0.985
        sl_price = max(sl_atr, sl_fixed)
        
        print(f"[AutoTrader] 보유 중인 포지션 실시간 감시:")
        print(f"  - 수익률: {profit_ratio * 100:.2f}%")
        print(f"  - 목표가(익절): {tp_price:,.0f}원")
        print(f"  - 손절가(SL): {sl_price:,.0f}원 (ATR 기반: {sl_atr:,.0f}원 / 고정: {sl_fixed:,.0f}원)")

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
        else:
            print("[AutoTrader] 익절/손절선 미도달. 포지션을 유지(HOLD)합니다.")
        return False
            
    else:
        # 포지션 미보유 -> 신규 진입 Confluence 체크
        if not should_analyze:
            print("[AutoTrader] 포지션 미보유 상태. 신규 진입 분석 대기 중 (1시간 주기)")
            return False
            
        print("[AutoTrader] 포지션 미보유 상태 & 분석 주기 도달 -> 신규 진입 Confluence 분석 실행...")
        report = upbit_analyzer.run_analysis("자동 매매 신규 진입 판단", ticker)
        decision = parse_decision_from_report(report)
        
        print(f"[AutoTrader] 분석 결과 최종 결정: {decision}")
        
        if decision == "BUY":
            buy_amount = krw_balance * 0.3
            if buy_amount < 5000.0:
                print(f"[AutoTrader] 매수 가능 금액이 최소 주문금액(5,000원) 미만입니다. (계산액: {buy_amount:.0f}원)")
                return True
                
            msg = f"⚡ [데이브] Confluence 6단계 진입 조건 전원 합치! (신규 매수 진입)\n📌 대상: {ticker}\n💰 투입 금액: {buy_amount:,.0f}원 (전체 예수금의 30%)\n📊 현재가: {current_price:,}원\n🚨 시장가 매수 주문을 집행합니다."
            print(msg)
            send_telegram_message(msg)
            if not sim_mode:
                res = upbit_analyzer.execute_buy(ticker, buy_amount)
                print(res)
        else:
            print("[AutoTrader] Confluence 합치 실패 또는 HOLD 상태로 신규 매수를 진행하지 않습니다.")
        return True

if __name__ == "__main__":
    args = sys.argv[1:]
    sim = "--sim" in args
    
    if "--once" in args:
        # 단발성 실행 시에는 즉시 분석도 같이 수행하도록 함
        run_auto_trade_cycle(sim_mode=sim, should_analyze=True)
    else:
        print("🤖 데이브 업비트 실시간 자동 매매 데몬 시작 (시세 감시: 10초, 신규 분석: 1시간)")
        send_telegram_message("🤖 데이브 업비트 자동 매매 데몬 가동을 시작합니다 (실시간 10초 시세 감시 및 1시간 주기 신규 분석).")
        
        # 기동 시 즉시 첫 분석이 실행될 수 있도록 과거 시점으로 초기화
        last_analysis_time = datetime.datetime.now() - datetime.timedelta(hours=2)
        
        while True:
            try:
                now = datetime.datetime.now()
                should_analyze = (now - last_analysis_time) >= datetime.timedelta(hours=1)
                
                # 분석을 수행했으면 True 반환, 시세 감시만 했으면 False 반환
                analysis_executed = run_auto_trade_cycle(sim_mode=sim, should_analyze=should_analyze)
                
                if analysis_executed:
                    last_analysis_time = now
            except Exception as e:
                print(f"[Daemon Error] {e}")
            
            # 실시간 10초 주기 시세 감시
            time.sleep(10)
