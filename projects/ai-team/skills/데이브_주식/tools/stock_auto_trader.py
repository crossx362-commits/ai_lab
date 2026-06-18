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

load_env()

# 로컬 모듈
sys.path.insert(0, _here)
from kis_client import KISClient

# 슈퍼트렌드 모니터링 종목 (사용자 지정)
SUPERTREND_WATCH = [
    ("240810", "원익IPS"),
]

# 기본 감시 대상 비활성화 (슈퍼트렌드만 사용)
BASE_STOCKS = []

WORKSPACE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))

def get_dynamic_stocks():
    """현빈 인텔 기반 동적 종목 선정 (퀀트 점수 기준)"""
    stocks = BASE_STOCKS.copy()

    try:
        intel_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "stock_market_intel.json")
        if os.path.exists(intel_path):
            with open(intel_path, "r", encoding="utf-8") as f:
                intel = json.load(f)

            # 현빈 종목별 퀀트 점수 확인 (상위 5개)
            if "stock_analysis" in intel:
                scored = [(s["code"], s["name"], s.get("score", 0)) for s in intel["stock_analysis"]]
                scored.sort(key=lambda x: x[2], reverse=True)

                for code, name, score in scored[:5]:
                    if score >= 50:  # 50점 이상만
                        stocks.append(code)

                print(f"[Dave 주식] 현빈 고득점: {', '.join([f'{n}({s}점)' for c, n, s in scored[:3]])}")

            # 중복 제거
            stocks = list(dict.fromkeys(stocks))
            print(f"[Dave 주식] 동적 종목: {len(stocks)}개 (기본 {len(BASE_STOCKS)} + 현빈 {len(stocks) - len(BASE_STOCKS)})")
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
        """종목 정보 수집"""
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

            return info
        except Exception as e:
            print(f"❌ {stock_code} 정보 수집 실패: {e}")
            return None

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

            return decision
        except Exception as e:
            print(f"  ❌ LLM 분석 실패: {e}")
            return {"decision": "HOLD", "reason": "LLM 오류"}

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
    global DAVE_STOCKS

    if "--daemon" in sys.argv:
        DAVE_STOCKS = get_dynamic_stocks()
        print(f"🤖 데이브 주식 자동매매 시작: {len(DAVE_STOCKS)}개 종목")
        iteration = 0

        with ProcessLock("dave_stock"):
            try:
                trader = StockAutoTrader()
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
