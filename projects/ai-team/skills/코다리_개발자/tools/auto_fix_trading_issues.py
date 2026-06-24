#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
코다리: 트레이딩 문제 자동 감지 및 수정
- 손절 미실행 감지 → 강제 매도
- 감시하지 않는 코인 보유 → 경고 및 매도
- 평단가 0원 오류 → 데이터 정정
"""
import os
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(AI_TEAM_ROOT / "skills/데이브_주식/tools"))

from _shared.env import load_env
from _shared.notify import send

load_env(str(WORKSPACE_ROOT))


def check_and_fix_positions():
    """보유 포지션 문제 감지 및 자동 수정"""
    try:
        import pyupbit
        import upbit_analyzer
        import json

        upbit = upbit_analyzer.get_upbit_client()
        if not upbit:
            return

        # 레오 감시 대상 코인
        LEO_TICKERS = [
            "KRW-SOL", "KRW-ETH", "KRW-BTC",
            "KRW-AVAX", "KRW-LINK", "KRW-NEAR",
            "KRW-DOGE", "KRW-STX"
        ]

        # 모든 보유 코인 확인
        balances = upbit.get_balances()
        issues_found = []
        actions_taken = []

        # 실제 보유 코인 수 계산
        actual_positions = 0
        for bal in balances:
            if bal['currency'] == 'KRW':
                continue
            balance = float(bal['balance'])
            if balance > 0:
                ticker = f"KRW-{bal['currency']}"
                current_price = pyupbit.get_current_price(ticker)
                if current_price and balance * current_price >= 5000:
                    actual_positions += 1

        # RiskGuard 상태 파일 검증
        risk_state_path = WORKSPACE_ROOT / "output/trading_logs/leo_risk_state.json"
        if risk_state_path.exists():
            try:
                with open(risk_state_path, 'r', encoding='utf-8') as f:
                    risk_state = json.load(f)

                stored_positions = risk_state.get('current_positions', 0)
                if stored_positions != actual_positions:
                    issues_found.append(f"RiskGuard 포지션 불일치: {stored_positions} → {actual_positions}")
                    print(f"[코다리] ⚠️ RiskGuard 포지션 불일치 감지: {stored_positions} → {actual_positions}")

                    # 수정
                    risk_state['current_positions'] = actual_positions
                    with open(risk_state_path, 'w', encoding='utf-8') as f:
                        json.dump(risk_state, f, indent=2)

                    actions_taken.append(f"RiskGuard 포지션 수정: {actual_positions}")
                    print(f"[코다리] ✅ RiskGuard 상태 수정 완료")
            except Exception as e:
                print(f"[코다리] RiskGuard 체크 실패: {e}")

        for bal in balances:
            currency = bal['currency']
            if currency == 'KRW':
                continue

            ticker = f"KRW-{currency}"
            balance = float(bal['balance'])
            avg_price = float(bal['avg_buy_price'])

            if balance <= 0:
                continue

            # 현재가 조회
            current_price = pyupbit.get_current_price(ticker)
            if not current_price:
                continue

            value = balance * current_price
            if value < 5000:  # 5000원 미만은 무시
                continue

            # 문제 1: 감시하지 않는 코인 보유
            if ticker not in LEO_TICKERS:
                issues_found.append(f"{currency}: 감시 목록 외 보유 ({value:,.0f}원)")
                print(f"[코다리] ⚠️ {currency} 감시 목록에 없음 - 매도 필요")

                # 자동 매도
                try:
                    result = upbit.sell_market_order(ticker, balance)
                    if result:
                        actions_taken.append(f"{currency} 매도 (감시 외)")
                        print(f"[코다리] ✅ {currency} 자동 매도 완료")
                except Exception as e:
                    print(f"[코다리] ❌ {currency} 매도 실패: {e}")
                continue

            # 문제 2: 평단가 0원
            if avg_price <= 0:
                issues_found.append(f"{currency}: 평단가 0원 오류")
                print(f"[코다리] ⚠️ {currency} 평단가 0원 - 매도 필요")

                try:
                    result = upbit.sell_market_order(ticker, balance)
                    if result:
                        actions_taken.append(f"{currency} 매도 (평단가 오류)")
                        print(f"[코다리] ✅ {currency} 자동 매도 완료")
                except Exception as e:
                    print(f"[코다리] ❌ {currency} 매도 실패: {e}")
                continue

            # 문제 3: 손절 기준 초과
            profit_pct = (current_price - avg_price) / avg_price * 100
            if profit_pct <= -2.0:
                issues_found.append(f"{currency}: 손절 기준 초과 ({profit_pct:.1f}%)")
                print(f"[코다리] ⚠️ {currency} 손절 미실행 ({profit_pct:.1f}%) - 매도 필요")

                try:
                    result = upbit.sell_market_order(ticker, balance)
                    if result:
                        actions_taken.append(f"{currency} 손절 ({profit_pct:.1f}%)")
                        print(f"[코다리] ✅ {currency} 손절 완료")
                except Exception as e:
                    print(f"[코다리] ❌ {currency} 손절 실패: {e}")

        # 소수점 잔고 확인 및 정리
        dust_cleaned = []
        for bal in balances:
            currency = bal['currency']
            if currency == 'KRW':
                continue

            balance = float(bal['balance'])
            if 0 < balance < 0.00001:  # 먼지 잔고
                ticker = f"KRW-{currency}"
                dust_cleaned.append(f"{currency}: {balance:.8f}개 (먼지)")
                print(f"[코다리] 먼지 잔고 발견: {ticker} {balance:.8f}개")

        if dust_cleaned:
            print(f"[코다리] 먼지 잔고 {len(dust_cleaned)}개 발견 (거래 불가, 무시)")

        # 알림
        if issues_found:
            summary = "\n".join(issues_found[:5])  # 최대 5개만
            send(f"⚠️ [코다리] 문제 {len(issues_found)}개:\n{summary}")

        if actions_taken:
            summary = "\n".join(actions_taken[:5])  # 최대 5개만
            send(f"✅ [코다리] 수정 {len(actions_taken)}개:\n{summary}")
            print(f"\n[코다리] 자동 수정:\n{summary}")

        return len(issues_found), len(actions_taken)

    except Exception as e:
        print(f"[코다리] 포지션 체크 실패: {e}")
        return 0, 0


if __name__ == "__main__":
    print("[코다리] 트레이딩 문제 자동 체크 시작...")
    issues, actions = check_and_fix_positions()
    print(f"[코다리] 완료: {issues}개 문제 감지, {actions}개 자동 수정")
