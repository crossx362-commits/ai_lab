#!/usr/bin/env python3
"""현빈 - 한국 주식 시장 정보 수집"""
import os, sys, json, time, requests
from datetime import datetime
from typing import Dict, Any

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
WORKSPACE_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock

load_env()

# KIS 클라이언트
sys.path.insert(0, os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools"))
from kis_client import KISClient


class StockMarketIntelligence:
    """한국 주식 시장 정보 수집"""

    def __init__(self):
        self.output_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "stock_market_intel.json")
        self.client = KISClient()

    def get_kospi_kosdaq_index(self) -> Dict[str, Any]:
        """KOSPI/KOSDAQ 지수 조회"""
        try:
            # KOSPI
            kospi = self.client.get_current_price("0001")  # KOSPI 지수
            # KOSDAQ
            kosdaq = self.client.get_current_price("1001")  # KOSDAQ 지수

            result = {
                "kospi": {
                    "value": int(kospi.get("output", {}).get("stck_prpr", 0)) if "output" in kospi else 0,
                    "change": float(kospi.get("output", {}).get("prdy_ctrt", 0)) if "output" in kospi else 0
                },
                "kosdaq": {
                    "value": int(kosdaq.get("output", {}).get("stck_prpr", 0)) if "output" in kosdaq else 0,
                    "change": float(kosdaq.get("output", {}).get("prdy_ctrt", 0)) if "output" in kosdaq else 0
                }
            }
            return result
        except Exception as e:
            print(f"[StockIntel] 지수 조회 실패: {e}")
            return {"error": str(e)}

    def get_top_volume_stocks(self) -> Dict[str, Any]:
        """거래량 상위 종목 (네이버 금융 스크래핑 대안)"""
        # TODO: KIS API로 거래량 상위 종목 조회
        # 현재는 수동으로 주요 종목만 확인
        hot_stocks = [
            "005930",  # 삼성전자
            "000660",  # SK하이닉스
            "035720",  # 카카오
            "035420",  # NAVER
        ]

        result = []
        for code in hot_stocks:
            try:
                data = self.client.get_current_price(code)
                if "output" in data:
                    output = data["output"]
                    result.append({
                        "code": code,
                        "name": output.get("prdt_name", "N/A"),
                        "price": int(output.get("stck_prpr", 0)),
                        "change": float(output.get("prdy_ctrt", 0)),
                        "volume": int(output.get("acml_vol", 0))
                    })
            except Exception as e:
                print(f"  ❌ {code} 조회 실패: {e}")

        return {"stocks": result}

    def get_economic_calendar(self) -> Dict[str, Any]:
        """경제 지표 일정 (수동 입력)"""
        # TODO: 실시간 경제 캘린더 API 연동
        events = {
            "this_week": [
                {"date": "2026-06-20", "event": "한국은행 금리 결정", "impact": "HIGH"},
                {"date": "2026-06-22", "event": "무역수지 발표", "impact": "MEDIUM"}
            ],
            "next_week": [
                {"date": "2026-06-27", "event": "GDP 성장률 발표", "impact": "HIGH"}
            ]
        }
        return events

    def analyze_market_sentiment(self, intel: Dict[str, Any]) -> str:
        """시장 심리 분석"""
        kospi_change = intel.get("indexes", {}).get("kospi", {}).get("change", 0)
        kosdaq_change = intel.get("indexes", {}).get("kosdaq", {}).get("change", 0)

        if kospi_change > 1 and kosdaq_change > 1:
            sentiment = "강세장 - 적극 매수"
            risk = "LOW"
        elif kospi_change < -1 and kosdaq_change < -1:
            sentiment = "약세장 - 신중 대응"
            risk = "HIGH"
        else:
            sentiment = "횡보장 - 관망"
            risk = "MEDIUM"

        return sentiment

    def collect_all(self, notify: bool = False) -> Dict[str, Any]:
        """모든 정보 수집"""
        print("[현빈 주식] 한국 주식 시장 정보 수집 시작...")

        intel = {
            "timestamp": datetime.now().isoformat(),
            "indexes": self.get_kospi_kosdaq_index(),
            "hot_stocks": self.get_top_volume_stocks(),
            "economic_calendar": self.get_economic_calendar(),
        }

        # 시장 심리 분석
        intel["sentiment"] = self.analyze_market_sentiment(intel)

        # JSON 저장
        os.makedirs(os.path.dirname(self.output_path), exist_ok=True)
        with open(self.output_path, "w", encoding="utf-8") as f:
            json.dump(intel, f, ensure_ascii=False, indent=2)

        print(f"✅ 저장: {self.output_path}")

        # 중요 변화 알림
        if notify:
            kospi = intel["indexes"]["kospi"]
            kosdaq = intel["indexes"]["kosdaq"]

            if abs(kospi.get("change", 0)) > 2 or abs(kosdaq.get("change", 0)) > 2:
                msg = f"📊 [현빈 주식] 시장 급변동\n"
                msg += f"KOSPI: {kospi.get('value', 0):,.0f} ({kospi.get('change', 0):+.2f}%)\n"
                msg += f"KOSDAQ: {kosdaq.get('value', 0):,.0f} ({kosdaq.get('change', 0):+.2f}%)"
                send(msg)

        return intel

    def generate_summary(self, intel: Dict[str, Any]) -> str:
        """요약 생성"""
        lines = ["📊 [현빈] 한국 주식 시장 정보\n"]

        # 지수
        kospi = intel.get("indexes", {}).get("kospi", {})
        kosdaq = intel.get("indexes", {}).get("kosdaq", {})
        lines.append(f"KOSPI: {kospi.get('value', 0):,.0f} ({kospi.get('change', 0):+.2f}%)")
        lines.append(f"KOSDAQ: {kosdaq.get('value', 0):,.0f} ({kosdaq.get('change', 0):+.2f}%)")

        # 시장 심리
        lines.append(f"\n시장 심리: {intel.get('sentiment', 'N/A')}")

        # 거래량 상위
        hot_stocks = intel.get("hot_stocks", {}).get("stocks", [])
        if hot_stocks:
            lines.append("\n📈 주요 종목:")
            for stock in hot_stocks[:3]:
                lines.append(f"  {stock['name']}: {stock['price']:,}원 ({stock['change']:+.2f}%)")

        return "\n".join(lines)


def main(notify=False):
    """메인 실행"""
    collector = StockMarketIntelligence()
    intel = collector.collect_all(notify=notify)
    summary = collector.generate_summary(intel)
    print(summary)
    print("\n✅ 한국 주식 시장 정보 수집 완료")


if __name__ == "__main__":
    if "--daemon" in sys.argv:
        print("🤖 [현빈 주식] 시장 정보 수집 데몬 시작 (5분 주기)")

        with ProcessLock("hyunbin_stock"):
            try:
                while True:
                    try:
                        main(notify=True)
                    except Exception as e:
                        print(f"[Daemon Error] {e}")
                    time.sleep(300)  # 5분 대기
            except KeyboardInterrupt:
                print("[Hyunbin Stock] stopped")
    else:
        main(notify=False)
