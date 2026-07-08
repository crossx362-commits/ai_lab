#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""수리 — 펫나 자동 개선형 개발자 (상시 개선 엔진).

봄이(QA)의 순찰 결과를 주기적으로 읽어 가장 영향이 크고 안전한 문제 하나를 골라
격리된 git worktree 브랜치에서 구독 클로드(claude -p headless)로 최소 수정하고,
브랜치 상태에서 봄이 재검수를 돌려 게이트를 통과한 저위험 P2/P3만 master에 자동 병합한다.
게이트 미통과·고위험·P0/P1 수정은 브랜치를 남기고 확인 요청 알림만 보낸다.

안전선:
- master 직접 수정 없음(항상 브랜치 + 게이트), projects/petnna/ 밖 파일 수정 시 병합 거부
- 금지 경로(supabase·api·migrations·결제·env·배포설정) 접촉 시 병합 거부(브랜치 대기)
- 재검수에서 대상 문제 미해결이거나 지표(P0/P1/총 건수) 악화 시 병합 거부
- 같은 이슈 3회 실패 → 보류 + 구조적 원인 알림, 무한 루프 방지
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import re
import shutil
import subprocess
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
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

QA_TOOL = AI_TEAM_ROOT / "skills" / "봄이_QA" / "tools" / "petnna_qa_patrol.py"
QA_STATE = PROJECT_ROOT / "output" / "qa" / "petnna" / "qa_state.json"
DEV_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "dev"
DEV_STATE = DEV_DIR / "dev_state.json"
WT_BASE = PROJECT_ROOT / "output" / "cache" / "suri_worktrees"
QA_PORT = int(os.getenv("SURI_QA_PORT", "8934"))
POLL_SEC = int(os.getenv("SURI_POLL_SEC", "3600"))
CLAUDE_TIMEOUT = int(os.getenv("SURI_CLAUDE_TIMEOUT", "900"))
MAX_ATTEMPTS = 3
MAX_FILES, MAX_LINES = 6, 200

# 자동 병합 후보 유형(P2/P3 한정). 그 외/그 이상은 브랜치 생성까지만.
SAFE_TYPES = {"접근성", "SEO", "콘텐츠", "링크", "반응형", "기능"}
# 이 경로를 건드린 diff는 자동 병합 금지(브랜치 대기) — 인증/결제/DB/배포/시크릿 계열
FORBIDDEN_PATHS = ["api/", "migrations/", "supabase", "inject-env", "freemium",
                   "manifest.json", "sw.js", "vercel.json", "package.json", "package-lock"]
FORBIDDEN_DIFF = re.compile(r"api[_-]?key|secret|token|password|Bearer ", re.IGNORECASE)
BRANCH_PREFIX = {"접근성": "a11y", "성능": "perf", "반응형": "ui", "UIUX": "ui",
                 "콘텐츠": "docs", "SEO": "fix", "링크": "fix", "기능": "fix"}
COMMIT_TYPE = {"접근성": "a11y", "성능": "perf", "반응형": "ui", "UIUX": "ui",
               "콘텐츠": "fix", "SEO": "fix", "링크": "fix", "기능": "fix"}
PRIORITY_ORDER = {"P0": 0, "P1": 1, "P2": 2, "P3": 3}


# ── 봄이 QA 모듈 로드 (한글 경로라 importlib 직접 사용) ────

def _load_qa_module():
    spec = importlib.util.spec_from_file_location("bomi_qa_patrol", QA_TOOL)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def run_qa(petnna_root: Path, out_dir: Path) -> list[dict]:
    """지정 루트(petnna 사본)에 대해 봄이 순찰을 실행하고 중복 제거된 발견 목록을 반환."""
    qa = _load_qa_module()
    qa.PETNNA_ROOT = petnna_root
    qa.QA_DIR = out_dir
    out_dir.mkdir(parents=True, exist_ok=True)
    srv = qa.start_server(QA_PORT)
    try:
        findings = qa.static_checks() + qa.browser_patrol(QA_PORT)
    finally:
        srv.shutdown()
    seen, unique = set(), []
    for f in findings:
        fp = qa._fingerprint(f)
        if fp not in seen:
            seen.add(fp)
            f["id"] = fp
            unique.append(f)
    return unique


def _counts(findings):
    c = {"P0": 0, "P1": 0, "P2": 0, "P3": 0}
    for f in findings:
        c[f["priority"]] += 1
    return c


# ── 상태/이슈 선택 ─────────────────────────────────────────

def load_dev_state() -> dict:
    try:
        return json.loads(DEV_STATE.read_text(encoding="utf-8"))
    except Exception:
        return {"issues": {}}


def save_dev_state(state: dict) -> None:
    DEV_DIR.mkdir(parents=True, exist_ok=True)
    DEV_STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")


def load_qa_findings() -> tuple[dict, str]:
    try:
        data = json.loads(QA_STATE.read_text(encoding="utf-8"))
        return data.get("findings", {}), data.get("last_run") or ""
    except Exception:
        return {}, ""


def select_issue(findings: dict, state: dict, qa_last_run: str) -> tuple[str, dict] | None:
    """우선순위 → 안전 유형 우선 → 반복 횟수 순. 보류/PR대기/완료(재발 아님)는 제외."""
    candidates = []
    for fp, f in findings.items():
        rec = state["issues"].get(fp, {})
        status = rec.get("status", "대기")
        if status == "보류" or status == "PR대기":
            continue
        if status == "완료":
            # 병합 이후의 순찰에서 다시 나타났으면 재발로 보고 재도전
            if rec.get("fixed_at", "") < qa_last_run:
                pass
            else:
                continue
        if rec.get("attempts", 0) >= MAX_ATTEMPTS:
            continue
        ftype = f.get("type", "기능")
        safe = f.get("priority") in ("P2", "P3") and ftype in SAFE_TYPES
        candidates.append((PRIORITY_ORDER.get(f.get("priority", "P3"), 3),
                           0 if safe else 1, -f.get("seen_count", 1), fp, f))
    if not candidates:
        return None
    candidates.sort(key=lambda t: t[:3])
    _, _, _, fp, f = candidates[0]
    return fp, f


# ── git/claude 헬퍼 ────────────────────────────────────────

def _git(args: list[str], cwd: Path) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=str(cwd), capture_output=True, text=True, timeout=120)


def _find_claude() -> str | None:
    # launchd PATH엔 /usr/local/bin 등이 없다(2026-07-08 가드레일) — 표준 경로 폴백 필수
    cli = shutil.which("claude")
    if cli:
        return cli
    for p in ("/usr/local/bin/claude", "/opt/homebrew/bin/claude",
              str(Path.home() / ".local" / "bin" / "claude")):
        if Path(p).exists():
            return p
    return None


def claude_fix(worktree: Path, finding: dict) -> tuple[bool, str]:
    cli = _find_claude()
    if not cli:
        return False, "claude CLI 미발견"
    prompt = (
        "너는 펫나(projects/petnna, 정적 SPA 웹앱) 개발자다. 아래 QA 문제 '하나만' 최소 수정으로 고쳐라.\n\n"
        f"[문제]\n- 우선순위: {finding.get('priority')}\n- 유형: {finding.get('type')}\n"
        f"- 제목: {finding.get('title')}\n- URL: {finding.get('url')} / 환경: {finding.get('env')}\n"
        f"- 상세: {finding.get('detail') or '(없음)'}\n\n"
        "[규칙]\n"
        "- projects/petnna/ 아래 파일만 수정한다. 그 외 파일은 절대 수정 금지.\n"
        "- 이 문제와 무관한 개선·리팩터링·포맷 변경 금지. diff를 최소화하라.\n"
        "- 새 라이브러리 추가 금지. 기존 코드 스타일을 따르라.\n"
        "- 테스트 삭제·규칙 완화·secret/키 추가 금지.\n"
        "- git 커밋은 하지 마라(커밋은 엔진이 한다).\n"
        "- 마지막에 어떤 파일을 왜 바꿨는지 1~3줄로 요약하라."
    )
    try:
        r = subprocess.run([cli, "-p", prompt, "--permission-mode", "acceptEdits"],
                           cwd=str(worktree), capture_output=True, text=True,
                           timeout=CLAUDE_TIMEOUT)
        tail = (r.stdout or r.stderr or "").strip()[-500:]
        return r.returncode == 0, tail
    except subprocess.TimeoutExpired:
        return False, f"claude -p 타임아웃({CLAUDE_TIMEOUT}s)"
    except Exception as e:
        return False, f"claude 실행 실패: {e}"


def diff_gate(worktree: Path) -> tuple[bool, str, list[str]]:
    """diff 범위 게이트: petnna 한정·크기 제한·금지 경로/내용."""
    num = _git(["diff", "master", "--numstat"], worktree).stdout.strip()
    if not num:
        return False, "변경 없음", []
    files, total = [], 0
    for line in num.splitlines():
        parts = line.split("\t")
        if len(parts) != 3:
            continue
        add, rm, path = parts
        files.append(path)
        total += (int(add) if add.isdigit() else 0) + (int(rm) if rm.isdigit() else 0)
    outside = [f for f in files if not f.startswith("projects/petnna/")]
    if outside:
        return False, f"petnna 밖 파일 수정: {outside[:3]}", files
    hit = [f for f in files if any(k in f for k in FORBIDDEN_PATHS)]
    if hit:
        return False, f"금지 경로 접촉(병합 대기): {hit[:3]}", files
    if len(files) > MAX_FILES or total > MAX_LINES:
        return False, f"변경 과대(파일 {len(files)}·{total}줄) — PR 분리 필요", files
    diff_text = _git(["diff", "master"], worktree).stdout
    added = "\n".join(ln for ln in diff_text.splitlines() if ln.startswith("+"))
    if FORBIDDEN_DIFF.search(added):
        return False, "추가된 줄에 시크릿/인증 의심 패턴", files
    return True, f"파일 {len(files)}개·{total}줄", files


# ── 개선 사이클 ────────────────────────────────────────────

def improve_cycle(do_send: bool = True) -> str:
    findings, qa_last_run = load_qa_findings()
    if not findings:
        return "QA 발견 없음 — 대기"
    state = load_dev_state()
    picked = select_issue(findings, state, qa_last_run)
    if not picked:
        return "처리 가능한 이슈 없음(전부 완료/보류/대기)"
    fp, f = picked
    rec = state["issues"].setdefault(fp, {"attempts": 0, "status": "대기", "title": f.get("title")})
    rec["attempts"] += 1
    rec["last_try"] = datetime.now().isoformat()
    save_dev_state(state)

    prefix = "hotfix" if f.get("priority") in ("P0", "P1") else BRANCH_PREFIX.get(f.get("type", ""), "fix")
    branch = f"{prefix}/petnna-{fp}"
    wt = WT_BASE / fp
    log = [f"[자동 개선 루프] {datetime.now():%Y-%m-%d %H:%M}",
           f"- 선택: [{f.get('priority')}][{f.get('type')}] {f.get('title')}",
           f"- 브랜치: {branch} (시도 {rec['attempts']}/{MAX_ATTEMPTS})"]
    merged = False
    try:
        # 워크트리 준비 (기존 잔재 정리 후 master 기준 새 브랜치)
        _git(["worktree", "remove", "--force", str(wt)], PROJECT_ROOT)
        _git(["branch", "-D", branch], PROJECT_ROOT)
        r = _git(["worktree", "add", "-b", branch, str(wt), "master"], PROJECT_ROOT)
        if r.returncode != 0:
            raise RuntimeError(f"worktree 생성 실패: {r.stderr.strip()[:200]}")

        base = run_qa(wt / "projects" / "petnna", DEV_DIR / "qa_base")
        base_ids = {x["id"] for x in base}
        if fp not in base_ids:
            rec["status"] = "완료"
            rec["fixed_at"] = datetime.now().isoformat()
            log.append("- 결과: 현재 코드에서 재현 안 됨 → 이미 해결된 것으로 처리")
            return "\n".join(log)

        ok, note = claude_fix(wt, f)
        log.append(f"- 수정 실행: {'성공' if ok else '실패'} — {note[:200]}")
        if not ok:
            raise RuntimeError("claude 수정 실패")

        ctype = COMMIT_TYPE.get(f.get("type", ""), "fix")
        _git(["add", "-A", "projects/petnna"], wt)
        c = _git(["commit", "-m",
                  f"{ctype}(petnna): {f.get('title', '')[:60]} (수리 자동 수정, QA {fp})"], wt)
        if c.returncode != 0:
            raise RuntimeError("변경 없음 — 수정이 만들어지지 않음")

        gate_ok, gate_note, files = diff_gate(wt)
        log.append(f"- diff 게이트: {'통과' if gate_ok else '거부'} — {gate_note}")
        log.append(f"- 변경 파일: {', '.join(files[:6])}")

        after = run_qa(wt / "projects" / "petnna", DEV_DIR / "qa_after")
        after_ids = {x["id"] for x in after}
        cb, ca = _counts(base), _counts(after)
        resolved = fp not in after_ids
        not_worse = (ca["P0"] == 0 and ca["P1"] <= cb["P1"]
                     and sum(ca.values()) <= sum(cb.values()))
        log.append(f"- 재검수: 대상 {'해결' if resolved else '미해결'}, "
                   f"지표 {cb} → {ca} ({'악화 없음' if not_worse else '악화'})")

        safe_priority = f.get("priority") in ("P2", "P3") and f.get("type") in SAFE_TYPES
        if gate_ok and resolved and not_worse and safe_priority:
            # 자동 병합 — main 트리의 petnna 경로가 깨끗할 때만
            dirty = _git(["status", "--porcelain", "--", "projects/petnna"], PROJECT_ROOT).stdout.strip()
            if dirty:
                rec["status"] = "PR대기"
                log.append("- 병합 보류: main 워킹트리 petnna 경로에 미커밋 변경 존재")
            else:
                m = _git(["merge", "--no-ff", branch, "-m",
                          f"Merge {branch}: 수리 자동 개선 (QA 재검수 통과)"], PROJECT_ROOT)
                if m.returncode == 0:
                    merged = True
                    rec["status"] = "완료"
                    rec["fixed_at"] = datetime.now().isoformat()
                    log.append("- 자동 병합: 완료 (봄이가 변경 감지로 재순찰 예정)")
                else:
                    rec["status"] = "PR대기"
                    log.append(f"- 병합 실패 → 브랜치 대기: {m.stderr.strip()[:150]}")
        else:
            rec["status"] = "PR대기" if (resolved and not_worse) else rec.get("status", "대기")
            reason = ("고위험 분류" if not safe_priority else
                      "게이트 거부" if not gate_ok else "재검수 미통과")
            log.append(f"- 자동 병합 안 함({reason}) — "
                       + (f"브랜치 {branch} 검토 대기" if rec["status"] == "PR대기" else "다음 루프 재시도"))
    except Exception as e:
        log.append(f"- 오류: {str(e)[:200]}")
    finally:
        _git(["worktree", "remove", "--force", str(wt)], PROJECT_ROOT)
        if merged or rec.get("status") not in ("PR대기",):
            if merged:
                _git(["branch", "-d", branch], PROJECT_ROOT)
        if rec["attempts"] >= MAX_ATTEMPTS and rec.get("status") not in ("완료", "PR대기"):
            rec["status"] = "보류"
            log.append(f"- {MAX_ATTEMPTS}회 실패 → 보류 전환, 구조적 원인 분석 필요")
        rec["branch"] = branch
        save_dev_state(state)

    report = "\n".join(log)
    DEV_DIR.mkdir(parents=True, exist_ok=True)
    (DEV_DIR / f"loop_{datetime.now():%Y%m%d_%H%M}.md").write_text(report, encoding="utf-8")
    if do_send:
        if merged:
            send(f"🔧 수리 자동 개선 병합\n{f.get('title','')[:100]}\n브랜치 {branch} → master, QA 재검수 통과")
        elif rec.get("status") == "PR대기":
            send(f"🔧 수리 — 확인 필요한 수정 대기\n[{f.get('priority')}] {f.get('title','')[:100]}\n"
                 f"브랜치 {branch} (자동 병합 조건 미충족 — 사람 검토 후 병합)")
        elif rec.get("status") == "보류":
            send(f"⚠️ 수리 — 반복 실패로 보류\n{f.get('title','')[:100]}\n"
                 f"{MAX_ATTEMPTS}회 시도 실패 — 구조적 원인 점검 필요", silent=True)
    print(report)
    return report


def daemon() -> None:
    with ProcessLock("suri_dev_engine"):
        print(f"[{datetime.now()}] 수리 데몬 시작 — QA 결과 {POLL_SEC}초 주기 확인")
        while True:
            try:
                result = improve_cycle(do_send=True)
                print(f"[{datetime.now()}] 사이클: {result.splitlines()[-1] if result else '-'}")
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 사이클 오류: {e}")
                try:
                    send(f"⚠️ 수리 개선 엔진 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(POLL_SEC)


def main() -> None:
    ap = argparse.ArgumentParser(description="수리 — 펫나 자동 개선 엔진")
    ap.add_argument("--once", action="store_true", help="개선 사이클 1회")
    ap.add_argument("--no-send", action="store_true", help="텔레그램 전송 생략")
    ap.add_argument("--daemon", action="store_true", help="상시 데몬")
    args = ap.parse_args()
    if args.daemon:
        daemon()
    else:
        improve_cycle(do_send=not args.no_send)


if __name__ == "__main__":
    main()
