#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 대차/공매도 추세 스캐너 — 대차잔고(공매도) '줄고 있는 종목' vs '늘고 있는 종목' 발굴.

데이터(정직): KIS 리테일은 대차잔고 일별 '수량' 시계열을 주지 않는다. 대신
  ① 공매도 일별 체결수량 추이(short_sale_series) — 대차받아 공매도하므로 대차 증감의 실질 프록시
  ② 현재 대차잔고율(whol_loan_rmnd_rate) 스냅샷
을 결합해 판정한다.

  - 줄고 있는(🟢 숏커버 우호): 최근 공매도 체결이 이전 대비 감소(신규 숏 유입 둔화) + 가격 반등 → 대차/숏 압력 축소.
  - 늘고 있는(🔴 공매도 압력): 최근 공매도 체결 증가 → 대차/숏 압력 확대.
"""

from __future__ import annotations

import argparse
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
from somi_kis_reporter import KISClient, num, pick  # noqa: E402
from somi_screener import _rank_candidates  # noqa: E402
from watchlist_manager import load_watchlist  # noqa: E402

load_env(str(PROJECT_ROOT))

MIN_RATIO = 0.3   # 공매도 비중(%) 하한 — 공매도 미미한 종목은 추세 무의미로 제외
MIN_DELTA = 15.0  # 추세 판정 문턱(%) — 최근3일 평균 공매도가 이전3일 대비 ±15% 넘게 변해야 추세로 인정


def _series_metrics(rows: list[dict]) -> dict | None:
    """공매도 일별 시계열 → 추세 지표. rows: KIS output2(일자 내림차순 가정, 재정렬)."""
    clean = []
    for r in rows:
        d = str(r.get("stck_bsop_date", "")).strip()
        q = num(r.get("ssts_cntg_qty"))          # 일별 공매도 체결수량
        if not d:
            continue
        clean.append((d, q, num(r.get("ssts_vol_rlim")), num(r.get("stck_clpr"))))
    if len(clean) < 6:
        return None
    clean.sort(key=lambda x: x[0])               # 날짜 오름차순(과거→현재)
    qtys = [c[1] for c in clean]
    ratios = [c[2] for c in clean if c[2]]
    closes = [c[3] for c in clean if c[3]]
    recent3 = sum(qtys[-3:]) / 3
    prior3 = sum(qtys[-6:-3]) / 3
    if prior3 <= 0:
        delta = 100.0 if recent3 > 0 else 0.0    # 이전 0 → 신규 공매도 등장
    else:
        delta = (recent3 - prior3) / prior3 * 100
    avg_ratio = (sum(ratios[-5:]) / len(ratios[-5:])) if ratios else 0.0
    price_chg = ((closes[-1] - closes[-4]) / closes[-4] * 100) if len(closes) >= 4 and closes[-4] else 0.0
    return {"delta": round(delta, 1), "avg_ratio": round(avg_ratio, 2),
            "price_chg": round(price_chg, 1), "recent3": int(recent3), "days": len(clean)}


def _universe(limit: int) -> list[tuple[str, str]]:
    """거래대금 상위(공매도 활발) + watchlist 병합."""
    kis = KISClient()
    uni: dict[str, str] = {}
    for code, name, _chg in _rank_candidates(kis, "3", limit):   # (code, name, chg)
        uni[code] = name
    for code, name in load_watchlist().items():
        uni.setdefault(str(code), name)
    return list(uni.items())


def scan(limit: int = 40) -> tuple[list[dict], list[dict]]:
    kis = KISClient()
    rows = []
    for code, name in _universe(limit):
        try:
            m = _series_metrics(kis.short_sale_series(code, 25))
        except Exception:
            m = None
        if not m or m["avg_ratio"] < MIN_RATIO:
            continue
        rows.append({"code": code, "name": name, **m})
        time.sleep(0.15)
    decreasing = sorted([r for r in rows if r["delta"] <= -MIN_DELTA], key=lambda r: r["delta"])
    increasing = sorted([r for r in rows if r["delta"] >= MIN_DELTA], key=lambda r: r["delta"], reverse=True)
    return decreasing, increasing


def _loan_rate(kis: KISClient, code: str) -> str:
    try:
        return pick(kis.quote(code), "whol_loan_rmnd_rate") or "-"
    except Exception:
        return "-"


def _line(kis: KISClient, r: dict) -> str:
    lr = _loan_rate(kis, r["code"])
    return (f"- {r['name']}({r['code']}) 공매도 {r['delta']:+.0f}% · 비중 {r['avg_ratio']:.1f}% · "
            f"대차잔고율 {lr}% · 주가 {r['price_chg']:+.1f}%")


def format_report(decreasing: list[dict], increasing: list[dict], top_n: int = 7) -> str:
    kis = KISClient()
    ts = datetime.now().strftime("%Y-%m-%d %H:%M")
    lines = [f"[대차/공매도 추세 스캔] {ts}",
             "· 기준: 최근3일 공매도 체결 vs 이전3일(±15% 추세), 공매도 비중 0.3%↑",
             "· 대차잔고 일별 수량은 KIS 미제공 → 공매도 일별 추이로 대차 증감 프록시\n"]
    lines.append(f"🟢 대차/공매도 '줄고 있는' 종목 (숏커버 우호) — {len(decreasing)}건")
    lines += ([_line(kis, r) for r in decreasing[:top_n]] or ["  (해당 없음)"])
    lines.append(f"\n🔴 대차/공매도 '늘고 있는' 종목 (공매도 압력) — {len(increasing)}건")
    lines += ([_line(kis, r) for r in increasing[:top_n]] or ["  (해당 없음)"])
    return "\n".join(lines)


def main() -> None:
    p = argparse.ArgumentParser(description="소미 대차/공매도 추세 스캐너")
    p.add_argument("--limit", type=int, default=40, help="거래대금 상위 스캔 종목 수")
    p.add_argument("--top", type=int, default=7, help="각 리스트 최대 표시")
    p.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = p.parse_args()
    dec, inc = scan(args.limit)
    report = format_report(dec, inc, args.top)
    print(report)
    if args.send:
        send(report)


if __name__ == "__main__":
    main()
