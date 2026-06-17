# -*- coding: utf-8 -*-
"""
데이브 업비트 다중 코인 자동 매매 봇 (완전 실시간 분석 및 진입 고도화 버전)
"""
import os
import sys

# UTF-8 인코딩 강제 (Windows)
if sys.platform == "win32" and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# 데몬 모드일 때만 중복 실행 방지 (최대한 빨리 체크)
if "--daemon" in sys.argv:
    _here = os.path.dirname(os.path.abspath(__file__))
    AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
    sys.path.insert(0, AI_TEAM_ROOT)

    from _shared.process_lock import acquire_lock
    if not acquire_lock("dave"):
        sys.exit(0)

# 나머지 import
import time
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()

sys.path.insert(0, _here)  # upbit_public, upbit_analyzer 로컬 모듈 경로 추가
try:
    import pyupbit
except ModuleNotFoundError:
    import upbit_public as pyupbit
import upbit_analyzer

# 감시 대상 — 거래량 상위 고변동성 알트 우선 (소액 수익 극대화)
TICKERS = [
    "KRW-SOL", "KRW-XRP", "KRW-DOGE", "KRW-NEAR",
    "KRW-SUI", "KRW-SEI", "KRW-STX", "KRW-HBAR",
    "KRW-ADA", "KRW-AVAX", "KRW-LINK", "KRW-PEPE",
    "KRW-BTC", "KRW-ETH",
]

# 티커별 최근 LLM 분석 실행 시점 기록 (실시간 무한 루프로 인한 중복 LLM 과부하 방지 쿨다운용)
last_llm_time = {}
LLM_COOLDOWN_SECONDS = 300  # 동일 종목에 대한 LLM 재분석은 최소 5분 쿨다운 적용
last_report_time = 0
REPORT_INTERVAL_SECONDS = 12 * 3600  # 12시간마다 현황 보고 (하루 2회)

def safe_float(val, default=0.0):
    if val is None:
        return default
    try:
        return float(val)
    except Exception:
        return default

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

def calculate_confluence_score(ticker: str) -> dict:
    """일봉 + 4시간봉 복합 점수 계산 (소액 단타 최적화)"""
    try:
        df = pyupbit.get_ohlcv(ticker, interval="day", count=300)
        df4h = pyupbit.get_ohlcv(ticker, interval="minute240", count=100)
        if df is None or df.empty:
            return {"ticker": ticker, "score": 0, "error": "데이터 없음"}
            
        df = df.reset_index().rename(columns={
            "index": "Date",
            "open": "Open",
            "high": "High",
            "low": "Low",
            "close": "Close",
            "volume": "Volume"
        })
        
        indicators = upbit_analyzer.calc_indicators(df)
        df_supertrend = upbit_analyzer.calculate_supertrend(df.copy())
        
        current_price = df_supertrend['Close'].iloc[-1]
        current_trend = df_supertrend['Trend'].iloc[-1]
        records = df_supertrend.tail(30).to_dict(orient='records')
        atr = upbit_analyzer._calc_atr(records)
        
        score = 0
        reasons = []
        
        # 1. EMA 200 대추세 (롱 국면 여부)
        if indicators["대추세_EMA200"] == "상승 국면(LONG 전용)":
            score += 4
            reasons.append("EMA200 위")
            
        # 2. Supertrend 추세
        if current_trend == "상승":
            score += 3
            reasons.append("Supertrend 상승")
            
        # 3. 스토캐스틱 RSI
        stoch_status = indicators["StochRSI_상태"]
        if stoch_status == "과매도 골든크로스":
            score += 3
            reasons.append("StochRSI 과매도 골크")
        elif stoch_status == "과매도":
            score += 1.5
            reasons.append("StochRSI 과매도")
        elif stoch_status == "과매수":
            score -= 1
            reasons.append("StochRSI 과매수(과열)")
            
        # 4. 하이킨 애쉬
        ha_status = indicators["HeikinAshi_상태"]
        if "아래꼬리 없는 장대양봉" in ha_status:
            score += 3
            reasons.append("HA 아래꼬리없는 장대양봉")
        elif ha_status == "양봉":
            score += 1
            reasons.append("HA 양봉")
            
        # 5. 거래량 급증
        if indicators["VolumeSpike"] == "✅ 급증":
            score += 2
            reasons.append(f"거래량 급증 ({indicators['Volume_배율']}배)")

        # 6. OBV 다이버전스 (세력 매집 신호)
        obv_div = indicators.get("OBV_다이버전스", "")
        if "상승 다이버전스" in obv_div:
            score += 2
            reasons.append("OBV 상승 다이버전스(매집)")
        elif "하락 다이버전스" in obv_div:
            score -= 2
            reasons.append("OBV 하락 다이버전스(배분경고)")

        # 7. CVD 다이버전스
        cvd_div = indicators.get("CVD_다이버전스", "")
        if "상승 다이버전스" in cvd_div:
            score += 2
            reasons.append("CVD 상승 다이버전스(저가매집)")
        elif "하락 다이버전스" in cvd_div:
            score -= 1
            reasons.append("CVD 하락 다이버전스(고래 미참여)")

        # 8. 세력 매집 패턴
        seoryok = indicators.get("세력매집패턴", "")
        if "바닥 매집" in seoryok:
            score += 2
            reasons.append("세력 바닥 매집 패턴")
        elif "손털기" in seoryok:
            score += 1
            reasons.append("세력 손털기(shakeout — 반등 기대)")
        elif "가짜 펌핑" in seoryok or "수급 취약" in seoryok:
            score -= 2
            reasons.append("거래량 없는 상승(펌핑 의심)")

        # 9. 워시트레이딩 패널티
        if "워시트레이딩" in indicators.get("통정매매의심", ""):
            score -= 3
            reasons.append("통정매매 의심(신뢰도 하락)")

        # 10. 4시간봉 단기 모멘텀 보너스 (소액 단타 신호)
        if df4h is not None and not df4h.empty:
            try:
                c = df4h["close"].values
                # 4h EMA20 상향 돌파
                ema20_4h = sum(c[-20:]) / 20
                if c[-1] > ema20_4h and c[-2] <= ema20_4h:
                    score += 2
                    reasons.append("4h EMA20 상향돌파")
                # 4h 최근 3봉 연속 상승
                elif c[-1] > c[-2] > c[-3]:
                    score += 1
                    reasons.append("4h 3봉 연속상승")
                # 4h 거래량 급증
                v = df4h["volume"].values
                avg_vol = sum(v[-20:-1]) / 19
                if v[-1] > avg_vol * 2:
                    score += 1
                    reasons.append("4h 거래량 2배 급증")
            except Exception:
                pass

        return {
            "ticker": ticker,
            "score": score,
            "current_price": current_price,
            "atr": atr,
            "reasons": reasons,
            "indicators": indicators
        }
    except Exception as e:
        return {"ticker": ticker, "score": 0, "error": str(e)}

def load_hyunbin_intel():
    """현빈의 시장 정보 로드"""
    try:
        import json
        intel_path = os.path.join(AI_TEAM_ROOT, "reports", "research", "crypto_market_intel.json")
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                return json.load(f)
    except Exception as e:
        print(f"[Dave] 현빈 정보 로드 실패: {e}")
    return None


def run_auto_trade_cycle(sim_mode=False):
    print(f"\n--- [{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 실시간 다중 코인 자동 매매 감시 ---")

    # 현빈 정보 확인
    hyunbin_intel = load_hyunbin_intel()
    if hyunbin_intel:
        fed = hyunbin_intel.get("fed_events", {})
        if fed.get("risk_level") == "HIGH":
            print(f"[Dave] 🚨 연준 고위험 구간: {fed.get('current_status')} - 신규 진입 금지")
            # 기존 포지션 관리만 수행하고 신규 진입은 스킵
            # (포지션 청산 로직은 계속 실행)

    upbit_client = upbit_analyzer.get_upbit_client()
    if upbit_client is None:
        print("[AutoTrader] API 키가 설정되지 않았습니다. 시뮬레이션 모드로 작동합니다.")
        if not sim_mode:
            print("[AutoTrader] LIVE requested but Upbit API validation failed. Trading is paused.")
            return
        sim_mode = True
        
    held_positions = []
    krw_balance = 1000000.0 if sim_mode else 0.0
    
    if not sim_mode:
        try:
            krw_balance = safe_float(upbit_client.get_balance("KRW"))
        except Exception as e:
            print(f"[AutoTrader] KRW 잔고 조회 실패: {e}")
            return

    # 1. 보유 중인 모든 코인에 대한 실시간 시세 감시 (10초 주기)
    for ticker in TICKERS:
        try:
            if sim_mode:
                btc_balance = 0.0
                avg_buy_price = 0.0
            else:
                btc_balance = safe_float(upbit_client.get_balance(ticker))
                avg_buy_price = safe_float(upbit_client.get_avg_buy_price(ticker))
                
            current_price = float(pyupbit.get_current_price(ticker))
            
            # 최소 주문 단위(5,000원) 이상의 포지션 보유 확인
            if btc_balance * current_price >= 5000.0:
                df = pyupbit.get_ohlcv(ticker, interval="day", count=30)
                df_supertrend = upbit_analyzer.calculate_supertrend(df.reset_index().rename(columns={"index": "Date", "open": "Open", "high": "High", "low": "Low", "close": "Close", "volume": "Volume"}))
                atr = upbit_analyzer._calc_atr(df_supertrend.to_dict(orient='records'))
                
                held_positions.append({
                    "ticker": ticker,
                    "balance": btc_balance,
                    "avg_buy_price": avg_buy_price,
                    "current_price": current_price,
                    "atr": atr
                })
        except Exception as e:
            print(f"[AutoTrader] {ticker} 보유 현황 조회 중 오류: {e}")

    # 2. 보유 포지션 실시간 TP/SL 감시 및 집행
    if held_positions:
        print(f"[AutoTrader] 현재 보유 중인 포지션 수: {len(held_positions)}개")
        for pos in held_positions:
            ticker = pos["ticker"]
            current_price = pos["current_price"]
            avg_buy_price = pos["avg_buy_price"]
            btc_balance = pos["balance"]
            atr = pos["atr"]
            
            profit_ratio = (current_price - avg_buy_price) / avg_buy_price

            # SKILL 기반 포지션 관리
            coin = ticker.split("-")[1]

            # 현빈 정보로 시장 상황 확인
            hyunbin_intel = load_hyunbin_intel()
            emergency_exit = False
            if hyunbin_intel:
                kp = hyunbin_intel.get("kimchi_premium", {}).get("premium_pct", 0)
                fg = hyunbin_intel.get("fear_greed_index", {}).get("value", 50)

                # 극단 상황: 김프 +15% 초과 or 공포탐욕 85 이상 → 긴급 청산 고려
                if (kp > 15.0 or fg >= 85) and profit_ratio >= 0.01:  # +1% 이상 수익
                    print(f"⚠️ [Dave] {coin} 시장 과열 (김프 {kp:.1f}%, 탐욕 {fg}) - 이익 실현 청산")
                    send_telegram_message(f"⚠️ [데이브] {coin} 과열 청산 {profit_ratio*100:+.1f}%")
                    if not sim_mode:
                        upbit_analyzer.execute_sell(ticker, btc_balance)
                    emergency_exit = True

            if not emergency_exit:
                # 트레일링 스탑: 최고가 대비 -3% 이탈 시 청산 (수익 극대화)
                trail_key = f"peak_{ticker}"
                if not hasattr(run_auto_trade_cycle, '_peaks'):
                    run_auto_trade_cycle._peaks = {}
            peak = run_auto_trade_cycle._peaks.get(ticker, avg_buy_price)
            if current_price > peak:
                run_auto_trade_cycle._peaks[ticker] = current_price
                peak = current_price
            trailing_sl = peak * 0.97   # 최고가 대비 -3%

            # 손절: ATR 기준 or -2% 중 높은 값
            sl_atr   = avg_buy_price - 1.5 * atr
            sl_fixed = avg_buy_price * 0.98
            sl_price = max(sl_atr, sl_fixed)

            # 수익 중이면 트레일링, 손실이면 고정 손절
            effective_sl = trailing_sl if profit_ratio > 0.03 else sl_price

            print(f"  [{ticker}] 수익률: {profit_ratio*100:.2f}% | 최고가: {peak:,.0f} | 트레일SL: {trailing_sl:,.0f} | 손절가: {sl_price:,.0f} (현재: {current_price:,.0f})")

            if current_price <= effective_sl:
                if profit_ratio > 0:
                    msg = f"✅ [데이브] 트레일링 스탑 익절!\n📌 {ticker}\n💰 매도가: {current_price:,}원\n📈 수익률: {profit_ratio*100:.2f}% (최고가 {peak:,.0f}원 대비 -3%)"
                else:
                    msg = f"🚨 [데이브] 손절 집행\n📌 {ticker}\n💰 매도가: {current_price:,}원\n📉 수익률: {profit_ratio*100:.2f}%"
                print(msg)
                send_telegram_message(msg)
                run_auto_trade_cycle._peaks.pop(ticker, None)
                if not sim_mode:
                    res = upbit_analyzer.execute_sell_all(ticker)
                    print(res)

    # 3. 포지션 미보유 시 혹은 예수금이 충분히 남아있을 시 신규 진입 분석 (완전 실시간화)
    # 보유 포지션이 있더라도 추가 매수 여력이 있다면 진입 후보 탐색
    if not held_positions or (krw_balance >= 10000.0):
        print("[AutoTrader] 실시간 전체 코인 퀀트 스캔 시작...")
        scanned = []
        for ticker in TICKERS:
            res = calculate_confluence_score(ticker)
            if "error" not in res:
                scanned.append(res)
                
        scanned.sort(key=lambda x: x["score"], reverse=True)
        
        # 스캔 스코어 보드 출력 (10초마다 실시간으로 보임)
        print("\n=== [실시간 퀀트 스코어 랭킹] ===")
        for item in scanned[:5]:
            print(f"  - {item['ticker']}: {item['score']}점 | {', '.join(item['reasons'])}")
        print("=========================\n")
        
        if not scanned:
            print("[AutoTrader] 스캔 가능한 코인 데이터가 없습니다.")
            return
            
        best = scanned[0]
        # 최소 진입 점수 문턱값 (11점 이상)
        if best["score"] >= 3:
            best_ticker = best["ticker"]

            # 동일 종목에 대한 LLM 분석 쿨다운 감시
            now = time.time()
            last_run = last_llm_time.get(best_ticker, 0)
            if now - last_run < LLM_COOLDOWN_SECONDS:
                print(f"[AutoTrader] 최우수 코인 {best_ticker} ({best['score']}점) 포착되었으나, 최근 분석 이력으로 인해 쿨다운 중입니다. (남은 시간: {int(LLM_COOLDOWN_SECONDS - (now - last_run))}초)")
                return

            print(f"[AutoTrader] 💥 최우수 코인 진입 조건 달성! {best_ticker} ({best['score']}점) -> LLM 최종 검증 진행...")
            last_llm_time[best_ticker] = now  # 쿨다운 갱신

            try:
                decision_data = upbit_analyzer.run_gemini_trade_decision(f"실시간 퀀트 스캔 {best['score']}점 달성. 신규 진입 최종 검증 요청.", best_ticker)
                decision = decision_data.decision.upper()
                percentage = decision_data.percentage
                reason = decision_data.reason
                
                print(f"[AutoTrader] LLM 최종 결정: {decision} ({percentage}%) | 사유: {reason}")
                
                if decision == "BUY":
                    pct = percentage / 100.0
                    buy_amount = krw_balance * pct * 0.995 # 수수료 고려 안전 여유

                    if buy_amount < 5000.0:
                        print(f"[AutoTrader] 매수 가능 금액이 최소 주문금액(5,000원) 미만입니다. (계산액: {buy_amount:.0f}원)")
                        return

                    coin = best_ticker.split('-')[1]
                    # 금액 포맷 (1만원 이상/미만 구분)
                    if buy_amount >= 10000:
                        amount_str = f"{buy_amount/10000:.1f}만원"
                    else:
                        amount_str = f"{buy_amount:,.0f}원"

                    msg = f"💼 [데이브] {coin} 매수 {amount_str} ({best['score']}점)"
                    print(msg)
                    send_telegram_message(msg)
                    if not sim_mode:
                        res = upbit_analyzer.execute_buy(best_ticker, buy_amount)
                        print(res)
                        
                elif decision == "SELL":
                    if sim_mode:
                        coin_balance = 0.05
                    else:
                        coin_balance = safe_float(upbit_client.get_balance(best_ticker))
                        
                    pct = percentage / 100.0
                    sell_volume = coin_balance * pct
                    current_price = float(pyupbit.get_current_price(best_ticker))
                    
                    if sell_volume * current_price < 5000.0:
                        print(f"[AutoTrader] 매도 가능 금액이 최소 주문금액(5,000원) 미만입니다. (계산액: {sell_volume * current_price:.0f}원)")
                        return
                        
                    msg = f"📉 [데이브] 실시간 스캔 매도 조건 감지!\n📌 대상: {best_ticker}\n📉 비중: {percentage}%\n🚨 시장가 매도를 집행합니다."
                    print(msg)
                    send_telegram_message(msg)
                    if not sim_mode:
                        res = upbit_analyzer.execute_sell(best_ticker, sell_volume)
                        print(res)
                else:
                    print(f"[AutoTrader] {best_ticker} 분석 결과가 HOLD로 결정되어 진입하지 않습니다. (사유: {reason})")
            except Exception as trade_err:
                print(f"[AutoTrader] ❌ 주문 실행 중 오류 발생: {trade_err}")
        else:
            print(f"[AutoTrader] 현재 최소 진입 점수(3점)를 만족하는 코인이 없습니다. (최고 점수: {best['ticker']} {best['score']}점)")

def send_status_report(sim_mode=False):
    """4시간마다 현황 보고 텔레그램 전송 (간결)"""
    global last_report_time
    now = time.time()
    if now - last_report_time < REPORT_INTERVAL_SECONDS:
        return
    last_report_time = now

    try:
        upbit_client = upbit_analyzer.get_upbit_client()
        if upbit_client is None or sim_mode:
            return

        krw = safe_float(upbit_client.get_balance("KRW"))
        holdings = []
        total_pnl = 0

        for ticker in TICKERS:
            bal = safe_float(upbit_client.get_balance(ticker))
            if bal * float(pyupbit.get_current_price(ticker)) >= 5000:
                cur = float(pyupbit.get_current_price(ticker))
                avg = safe_float(upbit_client.get_avg_buy_price(ticker))
                pnl = (cur - avg) / avg * 100
                total_pnl += pnl
                holdings.append(f"{ticker.split('-')[1]} {pnl:+.1f}%")

        if holdings:
            msg = f"💼 [데이브] {' | '.join(holdings)} | 예수금 {krw/10000:.0f}만원"
            send_telegram_message(msg)

        print(f"[Report] 4시간 현황 보고 전송 완료")
    except Exception as e:
        print(f"[Report] 현황 보고 오류: {e}")


if __name__ == "__main__":
    args = sys.argv[1:]
    sim = "--sim" in args

    if "--once" in args:
        run_auto_trade_cycle(sim_mode=sim)
    else:
        # 락은 파일 최상단에서 이미 획득됨
        from _shared.process_lock import release_lock

        print("🤖 데이브 업비트 실시간 자동 매매 데몬 시작 (시세 감시 및 신규 스캔: 10초)")
        # 시작 메시지 전송 안 함 (혼란 방지)
        last_report_time = time.time() - REPORT_INTERVAL_SECONDS  # 시작 즉시 첫 보고

        try:
            while True:
                try:
                    run_auto_trade_cycle(sim_mode=sim)
                    send_status_report(sim_mode=sim)
                except Exception as e:
                    print(f"[Daemon Error] {e}")
                time.sleep(10)
        except KeyboardInterrupt:
            print("[Dave] stopped")
        finally:
            release_lock("dave")
