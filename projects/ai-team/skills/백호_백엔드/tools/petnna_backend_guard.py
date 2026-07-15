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
from _shared.process import ProcessLock, advisory_lock, petnna_single_machine_guard  # noqa: E402
from _shared.utils import due_slot  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.backlog import is_infra_failure, apply_task_failure, TASK_MAX_ATTEMPTS  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "backend"
STATE = OUT_DIR / "state.json"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"
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
    # 2026-07-13: 프롬프트는 늘 "읽고"를 요구했는데 allowed_tools엔 Read가 없어 실제로는
    # 읽을 수 없었다(웹서치만 가능한 상태로 방치된 기존 버그, 이번에 같이 고침). investigate_
    # assigned_tasks와 동일하게 Bash(python3)로 스크립트를 작성해 스키마/마이그레이션/js에서
    # 각 문제와 관련된 부분만 걸러내고, 그 필터링된 결과 위에서만 원인·수정 방향을 판단하게 한다.
    ok, out = run_claude(
        "너는 Supabase 백엔드 전문가다. 펫나(정적 SPA + Supabase) 계약 감사에서 나온 P1 문제다:\n"
        f"{listing}\n\n"
        "[처리 방식]\n"
        "1. projects/petnna/supabase_schema.sql·migrations/*.sql·js/**/*.js 에서 위 문제들과 관련된 "
        "테이블/RLS/정책/함수/프론트 사용처만 뽑아내는 파이썬 스크립트를 작성해 `python3 -c \"...\"` 형태로 "
        "Bash 실행하라(다른 bash 명령·파일쓰기 금지). 관련 없는 원문은 출력하지 마라.\n"
        "2. 그 필터링된 결과를 근거로 각 문제의 원인과 안전한 수정 방향(마이그레이션 SQL 초안 포함 가능)을 "
        "5줄 이내씩 제시하라. 모르는 Supabase/PostgREST 동작은 웹서치로 확인하라. 코드는 수정하지 마라.",
        PROJECT_ROOT, timeout=600, allowed_tools="Read,Bash(python3:*),Bash(python:*),WebSearch,WebFetch",
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


# ── 회의/사람이 백호에게 위임한 조사 과제 소비 ──────────────
# (2026-07-09 발견: 회의가 owner=백호로 액션을 위임해도 이걸 읽어 실행하는
#  루프가 없어 조사 과제가 영구 대기하던 공백 — 여기서 메운다. 읽기 전용 조사만
#  수행, 코드/DB는 건드리지 않는다 — 수정 필요 결론이면 별도 [승인필요] 항목으로.)

def investigate_assigned_tasks(do_send: bool) -> int:
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        return 0
    todo = [it for it in data["items"] if it.get("owner") == "백호" and it.get("status") == "대기"]
    if not todo:
        return 0
    print(f"[백호] 위임 조사 착수: {len(todo)}건 — 한 번에 처리")
    # 과제마다 run_claude를 반복 호출하던 것을 하나로 묶는다(2026-07-13, 프로그래매틱 툴
    # 콜링 적용) — 항목마다 스키마·마이그레이션·js를 처음부터 다시 읽게 하던 반복 호출
    # 대신, 모델이 직접 작은 파이썬 스크립트를 작성해 Bash(python 한정)로 실행하게 한다.
    # 스크립트가 스키마·마이그레이션·js를 한 번만 읽어 과제별로 걸러내고, 원문이 아니라
    # 필터링된 결론만 stdout으로 남기도록 강제 — "도구를 하나씩 부르는 대신 프로그램을
    # 작성해 한 번에 실행, 중간 결과는 프로그램 안에서만 흐르고 최종 결과만 반환"하는
    # 구조를 subprocess(Bash) 경계로 실제 재현한다(진짜 code_execution API는 크레딧 0원
    # + 구독전용 정책으로 불가 — Bash(python)가 subscription CLI에서 낼 수 있는 최선의 근사).
    listing = "\n".join(
        f"{i}. [id={it.get('id')}] {it['title']}\n   상세: {it.get('detail', '')}"
        for i, it in enumerate(todo, 1))
    ok, out = run_claude(
        "너는 펫나 백엔드 지킴이 백호다. 회의/사람이 위임한 아래 조사 과제들을 전부 처리하라 — "
        "결론만 명확히, 코드/DB는 절대 수정하지 마라(읽기 전용 조사).\n\n"
        f"[조사 과제 {len(todo)}건]\n{listing}\n\n"
        "[처리 방식 — 반드시 이렇게 하라]\n"
        "1. projects/petnna/supabase_schema.sql·migrations/*.sql·js/**/*.js 내용을 읽어 위 과제들을 "
        "판정하는 파이썬 스크립트를 작성하라(정규식/문자열 검색으로 테이블·RLS·정책·함수 존재 여부를 "
        "직접 판정 — 네가 나중에 눈으로 다시 훑지 말고 스크립트가 판정하게 하라).\n"
        "2. 그 스크립트를 `python3 -c \"...\"` 형태로 Bash 실행해라(다른 bash 명령·파일쓰기 금지). "
        "스크립트의 출력은 과제별 결론만 남기고(원본 파일 텍스트·중간 검색 결과는 출력하지 마라).\n"
        "3. 스크립트만으로 판단이 안 되는 과제(코드 이해·설계 판단이 필요한 것)만 Read/웹서치를 추가로 써라.\n"
        "과제마다 결론은 6줄 이내: 무엇을 확인했는지, 결론, 후속 조치가 필요하면 무엇인지(신규 테이블 필요 여부 등 명확히).\n\n"
        "출력은 모든 과제를 담은 JSON 배열만(다른 텍스트 금지): "
        '[{"id": "<과제 id 그대로>", "conclusion": "<6줄 이내 결론>"}]',
        PROJECT_ROOT, timeout=900, allowed_tools="Read,Bash(python3:*),Bash(python:*),WebSearch,WebFetch",
        permission_mode="plan")
    results = extract_json(out) if ok and out else None
    by_id = {}
    if isinstance(results, list):
        by_id = {str(r.get("id")): (r.get("conclusion") or "").strip()
                 for r in results if isinstance(r, dict) and r.get("id")}
    whole_call_infra_failed = not (ok and out) and is_infra_failure(out)

    # 백로그를 다시 읽어 적용한다(2026-07-12 가드레일 유지) — 그 사이 다른 에이전트가
    # backlog.json에 적재·변경한 내용을 유실하지 않도록 시작 시점 스냅샷을 쓰지 않는다.
    try:
        fresh = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception as e:
        # 조용한 반환은 호출자가 백로그 갱신 실패를 알 방법이 없다(2026-07-13 파이프라인
        # 감사가 발견 — 미오·나무의 add_backlog_items는 이미 로그를 남기는데 여기만 비대칭).
        print(f"[백호] 백로그 재읽기 실패(파일 손상 가능) — 과제 처리 결과 미반영: {e}")
        return len(todo)
    sent = []
    for it in todo:
        target = next((x for x in fresh["items"] if x.get("id") == it.get("id")), None)
        if target is None:
            continue  # 그 사이 항목이 없어짐(수동 삭제 등) — 스킵
        conclusion = by_id.get(str(it.get("id")))
        if conclusion:
            target["status"] = "완료"
            target["finding"] = conclusion[:1500]
            target["updated"] = datetime.now().isoformat()
            print(f"[백호] 조사 완료: {it['title'][:60]}")
            sent.append(f"- {it['title'][:80]}\n  {conclusion[:400]}")
        elif whole_call_infra_failed:
            # 호출 자체가 인프라 사유로 전멸 — 과제 탓이 아니다. 시도 미차감(수리
            # _improve_cycle과 동일 원칙), 다음 주기 통째로 재시도.
            print(f"[백호] 조사 실패(인프라) — 다음 주기 재시도: {it['title'][:60]}")
        else:
            # 이 과제만 응답에서 누락됐거나 호출이 비-인프라 사유로 실패 — attempts를
            # 반영해야 상한 도달 시 보류로 전환되는 안전장치가 유지된다.
            apply_task_failure(target)
            print(f"[백호] 조사 실패(시도 {target.get('attempts')}/{TASK_MAX_ATTEMPTS}): {it['title'][:60]}")
    BACKLOG.write_text(json.dumps(fresh, ensure_ascii=False, indent=1), encoding="utf-8")
    if do_send and sent:
        send(f"🐯 백호 — 위임 조사 완료 {len(sent)}건\n\n" + "\n\n".join(sent), silent=True)
    return len(todo)


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
            # DEVNULL이면 락 충돌로 회의가 실제로 안 열려도 흔적이 안 남는다(2026-07-12
            # 자동 파이프라인 감사가 발견 — 유휴디스패치 제거 원인과 동일 계열 패턴).
            log_dir = PROJECT_ROOT / "output" / "bot_logs"
            out_f = err_f = None
            try:
                log_dir.mkdir(parents=True, exist_ok=True)
                out_f = open(log_dir / "petnna_council_trigger.out.log", "a", encoding="utf-8")
                err_f = open(log_dir / "petnna_council_trigger.err.log", "a", encoding="utf-8")
                print(f"[{datetime.now()}] === 백호 회의 소집: {new_p1[0]['title'][:80]} ===",
                      file=out_f, flush=True)
                subprocess.Popen([sys.executable, str(council),
                                  "--topic", f"백엔드 계약 위반: {new_p1[0]['title'][:120]}",
                                  "--context", new_p1[0]["detail"][:1500], "--priority", "P1"],
                                 cwd=str(PROJECT_ROOT), stdout=out_f, stderr=err_f, **nowin)
            except Exception:
                pass
            finally:
                if out_f:
                    out_f.close()
                if err_f:
                    err_f.close()


def daemon() -> None:
    if petnna_single_machine_guard("백호"):
        return
    slots = os.getenv("BAEKHO_SLOTS", "10:30").split(",")
    with ProcessLock("baekho_backend_guard_daemon"):  # 중복 데몬 기동 방지(상시 보유, 이 이름 전용)
        print(f"[{datetime.now()}] 백호 데몬 시작 — 매일 {','.join(slots)}")
        while True:
            try:
                # "baekho_backend_guard"(daemon 접미사 없음)는 실행 구간에만 짧게 잡는
                # 비치명적 락 — 예원 워치독의 수동 디스패치와 겹쳐도 데몬이 죽지 않는다.
                with advisory_lock("baekho_backend_guard") as got:
                    if got:
                        if due_slot(slots, SLOT_STATE, weekdays_only=False):
                            run_audit(do_send=True)
                        investigate_assigned_tasks(do_send=True)  # 빈 목록이면 즉시 반환 — 매 주기 저비용 확인
                    else:
                        print(f"[{datetime.now()}] 다른 실행이 진행 중 — 이번 주기 건너뜀")
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
    ap.add_argument("--tasks", action="store_true", help="위임 조사 과제만 즉시 처리")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--daemon", action="store_true")
    args = ap.parse_args()
    if args.daemon:
        daemon()
        return
    with advisory_lock("baekho_backend_guard") as got:
        if not got:
            print("다른 실행이 진행 중 — 건너뜀")
            return
        if args.tasks:
            n = investigate_assigned_tasks(do_send=args.send)
            print(f"위임 과제 {n}건 처리")
        else:
            run_audit(do_send=args.send)


if __name__ == "__main__":
    main()
