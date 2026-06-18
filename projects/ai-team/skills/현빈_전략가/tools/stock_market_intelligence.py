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
from _shared.llm import ollama as llm_ollama

load_env()

# KIS 클라이언트
sys.path.insert(0, os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools"))
from kis_client import KISClient


class StockMarketIntelligence:
    """한국 주식 시장 정보 수집"""

    def __init__(self):
        self.output_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "stock_market_intel.json")
        self.client = None  # Lazy init to share token

    def _get_client(self):
        """KIS 클라이언트 싱글톤"""
        if self.client is None:
            self.client = KISClient()
        return self.client

    def get_kospi_kosdaq_index(self) -> Dict[str, Any]:
        """KOSPI/KOSDAQ 지수 조회 (KIS 업종/지수 TR: FHPUP02100000)"""
        try:
            client = self._get_client()
            # KOSPI (0001), KOSDAQ (1001) — 지수 전용 API 사용
            kospi_raw  = client.get_index_price("0001")
            kosdaq_raw = client.get_index_price("1001")

            def _parse(raw: dict, label: str) -> dict:
                out = raw.get("output", {})
                if not out:
                    print(f"[StockIntel] {label} 지수 응답 없음: {raw}")
                    return {"value": 0, "change": 0.0, "volume": 0}
                return {
                    "value":  float(out.get("bstp_nmix_prpr",  0)),   # 업종 지수 현재가
                    "change": float(out.get("bstp_nmix_prdy_ctrt", 0)), # 전일 대비율(%)
                    "volume": int(out.get("acml_vol", 0)),             # 누적 거래량
                }

            result = {
                "kospi":  _parse(kospi_raw,  "KOSPI"),
                "kosdaq": _parse(kosdaq_raw, "KOSDAQ"),
            }
            print(f"[StockIntel] KOSPI {result['kospi']['value']:,.2f} ({result['kospi']['change']:+.2f}%)")
            print(f"[StockIntel] KOSDAQ {result['kosdaq']['value']:,.2f} ({result['kosdaq']['change']:+.2f}%)")
            return result
        except Exception as e:
            print(f"[StockIntel] 지수 조회 실패: {e}")
            return {"error": str(e)}

    def analyze_top_stocks(self) -> list:
        """주요 종목 퀀트 점수 계산"""
        client = self._get_client()

        # KOSPI 우량주 + 성장주 + 배당주 (30개)
        STOCKS = [
            ("005930", "삼성전자"), ("000660", "SK하이닉스"), ("035720", "카카오"),
            ("035420", "NAVER"), ("051910", "LG화학"), ("006400", "삼성SDI"),
            ("005380", "현대차"), ("000270", "기아"), ("068270", "셀트리온"),
            ("207940", "삼성바이오로직스"), ("005490", "POSCO홀딩스"), ("373220", "LG에너지솔루션"),
            ("105560", "KB금융"), ("055550", "신한지주"), ("086790", "하나금융지주"),
            ("012330", "현대모비스"), ("009150", "삼성전기"), ("017670", "SK텔레콤"),
            ("036570", "엔씨소프트"), ("251270", "넷마블"), ("003670", "포스코퓨처엠"),
            ("096770", "SK이노베이션"), ("018260", "삼성에스디에스"), ("028260", "삼성물산"),
            ("000810", "삼성화재"), ("032830", "삼성생명"), ("015760", "한국전력"),
            ("034730", "SK"), ("051900", "LG생활건강"), ("003550", "LG"),
        ]

        results = []
        for code, name in STOCKS:
            try:
                # 현재가 조회
                data = client.get_current_price(code)
                if "output" not in data:
                    continue

                output = data["output"]
                price = int(output.get("stck_prpr", 0))
                change_pct = float(output.get("prdy_ctrt", 0))
                volume = int(output.get("acml_vol", 0))

                # 일봉 데이터 조회 (최근 50일)
                daily = client.get_daily_price(code, days=50)
                if not daily or "output2" not in daily:
                    continue

                candles = daily["output2"]
                if len(candles) < 20:
                    continue

                # 퀀트 점수 계산
                score = 0
                closes = [int(c["stck_clpr"]) for c in candles[:20]]
                volumes = [int(c["acml_vol"]) for c in candles[:20]]

                # 추세 (25점)
                ma5 = sum(closes[:5]) / 5
                ma20 = sum(closes) / 20
                if price > ma5 > ma20:
                    score += 25
                elif price > ma5:
                    score += 15
                elif price > ma20:
                    score += 10

                # 거래량 (20점)
                avg_vol = sum(volumes) / len(volumes)
                if volume > avg_vol * 1.5:
                    score += 20
                elif volume > avg_vol:
                    score += 10

                # 모멘텀 (20점)
                if change_pct > 3:
                    score += 20
                elif change_pct > 1:
                    score += 10
                elif change_pct > 0:
                    score += 5

                # 변동성 (15점) - 일간 등락률 표준편차
                changes = [(closes[i] - closes[i+1]) / closes[i+1] * 100 for i in range(len(closes)-1)]
                volatility = (sum([(c - sum(changes)/len(changes))**2 for c in changes]) / len(changes)) ** 0.5
                if volatility > 3:
                    score += 15
                elif volatility > 2:
                    score += 10
                elif volatility > 1:
                    score += 5

                # 상승 연속성 (10점)
                up_days = sum(1 for c in changes[:5] if c > 0)
                if up_days >= 4:
                    score += 10
                elif up_days >= 3:
                    score += 5

                results.append({
                    "code": code,
                    "name": name,
                    "score": score,
                    "price": price,
                    "change_pct": round(change_pct, 2),
                    "volatility": round(volatility, 2),
                    "volume_ratio": round(volume / avg_vol, 2) if avg_vol > 0 else 0
                })

            except Exception as e:
                print(f"[현빈 주식] {code} {name} 분석 실패: {e}")

        results.sort(key=lambda x: x["score"], reverse=True)
        return results

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

    def analyze_with_ollama(self, intel: Dict[str, Any]) -> str | None:
        """Ollama 로컴 LLM으로 주식 데이터 지능형 분석"""
        kospi = intel.get("indexes", {}).get("kospi", {})
        kosdaq = intel.get("indexes", {}).get("kosdaq", {})
        stocks = intel.get("stock_analysis", [])[:5]
        sentiment = intel.get("sentiment", "N/A")
        calendar = intel.get("economic_calendar", {})

        top_stocks = "\n".join(
            f"- {s['name']}({s['code']}): 퀀트 {s['score']}점, {s['change_pct']:+.1f}%, 거래량비 {s['volume_ratio']:.1f}x"
            for s in stocks
        ) if stocks else "데이터 없음"

        this_week = ", ".join(
            f"{e['date']} {e['event']}({e['impact']})"
            for e in calendar.get("this_week", [])
        ) or "없음"

        prompt = f"""당신은 현빈, 한국 주식 시장 분석 전문 AI입니다.
아래 실시간 주식 시장 데이터를 분석하고, 사장님(투자자)에게 한국어로 간결하게 실용적인 인사이트를 제공하세요.

=== 주식 시장 데이터 ({intel.get('timestamp', 'N/A')[:16]}) ===

[시장 지수]
- KOSPI: {kospi.get('value', 0):,.0f} ({kospi.get('change', 0):+.2f}%)
- KOSDAQ: {kosdaq.get('value', 0):,.0f} ({kosdaq.get('change', 0):+.2f}%)
- 시장 심리: {sentiment}

[퀀트 점수 TOP 5 종목]
{top_stocks}

[이번 주 주요 경제 일정]
{this_week}

=== 분석 요청 ===
1. 현재 KOSPI/KOSDAQ 시장 한줄 평가 (강세/약세/횟보)
2. 주목할 종목 또는 섹터 1~2개
3. 투자자에게 실용적인 한 줄 조언

간결하게 3~5줄 이내로 답변하세요."""

        print("[현빈 주식] Ollama 로컴 분석 요청 중...")
        result = llm_ollama(prompt, max_tokens=400, temperature=0.5)
        if result:
            print(f"[현빈 주식] Ollama 분석 완료 ({len(result)}자)")
        else:
            print("[현빈 주식] Ollama 분석 실패 → 규칙 기반 요약 사용")
        return result

    def collect_all(self, notify: bool = False) -> Dict[str, Any]:
        """모든 정보 수집"""
        print("[현빈 주식] 한국 주식 시장 정보 수집 시작...")

        intel = {
            "timestamp": datetime.now().isoformat(),
            "indexes": self.get_kospi_kosdaq_index(),
            "stock_analysis": self.analyze_top_stocks(),
            "economic_calendar": self.get_economic_calendar(),
        }

        # 시장 심리 분석
        intel["sentiment"] = self.analyze_market_sentiment(intel)

        # 상위 5개 요약
        if intel["stock_analysis"]:
            print(f"\n[현빈 주식] 퀀트 점수 TOP 5:")
            for s in intel["stock_analysis"][:5]:
                print(f"  {s['name']}({s['code']}): {s['score']}점 (변동 {s['change_pct']:+.1f}%)")

        # Ollama 로컴 분석
        ollama_analysis = self.analyze_with_ollama(intel)
        if ollama_analysis:
            intel["ollama_analysis"] = ollama_analysis

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
                ollama_insight = intel.get("ollama_analysis", "")
                msg = f"📊 [현빈 주식] 시장 급변동\n"
                msg += f"KOSPI: {kospi.get('value', 0):,.0f} ({kospi.get('change', 0):+.2f}%)\n"
                msg += f"KOSDAQ: {kosdaq.get('value', 0):,.0f} ({kosdaq.get('change', 0):+.2f}%)"
                if ollama_insight:
                    msg += f"\n\n🤖 Ollama 분석:\n{ollama_insight}"
                send(msg)

        return intel

    def generate_summary(self, intel: Dict[str, Any]) -> str:
        """Ollama 분석 우선, 없으면 규칙 기반 요약"""
        ollama_analysis = intel.get("ollama_analysis")
        if ollama_analysis:
            return "📊 [현빈] 한국 주식 분석 (Ollama 로컴)\n" + ollama_analysis

        # Fallback: 규칙 기반 요약
        lines = ["📊 [현빈] 한국 주식 시장 정보\n"]

        # 지수
        kospi = intel.get("indexes", {}).get("kospi", {})
        kosdaq = intel.get("indexes", {}).get("kosdaq", {})
        lines.append(f"KOSPI: {kospi.get('value', 0):,.0f} ({kospi.get('change', 0):+.2f}%)")
        lines.append(f"KOSDAQ: {kosdaq.get('value', 0):,.0f} ({kosdaq.get('change', 0):+.2f}%)")

        # 시장 심리
        lines.append(f"\n시장 심리: {intel.get('sentiment', 'N/A')}")

        # 퀀트 점수 상위
        stocks = intel.get("stock_analysis", [])
        if stocks:
            lines.append("\n📈 퀀트 점수 TOP 3:")
            for stock in stocks[:3]:
                lines.append(f"  {stock['name']}: {stock['score']}점 ({stock['change_pct']:+.2f}%)")

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
