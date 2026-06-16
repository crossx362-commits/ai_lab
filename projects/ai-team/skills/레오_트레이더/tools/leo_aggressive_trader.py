# -*- coding: utf-8 -*-
"""
레오 공격적 단타 트레이더
고변동성 알트코인 전문, 빠른 수익 실현
"""
import os
import sys
import time
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

# 데이브의 upbit_analyzer 재사용
DAVE_TOOLS = os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools")
sys.path.insert(0, DAVE_TOOLS)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()

import pyupbit
import upbit_analyzer

# 레오 전용 감시 코인 (고변동성 알트)
LEO_TICKERS = [
    "KRW-DOGE",   # 밈코인 대장
    "KRW-PEPE",   # 밈코인 급등주
    "KRW-NEAR",   # 레이어1 고변동
    "KRW-SUI",    # 신규 레이어1
    "KRW-SEI",    # 신규 레이어1
    "KRW-HBAR",   # 엔터프라이즈
    "KRW-STX",    # 비트코인 L2
]

# 위험 관리 변수
consecutive_losses = 0
daily_loss_pct = 0.0
trades_today = []
last_trade_time = {}

# 설정
MAX_CONSECUTIVE_LOSSES = 3
MAX_DAILY_LOSS_PCT = -5.0
MAX_TRADES_PER_HOUR = 5
COOLDOWN_AFTER_LOSS = 1800  # 30분


def safe_float(val, default=0.0):
    if val is None:
        return default
    try:
        return float(val)
    except Exception:
        return default


def load_hyunbin_intel():
    """현빈의 시장 정보 로드"""
    try:
        import json
        intel_path = os.path.join(AI_TEAM_ROOT, "reports", "research", "crypto_market_intel.json")
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                return json.load(f)
    except Exception as e:
        print(f"[Leo] 현빈 정보 로드 실패: {e}")
    return None


def calculate_leo_score(ticker: str) -> dict:
    """레오용 단순화된 스코어 계산 (현빈 정보 활용)"""
    try:
        df = pyupbit.get_ohlcv(ticker, interval="minute60", count=50)
        if df is None or df.empty:
            return {"ticker": ticker, "score": 0, "error": "데이터 없음"}

        current_price = df['close'].iloc[-1]
        score = 0
        reasons = []

        # 현빈 정보 로드
        hyunbin_intel = load_hyunbin_intel()

        # 1. StochRSI 과매도 체크 (가장 중요)
        try:
            closes = df['close'].values
            # 단순 RSI 계산
            deltas = [closes[i] - closes[i-1] for i in range(1, len(closes))]
            gains = [d if d > 0 else 0 for d in deltas]
            losses = [-d if d < 0 else 0 for d in deltas]
            avg_gain = sum(gains[-14:]) / 14
            avg_loss = sum(losses[-14:]) / 14
            rs = avg_gain / avg_loss if avg_loss != 0 else 100
            rsi = 100 - (100 / (1 + rs))

            if rsi < 30:
                score += 3
                reasons.append(f"RSI 과매도 ({rsi:.1f})")
            elif rsi > 70:
                score -= 2
                reasons.append(f"RSI 과매수 ({rsi:.1f})")
        except Exception:
            pass

        # 2. Volume Spike (1.5배 이상)
        volumes = df['volume'].values
        avg_vol = sum(volumes[-20:-1]) / 19
        current_vol = volumes[-1]
        vol_ratio = current_vol / avg_vol if avg_vol > 0 else 1.0

        if vol_ratio >= 1.5:
            score += 2
            reasons.append(f"거래량 {vol_ratio:.1f}배")

        # 3. 1시간 모멘텀 (상승 중)
        price_1h_ago = df['close'].iloc[-2]
        momentum_1h = (current_price - price_1h_ago) / price_1h_ago * 100

        if momentum_1h > 1.0:
            score += 2
            reasons.append(f"1h 모멘텀 +{momentum_1h:.1f}%")
        elif momentum_1h < -1.0:
            score -= 1
            reasons.append(f"1h 모멘텀 {momentum_1h:.1f}%")

        # 4. 현빈 정보 활용 (공포탐욕, 김치프리미엄, 연준 이벤트)
        if hyunbin_intel:
            # 김치 프리미엄
            kp = hyunbin_intel.get("kimchi_premium", {}).get("premium_pct", 0)
            if kp >= 3.0:
                score += 1
                reasons.append(f"김프 +{kp:.1f}% (국내 관심)")

            # 공포탐욕지수 (극단 구간 보너스)
            fg = hyunbin_intel.get("fear_greed_index", {})
            if fg.get("action") == "BUY_SIGNAL":
                score += 2
                reasons.append(f"극공포 ({fg.get('value')})")
            elif fg.get("action") == "SELL_SIGNAL":
                score -= 2
                reasons.append(f"극탐욕 ({fg.get('value')})")

            # 연준 이벤트 (레오는 변동성 기회로 활용)
            fed = hyunbin_intel.get("fed_events", {})
            if fed.get("risk_level") == "HIGH":
                score += 1  # 데이브와 반대로 변동성 기회
                reasons.append("연준 이벤트 변동성")

        return {
            "ticker": ticker,
            "score": score,
            "current_price": current_price,
            "volume_ratio": vol_ratio,
            "momentum_1h": momentum_1h,
            "reasons": reasons
        }
    except Exception as e:
        return {"ticker": ticker, "score": 0, "error": str(e)}


def check_risk_limits() -> tuple[bool, str]:
    """위험 한도 체크"""
    global consecutive_losses, daily_loss_pct, trades_today

    # 일일 손실 한도
    if daily_loss_pct <= MAX_DAILY_LOSS_PCT:
        return False, f"일일 손실 한도 도달 ({daily_loss_pct:.2f}% ≤ {MAX_DAILY_LOSS_PCT}%)"

    # 연속 손절 한도
    if consecutive_losses >= MAX_CONSECUTIVE_LOSSES:
        return False, f"연속 손절 {consecutive_losses}회 - 30분 휴식 필요"

    # 시간당 거래 한도
    now = time.time()
    recent_trades = [t for t in trades_today if now - t < 3600]
    if len(recent_trades) >= MAX_TRADES_PER_HOUR:
        return False, f"1시간 내 {len(recent_trades)}회 거래 - 과열 방지"

    return True, "OK"


def check_dave_holdings(ticker: str) -> bool:
    """데이브가 보유한 코인인지 체크 (충돌 방지)"""
    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        return False

    try:
        balance = safe_float(upbit_client.get_balance(ticker))
        current_price = float(pyupbit.get_current_price(ticker))

        # 5,000원 이상 보유 시 데이브 소유로 간주
        if balance * current_price >= 5000:
            return True
    except Exception:
        pass

    return False


def run_leo_cycle(sim_mode=False):
    """레오 단타 사이클 실행"""
    global consecutive_losses, daily_loss_pct, trades_today, last_trade_time

    print(f"\n⚡ [{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 레오 단타 스캔")

    # 위험 한도 체크
    can_trade, risk_msg = check_risk_limits()
    if not can_trade:
        print(f"[Leo] {risk_msg} - 거래 중단")
        return

    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        print("[Leo] API 키 미설정 - 시뮬레이션 모드")
        sim_mode = True

    # KRW 잔고 조회
    if sim_mode:
        total_krw = 1000000.0
    else:
        try:
            total_krw = safe_float(upbit_client.get_balance("KRW"))
        except Exception as e:
            print(f"[Leo] KRW 잔고 조회 실패: {e}")
            return

    # 데이브 예약금 제외 (40%)
    dave_reserve = total_krw * 0.4
    leo_budget = total_krw - dave_reserve

    if leo_budget < 5000:
        print(f"[Leo] 운용 가능 금액 부족 ({leo_budget:,.0f}원)")
        return

    # 보유 포지션 체크 및 익절/손절 감시
    leo_positions = []
    for ticker in LEO_TICKERS:
        if sim_mode:
            continue

        try:
            balance = safe_float(upbit_client.get_balance(ticker))
            avg_buy_price = safe_float(upbit_client.get_avg_buy_price(ticker))
            current_price = float(pyupbit.get_current_price(ticker))

            if balance * current_price >= 5000:
                # 데이브 소유인지 체크
                if check_dave_holdings(ticker):
                    print(f"[Leo] {ticker} - 데이브 보유 중, 스킵")
                    continue

                profit_pct = (current_price - avg_buy_price) / avg_buy_price * 100

                # 익절 체크
                if profit_pct >= 5.0:
                    # 전량 익절
                    print(f"💰 [Leo] {ticker} 2차 익절 +{profit_pct:.2f}% - 전량 매도")
                    msg = f"💰 [레오] {ticker} 익절\n수익률: +{profit_pct:.2f}%\n매도가: {current_price:,}원"
                    send_telegram_message(msg)
                    if not sim_mode:
                        upbit_analyzer.execute_sell(ticker, balance)
                    consecutive_losses = 0  # 익절 시 연속 손절 리셋

                elif profit_pct >= 3.0:
                    # 50% 익절
                    sell_amount = balance * 0.5
                    print(f"💰 [Leo] {ticker} 1차 익절 +{profit_pct:.2f}% - 50% 매도")
                    msg = f"💰 [레오] {ticker} 1차 익절\n수익률: +{profit_pct:.2f}%\n매도 비중: 50%"
                    send_telegram_message(msg)
                    if not sim_mode:
                        upbit_analyzer.execute_sell(ticker, sell_amount)

                # 손절 체크
                elif profit_pct <= -2.0:
                    print(f"🛑 [Leo] {ticker} 손절 {profit_pct:.2f}% - 전량 매도")
                    msg = f"🛑 [레오] {ticker} 손절\n손실: {profit_pct:.2f}%\n매도가: {current_price:,}원"
                    send_telegram_message(msg)
                    if not sim_mode:
                        upbit_analyzer.execute_sell(ticker, balance)
                    consecutive_losses += 1
                    daily_loss_pct += profit_pct

                leo_positions.append({
                    "ticker": ticker,
                    "balance": balance,
                    "avg_buy_price": avg_buy_price,
                    "current_price": current_price,
                    "profit_pct": profit_pct
                })
        except Exception as e:
            print(f"[Leo] {ticker} 포지션 체크 실패: {e}")

    # 최대 3개 코인 동시 보유 제한
    if len(leo_positions) >= 3:
        print(f"[Leo] 최대 보유 개수 도달 ({len(leo_positions)}/3) - 신규 진입 금지")
        return

    # 스캔 및 진입
    scanned = []
    for ticker in LEO_TICKERS:
        # 데이브 보유 체크
        if check_dave_holdings(ticker):
            continue

        # 이미 레오가 보유 중
        if any(p["ticker"] == ticker for p in leo_positions):
            continue

        # 1시간 내 재진입 금지
        if ticker in last_trade_time:
            if time.time() - last_trade_time[ticker] < 3600:
                continue

        result = calculate_leo_score(ticker)
        if "error" not in result:
            scanned.append(result)

    if not scanned:
        print("[Leo] 스캔 가능한 코인이 없습니다")
        return

    scanned.sort(key=lambda x: x["score"], reverse=True)

    print("\n=== [레오 스캔 랭킹] ===")
    for item in scanned[:5]:
        reasons_str = ", ".join(item.get("reasons", []))
        print(f"  - {item['ticker']}: {item['score']}점 | {reasons_str}")
    print("======================\n")

    best = scanned[0]

    # 최소 진입 점수: 1점 이상
    if best["score"] >= 1:
        ticker = best["ticker"]

        # 투입 금액 계산 (30~50% 랜덤)
        import random
        invest_pct = random.uniform(0.3, 0.5)
        buy_amount = leo_budget * invest_pct * 0.995  # 수수료 여유

        if buy_amount < 5000:
            print(f"[Leo] 매수 금액 부족 ({buy_amount:,.0f}원)")
            return

        current_price = best["current_price"]
        tp1 = current_price * 1.03  # +3%
        tp2 = current_price * 1.05  # +5%
        sl = current_price * 0.98   # -2%

        msg = (
            f"⚡ [레오] 단타 진입!\n"
            f"📌 {ticker} (점수: {best['score']}점)\n"
            f"💰 투입: {buy_amount:,.0f}원 ({invest_pct*100:.0f}%)\n"
            f"📊 현재가: {current_price:,}원\n"
            f"🎯 1차 익절: {tp1:,}원 (+3%)\n"
            f"🎯 2차 익절: {tp2:,}원 (+5%)\n"
            f"🛑 손절: {sl:,}원 (-2%)"
        )

        print(msg)
        send_telegram_message(msg)

        if not sim_mode:
            res = upbit_analyzer.execute_buy(ticker, buy_amount)
            print(res)

        last_trade_time[ticker] = time.time()
        trades_today.append(time.time())

    else:
        print(f"[Leo] 진입 점수 부족 (최고: {best['ticker']} {best['score']}점)")


def send_status_report(sim_mode=False):
    """2시간마다 현황 보고"""
    global last_report_time

    now = time.time()
    if not hasattr(send_status_report, 'last_report'):
        send_status_report.last_report = 0

    if now - send_status_report.last_report < 7200:  # 2시간
        return

    send_status_report.last_report = now

    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None or sim_mode:
        return

    try:
        # 레오 포지션 현황
        leo_positions = []
        for ticker in LEO_TICKERS:
            balance = safe_float(upbit_client.get_balance(ticker))
            current_price = float(pyupbit.get_current_price(ticker))

            if balance * current_price >= 5000 and not check_dave_holdings(ticker):
                avg = safe_float(upbit_client.get_avg_buy_price(ticker))
                profit = (current_price - avg) / avg * 100
                leo_positions.append(f"  • {ticker.split('-')[1]}: +{profit:.2f}%")

        pos_str = "\n".join(leo_positions) if leo_positions else "  • 없음"

        msg = (
            f"⚡ [레오] 2시간 현황 보고\n\n"
            f"📦 보유 포지션:\n{pos_str}\n\n"
            f"📊 일일 손익: {daily_loss_pct:+.2f}%\n"
            f"🔄 연속 손절: {consecutive_losses}회\n"
            f"🤖 데몬 정상 가동 중"
        )

        send_telegram_message(msg)
        print("[Leo] 2시간 현황 보고 전송 완료")
    except Exception as e:
        print(f"[Leo] 현황 보고 오류: {e}")


if __name__ == "__main__":
    args = sys.argv[1:]
    sim = "--sim" in args

    if "--once" in args:
        run_leo_cycle(sim_mode=sim)
    else:
        print("⚡ 레오 공격적 단타 트레이더 시작 (10초 주기)")
        send_telegram_message("⚡ [레오] 공격적 단타 트레이더 가동 시작")

        while True:
            try:
                run_leo_cycle(sim_mode=sim)
                send_status_report(sim_mode=sim)
            except Exception as e:
                print(f"[Leo Daemon Error] {e}")

            time.sleep(10)  # 10초 주기
