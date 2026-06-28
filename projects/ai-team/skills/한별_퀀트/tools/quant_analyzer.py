#!/usr/bin/env python3
"""quant_analyzer.py — 한별(퀀트/성과분석가): 수익률 개선 피드백 루프.

역할: 소미가 쏜 신호·체결 결과를 기록·복기하고, 데이터로 소미의 점수/손절/목표를
튜닝하며, 리스크 기반 포지션 사이징을 제안한다.

데이터: output/cache/trade_journal.json (체결부가 append). 자기완결적 FIFO 손익 계산.

명령:
  python quant_analyzer.py performance     # 승률·손익비·MDD 성과 복기
  python quant_analyzer.py tune            # 점수버킷 분석 → 소미 튜닝 추천(+somi_tuning.json)
  python quant_analyzer.py size --price 50000 --stop 47000   # 리스크 기반 수량 제안
  python quant_analyzer.py add --action buy --symbol 012450 --name 한화에어로 --qty 20 --price 50000 --score 8
"""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

JOURNAL = PROJECT_ROOT / "output" / "cache" / "trade_journal.json"
TUNING = PROJECT_ROOT / "output" / "cache" / "somi_tuning.json"

# 리스크 파라미터(환경변수 조절)
ACCOUNT_EQUITY = float(os.getenv("SOMI_ACCOUNT_EQUITY", "10000000"))   # 운용 자본
RISK_PER_TRADE = float(os.getenv("SOMI_RISK_PER_TRADE", "0.01"))      # 1거래당 감수 손실 비율(1%)


# ── 거래일지 입출력 ──────────────────────────────────────────────
def _load() -> list[dict]:
    try:
        return json.loads(JOURNAL.read_text(encoding="utf-8"))
    except Exception:
        return []


def _save(events: list[dict]) -> None:
    JOURNAL.parent.mkdir(parents=True, exist_ok=True)
    JOURNAL.write_text(json.dumps(events[-1000:], ensure_ascii=False, indent=2), encoding="utf-8")


def append(action: str, symbol: str, name: str = "", qty: int = 0, price: float = 0,
           score=None, entry=None, stop=None, target=None) -> None:
    """체결부가 호출 — 매수/매도 1건 기록."""
    ev = _load()
    ev.append({
        "ts": datetime.now().isoformat(timespec="seconds"),
        "action": action, "symbol": str(symbol), "name": name,
        "qty": int(qty or 0), "price": float(price or 0),
        "score": score, "entry": entry, "stop": stop, "target": target,
    })
    _save(ev)


# ── FIFO 손익 매칭 ───────────────────────────────────────────────
def _realized_trades() -> tuple[list[dict], dict]:
    """매수/매도를 FIFO로 짝지어 실현 거래 리스트 + 미청산 보유 반환."""
    open_lots: dict[str, list[dict]] = {}    # symbol -> [{qty, price, score, ts}]
    trades: list[dict] = []
    for e in _load():
        sym = e["symbol"]
        if e["action"] == "buy":
            open_lots.setdefault(sym, []).append(
                {"qty": e["qty"], "price": e["price"], "score": e.get("score"), "ts": e["ts"], "name": e.get("name", "")})
        elif e["action"] == "sell":
            remaining = e["qty"]
            sell_px = e["price"]
            lots = open_lots.get(sym, [])
            while remaining > 0 and lots:
                lot = lots[0]
                matched = min(remaining, lot["qty"])
                if sell_px and lot["price"]:
                    pnl = (sell_px - lot["price"]) * matched
                    pnl_pct = (sell_px - lot["price"]) / lot["price"] * 100
                    trades.append({
                        "symbol": sym, "name": e.get("name") or lot.get("name", ""),
                        "qty": matched, "buy": lot["price"], "sell": sell_px,
                        "pnl": pnl, "pnl_pct": pnl_pct, "score": lot.get("score"),
                        "buy_ts": lot["ts"], "sell_ts": e["ts"],
                    })
                lot["qty"] -= matched
                remaining -= matched
                if lot["qty"] <= 0:
                    lots.pop(0)
    open_pos = {s: lots for s, lots in open_lots.items() if lots}
    return trades, open_pos


# ── 성과 복기 ────────────────────────────────────────────────────
def performance() -> str:
    trades, open_pos = _realized_trades()
    if not trades:
        n_open = sum(len(v) for v in open_pos.values())
        return f"📊 [한별] 실현 거래 없음 (미청산 보유 {n_open}건). 매도 체결이 쌓이면 복기가 시작됩니다."

    wins = [t for t in trades if t["pnl"] > 0]
    losses = [t for t in trades if t["pnl"] <= 0]
    total_pnl = sum(t["pnl"] for t in trades)
    win_rate = len(wins) / len(trades) * 100
    avg_win = sum(t["pnl_pct"] for t in wins) / len(wins) if wins else 0
    avg_loss = sum(t["pnl_pct"] for t in losses) / len(losses) if losses else 0
    gross_win = sum(t["pnl"] for t in wins)
    gross_loss = -sum(t["pnl"] for t in losses)
    pf = (gross_win / gross_loss) if gross_loss > 0 else float("inf")

    # 누적 손익 곡선 기반 MDD(%)
    eq, peak, mdd = 0.0, 0.0, 0.0
    for t in trades:
        eq += t["pnl"]
        peak = max(peak, eq)
        if peak > 0:
            mdd = min(mdd, (eq - peak) / peak * 100)

    lines = [
        "📊 [한별] 거래 성과 복기",
        f"  실현 거래 {len(trades)}건 | 승 {len(wins)} / 패 {len(losses)} | 승률 {win_rate:.1f}%",
        f"  총 실현손익 {total_pnl:,.0f}원 | 손익비(PF) {pf:.2f}",
        f"  평균 수익 +{avg_win:.2f}% / 평균 손실 {avg_loss:.2f}%",
        f"  최대 낙폭(MDD) {mdd:.1f}%",
    ]
    best = sorted(trades, key=lambda t: t["pnl_pct"], reverse=True)[:3]
    worst = sorted(trades, key=lambda t: t["pnl_pct"])[:3]
    lines.append("  🏆 베스트: " + ", ".join(f"{t['name'] or t['symbol']} {t['pnl_pct']:+.1f}%" for t in best))
    lines.append("  ⚠️ 워스트: " + ", ".join(f"{t['name'] or t['symbol']} {t['pnl_pct']:+.1f}%" for t in worst))
    n_open = sum(len(v) for v in open_pos.values())
    if n_open:
        lines.append(f"  📦 미청산 보유 {n_open}건")
    return "\n".join(lines)


# ── 튜닝 추천 (점수 버킷 분석) ───────────────────────────────────
def tune() -> str:
    trades, _ = _realized_trades()
    scored = [t for t in trades if isinstance(t.get("score"), (int, float))]
    if len(scored) < 5:
        return (f"🔧 [한별] 튜닝 보류 — 점수 보유 실현 거래 {len(scored)}건(최소 5건 필요).\n"
                "  표본이 쌓이면 점수 구간별 승률로 소미 임계점을 자동 추천합니다.")

    buckets: dict[int, list[dict]] = {}
    for t in scored:
        b = int(t["score"])
        buckets.setdefault(b, []).append(t)

    lines = ["🔧 [한별] 소미 신호 튜닝 분석 (점수 구간별 성과)"]
    bucket_stats = []
    for b in sorted(buckets):
        ts = buckets[b]
        wr = sum(1 for t in ts if t["pnl"] > 0) / len(ts) * 100
        avg = sum(t["pnl_pct"] for t in ts) / len(ts)
        bucket_stats.append((b, len(ts), wr, avg))
        lines.append(f"  점수 {b}: {len(ts)}건 | 승률 {wr:.0f}% | 평균 {avg:+.2f}%")

    # 추천 임계점: 표본 3건 이상이며 승률 55% 이상인 최저 점수
    good = [b for (b, n, wr, avg) in bucket_stats if n >= 3 and wr >= 55]
    rec_min_score = min(good) if good else max(b for b, *_ in bucket_stats)

    # 손절/목표 배수 추천: 평균 손실 폭으로 손절, 평균 수익 폭으로 목표 보정
    wins = [t["pnl_pct"] for t in scored if t["pnl"] > 0]
    losses = [abs(t["pnl_pct"]) for t in scored if t["pnl"] <= 0]
    avg_win = sum(wins) / len(wins) if wins else 6.0
    avg_loss = sum(losses) / len(losses) if losses else 3.0

    tuning = {
        "ts": datetime.now().isoformat(timespec="seconds"),
        "recommend_min_score": rec_min_score,
        "suggest_stop_pct": round(min(max(avg_loss, 2.0), 8.0), 2),
        "suggest_target_pct": round(min(max(avg_win, 3.0), 20.0), 2),
        "sample": len(scored),
    }
    TUNING.parent.mkdir(parents=True, exist_ok=True)
    TUNING.write_text(json.dumps(tuning, ensure_ascii=False, indent=2), encoding="utf-8")

    lines += [
        "",
        f"  👉 추천 최소점수: {rec_min_score} 이상만 신호 (SOMI_SIGNAL_MIN_SCORE)",
        f"  👉 추천 손절 {tuning['suggest_stop_pct']}% / 목표 {tuning['suggest_target_pct']}%",
        f"  → somi_tuning.json 저장. 소미가 다음 스캔부터 참고할 수 있습니다.",
    ]
    return "\n".join(lines)


# ── 리스크 기반 포지션 사이징 ────────────────────────────────────
def size(price: float, stop: float) -> str:
    if price <= 0 or stop <= 0 or stop >= price:
        return "⚠️ [한별] 진입가 > 손절가 필요 (예: price 50000 stop 47000)."
    risk_per_share = price - stop
    risk_budget = ACCOUNT_EQUITY * RISK_PER_TRADE
    qty = int(risk_budget // risk_per_share)
    cost = qty * price
    return (
        "📐 [한별] 리스크 기반 포지션 사이징\n"
        f"  자본 {ACCOUNT_EQUITY:,.0f}원 · 1거래 감수손실 {RISK_PER_TRADE*100:.1f}%({risk_budget:,.0f}원)\n"
        f"  진입 {price:,.0f} / 손절 {stop:,.0f} → 주당 위험 {risk_per_share:,.0f}원\n"
        f"  👉 권장 수량 {qty:,}주 (투입 {cost:,.0f}원, 손절 시 약 {qty*risk_per_share:,.0f}원 손실)"
    )


def main() -> None:
    import argparse
    p = argparse.ArgumentParser(description="한별 퀀트/성과분석")
    sub = p.add_subparsers(dest="cmd")
    sub.add_parser("performance")
    sub.add_parser("tune")
    ps = sub.add_parser("size"); ps.add_argument("--price", type=float, required=True); ps.add_argument("--stop", type=float, required=True)
    pa = sub.add_parser("add")
    pa.add_argument("--action", required=True, choices=["buy", "sell"])
    pa.add_argument("--symbol", required=True); pa.add_argument("--name", default="")
    pa.add_argument("--qty", type=int, default=0); pa.add_argument("--price", type=float, default=0)
    pa.add_argument("--score", type=float, default=None)
    pa.add_argument("--entry", type=float, default=None); pa.add_argument("--stop", type=float, default=None); pa.add_argument("--target", type=float, default=None)
    args = p.parse_args()

    if args.cmd == "performance":
        print(performance())
    elif args.cmd == "tune":
        print(tune())
    elif args.cmd == "size":
        print(size(args.price, args.stop))
    elif args.cmd == "add":
        append(args.action, args.symbol, args.name, args.qty, args.price,
               args.score, args.entry, args.stop, args.target)
        print("기록 완료")
    else:
        print(performance())


if __name__ == "__main__":
    main()
