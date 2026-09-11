"""autodev CLI — PHASE 1.

명령:
  doctor                환경 점검(있는 것/없는 것을 사실대로)
  run "<목표>"          worktree 격리 → Codex → 변경 확인 → Unity 컴파일 → 최대 3회 → 커밋
  verify [--project P]  Unity 컴파일 판정만 단독 실행 (네거티브 컨트롤용)
  status [--task N]     Task/시도 이력
  stop                  실행 중으로 기록된 프로세스 그룹 정리
  clean --task N        worktree/브랜치 제거
"""

from __future__ import annotations

import argparse
import shutil
import sys
import time
from pathlib import Path

from . import config, db, gitwt, logs, proc, unityrun
from .agents import REGISTRY


def _p(msg: str = "") -> None:
    print(msg, flush=True)


def _ts() -> str:
    return time.strftime("%Y%m%d-%H%M%S")


# --------------------------------------------------------------------------
# doctor
# --------------------------------------------------------------------------


def cmd_doctor(args) -> int:
    cfg = config.load()
    ok = True
    _p("=== 환경 점검 ===")
    for name in ("git", "codex", "claude", "grok", "ollama"):
        path = shutil.which(name)
        _p(f"  {name:8s} {'OK  ' + path if path else 'MISSING'}")
        if name in ("git", "codex") and not path:
            ok = False
    try:
        t = cfg.target(args.target)
        _p(f"  target   {t.name}")
        _p(f"    repo          {t.repo}")
        _p(f"    unity project {t.unity_project}")
        _p(f"    unity editor  {t.unity_editor}")
        _p(f"    git repo?     {gitwt.is_repo(t.repo)}")
        if gitwt.is_repo(t.repo):
            _p(f"    branch/HEAD   {gitwt.current_branch(t.repo)} / {gitwt.head(t.repo)[:8]}")
            _p(f"    clean?        {gitwt.is_clean(t.repo)}")
        else:
            ok = False
    except config.ConfigError as e:
        _p(f"  target   ERROR: {e}")
        ok = False
    conn = db.connect()
    _p(f"  db       {db.DB_PATH} (tasks={len(db.list_tasks(conn, 10**6))})")
    _p()
    _p("결과: " + ("사용 가능" if ok else "필수 항목 누락 — 위 MISSING/ERROR 참조"))
    return 0 if ok else 1


# --------------------------------------------------------------------------
# verify (Unity 단독)
# --------------------------------------------------------------------------


def cmd_verify(args) -> int:
    cfg = config.load()
    t = cfg.target(args.target)
    project = Path(args.project).resolve() if args.project else t.unity_project
    prefix = config.LOG_DIR / f"verify-{_ts()}"
    _p(f"Unity 컴파일 판정: {project}")
    conn = db.connect()
    r = unityrun.compile_check(t, project, log_prefix=prefix, conn=conn)
    _p(f"  verdict : {r.verdict}")
    _p(f"  reason  : {r.reason}")
    _p(f"  exit    : {r.exit_code}   ({r.duration_s:.1f}s)")
    _p(f"  log     : {r.log_path}")
    if r.errors:
        _p("  errors  :")
        for e in r.errors[:15]:
            _p(f"    {e}")
    return 0 if r.verdict == "PASS" else (1 if r.verdict == "FAILED" else 2)


# --------------------------------------------------------------------------
# run
# --------------------------------------------------------------------------


def cmd_run(args) -> int:
    cfg = config.load()
    t = cfg.target(args.target)
    agent_cfg = cfg.agent(args.agent)
    agent_cls = REGISTRY.get(args.agent)
    if agent_cls is None:
        _p(f"미지원 agent: {args.agent}")
        return 2
    agent = agent_cls(agent_cfg)

    if not gitwt.is_repo(t.repo):
        _p(f"git 저장소가 아니다: {t.repo}")
        return 2
    if not gitwt.is_clean(t.repo):
        _p(f"대상 저장소가 더럽다: {t.repo}\n  — 사용자의 미커밋 변경을 보호하기 위해 중단한다.")
        return 2

    max_attempts = args.max_attempts or cfg.max_attempts
    conn = db.connect()
    task_id = db.create_task(conn, args.goal, t.name, args.agent, agent.model_name())
    base = gitwt.head(t.repo)

    _p(f"[task {task_id}] target={t.name} base={base[:8]} agent={args.agent} max_attempts={max_attempts}")

    try:
        wt, branch = gitwt.create_worktree(t.repo, task_id)
    except gitwt.GitError as e:
        db.update_task(conn, task_id, status="FAILED", reason=str(e), ended_at=db.now())
        _p(f"worktree 생성 실패: {e}")
        return 2
    db.update_task(conn, task_id, status="RUNNING", branch=branch, worktree=str(wt), base_commit=base)
    _p(f"[task {task_id}] worktree={wt}")
    _p(f"[task {task_id}] branch={branch}")

    failure = None
    final_verdict = "UNKNOWN"
    final_reason = "시도를 시작하지 못했다"
    commit_hash = None

    for n in range(1, max_attempts + 1):
        attempt_id = db.create_attempt(conn, task_id, n, args.agent, agent.model_name())
        db.update_task(conn, task_id, attempts=n)
        prefix = config.LOG_DIR / f"task{task_id:04d}-a{n}"
        _p(f"\n--- 시도 {n}/{max_attempts} ---")

        prompt = agent.build_prompt(
            goal=args.goal,
            worktree=wt,
            allowed=t.allowed_write_globs,
            unity_version=t.unity_version,
            failure=failure,
        )
        logs.write(Path(f"{prefix}.prompt.txt"), prompt)

        _p(f"  {args.agent} 호출 중 (timeout {agent_cfg.timeout_sec}s)…")
        ar = agent.run(prompt, worktree=wt, log_prefix=prefix,
                       conn=conn, task_id=task_id, attempt_id=attempt_id)
        _p(f"  {args.agent}: exit={ar.exit_code} status={ar.status} ({ar.duration_s:.1f}s) log={ar.stdout_path.name}")
        db.record_usage(conn, task_id=task_id, attempt_id=attempt_id, agent=args.agent,
                        model=agent.model_name(), ok=ar.ok, duration_s=ar.duration_s,
                        prompt_chars=len(prompt), output_chars=len(ar.output or ""))

        # 0) 인프라 실패는 "이 목표가 어렵다"가 아니다 — 시도를 태우지 말고 즉시 세운다.
        #    (CLI 부재·타임아웃을 "3회 실패"로 오판해 멀쩡한 과제를 보류시킨 전례가 있다.)
        if ar.status in ("SPAWN_FAILED", "TIMEOUT"):
            db.update_attempt(conn, attempt_id, status="UNKNOWN", agent_exit=ar.exit_code,
                              changed_files=0, changed_lines=0, compile_verdict="NOT_RUN",
                              reason=ar.reason, ended_at=db.now())
            db.update_task(conn, task_id, attempts=n - 1)  # 인프라 실패는 시도 미차감
            final_verdict, final_reason = "UNKNOWN", f"인프라 실패(시도 미차감): {ar.reason}"
            _p(f"  → UNKNOWN: {final_reason}")
            break

        # 1) 실제로 파일이 바뀌었는가 — "완료했다"는 말은 여기서 반증된다.
        try:
            ch = gitwt.collect_changes(wt, base)
        except gitwt.GitError as e:
            db.update_attempt(conn, attempt_id, status="UNKNOWN", reason=f"git 실패: {e}", ended_at=db.now())
            final_verdict, final_reason = "UNKNOWN", f"git 상태를 읽지 못했다: {e}"
            break
        _p(f"  변경: 파일 {len(ch.files)}개 / +{ch.insertions} -{ch.deletions}")

        if not ch.changed:
            reason = ar.reason or "AI가 파일을 전혀 고치지 않았다"
            db.update_attempt(conn, attempt_id, status="FAILED", agent_exit=ar.exit_code,
                              changed_files=0, changed_lines=0, compile_verdict="NOT_RUN",
                              reason=reason, ended_at=db.now())
            _p(f"  → FAILED: {reason} (Unity는 돌리지 않는다)")
            failure = {"n": n, "verdict": "NO_CHANGE", "reason": reason,
                       "errors": "파일이 하나도 바뀌지 않았다. 실제로 편집하라."}
            final_verdict, final_reason = "FAILED", reason
            continue

        # 2) 허용 범위 밖을 건드렸는가
        bad = gitwt.violates_write_scope(ch.files, t.allowed_write_globs)
        if bad:
            reason = f"허용 범위 밖 파일 수정: {', '.join(bad[:5])}"
            db.update_attempt(conn, attempt_id, status="FAILED", agent_exit=ar.exit_code,
                              changed_files=len(ch.files), changed_lines=ch.lines,
                              compile_verdict="NOT_RUN", reason=reason, ended_at=db.now())
            _p(f"  → FAILED: {reason}")
            failure = {"n": n, "verdict": "SCOPE", "reason": reason, "errors": "\n".join(bad[:20])}
            final_verdict, final_reason = "FAILED", reason
            continue

        # 3) Unity 판정
        _p(f"  Unity 컴파일 판정 중 (timeout {t.unity_timeout_sec}s)…")
        ur = unityrun.compile_check(t, wt, log_prefix=prefix, conn=conn,
                                    task_id=task_id, attempt_id=attempt_id)
        _p(f"  Unity: {ur.verdict} — {ur.reason} ({ur.duration_s:.1f}s)")
        for e in ur.errors[:8]:
            _p(f"    {e}")

        db.update_attempt(conn, attempt_id,
                          status=ur.verdict, agent_exit=ar.exit_code,
                          changed_files=len(ch.files), changed_lines=ch.lines,
                          compile_verdict=ur.verdict, reason=ur.reason,
                          error_summary=ur.error_summary, ended_at=db.now())
        db.update_task(conn, task_id, attempts=n)

        if ur.verdict == "PASS":
            commit_hash = gitwt.commit(wt, f"autodev(task-{task_id:04d}): {args.goal}\n\n시도 {n}회, Unity 컴파일 PASS")
            final_verdict, final_reason = "PASS", ur.reason
            break

        if ur.verdict == "UNKNOWN":
            # 검증 자체가 성립 안 한 상태에서 AI를 더 태우지 않는다.
            final_verdict, final_reason = "UNKNOWN", ur.reason
            break

        failure = {"n": n, "verdict": ur.verdict, "reason": ur.reason,
                   "errors": ur.error_summary or "(CS 오류를 추출하지 못했다)"}
        final_verdict, final_reason = "FAILED", ur.reason

    # 결과 확정
    # DONE은 오직 Unity PASS에서만 나온다. 나머지는 사람이 보라고 BLOCKED로 세운다(§9).
    status = "DONE" if final_verdict == "PASS" else "BLOCKED"
    db.update_task(conn, task_id, status=status, verdict=final_verdict,
                   reason=final_reason, commit_hash=commit_hash, ended_at=db.now())

    _p("\n=== 결과 ===")
    _p(f"  task     : {task_id}")
    _p(f"  verdict  : {final_verdict}")
    _p(f"  status   : {status}")
    _p(f"  reason   : {final_reason}")
    _p(f"  branch   : {branch} (main/master에는 병합하지 않았다)")
    _p(f"  worktree : {wt}")
    if commit_hash:
        _p(f"  commit   : {commit_hash[:12]}")
        diff = gitwt.git(wt, "show", "--stat", "--oneline", commit_hash)
        _p("  --- diff stat ---")
        for ln in diff.splitlines()[:30]:
            _p(f"  {ln}")
    _p(f"  logs     : {config.LOG_DIR}")
    return 0 if final_verdict == "PASS" else 1


# --------------------------------------------------------------------------
# status / stop / clean
# --------------------------------------------------------------------------


def cmd_status(args) -> int:
    conn = db.connect()
    if args.task:
        t = db.get_task(conn, args.task)
        if not t:
            _p(f"task {args.task} 없음")
            return 2
        for k in t.keys():
            _p(f"  {k:12s} {t[k]}")
        _p("  --- 시도 ---")
        for a in db.list_attempts(conn, args.task):
            _p(f"   #{a['n']} {a['status']:8s} files={a['changed_files']} "
               f"compile={a['compile_verdict']} :: {a['reason']}")
        return 0
    rows = db.list_tasks(conn, args.limit)
    if not rows:
        _p("task 없음")
        return 0
    _p(f"{'id':>4}  {'status':8s} {'verdict':8s} {'att':>3}  goal")
    for r in rows:
        _p(f"{r['id']:>4}  {r['status']:8s} {str(r['verdict'] or '-'):8s} {r['attempts']:>3}  {r['goal'][:60]}")
    return 0


def cmd_stop(args) -> int:
    conn = db.connect()
    notes = proc.stop_all(conn)
    if not notes:
        _p("실행 중으로 기록된 프로세스 없음")
    for n in notes:
        _p(f"  {n}")
    return 0


def cmd_clean(args) -> int:
    cfg = config.load()
    conn = db.connect()
    t = db.get_task(conn, args.task)
    if not t:
        _p(f"task {args.task} 없음")
        return 2
    tgt = cfg.target(t["target"])
    if t["worktree"]:
        gitwt.remove_worktree(tgt.repo, Path(t["worktree"]), t["branch"] if args.delete_branch else None)
        _p(f"worktree 제거: {t['worktree']}")
        if args.delete_branch:
            _p(f"브랜치 삭제: {t['branch']}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog="autodev", description="Unity 자율개발 오케스트레이터 (PHASE 1)")
    ap.add_argument("--target", default=None, help="대상 프로젝트 이름 (config.json)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    d = sub.add_parser("doctor", help="환경 점검")
    d.set_defaults(func=cmd_doctor)

    r = sub.add_parser("run", help="목표 하나를 자율 실행")
    r.add_argument("goal")
    r.add_argument("--agent", default="codex")
    r.add_argument("--max-attempts", type=int, default=None)
    r.set_defaults(func=cmd_run)

    v = sub.add_parser("verify", help="Unity 컴파일 판정만 실행")
    v.add_argument("--project", default=None)
    v.set_defaults(func=cmd_verify)

    s = sub.add_parser("status", help="Task 상태")
    s.add_argument("--task", type=int, default=None)
    s.add_argument("--limit", type=int, default=20)
    s.set_defaults(func=cmd_status)

    st = sub.add_parser("stop", help="실행 중 프로세스 그룹 정리")
    st.set_defaults(func=cmd_stop)

    c = sub.add_parser("clean", help="worktree 정리")
    c.add_argument("--task", type=int, required=True)
    c.add_argument("--delete-branch", action="store_true")
    c.set_defaults(func=cmd_clean)
    return ap


def main(argv=None) -> int:
    ap = build_parser()
    args = ap.parse_args(argv)
    try:
        return args.func(args)
    except config.ConfigError as e:
        _p(f"설정 오류: {e}")
        return 2
    except KeyboardInterrupt:
        _p("\n중단됨 — `autodev stop`으로 남은 프로세스를 정리하라.")
        return 130


if __name__ == "__main__":
    sys.exit(main())
