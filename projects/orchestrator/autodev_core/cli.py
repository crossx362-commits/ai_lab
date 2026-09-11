"""autodev CLI.

명령:
  doctor                 환경 점검(있는 것/없는 것을 사실대로)
  run "<목표>"           worktree 격리 → 에이전트 → 변경 확인 → Unity → 리뷰 → 커밋
  plan "<목표>"          목표를 Task로 분해(실행 안 함)
  run-plan --plan N      계획을 의존성 순서로 실행
  plans [--plan N]       계획 목록·진행
  verify [--tests]       Unity 판정만 단독 실행(네거티브 컨트롤용)
  status [--task N]      Task/시도 이력
  stop / resume          STOP 플래그 + 프로세스 그룹 정리 / 해제
  archive --task N...    기록은 남기고 화면에서 내림
  gc / clean --task N    잔재 점검 / worktree·브랜치 제거
"""

from __future__ import annotations

import argparse
import shutil
import sys
import time
from pathlib import Path

from . import (agents, config, db, gitwt, loganalyze, logs, memory, planner,
               proc, review, router, safety, unityrun)


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
    r = unityrun.compile_check(t, project, log_prefix=prefix, conn=conn, unity_slots=cfg.unity_slots)
    _p(f"  verdict : {r.verdict}")
    _p(f"  reason  : {r.reason}")
    _p(f"  exit    : {r.exit_code}   ({r.duration_s:.1f}s)")
    _p(f"  log     : {r.log_path}")
    if r.errors:
        _p("  errors  :")
        for e in r.errors[:15]:
            _p(f"    {e}")
    if not args.tests or r.verdict != "PASS":
        return 0 if r.verdict == "PASS" else (1 if r.verdict == "FAILED" else 2)

    worst = "PASS"
    for plat in t.test_platforms:
        tr = unityrun.run_tests(t, project, plat, log_prefix=prefix, conn=conn, unity_slots=cfg.unity_slots)
        _p(f"  {tr.summary}  ({tr.duration_s:.1f}s) — {tr.reason}")
        for f in tr.failures[:10]:
            _p(f"    {f}")
        if tr.verdict == "FAILED":
            worst = "FAILED"
        elif tr.verdict == "UNKNOWN" and worst != "FAILED":
            worst = "UNKNOWN"
    _p(f"  종합    : {worst}")
    return 0 if worst == "PASS" else (1 if worst == "FAILED" else 2)


# --------------------------------------------------------------------------
# run
# --------------------------------------------------------------------------


def run_side_agent(cfg, conn, *, agent_name: str, prompt: str, workdir: Path,
                   log_prefix: Path, task_id=None, attempt_id=None):
    """파일을 만들어 내는 보조 호출(분해·리뷰). worktree가 아니라 **별도 작업 폴더**에서 돈다 —
    그래야 보조가 만든 파일이 개발 diff에 섞이지 않는다."""
    workdir.mkdir(parents=True, exist_ok=True)
    agent = agents.build(cfg.agent(agent_name))
    logs.write(Path(f"{log_prefix}.prompt.txt"), prompt)
    ar = agent.run(prompt, worktree=workdir, log_prefix=log_prefix,
                   conn=conn, task_id=task_id, attempt_id=attempt_id)
    db.record_usage(conn, task_id=task_id, attempt_id=attempt_id, agent=agent_name,
                    model=agent.model_name(), ok=ar.ok, duration_s=ar.duration_s,
                    prompt_chars=len(prompt), output_chars=len(ar.output or ""))
    return ar


def run_reviewer(cfg, conn, *, task_id, attempt_id, prefix: Path, goal: str,
                 done_criteria: str, gates: str, diff: str):
    workdir = config.STATE_DIR / "reviews" / f"task{task_id:04d}-a{attempt_id}"
    rpath = workdir / review.REVIEW_FILE
    if rpath.exists():
        rpath.unlink()
    prompt = review.build_prompt(goal=goal, done_criteria=done_criteria, gates=gates,
                                 diff=diff, review_path=rpath)
    ar = run_side_agent(cfg, conn, agent_name=cfg.reviewer, prompt=prompt, workdir=workdir,
                        log_prefix=Path(f"{prefix}.review"), task_id=task_id, attempt_id=attempt_id)
    if ar.status in ("SPAWN_FAILED", "TIMEOUT"):
        return review.Review("UNKNOWN", f"리뷰를 돌리지 못했다: {ar.reason}")
    return review.parse(rpath)


def cmd_run(args) -> int:
    cfg = config.load()
    t = cfg.target(args.target)
    rc = execute_goal(cfg, t, goal=args.goal, agent=args.agent,
                      max_attempts=args.max_attempts)
    return rc


def execute_goal(cfg, t, *, goal: str, agent: str | None = None,
                 max_attempts: int | None = None, plan_id: int | None = None,
                 plan_key: str | None = None, done_criteria: str = "") -> int:
    """목표 하나를 끝까지 몬다. run과 run-plan이 **같은 본체**를 쓴다 —
    게이트가 두 벌이 되면 한쪽만 강화되고 다른 쪽이 구멍이 된다."""
    args_goal = goal
    # 사다리: agent를 주면 그 하나로 고정, 안 주면 라우터가 시도마다 등급을 고른다.
    ladder = [agent] if agent else cfg.ladder
    try:
        for a in ladder:
            agents.build(cfg.agent(a))  # 설정이 실제로 만들어지는지 먼저 확인
    except (KeyError, config.ConfigError) as e:
        _p(f"사다리 설정 오류: {e}")
        return 2
    _p(f"[사다리] {' → '.join(ladder)}" + (f" (조사: {cfg.research_agent})" if cfg.research_agent else ""))

    if safety.stop_requested():
        _p(f"STOP 상태다 — 새 작업을 시작하지 않는다 ({safety.STOP_FILE})\n  해제: autodev resume")
        return 2

    # 메모리도 시작 전에 본다. RED면 잠시 기다리되 무한정은 아니다 —
    # 스왑이 차는 채로 Unity를 띄우면 기계 전체가 느려지고 판정 시간도 못 믿게 된다.
    msnap = memory.sample()
    mstate = memory.assess(msnap)
    _p(f"[메모리] {msnap.line()} → {mstate.summary()}")
    if mstate.state == "RED":
        _p("  메모리 RED — 여유가 생길 때까지 기다린다(최대 300초)")
        state, msnap, notes = memory.wait_for_room()
        for nt in notes:
            _p(f"  {nt}")
        if state == "RED":
            _p("  중단: 메모리 압박이 풀리지 않았다 — 지금 시작하지 않는다")
            return 2
        mstate = memory.Assessment(state, ["대기 후 회복"])
    elif mstate.state == "YELLOW":
        for nt in memory.relieve("YELLOW", cfg.log_summarizer.get("model")):
            _p(f"  {nt}")

    # 디스크는 시작 전에 막는다 — worktree 중간에 터지면 Unity Library가 반쯤 남는다.
    try:
        free = safety.ensure_disk(t.repo)
        _p(f"[디스크] 여유 {free:.1f}GB")
    except safety.SafetyError as e:
        _p(f"중단: {e}")
        return 2

    if not gitwt.is_repo(t.repo):
        _p(f"git 저장소가 아니다: {t.repo}")
        return 2
    if not gitwt.is_clean(t.repo):
        _p(f"대상 저장소가 더럽다: {t.repo}\n  — 사용자의 미커밋 변경을 보호하기 위해 중단한다.")
        return 2

    max_attempts = max_attempts or cfg.max_attempts
    conn = db.connect()
    task_id = db.create_task(conn, args_goal, t.name, ladder[0], None,
                             plan_id=plan_id, plan_key=plan_key, done_criteria=done_criteria)
    base = gitwt.head(t.repo)

    _p(f"[task {task_id}] target={t.name} base={base[:8]} max_attempts={max_attempts}")

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
    history: list[dict] = []   # 라우터가 보는 실패 이력
    final_verdict = "UNKNOWN"
    final_reason = "시도를 시작하지 못했다"
    commit_hash = None

    stopped = False
    for n in range(1, max_attempts + 1):
        if safety.stop_requested():
            stopped = True
            final_verdict, final_reason = "UNKNOWN", "STOP 요청으로 중단(시도 시작 전)"
            _p(f"\n  → STOP: {final_reason}")
            break
        prefix = config.LOG_DIR / f"task{task_id:04d}-a{n}"
        _p(f"\n--- 시도 {n}/{max_attempts} ---")

        # 이번 시도의 등급을 고른다. 왜 그 등급인지를 함께 남긴다 — 사유 없는 승격은
        # 나중에 규칙을 고칠 수도, 비용을 따질 수도 없다.
        decision = router.decide(goal=args_goal, ladder=ladder, attempt=n,
                                 failures=history, research_agent=cfg.research_agent)
        agent_name = decision.agent
        agent_cfg = cfg.agent(agent_name)
        agent = agents.build(agent_cfg)
        _p(f"  담당: {agent_name} — {decision.reason}")

        attempt_id = db.create_attempt(conn, task_id, n, agent_name, agent.model_name())
        db.update_task(conn, task_id, attempts=n, agent=agent_name, model=agent.model_name())

        prompt = agent.build_prompt(
            goal=args_goal,
            worktree=wt,
            allowed=t.allowed_write_globs,
            unity_version=t.unity_version,
            failure=failure,
        )
        logs.write(Path(f"{prefix}.prompt.txt"), prompt)

        _p(f"  {agent_name} 호출 중 (timeout {agent_cfg.timeout_sec}s)…")
        ar = agent.run(prompt, worktree=wt, log_prefix=prefix,
                       conn=conn, task_id=task_id, attempt_id=attempt_id)
        _p(f"  {agent_name}: exit={ar.exit_code} status={ar.status} ({ar.duration_s:.1f}s) log={ar.stdout_path.name}")
        db.record_usage(conn, task_id=task_id, attempt_id=attempt_id, agent=agent_name,
                        model=agent.model_name(), ok=ar.ok, duration_s=ar.duration_s,
                        prompt_chars=len(prompt), output_chars=len(ar.output or ""))

        # STOP이 걸린 뒤에 에이전트가 죽어 돌아온 것을 "실패"로 기록하지 않는다 —
        # 중단은 실패가 아니다. 여기서 상태를 STOPPED로 남기고 빠진다.
        if safety.stop_requested():
            db.update_attempt(conn, attempt_id, status="STOPPED", agent_exit=ar.exit_code,
                              reason="STOP 요청으로 중단", ended_at=db.now())
            db.update_task(conn, task_id, attempts=n - 1)
            stopped = True
            final_verdict, final_reason = "UNKNOWN", "STOP 요청으로 중단(에이전트 실행 중)"
            _p(f"  → STOP: {final_reason}")
            break

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
            history.append(failure)
            final_verdict, final_reason = "FAILED", reason
            continue

        # 2) 검증 장치를 건드렸는가 — 게이트를 지워 PASS를 만드는 길을 코드로 막는다.
        #    테스트는 **쓰는 것은 허용, 지우는 것은 금지**다(§2-16). 그래서 삭제만 따로 본다.
        tampered = gitwt.touches_protected(ch.files, t.protected_globs)
        tampered += [f"(삭제) {f}" for f in gitwt.touches_protected(ch.deleted, t.test_guard_globs)]
        if tampered:
            reason = f"검증 장치 변조 시도: {', '.join(tampered[:5])}"
            db.update_attempt(conn, attempt_id, status="FAILED", agent_exit=ar.exit_code,
                              changed_files=len(ch.files), changed_lines=ch.lines,
                              compile_verdict="NOT_RUN", reason=reason, ended_at=db.now())
            _p(f"  → FAILED: {reason} (Unity는 돌리지 않는다)")
            gitwt.restore_tracked(wt)
            failure = {"n": n, "verdict": "TAMPER", "reason": reason,
                       "errors": "검증 장치(Assets/AutoDev)는 수정 대상이 아니다. 되돌렸다."}
            history.append(failure)
            final_verdict, final_reason = "FAILED", reason
            continue

        # 3) 허용 범위 밖을 건드렸는가
        bad = gitwt.violates_write_scope(ch.files, t.allowed_write_globs)
        if bad:
            reason = f"허용 범위 밖 파일 수정: {', '.join(bad[:5])}"
            db.update_attempt(conn, attempt_id, status="FAILED", agent_exit=ar.exit_code,
                              changed_files=len(ch.files), changed_lines=ch.lines,
                              compile_verdict="NOT_RUN", reason=reason, ended_at=db.now())
            _p(f"  → FAILED: {reason}")
            failure = {"n": n, "verdict": "SCOPE", "reason": reason, "errors": "\n".join(bad[:20])}
            history.append(failure)
            final_verdict, final_reason = "FAILED", reason
            continue

        # 4) Unity 판정
        mnow = memory.assess(memory.sample())
        slots = memory.effective_unity_slots(mnow.state, cfg.unity_slots)
        if slots != cfg.unity_slots:
            _p(f"  메모리 {mnow.state} — Unity 동시 실행을 {slots}개로 줄인다")
        _p(f"  Unity 컴파일 판정 중 (timeout {t.unity_timeout_sec}s)…")
        ur = unityrun.compile_check(t, wt, log_prefix=prefix, conn=conn,
                                    task_id=task_id, attempt_id=attempt_id,
                                    unity_slots=slots)
        _p(f"  Unity: {ur.verdict} — {ur.reason} ({ur.duration_s:.1f}s)")
        for e in ur.errors[:8]:
            _p(f"    {e}")

        # 5) 컴파일이 통과했으면 테스트까지 본다. 컴파일은 "돌아간다"이지 "맞다"가 아니다.
        verdict, reason, errtext = ur.verdict, ur.reason, ur.error_summary
        if ur.verdict == "PASS" and t.test_platforms:
            for plat in t.test_platforms:
                tr = unityrun.run_tests(t, wt, plat, log_prefix=prefix, conn=conn,
                                        task_id=task_id, attempt_id=attempt_id,
                                        unity_slots=slots)
                _p(f"  {tr.summary} ({tr.duration_s:.1f}s) — {tr.reason}")
                for f in tr.failures[:6]:
                    _p(f"    {f}")
                if tr.verdict != "PASS":
                    verdict = tr.verdict
                    reason = f"{plat} {tr.reason}"
                    errtext = "\n".join(tr.failures[:20]) or tr.reason
                    break

        # 6) 게이트를 다 지났으면 한 등급 위가 diff를 다시 본다 — "돌아간다"와 "목표를 했다"는 다르다.
        review_note = ""
        review_unknown = False
        if verdict == "PASS" and cfg.reviewer:
            rv = run_reviewer(cfg, conn, task_id=task_id, attempt_id=attempt_id, prefix=prefix,
                              goal=args_goal, done_criteria=done_criteria,
                              gates="컴파일 + " + "·".join(t.test_platforms), diff=ch.diff)
            _p(f"  리뷰({cfg.reviewer}): {rv.verdict} — {rv.reason}")
            for rr in rv.reasons[:5]:
                _p(f"    {rr}")
            db.update_task(conn, task_id, review=f"{rv.verdict}: {rv.reason}")
            if rv.verdict == "REJECT":
                verdict, reason = "REJECTED", rv.reason
                errtext = "\n".join(rv.reasons) or rv.reason
            elif rv.verdict == "UNKNOWN":
                # 리뷰가 돌았는지 모르는 상태는 승인도 실패도 아니다. 게이트는 통과했으니
                # 커밋은 하되 **DONE으로 올리지 않는다** — "확인 못 한 것은 완료가 아니다".
                review_note = "\n\n리뷰: 판정 불가(사람 검토 필요)"
                review_unknown = True
            else:
                review_note = "\n\n리뷰: 승인"

        db.update_attempt(conn, attempt_id,
                          status=verdict, agent_exit=ar.exit_code,
                          changed_files=len(ch.files), changed_lines=ch.lines,
                          compile_verdict=f"compile={ur.verdict}/final={verdict}", reason=reason,
                          error_summary=errtext, ended_at=db.now())
        db.update_task(conn, task_id, attempts=n)
        ur = unityrun.UnityResult(verdict, reason, ur.exit_code, ur.errors, ur.log_path, ur.report, ur.duration_s)
        if errtext:
            ur.errors = errtext.splitlines()

        if ur.verdict == "PASS":
            gate = "컴파일 + " + "·".join(t.test_platforms) if t.test_platforms else "컴파일"
            commit_hash = gitwt.commit(
                wt, f"autodev(task-{task_id:04d}): {args_goal}\n\n시도 {n}회, Unity {gate} PASS{review_note}")
            final_verdict, final_reason = "PASS", ur.reason
            break

        if ur.verdict == "UNKNOWN":
            # 검증 자체가 성립 안 한 상태에서 AI를 더 태우지 않는다.
            final_verdict, final_reason = "UNKNOWN", ur.reason
            break

        if ur.verdict == "REJECTED":
            # 리뷰 반려는 "돌아가지만 목표를 안 했다"는 뜻이다. 남은 시도가 있으면 사유를 들고
            # 다시 시킨다. 없으면 커밋해서 사람이 볼 수 있게 두되 DONE으로 올리지 않는다.
            failure = {"n": n, "verdict": "REJECTED", "reason": ur.reason,
                       "errors": ur.error_summary or ur.reason}
            history.append(failure)
            final_verdict, final_reason = "REJECTED", ur.reason
            if n >= max_attempts:
                commit_hash = gitwt.commit(
                    wt, f"autodev(task-{task_id:04d}) [리뷰 반려]: {args_goal}\n\n{ur.reason}")
            continue

        # 다음 시도에는 로그를 던지는 대신 **분석한 것**을 준다 — 오류가 가리키는 자리를 펼쳐서.
        analysis = loganalyze.analyze(ur.errors, wt)
        if analysis.findings:
            _p(f"  분석: 관련 파일 {len(analysis.files)}개 — {', '.join(analysis.files[:4]) or '(미상)'}")
        rendered = analysis.render()
        # 오류가 너무 많으면 로컬 모델이 줄인다. 실패하면 원문 그대로 간다(요약 실패 ≠ 오류 없음).
        digest = (loganalyze.maybe_summarize(rendered, cfg.log_summarizer)
                  if mnow.state == "GREEN" else None)
        if mnow.state != "GREEN" and cfg.log_summarizer.get("enabled"):
            _p(f"  메모리 {mnow.state} — 로컬 요약 건너뜀(큰 모델을 올리지 않는다)")
        if digest:
            _p("  로컬 요약 사용(gemma) — 원문은 로그에 남는다")
            rendered = f"[로컬 요약]\n{digest}\n\n[원문 일부]\n{rendered[:4000]}"
        failure = {"n": n, "verdict": ur.verdict, "reason": ur.reason,
                   "errors": rendered or ur.error_summary or "(오류를 추출하지 못했다)"}
        history.append(failure)
        final_verdict, final_reason = "FAILED", ur.reason

    # 결과 확정
    # DONE은 오직 Unity PASS에서만 나온다. 나머지는 사람이 보라고 BLOCKED로 세운다(§9).
    if stopped:
        status = "STOPPED"
    elif final_verdict == "PASS":
        status = "REVIEW" if review_unknown else "DONE"
    elif final_verdict == "REJECTED":
        status = "REVIEW"      # 사람이 봐야 한다. 자동 DONE 아님.
    else:
        status = "BLOCKED"
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
    # 순서가 중요하다: **플래그부터** 세운다. 프로세스를 먼저 죽이면 그 사이에 루프가
    # 다음 시도를 시작해버린다(실제로 그렇게 한 번 안 멈췄다).
    safety.request_stop(args.reason or "")
    _p(f"STOP 플래그: {safety.STOP_FILE}")
    conn = db.connect()
    notes = proc.stop_all(conn)
    if not notes:
        _p("  실행 중으로 기록된 프로세스 없음")
    for n in notes:
        _p(f"  {n}")
    _p("재개하려면: autodev resume")
    return 0


def cmd_resume(args) -> int:
    if safety.clear_stop():
        _p("STOP 해제됨")
    else:
        _p("STOP 상태가 아니었다")
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


def cmd_plan(args) -> int:
    """목표를 Task로 분해만 한다. 실행은 run-plan이 따로 한다 — 분해가 틀렸을 때
    이미 코드가 고쳐져 있으면 되돌리는 비용이 크다."""
    cfg = config.load()
    t = cfg.target(args.target)
    conn = db.connect()
    agent_name = args.agent or cfg.planner
    workdir = config.STATE_DIR / "plans" / f"plan-{_ts()}"
    ppath = workdir / planner.PLAN_FILE

    plan_id = db.create_plan(conn, args.goal, agent_name, str(workdir))
    _p(f"[plan {plan_id}] 분해 담당: {agent_name}")
    prompt = planner.build_prompt(goal=args.goal, project=t.unity_project,
                                  unity_version=t.unity_version, plan_path=ppath)
    ar = run_side_agent(cfg, conn, agent_name=agent_name, prompt=prompt, workdir=workdir,
                        log_prefix=config.LOG_DIR / f"plan{plan_id:04d}")
    _p(f"  {agent_name}: exit={ar.exit_code} status={ar.status} ({ar.duration_s:.1f}s)")

    if ar.status in ("SPAWN_FAILED", "TIMEOUT"):
        db.update_plan(conn, plan_id, status="BLOCKED", note=f"인프라 실패: {ar.reason}")
        _p(f"  → UNKNOWN: {ar.reason}")
        return 2

    p = planner.parse(ppath)
    if not p.ok:
        db.update_plan(conn, plan_id, status="BLOCKED", note=p.reason)
        _p(f"  → 분해 실패: {p.reason}")
        return 1

    ordered = planner.order(p.tasks)
    for pt in ordered:
        db.create_task(conn, pt.goal, t.name, cfg.ladder[0], None, status="BACKLOG",
                       plan_id=plan_id, plan_key=pt.key, depends_on=",".join(pt.depends_on),
                       done_criteria=pt.done_criteria, risk=pt.risk)
    db.update_plan(conn, plan_id, status="READY", note=p.reason)

    _p(f"\n=== 계획 {plan_id} ({len(ordered)}개, 의존성 순) ===")
    for pt in ordered:
        dep = f" ← {','.join(pt.depends_on)}" if pt.depends_on else ""
        _p(f"  {pt.key:4s} [{pt.risk:6s}]{dep}  {pt.goal}")
        if pt.done_criteria:
            _p(f"        완료조건: {pt.done_criteria}")
    _p(f"\n실행: autodev run-plan --plan {plan_id}")
    return 0


def cmd_run_plan(args) -> int:
    cfg = config.load()
    t = cfg.target(args.target)
    conn = db.connect()
    plan = db.get_plan(conn, args.plan)
    if not plan:
        _p(f"계획 {args.plan} 없음")
        return 2

    tasks = [r for r in db.plan_tasks(conn, args.plan) if r["status"] == "BACKLOG"]
    if not tasks:
        _p("실행할 Task가 없다(이미 끝났거나 비었다)")
        return 0

    db.update_plan(conn, args.plan, status="RUNNING")
    done_keys = {r["plan_key"] for r in db.plan_tasks(conn, args.plan) if r["status"] == "DONE"}
    rc_all = 0
    for row in tasks:
        if safety.stop_requested():
            _p("STOP 상태 — 남은 Task를 시작하지 않는다")
            db.update_plan(conn, args.plan, status="BLOCKED", note="STOP")
            return 2
        deps = [d for d in (row["depends_on"] or "").split(",") if d]
        missing = [d for d in deps if d not in done_keys]
        if missing:
            # 선행이 끝나지 않았는데 밀어붙이지 않는다 — 반쪽 위에 쌓으면 원인을 못 가린다.
            _p(f"\n[{row['plan_key']}] 건너뜀 — 선행 미완료: {', '.join(missing)}")
            db.update_task(conn, row["id"], status="BLOCKED", reason=f"선행 미완료: {','.join(missing)}")
            rc_all = 1
            continue
        _p(f"\n========== [{row['plan_key']}] {row['goal']} ==========")
        # BACKLOG 자리표시 task는 지우고, 실제 실행은 본체가 자기 task를 만들어 돈다.
        db.update_task(conn, row["id"], status="ARCHIVED", reason="[계획 자리표시]")
        rc = execute_goal(cfg, t, goal=row["goal"], max_attempts=args.max_attempts,
                          plan_id=args.plan, plan_key=row["plan_key"],
                          done_criteria=row["done_criteria"] or "")
        if rc == 0:
            done_keys.add(row["plan_key"])
        else:
            rc_all = rc
            if not args.keep_going:
                _p(f"\n[{row['plan_key']}]에서 멈춘다 (계속하려면 --keep-going)")
                db.update_plan(conn, args.plan, status="BLOCKED", note=f"{row['plan_key']}에서 멈춤")
                return rc
    db.update_plan(conn, args.plan, status="DONE" if rc_all == 0 else "BLOCKED")
    return rc_all


def cmd_plans(args) -> int:
    conn = db.connect()
    for p in db.list_plans(conn, args.limit):
        tasks = db.plan_tasks(conn, p["id"])
        done = sum(1 for r in tasks if r["status"] == "DONE")
        _p(f"  #{p['id']} {p['status']:8s} {done}/{len(tasks)} 완료  {p['goal'][:60]}")
        if args.plan and p["id"] == args.plan:
            for r in tasks:
                _p(f"     {r['plan_key'] or '-':4s} {r['status']:9s} {str(r['verdict'] or '-'):8s} {r['goal'][:60]}")
    return 0


def cmd_mem(args) -> int:
    snap = memory.sample()
    a = memory.assess(snap)
    _p(f"  {snap.line()}")
    _p(f"  상태: {a.summary()}")
    if snap.procs:
        _p("  프로세스: " + ", ".join(f"{k} {v:.0f}MB" for k, v in sorted(snap.procs.items(), key=lambda x: -x[1])))
    _p(f"  Unity 동시 실행 허용: {memory.effective_unity_slots(a.state, config.load().unity_slots)}개")
    if args.relieve:
        for nt in memory.relieve(a.state if a.state != "GREEN" else "YELLOW"):
            _p(f"  {nt}")
    return 0 if a.state == "GREEN" else (1 if a.state == "YELLOW" else 2)


def cmd_unity_kill(args) -> int:
    """멎은 배치 Unity를 세운다 — **대상 프로젝트 경로를 인자로 가진 것만**.
    이 기계에는 다른 세션의 Unity가 같이 돈다. 전역 패턴 kill은 쓰지 않는다(2026-09-11 사고)."""
    cfg = config.load()
    t = cfg.target(args.target)
    procs = safety.unity_procs(t.unity_project)
    if not procs:
        _p(f"[{t.name}] 이 프로젝트의 Unity 프로세스 없음 (다른 프로젝트 것은 건드리지 않는다)")
        return 0
    for pid, cmd in procs:
        _p(f"  pid {pid}  {cmd[:110]}")
    if not args.yes:
        _p(f"위 {len(procs)}개를 세우려면 --yes를 붙여라")
        return 1
    for n in safety.kill_unity(t.unity_project, force=args.force):
        _p(f"  {n}")
    return 0


def cmd_archive(args) -> int:
    """기록은 남기고 화면에서만 내린다 — 시험용으로 돌린 판이 보드의 '막힘'을 채우지 않게."""
    conn = db.connect()
    for tid in args.task:
        t = db.get_task(conn, tid)
        if not t:
            _p(f"task {tid} 없음")
            continue
        if t["status"] == "RUNNING":
            _p(f"task {tid}은 RUNNING이다 — 먼저 세우고 정리하라")
            continue
        db.update_task(conn, tid, status="ARCHIVED",
                       reason=f"[보관] {t['reason'] or t['status']}")
        _p(f"task {tid} 보관 ({t['status']} → ARCHIVED)")
    return 0


def cmd_gc(args) -> int:
    cfg = config.load()
    t = cfg.target(args.target)
    conn = db.connect()
    # DB가 아는 worktree는 전부 "잔재 아님"이다 — 끝난 것은 아래에서 따로 보고한다.
    # (둘을 섞어 같은 경로를 두 가지로 말한 적이 있다.)
    keep = {r["worktree"] for r in db.list_tasks(conn, 10**6) if r["worktree"]}
    _p(f"[디스크] 여유 {safety.free_gb(t.repo):.1f}GB")
    for n in gitwt.gc(t.repo, keep):
        _p(f"  {n}")
    stale = [r for r in db.list_tasks(conn, 10**6)
             if r["worktree"] and Path(r["worktree"]).is_dir() and r["status"] in ("DONE", "BLOCKED")]
    if stale:
        _p(f"  끝난 task의 worktree {len(stale)}개가 남아 있다 (증거 보존용). "
           f"지우려면: autodev clean --task <N> [--delete-branch]")
        for r in stale[:10]:
            _p(f"    task {r['id']} {r['status']:8s} {r['worktree']}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog="autodev", description="Unity 자율개발 오케스트레이터 (PHASE 1)")
    ap.add_argument("--target", default=None, help="대상 프로젝트 이름 (config.json)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    d = sub.add_parser("doctor", help="환경 점검")
    d.set_defaults(func=cmd_doctor)

    r = sub.add_parser("run", help="목표 하나를 자율 실행")
    r.add_argument("goal")
    # 기본값을 두면 사다리가 조용히 무시된다 — 그 탓에 시험판이 실제 유료 모델을 부른 적이 있다.
    # 비워 두고, 지정이 없으면 config.json의 ladder를 쓴다.
    r.add_argument("--agent", default=None, help="한 등급으로 고정(미지정 시 ladder 사용)")
    r.add_argument("--max-attempts", type=int, default=None)
    r.set_defaults(func=cmd_run)

    v = sub.add_parser("verify", help="Unity 컴파일 판정 (+ --tests 로 테스트까지)")
    v.add_argument("--project", default=None)
    v.add_argument("--tests", action="store_true", help="컴파일 후 테스트도 실행")
    v.set_defaults(func=cmd_verify)

    s = sub.add_parser("status", help="Task 상태")
    s.add_argument("--task", type=int, default=None)
    s.add_argument("--limit", type=int, default=20)
    s.set_defaults(func=cmd_status)

    st = sub.add_parser("stop", help="STOP 플래그 + 실행 중 프로세스 그룹 정리")
    st.add_argument("--reason", default="")
    st.set_defaults(func=cmd_stop)

    rs = sub.add_parser("resume", help="STOP 해제")
    rs.set_defaults(func=cmd_resume)

    pl = sub.add_parser("plan", help="목표를 Task로 분해(실행 안 함)")
    pl.add_argument("goal")
    pl.add_argument("--agent", default=None, help="분해 담당(기본 config.planner)")
    pl.set_defaults(func=cmd_plan)

    rp = sub.add_parser("run-plan", help="계획을 의존성 순서로 실행")
    rp.add_argument("--plan", type=int, required=True)
    rp.add_argument("--max-attempts", type=int, default=None)
    rp.add_argument("--keep-going", action="store_true", help="실패해도 남은 Task를 계속")
    rp.set_defaults(func=cmd_run_plan)

    ps = sub.add_parser("plans", help="계획 목록")
    ps.add_argument("--plan", type=int, default=None)
    ps.add_argument("--limit", type=int, default=10)
    ps.set_defaults(func=cmd_plans)

    mm = sub.add_parser("mem", help="메모리 상태(압박·스왑·프로세스)")
    mm.add_argument("--relieve", action="store_true", help="로컬 모델을 내려 압박을 던다")
    mm.set_defaults(func=cmd_mem)

    uk = sub.add_parser("unity-kill", help="**이 target의** 멎은 Unity만 골라 세운다")
    uk.add_argument("--target", default=None)
    uk.add_argument("--force", action="store_true", help="SIGTERM 대신 SIGKILL")
    uk.add_argument("--yes", action="store_true", help="확인 없이 종료")
    uk.set_defaults(func=cmd_unity_kill)

    ar = sub.add_parser("archive", help="task를 보관 처리(기록 유지, 화면에서 내림)")
    ar.add_argument("--task", type=int, nargs="+", required=True)
    ar.set_defaults(func=cmd_archive)

    g = sub.add_parser("gc", help="잔재 worktree 점검·prune")
    g.set_defaults(func=cmd_gc)

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
