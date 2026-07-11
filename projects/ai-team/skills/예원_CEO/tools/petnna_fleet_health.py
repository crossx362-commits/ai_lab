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
import json
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


# Management API는 Cloudflare WAF가 Python-urllib UA를 1010으로 차단 → 브라우저 UA 필수.
_MGMT_UA = ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
            "(KHTML, like Gecko) Chrome/125.0 Safari/537.36")


def _run_mgmt_sql(sql: str) -> tuple[bool, str]:
    """Supabase Management API로 임의 SQL 실행. 성공(True, 응답요약)/실패(False, 오류).
    SUPABASE_ACCESS_TOKEN(개인 액세스 토큰)이 있어야 동작한다."""
    token = os.getenv("SUPABASE_ACCESS_TOKEN", "")
    ref = (os.getenv("SUPABASE_PROJECT_REF", "")
           or os.getenv("SUPABASE_URL", "").split("//")[-1].split(".")[0])
    if not token or not ref:
        return False, "토큰/프로젝트ref 없음"
    data = json.dumps({"query": sql}).encode("utf-8")
    req = urllib.request.Request(
        f"https://api.supabase.com/v1/projects/{ref}/database/query",
        data=data, method="POST",
        headers={"Authorization": f"Bearer {token}",
                 "Content-Type": "application/json", "User-Agent": _MGMT_UA})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return True, r.read().decode("utf-8", "replace")[:200]
    except urllib.error.HTTPError as e:
        try:
            body = e.read().decode("utf-8", "replace")[:200]
        except Exception:
            body = ""
        return False, f"HTTP {e.code}: {body}"
    except Exception as e:
        return False, str(e)[:200]


def apply_pending_migrations(gap: list[tuple[str, str]]) -> tuple[list[str], list[tuple[str, str]]]:
    """미적용 마이그레이션 파일을 Management API로 자동 적용. (적용성공 파일, 실패[파일,오류]).
    파일은 CREATE TABLE IF NOT EXISTS 등 멱등이라 재실행 안전. 표-누락 감지분만 다룬다
    (add_handle_new_user_trigger 같은 비-테이블 마이그레이션은 자동 트리거되지 않음)."""
    applied, failed = [], []
    by_file: dict[str, list[str]] = {}
    for table, fname in gap:
        by_file.setdefault(fname, []).append(table)
    for fname in sorted(by_file):
        try:
            sql = (MIGRATIONS / fname).read_text(encoding="utf-8")
        except Exception as e:
            failed.append((fname, f"파일 읽기 실패: {str(e)[:100]}"))
            continue
        ok, msg = _run_mgmt_sql(sql)
        if ok:
            applied.append(fname)
        else:
            failed.append((fname, msg))
    if applied:
        # Management API raw DDL은 PostgREST 스키마 캐시를 자동 갱신하지 않는다 →
        # 리로드를 명시해야 앱(supabase-js/REST)이 새 테이블을 즉시 인식(안 하면 계속 PGRST205).
        _run_mgmt_sql("NOTIFY pgrst, 'reload schema';")
    return applied, failed


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
    applied: list[str] = []
    failed: list[tuple[str, str]] = []
    if gap and os.getenv("SUPABASE_ACCESS_TOKEN"):
        # 토큰 있으면 콘솔 없이 자동 적용(오너 지시 2026-07-11) → 적용 후 재확인
        applied, failed = apply_pending_migrations(gap)
        if applied:
            gap = migration_gap()

    if applied:
        lines.append("  🛠️ 마이그레이션 자동 적용: " + ", ".join(applied))
    if gap:
        lines.append("  🗄️ 미적용 마이그레이션(수동 필요): "
                     + ", ".join(f"{t}({f})" for t, f in gap))
    elif not applied:
        lines.append("  ✅ DB 스키마: 선언된 테이블 전부 라이브 적용됨")
    print("\n".join(lines))

    if do_send and (stale or gap or applied or failed):
        parts = []
        if stale:
            parts.append("⚠️ 함대 신선도 경보 — 죽은 잡 의심\n"
                         + "\n".join(f"· {s}" for s in stale)
                         + "\n(Hermes 크론 산출물이 임계 초과로 무갱신)")
        if applied:
            parts.append("🛠️ 마이그레이션 자동 적용 완료 (예원 · Management API)\n"
                         + "\n".join(f"· migrations/{f}" for f in applied))
        if gap:
            ref = os.getenv("SUPABASE_URL", "").split("//")[-1].split(".")[0]
            link = _SUPA_SQL_LINK.format(ref=ref) if ref else "Supabase SQL Editor"
            why = " (자동적용 실패 — 토큰/SQL 확인)" if failed else " (토큰 없음 — 수동 실행 필요)"
            parts.append("🗄️ 미적용 마이그레이션 " + str(len(gap)) + "개" + why + "\n"
                         + "\n".join(f"· {t} → migrations/{f}" for t, f in gap)
                         + f"\n실행: {link}")
        send("[예원] 펫나 감사\n\n" + "\n\n".join(parts), silent=not (stale or gap or failed))
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
