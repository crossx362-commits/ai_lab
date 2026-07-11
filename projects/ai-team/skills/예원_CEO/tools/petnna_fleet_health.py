#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 펫나 함대 신선도 감사 (Hermes 크론 호환 모니터).

옛 harness_monitor는 "상시 데몬 프로세스 생존"으로 건강을 판정했으나, 펫나 6개
에이전트가 Hermes 크론(정시 실행, 상시 프로세스 없음)으로 이관되면서 그 방식은
항상 down 오탐을 낸다. 이 모니터는 CLAUDE.md 가드레일("프로세스 생존 ≠ 일하는 중,
산출물 신선도로 판정")대로 각 에이전트의 최신 산출물 mtime을 신선도 임계와 비교한다.

- 정상: 최신 산출물이 임계 이내 → up
- 지연: 임계 초과 → stale (죽은 잡 의심)
- stale가 하나라도 있으면 텔레그램 경보(없으면 조용 — 정보성 알림 스팸 방지).
"""

from __future__ import annotations

import argparse
import os
import re
import sys
import time
import urllib.error
import urllib.request
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
from _shared.telegram import send  # noqa: E402

load_env(str(PROJECT_ROOT))

QA = PROJECT_ROOT / "output" / "qa" / "petnna"
HOUR = 3600.0

# (라벨, 산출물 글롭, 신선도 임계 시간). 일간 잡=30h(주말·공휴일 여유), 주간 잡=8일.
AGENTS = [
    ("백호(백엔드)", QA / "backend", "report_*.md", 30 * HOUR),
    ("봄이(QA)",     QA,             "report_*.md", 30 * HOUR),
    ("테오(테스트)", QA / "tests",   "results.json", 30 * HOUR),
    ("수리(개발)",   QA / "dev",     "loop_*.md",   30 * HOUR),
    ("미오(디자인)", QA / "design",  "review_*.md", 8 * 24 * HOUR),
    ("나무(기획)",   QA / "product", "plan_*.md",   8 * 24 * HOUR),
]


MIGRATIONS = PROJECT_ROOT / "projects" / "petnna" / "migrations"
_SUPA_SQL_LINK = "https://supabase.com/dashboard/project/{ref}/sql/new"


def _declared_tables() -> dict[str, str]:
    """migrations/*.sql이 선언하는 테이블 → 선언 파일명 매핑(CREATE TABLE만)."""
    out: dict[str, str] = {}
    if not MIGRATIONS.exists():
        return out
    pat = re.compile(
        r"CREATE\s+TABLE(?:\s+IF\s+NOT\s+EXISTS)?\s+(?:public\.)?([a-zA-Z_][a-zA-Z0-9_]*)", re.I)
    for f in sorted(MIGRATIONS.glob("*.sql")):
        for name in pat.findall(f.read_text(encoding="utf-8", errors="replace")):
            out.setdefault(name, f.name)
    return out


def _table_exists(url: str, key: str, table: str) -> bool | None:
    """라이브 Supabase에 테이블이 있는가. True/False, 판정 불가 시 None.
    RLS로 anon select가 막혀도(401/403) 테이블은 존재하는 것으로 본다.
    PGRST205(schema cache에 없음)만 '없음'으로 판정한다."""
    req = urllib.request.Request(
        f"{url}/rest/v1/{table}?select=*&limit=1",
        headers={"apikey": key, "Authorization": f"Bearer {key}"})
    try:
        with urllib.request.urlopen(req, timeout=12):
            return True
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            return True
        try:
            body = e.read().decode("utf-8", "replace")
        except Exception:
            body = ""
        if "PGRST205" in body:
            return False
        return None
    except Exception:
        return None


def migration_gap() -> list[tuple[str, str]]:
    """선언됐지만 라이브 DB에 없는 (테이블, 마이그레이션파일) 목록."""
    url = os.getenv("SUPABASE_URL", "").rstrip("/")
    key = os.getenv("SUPABASE_ANON_KEY", "")
    if not url or not key:
        return []
    missing = []
    for table, fname in sorted(_declared_tables().items()):
        if _table_exists(url, key, table) is False:
            missing.append((table, fname))
    return missing


def _newest_mtime(directory: Path, pattern: str) -> float | None:
    if not directory.exists():
        return None
    files = list(directory.glob(pattern))
    if not files:
        return None
    return max(f.stat().st_mtime for f in files)


def audit(do_send: bool) -> int:
    now = time.time()
    lines = [f"[{datetime.now():%Y-%m-%d %H:%M}] 🩺 예원 함대 신선도 감사"]
    stale = []
    for label, directory, pattern, limit in AGENTS:
        mtime = _newest_mtime(directory, pattern)
        if mtime is None:
            stale.append(label)
            lines.append(f"  ❌ {label}: 산출물 없음")
            continue
        age_h = (now - mtime) / HOUR
        if now - mtime > limit:
            stale.append(label)
            lines.append(f"  ⚠️ {label}: {age_h:.0f}h 무갱신 (임계 {limit/HOUR:.0f}h 초과)")
        else:
            lines.append(f"  ✅ {label}: {age_h:.0f}h 전 산출")
    # 미적용 마이그레이션 감지 — 기능은 배포됐는데 라이브 DB에 테이블이 없어
    # localStorage 폴백으로만 조용히 도는 상황을 자동 포착(2026-07-11 4개 누락 사고 방지).
    gap = migration_gap()
    if gap:
        lines.append("  🗄️ 미적용 마이그레이션: "
                     + ", ".join(f"{t}({f})" for t, f in gap))
    else:
        lines.append("  ✅ DB 스키마: 선언된 테이블 전부 라이브 적용됨")
    print("\n".join(lines))

    if do_send and (stale or gap):
        parts = []
        if stale:
            parts.append("⚠️ 함대 신선도 경보 — 죽은 잡 의심\n"
                         + "\n".join(f"· {s}" for s in stale)
                         + "\n(Hermes 크론 산출물이 임계 초과로 무갱신)")
        if gap:
            ref = os.getenv("SUPABASE_URL", "").split("//")[-1].split(".")[0]
            link = _SUPA_SQL_LINK.format(ref=ref) if ref else "Supabase SQL Editor"
            parts.append("🗄️ 미적용 마이그레이션 " + str(len(gap)) + "개 — 기능이 localStorage로만 동작 중\n"
                         + "\n".join(f"· {t} → migrations/{f}" for t, f in gap)
                         + f"\n실행: {link} 에 해당 SQL 붙여넣고 Run")
        send("[예원] 펫나 감사\n\n" + "\n\n".join(parts), silent=False)
    return len(stale) + len(gap)


def main() -> None:
    ap = argparse.ArgumentParser(description="예원 — 펫나 함대 신선도 감사")
    ap.add_argument("--once", action="store_true", help="감사 1회")
    ap.add_argument("--send", action="store_true", help="stale 시 텔레그램 경보")
    args = ap.parse_args()
    n = audit(do_send=args.send)
    sys.exit(0 if n == 0 else 0)  # stale는 경보로만 알리고 종료코드는 정상(크론 실패 아님)


if __name__ == "__main__":
    main()
