#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""백호 — 펫나 백엔드 지킴이 (Supabase 스키마·RLS·프론트 계약 감사).

매일 1회:
1. supabase_schema.sql + migrations/*.sql 파싱 → 테이블·RLS·정책·함수 목록
2. js/*.js 스캔 → 프론트가 실제 사용하는 테이블(.from)·RPC(.rpc) 수집
3. 계약 불일치 감사:
   - 프론트 사용 O + 스키마 정의 X → P1 (런타임 404/406의 근원 — 이번 profiles 406류)
   - 테이블 RLS 미활성 or 정책 0개 → P2 (보안 기초)
   - 스키마 정의 O + 프론트 미사용 → P3 (정리 후보, 정보성)
4. P1 존재 시 구독 클로드(웹서치 허용)로 원인·수정 방향 분석 첨부
5. 보고서 output/qa/petnna/backend/ + 텔레그램 요약. 읽기 전용 — DB/코드 수정 없음.
"""

from __future__ import annotations

import argparse
import json
import os
import re
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

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared.utils import due_slot  # noqa: E402
from _shared.cc import run_claude  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "backend"
STATE = OUT_DIR / "state.json"
SLOT_STATE = PROJECT_ROOT / "output" / "cache" / "baekho_slots.json"


def _sql_sources() -> str:
    texts = []
    schema = PETNNA_ROOT / "supabase_schema.sql"
    if schema.exists():
        texts.append(schema.read_text(encoding="utf-8", errors="replace"))
    mig = PETNNA_ROOT / "migrations"
    if mig.is_dir():
        for p in sorted(mig.glob("*.sql")):
            texts.append(p.read_text(encoding="utf-8", errors="replace"))
    return "\n".join(texts)


def parse_schema(sql: str) -> dict:
    tables = set(re.findall(
        r"create\s+table\s+(?:if\s+not\s+exists\s+)?(?:public\.)?[\"']?(\w+)", sql, re.I))
    rls_enabled = set(re.findall(
        r"alter\s+table\s+(?:public\.)?[\"']?(\w+)[\"']?\s+enable\s+row\s+level\s+security", sql, re.I))
    policies = re.findall(r"create\s+policy\s+.*?\bon\s+(?:public\.)?[\"']?(\w+)", sql, re.I | re.S)
    policy_count: dict[str, int] = {}
    for t in policies:
        policy_count[t] = policy_count.get(t, 0) + 1
    functions = set(re.findall(
        r"create\s+(?:or\s+replace\s+)?function\s+(?:public\.)?[\"']?(\w+)", sql, re.I))
    return {"tables": tables, "rls": rls_enabled, "policies": policy_count, "functions": functions}


def scan_frontend() -> dict:
    used_tables: dict[str, set[str]] = {}
    used_rpcs: dict[str, set[str]] = {}
    for p in sorted((PETNNA_ROOT / "js").glob("**/*.js")):
        if p.name in ("supabase-js.js", "tailwind.js", "leaflet.js", "chart.umd.min.js"):
            continue  # 벤더 라이브러리 제외
        text = p.read_text(encoding="utf-8", errors="replace")
        for t in re.findall(r"\.from\(\s*['\"](\w+)['\"]", text):
            used_tables.setdefault(t, set()).add(p.name)
        for fn in re.findall(r"\.rpc\(\s*['\"](\w+)['\"]", text):
            used_rpcs.setdefault(fn, set()).add(p.name)
    return {"tables": used_tables, "rpcs": used_rpcs}


def audit() -> list[dict]:
    sql = _sql_sources()
    if not sql.strip():
        return [{"priority": "P2", "title": "스키마 SQL을 찾을 수 없음",
                 "detail": "supabase_schema.sql/migrations 부재 — 계약 감사 불가(추가 확인 필요)"}]
    schema = parse_schema(sql)
    front = scan_frontend()
    findings = []
    for t, files in sorted(front["tables"].items()):
        if t not in schema["tables"]:
            findings.append({"priority": "P1", "title": f"프론트가 쓰는 테이블 '{t}'가 스키마에 없음",
                             "detail": f"사용 파일: {', '.join(sorted(files))} — 런타임 404/406 위험"})
    for fn, files in sorted(front["rpcs"].items()):
        if fn not in schema["functions"]:
            findings.append({"priority": "P1", "title": f"프론트가 호출하는 RPC '{fn}'가 스키마에 없음",
                             "detail": f"사용 파일: {', '.join(sorted(files))}"})
    for t in sorted(schema["tables"]):
        if t not in schema["rls"]:
            findings.append({"priority": "P2", "title": f"테이블 '{t}' RLS 미활성",
                             "detail": "anon 키로 전체 접근 가능성 — ENABLE ROW LEVEL SECURITY 검토"})
        elif not schema["policies"].get(t):
            findings.append({"priority": "P2", "title": f"테이블 '{t}' RLS 활성이나 정책 0개",
                             "detail": "정책이 없으면 전면 차단 — 프론트 406/빈 응답의 전형 원인"})
    used = set(front["tables"])
    for t in sorted(schema["tables"] - used):
        findings.append({"priority": "P3", "title": f"테이블 '{t}' 프론트 미사용",
                         "detail": "정리 후보(서버 전용일 수 있음 — 추가 확인 필요)"})
    return findings


def llm_analysis(findings: list[dict]) -> str:
    p1 = [f for f in findings if f["priority"] == "P1"]
    if not p1:
        return ""
    listing = "\n".join(f"- {f['title']} ({f['detail']})" for f in p1[:5])
    ok, out = run_claude(
        "너는 Supabase 백엔드 전문가다. 펫나(정적 SPA + Supabase) 계약 감사에서 나온 P1 문제다:\n"
        f"{listing}\n\n"
        "projects/petnna/supabase_schema.sql·migrations/·js/ 를 실제로 읽고, 각 문제의 원인과 "
        "안전한 수정 방향(마이그레이션 SQL 초안 포함 가능)을 5줄 이내씩 제시하라. "
        "모르는 Supabase/PostgREST 동작은 웹서치로 확인하라. 코드는 수정하지 마라.",
        PROJECT_ROOT, timeout=600, allowed_tools="WebSearch,WebFetch",
        permission_mode="acceptEdits")
    return out[:2500] if ok else ""


def diff_with_prev(findings: list[dict]) -> tuple[list, list]:
    prev = set()
    try:
        prev = set(json.loads(STATE.read_text(encoding="utf-8")).get("titles", []))
    except Exception:
        pass
    now = {f["title"] for f in findings}
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps({"titles": sorted(now), "run_at": datetime.now().isoformat()},
                                ensure_ascii=False, indent=1), encoding="utf-8")
    return sorted(now - prev), sorted(prev - now)


def run_audit(do_send: bool) -> None:
    print(f"[{datetime.now()}] 🐯 백호 감사 시작")
    findings = audit()
    new, resolved = diff_with_prev(findings)
    analysis = llm_analysis(findings)
    c = {"P1": 0, "P2": 0, "P3": 0}
    for f in findings:
        c[f["priority"]] += 1
    lines = [f"# 펫나 백엔드 계약 감사 — {datetime.now():%Y-%m-%d %H:%M} (백호)", "",
             f"- P1 {c['P1']} / P2 {c['P2']} / P3 {c['P3']} · 신규 {len(new)} · 해결 {len(resolved)}", ""]
    for f in sorted(findings, key=lambda x: x["priority"]):
        tag = " 🆕" if f["title"] in new else ""
        lines.append(f"- [{f['priority']}] {f['title']}{tag} — {f['detail']}")
    if resolved:
        lines += ["", "## 해결됨"] + [f"- {t}" for t in resolved]
    if analysis:
        lines += ["", "## 클로드 분석 (P1)", analysis]
    report_path = OUT_DIR / f"report_{datetime.now():%Y%m%d_%H%M}.md"
    report_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"감사 완료 — P1 {c['P1']}·P2 {c['P2']}·P3 {c['P3']}, 보고서 {report_path}")
    if do_send:
        head = [f"🐯 백호 백엔드 감사 — P1 {c['P1']}·P2 {c['P2']}·P3 {c['P3']}"]
        head += [f"⚠️ {f['title']}" for f in findings if f["priority"] == "P1"][:5]
        if new:
            head.append(f"🆕 신규 {len(new)}건")
        head.append(f"📄 {report_path}")
        send("\n".join(head), silent=(c["P1"] == 0))
        # 신규 P1 계약 불일치 = 큰 이슈 → 전 에이전트 긴급 회의(비차단)
        new_p1 = [f for f in findings if f["priority"] == "P1" and f["title"] in new]
        if new_p1:
            import subprocess
            council = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"
            nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {"start_new_session": True}
            try:
                subprocess.Popen([sys.executable, str(council),
                                  "--topic", f"백엔드 계약 위반: {new_p1[0]['title'][:120]}",
                                  "--context", new_p1[0]["detail"][:1500], "--priority", "P1"],
                                 cwd=str(PROJECT_ROOT),
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **nowin)
            except Exception:
                pass


def daemon() -> None:
    if sys.platform == "win32" and os.getenv("PETNNA_AGENTS_ON_WINDOWS") != "true":
        print("펫나 에이전트는 맥 전용(이중 가동 방지)")
        return
    slots = os.getenv("BAEKHO_SLOTS", "10:30").split(",")
    with ProcessLock("baekho_backend_guard"):
        print(f"[{datetime.now()}] 백호 데몬 시작 — 매일 {','.join(slots)}")
        while True:
            try:
                if due_slot(slots, SLOT_STATE, weekdays_only=False):
                    run_audit(do_send=True)
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 백호 오류: {e}")
                try:
                    send(f"⚠️ 백호 감사 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(300)


def main() -> None:
    ap = argparse.ArgumentParser(description="백호 — 펫나 백엔드 지킴이")
    ap.add_argument("--once", action="store_true", help="감사 1회")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--daemon", action="store_true")
    args = ap.parse_args()
    if args.daemon:
        daemon()
    else:
        run_audit(do_send=args.send)


if __name__ == "__main__":
    main()
