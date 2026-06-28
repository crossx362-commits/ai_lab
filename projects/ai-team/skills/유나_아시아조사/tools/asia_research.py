#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""유나 — 아시아 시장 조사관.

watchlist 종목의 DART 공시 + 환율(USD/KRW·JPY·CNY) + 아시아 지수를 수집해
output/research/region_asia.json 에 저장한다. --send 시 텔레그램 요약 전송.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared import research  # noqa: E402

load_env(str(PROJECT_ROOT))

INDEX_SYMBOLS = {"코스피": "^KS11", "상해종합": "000001.SS", "닛케이": "^N225"}  # 베트남VN: Yahoo 미지원으로 제외


def collect() -> dict:
    wl = research.load_watchlist()
    codes = set(wl.keys())
    payload = {
        "watchlist": wl,
        "disclosures": research.dart_recent(codes, days=2),
        "fx": research.fx("KRW", "JPY", "CNY", "VND"),
        "indices": research.indices(INDEX_SYMBOLS),
        "news": research.news_rss("https://www.hankyung.com/feed/finance", 8),
        "web_issues": research.web_brief(
            "오늘 아시아 증시 주요 뉴스·핫이슈를 국가별로 정리하라: 한국·중국·일본·베트남. "
            "각 국가 1~2줄, 지수 등락과 핵심 이슈(종목·정책·수급) 위주로 간결히."
        ),
    }
    research.save_region("asia", payload)
    return payload


def brief_text(p: dict) -> str:
    lines = ["🌏 아시아 시장 브리프"]
    fx = p.get("fx", {})
    fx_bits = []
    if fx.get("KRW"):
        fx_bits.append(f"USD/KRW {fx['KRW']:.1f}")
    if fx.get("JPY"):
        fx_bits.append(f"USD/JPY {fx['JPY']:.1f}")
    if fx.get("CNY"):
        fx_bits.append(f"USD/CNY {fx['CNY']:.2f}")
    if fx_bits:
        lines.append("💱 " + " · ".join(fx_bits))
    idx = [f"{k} {v['close']}" for k, v in (p.get("indices") or {}).items() if v]
    if idx:
        lines.append("📈 " + " / ".join(idx))
    d = p.get("disclosures") or []
    if d:
        lines.append(f"\n📑 watchlist 공시 {len(d)}건")
        for x in d[:10]:
            lines.append(f"- {x['name']}({x['code']}) {x['report']} [{x['date']}]")
    else:
        lines.append("\n📑 watchlist 신규 공시 없음")
    news = p.get("news") or []
    if news:
        lines.append("\n📰 증권 뉴스 헤드라인")
        for n in news[:5]:
            lines.append(f"- {n}")
    web = p.get("web_issues")
    if web:
        lines.append("\n🔎 웹 핫이슈\n" + web)
    return "\n".join(lines)


def main() -> None:
    ap = argparse.ArgumentParser(description="유나 아시아 시장 조사")
    ap.add_argument("--send", action="store_true", help="텔레그램 전송")
    ap.add_argument("--print", action="store_true", help="콘솔 출력")
    args = ap.parse_args()

    payload = collect()
    txt = brief_text(payload)
    if args.print or not args.send:
        print(txt)
    if args.send:
        send(txt)


if __name__ == "__main__":
    main()
