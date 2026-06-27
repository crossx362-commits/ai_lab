#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 포지션 감시 — 보유 종목 수익률/목표가/손절가 점검 후 익절·손절 제안.

제안만 한다. 실제 매도는 사용자가 텔레그램에서 승인('소미 매도 …')해야 실행.
"""

from __future__ import annotations

import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

import os  # noqa: E402

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from somi_kis_reporter import KISClient, num  # noqa: E402
from somi_trade_advisor import load_positions, remove_position  # noqa: E402

load_env(str(PROJECT_ROOT))

TRAIL_PROFIT_PCT = 5.0   # 목표가 전이라도 +5% 이상이면 분할익절 참고 제안


def _is_paper() -> bool:
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


def _paper_sell(symbol: str, qty: int) -> str:
    """모의 모드 자동 매도 (실거래면 호출 안 함)."""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:
        return ""
    try:
        trader.order(symbol, int(qty), "sell", 0)
        remove_position(symbol)
        return " → 🧪 모의 자동 매도 체결"
    except Exception as exc:
        return f" → 자동 매도 실패: {exc}"


def check_positions() -> list[str]:
    positions = load_positions()
    if not positions:
        return []
    kis = KISClient()
    alerts = []
    for symbol, p in positions.items():
        try:
            q = kis.quote(symbol)
        except Exception:
            continue
        cur = num(q.get("stck_prpr"))
        if not cur:
            continue
        entry = num(p.get("entry"))
        stop = num(p.get("stop"))
        target = num(p.get("target"))
        name = p.get("name", symbol)
        pnl = ((cur - entry) / entry * 100) if entry else 0
        won = lambda v: f"{int(v):,}원"

        qty = int(num(p.get("qty")))
        paper = _is_paper()
        if stop and cur <= stop:
            action = _paper_sell(symbol, qty) if paper else "\n  매도하려면 '소미 매도 {} '라고 답해줘요.".format(name)
            alerts.append(
                f"🔴 손절 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%), 손절가 {won(stop)} 도달/이탈.{action}"
            )
        elif target and cur >= target:
            action = _paper_sell(symbol, qty) if paper else "\n  '소미 매도 {}' 또는 수량 지정 매도.".format(name)
            alerts.append(
                f"🟢 익절 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%), 목표가 {won(target)} 도달.{action}"
            )
        elif pnl >= TRAIL_PROFIT_PCT:
            alerts.append(
                f"🟡 분할익절 참고 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%). 목표가({won(target)}) 전이지만 일부 익절 고려 가능."
            )
    return alerts


def run(do_send: bool = False) -> str:
    alerts = check_positions()
    if not alerts:
        report = "보유 포지션: 익절/손절 신호 없음 (정상 감시 중)."
    else:
        report = f"[소미 포지션 점검 / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n\n" + "\n\n".join(alerts)
        if do_send:
            send(report)
    return report


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="소미 보유 포지션 익절/손절 점검")
    parser.add_argument("--send", action="store_true", help="신호 있으면 텔레그램 전송")
    args = parser.parse_args()
    print(run(args.send))


if __name__ == "__main__":
    main()
