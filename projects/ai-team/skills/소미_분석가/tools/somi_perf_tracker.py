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
    """최근 7일 거래일지 조건조합 분석 — 어떤 조건/시간대/국면에서 수익·손실이 났는지."""
    recent = _recent(all_trades, 7)
    if len(recent) < MIN_SAMPLE:
        return f"\n[주간 조건분석] 최근 7일 청산 {len(recent)}건 — 표본 부족({MIN_SAMPLE} 미만), 분석 보류."
    st = _realized_stats(recent)
    lines = [f"\n[주간 조건분석] 최근 7일 청산 {len(recent)}건",
             f" 종합: 승률 {st['win_rate']}% · 손익비 {st['profit_factor']} · 평균 {st['avg_ret']}% · MDD {_mdd(recent)}%"]
    slot_rows = _group_winrate(recent, lambda t: t.get("slot"))
    if slot_rows:
        lines.append(" · 시간대별: " + ", ".join(f"{k} {w}%승({n})" for k, n, w, a in slot_rows))
    reg_rows = _group_winrate(recent, lambda t: t.get("regime"))
    if reg_rows:
        lines.append(" · 국면별: " + ", ".join(f"{k} {w}%승({n})" for k, n, w, a in reg_rows))
    rsn_rows = _group_winrate(recent, lambda t: t.get("sell_reason") or t.get("reason"))
    if rsn_rows:
        lines.append(" · 청산사유별 평균수익: " + ", ".join(f"{k} {a}%({n})" for k, n, w, a in rsn_rows))
    hi = [t.get("ret_pct", 0) for t in recent if (t.get("entry_score") or 0) >= 70]
    lo = [t.get("ret_pct", 0) for t in recent if t.get("entry_score") is not None and (t.get("entry_score") or 0) < 70]
    if hi or lo:
        lines.append(f" · 진입점수 기여: ≥70 평균 {_avg(hi)}%({len(hi)}) vs <70 평균 {_avg(lo)}%({len(lo)})")
    tp = [t for t in recent if t.get("took_profit") and t.get("max_up_pct") is not None]
    if tp:
        gap = [t.get("max_up_pct", 0) - t.get("ret_pct", 0) for t in tp]
        lines.append(f" · 익절 타이밍: 고점 대비 평균 {_avg(gap)}%p 일찍 실현(↑클수록 익절 과속)")
    sl = [t for t in recent if t.get("stopped") and t.get("max_up_pct") is not None]
    if sl:
        lines.append(f" · 손절 거래 손절 전 평균 고점 +{_avg([t.get('max_up_pct', 0) for t in sl])}% (↑크면 손절 과도하게 빨랐을 수)")
    nn = [t.get("ret_pct", 0) for t in recent if t.get("news")]
    if nn:
        lines.append(f" · 뉴스호재 동반 {len(nn)}건 평균 {_avg(nn)}%")
    lines.append(" ⚠️ 과최적화 주의: 백테스트·아웃오브샘플·모의 결과가 함께 일치할 때만 가중치 조정. 표본 적으면 신뢰 보류.")
    return "\n".join(lines)


def build() -> str:
    closed = _load_closed()
    real = _realized_stats(closed)
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
    lines.append(weekly_condition_analysis(closed))
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
