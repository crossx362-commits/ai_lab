#!/usr/bin/env python3
"""현빈 - 암호화폐 시장 정보 수집 에이전트"""
import os, sys, json, time, datetime, requests
from typing import Dict, Any

# UTF-8 인코딩 강제
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
WORKSPACE_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock

load_env()


class CryptoMarketIntelligence:
    """암호화폐 시장 정보 수집 클래스"""

    def __init__(self):
        self.output_path = os.path.join(WORKSPACE_ROOT, "reports", "research", "crypto_market_intel.json")
        self.cache_ttl = 300  # 5분 캐시

    def get_fed_events(self) -> Dict[str, Any]:
        """연준(Fed) 주요 이벤트 일정 수집"""
        # TODO: Fed Calendar API 또는 웹 스크래핑
        # 현재는 수동 입력된 주요 일정 반환
        events = {
            "next_fomc": "2026-06-17",
            "next_cpi": "2026-07-10",
            "next_nfp": "2026-07-05",
            "current_status": "FOMC 발표 1일 전 - 고위험 관망 구간",
            "risk_level": "HIGH"  # LOW, MEDIUM, HIGH
        }
        return events

    def get_fear_greed_index(self) -> Dict[str, Any]:
        """공포탐욕지수 (Fear & Greed Index) 수집"""
        try:
            url = "https://api.alternative.me/fng/?limit=1"
            response = requests.get(url, timeout=10)
            data = response.json()

            if data and "data" in data and len(data["data"]) > 0:
                latest = data["data"][0]
                value = int(latest["value"])
                classification = latest["value_classification"]

                # 데이브 SKILL 기준 해석
                if value <= 20:
                    signal = "극단적 공포 - 역발상 매수 준비"
                    action = "BUY_SIGNAL"
                elif value >= 75:
                    signal = "극단적 탐욕 - 포모 경보, 신규 진입 금지"
                    action = "SELL_SIGNAL"
                else:
                    signal = "중립 구간"
                    action = "NEUTRAL"

                return {
                    "value": value,
                    "classification": classification,
                    "signal": signal,
                    "action": action,
                    "timestamp": latest["timestamp"]
                }
        except Exception as e:
            print(f"[CryptoIntel] 공포탐욕지수 수집 실패: {e}")
            return {"error": str(e)}

        return {"error": "No data"}

    def get_kimchi_premium(self) -> Dict[str, Any]:
        """김치 프리미엄 계산 (업비트 vs 바이낸스)"""
        try:
            # 업비트 BTC 가격
            upbit_url = "https://api.upbit.com/v1/ticker?markets=KRW-BTC"
            upbit_res = requests.get(upbit_url, timeout=10)
            upbit_price = upbit_res.json()[0]["trade_price"]

            # 바이낸스 BTC 가격 (USDT)
            binance_url = "https://api.binance.com/api/v3/ticker/price?symbol=BTCUSDT"
            binance_res = requests.get(binance_url, timeout=10)
            binance_price_usd = float(binance_res.json()["price"])

            # 환율 (USD to KRW) - 고정 1,300원 또는 실시간 API 사용 가능
            usd_to_krw = 1300  # TODO: 실시간 환율 API 연동
            binance_price_krw = binance_price_usd * usd_to_krw

            # 김치 프리미엄 계산
            premium_pct = ((upbit_price - binance_price_krw) / binance_price_krw) * 100

            # 데이브 SKILL 기준 해석
            if premium_pct >= 5.0:
                signal = "국내 투기 과열 - 고점 경보, 자산 대피"
                action = "SELL_SIGNAL"
            elif premium_pct <= -3.0:
                signal = "국내 공포 극대화 - 역발상 저점 신호"
                action = "BUY_SIGNAL"
            else:
                signal = "정상 범위"
                action = "NEUTRAL"

            return {
                "upbit_price": upbit_price,
                "binance_price_krw": binance_price_krw,
                "premium_pct": round(premium_pct, 2),
                "signal": signal,
                "action": action
            }
        except Exception as e:
            print(f"[CryptoIntel] 김치 프리미엄 계산 실패: {e}")
            return {"error": str(e)}

    def get_whale_alerts(self) -> Dict[str, Any]:
        """고래 거래 알림 (Whale Alert API)"""
        # TODO: Whale Alert API 키 필요
        # https://docs.whale-alert.io/
        return {
            "status": "not_implemented",
            "message": "Whale Alert API 키 필요 - 추후 구현"
        }

    def get_liquidation_map(self) -> Dict[str, Any]:
        """청산 맵 데이터 (Coinglass API)"""
        try:
            # Coinglass Public API (일부 데이터는 무료)
            # TODO: 상세 청산 맵은 유료 API 키 필요
            url = "https://open-api.coinglass.com/public/v2/liquidation"
            headers = {"accept": "application/json"}
            response = requests.get(url, headers=headers, timeout=10)

            if response.status_code == 200:
                data = response.json()
                return {
                    "status": "success",
                    "data": data,
                    "message": "청산 맵 데이터 수집 완료"
                }
            else:
                return {"error": f"HTTP {response.status_code}"}
        except Exception as e:
            print(f"[CryptoIntel] 청산 맵 수집 실패: {e}")
            return {
                "status": "not_implemented",
                "message": "Coinglass API 연동 필요 - 추후 구현"
            }

    def get_crypto_news(self) -> Dict[str, Any]:
        """암호화폐 주요 뉴스 (CryptoPanic API)"""
        try:
            # CryptoPanic Free API
            url = "https://cryptopanic.com/api/v1/posts/?auth_token=free&currencies=BTC,ETH&public=true"
            response = requests.get(url, timeout=10)

            if response.status_code == 200:
                data = response.json()
                if "results" in data:
                    # 최근 5개 주요 뉴스만
                    news_items = []
                    for item in data["results"][:5]:
                        news_items.append({
                            "title": item.get("title"),
                            "published_at": item.get("published_at"),
                            "url": item.get("url"),
                            "currencies": item.get("currencies", [])
                        })
                    return {
                        "status": "success",
                        "count": len(news_items),
                        "news": news_items
                    }
        except Exception as e:
            print(f"[CryptoIntel] 뉴스 수집 실패: {e}")
            return {"error": str(e)}

        return {"error": "No data"}

    def collect_all(self, notify=False) -> Dict[str, Any]:
        """모든 정보를 한 번에 수집"""
        print("[현빈] 암호화폐 시장 정보 수집 시작...")

        intel = {
            "timestamp": datetime.datetime.now().isoformat(),
            "fed_events": self.get_fed_events(),
            "fear_greed_index": self.get_fear_greed_index(),
            "kimchi_premium": self.get_kimchi_premium(),
            "whale_alerts": self.get_whale_alerts(),
            "liquidation_map": self.get_liquidation_map(),
            "crypto_news": self.get_crypto_news()
        }

        # JSON 파일로 저장 (데이브가 읽을 수 있도록)
        os.makedirs(os.path.dirname(self.output_path), exist_ok=True)
        with open(self.output_path, "w", encoding="utf-8") as f:
            json.dump(intel, f, ensure_ascii=False, indent=2)

        print(f"[현빈] 정보 수집 완료: {self.output_path}")

        # 중요 변화만 알림
        if notify:
            self.check_and_notify(intel)

        return intel

    def check_and_notify(self, intel: Dict[str, Any]):
        """상태 변화만 텔레그램 알림 (중복 방지)"""
        import json

        # 이전 상태 로드
        state_file = os.path.join(os.path.dirname(self.output_path), "hyunbin_alert_state.json")
        prev_state = {}
        if os.path.exists(state_file):
            try:
                with open(state_file, "r", encoding="utf-8") as f:
                    prev_state = json.load(f)
            except Exception:
                pass

        # 현재 상태
        fed = intel.get("fed_events", {})
        fg = intel.get("fear_greed_index", {})
        kp = intel.get("kimchi_premium", {})

        current_state = {
            "fed_risk": fed.get("risk_level"),
            "fg_action": fg.get("action"),
            "kp_alert": "HIGH" if abs(kp.get("premium_pct", 0)) >= 5 else "NORMAL"
        }

        # 변화 감지
        alerts = []

        # 연준 위험도 변화 (NORMAL → HIGH 또는 HIGH → NORMAL)
        if current_state["fed_risk"] != prev_state.get("fed_risk"):
            if current_state["fed_risk"] == "HIGH":
                alerts.append(f"🏛️ {fed.get('current_status')}")

        # 공포탐욕 극단 진입/탈출
        if current_state["fg_action"] != prev_state.get("fg_action"):
            if fg.get("action") in ["BUY_SIGNAL", "SELL_SIGNAL"]:
                alerts.append(f"😱 공포탐욕 {fg['value']} - {fg['signal']}")

        # 김프 극단 진입/탈출
        if current_state["kp_alert"] != prev_state.get("kp_alert"):
            if current_state["kp_alert"] == "HIGH":
                alerts.append(f"🌶️ 김프 {kp['premium_pct']:+.1f}% - {kp['signal']}")

        # 변화가 있을 때만 알림
        if alerts:
            msg = "🚨 [현빈] 중요 시장 변화\n" + "\n".join(alerts)
            send(msg)

        # 현재 상태 저장
        with open(state_file, "w", encoding="utf-8") as f:
            json.dump(current_state, f, ensure_ascii=False, indent=2)

    def generate_summary(self, intel: Dict[str, Any]) -> str:
        """수집된 정보를 요약 메시지로 변환"""
        lines = ["📊 [현빈] 암호화폐 시장 정보 업데이트\n"]

        # 연준 이벤트
        fed = intel.get("fed_events", {})
        lines.append(f"🏛️ 연준 일정: {fed.get('current_status', 'N/A')}")
        lines.append(f"   - 다음 FOMC: {fed.get('next_fomc', 'N/A')}")
        lines.append(f"   - 위험도: {fed.get('risk_level', 'N/A')}\n")

        # 공포탐욕지수
        fg = intel.get("fear_greed_index", {})
        if "value" in fg:
            lines.append(f"😱 공포탐욕지수: {fg['value']} ({fg['classification']})")
            lines.append(f"   → {fg['signal']}\n")

        # 김치 프리미엄
        kp = intel.get("kimchi_premium", {})
        if "premium_pct" in kp:
            lines.append(f"🌶️ 김치 프리미엄: {kp['premium_pct']}%")
            lines.append(f"   → {kp['signal']}\n")

        # 뉴스
        news = intel.get("crypto_news", {})
        if news.get("status") == "success":
            lines.append(f"📰 주요 뉴스 ({news.get('count', 0)}건)")
            for item in news.get("news", [])[:3]:
                lines.append(f"   • {item['title'][:50]}...")

        return "\n".join(lines)


def main(notify=False):
    """메인 실행 함수"""
    collector = CryptoMarketIntelligence()

    # 전체 정보 수집 (중요 변화만 알림)
    intel = collector.collect_all(notify=notify)

    # 요약 출력 (텔레그램은 중요 변화만)
    summary = collector.generate_summary(intel)
    print(summary)

    print("\n✅ 암호화폐 시장 정보 수집 완료")


if __name__ == "__main__":
    if "--daemon" in sys.argv:
        print("🤖 [현빈] 암호화폐 정보 수집 데몬 시작 (5분 주기)")

        with ProcessLock("hyunbin"):
            try:
                while True:
                    try:
                        main(notify=True)  # 데몬 모드는 중요 변화만 알림
                    except Exception as e:
                        print(f"[Daemon Error] {e}")

                    time.sleep(300)  # 5분 대기
            except KeyboardInterrupt:
                print("[Hyunbin] stopped")
    else:
        main(notify=False)  # 단발 실행은 알림 안 함
