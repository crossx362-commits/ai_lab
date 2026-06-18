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

# 데이브 감시 대상 (우량주 중심)
DAVE_STOCKS = [
    "005930",  # 삼성전자
    "000660",  # SK하이닉스
    "005380",  # 현대차
    "035720",  # 카카오
    "051910",  # LG화학
    "006400",  # 삼성SDI
    "035420",  # NAVER
    "000270",  # 기아
]

class StockAutoTrader:
    """한국 주식 자동매매 (데이브 보수적 전략)"""

    def __init__(self):
        self.client = KISClient()
        self.cooldown = {}  # LLM 분석 쿨다운
        self.llm_cooldown_seconds = 600  # 10분

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
        """1회 매매 사이클"""
        print(f"\n--- [{datetime.now().strftime('%H:%M:%S')}] 데이브 주식 자동매매 ---")

        # 잔고 조회
        balance = self.client.get_balance()
        balance_data = {"cash": 0}

        if "output2" in balance and balance["output2"]:
            for item in balance["output2"]:
                if item.get("acnt_prdt_cd") == "01":  # 현금잔고
                    balance_data["cash"] = int(item.get("prvs_rcdl_excc_amt", 0))
                    break

        print(f"현금 잔고: {balance_data['cash']:,}원")

        # 우량주 스캔
        for stock_code in DAVE_STOCKS:
            stock_info = self.get_stock_info(stock_code)
            if not stock_info:
                continue

            print(f"\n📊 {stock_info['name']} ({stock_code})")
            print(f"   현재가: {stock_info['current_price']:,}원 ({stock_info['change_rate']:+.2f}%)")

            # LLM 분석
            decision = self.analyze_with_llm(stock_info, balance_data)
            print(f"   판단: {decision['decision']} - {decision.get('reason', 'N/A')}")

            # 매매 실행 (실전에서는 주의!)
            # self.execute_trade(stock_code, decision)

        print("\n✅ 사이클 완료")


def main():
    """메인 함수"""
    trader = StockAutoTrader()

    if "--daemon" in sys.argv:
        print("🤖 데이브 주식 자동매매 데몬 시작 (1분 주기)")

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
