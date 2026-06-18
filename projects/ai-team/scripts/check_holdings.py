#!/usr/bin/env python3
"""보유 코인 현황 확인"""
import os, sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Add _shared to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from _shared.env import load_env
load_env()

import pyupbit
# Use upbit_analyzer from Dave's tools
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "skills", "데이브_주식", "tools"))
import upbit_analyzer

def check_holdings():
    client = upbit_analyzer.get_upbit_client()
    if not client:
        print("API 키 미설정")
        return

    tickers = [
        "KRW-BTC", "KRW-ETH", "KRW-SOL", "KRW-XRP",
        "KRW-DOGE", "KRW-PEPE", "KRW-NEAR", "KRW-SUI",
        "KRW-SEI", "KRW-HBAR", "KRW-STX"
    ]

    print("\n=== 보유 코인 현황 ===\n")

    holdings = []
    for ticker in tickers:
        try:
            balance = float(client.get_balance(ticker))
            current_price = float(pyupbit.get_current_price(ticker))
            value = balance * current_price

            if value >= 5000:
                avg_price = float(client.get_avg_buy_price(ticker))
                profit_pct = (current_price - avg_price) / avg_price * 100

                holdings.append({
                    "ticker": ticker,
                    "coin": ticker.split("-")[1],
                    "balance": balance,
                    "avg_price": avg_price,
                    "current_price": current_price,
                    "value": value,
                    "profit_pct": profit_pct
                })
        except Exception:
            pass

    if not holdings:
        print("보유 코인 없음")
        return

    # 데이브 vs 레오 분류
    dave_coins = ["BTC", "ETH", "SOL", "XRP"]

    print("🔵 데이브 (보수적):")
    for h in holdings:
        if h["coin"] in dave_coins:
            emoji = "📈" if h["profit_pct"] > 0 else "📉"
            print(f"  {emoji} {h['coin']}: {h['balance']:.6f}개")
            print(f"     평단: {h['avg_price']:,.0f}원 | 현재: {h['current_price']:,.0f}원 | {h['profit_pct']:+.2f}%")
            print(f"     평가: {h['value']:,.0f}원\n")

    print("🔴 레오 (공격적):")
    for h in holdings:
        if h["coin"] not in dave_coins:
            emoji = "📈" if h["profit_pct"] > 0 else "📉"
            print(f"  {emoji} {h['coin']}: {h['balance']:.6f}개")
            print(f"     평단: {h['avg_price']:,.0f}원 | 현재: {h['current_price']:,.0f}원 | {h['profit_pct']:+.2f}%")
            print(f"     평가: {h['value']:,.0f}원\n")

    # 총합
    total_value = sum(h['value'] for h in holdings)
    total_profit = sum(h['value'] * h['profit_pct'] / 100 for h in holdings)
    total_profit_pct = (total_profit / total_value) * 100 if total_value > 0 else 0

    print("="*50)
    print(f"총 평가액: {total_value:,.0f}원")
    print(f"총 수익률: {total_profit_pct:+.2f}%")
    print("="*50)

if __name__ == "__main__":
    check_holdings()
