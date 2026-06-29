#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 모의 성과 추적 — 실제 청산 거래를 백테스트 기대치와 비교.

'백테스트가 +298~385%였는데 실제도 그렇게 가고 있나?'를 검증하는 관문.
청산 로그(somi_closed_trades.json)로 실제 승률·손익비·평균수익을 계산하고,
검증된 백테스트 기대치(모멘텀+국면+보유20+확신사이징)와 나란히 보여준다.
"""

from __future__ import annotations

import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

import json  # noqa: E402

from _shared.env import load_env  # noqa: E402
from _shared.notify import publish_report  # noqa: E402
from somi_trade_advisor import CLOSED_TRADES_FILE, load_positions  # noqa: E402

load_env(str(PROJECT_ROOT))

# 검증된 백테스트 기대치 (모멘텀 점수60 + 상승국면 게이트 + 보유20일 + 확신사이징, 24/30개월)
BENCH = {"win_rate": 66.0, "profit_factor": 1.4, "avg_ret": 1.6}
MIN_SAMPLE = 5  # 이보다 적으면 통계 무의미 — 더 모아야


def _load_closed() -> list[dict]:
    try:
        d = json.loads(CLOSED_TRADES_FILE.read_text(encoding="utf-8")) if CLOSED_TRADES_FILE.exists() else []
        return d if isinstance(d, list) else []
    except Exception:
        return []


def _realized_stats(trades: list[dict]) -> dict:
    rets = [t.get("ret_pct", 0) for t in trades]
    wins = [r for r in rets if r > 0]
    losses = [r for r in rets if r <= 0]
    eq = 1.0
    for r in rets:
        eq *= (1 + r / 100)
    pf = (sum(wins) / -sum(losses)) if losses and sum(losses) < 0 else 0
    return {
        "n": len(rets),
        "win_rate": round(len(wins) / len(rets) * 100, 1) if rets else 0,
        "avg_ret": round(sum(rets) / len(rets), 2) if rets else 0,
        "avg_win": round(sum(wins) / len(wins), 2) if wins else 0,
        "avg_loss": round(sum(losses) / len(losses), 2) if losses else 0,
        "profit_factor": round(pf, 2),
        "total_return": round((eq - 1) * 100, 1),
    }


def _unrealized() -> dict:
    """현재 보유 모의 포지션의 평가손익."""
    from somi_kis_reporter import KISClient, num
    pos = load_positions()
    if not pos:
        return {"n": 0, "upnl": 0.0}
    kis = KISClient()
    total = 0.0
    n = 0
    for sym, p in pos.items():
        try:
            cur = num(kis.quote(sym).get("stck_prpr"))
        except Exception:
            continue
        entry = num(p.get("entry"))
        if cur and entry:
            total += (cur - entry) / entry * 100
            n += 1
    return {"n": n, "upnl": round(total / n, 2) if n else 0.0}


def _verdict(real: dict) -> str:
    if real["n"] < MIN_SAMPLE:
        return f"표본 부족({real['n']}/{MIN_SAMPLE}) — 더 쌓여야 판단 가능"
    wr_ok = real["win_rate"] >= BENCH["win_rate"] - 12      # 허용 오차
    pf_ok = real["profit_factor"] >= BENCH["profit_factor"] - 0.3
    if wr_ok and pf_ok:
        return "✅ 백테스트 기대치 추종 중 — 전략이 실제로도 작동"
    if real["profit_factor"] >= 1.0:
        return "🟡 수익은 나나 기대 이하 — 표본 더 보며 관찰"
    return "🔴 기대 미달(손익비<1) — 라이브 가정 점검 필요"


def build() -> str:
    real = _realized_stats(_load_closed())
    unreal = _unrealized()
    lines = ["📊 소미 모의 성과 (백테스트 대비)\n"]
    lines.append(f"[실현] 청산 {real['n']}건")
    if real["n"]:
        lines.append(f" 승률 {real['win_rate']}% (기대 {BENCH['win_rate']}%)")
        lines.append(f" 손익비 {real['profit_factor']} (기대 {BENCH['profit_factor']})")
        lines.append(f" 평균수익 {real['avg_ret']}%/건 (기대 {BENCH['avg_ret']}%) · 누적 {real['total_return']}%")
        lines.append(f" 평균 익절 +{real['avg_win']}% / 평균 손절 {real['avg_loss']}%")
    lines.append(f"[평가] 보유 {unreal['n']}종목 평가손익 {unreal['upnl']:+.2f}%/종목")
    lines.append(f"\n판정: {_verdict(real)}")
    return "\n".join(lines)


def main() -> None:
    import argparse
    ap = argparse.ArgumentParser(description="소미 모의 성과 추적(백테스트 대비)")
    ap.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = ap.parse_args()
    report = build()
    print(report)
    if args.send:
        publish_report("소미 모의 성과", report)


if __name__ == "__main__":
    main()
