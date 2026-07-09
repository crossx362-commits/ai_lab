#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 아침 브리핑 — 오늘 일정·에이전트 현황·예정 루틴을 모아 노션에 작성.

사용:
    python morning_brief.py           # 브리핑 생성 후 노션 작성(텔레그램 X)
    python morning_brief.py --dry     # 작성 없이 콘솔 출력만
"""

from __future__ import annotations

import argparse
import json
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
from _shared.notify import status_report  # noqa: E402
from _shared.telegram import publish_report  # noqa: E402
from _shared import research  # noqa: E402

CAL_CACHE = AI_TEAM_ROOT / "_shared" / "calendar_cache.md"
SCHEDULES = SCRIPT_DIR / "schedules.json"

_WEEKDAY_KO = ["월", "화", "수", "목", "금", "토", "일"]


def _today_events() -> str:
    """calendar_cache.md에서 일정 라인(- 로 시작)을 추출."""
    try:
        text = CAL_CACHE.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return "일정 캐시를 읽지 못했어요."
    events = [ln.strip() for ln in text.splitlines() if ln.strip().startswith("-")]
    if not events:
        return "오늘 등록된 일정은 없어요."
    return "\n".join(events[:8])


def _cron_runs_today(cron: str, now: datetime) -> str | None:
    """cron(분 시 일 월 요일)이 오늘 실행되면 'HH:MM' 반환, 아니면 None.
    croniter 없이 분/시/요일만 가볍게 해석(* 와 콤마/범위 지원)."""
    try:
        minute, hour, _dom, _mon, dow = cron.split()
    except ValueError:
        return None

    def _match(field: str, value: int, names_max: int) -> bool:
        if field == "*":
            return True
        for part in field.split(","):
            if "-" in part:
                lo, hi = part.split("-")
                if int(lo) <= value <= int(hi):
                    return True
            elif part.isdigit() and int(part) % (names_max + 1) == value:
                return True
            elif part.isdigit() and int(part) == value:
                return True
        return False

    # cron 요일: 0=일~6=토. datetime.weekday(): 0=월~6=일 → 변환
    cron_dow = (now.weekday() + 1) % 7
    if not _match(dow, cron_dow, 6):
        return None
    if hour == "*" or minute == "*":
        return "수시"
    try:
        return f"{int(hour):02d}:{int(minute):02d}"
    except ValueError:
        return None


def _today_routines(now: datetime) -> str:
    try:
        data = json.loads(SCHEDULES.read_text(encoding="utf-8", errors="replace"))
    except OSError:
        return "스케줄 정보를 읽지 못했어요."
    rows: list[tuple[str, str, str]] = []
    for s in data.get("schedules", []):
        if not s.get("enabled", True):
            continue
        t = _cron_runs_today(s.get("cron", ""), now)
        if t:
            rows.append((t, s.get("agent", "?"), s.get("task", s.get("id", ""))))
    if not rows:
        return "오늘 예정된 자동 루틴은 없어요."
    rows.sort(key=lambda r: r[0])
    return "\n".join(f"- {t} · {agent} · {task}" for t, agent, task in rows)


def _market_section() -> str:
    """마켓데스크 종합 브리프(환율 + 데스크 코멘트)를 아침 브리핑에 요약."""
    mb = research.load_market_brief()
    if not mb:
        return ""
    lines = ["📋 오늘의 시장"]
    fx = mb.get("fx", {}) or {}
    if fx.get("KRW"):
        krw = f"USD/KRW {fx['KRW']:.1f}"
        jpy = f" · USD/JPY {fx['JPY']:.1f}" if fx.get("JPY") else ""
        lines.append(krw + jpy)
    comment = (mb.get("comment") or "").strip()
    if comment:
        lines.append(comment)
    return ("\n".join(lines) + "\n\n") if len(lines) > 1 else ""


def build_brief(now: datetime | None = None) -> str:
    now = now or datetime.now()
    head = f"☀️ 사장님, 좋은 아침이에요! {now.strftime('%Y-%m-%d')} ({_WEEKDAY_KO[now.weekday()]})"
    return (
        f"{head}\n\n"
        f"📅 오늘 일정\n{_today_events()}\n\n"
        f"⏰ 오늘 예정 루틴\n{_today_routines(now)}\n\n"
        f"{_market_section()}"
        f"🤖 {status_report()}"
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="영숙 아침 브리핑")
    parser.add_argument("--dry", action="store_true", help="전송 없이 출력만")
    args = parser.parse_args()

    load_env()
    brief = build_brief()
    print(brief)
    if not args.dry:
        publish_report("영숙 아침 브리프", brief)  # 텔레그램 X → 노션 작성


if __name__ == "__main__":
    main()
