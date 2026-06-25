#!/usr/bin/env python3
"""데이브 한국 주식 자동매매 봇 (보수적 가치투자 전략)"""
import os, sys, time, json
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "..", "..", ".."))

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock
from _shared.llm import text as llm_text
from _shared.trading_entry_evaluator import (
    backfill_pending_outcomes,
    evaluate_entry,
    format_evaluation,
    record_decision,
)

load_env()

# 로컬 모듈
sys.path.insert(0, _here)
from kis_client import KISClient

# 슈퍼트렌드 모니터링 종목 (동적 로드)
def load_supertrend_watch():
    """JSON 파일에서 슈퍼트렌드 감시 목록 로드"""
    watch_file = os.path.join(os.path.dirname(__file__), ".supertrend_watch.json")
    try:
        if os.path.exists(watch_file):
            with open(watch_file, "r", encoding="utf-8") as f:
                data = json.load(f)
                return [(s["code"], s["name"]) for s in data.get("stocks", []) if s.get("enabled")]
    except Exception as e:
        print(f"[Dave 주식] 감시 목록 로드 실패: {e}")
    return [("240810", "원익IPS")]  # 기본값

SUPERTREND_WATCH = load_supertrend_watch()

# 기본 감시 대상 비활성화 (슈퍼트렌드만 사용)
BASE_STOCKS = []

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))

def get_dynamic_stocks():
    """펄스 인텔 기반 동적 종목 선정 (퀀트 점수 기준)"""
    stocks = BASE_STOCKS.copy()

    try:
        intel_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "market_pulse.json")
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                intel = json.load(f)

            # 펄스 주식 정보 확인
            stock_data = intel.get("stock", {})
            if "top_stocks" in stock_data:
                for item in stock_data["top_stocks"][:5]:
                    code = item.get("code")
                    score = item.get("score", 0)
                    if code and score >= 50:
                        stocks.append(code)
                print(f"[Dave 주식] 펄스 고득점 종목 반영 완료")

            stocks = list(dict.fromkeys(stocks))
            print(f"[Dave 주식] 동적 종목: {len(stocks)}개")
    except Exception as e:
        print(f"[Dave 주식] 동적 종목 로드 실패, 기본 종목 사용: {e}")

    return stocks

DAVE_STOCKS = get_dynamic_stocks()

class StockAutoTrader:
    """한국 주식 자동매매 (데이브 보수적 전략)"""

    def __init__(self):
        self.client = KISClient()
        self.cooldown = {}  # LLM 분석 쿨다운
        self.llm_cooldown_seconds = 600  # 10분
        self.supertrend_state = {}  # 슈퍼트렌드 추세 상태 저장

    def get_stock_info(self, stock_code: str) -> dict:
        """종목 정보 수집 (RSI, MACD, 외국인 매수세 포함)"""
        try:
            # 현재가
            price_data = self.client.get_current_price(stock_code)
            if "output" not in price_data:
                return None

            output = price_data["output"]

            info = {
                "code": stock_code,
                "name": output.get("prdt_name", "N/A"),
                "current_price": int(output.get("stck_prpr", 0)),
                "change_rate": float(output.get("prdy_ctrt", 0)),
                "volume": int(output.get("acml_vol", 0)),
                "high": int(output.get("stck_hgpr", 0)),
                "low": int(output.get("stck_lwpr", 0)),
                "open": int(output.get("stck_oprc", 0)),
            }

            # RSI, MACD 계산 (일봉 기준)
            daily_data = self.client.get_daily_price(stock_code, days=30)
            if daily_data and "output2" in daily_data:
                indicators = self.calculate_indicators(daily_data["output2"])
                info.update(indicators)

            # 외국인 3일 연속 매수 체크
            foreign_data = self.check_foreign_buying(stock_code)
            info.update(foreign_data)

            return info
        except Exception as e:
            print(f"❌ {stock_code} 정보 수집 실패: {e}")
            return None

    def calculate_indicators(self, daily_candles: list) -> dict:
        """RSI, MACD 계산"""
        try:
            if len(daily_candles) < 26:
                return {"rsi": 0, "macd": 0, "macd_signal": 0, "macd_golden": False}

            # 최신순 → 과거순으로 정렬
            candles = list(reversed(daily_candles[:30]))
            closes = [int(c["stck_clpr"]) for c in candles]

            # RSI 계산 (14일)
            rsi = self._calculate_rsi(closes, period=14)

            # MACD 계산 (12, 26, 9)
            macd, signal = self._calculate_macd(closes)
            macd_golden = macd > signal

            return {
                "rsi": round(rsi, 2),
                "macd": round(macd, 2),
                "macd_signal": round(signal, 2),
                "macd_golden": macd_golden
            }
        except Exception as e:
            print(f"  보조지표 계산 실패: {e}")
            return {"rsi": 0, "macd": 0, "macd_signal": 0, "macd_golden": False}

    def _calculate_rsi(self, closes: list, period: int = 14) -> float:
        """RSI 계산"""
        if len(closes) < period + 1:
            return 50.0

        gains = []
        losses = []
        for i in range(1, len(closes)):
            change = closes[i] - closes[i - 1]
            gains.append(max(change, 0))
            losses.append(abs(min(change, 0)))

        avg_gain = sum(gains[-period:]) / period
        avg_loss = sum(losses[-period:]) / period

        if avg_loss == 0:
            return 100.0

        rs = avg_gain / avg_loss
        rsi = 100 - (100 / (1 + rs))
        return rsi

    def _calculate_macd(self, closes: list, fast=12, slow=26, signal=9):
        """MACD 계산"""
        if len(closes) < slow:
            return 0.0, 0.0

        # EMA 계산
        def ema(data, period):
            multiplier = 2 / (period + 1)
            ema_val = sum(data[:period]) / period
            for price in data[period:]:
                ema_val = (price - ema_val) * multiplier + ema_val
            return ema_val

        fast_ema = ema(closes, fast)
        slow_ema = ema(closes, slow)
        macd_line = fast_ema - slow_ema

        # 시그널선 (MACD의 9일 EMA)
        macd_history = [macd_line]  # 간단히 현재값만 사용
        signal_line = macd_line * 0.9  # 근사값

        return macd_line, signal_line

    def check_foreign_buying(self, stock_code: str) -> dict:
        """외국인 3일 연속 매수 확인"""
        try:
            # 일봉 데이터에서 외국인 순매수 추출
            daily_data = self.client.get_daily_price(stock_code, days=5)
            if not daily_data or "output2" not in daily_data:
                return {"foreign_3day_buy": False, "foreign_buy_days": 0}

            candles = daily_data["output2"][:3]  # 최근 3일
            consecutive_days = 0

            for candle in candles:
                # 외국인 순매수량 (frgn_ntby_qty)
                foreign_net = int(candle.get("frgn_ntby_qty", 0) or 0)
                if foreign_net > 0:
                    consecutive_days += 1
                else:
                    break

            is_3day = consecutive_days >= 3

            return {
                "foreign_3day_buy": is_3day,
                "foreign_buy_days": consecutive_days
            }
        except Exception as e:
            print(f"  외국인 매수세 확인 실패: {e}")
            return {"foreign_3day_buy": False, "foreign_buy_days": 0}

    def calculate_supertrend(self, stock_code: str, period: int = 10, multiplier: float = 3.0) -> dict:
        """슈퍼트렌드 지표 계산 (1분봉)"""
        try:
            # 1분봉 데이터 조회
            minute_data = self.client.get_minute_price(stock_code)
            if not minute_data or "output2" not in minute_data:
                return None

            candles = minute_data["output2"]
            if len(candles) < period:
                return None

            # 최근 데이터만 사용 (역순이므로 reverse)
            candles = list(reversed(candles[:period + 1]))

            # ATR 계산 (분봉 데이터 키 이름 다름)
            atr_values = []
            for i in range(len(candles)):
                high = int(candles[i]["stck_hgpr"])
                low = int(candles[i]["stck_lwpr"])
                close = int(candles[i]["stck_prpr"])  # 분봉은 stck_prpr

                if i == 0:
                    tr = high - low
                else:
                    prev_close = int(candles[i-1]["stck_prpr"])
                    tr = max(high - low, abs(high - prev_close), abs(low - prev_close))

                atr_values.append(tr)

            atr = sum(atr_values) / len(atr_values)

            # 최신 캔들 (마지막)
            latest = candles[-1]
            high = int(latest["stck_hgpr"])
            low = int(latest["stck_lwpr"])
            close = int(latest["stck_prpr"])

            # 슈퍼트렌드 계산
            hl_avg = (high + low) / 2
            upperband = hl_avg + (multiplier * atr)
            lowerband = hl_avg - (multiplier * atr)

            # 추세 판단
            if close > upperband:
                trend = "UP"
                supertrend_value = lowerband
            elif close < lowerband:
                trend = "DOWN"
                supertrend_value = upperband
            else:
                # 이전 추세 유지
                prev_state = self.supertrend_state.get(stock_code, {})
                trend = prev_state.get("trend", "NEUTRAL")
                supertrend_value = upperband if trend == "DOWN" else lowerband

            return {
                "trend": trend,
                "supertrend": supertrend_value,
                "atr": atr,
                "close": close,
                "upperband": upperband,
                "lowerband": lowerband
            }

        except Exception as e:
            print(f"  ❌ 슈퍼트렌드 계산 실패: {e}")
            return None

    def check_supertrend_signal(self, stock_code: str, stock_name: str):
        """슈퍼트렌드 추세 변환 감지 및 알림"""
        current = self.calculate_supertrend(stock_code)
        if not current:
            return

        current_trend = current["trend"]
        prev_state = self.supertrend_state.get(stock_code, {})
        prev_trend = prev_state.get("trend")

        # 추세 변환 감지
        if prev_trend and prev_trend != current_trend:
            if current_trend == "UP":
                signal = "🟢 매수 신호"
                emoji = "📈"
            elif current_trend == "DOWN":
                signal = "🔴 매도 신호"
                emoji = "📉"
            else:
                signal = "⚪ 중립"
                emoji = "➡️"

            msg = f"""{emoji} [슈퍼트렌드] {stock_name} 추세 변환

{prev_trend} → {current_trend}

{signal}
현재가: {current['close']:,}원
슈퍼트렌드: {current['supertrend']:,.0f}원
ATR: {current['atr']:,.0f}"""

            send(msg)
            print(f"\n{msg}\n")

        # 상태 저장
        self.supertrend_state[stock_code] = current

    def analyze_with_llm(self, stock_info: dict, balance_data: dict) -> dict:
        """LLM을 이용한 매매 판단"""
        stock_code = stock_info["code"]

        # 쿨다운 체크
        now = time.time()
        if stock_code in self.cooldown:
            elapsed = now - self.cooldown[stock_code]
            if elapsed < self.llm_cooldown_seconds:
                print(f"  [{stock_info['name']}] LLM 분석 쿨다운 중 (남은 시간: {int(self.llm_cooldown_seconds - elapsed)}초)")
                return {"decision": "HOLD", "reason": "쿨다운"}

        raw_score = self.calculate_stock_entry_score(stock_info)
        backfill_pending_outcomes(
            agent="dave_stock",
            ticker=stock_code,
            current_price=stock_info.get("current_price", 0),
            workspace_root=WORKSPACE_ROOT,
        )
        evaluation = evaluate_entry(
            agent="dave_stock",
            ticker=stock_code,
            raw_score=raw_score,
            max_raw_score=10,
            reasons=[f"change_rate={stock_info.get('change_rate', 0):+.2f}%"],
            metrics={"current_price": stock_info.get("current_price"), "momentum_pct": stock_info.get("change_rate", 0)},
            workspace_root=WORKSPACE_ROOT,
        )
        print(f"  [{stock_info['name']}] 진입 평가: {format_evaluation(evaluation)}")
        record_decision(
            agent="dave_stock",
            ticker=stock_code,
            decision=evaluation["decision"],
            evaluation=evaluation,
            reason="stock_pre_llm_entry_gate",
            workspace_root=WORKSPACE_ROOT,
            extra={"observed_price": stock_info.get("current_price")},
        )
        if evaluation["decision"] == "HOLD":
            return {"decision": "HOLD", "reason": f"진입평가 HOLD {evaluation['entry_score']}점"}

        # 프롬프트 생성
        prompt = f"""한국 주식 매매 판단 AI.

종목: {stock_info['name']} ({stock_code})
현재가: {stock_info['current_price']:,}원
등락률: {stock_info['change_rate']:+.2f}%
거래량: {stock_info['volume']:,}주
고가: {stock_info['high']:,}원
저가: {stock_info['low']:,}원

전략: 데이브 보수적 가치투자
- 우량주 중심, 장기 보유
- 급락 시 저가 매수
- 과열 시 일부 차익실현

현재 잔고: {balance_data.get('cash', 0):,}원

판단:
1. BUY: 저평가 구간, 매수 기회
2. SELL: 과열, 차익실현
3. HOLD: 관망

JSON 형식으로 답변:
{{
  "decision": "BUY|SELL|HOLD",
  "reason": "40자 이내 이유",
  "confidence": 0-100
}}
"""

        try:
            result = llm_text(prompt, max_tokens=200, temperature=0.3, json_mode=True, lm_first=True)
            decision = json.loads(result)

            # 쿨다운 갱신
            self.cooldown[stock_code] = now
            record_decision(
                agent="dave_stock",
                ticker=stock_code,
                decision=decision.get("decision", "HOLD"),
                evaluation=evaluation,
                reason=decision.get("reason", ""),
                workspace_root=WORKSPACE_ROOT,
                extra={"llm_confidence": decision.get("confidence")},
            )

            return decision
        except Exception as e:
            print(f"  ❌ LLM 분석 실패: {e}")
            return {"decision": "HOLD", "reason": "LLM 오류"}

    def calculate_stock_entry_score(self, stock_info: dict) -> float:
        """주식용 진입 원점수 (외국인 매수세 + 보조지표 반영)"""
        score = 0.0
        change_rate = float(stock_info.get("change_rate", 0) or 0)
        volume = int(stock_info.get("volume", 0) or 0)
        current = int(stock_info.get("current_price", 0) or 0)
        open_price = int(stock_info.get("open", 0) or 0)
        high = int(stock_info.get("high", 0) or 0)
        low = int(stock_info.get("low", 0) or 0)

        # 기존 등락률 기준
        if -3.0 <= change_rate <= -0.5:
            score += 3.0  # 보수적 저가 매수 후보
        elif 0.5 <= change_rate <= 4.0:
            score += 2.0  # 상승 확인
        elif change_rate > 7.0:
            score -= 2.0  # 과열

        # 거래량/기술 지표
        if volume >= 100_000:
            score += 2.0
        if open_price > 0 and current > open_price:
            score += 1.5
        if high > low and current > (low + (high - low) * 0.6):
            score += 1.5

        # 외국인 3일 연속 매수
        if stock_info.get("foreign_3day_buy", False):
            score += 2.0
            print(f"  [외국인] 3일 연속 순매수 확인 (+2점)")

        # RSI 50 이상 (상승 압력)
        rsi = stock_info.get("rsi", 0)
        if rsi >= 50:
            score += 2.0
            print(f"  [RSI] {rsi:.1f} ≥ 50 (+2점)")

        # MACD 골든크로스
        if stock_info.get("macd_golden", False):
            score += 2.0
            macd = stock_info.get("macd", 0)
            signal = stock_info.get("macd_signal", 0)
            print(f"  [MACD] 골든크로스 ({macd:.2f} > {signal:.2f}) (+2점)")

        if current <= 0:
            score = 0.0
        return max(score, 0.0)

    def execute_trade(self, stock_code: str, decision: dict):
        """매매 실행"""
        if decision["decision"] == "HOLD":
            return

        stock_info = self.get_stock_info(stock_code)
        if not stock_info:
            return

        try:
            if decision["decision"] == "BUY":
                # 시장가 1주 매수 (테스트)
                result = self.client.buy_stock(stock_code, 1, 0)
                if result.get("rt_cd") == "0":
                    send(f"✅ [데이브 주식] {stock_info['name']} 매수\n이유: {decision['reason']}")
                else:
                    print(f"  ❌ 매수 실패: {result}")

            elif decision["decision"] == "SELL":
                # 보유 확인 후 매도
                result = self.client.sell_stock(stock_code, 1, 0)
                if result.get("rt_cd") == "0":
                    send(f"✅ [데이브 주식] {stock_info['name']} 매도\n이유: {decision['reason']}")
                else:
                    print(f"  ❌ 매도 실패: {result}")

        except Exception as e:
            print(f"  ❌ 매매 실행 오류: {e}")

    def run_cycle(self):
        """1회 매매 사이클 (슈퍼트렌드 모니터링)"""
        print(f"\n--- [{datetime.now().strftime('%H:%M:%S')}] 슈퍼트렌드 모니터링 ---")

        # 슈퍼트렌드 감시 종목 체크
        for stock_code, stock_name in SUPERTREND_WATCH:
            print(f"\n📊 {stock_name} ({stock_code}) 슈퍼트렌드 체크...")
            self.check_supertrend_signal(stock_code, stock_name)

        print("\n✅ 사이클 완료")


def main():
    """메인 함수"""
    trader = StockAutoTrader()

    if "--daemon" in sys.argv:
        print("🤖 원익IPS 슈퍼트렌드 모니터링 시작 (1분봉)")

        with ProcessLock("dave_stock"):
            try:
                while True:
                    # 장중에만 실행 (09:00 ~ 15:30)
                    now = datetime.now()
                    hour = now.hour
                    minute = now.minute

                    if 9 <= hour < 16:
                        if not (hour == 15 and minute >= 30):
                            try:
                                trader.run_cycle()
                            except Exception as e:
                                print(f"[Daemon Error] {e}")
                        else:
                            print(f"[{now.strftime('%H:%M')}] 장 종료 시간")
                    else:
                        print(f"[{now.strftime('%H:%M')}] 장외 시간")

                    time.sleep(60)  # 1분 대기
            except KeyboardInterrupt:
                print("[Dave Stock] stopped")
    else:
        # 1회 실행
        trader.run_cycle()


if __name__ == "__main__":
    main()
