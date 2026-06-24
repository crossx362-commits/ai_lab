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
    import check_holdings

    portfolio = check_holdings.get_upbit_portfolio()
    if not portfolio:
        print("API 키 미설정 또는 조회 실패")
        return

    krw = portfolio["krw"]
    holdings = portfolio["holdings"]
    total_value = portfolio["total_value"]

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
