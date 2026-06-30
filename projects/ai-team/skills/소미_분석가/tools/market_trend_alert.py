#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""시장 추세 변경 알림 — KOSPI200·KOSDAQ150 국면이 직전과 달라질 때만 텔레그램 알림.

market_regime(HMM)로 두 지수의 상승/하락/횡보 국면을 추정하고,
직전 국면을 캐시(market_regime_state.json)에 저장해 '바뀔 때만' 알린다
(같은 국면이 이어지면 반복 알림 없음).

  python market_trend_alert.py --check                 # 1회 점검 (변경 시 알림)
  python market_trend_alert.py --daemon --interval 900 # 장중 평일 15분 주기 감시
"""

from __future__ import annotations

import sys
import time
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

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from market_regime import stable_regime, regime_label, KOSPI_PROXY, KOSDAQ_PROXY  # noqa: E402

load_env(str(PROJECT_ROOT))

INDEXES = [("KOSPI", KOSPI_PROXY), ("KOSDAQ", KOSDAQ_PROXY)]


def check(do_send: bool = True) -> str:
    """두 지수의 '확정 국면'(신뢰도 게이트 통과)이 바뀔 때만 알린다.
    stable_regime(advance=True)가 확정 국면 전환을 판정·저장한다(이 데몬이 authoritative)."""
    changes = []
    for label, proxy in INDEXES:
        r = stable_regime(proxy, advance=True)
        if r.get("changed"):
            changes.append((label, r["prev"], r["regime"], r))

    if not changes:
        return "추세 변경 없음"

    lines = ["📊 시장 추세 변경"]
    for label, prev, cur, r in changes:
        lines.append(
            f"\n{label}: {regime_label(prev)} → {regime_label(cur)}\n"
            f"   최근5일 {r.get('recent5_ret', '?')}% · 신뢰도 {r.get('confidence', '?')}"
        )
    msg = "\n".join(lines)
    print(msg)
    if do_send:
        send(msg)
    return msg


def _daemon(interval: int) -> None:
    with ProcessLock("market_trend_alert"):
        print(f"[추세알림] 데몬 시작 — {interval}s 간격 (평일 장중)")
        while True:
            now = datetime.now()
            if now.weekday() < 5 and 9 <= now.hour < 16:
                try:
                    check(do_send=True)
                except Exception as exc:
                    print(f"[추세알림] 오류: {exc}")
            time.sleep(max(60, interval))


def main() -> int:
    import argparse

    p = argparse.ArgumentParser(description="시장 추세(KOSPI·KOSDAQ) 변경 알림")
    p.add_argument("--check", action="store_true", help="1회 점검 (변경 시 알림)")
    p.add_argument("--daemon", action="store_true", help="데몬 모드 (장중 평일 주기 감시)")
    p.add_argument("--interval", type=int, default=900, help="데몬 점검 간격(초)")
    p.add_argument("--no-send", action="store_true", help="텔레그램 전송 안 함(테스트)")
    args = p.parse_args()

    if args.daemon:
        _daemon(args.interval)
    else:
        print(check(do_send=not args.no_send))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
