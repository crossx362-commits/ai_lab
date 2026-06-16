# -*- coding: utf-8 -*-
"""
레오 자가 학습 시스템
거래 결과를 분석하고 전략을 개선
"""
import os
import sys
import json
import datetime
from typing import List, Dict

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.ollama_client import call_ollama

load_env()


class LeoLearningSystem:
    """레오의 자가 학습 시스템"""

    def __init__(self):
        self.trade_log_path = os.path.join(AI_TEAM_ROOT, "reports", "learning", "leo_trade_log.jsonl")
        self.knowledge_path = os.path.join(
            AI_TEAM_ROOT, "skills", "레오_트레이더", "knowledge", "learned_strategies.md"
        )

    def log_trade(self, trade_data: Dict):
        """거래 기록 저장"""
        os.makedirs(os.path.dirname(self.trade_log_path), exist_ok=True)

        log_entry = {
            "timestamp": datetime.datetime.now().isoformat(),
            **trade_data
        }

        with open(self.trade_log_path, "a", encoding="utf-8") as f:
            f.write(json.dumps(log_entry, ensure_ascii=False) + "\n")

    def analyze_trades(self, days: int = 7) -> Dict:
        """최근 N일 거래 분석"""
        if not os.path.exists(self.trade_log_path):
            return {"error": "거래 기록 없음"}

        trades = []
        cutoff = datetime.datetime.now() - datetime.timedelta(days=days)

        with open(self.trade_log_path, "r", encoding="utf-8") as f:
            for line in f:
                try:
                    trade = json.loads(line)
                    trade_time = datetime.datetime.fromisoformat(trade["timestamp"])
                    if trade_time >= cutoff:
                        trades.append(trade)
                except Exception:
                    continue

        if not trades:
            return {"error": f"최근 {days}일 거래 기록 없음"}

        # 승률 계산
        wins = [t for t in trades if t.get("result") == "WIN"]
        losses = [t for t in trades if t.get("result") == "LOSS"]
        total = len(wins) + len(losses)
        win_rate = len(wins) / total * 100 if total > 0 else 0

        # 평균 수익률
        avg_profit = sum(t.get("profit_pct", 0) for t in wins) / len(wins) if wins else 0
        avg_loss = sum(t.get("profit_pct", 0) for t in losses) / len(losses) if losses else 0

        # 최고/최악 코인
        coin_stats = {}
        for t in trades:
            ticker = t.get("ticker")
            if ticker not in coin_stats:
                coin_stats[ticker] = {"wins": 0, "losses": 0, "total_profit": 0}

            if t.get("result") == "WIN":
                coin_stats[ticker]["wins"] += 1
                coin_stats[ticker]["total_profit"] += t.get("profit_pct", 0)
            elif t.get("result") == "LOSS":
                coin_stats[ticker]["losses"] += 1
                coin_stats[ticker]["total_profit"] += t.get("profit_pct", 0)

        best_coin = max(coin_stats.items(), key=lambda x: x[1]["total_profit"])[0] if coin_stats else None
        worst_coin = min(coin_stats.items(), key=lambda x: x[1]["total_profit"])[0] if coin_stats else None

        return {
            "period_days": days,
            "total_trades": total,
            "wins": len(wins),
            "losses": len(losses),
            "win_rate": round(win_rate, 2),
            "avg_profit": round(avg_profit, 2),
            "avg_loss": round(avg_loss, 2),
            "best_coin": best_coin,
            "worst_coin": worst_coin,
            "coin_stats": coin_stats
        }

    def generate_insights(self, analysis: Dict) -> str:
        """Ollama로 인사이트 생성"""
        if "error" in analysis:
            return analysis["error"]

        prompt = f"""
당신은 레오, 공격적 단타 트레이더입니다.

최근 {analysis['period_days']}일 거래 성과:
- 총 거래: {analysis['total_trades']}회
- 승률: {analysis['win_rate']}%
- 평균 수익: {analysis['avg_profit']}%
- 평균 손실: {analysis['avg_loss']}%
- 최고 코인: {analysis['best_coin']}
- 최악 코인: {analysis['worst_coin']}

코인별 상세:
{json.dumps(analysis['coin_stats'], ensure_ascii=False, indent=2)}

위 데이터를 분석하고 다음을 제시하세요:

1. **전략 개선 포인트** (3가지)
   - 어떤 패턴에서 손실이 많았는가?
   - 어떤 코인/조건에서 승률이 높았는가?

2. **진입/청산 규칙 조정 제안**
   - 진입 점수 기준 조정 필요?
   - 익절/손절 비율 조정 필요?

3. **감시 코인 리스트 업데이트**
   - 제외할 코인
   - 추가할 코인

간결하고 실행 가능한 제안만 해주세요.
"""

        return call_ollama(prompt, model="llama3.1:latest")

    def update_knowledge(self, insights: str):
        """학습한 내용을 지식 파일에 추가"""
        os.makedirs(os.path.dirname(self.knowledge_path), exist_ok=True)

        timestamp = datetime.datetime.now().strftime("%Y-%m-%d")
        new_content = f"""
## 학습 일지: {timestamp}

{insights}

---

"""

        # 기존 내용 앞에 추가
        existing = ""
        if os.path.exists(self.knowledge_path):
            with open(self.knowledge_path, "r", encoding="utf-8") as f:
                existing = f.read()

        with open(self.knowledge_path, "w", encoding="utf-8") as f:
            f.write(new_content + existing)

        print(f"[Leo Learning] 지식 업데이트 완료: {self.knowledge_path}")

    def run_daily_learning(self):
        """일일 학습 루틴"""
        print("[Leo Learning] 거래 성과 분석 시작...")

        # 최근 7일 분석
        analysis = self.analyze_trades(days=7)

        if "error" in analysis:
            print(f"[Leo Learning] {analysis['error']}")
            return

        print(f"[Leo Learning] 최근 7일 성과:")
        print(f"  - 거래: {analysis['total_trades']}회")
        print(f"  - 승률: {analysis['win_rate']}%")
        print(f"  - 평균 수익/손실: {analysis['avg_profit']}% / {analysis['avg_loss']}%")

        # Ollama로 인사이트 생성
        print("[Leo Learning] Ollama 분석 중...")
        insights = self.generate_insights(analysis)

        # 지식 파일 업데이트
        self.update_knowledge(insights)

        print("[Leo Learning] 학습 완료!")
        print(insights)


if __name__ == "__main__":
    learner = LeoLearningSystem()

    # 테스트용 샘플 데이터
    if "--test" in sys.argv:
        sample_trades = [
            {
                "ticker": "KRW-DOGE",
                "entry_price": 100,
                "exit_price": 105,
                "profit_pct": 5.0,
                "result": "WIN"
            },
            {
                "ticker": "KRW-PEPE",
                "entry_price": 50,
                "exit_price": 48,
                "profit_pct": -4.0,
                "result": "LOSS"
            },
            {
                "ticker": "KRW-DOGE",
                "entry_price": 102,
                "exit_price": 105,
                "profit_pct": 2.94,
                "result": "WIN"
            },
        ]

        for trade in sample_trades:
            learner.log_trade(trade)

    # 학습 실행
    learner.run_daily_learning()
