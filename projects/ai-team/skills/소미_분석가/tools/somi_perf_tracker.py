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
from datetime import datetime, timedelta  # noqa: E402

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
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


def _account() -> dict:
    """실제 모의 계좌 평가액 — 사용자가 예수금과 대조 가능한 '진짜' 성과.
    (거래통계 누적%는 매거래 전액투입 가정이라 실계좌와 다르다 — 이 값이 진짜.)"""
    from somi_kis_reporter import KISClient, num
    import json as _json
    try:
        led = _json.loads((PROJECT_ROOT / "output" / "cache" / "somi_paper.json").read_text(encoding="utf-8"))
    except Exception:
        return {}
    import os
    start = int(os.getenv("SOMI_PAPER_CASH", "10000000"))
    cash = float(led.get("cash", start))
    kis = KISClient()
    pos_val = 0.0
    for sym, p in (led.get("positions") or {}).items():
        try:
            cur = num(kis.quote(sym).get("stck_prpr"))
        except Exception:
            cur = num(p.get("avg"))
        pos_val += (cur or 0) * int(p.get("qty") or 0)
    value = cash + pos_val
    return {"start": start, "cash": cash, "pos_val": pos_val, "value": value,
            "ret": round((value / start - 1) * 100, 2) if start else 0.0}


# 암호 같은 내부 태그 → 한글 (보고 가독성)
_SLOT_KR = {"buy": "정규매수", "buy_fast": "고속매수", "buy_close": "마감권", "collect": "관찰편입"}
_REASON_KR = {"stop": "손절", "target": "목표달성", "tp1_partial": "1차익절", "trailing": "트레일링",
              "trail": "트레일링", "early_exit": "조기청산", "timeout": "시간초과"}
_REGIME_KR = {"bear": "하락장", "bull": "상승장", "sideways": "횡보장", "unknown": "미확인"}


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


def _recent(trades: list[dict], days: int = 7) -> list[dict]:
    cut = datetime.now() - timedelta(days=days)
    out = []
    for t in trades:
        try:
            ts = datetime.strptime(str(t.get("ts_close", ""))[:16], "%Y-%m-%d %H:%M")
        except Exception:
            continue
        if ts >= cut:
            out.append(t)
    return out


def _mdd(trades: list[dict]) -> float:
    """청산 순서 기준 누적자산 최대낙폭(%)."""
    eq = peak = 1.0
    mdd = 0.0
    for t in sorted(trades, key=lambda x: str(x.get("ts_close", ""))):
        eq *= (1 + t.get("ret_pct", 0) / 100)
        peak = max(peak, eq)
        mdd = min(mdd, (eq - peak) / peak * 100)
    return round(mdd, 1)


def _group_winrate(trades: list[dict], keyfn) -> list[tuple]:
    groups: dict = {}
    for t in trades:
        k = keyfn(t)
        if k in (None, ""):
            continue
        groups.setdefault(k, []).append(t.get("ret_pct", 0))
    rows = []
    for k, rets in sorted(groups.items(), key=lambda kv: str(kv[0])):
        wins = [r for r in rets if r > 0]
        rows.append((k, len(rets), round(len(wins) / len(rets) * 100), round(sum(rets) / len(rets), 2)))
    return rows


def _avg(xs: list[float]) -> float:
    return round(sum(xs) / len(xs), 2) if xs else 0.0


def weekly_condition_analysis(all_trades: list[dict]) -> str:
    """최근 7일 — 무엇이 잘/안 됐나를 한글로 간단히(핵심 3~4줄만)."""
    recent = _recent(all_trades, 7)
    if len(recent) < MIN_SAMPLE:
        return f"📅 최근 7일: 청산 {len(recent)}건 (표본 부족 — 분석 보류)"
    st = _realized_stats(recent)
    won = str(_won(round(sum(t.get('ret_pct', 0) for t in recent) / len(recent), 2)))
    lines = [f"📅 최근 7일 · 청산 {len(recent)}건",
             f"   승률 {st['win_rate']}% · 손익비 {st['profit_factor']} · 건당평균 {st['avg_ret']:+}%"]
    # 청산사유별 — 무엇으로 벌고 잃었나(한글)
    rsn = _group_winrate(recent, lambda t: _REASON_KR.get(t.get("sell_reason") or t.get("reason"), t.get("reason")))
    if rsn:
        good = [f"{k} {a:+}%({n})" for k, n, w, a in sorted(rsn, key=lambda r: r[3], reverse=True) if a > 0]
        bad = [f"{k} {a:+}%({n})" for k, n, w, a in sorted(rsn, key=lambda r: r[3]) if a <= 0]
        if good:
            lines.append("   💚 수익원: " + ", ".join(good[:3]))
        if bad:
            lines.append("   ❤️ 손실원: " + ", ".join(bad[:3]))
    # 국면별(한글) — 지금 하락장에서 잘 되나
    reg = _group_winrate(recent, lambda t: _REGIME_KR.get(t.get("regime"), t.get("regime")))
    if reg:
        lines.append("   🌦 국면별 승률: " + ", ".join(f"{k} {w}%({n}건)" for k, n, w, a in reg))
    return "\n".join(lines)


def _won(pct: float) -> str:
    """수익률(%)을 시작자본 1천만 기준 원화 근사로 — 직관용."""
    return f"{pct:+}%"


def build() -> str:
    closed = _load_closed()
    real = _realized_stats(closed)
    acct = _account()
    lines = ["📊 소미 모의 성과 보고\n"]

    # ── 1. 실제 계좌(진짜 성과) — 사용자가 예수금과 대조 가능 ──
    if acct:
        sign = "🟢" if acct["ret"] >= 0 else "🔴"
        lines.append(f"{sign} 계좌 평가액 {acct['value']:,.0f}원  ({acct['ret']:+}%)")
        lines.append(f"   시작 {acct['start']:,.0f} → 현금 {acct['cash']:,.0f} + 주식 {acct['pos_val']:,.0f}")

    # ── 2. 거래 품질(48건 통계) — 전략이 좋은지 ──
    if real["n"]:
        def mark(v, b, tol):  # 기대치 달성 여부
            return "✅" if v >= b - tol else "🟡"
        lines.append(f"\n🎯 거래 품질 (청산 {real['n']}건)")
        lines.append(f"   승률 {real['win_rate']}% {mark(real['win_rate'], BENCH['win_rate'], 12)} (목표 {BENCH['win_rate']}%)")
        lines.append(f"   손익비 {real['profit_factor']} {mark(real['profit_factor'], BENCH['profit_factor'], 0.3)} (목표 {BENCH['profit_factor']})")
        lines.append(f"   이기면 평균 +{real['avg_win']}% / 지면 {real['avg_loss']}%")
        lines.append(f"   판정: {_verdict(real)}")
        lines.append("   ※ '거래 품질'은 개별 거래의 좋고나쁨. 실제 벌이는 위 '계좌 평가액'이 진짜.")

    # ── 3. 최근 7일 ──
    lines.append("\n" + weekly_condition_analysis(closed))
    return "\n".join(lines)


def main() -> None:
    import argparse
    ap = argparse.ArgumentParser(description="소미 모의 성과 추적(백테스트 대비)")
    ap.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = ap.parse_args()
    report = build()
    print(report)
    if args.send:
        send(report)   # 짧고 명확한 요약이라 노션 링크 대신 텔레그램 직접(가독성 우선)


if __name__ == "__main__":
    main()
