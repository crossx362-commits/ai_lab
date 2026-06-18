# -*- coding: utf-8 -*-
"""
일일 잔고 점검 스크립트
매일 아침 9시 잔고 확인 및 리포트
"""
import os
import sys

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

DAVE_TOOLS = os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools")
sys.path.insert(0, DAVE_TOOLS)

from _shared.env import load_env
from _shared.notify import send

load_env()

import pyupbit
import upbit_analyzer


def daily_balance_report():
    """일일 잔고 리포트"""
    client = upbit_analyzer.get_upbit_client()
    if not client:
        print("API 키 미설정")
        return

    # KRW 잔고
    krw = float(client.get_balance("KRW"))

    # 보유 코인
    tickers = [
        "KRW-BTC", "KRW-ETH", "KRW-SOL", "KRW-XRP",
        "KRW-DOGE", "KRW-PEPE", "KRW-NEAR", "KRW-SUI",
        "KRW-SEI", "KRW-HBAR", "KRW-STX"
    ]

    holdings = []
    total_value = krw

    for ticker in tickers:
        try:
            balance = float(client.get_balance(ticker))
            current_price = float(pyupbit.get_current_price(ticker))
            value = balance * current_price

            if value >= 5000:
                avg_price = float(client.get_avg_buy_price(ticker))
                profit_pct = (current_price - avg_price) / avg_price * 100

                holdings.append({
                    "coin": ticker.split("-")[1],
                    "value": value,
                    "profit_pct": profit_pct
                })

                total_value += value
        except Exception:
            pass

    # 리포트 생성
    holdings_str = ""
    for h in holdings:
        emoji = "📈" if h["profit_pct"] > 0 else "📉"
        holdings_str += f"{emoji} {h['coin']} {h['value']/1000:.1f}k ({h['profit_pct']:+.1f}%)\n"

    if not holdings_str:
        holdings_str = "없음\n"

    msg = f"""💰 일일 잔고 점검

KRW: {krw:,.0f}원
보유:
{holdings_str}
총 자산: {total_value:,.0f}원
"""

    print(msg)
    send(msg)


if __name__ == "__main__":
    daily_balance_report()
