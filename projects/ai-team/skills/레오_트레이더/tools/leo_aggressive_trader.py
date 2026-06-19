# -*- coding: utf-8 -*-
"""
레오 공격적 단타 트레이더
고변동성 알트코인 전문, 빠른 수익 실현
"""
import os
import sys
import time
import datetime
import subprocess

def get_common_trader_prompt():
    """공통 트레이더 시스템 프롬프트 (데이브와 동일)"""
    return """너는 암호화폐 매매 최종 판단 AI다.
목표는 제한된 토큰으로 기대값이 양수인 거래를 반복하는 것이다.

원칙:
- 완벽한 진입점보다 확률 우위가 중요하다.
- HOLD는 명확한 회피 사유가 있을 때만 선택한다.
- 단순 불확실성만으로 HOLD 금지.
- 예상 승률 55% 이상 또는 RR 1:1.5 이상이면 진입 검토.
- 항상 BUY, SELL, HOLD 중 하나만 선택한다.
- 설명은 40자 이내.
- 사고 과정 출력 금지.

강제 HOLD:
- FOMC/CPI 전후 24시간
- 연속손실 제한 초과
- 일일손실 제한 초과
- 거래 쿨다운 중

출력 JSON:
{
  "decision": "BUY|SELL|HOLD",
  "percentage": 0|5|10|20|40|50,
  "confidence": 0-100,
  "reason": "40자 이내"
}"""

def get_leo_system_prompt():
    """레오: 공격적 단타 트레이더"""
    common = get_common_trader_prompt()
    leo_specific = """

--- 레오 특화 ---
대상 코인: DOGE, PEPE, NEAR, SUI, SEI, HBAR, STX

성향: 공격적 단타 트레이더
- 단기 변동성과 거래량 폭발 우선
- 애매하면 HOLD보다 5% 소액 진입 우선 검토
- 강한 추세에서는 일부 지표 불완전해도 진입 가능
- 기회를 놓치는 것도 손실로 간주

점수 → 판단:
85~100: BUY 20%
70~84: BUY 10%
55~69: BUY 5%
40~54: HOLD 또는 5% 소액 진입
0~39: HOLD

위험관리:
- 연속손실 3회: 강제 HOLD
- 일일손실 -5%: 강제 HOLD
- 시간당 최대 5회 거래
- 손실 후 30분 쿨다운"""

    return common + leo_specific

PRO_TRADER_DIRECTIVE = get_leo_system_prompt()

#!/usr/bin/env python3
# UTF-8 인코딩 강제
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
WORKSPACE_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

# 데이브의 upbit_analyzer 재사용
DAVE_TOOLS = os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools")
sys.path.insert(0, DAVE_TOOLS)

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock

load_env()

IMPORT_ERROR = None
try:
    import pyupbit
except ModuleNotFoundError as e:
    try:
        import upbit_public as pyupbit
    except ModuleNotFoundError:
        IMPORT_ERROR = e
        pyupbit = None

try:
    import upbit_analyzer
except ModuleNotFoundError as e:
    IMPORT_ERROR = e
    upbit_analyzer = None

# 기본 감시 대상 (고변동성 우선)
BASE_LEO_TICKERS = [
    "KRW-DOGE", "KRW-PEPE", "KRW-NEAR", "KRW-SUI",
    "KRW-SEI", "KRW-HBAR", "KRW-STX",
]

def _signal_intel_path():
    signal_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "market_signal.json")
    compat_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "market_pulse.json")
    return signal_path if os.path.exists(signal_path) else compat_path


def get_dynamic_leo_tickers():
    """시그널 인텔 기반 동적 종목 선정 (퀀트 점수 기준, 공격적)"""
    import json
    tickers = BASE_LEO_TICKERS.copy()

    try:
        intel_path = _signal_intel_path()
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                intel = json.load(f)

            # 시그널 코인 정보 확인
            crypto_data = intel.get("crypto", {})
            if "top_coins" in crypto_data:
                for coin in crypto_data["top_coins"][:10]:
                    ticker = coin.get("ticker")
                    score = coin.get("score", 0)
                    if ticker and score >= 40:
                        tickers.append(ticker)
                print(f"[Leo] 시그널 고득점 코인 반영 완료")

            tickers = list(dict.fromkeys(tickers))
            print(f"[Leo] 동적 종목: {len(tickers)}개")
    except Exception as e:
        print(f"[Leo] 동적 종목 실패, 기본 사용: {e}")

    return tickers

LEO_TICKERS = get_dynamic_leo_tickers()

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
PULSE_INTEL_MAX_AGE_SECONDS = int(os.getenv("SIGNAL_INTEL_MAX_AGE_SECONDS", os.getenv("PULSE_INTEL_MAX_AGE_SECONDS", "600")))


def safe_float(val, default=0.0):
    if val is None:
        return default
    try:
        return float(val)
    except Exception:
        return default


def _pulse_intel_path():
    return _signal_intel_path()


def _refresh_pulse_intel_if_stale(max_age_seconds=PULSE_INTEL_MAX_AGE_SECONDS):
    """거래 판단 직전 시그널 시장 조사가 낡았으면 단발 수집을 실행한다."""
    intel_path = _pulse_intel_path()
    now = time.time()
    age = None
    if os.path.exists(intel_path):
        age = now - os.path.getmtime(intel_path)
        if age <= max_age_seconds:
            return

    lock_path = os.path.join(os.path.dirname(intel_path), ".signal_refresh.lock")
    os.makedirs(os.path.dirname(intel_path), exist_ok=True)

    lock_fd = None
    try:
        try:
            lock_fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
            os.write(lock_fd, str(os.getpid()).encode("ascii", errors="ignore"))
        except FileExistsError:
            lock_age = now - os.path.getmtime(lock_path) if os.path.exists(lock_path) else 0
            if lock_age > 60:
                try:
                    os.remove(lock_path)
                except OSError:
                    pass
            for _ in range(10):
                time.sleep(1)
                if os.path.exists(intel_path) and time.time() - os.path.getmtime(intel_path) <= max_age_seconds:
                    return
            return

        age_text = "없음" if age is None else f"{age:.0f}s"
        print(f"[Leo] 시그널 시장 조사 최신화 실행 (기존 age={age_text})")
        script_path = os.path.join(AI_TEAM_ROOT, "skills", "시그널_분석가", "tools", "market_signal.py")
        result = subprocess.run(
            [sys.executable, script_path],
            cwd=AI_TEAM_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=45,
            env={**os.environ, "PYTHONUTF8": "1"},
        )
        if result.returncode != 0:
            print(f"[Leo] 시그널 조사 실행 실패: {result.stderr.strip()}")
        else:
            print("[Leo] 시그널 시장 조사 최신화 완료")
    except Exception as e:
        print(f"[Leo] 시그널 조사 최신화 실패: {e}")
    finally:
        if lock_fd is not None:
            try:
                os.close(lock_fd)
            except OSError:
                pass
            try:
                os.remove(lock_path)
            except OSError:
                pass


def load_pulse_intel(refresh=True):
    """시그널의 시장 정보 로드"""
    try:
        import json
        if refresh:
            _refresh_pulse_intel_if_stale()
        intel_path = _pulse_intel_path()
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                return json.load(f)
    except Exception as e:
        print(f"[Leo] 시그널 정보 로드 실패: {e}")
    return None


def calculate_leo_score(ticker: str) -> dict:
    """레오용 단순화된 스코어 계산 (펄스 정보 활용)"""
    try:
        df = pyupbit.get_ohlcv(ticker, interval="minute60", count=50)
        if df is None or df.empty:
            return {"ticker": ticker, "score": 0, "error": "데이터 없음"}

        current_price = df['close'].iloc[-1]
        score = 0
        reasons = []

        # 펄스 정보 로드
        pulse_intel = load_pulse_intel()

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

        # 4. 펄스 정보 활용 (공포탐욕, 김치프리미엄)
        if pulse_intel:
            crypto_data = pulse_intel.get("crypto", {})

            # 김치 프리미엄
            kp = crypto_data.get("kimchi_premium", {}).get("value", 0)
            if kp >= 3.0:
                score += 1
                reasons.append(f"김프 +{kp:.1f}% (국내 관심)")

            # 공포탐욕지수 (극단 구간 보너스)
            fg = crypto_data.get("fear_greed", {})
            if fg.get("signal") == "BUY":
                score += 2
                reasons.append(f"극공포 ({fg.get('value')})")
            elif fg.get("signal") == "SELL":
                score -= 2
                reasons.append(f"극탐욕 ({fg.get('value')})")

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


def run_leo_cycle():
    """레오 단타 사이클 실행"""
    global consecutive_losses, daily_loss_pct, trades_today, last_trade_time

    print(f"\n⚡ [{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 레오 단타 스캔")

    if IMPORT_ERROR:
        print(f"[Leo] 의존성 누락: {IMPORT_ERROR}")
        print("[Leo] pyupbit 또는 upbit_public fallback 확인이 필요합니다.")
        return

    # 위험 한도 체크
    can_trade, risk_msg = check_risk_limits()
    if not can_trade:
        print(f"[Leo] {risk_msg} - 거래 중단")
        return

    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        print("[Leo] API 키 미설정 - Upbit API 검증 실패. 거래 중지.")
        return

    # KRW 잔고 조회
    try:
        total_krw = safe_float(upbit_client.get_balance("KRW"))
    except Exception as e:
        print(f"[Leo] KRW 잔고 조회 실패: {e}")
        return

    leo_budget = total_krw

    if leo_budget < 5000:
        return

    # 보유 포지션 체크 및 익절/손절 감시
    leo_positions = []
    for ticker in LEO_TICKERS:
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

                coin = ticker.split('-')[1]

                # SKILL 기반 익절/손절 관리
                # 익절 체크
                if profit_pct >= 5.0:
                    # 2차 익절: 전량 청산
                    print(f"💰 [Leo] {coin} 2차 익절 +{profit_pct:.2f}% - 전량 매도")
                    print(f"  평단: {avg_buy_price:,.0f}원 → 현재: {current_price:,.0f}원")
                    send(f"💰 [레오] {coin} +{profit_pct:.1f}% 익절")
                    upbit_analyzer.execute_sell(ticker, balance)
                    consecutive_losses = 0  # 익절 시 연속 손절 리셋

                elif profit_pct >= 3.0:
                    # 1차 익절: 50% 청산 (위험 헷지)
                    sell_amount = balance * 0.5
                    remaining = balance * 0.5
                    print(f"💰 [Leo] {coin} 1차 익절 +{profit_pct:.2f}% - 50% 매도")
                    print(f"  청산: {sell_amount:.6f}개 | 유지: {remaining:.6f}개")
                    send(f"💰 [레오] {coin} +{profit_pct:.1f}% (50%)")
                    upbit_analyzer.execute_sell(ticker, sell_amount)

                # 손절 체크 (SKILL: -2% 기계적 손절)
                elif profit_pct <= -2.0:
                    print(f"🛑 [Leo] {coin} 손절 {profit_pct:.2f}% - 전량 매도")
                    print(f"  평단: {avg_buy_price:,.0f}원 → 현재: {current_price:,.0f}원")
                    send(f"🛑 [레오] {coin} {profit_pct:.1f}% 손절")
                    upbit_analyzer.execute_sell(ticker, balance)
                    consecutive_losses += 1
                    daily_loss_pct += profit_pct

                # 홀딩 중 (익절/손절 범위 밖)
                else:
                    # 1시간 이상 보유 중이고 수익 < +1%인 경우 펄스 정보 재확인
                    if ticker in last_trade_time:
                        holding_hours = (time.time() - last_trade_time.get(ticker, 0)) / 3600
                        if holding_hours >= 1.0 and -1.0 < profit_pct < 1.0:
                            # 펄스 정보로 시장 상황 재확인
                            pulse_intel = load_pulse_intel()
                            if pulse_intel:
                                crypto_data = pulse_intel.get("crypto", {})
                                fg = crypto_data.get("fear_greed", {}).get("value", 50)

                                # 극단 탐욕 변화 시 손절 기준 완화 (-1% 손절)
                                # 김치프리미엄은 조기 청산 필터로 사용하지 않는다.
                                if fg >= 85:
                                    print(f"⚠️ [Leo] {coin} 시장 과열 감지 (탐욕 {fg}) - 조기 청산 고려")
                                    if profit_pct >= 0:
                                        # 소폭 이익이라도 청산
                                        print(f"  소폭 이익 {profit_pct:+.2f}% 조기 청산")
                                        send(f"⚠️ [레오] {coin} 과열 조기청산 {profit_pct:+.1f}%")
                                        upbit_analyzer.execute_sell(ticker, balance)
                                    elif profit_pct <= -1.0:
                                        # -1% 이하 손실 시 조기 손절
                                        print(f"  과열 구간 조기 손절 {profit_pct:.2f}%")
                                        send(f"🛑 [레오] {coin} 과열 조기손절 {profit_pct:.1f}%")
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

    # SKILL 기준 강화: 최소 진입 점수 2점 + 펄스 정보 종합 판단
    min_score = 2  # 1점 → 2점으로 상향

    # 펄스 정보 확인
    pulse_intel = load_pulse_intel()

    # 시그널 위험 신호 체크. 김치프리미엄은 참고값으로만 쓰고
    # 신규 진입 차단 필터로 사용하지 않는다.
    if pulse_intel:
        crypto_data = pulse_intel.get("crypto", {})

        # 공포탐욕 극단 탐욕(80 이상) → 신규 진입 금지
        fg = crypto_data.get("fear_greed", {})
        if fg.get("value", 50) >= 80:
            print(f"[Leo] 공포탐욕지수 {fg['value']} - 극탐욕, 신규 진입 금지")
            return

    # 진입 조건 체크
    if best["score"] >= min_score:
        ticker = best["ticker"]

        # SKILL 필수 조건 재검증
        # 1. 거래량 급증 필수 (최소 1.5배)
        if best.get("volume_ratio", 0) < 1.5:
            print(f"[Leo] {ticker} - 거래량 부족 ({best.get('volume_ratio', 0):.1f}배), 진입 금지")
            return

        # 2. 모멘텀 확인 (최소 +0.5% 이상)
        if best.get("momentum_1h", 0) < 0.5:
            print(f"[Leo] {ticker} - 모멘텀 부족 ({best.get('momentum_1h', 0):.1f}%), 진입 금지")
            return

        reasons = best.get("reasons", [])

        # 투입 금액 계산 (스코어에 따라 차등)
        if best["score"] >= 4:
            invest_pct = 0.5  # 고득점: 50%
        elif best["score"] >= 3:
            invest_pct = 0.4  # 중득점: 40%
        else:
            invest_pct = 0.3  # 저득점: 30%

        buy_amount = leo_budget * invest_pct * 0.995  # 수수료 여유

        if buy_amount < 5000:
            print(f"[Leo] 매수 금액 부족 ({buy_amount:,.0f}원)")
            return

        coin = ticker.split('-')[1]

        # 진입 근거 로깅
        print(f"\n[Leo] ✅ 진입 조건 만족!")
        print(f"  코인: {ticker}")
        print(f"  스코어: {best['score']}점")
        print(f"  근거: {', '.join(reasons)}")
        print(f"  거래량: {best.get('volume_ratio', 0):.1f}배")
        print(f"  모멘텀: {best.get('momentum_1h', 0):+.1f}%")
        print(f"  투입: {buy_amount:,.0f}원 ({invest_pct*100:.0f}%)\n")

        # 금액 포맷 (1만원 이상/미만 구분)
        if buy_amount >= 10000:
            amount_str = f"{buy_amount/10000:.1f}만원"
        else:
            amount_str = f"{buy_amount:,.0f}원"

        msg = f"⚡ [레오] {coin} 진입 {amount_str} ({best['score']}점)"
        send(msg)

        res = upbit_analyzer.execute_buy(ticker, buy_amount)
        print(res)

        last_trade_time[ticker] = time.time()
        trades_today.append(time.time())

    else:
        coin = best['ticker'].split('-')[1]
        print(f"[Leo] ❌ {coin} 진입 점수 부족 ({best['score']}점 < {min_score}점)")
        print(f"  근거: {', '.join(best.get('reasons', []))}")


def send_status_report():
    """2시간마다 현황 보고 (간결)"""
    if IMPORT_ERROR:
        return

    now = time.time()
    if not hasattr(send_status_report, 'last_report'):
        send_status_report.last_report = 0

    if now - send_status_report.last_report < 7200:  # 2시간
        return

    send_status_report.last_report = now

    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        return

    try:
        leo_positions = []
        for ticker in LEO_TICKERS:
            balance = safe_float(upbit_client.get_balance(ticker))
            current_price = float(pyupbit.get_current_price(ticker))

            if balance * current_price >= 5000 and not check_dave_holdings(ticker):
                avg = safe_float(upbit_client.get_avg_buy_price(ticker))
                profit = (current_price - avg) / avg * 100
                leo_positions.append(f"{ticker.split('-')[1]} {profit:+.1f}%")

        if leo_positions or consecutive_losses > 0:
            pos = ' | '.join(leo_positions) if leo_positions else '없음'
            msg = f"⚡ [레오] {pos} | 일손익 {daily_loss_pct:+.1f}%"
            if consecutive_losses > 0:
                msg += f" | 연손 {consecutive_losses}회"
            send(msg)

        print("[Leo] 2시간 현황 보고 전송 완료")
    except Exception as e:
        print(f"[Leo] 현황 보고 오류: {e}")


def print_status():
    """외부 API 주문 없이 레오 설정과 실행 방법만 출력."""
    global LEO_TICKERS
    LEO_TICKERS = get_dynamic_leo_tickers()
    print("⚡ 레오 트레이더 상태")
    print(f"- 감시 코인: {len(LEO_TICKERS)}개")
    print(f"  기본: {', '.join(BASE_LEO_TICKERS)}")
    print(f"  시그널: {', '.join([t for t in LEO_TICKERS if t not in BASE_LEO_TICKERS][:5])}...")
    print(f"- 최소 진입 점수: 2점")
    print(f"- 연속 손절 제한: {MAX_CONSECUTIVE_LOSSES}회")
    print(f"- 일일 손실 한도: {MAX_DAILY_LOSS_PCT:.1f}%")
    print(f"- 시간당 거래 제한: {MAX_TRADES_PER_HOUR}회")
    print("- 1회 스캔: leo_aggressive_trader.py --once")
    print("- 데몬: leo_aggressive_trader.py --daemon")
    if IMPORT_ERROR:
        print(f"- 현재 의존성 상태: 누락 ({IMPORT_ERROR})")
    elif getattr(pyupbit, "__name__", "") == "upbit_public":
        print("- 현재 의존성 상태: 공개 시세 fallback 사용 중 (실거래 불가)")


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--status" in args or "--help" in args or "-h" in args:
        print_status()
        sys.exit(0)

    if "--once" in args:
        run_leo_cycle()
    elif "--daemon" in args:
        LEO_TICKERS = get_dynamic_leo_tickers()
        print(f"⚡ 레오 공격적 단타 트레이더 시작: {len(LEO_TICKERS)}개 종목")
        print(f"   기본: {', '.join(BASE_LEO_TICKERS)}")
        print(f"   시그널: {', '.join([t for t in LEO_TICKERS if t not in BASE_LEO_TICKERS][:5])}...")
        iteration = 0

        with ProcessLock("leo"):
            try:
                while True:
                    try:
                        # 30분마다 종목 재로드
                        if iteration % 180 == 0:
                            LEO_TICKERS = get_dynamic_leo_tickers()

                        run_leo_cycle()
                        send_status_report()
                        iteration += 1
                    except Exception as e:
                        print(f"[Leo Daemon Error] {e}")

                    time.sleep(10)
            except KeyboardInterrupt:
                print("[Leo] stopped")
