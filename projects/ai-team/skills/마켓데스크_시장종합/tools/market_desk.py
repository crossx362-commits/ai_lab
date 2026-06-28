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


def _short(s: str, n: int = 120) -> str:
    s = (s or "").strip().replace("\n", " ")
    return (s[:n] + "…") if len(s) > n else s


def _somi_candidates(limit: int = 20) -> list[tuple[str, str]]:
    """소미 매매 후보(거래대금 상위) 종목 — 뉴스 평가 대상에 포함시키기 위해."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from somi_screener import get_candidates
        from somi_kis_reporter import KISClient
        return get_candidates(KISClient(), limit)
    except Exception as exc:
        print(f"[market_desk] 소미 후보 조회 실패: {exc}", file=sys.stderr)
        return []


def _build_issue_impact(disclosures: list, us: dict, asia: dict, eu: dict) -> None:
    """소미 후보 ∪ 공시 종목에 대해 시장 맥락 기반 호재/악재(-2~+2) 평가 → issue_impact.json.
    실제 뉴스 근거가 있는(0이 아닌) 종목만 저장한다(0=뉴스없음 취급)."""
    import json as _json

    # 평가 대상 종목 (코드→이름)
    targets: dict[str, str] = {}
    for code, name in _somi_candidates(20):
        targets[code] = name
    by_code: dict[str, dict] = {}
    for d in disclosures or []:
        by_code.setdefault(d["code"], {"name": d["name"], "reports": []})["reports"].append(d["report"])
        targets[d["code"]] = d["name"]
    if not targets:
        return

    # LLM에 줄 시장 맥락 (지역 웹이슈 + 증권뉴스 + 공시 상세 + 심리)
    ctx = []
    for label, reg in (("미국", us), ("아시아/한국", asia), ("유럽", eu)):
        w = reg.get("web_issues")
        if w:
            ctx.append(f"[{label} 웹이슈] {w}")
    if asia.get("news"):
        ctx.append("[증권뉴스] " + " | ".join(asia["news"][:8]))
    if by_code:
        ctx.append("[공시] " + "; ".join(f"{v['name']}: {', '.join(v['reports'])}" for v in by_code.values()))
    fg = research.fear_greed()
    if fg.get("score") is not None:
        ctx.append(f"[공포탐욕] {fg['score']}({fg.get('rating', '')})")
    ctx_str = "\n".join(ctx) or "특이 뉴스 없음"
    target_list = "\n".join(f"{c} {n}" for c, n in targets.items())

    prompt = (
        "아래 '시장 맥락'(뉴스·공시·웹이슈)을 근거로, '평가 대상 종목' 각각의 향후 주가 영향도를 "
        "정수 -2(강한 악재)~+2(강한 호재)로 평가하라. 해당 종목에 대한 구체적 뉴스/공시 근거가 없으면 0으로 둬라. "
        "반드시 JSON만 출력: {\"종목코드\":{\"score\":정수,\"reason\":\"한 줄(근거 없으면 빈 문자열)\"}}\n\n"
        f"[시장 맥락]\n{ctx_str}\n\n[평가 대상 종목]\n{target_list}"
    )
    impact = {}
    try:
        resp = text(prompt, json_mode=True, max_tokens=900, temperature=0.2, task="blog")
        if resp:
            raw = _json.loads(resp)
            # 실제 영향(0이 아닌) 종목만 저장 — 0/근거없음은 '뉴스없음'으로 둠
            for code, v in (raw.items() if isinstance(raw, dict) else []):
                if isinstance(v, dict) and isinstance(v.get("score"), int) and v["score"] != 0:
                    impact[str(code)] = {"score": v["score"], "reason": v.get("reason", "")}
    except Exception as exc:
        print(f"[market_desk] issue_impact 평가 실패: {exc}", file=sys.stderr)
    research.save_issue_impact(impact)
    print(f"[market_desk] issue_impact 저장: {len(impact)}종목 (대상 {len(targets)})")


def build() -> dict:
    us = research.load_region("us")
    asia = research.load_region("asia")
    eu = research.load_region("eu")
    disclosures = asia.get("disclosures", []) or []
    fx = asia.get("fx", {}) or {}

    # 종목별 이슈 영향도 → issue_impact.json (소미가 소비).
    # 평가 대상 = 소미 매매 후보(거래대금 상위) ∪ 공시 종목 — 소미가 실제 보는 종목에 뉴스가 붙도록 맞춤.
    _build_issue_impact(disclosures, us, asia, eu)

    # 데이터 요약 (LLM 입력 + 본문)
    facts = []
    if fx.get("KRW"):
        facts.append(f"USD/KRW {fx['KRW']:.1f}")
    for label, reg in (("미국", us), ("아시아", asia), ("유럽", eu)):
        line = _idx_line(reg)
        if line:
            facts.append(f"{label} 지수: {line}")
        w = reg.get("web_issues")
        if w:
            facts.append(f"{label} 웹이슈: {w}")
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

    # 향후 전망 + 비트코인 전망 (웹검색 1회로 묶음, 토큰 절약)
    outlook = research.web_brief(
        "다음 두 가지를 각각 2줄 이내로 간결히 정리하라. 근거 위주, 단정적 표현은 피하라: "
        "(1) 향후 1~2주 한국·미국 증시 단기 전망 (2) 비트코인 가격 동향과 단기 전망.",
        max_tokens=500,
    )

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
    web_sections = []
    for label, reg in (("🇺🇸 미국", us), ("🌏 아시아", asia), ("🇪🇺 유럽", eu)):
        w = reg.get("web_issues")
        if w:
            web_sections.append(f"**{label}**\n{w}")
    if web_sections:
        md_lines.append("## 🔎 지역 웹이슈")
        md_lines += web_sections
        md_lines.append("")
    if fg.get("score") is not None:
        md_lines += ["## 😨 시장 심리", f"미국 공포탐욕지수 {fg['score']} ({fg.get('rating', '')})", ""]
    if news:
        md_lines.append("## 📰 증권 뉴스")
        md_lines += [f"- {n}" for n in news[:6]]
        md_lines.append("")
    if comment:
        md_lines += ["## 🧭 데스크 코멘트", comment, ""]
    if outlook:
        md_lines += ["## 📈 전망 · 비트코인", outlook, ""]

    md = "\n".join(md_lines)
    research.save_market_brief(md, {
        "fx": fx,
        "indices": {"us": us.get("indices"), "asia": asia.get("indices"), "eu": eu.get("indices")},
        "disclosures": disclosures,
        "comment": comment,
    })

    # 노션에 간결 기록 (제목 + 핵심 불릿 몇 줄)
    bullets = []
    if fx.get("KRW"):
        bullets.append(f"💱 USD/KRW {fx['KRW']:.1f}")
    for label, reg in (("🇺🇸 미국", us), ("🌏 한국", asia), ("🇪🇺 유럽", eu)):
        w = reg.get("web_issues")
        if w:
            bullets.append(f"{label} {_short(w, 110)}")
    if disclosures:
        bullets.append("📑 공시 " + str(len(disclosures)) + "건: "
                       + ", ".join(f"{d['name']} {d['report']}" for d in disclosures[:3]))
    news = asia.get("news") or []
    if news:
        bullets.append("📰 뉴스: " + " · ".join(news[:2]))
    if comment:
        bullets.append("🧭 " + _short(comment, 160))
    if outlook:
        bullets.append("📈 전망·BTC: " + _short(outlook, 170))
    research.notion_page(f"📊 시장 브리프 {now}", bullets)

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
