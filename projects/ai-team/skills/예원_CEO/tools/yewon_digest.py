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
from _shared.notify import send, status_report  # noqa: E402
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


def _region_thin(name: str, reg: dict) -> bool:
    """지역 조사 데이터가 부실한지 — 지수도 웹이슈도 (아시아면 뉴스도) 비었으면 부실."""
    has = bool(reg.get("indices")) or bool(reg.get("web_issues"))
    if name == "asia":
        has = has or bool(reg.get("news")) or bool(reg.get("disclosures"))
    return not has


def ensure_news_quality() -> list[str]:
    """예원이 뉴스팀 산출물을 검수 → 부실하면 해당 조사 에이전트에 재수집 지시(재실행).
    보완한 내역을 반환. (1회만 재시도해 무한루프 방지)"""
    actions = []
    regions = {n: research.load_region(n) for n in ("us", "asia", "eu")}
    weak = [n for n, reg in regions.items() if _region_thin(n, reg)]

    for n in weak:
        if _run_research(n):
            actions.append(f"{n} 지역조사 재수집")

    # 마켓데스크 종합/영향도 점검 — 비었거나 코멘트 없으면 재집계
    mb = research.load_market_brief()
    impact = research.load_issue_impact()
    need_desk = bool(weak) or (not mb) or (not mb.get("comment")) or (not impact)
    if need_desk and _run_research("desk"):
        actions.append("마켓데스크 종합·영향도 재작성")

    return actions


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
    fixed = ensure_news_quality()
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

    qa = ("예원 검수: 부실 항목 보강함 — " + ", ".join(fixed)) if fixed else "예원 검수: 뉴스팀 자료 양호"
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
        for i in range(0, len(report), 3900):
            send(report[i:i + 3900])


if __name__ == "__main__":
    main()
