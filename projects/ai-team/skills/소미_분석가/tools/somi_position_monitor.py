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
import time  # noqa: E402

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from somi_kis_reporter import KISClient, num  # noqa: E402
from somi_trade_advisor import load_positions, log_closed_trade, remove_position  # noqa: E402

load_env(str(PROJECT_ROOT))

TRAIL_PROFIT_PCT = 5.0   # 목표가 전이라도 +5% 이상이면 분할익절 참고 제안
# 백테스트 재검증(40종목·다기간 12/24/30개월, 기준 60): 보유 18~25일이 견고한 고원.
# 7일은 승자를 일찍 놔줘 모멘텀 수익을 버림 — 20일이 절대수익 최상위·MDD 균형(24mo +134→+264%).
MAX_HOLD_DAYS = int(os.getenv("SOMI_MAX_HOLD_DAYS", "20"))


def _busdays_held(ts: str) -> int:
    """기록 시각(ts)부터 오늘까지 거래일(평일) 경과 수."""
    import numpy as np
    from datetime import datetime
    try:
        d0 = datetime.strptime(str(ts)[:10], "%Y-%m-%d").date()
    except Exception:
        return 0
    return int(np.busday_count(d0, datetime.now().date()))


def _is_paper() -> bool:
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


def _paper_sell(symbol: str, reason: str = "", exit_price: float = 0.0) -> str:
    """모의 모드 자동 매도. 실제 페이퍼 보유분 기준 전량 매도, 청산거래 로그 적재 후 기록 정리(자가복구)."""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:
        return ""
    held = next((int(h["qty"]) for h in trader.balance().get("holdings", []) if h["symbol"] == symbol), 0)
    if held <= 0:  # 원장에 이미 없음 → 메타만 정리(고스트 제거)
        remove_position(symbol)
        return " → (이미 청산됨, 기록 정리)"
    try:
        res = trader.order(symbol, held, "sell", 0)
    except Exception as exc:
        return f" → 자동 매도 실패: {exc}"
    sell_fill = res.get("price") or exit_price  # 실제 매도 체결가(별도 quote 아님)
    # 청산 거래 로그(성과추적: 백테스트 대비 실제 승률·수익률)
    try:
        meta = load_positions().get(symbol, {})
        log_closed_trade(symbol, meta.get("name", symbol), num(meta.get("entry")),
                         sell_fill or num(meta.get("entry")), held, reason,
                         meta.get("ts", ""), meta.get("score"))
    except Exception:
        pass
    remove_position(symbol)
    return f" → 🧪 모의 자동 매도 체결({held}주)"


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

        paper = _is_paper()
        if stop and cur <= stop:
            action = _paper_sell(symbol, "stop", cur) if paper else "\n  매도하려면 '소미 매도 {} '라고 답해줘요.".format(name)
            alerts.append(
                f"🔴 손절 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%), 손절가 {won(stop)} 도달/이탈.{action}"
            )
        elif target and cur >= target:
            action = _paper_sell(symbol, "target", cur) if paper else "\n  '소미 매도 {}' 또는 수량 지정 매도.".format(name)
            alerts.append(
                f"🟢 익절 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%), 목표가 {won(target)} 도달.{action}"
            )
        elif _busdays_held(p.get("ts", "")) >= MAX_HOLD_DAYS:
            # 시간초과 청산 — 목표·손절 미도달이라도 보유 한도 넘으면 정리(백테스트 검증)
            action = _paper_sell(symbol, "timeout", cur) if paper else "\n  매도하려면 '소미 매도 {}' 라고 답해줘요.".format(name)
            alerts.append(
                f"⏰ 시간초과 청산 — {name}({symbol})\n"
                f"  현재 {won(cur)} (수익률 {pnl:+.1f}%), 보유 {MAX_HOLD_DAYS}거래일 경과 — 목표·손절 미도달.{action}"
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
    parser.add_argument("--daemon", action="store_true", help="데몬 모드 (장중 평일 N분 주기 자동 점검/청산)")
    args = parser.parse_args()

    if args.daemon:
        interval = int(os.getenv("SOMI_POSITION_INTERVAL", "300"))  # 기본 5분
        with ProcessLock("somi_position_monitor"):
            print(f"[{datetime.now()}] 소미 포지션 감시 데몬 시작 (장중 평일 09~16시, {interval // 60}분 주기)")
            while True:
                now = datetime.now()
                if now.weekday() < 5 and 9 <= now.hour < 16:  # 평일 장중만
                    try:
                        result = run(do_send=True)
                        print(f"[{now}] {result[:200]}")
                    except Exception as e:
                        send(f"⚠️ 소미 포지션 감시 오류: {e}")
                        print(f"[{now}] 오류: {e}")
                else:
                    print(f"[{now}] 장외 대기")
                time.sleep(interval)
        return

    print(run(args.send))


if __name__ == "__main__":
    main()
