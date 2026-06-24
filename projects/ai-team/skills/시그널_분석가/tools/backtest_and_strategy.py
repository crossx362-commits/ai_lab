#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
시그널 백테스팅 & 개선 전략
- 과거 데이터로 매매 성과 측정
- RSI + 볼린저밴드 + 이동평균 기반 개선 전략
- 손절/익절/트레일링 스탑 적용
"""
import os
import sys
import json
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
load_env(str(WORKSPACE_ROOT))

try:
    import pyupbit
    import pandas as pd
except ImportError as e:
    print(f"필수 모듈 없음: {e}")
    sys.exit(1)


class TradingBacktester:
    """통합 백테스팅 엔진"""

    def __init__(self, strategy="improved", initial_balance=1_000_000):
        self.strategy = strategy
        self.initial_balance = initial_balance
        self.balance = initial_balance
        self.positions = {}
        self.trades = []
        self.daily_balance = []

        # 리스크 관리
        if strategy == "improved":
            self.stop_loss_pct = -2.0
            self.take_profit_pct = 3.0
            self.trailing_stop_pct = -1.5
            self.max_position_pct = 15
            self.consecutive_loss_limit = 2
            self.daily_trade_limit = 3
        elif strategy == "leo":
            self.stop_loss_pct = -3.0
            self.take_profit_pct = None
            self.trailing_stop_pct = None
            self.max_position_pct = 20
            self.consecutive_loss_limit = 3
            self.daily_trade_limit = 5
        else:  # dave
            self.stop_loss_pct = -5.0
            self.take_profit_pct = None
            self.trailing_stop_pct = None
            self.max_position_pct = 15
            self.consecutive_loss_limit = None
            self.daily_trade_limit = None

        self.consecutive_losses = 0
        self.daily_trades_count = {}

    def calculate_rsi(self, prices: pd.Series, period: int = 14) -> float:
        """RSI 계산"""
        if len(prices) < period + 1:
            return 50.0
        delta = prices.diff()
        gain = (delta.where(delta > 0, 0)).rolling(window=period).mean()
        loss = (-delta.where(delta < 0, 0)).rolling(window=period).mean()
        rs = gain / loss
        rsi = 100 - (100 / (1 + rs))
        return float(rsi.iloc[-1]) if not rsi.empty and not pd.isna(rsi.iloc[-1]) else 50.0

    def calculate_bollinger_bands(self, prices: pd.Series, period: int = 20) -> dict:
        """볼린저 밴드"""
        if len(prices) < period:
            return {'upper': 0, 'middle': 0, 'lower': 0}
        sma = prices.rolling(window=period).mean()
        std = prices.rolling(window=period).std()
        upper = sma + (std * 2)
        lower = sma - (std * 2)
        return {
            'upper': float(upper.iloc[-1]) if not upper.empty else 0,
            'middle': float(sma.iloc[-1]) if not sma.empty else 0,
            'lower': float(lower.iloc[-1]) if not lower.empty else 0
        }

    def calculate_signal(self, df: pd.DataFrame, idx: int) -> dict:
        """신호 계산"""
        close = float(df['close'].iloc[idx])
        volume = float(df['volume'].iloc[idx])

        prices = df['close'].iloc[:idx+1]
        volumes = df['volume'].iloc[:idx+1]

        # 이동평균
        ma5 = float(prices.iloc[-5:].mean()) if len(prices) >= 5 else close
        ma20 = float(prices.iloc[-20:].mean()) if len(prices) >= 20 else close
        ma60 = float(prices.iloc[-60:].mean()) if len(prices) >= 60 else close

        # RSI & 볼린저 (improved만)
        rsi = self.calculate_rsi(prices) if self.strategy == "improved" else 50.0
        bb = self.calculate_bollinger_bands(prices) if self.strategy == "improved" else {}

        # 거래량
        volume_ma = float(volumes.iloc[-20:].mean()) if len(volumes) >= 20 else volume

        # 변화율
        prev = float(df['close'].iloc[idx-1]) if idx > 0 else close
        change = ((close - prev) / prev * 100) if prev > 0 else 0

        # 점수 계산
        if self.strategy == "improved":
            score = 50
            if close > ma5 > ma20:
                score += 20
            elif close < ma5 < ma20:
                score -= 20
            if ma5 > ma20 > ma60:
                score += 20
            elif ma5 < ma20 < ma60:
                score -= 20
            if rsi < 30:
                score += 15
            elif rsi > 70:
                score -= 15
            elif 40 <= rsi <= 60:
                score += 5
            if bb.get('lower', 0) > 0 and close < bb['lower']:
                score += 15
            elif bb.get('upper', 0) > 0 and close > bb['upper']:
                score -= 10
            if volume_ma > 0:
                vol_ratio = volume / volume_ma
                if vol_ratio > 2.0:
                    score += 20
                elif vol_ratio > 1.5:
                    score += 10
                elif vol_ratio < 0.5:
                    score -= 10
            if change > 5.0:
                score -= 10
            elif change < -3.0:
                score += 10
        else:  # leo/dave (기존 로직)
            score = 0
            if close > ma5:
                score += 25
            if close > ma20:
                score += 25
            if change > 1.0:
                score += 20
            if volume_ma > 0 and volume > volume_ma * 1.5:
                score += 20
            if change < -3.0:
                score -= 20

        return {
            'score': max(0, min(100, score)),
            'change': change,
            'rsi': rsi,
            'close': close
        }

    def decide_action(self, ticker: str, signal: dict, date: datetime) -> dict:
        """매매 결정"""
        score = signal['score']
        change = signal['change']
        close = signal['close']
        rsi = signal.get('rsi', 50)

        # 연속 손실 제한
        if self.consecutive_loss_limit and self.consecutive_losses >= self.consecutive_loss_limit:
            return {"action": "HOLD", "percentage": 0}

        # 일일 거래 제한
        if self.daily_trade_limit:
            date_str = date.strftime("%Y-%m-%d")
            if self.daily_trades_count.get(date_str, 0) >= self.daily_trade_limit:
                return {"action": "HOLD", "percentage": 0}

        # 보유 중 손절/익절
        if ticker in self.positions:
            pos = self.positions[ticker]
            profit_pct = ((close - pos['avg_price']) / pos['avg_price'] * 100)
            highest = pos.get('highest_price', pos['avg_price'])

            if close > highest:
                self.positions[ticker]['highest_price'] = close
                highest = close

            # 손절
            if profit_pct <= self.stop_loss_pct:
                return {"action": "SELL", "percentage": 100}

            # 익절
            if self.take_profit_pct and profit_pct >= self.take_profit_pct:
                return {"action": "SELL", "percentage": 100}

            # 트레일링
            if self.trailing_stop_pct:
                trailing_loss = ((close - highest) / highest * 100)
                if trailing_loss <= self.trailing_stop_pct:
                    return {"action": "SELL", "percentage": 100}

            # 신호 약화
            if self.strategy == "improved" and score < 40:
                return {"action": "SELL", "percentage": 50}

        # 진입
        if self.strategy == "improved":
            if score >= 75 and rsi < 70:
                return {"action": "BUY", "percentage": 15}
            elif score >= 65 and rsi < 60:
                return {"action": "BUY", "percentage": 10}
            elif score >= 55 and rsi < 50 and change > 0:
                return {"action": "BUY", "percentage": 5}
        else:  # leo/dave
            if score >= 85:
                return {"action": "BUY", "percentage": 20 if self.strategy == "leo" else 15}
            elif score >= 70:
                return {"action": "BUY", "percentage": 10 if self.strategy == "leo" else 8}
            elif score >= 55:
                return {"action": "BUY", "percentage": 5}
            elif change < -3.0 and ticker in self.positions:
                return {"action": "SELL", "percentage": 50}

        return {"action": "HOLD", "percentage": 0}

    def execute_trade(self, ticker: str, action: str, pct: int, price: float, date: datetime):
        """거래 실행"""
        if action == "BUY" and self.balance > 0:
            max_cost = self.initial_balance * (self.max_position_pct / 100)
            current_val = 0
            if ticker in self.positions:
                current_val = self.positions[ticker]['amount'] * price
            available = max_cost - current_val
            if available <= 0:
                return

            buy_amt = min(self.balance * (pct / 100), available) / price
            cost = buy_amt * price

            if ticker not in self.positions:
                self.positions[ticker] = {"amount": 0, "avg_price": 0, "highest_price": price}

            old_amt = self.positions[ticker]["amount"]
            old_avg = self.positions[ticker]["avg_price"]
            new_amt = old_amt + buy_amt
            new_avg = ((old_avg * old_amt) + (price * buy_amt)) / new_amt if new_amt > 0 else price

            self.positions[ticker]["amount"] = new_amt
            self.positions[ticker]["avg_price"] = new_avg
            self.positions[ticker]["highest_price"] = price
            self.balance -= cost

            self.trades.append({"date": date, "ticker": ticker, "action": "BUY", "price": price, "amount": buy_amt})
            if self.daily_trade_limit:
                ds = date.strftime("%Y-%m-%d")
                self.daily_trades_count[ds] = self.daily_trades_count.get(ds, 0) + 1

        elif action == "SELL" and ticker in self.positions and self.positions[ticker]["amount"] > 0:
            sell_amt = self.positions[ticker]["amount"] * (pct / 100)
            revenue = sell_amt * price
            profit = revenue - (sell_amt * self.positions[ticker]["avg_price"])
            profit_rate = (profit / (sell_amt * self.positions[ticker]["avg_price"]) * 100)

            self.positions[ticker]["amount"] -= sell_amt
            self.balance += revenue

            if profit < 0:
                self.consecutive_losses += 1
            else:
                self.consecutive_losses = 0

            self.trades.append({"date": date, "ticker": ticker, "action": "SELL", "price": price, "profit": profit, "profit_rate": profit_rate})
            if self.daily_trade_limit:
                ds = date.strftime("%Y-%m-%d")
                self.daily_trades_count[ds] = self.daily_trades_count.get(ds, 0) + 1

            if self.positions[ticker]["amount"] < 0.0001:
                del self.positions[ticker]

    def calculate_total_value(self, prices: dict) -> float:
        total = self.balance
        for ticker, pos in self.positions.items():
            if ticker in prices:
                total += pos["amount"] * prices[ticker]
        return total

    def run(self, tickers: list, days: int = 30):
        """백테스팅 실행"""
        print(f"\n{'='*60}")
        print(f"{self.strategy.upper()} 전략 백테스팅 (초기자본: {self.initial_balance:,}원)")
        print(f"{'='*60}\n")

        all_data = {}
        for ticker in tickers:
            try:
                df = pyupbit.get_ohlcv(ticker, interval="day", count=days+60)
                if df is not None and len(df) >= days:
                    all_data[ticker] = df
                    print(f"✓ {ticker}: {len(df)}일")
            except Exception as e:
                print(f"✗ {ticker}: {e}")

        if not all_data:
            return

        min_len = min(len(df) for df in all_data.values())
        start_idx = 60 if self.strategy == "improved" else 20

        for day_idx in range(start_idx, min_len):
            current_prices = {}
            for ticker, df in all_data.items():
                signal = self.calculate_signal(df, day_idx)
                current_prices[ticker] = signal['close']
                decision = self.decide_action(ticker, signal, df.index[day_idx])

                if decision['action'] in ['BUY', 'SELL']:
                    self.execute_trade(ticker, decision['action'], decision['percentage'], signal['close'], df.index[day_idx])

            total = self.calculate_total_value(current_prices)
            self.daily_balance.append({"date": all_data[tickers[0]].index[day_idx], "total": total})

        self.print_results()

    def print_results(self):
        """결과 출력"""
        final = self.daily_balance[-1]['total'] if self.daily_balance else self.initial_balance
        ret = ((final - self.initial_balance) / self.initial_balance * 100)

        print(f"\n{'='*60}")
        print(f"결과")
        print(f"{'='*60}")
        print(f"초기: {self.initial_balance:,}원 → 최종: {final:,.0f}원")
        print(f"수익률: {ret:+.2f}%")
        print(f"거래: {len(self.trades)}회")

        sells = [t for t in self.trades if t['action'] == 'SELL']
        if sells:
            wins = [t for t in sells if t.get('profit', 0) > 0]
            win_rate = len(wins) / len(sells) * 100
            avg_profit = sum(t.get('profit', 0) for t in sells) / len(sells)
            print(f"승률: {win_rate:.1f}% | 평균손익: {avg_profit:+,.0f}원")

        if self.positions:
            print(f"\n보유 중:")
            for ticker, pos in self.positions.items():
                print(f"  {ticker}: {pos['amount']:.4f}개 @ {pos['avg_price']:.0f}원")

        print(f"\n최근 거래:")
        for t in self.trades[-10:]:
            if t['action'] == 'SELL':
                print(f"  {t['date'].strftime('%Y-%m-%d')} | SELL | {t['ticker']:12s} | {t['price']:8,.0f}원 | {t.get('profit_rate', 0):+6.1f}%")
            else:
                print(f"  {t['date'].strftime('%Y-%m-%d')} | BUY  | {t['ticker']:12s} | {t['price']:8,.0f}원")
        print(f"{'='*60}\n")


def main():
    leo_tickers = ["KRW-DOGE", "KRW-PEPE", "KRW-NEAR", "KRW-SUI", "KRW-SEI", "KRW-HBAR", "KRW-STX"]
    dave_tickers = ["KRW-BTC", "KRW-ETH", "KRW-SOL", "KRW-XRP"]

    # 기존 레오 전략
    print("\n🔥 기존 레오 전략")
    leo_old = TradingBacktester(strategy="leo", initial_balance=1_000_000)
    leo_old.run(leo_tickers, days=30)

    # 개선된 전략
    print("\n✨ 개선된 전략 (RSI+볼린저+손절익절)")
    improved = TradingBacktester(strategy="improved", initial_balance=1_000_000)
    improved.run(leo_tickers, days=30)

    # 결과 저장
    output = WORKSPACE_ROOT / "reports" / "research"
    output.mkdir(parents=True, exist_ok=True)

    results = {
        "timestamp": datetime.now().isoformat(),
        "strategies": {
            "original_leo": {
                "return": leo_old.daily_balance[-1]['total'] / leo_old.initial_balance - 1 if leo_old.daily_balance else 0,
                "trades": len(leo_old.trades),
                "final_value": leo_old.daily_balance[-1]['total'] if leo_old.daily_balance else leo_old.initial_balance
            },
            "improved": {
                "return": improved.daily_balance[-1]['total'] / improved.initial_balance - 1 if improved.daily_balance else 0,
                "trades": len(improved.trades),
                "final_value": improved.daily_balance[-1]['total'] if improved.daily_balance else improved.initial_balance,
                "risk_management": {
                    "stop_loss": improved.stop_loss_pct,
                    "take_profit": improved.take_profit_pct,
                    "trailing_stop": improved.trailing_stop_pct
                }
            }
        }
    }

    with open(output / "backtest_results.json", "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)

    print(f"\n결과 저장: {output / 'backtest_results.json'}")


if __name__ == "__main__":
    main()
