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

def get_upbit_portfolio():
    """업비트 잔고 및 보유 코인 평가 정보 조회"""
    client = upbit_analyzer.get_upbit_client()
    if not client:
        return None

    try:
        krw = float(client.get_balance("KRW"))
    except Exception:
        krw = 0.0

    holdings = []
    total_value = krw

    try:
        balances = client.get_balances()
    except Exception:
        balances = []

    for b in balances:
        currency = b.get('currency')
        if currency == "KRW":
            continue
        try:
            balance = float(b.get('balance', 0))
            locked = float(b.get('locked', 0))
            total_balance = balance + locked
            if total_balance <= 0:
                continue

            ticker = f"KRW-{currency}"
            current_price = pyupbit.get_current_price(ticker)
            if current_price is None:
                continue

            current_price = float(current_price)
            value = total_balance * current_price

            if value >= 5000:
                avg_price = float(b.get('avg_buy_price', 0))
                profit_pct = ((current_price - avg_price) / avg_price * 100) if avg_price > 0 else 0.0

                holdings.append({
                    "ticker": ticker,
                    "coin": currency,
                    "balance": total_balance,
                    "avg_price": avg_price,
                    "current_price": current_price,
                    "value": value,
                    "profit_pct": profit_pct
                })

                total_value += value
        except Exception:
            pass

    return {
        "krw": krw,
        "holdings": holdings,
        "total_value": total_value
    }


def check_holdings():
    portfolio = get_upbit_portfolio()
    if not portfolio:
        print("API 키 미설정 또는 조회 실패")
        return

    holdings = portfolio["holdings"]

    print("\n=== 보유 코인 현황 ===\n")

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
    total_value = portfolio["total_value"]
    coin_value = sum(h['value'] for h in holdings)
    total_profit = sum(h['value'] * h['profit_pct'] / 100 for h in holdings)
    total_profit_pct = (total_profit / coin_value) * 100 if coin_value > 0 else 0

    print("="*50)
    print(f"총 평가액 (KRW 포함): {total_value:,.0f}원")
    print(f"코인 평가액: {coin_value:,.0f}원")
    print(f"코인 수익률: {total_profit_pct:+.2f}%")
    print("="*50)

if __name__ == "__main__":
    check_holdings()
