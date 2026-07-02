#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""somi_accum_scanner.py — 조용한매집 선행 발굴 (대형주 한정, 장 마감 후 스캔).

근거(2026-07-02 백테스트, 12개월·실비용): 매집 신호(5일 기관+외인 대규모 순매수 +
당일 등락 ≤3% '아직 안 튐' + 20일선 위)는 대형주에서 문턱 65·보유 5일 기준
36건·승률 67%·PF 2.77·누적 +119.8%·MDD -20.9%(샤프 6.9).
⚠️ 중소형에선 같은 신호가 -99%(749건) 참사 — 반드시 대형주 유니버스 한정.

역할: 발굴(관찰 편입)만 한다. 매수는 기존 게이트(탐지 60↑·수급확인·관찰시간)가 결정.
신호 산식은 backtest._accum_levels를 그대로 재사용 — 검증된 정의와 드리프트 방지.

실행:
  python somi_accum_scanner.py            # 스캔 + 관심등록 + 텔레그램 보고
  python somi_accum_scanner.py --dry      # 스캔만(등록·전송 없음)
"""
from __future__ import annotations

import os
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
sys.path.insert(0, str(AI_TEAM))
sys.path.insert(0, str(_here))

from _shared.env import load_env      # noqa: E402
from _shared.notify import send       # noqa: E402

load_env()

import backtest as bt                 # noqa: E402 — 검증된 신호 산식 단일 소스
import soomgeup_history               # noqa: E402
from somi_kis_reporter import KISClient  # noqa: E402
from watchlist_manager import auto_register  # noqa: E402

# 채택 구성(백테스트 검증값): 문턱 65 — 백테스트 스윕에서 n=36·PF 2.77·MDD -21%
ACCUM_MIN_SCORE = int(os.getenv("SOMI_ACCUM_MIN_SCORE", "65"))
TOP_N = int(os.getenv("SOMI_ACCUM_TOP", "5"))


def scan() -> list[dict]:
    """대형주 유니버스(backtest.UNIVERSE — 검증에 쓴 동일 바스켓)에서 매집 신호 탐지."""
    kis = KISClient()
    found = []
    for code, name in bt.UNIVERSE.items():
        try:
            bars = bt._history(kis, code, 3)          # MA20 계산에 3개월이면 충분
            if len(bars) < 25:
                continue
            bt._SOOMGEUP = soomgeup_history.fetch(code, 2)  # 최근 ~40거래일 수급
            score, entry, stop, target = bt._accum_levels(bars, len(bars) - 1)
            if score >= ACCUM_MIN_SCORE:
                found.append({"symbol": code, "name": name, "score": score,
                              "entry": entry, "stop": stop, "target": target})
        except Exception as e:
            print(f"  [스킵] {name}({code}): {e}")
    return sorted(found, key=lambda x: x["score"], reverse=True)[:TOP_N]


def run(dry: bool = False) -> str:
    found = scan()
    ts = datetime.now().strftime("%m-%d %H:%M")
    if not found:
        msg = f"🔍 [소미 매집스캔 {ts}] 대형주 매집 신호 없음"
        print(msg)
        return msg
    lines = [f"🔍 [소미 매집스캔 {ts}] 선행 발굴 {len(found)}종목 (매수 아님·관찰 편입)"]
    for f in found:
        lines.append(f"· {f['name']}({f['symbol']}) 매집점수 {f['score']} — "
                     f"현재 {int(f['entry']):,} / 참고손절 {int(f['stop']):,}")
    if not dry:
        added = auto_register(found, min_score=ACCUM_MIN_SCORE)
        if added:
            lines.append(f"📌 관심종목 등록: {', '.join(added)}")
    msg = "\n".join(lines)
    print(msg)
    if not dry:
        send(msg)
    return msg


if __name__ == "__main__":
    run(dry="--dry" in sys.argv)
