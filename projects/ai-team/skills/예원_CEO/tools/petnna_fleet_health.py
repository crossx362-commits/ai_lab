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
import subprocess
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
from _shared.backlog import noncanonical_items, canonical_target  # noqa: E402

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

# 라벨 → (스크립트 상대경로, 1회 실행 인자). 무갱신 에이전트를 예원이 직접 깨울 때 쓴다.
AGENT_SCRIPTS = {
    "백호(백엔드)": ("백호_백엔드/tools/petnna_backend_guard.py", ["--once"]),
    "봄이(QA)":     ("봄이_QA/tools/petnna_qa_patrol.py", ["--once"]),
    "테오(테스트)": ("테오_테스트/tools/petnna_test_engineer.py", ["--run"]),
    "수리(개발)":   ("수리_개발자/tools/petnna_dev_engine.py", ["--once"]),
    "미오(디자인)": ("미오_디자인/tools/petnna_design_review.py", ["--once"]),
    "나무(기획)":   ("나무_기획/tools/petnna_product_manager.py", ["--once"]),
}
STATE_PATH = QA / "fleet_health_state.json"
KICK_COOLDOWN = 6 * HOUR  # 같은 에이전트를 이 시간 안에 다시 깨우지 않는다(재점화 루프 방지)


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


def remediate_stale_branches() -> list[str]:
    """커밋이 하나도 없는 펫나 작업 브랜치를 지운다 — 잃을 게 없는 잔재다.

    수리가 과제를 집으면 먼저 브랜치를 만들고 그 다음 클로드를 부른다. 그 호출이
    인프라 사유(토큰 만료·포트 충돌·타임아웃)로 죽으면 **커밋 0개짜리 브랜치만** 남는다.
    쌓이면 두 가지가 나빠진다:
      · 사람이 `git branch`를 봤을 때 "검토 대기가 10개"로 보여 병목을 오판한다
        (2026-08-04 실측: 8개 중 5개가 빈 껍데기였는데 적체로 읽혔다).
      · SURI_MAX_PENDING 상한 판단을 흐린다.
    이 세션이 같은 청소를 하루에 세 번 손으로 했다 — 오너 지시(2026-08-04)로 예원에게 넘긴다.

    master에 없는 커밋이 하나라도 있으면 절대 지우지 않는다. 판정은 rev-list 카운트로
    한다(브랜치 이름·시각 같은 간접 신호를 쓰면 진짜 작업을 날릴 수 있다).
    """
    import subprocess

    def _git(*args: str) -> tuple[int, str]:
        r = subprocess.run(["git", *args], cwd=str(PROJECT_ROOT), capture_output=True,
                           text=True, encoding="utf-8", errors="replace", timeout=20)
        return r.returncode, (r.stdout or "").strip()

    rc, out = _git("branch", "--list", "fix/petnna-*", "ui/petnna-*",
                   "feat/petnna-*", "hotfix/petnna-*")
    if rc != 0 or not out:
        return []
    deleted = []
    for line in out.splitlines():
        br = line.strip().lstrip("* ").strip()
        if not br:
            continue
        rc2, cnt = _git("rev-list", "--count", f"master..{br}")
        if rc2 != 0 or cnt != "0":
            continue          # 커밋이 있거나 판정 불가 → 손대지 않는다
        rc3, _ = _git("branch", "-D", br)
        if rc3 == 0:
            deleted.append(br)
    return deleted


def remediate_backlog_ghosts() -> tuple[list[dict], list[dict]]:
    """백로그 비어휘 상태(정지 유령)를 안전 매핑 가능하면 자동 정규화(파일 기록),
    아니면 미해결로 남긴다. 오너 지시(2026-07-22): '텔레그램 보내기 전에 먼저 고쳐' —
    고칠 수 있는 건 조용히 고치고, 못 고치는 것(사람 확인 필요)만 경보한다.
    반환: (fixed[{id,from,status}], unresolved[{id,status}])."""
    path = QA / "backlog.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return [], []
    items = data if isinstance(data, list) else (data.get("items") or data.get("backlog") or [])
    fixed: list[dict] = []
    unresolved: list[dict] = []
    for it in noncanonical_items(items):
        tgt = canonical_target(it.get("status"))
        if tgt:
            fixed.append({"id": it.get("id", "?"), "from": it.get("status"), "status": tgt})
            it["normalized_from"] = it.get("status")
            it["status"] = tgt
        else:
            unresolved.append({"id": it.get("id", "?"), "status": it.get("status")})
    if fixed:
        try:  # 다른 도구와 동일 포맷(indent=1) 유지, 해당 항목만 바꿔 즉시 기록
            path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
        except Exception:
            pass
    return fixed, unresolved


DEPLOY_URL = os.getenv("PETNNA_DEPLOY_URL", "https://petnna.vercel.app")
_ASSET_RE = re.compile(r'(?:src|href)="((?:js|css)/[^"?]+)\?v=(\d+)"')


def deploy_drift() -> tuple[int, list[str]] | None:
    """저장소의 index.html과 배포본의 자산 버전(?v=)을 대조한다.

    왜 필요한가(2026-07-28·07-31 사고): 배포가 조용히 멈춰도 **자동 검증이 전부
    통과한다** — 코드·E2E·QA 순찰이 모두 로컬을 보기 때문이다. 실제로 배포가 2.5일간
    얼어 있었고, 그 사이 함대가 병합한 것이 사용자에게 하나도 가지 않았는데 어떤
    경보도 뜨지 않았다. 신선도 감사가 '에이전트가 일했나'만 보고 '그 결과가 나갔나'는
    안 봤던 것이다.

    반환: (뒤처진 자산 수, 예시 몇 줄). 네트워크 실패 등 판정 불가면 None —
    '판정 불가'를 '이상 없음'으로 뭉개지 않는다(2026-07-25 빈 응답 교훈).
    """
    local_html = PROJECT_ROOT / "projects" / "petnna" / "index.html"
    try:
        local = dict(_ASSET_RE.findall(local_html.read_text(encoding="utf-8")))
    except Exception:
        return None
    try:
        req = urllib.request.Request(f"{DEPLOY_URL}/index.html",
                                     headers={"Cache-Control": "no-cache"})
        with urllib.request.urlopen(req, timeout=20) as r:
            remote = dict(_ASSET_RE.findall(r.read().decode("utf-8", "replace")))
    except Exception:
        return None
    if not remote:
        return None
    drift = [f"{p}: 로컬 v{v} / 배포 v{remote.get(p, '없음')}"
             for p, v in local.items() if remote.get(p) != v]
    return len(drift), drift[:5]


def _state() -> dict:
    try:
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {}


def _save_state(data: dict) -> None:
    try:
        STATE_PATH.parent.mkdir(parents=True, exist_ok=True)
        STATE_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    except Exception:
        pass


def remediate_stale_agents(stale: list[str]) -> tuple[list[str], list[str]]:
    """무갱신 에이전트를 예원이 직접 1회 실행해 깨운다(오너 지시 2026-08-04:
    "경고 메시지는 알아서 해결하고 텔레그램으로 보내지 마라").

    반환: (깨운 라벨, 못 깨운 라벨). 못 깨운 것만 경보 대상이다.

    가드는 전부 2026-07-11~12 유휴 디스패치 사고에서 실제로 터진 것들이다:
      1. 단일 기계 정책을 먼저 본다 — 이걸 안 봐서 이중 가동 우회 경로가 생겼다.
      2. 스크립트 경로 실재를 **띄우기 전에** 확인한다 — 경로가 틀려도 Popen은
         성공하므로, 3시간 넘게 20분마다 조용히 전면 실패한 적이 있다.
      3. 출력을 DEVNULL로 버리지 않고 평소 데몬과 같은 로그에 이어 쓴다.
      4. 같은 에이전트를 쿨다운 안에 다시 깨우지 않는다.
    """
    if not stale:
        return [], []
    from _shared.process import petnna_single_machine_guard
    if petnna_single_machine_guard("예원 자동 복구"):
        return [], stale  # 이 기계는 운영기가 아니다 — 남의 함대를 깨우지 않는다

    now = time.time()
    st = _state()
    kicks = st.setdefault("last_kick", {})
    logdir = PROJECT_ROOT / "output" / "bot_logs"
    logdir.mkdir(parents=True, exist_ok=True)
    kicked, unfixable = [], []

    for label in stale:
        entry = AGENT_SCRIPTS.get(label)
        if not entry:
            unfixable.append(f"{label}: 실행 방법 미등록")
            continue
        rel, flags = entry
        script = AI_TEAM_ROOT / "skills" / rel
        if not script.is_file():
            unfixable.append(f"{label}: 스크립트 없음({rel})")
            continue
        if now - float(kicks.get(label) or 0) < KICK_COOLDOWN:
            unfixable.append(f"{label}: 직전 자동 복구 후에도 산출물 없음")
            continue
        log = logdir / (script.stem + ".log")
        try:
            fh = open(log, "a", encoding="utf-8", errors="replace")
        except Exception as e:
            unfixable.append(f"{label}: 로그 열기 실패({e})")
            continue
        try:
            fh.write(f"\n[{datetime.now():%Y-%m-%d %H:%M}] 예원 자동 복구 — {label} 무갱신으로 1회 실행\n")
            fh.flush()
            subprocess.Popen([sys.executable, str(script), *flags],
                             cwd=str(PROJECT_ROOT), stdout=fh, stderr=fh)
            kicks[label] = now
            kicked.append(label)
        except Exception as e:
            unfixable.append(f"{label}: 실행 실패({e})")
        finally:
            fh.close()  # 자식이 fd를 상속했으므로 부모는 닫아도 된다(fd 누수 방지)

    st["last_kick"] = kicks
    _save_state(st)
    return kicked, unfixable


def _vercel(path: str, body: dict | None = None) -> dict | None:
    tok = os.getenv("VERCEL_TOKEN")
    if not tok:
        return None
    try:
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(
            "https://api.vercel.com" + path, data=data,
            headers={"Authorization": f"Bearer {tok}", "Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=25) as r:
            return json.loads(r.read().decode("utf-8", "replace"))
    except Exception:
        return None


def remediate_deploy_drift() -> tuple[str, str]:
    """배포 표류를 예원이 직접 해소한다. 반환: (결과코드, 사람이 읽을 설명).

    결과코드: fixed(재배포 걸었음) | pending(빌드 진행 중) | blocked(사람 필요) | unknown

    2026-07-26·07-28 사고 재발 방지: 저장소는 최신인데 사용자 화면이 옛것인 상황은
    코드·E2E·QA가 전부 통과하므로 아무도 못 잡는다. 빌드가 스킵됐을 뿐이면(직전 배포가
    READY) 같은 커밋으로 재배포를 걸면 끝난다 — 이건 예원이 조용히 처리한다.
    빌드가 ERROR면 설정·소스 문제라 예원이 못 고치니 그 사유만 사람에게 넘긴다.
    """
    # 팀 id는 .vercel/project.json의 orgId가 정본 — env VERCEL_TEAM_ID는 낡아서 403이 난다.
    pj = _project_json()
    team = pj.get("orgId") or os.getenv("VERCEL_TEAM_ID", "")
    proj = pj.get("projectId") or os.getenv("VERCEL_PROJECT_ID", "")
    if not (team and proj):
        return "unknown", "Vercel 프로젝트 식별 정보 없음"
    lst = _vercel(f"/v6/deployments?projectId={proj}&teamId={team}&limit=1&target=production")
    if not lst or not lst.get("deployments"):
        return "unknown", "Vercel 배포 목록 조회 실패"
    latest = lst["deployments"][0]
    state = str(latest.get("state") or latest.get("readyState") or "")
    if state in ("BUILDING", "QUEUED", "INITIALIZING"):
        return "pending", f"빌드 진행 중({state}) — 곧 반영"
    full = _vercel(f"/v13/deployments/{latest['uid']}?teamId={team}") or {}
    if state == "ERROR":
        return "blocked", f"직전 배포 실패: {full.get('errorMessage') or '사유 미확인'}"

    src = full.get("gitSource") or {}
    if not src.get("repoId"):
        return "blocked", "재배포에 필요한 git 소스 정보 없음"
    st = _state()
    sha = str(src.get("sha") or "")
    if st.get("redeploy_sha") == sha:
        return "blocked", f"같은 커밋({sha[:8]})으로 이미 재배포했는데도 표류 지속 — 배포 설정 확인 필요"
    made = _vercel(f"/v13/deployments?teamId={team}", {
        "name": full.get("name") or "petnna",
        "project": proj,
        "target": "production",
        "gitSource": {"type": src.get("type", "github"), "repoId": src["repoId"], "ref": "master"},
    })
    if not made:
        return "blocked", "재배포 요청 실패(토큰 권한/네트워크 확인)"
    st["redeploy_sha"] = sha
    _save_state(st)
    return "fixed", f"master 재배포 트리거함({made.get('id') or made.get('uid', '')})"


def _project_json() -> dict:
    try:
        return json.loads((PROJECT_ROOT / "projects" / "petnna" / ".vercel" / "project.json")
                          .read_text(encoding="utf-8"))
    except Exception:
        return {}


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
    # 배포 표류: 에이전트가 일해도 그 결과가 사용자에게 안 나가면 일하지 않은 것과 같다.
    drift = deploy_drift()
    drift_block = ""
    if drift is None:
        lines.append("  ❔ 배포 대조: 판정 불가(네트워크/파싱 실패)")
    elif drift[0]:
        lines.append(f"  🚨 배포 표류: 자산 {drift[0]}개가 배포본에 반영 안 됨")
        lines.extend(f"      · {d}" for d in drift[1])
        code, why = remediate_deploy_drift()
        lines.append(f"      ↳ 자동 대응[{code}]: {why}")
        if code == "blocked":
            drift_block = why
    else:
        lines.append("  ✅ 배포: 저장소와 배포본 자산 버전 일치")
    # 인프라 사유로 죽은 사이클이 남긴 빈 브랜치 청소 — 잃을 게 없고, 쌓이면
    # 검토 적체로 오독된다(오너 지시 2026-08-04: "이런건 다 예원이가 판단하고 배분해서 해결해").
    dead_branches = remediate_stale_branches()
    if dead_branches:
        lines.append(f"  🧹 빈 작업 브랜치 정리: {len(dead_branches)}개 "
                     + ", ".join(b.split("petnna-")[-1][:24] for b in dead_branches[:5]))
    # 백로그 비어휘 상태(정지 유령): 먼저 자동 정규화, 못 고친 것만 아래에서 경보.
    fixed, ghosts = remediate_backlog_ghosts()
    for f in fixed:
        lines.append(f"  🔧 백로그 유령 자동 정규화: {f['id']} {f['from']}→{f['status']}")
    if ghosts:
        lines.append("  👻 백로그 정지 유령(자동정규화 불가·사람 확인 필요): "
                     + ", ".join(f"{g['id']}={g['status']}" for g in ghosts))
    # 무갱신 에이전트는 경보하지 말고 직접 깨운다 — 깨우지 못한 것만 아래에서 보고.
    kicked, stale_unfixable = remediate_stale_agents(stale)
    for k in kicked:
        lines.append(f"  🔄 자동 복구 실행: {k} (1회 수동 사이클 기동)")
    print("\n".join(lines))

    # 텔레그램은 **예원이 못 고친 것만** 나간다(오너 지시 2026-08-04).
    # 자동 적용/정규화/청소/복구 실행은 로그에만 남기고 알리지 않는다.
    parts = []
    if drift_block:
        parts.append("🚨 배포 표류 — 예원이 못 고침\n· " + drift_block)
    if stale_unfixable:
        parts.append("⚠️ 자동 복구 실패 에이전트\n"
                     + "\n".join(f"· {s}" for s in stale_unfixable))
    if gap:
        ref = os.getenv("SUPABASE_URL", "").split("//")[-1].split(".")[0]
        link = _SUPA_SQL_LINK.format(ref=ref) if ref else "Supabase SQL Editor"
        why = " (자동적용 실패 — 토큰/SQL 확인)" if failed else " (토큰 없음 — 수동 실행 필요)"
        parts.append("🗄️ 미적용 마이그레이션 " + str(len(gap)) + "개" + why + "\n"
                     + "\n".join(f"· {t} → migrations/{f}" for t, f in gap)
                     + f"\n실행: {link}")
    if ghosts:
        parts.append("👻 백로그 정지 유령 " + str(len(ghosts)) + "개 (자동 정규화 불가)\n"
                     + "\n".join(f"· {g['id']} = {g['status']}" for g in ghosts))
    if do_send and parts:
        send("[예원] 펫나 감사 — 사람 확인 필요\n\n" + "\n\n".join(parts))
    return len(stale_unfixable) + len(gap) + len(ghosts)


def main() -> None:
    ap = argparse.ArgumentParser(description="예원 — 펫나 함대 신선도 감사")
    ap.add_argument("--once", action="store_true", help="감사 1회")
    ap.add_argument("--send", action="store_true", help="stale 시 텔레그램 경보")
    args = ap.parse_args()
    n = audit(do_send=args.send)
    sys.exit(0 if n == 0 else 0)  # stale는 경보로만 알리고 종료코드는 정상(크론 실패 아님)


if __name__ == "__main__":
    main()
