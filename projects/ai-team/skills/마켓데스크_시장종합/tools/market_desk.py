#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""마켓데스크 — 지역 브리프 종합.

행크(미국)·유나(아시아)·레온(유럽)이 저장한 region_*.json 을 모아
하나의 시장 종합 브리프(market_brief.md/json)를 만들고, LLM으로 한국 증시
관점의 코멘트를 덧붙인다. --send 시 텔레그램 전송.
"""

from __future__ import annotations

import argparse
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

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.llm import text  # noqa: E402
from _shared import research  # noqa: E402

load_env(str(PROJECT_ROOT))


def _idx_line(region: dict) -> str:
    return " / ".join(f"{k} {v['close']}" for k, v in (region.get("indices") or {}).items() if v)


def build() -> dict:
    us = research.load_region("us")
    asia = research.load_region("asia")
    eu = research.load_region("eu")
    disclosures = asia.get("disclosures", []) or []
    fx = asia.get("fx", {}) or {}

    # 종목별 이슈 영향도 (공시 기반 LLM 평가) → issue_impact.json, 소미가 소비
    if disclosures:
        by_code: dict[str, dict] = {}
        for d in disclosures:
            by_code.setdefault(d["code"], {"name": d["name"], "reports": []})["reports"].append(d["report"])
        listing = "\n".join(f"{c} {v['name']}: {'; '.join(v['reports'])}" for c, v in by_code.items())
        impact = {}
        try:
            import json as _json
            resp = text(
                "다음은 종목별 최근 공시다. 각 종목의 주가 영향도를 정수 -2(악재)~+2(호재)로 평가하고 "
                "한 줄 이유를 달아라. 반드시 JSON만 출력하라: "
                "{\"종목코드\":{\"score\":정수,\"reason\":\"...\"}}\n\n" + listing,
                json_mode=True, max_tokens=600, temperature=0.2, task="blog",
            )
            if resp:
                impact = _json.loads(resp)
        except Exception:
            impact = {}
        research.save_issue_impact(impact)

    # 데이터 요약 (LLM 입력 + 본문)
    facts = []
    if fx.get("KRW"):
        facts.append(f"USD/KRW {fx['KRW']:.1f}")
    for label, reg in (("미국", us), ("아시아", asia), ("유럽", eu)):
        line = _idx_line(reg)
        if line:
            facts.append(f"{label} 지수: {line}")
    if disclosures:
        facts.append("watchlist 공시: " + "; ".join(f"{d['name']} {d['report']}" for d in disclosures[:8]))
    fg = research.fear_greed()
    if fg.get("score") is not None:
        facts.append(f"미국 공포탐욕지수 {fg['score']} ({fg.get('rating', '')})")
    news = asia.get("news") or []
    if news:
        facts.append("증권 뉴스: " + " | ".join(news[:5]))
    facts_str = "\n".join(facts) if facts else "수집된 데이터가 적음"

    # LLM 종합 코멘트 (실패 시 폴백)
    comment = ""
    try:
        prompt = (
            "다음은 오늘의 시장 데이터다. 한국 증시 관점에서 주목할 점을 3줄 이내로 "
            "간결히 요약하라. 과장 없이 사실 위주로.\n\n" + facts_str
        )
        comment = (text(prompt, max_tokens=300, temperature=0.4, task="blog") or "").strip()
    except Exception:
        comment = ""

    now = datetime.now().strftime("%Y-%m-%d %H:%M")
    md_lines = [f"# 📋 시장 종합 브리프 — {now}", ""]
    if fx.get("KRW"):
        krw = f"USD/KRW {fx['KRW']:.1f}"
        jpy = f" · USD/JPY {fx['JPY']:.1f}" if fx.get("JPY") else ""
        md_lines += ["## 💱 환율", krw + jpy, ""]
    md_lines.append("## 📈 지수")
    for label, reg in (("미국", us), ("아시아", asia), ("유럽", eu)):
        line = _idx_line(reg)
        md_lines.append(f"- {label}: {line}" if line else f"- {label}: (조회 실패)")
    md_lines.append("")
    md_lines.append(f"## 📑 watchlist 공시 ({len(disclosures)}건)")
    if disclosures:
        for d in disclosures[:15]:
            md_lines.append(f"- {d['name']}({d['code']}) {d['report']} [{d['date']}]")
    else:
        md_lines.append("- 신규 공시 없음")
    md_lines.append("")
    if fg.get("score") is not None:
        md_lines += ["## 😨 시장 심리", f"미국 공포탐욕지수 {fg['score']} ({fg.get('rating', '')})", ""]
    if news:
        md_lines.append("## 📰 증권 뉴스")
        md_lines += [f"- {n}" for n in news[:6]]
        md_lines.append("")
    if comment:
        md_lines += ["## 🧭 데스크 코멘트", comment, ""]

    md = "\n".join(md_lines)
    research.save_market_brief(md, {
        "fx": fx,
        "indices": {"us": us.get("indices"), "asia": asia.get("indices"), "eu": eu.get("indices")},
        "disclosures": disclosures,
        "comment": comment,
    })
    return {"md": md}


def main() -> None:
    ap = argparse.ArgumentParser(description="마켓데스크 시장 종합")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--print", action="store_true")
    args = ap.parse_args()
    md = build()["md"]
    if args.print or not args.send:
        print(md)
    if args.send:
        # 텔레그램 길이 제한 대비 3900자 분할
        for i in range(0, len(md), 3900):
            send(md[i:i + 3900])


if __name__ == "__main__":
    main()
