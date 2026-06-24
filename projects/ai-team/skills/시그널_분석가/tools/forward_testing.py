#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
포워드 테스팅: 실시간 시그널 정확도 추적
- 시그널 발생 시각 기록
- 24시간/7일/30일 수익률 추적
- 정확도 통계 산출
"""
import os
import sys
import json
import time
from pathlib import Path
from datetime import datetime, timedelta

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
load_env(str(WORKSPACE_ROOT))

FORWARD_TEST_FILE = WORKSPACE_ROOT / "reports/research/forward_test_signals.json"


def load_forward_tests():
    """저장된 포워드 테스트 로드"""
    if not FORWARD_TEST_FILE.exists():
        return []

    try:
        with open(FORWARD_TEST_FILE, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        print(f"포워드 테스트 로드 실패: {e}")
        return []


def save_forward_tests(tests):
    """포워드 테스트 저장"""
    try:
        FORWARD_TEST_FILE.parent.mkdir(parents=True, exist_ok=True)
        with open(FORWARD_TEST_FILE, 'w', encoding='utf-8') as f:
            json.dump(tests, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"포워드 테스트 저장 실패: {e}")


def record_signal(ticker, score, signal, price, rsi=None, bb_pos=None):
    """새 시그널 기록"""
    tests = load_forward_tests()

    # 새 시그널 추가
    new_signal = {
        "ticker": ticker,
        "score": score,
        "signal": signal,
        "entry_price": price,
        "entry_time": datetime.now().isoformat(),
        "rsi": rsi,
        "bb_pos": bb_pos,
        "results": {}
    }

    tests.append(new_signal)

    # 최근 100개만 유지
    if len(tests) > 100:
        tests = tests[-100:]

    save_forward_tests(tests)
    print(f"[ForwardTest] 시그널 기록: {ticker} @ {price:,.0f}원 (score={score})")


def update_results():
    """모든 시그널 결과 업데이트"""
    tests = load_forward_tests()
    if not tests:
        return

    try:
        import pyupbit
    except:
        print("pyupbit 없음 - 결과 업데이트 스킵")
        return

    now = datetime.now()
    updated = 0

    for test in tests:
        ticker = test["ticker"]
        entry_price = test["entry_price"]
        entry_time = datetime.fromisoformat(test["entry_time"])

        # 이미 30일 경과한 시그널은 스킵
        if "30d" in test["results"]:
            continue

        try:
            current_price = pyupbit.get_current_price(ticker)
            if not current_price:
                continue

            profit_pct = ((current_price - entry_price) / entry_price * 100)
            elapsed = (now - entry_time).total_seconds()

            # 24시간 (86400초)
            if elapsed >= 86400 and "24h" not in test["results"]:
                test["results"]["24h"] = round(profit_pct, 2)
                updated += 1

            # 7일 (604800초)
            if elapsed >= 604800 and "7d" not in test["results"]:
                test["results"]["7d"] = round(profit_pct, 2)
                updated += 1

            # 30일 (2592000초)
            if elapsed >= 2592000 and "30d" not in test["results"]:
                test["results"]["30d"] = round(profit_pct, 2)
                updated += 1

        except Exception as e:
            print(f"[ForwardTest] {ticker} 업데이트 실패: {e}")

    if updated > 0:
        save_forward_tests(tests)
        print(f"[ForwardTest] {updated}개 결과 업데이트 완료")


def calculate_statistics():
    """포워드 테스트 통계 계산"""
    tests = load_forward_tests()
    if not tests:
        return None

    stats = {
        "total_signals": len(tests),
        "by_signal": {},
        "by_period": {},
        "accuracy": {}
    }

    # 시그널 타입별 통계
    for signal_type in ["STRONG_BUY", "BUY", "NEUTRAL", "SELL"]:
        subset = [t for t in tests if t.get("signal") == signal_type]
        if not subset:
            continue

        stats["by_signal"][signal_type] = {
            "count": len(subset),
            "avg_score": round(sum(t["score"] for t in subset) / len(subset), 1)
        }

        # 기간별 수익률
        for period in ["24h", "7d", "30d"]:
            results = [t["results"].get(period) for t in subset if period in t["results"]]
            if results:
                stats["by_signal"][signal_type][f"{period}_avg"] = round(sum(results) / len(results), 2)
                stats["by_signal"][signal_type][f"{period}_win_rate"] = round(
                    len([r for r in results if r > 0]) / len(results) * 100, 1
                )

    # 전체 기간별 통계
    for period in ["24h", "7d", "30d"]:
        results = [t["results"].get(period) for t in tests if period in t["results"]]
        if results:
            stats["by_period"][period] = {
                "count": len(results),
                "avg_return": round(sum(results) / len(results), 2),
                "win_rate": round(len([r for r in results if r > 0]) / len(results) * 100, 1),
                "max_gain": round(max(results), 2),
                "max_loss": round(min(results), 2)
            }

    # 정확도 (BUY 시그널이 실제로 수익을 냈는지)
    buy_signals = [t for t in tests if t.get("signal") in ["BUY", "STRONG_BUY"]]
    for period in ["24h", "7d", "30d"]:
        buy_results = [t["results"].get(period) for t in buy_signals if period in t["results"]]
        if buy_results:
            accuracy = len([r for r in buy_results if r > 0]) / len(buy_results) * 100
            stats["accuracy"][period] = round(accuracy, 1)

    return stats


def print_report():
    """포워드 테스트 리포트 출력"""
    print("\n" + "="*60)
    print("포워드 테스팅 리포트")
    print("="*60 + "\n")

    stats = calculate_statistics()
    if not stats:
        print("기록된 시그널 없음")
        return

    print(f"총 시그널: {stats['total_signals']}개\n")

    # 시그널 타입별
    print("시그널 타입별 성과:")
    for sig_type, data in stats["by_signal"].items():
        print(f"\n  {sig_type} ({data['count']}개, avg score={data['avg_score']})")
        for period in ["24h", "7d", "30d"]:
            avg_key = f"{period}_avg"
            wr_key = f"{period}_win_rate"
            if avg_key in data:
                print(f"    {period}: {data[avg_key]:+.2f}% (승률 {data[wr_key]:.1f}%)")

    # 전체 기간별
    print("\n전체 기간별 성과:")
    for period, data in stats["by_period"].items():
        print(f"  {period} ({data['count']}개): avg={data['avg_return']:+.2f}% " +
              f"승률={data['win_rate']:.1f}% " +
              f"max={data['max_gain']:+.2f}% min={data['max_loss']:+.2f}%")

    # 정확도
    print("\nBUY 시그널 정확도:")
    for period, accuracy in stats["accuracy"].items():
        print(f"  {period}: {accuracy:.1f}%")

    print("\n" + "="*60 + "\n")


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == "update":
        print("[ForwardTest] 결과 업데이트 시작...")
        update_results()
        print_report()
    elif len(sys.argv) > 1 and sys.argv[1] == "report":
        print_report()
    else:
        print("사용법:")
        print("  python forward_testing.py update  # 결과 업데이트")
        print("  python forward_testing.py report  # 리포트 출력")
