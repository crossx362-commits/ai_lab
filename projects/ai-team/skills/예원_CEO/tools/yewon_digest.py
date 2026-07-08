#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 종합 보고 — 모든 에이전트 결과를 병합해 하루 3회만 보고한다.

개별 에이전트(조사팀·소미·영숙)는 텔레그램으로 직접 보고하지 않고 결과를
파일로 남기며, 예원이 장전(morning)·장중(midday)·마감(close) 3회에 한 번씩
종합해 사장님께 한 건으로 올린다. 긴급 속보·급변동은 이 경로를 거치지 않고
즉시 보고된다(somi_price_monitor / breaking_monitor).
"""

from __future__ import annotations

import argparse
import re
import subprocess
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
from _shared.notify import publish_report, status_report  # noqa: E402
from _shared.llm import text  # noqa: E402
from _shared import research  # noqa: E402

load_env(str(PROJECT_ROOT))

SOMI_REPORTER = AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools" / "somi_kis_reporter.py"
CAL_CACHE = AI_TEAM_ROOT / "_shared" / "calendar_cache.md"

# 조사팀 재수집용 스크립트 (예원이 부실 판단 시 재실행)
RESEARCH_SCRIPTS = {
    "us": AI_TEAM_ROOT / "skills" / "행크_미국조사" / "tools" / "us_research.py",
    "asia": AI_TEAM_ROOT / "skills" / "유나_아시아조사" / "tools" / "asia_research.py",
    "eu": AI_TEAM_ROOT / "skills" / "레온_유럽조사" / "tools" / "eu_research.py",
    "desk": AI_TEAM_ROOT / "skills" / "마켓데스크_시장종합" / "tools" / "market_desk.py",
}

SLOT_LABEL = {"morning": "장전", "midday": "장중", "close": "마감"}


def _run_research(key: str, timeout: int = 240) -> bool:
    """조사 스크립트 1회 재실행 (텔레그램 전송 없이 데이터만 갱신)."""
    script = RESEARCH_SCRIPTS.get(key)
    if not script or not script.exists():
        return False
    try:
        subprocess.run([sys.executable, str(script)], cwd=str(PROJECT_ROOT),
                       capture_output=True, text=True, timeout=timeout)
        return True
    except Exception:
        return False


NEWS_REVIEW_PROMPT = Path(__file__).resolve().parent / "news_review_prompt.md"


def _region_thin(name: str, reg: dict) -> bool:
    """지역 조사 데이터가 비었는지 — 재수집 대상 지역을 고를 때 사용."""
    has = bool(reg.get("indices")) or bool(reg.get("web_issues"))
    if name == "asia":
        has = has or bool(reg.get("news")) or bool(reg.get("disclosures"))
    return not has


def _news_report_text() -> str:
    """검토 대상 보고서 = market_brief.md + 지역별 핵심 데이터 요약."""
    parts = []
    p = research.RESEARCH_DIR / "market_brief.md"
    if p.exists():
        parts.append(p.read_text(encoding="utf-8", errors="replace"))
    for n in ("us", "asia", "eu"):
        reg = research.load_region(n)
        parts.append(
            f"[{n}] 지수: {reg.get('indices') or '없음'} / "
            f"웹이슈: {(reg.get('web_issues') or '없음')[:400]} / "
            f"뉴스: {(reg.get('news') or [])[:5]}"
        )
    impact = research.load_issue_impact()
    parts.append(f"[종목 영향도] {len(impact)}건: {list(impact.items())[:5]}")
    return "\n\n".join(parts) or "(보고서 없음)"


def _parse_grade(out: str) -> str:
    """검토 출력에서 등급 추출 — '등급: 부실' 우선, 없으면 본문 내 키워드."""
    m = re.search(r"등급[^\n]{0,10}?(부실|보통|양호)", out)
    if m:
        return m.group(1)
    for g in ("부실", "보통", "양호"):
        if g in out:
            return g
    return "unknown"


def review_news() -> dict:
    """news_review_prompt.md 로 뉴스팀 보고서 LLM 품질 검토 → {grade, review}.
    검수는 품질이 중요하므로 클라우드(gpt→gemini) 우선."""
    try:
        tmpl = NEWS_REVIEW_PROMPT.read_text(encoding="utf-8")
    except Exception as exc:
        return {"grade": "unknown", "review": f"(검토 프롬프트 로드 실패: {exc})"}
    prompt = tmpl.replace("{보고서}", _news_report_text())
    # 검수는 품질이 중요 → 클라우드(구독 클로드→gemini) 직접 호출. (text()는 env상 Ollama 우선이라 '-' 빈응답 위험)
    # llm.gpt는 유료 API 퇴출 리팩터링에서 제거됨 — 잔존 참조가 다이제스트 3종을 전멸시켰다(2026-07-08)
    from _shared import llm
    out = ""
    for fn in (llm.claude_code, llm.gemini):
        try:
            out = (fn(prompt, max_tokens=500, temperature=0.2) or "").strip()
        except Exception:
            out = ""
        if len(out) >= 20:
            break
    if len(out) < 20:  # LLM 빈/쓰레기 응답 → 판정 불가(기계적 폴백이 처리)
        return {"grade": "unknown", "review": out}
    return {"grade": _parse_grade(out), "review": out}


def _news_mechanically_thin() -> bool:
    """LLM 판정 불가일 때 쓰는 기계적 부실 판정 — 지역/브리프/영향도가 비었는가."""
    if any(_region_thin(n, research.load_region(n)) for n in ("us", "asia", "eu")):
        return True
    mb = research.load_market_brief()
    return (not mb) or (not mb.get("comment")) or (not research.load_issue_impact())


def ensure_news_quality() -> dict:
    """예원이 news_review_prompt 로 검수 → '부실'(또는 판정불가+기계적 부실)이면 재수집 후 재검토.
    {grade, review, actions, grade_after} 반환. (1회만 재시도)"""
    rv = review_news()
    actions: list[str] = []
    need = rv["grade"] == "부실" or (rv["grade"] == "unknown" and _news_mechanically_thin())
    if need:
        # 비어있는 지역은 콕 집어 재조사, 마켓데스크는 항상 재집계
        for n in ("us", "asia", "eu"):
            if _region_thin(n, research.load_region(n)) and _run_research(n):
                actions.append(f"{n} 지역조사 재수집")
        if _run_research("desk"):
            actions.append("마켓데스크 종합·영향도 재작성")
        rv["grade_after"] = review_news()["grade"] if actions else rv["grade"]
    return {"grade": rv["grade"], "review": rv["review"],
            "actions": actions, "grade_after": rv.get("grade_after")}


def _somi_text() -> str:
    """소미 watchlist 종목 보고를 직접 받아온다(텔레그램 전송 없이 --print)."""
    try:
        r = subprocess.run(
            [sys.executable, str(SOMI_REPORTER), "--print"],
            capture_output=True, text=True, timeout=180,
        )
        return (r.stdout or "").strip()
    except Exception as exc:
        return f"(소미 보고 수집 실패: {exc})"


def _today_events() -> str:
    try:
        text_md = CAL_CACHE.read_text(encoding="utf-8", errors="replace")
        events = [ln.strip() for ln in text_md.splitlines() if ln.strip().startswith("-")]
        return "\n".join(events[:6]) if events else "등록된 일정 없음"
    except Exception:
        return "일정 확인 불가"


def build(slot: str) -> str:
    now = datetime.now()
    label = SLOT_LABEL.get(slot, slot)
    # 예원 검수: 뉴스팀 산출물이 부실하면 먼저 재수집 지시(보강 후 보고 작성)
    qa_info = ensure_news_quality()  # news_review_prompt.md 로 LLM 검수 → 부실하면 재수집
    # 마켓데스크 종합 브리프 전체(국가별 뉴스·거시·지정학·트렌드·전망·비트코인)를 그대로 읽어
    # 예원이 항상 체크하게 한다.
    mb_md = ""
    try:
        p = research.RESEARCH_DIR / "market_brief.md"
        mb_md = p.read_text(encoding="utf-8", errors="replace") if p.exists() else ""
    except Exception:
        mb_md = ""
    somi = _somi_text()
    events = _today_events()
    status = status_report()

    qa = f"예원 검수 등급: {qa_info['grade']}"
    if qa_info["actions"]:
        qa += " → 보강: " + ", ".join(qa_info["actions"]) + f" (재검토: {qa_info.get('grade_after')})"
    raw = (
        f"[시간대] {label}\n"
        f"[{qa}]\n\n"
        f"[시장 종합 브리프 — 국가별 뉴스·거시·지정학·트렌드·전망·비트코인]\n{mb_md or '브리프 없음'}\n\n"
        f"[소미 종목 보고]\n{somi or '없음'}\n\n"
        f"[오늘 일정]\n{events}\n\n"
        f"[에이전트 현황]\n{status}"
    )

    prompt = (
        f"너는 CEO 예원이다. 아래 자료를 사장님께 올리는 '{label} 종합 보고'로 정리하라. "
        "반드시 다음을 빠짐없이 포함: ① 국가별 증시·뉴스 ② 거시·지정학·전쟁 ③ 트렌드 이슈 "
        "④ 증시·비트코인 전망 ⑤ 보유/관심 종목 ⑥ 일정·특이사항. 각 항목 핵심만 간결히, "
        "군더더기·인사말 최소화, 사장님 호칭, 수치는 그대로 유지.\n\n" + raw
    )
    body = (text(prompt, max_tokens=1200, temperature=0.4, task="blog") or "").strip()
    if not body:
        body = raw  # LLM 실패 시 원자료라도 전달

    return f"🧭 예원 {label} 종합보고 ({now.strftime('%m-%d %H:%M')})\n\n{body}"


def main() -> None:
    ap = argparse.ArgumentParser(description="예원 종합 보고 (하루 3회)")
    ap.add_argument("--slot", choices=["morning", "midday", "close"], default="morning")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--print", action="store_true")
    args = ap.parse_args()

    report = build(args.slot)
    if args.print or not args.send:
        print(report)
    if args.send:
        # 보고서는 노션에 작성, 텔레그램엔 링크만
        title = report.split("\n", 1)[0][:200] or "예원 종합보고"
        publish_report(title, report)


if __name__ == "__main__":
    main()
