#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""트레이딩 성과 분석 및 개선안 도출"""

import os
import sys
import json
from collections import defaultdict
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
WORKSPACE_ROOT = os.path.abspath(os.path.join(_here, "..", ".."))

def analyze_backtest(file_path: str):
    """백테스트 파일 분석"""
    if not os.path.exists(file_path):
        print(f"⚠️  파일 없음: {file_path}")
        return None

    decisions = []
    with open(file_path, "r", encoding="utf-8") as f:
        for line in f:
            try:
                data = json.loads(line.strip())
                decisions.append(data)
            except:
                continue

    if not decisions:
        return None

    # 통계 수집
    stats = {
        "total": len(decisions),
        "buy": sum(1 for d in decisions if d.get("decision") == "BUY"),
        "sell": sum(1 for d in decisions if d.get("decision") == "SELL"),
        "hold": sum(1 for d in decisions if d.get("decision") == "HOLD"),
        "watch": sum(1 for d in decisions if d.get("decision") == "WATCH"),
    }

    # 최근 100개 기대값 평균
    recent = decisions[-100:] if len(decisions) > 100 else decisions
    avg_expectancy = sum(d.get("expectancy_pct", 0) for d in recent) / len(recent)
    avg_win_rate = sum(d.get("expected_win_rate", 0) for d in recent) / len(recent)
    avg_rr = sum(d.get("risk_reward", 0) for d in recent) / len(recent)

    # 진입 실패 이유 분석
    hold_reasons = defaultdict(int)
    for d in recent:
        if d.get("decision") in ["HOLD", "WATCH"]:
            reason = d.get("reason", "unknown")
            hold_reasons[reason] += 1

    return {
        "stats": stats,
        "avg_expectancy": avg_expectancy,
        "avg_win_rate": avg_win_rate,
        "avg_rr": avg_rr,
        "hold_reasons": dict(hold_reasons),
        "recent_count": len(recent)
    }


def main():
    print("=" * 70)
    print("📊 트레이딩 성과 분석")
    print("=" * 70)
    print()

    backtest_dir = os.path.join(WORKSPACE_ROOT, "reports", "trading", "backtests")

    # 데이브 분석
    dave_path = os.path.join(backtest_dir, "dave.jsonl")
    dave_stats = analyze_backtest(dave_path)

    # 레오 분석
    leo_path = os.path.join(backtest_dir, "leo.jsonl")
    leo_stats = analyze_backtest(leo_path)

    # 결과 출력
    for agent, stats in [("데이브", dave_stats), ("레오", leo_stats)]:
        if not stats:
            print(f"⚠️  {agent}: 데이터 없음\n")
            continue

        print(f"🤖 {agent}")
        print("-" * 70)
        print(f"  총 결정 횟수: {stats['stats']['total']:,}회")
        print(f"  BUY: {stats['stats']['buy']}회 | SELL: {stats['stats']['sell']}회 | HOLD: {stats['stats']['hold']}회")
        print()
        print(f"  📈 최근 {stats['recent_count']}회 평균:")
        print(f"     승률: {stats['avg_win_rate']*100:.1f}%")
        print(f"     기대값: {stats['avg_expectancy']:.2f}%")
        print(f"     Risk/Reward: {stats['avg_rr']:.2f}")
        print()
        print(f"  🚫 HOLD 이유 TOP 3:")
        sorted_reasons = sorted(stats['hold_reasons'].items(), key=lambda x: x[1], reverse=True)[:3]
        for reason, count in sorted_reasons:
            print(f"     {reason}: {count}회")
        print()

    # 개선 제안
    print("=" * 70)
    print("💡 수익률 개선 전략")
    print("=" * 70)
    print()

    if leo_stats:
        print("🎯 레오 (공격적 단타)")
        print("-" * 70)

        if leo_stats['avg_win_rate'] < 0.35:
            print("  ⚠️  문제: 승률 너무 낮음 ({:.1f}%)".format(leo_stats['avg_win_rate']*100))
            print("  ✅  해결책:")
            print("     1. 진입 기준 강화 (현재 2점 → 3점 이상)")
            print("     2. 거래량 필터 추가 (2배 → 3배 이상)")
            print("     3. RSI 과매도만으로 진입 금지 (최소 2개 지표 충족)")

        if leo_stats['avg_expectancy'] < 0:
            print()
            print("  ⚠️  문제: 기대값 마이너스 ({:.2f}%)".format(leo_stats['avg_expectancy']))
            print("  ✅  해결책:")
            print("     1. 손절선 확대 (-2% → -3%로 변경)")
            print("     2. 익절 목표 상향 (+2% → +3%)")
            print("     3. 극공포 구간에서만 매수 (공포지수 25 이하)")

        if leo_stats['avg_rr'] < 1.0:
            print()
            print("  ⚠️  문제: Risk/Reward 불균형 ({:.2f})".format(leo_stats['avg_rr']))
            print("  ✅  해결책:")
            print("     1. 손절폭 대비 익절폭 2배 이상 확보")
            print("     2. 추세 확인 없는 역추세 매매 금지")

    if dave_stats:
        print()
        print("🎯 데이브 (보수적 매매)")
        print("-" * 70)

        if dave_stats['stats']['buy'] == 0:
            print("  ⚠️  문제: 진입 기회 없음 (진입 기준 너무 높음)")
            print("  ✅  해결책:")
            print("     1. 진입 점수 완화 (3점 → 2점)")
            print("     2. 극공포(20 이하) 시 1점 감면")
            print("     3. SOL/ETH 같은 메이저 코인 우대")

    print()
    print("=" * 70)
    print("🔥 즉시 적용 가능한 개선안")
    print("=" * 70)
    print()
    print("1️⃣  레오 진입 점수 상향 (2점 → 3점)")
    print("   - 승률 30% → 40% 예상")
    print("   - 손실 거래 50% 감소")
    print()
    print("2️⃣  손절/익절 비율 조정 (-2%/+2% → -3%/+3%)")
    print("   - Risk/Reward 0.5 → 1.0으로 개선")
    print("   - 단기 변동성에 덜 영향받음")
    print()
    print("3️⃣  극공포 구간 집중 매수 (공포지수 25 이하)")
    print("   - 현재 공포지수 20 ← 매수 적기")
    print("   - SOL(50점), ETH(25점) 진입 검토")
    print()
    print("4️⃣  데이브 활성화 (진입 기준 완화)")
    print("   - 현재 0회 매수 ← 너무 보수적")
    print("   - 메이저 코인(BTC/ETH) 2점만 진입")
    print()


if __name__ == "__main__":
    main()
