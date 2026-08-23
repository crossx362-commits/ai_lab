# 재와별 자율 병렬 게임 개발 루프 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 재와별 게임 개발을 Claude·Codex·Grok의 강한 모델로 한 작업 또는 최대 세 작업까지 안전하게 병렬 수행하고, 다른 제공자의 독립 검토와 실제 실행·시각 증거를 통과한 정확한 커밋만 `autonomous/integration`에 원자적으로 통합하는 상시 루프를 완성한다.

**Architecture:** 저장소 루트는 오너 입력 원천이며 AI 세션에는 읽기 전용이다. 셸 런처는 매 바퀴 새 Python 코디네이터 프로세스만 호출한다. 코디네이터는 독립 `git clone --no-local`과 macOS `sandbox-exec` 안에서 비영속 AI 세션을 실행하고, 중앙 저장소에는 manifest가 소유한 임시 ref와 `autonomous/integration`만 CAS로 쓴다. 상태·WAL·로그·STATUS 미러는 코디네이터만 원자 기록하며 보드는 구조화 산출물만 읽는다. launchd 실행본은 live worktree가 아니라 검증된 release SHA에서 불변 release 디렉터리로 배포한다.

**Tech Stack:** Bash, Python 3.10+ 표준 라이브러리, Git plumbing, macOS `sandbox-exec`·launchd, `unittest`, Claude CLI, Codex CLI, Grok CLI.

**Spec:** `docs/superpowers/specs/2026-08-23-autonomous-game-development-loop-design.md`

## Global Constraints

- 루프는 `/Users/junholee/ai_lab`의 사용자 작업트리, `master`, 타 AI 브랜치/ref, 기존 worktree를 쓰지 않는다. 동시 외부 변경은 정상으로 감사 기록하고, 소유 ref·Git config/hooks·보호 canary 변조만 hard fail한다.
- 실행 중 `master`가 이동하면 실패로 위장하지 않는다. 최신 master를 `autonomous/integration`에 합치는 별도 동기화 후보를 만들고 전체 검사·다른 제공자 검토를 다시 거친다. 충돌 시 즉시 abort하고 보류한다.
- 자동 쓰기 대상은 `refs/heads/autonomous/integration`, 현재 run manifest의 `refs/autonomous-loop/*`, `logs/`, `output/cache/autonomous_loop/`, 격리 clone/temp뿐이다.
- `projects/ai-team/_shared/**`와 `projects/ai-team/harness/**`는 수정하지 않는다. 배포본은 `_shared`에 런타임 의존하지 않는다.
- 모든 동작 변경은 실패 테스트와 기대한 실패 이유를 먼저 확인한다. 커밋 직전 관련 테스트, 완료 직전 전체 회귀와 하네스를 새로 실행한다.
- 스테이징은 검증된 정확한 파일만 `git add -- <paths>`로 하고 같은 명령 흐름에서 즉시 커밋한다. `git add -A`, `git commit -a`, force push를 사용하지 않는다.
- Claude는 `fable`, 사용량 소진·이용 불가 때만 `opus5`; Codex는 `gpt-5.6-sol`/`xhigh`; Grok은 `grok-4.6`이다. 실제 모델이 허용 목록인지 스트림에서 확인할 수 없으면 실패한다.
- Ollama 기본값은 `off`이며 명시적으로 켠 비판단적 분류·형식 변환만 맡긴다. 게임 코드·설계·밸런스·QA·시각 판정·검토에는 사용하지 않는다.
- 이미지 생성은 Higgsfield 또는 Grok Imagine capability가 확인된 경우만 허용한다. 둘 다 없으면 해당 작업을 인프라 보류한다.
- 기본값은 최대 동시 작업 3, 최대 턴 30, worker 1,800초, reviewer 900초, 재시도 2, 회로 임계 3, 회로 대기 900초, idle poll 60초, 바퀴 간 10초, 최소 여유/캐시 상한 각 20 GiB다. smoke는 세션당 4턴·300초다.
- 한 바퀴 종료 코드는 `0`=AI 세션을 실행한 계수 바퀴 완료, `10`=idle 비계수, `20`=STOP 정상 drain, `75`=인프라 보류 비계수, 그 외=치명적 코디네이터 실패다.
- launchd는 두 실제 smoke 바퀴와 최종 회귀가 끝난 뒤 설치 파일만 배치한다. `bootstrap`, `load`, `kickstart`는 실행하지 않으며 현재 등록돼 있으면 설치도 거부한다.

## 공통 직렬화 계약

구현자는 아래 이름·필드를 그대로 사용한다. 모든 JSON은 `schema_version: 1`, UTF-8, 정렬 키, trailing newline을 사용하며 알 수 없는 필드와 잘못된 타입을 거부한다.

- `RepoPath(raw, parts, comparison_keys)`: `comparison_keys`는 NFC/NFD casefold 구성요소 tuple의 frozenset이다.
- `ChangedPath(status, old_path, new_path)`: add/delete는 한쪽이 `None`, rename/copy는 양쪽이 필수다.
- `TestCommand(command_id, argv, cwd, timeout_s)`: `argv`는 비어 있지 않은 문자열 tuple이며 shell 문자열·메타문자를 실행기로 해석하지 않는다.
- `TaskSpec(task_id, goal, acceptance, priority, reproduction, evidence, role, base_sha, write_set, shared_resource_tags, test_commands, visual_spec, forbidden_paths, protection_conditions, competitor_refs)`.
- `TaskAssignment(task, worker_provider, worker_requested_model, reviewer_provider, reviewer_requested_model, candidate_ref, clone_root, selection_reason)`: 실행 전에 확정·직렬화하며 worker/reviewer provider가 달라야 한다.
- `FileSnapshot(path, sha256, content)`, `LeaseToken(generation, fence_token, pid, acquired_at, expires_at)`.
- `AttemptRecord(run_id, task_id, attempt_id, state, generation, fence_token, base_sha, source_candidate_sha, game_result_sha, status_commit_sha, integration_candidate_sha, lease_expires_at, failure_kind, reason)`.
- `WriteAheadIntent(run_id, task_id, attempt_id, generation, fence_token, expected_old_sha, base_sha, source_candidate_sha, game_result_sha, status_commit_sha, integration_candidate_sha, review_decision_sha)`.
- `ReviewDecision(base_sha, source_candidate_sha, integration_candidate_sha, input_snapshot_sha, test_evidence_sha, visual_evidence_sha, received_attachment_hashes, reviewer, requested_model, effective_model, approved, rubric, decision_sha)`. `decision_sha`는 그 필드만 제외한 canonical JSON의 SHA-256이다.
- `ProviderContract(provider, executable, requested_model, allowed_effective_models, max_turns, deadline_s, argv)`.
- `ProviderRequest(role, bootstrap, cwd, attachments, expected_json_schema, fence_token)`, `ProviderResult(session_id, exit_code, timed_out, effective_model, event_count, output, received_attachment_hashes, log_sha256, log_path)`.
- `run.json`은 run/lease/STOP/circuit/disk/active task/최종 outcome을, 작업별 `manifest.json`은 TaskSpec·snapshot hashes·provider 선택 이유·모든 SHA를 기록한다.
- STATUS mirror는 `integration_sha`, `content_sha256`, `generated_at`, `content`를 가지며 둘 중 하나라도 현재 integration과 맞지 않으면 폐기하고 `git show <exact-sha>:docs/STATUS.md`를 정본으로 읽는다.

---

### Task 1: 계약·경로 정규화·결정론적 충돌 판정

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/__init__.py`
- Create: `projects/ai-team/scripts/autonomous_loop/contracts.py`
- Create: `projects/ai-team/scripts/autonomous_loop/paths.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_contracts.py`

**Interfaces:** 공통 직렬화 계약의 모든 dataclass/enum, `validate_repo_path()`, `parse_name_status_z()`, `validate_changed_paths()`, `repo_path_covers()`, `paths_overlap()`, `tasks_conflict()`, `select_independent_tasks(limit=3)`.

- [ ] 먼저 경로 거부 테스트를 쓴다. 절대·빈·`.`·`..`·NUL·역슬래시·glob·pathspec magic·`.git`·gitlink 하위·저장소 밖 symlink가 각각 기대한 `PathPolicyError`로 실패해야 한다.
- [ ] 테스트를 실행해 없는 모듈/심볼 때문에 RED인지 확인하고, 경로 정규화 최소 구현으로 GREEN을 만든다.
- [ ] NFC/NFD·casefold·부모/자식·공유 자원 태그 충돌 테스트를 추가해 RED를 확인한 뒤 component 단위 비교로 통과시킨다.
- [ ] `git diff --name-status -z --find-renames`의 rename/copy 양쪽과 submodule mode 160000 범위를 검사하는 테스트를 추가하고 통과시킨다.
- [ ] malformed planner JSON, task/ref ID 삽입, shell 문자열/명령 argv 삽입, worker=reviewer, forbidden/write set 겹침을 거부하는 schema 테스트를 추가해 통과시킨다.
- [ ] `(priority, task_id)` 안정 정렬과 최대 1~3개 greedy 독립 선택을 구현한다. 입력 순서와 완료 순서를 바꿔도 결과가 같은지 확인한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_contracts.py' -v`

- [ ] 통과 후 정확한 네 파일을 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/__init__.py projects/ai-team/scripts/autonomous_loop/contracts.py projects/ai-team/scripts/autonomous_loop/paths.py projects/ai-team/tests/test_autonomous_loop_contracts.py && git commit -m "기능(재와별): 작업 계약과 경로 충돌 판정 추가"
git push
```

---

### Task 2: 원자 상태·작업 lease·WAL·로그·보존 가드

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/runtime.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_runtime.py`

**Interfaces:** `atomic_write_json()`, `read_stable_snapshot()`, `CoordinatorLease`, `TaskLease`, `CoordinatorCrashBreaker`, `StateStore.transition()`, `decide_intent_recovery()`, `RunLogger`, `DiskGuard`, `safe_cleanup()`.

- [ ] atomic JSON test를 먼저 만든다. 같은 디렉터리 임시 파일 write→flush→file fsync→`os.replace`→parent fsync 순서와 replace 전 crash에서 이전 JSON 보존을 검증해 RED 후 GREEN을 만든다.
- [ ] coordinator flock FD가 프로세스 수명 동안 유지되고 lock inode를 unlink하지 않는 테스트를 만든다. PID만으로 탈취하지 못하고 heartbeat 만료+락 해제+PID 부재 뒤에만 generation이 증가해야 한다.
- [ ] 작업별 claim/heartbeat/expiry 테스트를 만든다. lease 만료는 `EXPIRED`, 재시도는 새 `attempt_id`, stale attempt/generation/fence의 전이는 모두 거부해야 한다.
- [ ] 상태 전이 표 `PLANNED→CLAIMED→RUNNING→CANDIDATE→CANDIDATE_VALIDATED→MERGE_PREPARED→MERGE_VALIDATED→COMMITTING→MERGED`와 `FAILED/EXPIRED`를 구현하고 역방향·건너뛰기를 거부한다.
- [ ] WAL 복구 테스트를 만든다. intent의 task/attempt/generation/fence와 모든 SHA 결합이 현재 state 및 candidate commit identity와 일치해야 하며, current=old면 `RETRY_CAS`, current=candidate 또는 검증된 후손이면 `MARK_MERGED`, 그 밖은 `EXPIRE`다.
- [ ] 날짜/run-id 아래 `run.json`, `events.jsonl`, `coordinator.log`, `summary.md`, 작업별 `manifest.json`, provider 원문 로그, `tests/<id>.{stdout,stderr,json}`, `visual/index.json`을 기록한다. prompt/manifest/원문을 포함해 키·토큰·Authorization·로드된 secret을 마스킹하는 테스트를 통과시킨다.
- [ ] circuit/backoff 상태가 새 coordinator 프로세스에서도 유지되는 round-trip 테스트를 추가한다.
- [ ] 처리되지 않은 coordinator 예외도 별도 persistent crash breaker에 기록한다. 연속 3회면 900초 동안 exit 75의 인프라 hold로 전환하고, 셸은 살아서 STOP을 확인하며, half-open 한 바퀴 성공 때만 닫는다.
- [ ] 성공 clone만 도달 가능성 확인 후 정리하고 실패 clone의 개수·용량을 집계한다. 경계 밖·symlink·manifest 없는 경로, 로그, 사용자 worktree는 삭제하지 않으며 한도에서 fail-closed한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_runtime.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/runtime.py projects/ai-team/tests/test_autonomous_loop_runtime.py && git commit -m "기능(재와별): 원자 상태와 작업 임대 복구 추가"
git push
```

---

### Task 3: 독립 Git 복제·검증 병합·master 동기화·CAS

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/git_ops.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_git.py`

**Interfaces:** `GitRepo.ensure_integration_ref()`, `assert_integration_not_checked_out()`, `create_isolated_clone()`, `import_candidate()`, `create_verify_worktree()`, `merge_candidate_or_abort()`, `validate_candidate_scope()`, `build_master_sync_candidate()`, `cas_integration()`, `recover_intent()`.

`runtime.decide_intent_recovery()`가 유일한 복구 판단자다. `GitRepo.recover_intent()`는 ref/조상/candidate commit identity 사실만 수집해 이 순수 함수에 한 번 위임하고, 반환된 결정을 실행할 뿐 자체 판정을 추가하지 않는다.

- [ ] 실제 임시 저장소로 `git clone --no-local --no-checkout`, origin 제거, clone 내부 common-dir, alternates 없음, base SHA checkout을 테스트한 뒤 구현한다.
- [ ] manifest에 등록한 `refs/autonomous-loop/<run>/<task>/<attempt>` 외 import, `refs/heads/master`, 사용자 branch, `autonomous/member-style` 같은 타 ref update를 명시적으로 거부한다.
- [ ] 기존 symlink/gitlink와 rename 양쪽, 범위 밖 staging residue까지 검출하는 candidate scope 테스트를 통과시킨다.
- [ ] detached verify worktree에서만 병합한다. 실제 충돌 fixture에서 즉시 `merge --abort`한 뒤 `MERGE_HEAD`·충돌 마커·dirty tree가 모두 없어야 한다.
- [ ] master가 이동한 fixture에서 integration 기준 master-sync 후보를 만들고 전체 검사/새 reviewer 승인이 없으면 CAS를 거부한다. 타 AI ref/worktree 이동은 audit event만 남기며 루프 자체가 쓴 흔적이 없으면 허용한다.
- [ ] 첫 병렬 후보 통합 후 두 번째 후보의 옛 승인이 만료되고 최신 integration에서 merge→STATUS→검사→review를 다시 요구하는 테스트를 통과시킨다.
- [ ] zero-OID 최초 생성, exact expected SHA, reviewed SHA, intent identity를 검사한 뒤 `git update-ref --create-reflog -m REASON refs/heads/autonomous/integration NEW_SHA EXPECTED_SHA` CAS를 수행한다. CAS 전/후 crash와 후속 통합 뒤 멱등 복구를 실제 ref로 검증한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_git.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/git_ops.py projects/ai-team/tests/test_autonomous_loop_git.py && git commit -m "기능(재와별): 격리 후보와 검증된 CAS 동기화 추가"
git push
```

---

### Task 4: 제공자 계약·인증 격리·OS sandbox·프로세스 그룹

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/providers.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_providers.py`

**Interfaces:** `ProviderRunner.preflight()`, `build_contract()`, `materialize_auth()`, `build_child_env()`, `render_sandbox_profile()`, `verify_sandbox_negative_controls()`, `run_provider_session()`, `verify_effective_model()`, `terminate_process_group()`.

- [ ] fake executable가 받은 argv/stdin/env와 출력 event를 기록하는 테스트를 만든다. 정확한 계약은 아래와 같다.

```text
claude -p <bootstrap> --model fable --fallback-model opus5 --no-session-persistence --permission-mode acceptEdits --output-format stream-json --verbose
codex exec --ephemeral --model gpt-5.6-sol --sandbox workspace-write --approve-for-me --json -c model_reasoning_effort="xhigh" -
grok --model grok-4.6 --no-memory --no-subagents --max-turns <N> --permission-mode auto --output-format streaming-json -p <bootstrap>
```

- [ ] `resume`, `continue`, `bypassPermissions`, `always-approve`, `danger-full-access`가 실제 호출에 들어가면 실패시킨다. Claude는 wrapper가 stream event 수로 turn 상한을 집행한다.
- [ ] Fable quota/unavailable로 분류한 event일 때만 Opus 5 effective model을 허용하고, 다른 사유의 fallback·하위 모델·모델 미확정은 실패시키는 테스트를 통과시킨다.
- [ ] provider별 필요한 인증만 0700 session HOME에 materialize한다: Claude `.claude/.credentials.json`과 필요한 설정, Codex `.codex/auth.json`/`config.toml`, Grok `.grok/auth.json`/`config.toml`. 값·경로 내용은 로그에 남기지 않고 종료 후 temp를 정리한다.
- [ ] 환경은 명시 allowlist 후 로컬 secret redactor를 거친다. `_shared`를 import하거나 전체 `os.environ`을 상속하지 않는다.
- [ ] sandbox profile은 clone/session HOME/temp만 쓰기 허용한다. root 저장소·공용 `.git`·형제 worktree·coordinator state/logs·symlink 경유 canary 쓰기가 실제로 실패하지 않으면 provider를 fail-closed 제외한다.
- [ ] `ProviderRequest.attachments`의 원본 screenshot/sprite sheet를 sandbox read allowlist와 prompt hash manifest에 넣고 fake provider가 실제 bytes/hash를 읽었는지 검증한다.
- [ ] `Popen(start_new_session=True)`로 스트림을 수집한다. deadline/STOP에는 PGID TERM→유예→KILL 후 자식 부재를 검증하고 실제 모델·이벤트 수·원문 log hash를 반환한다.
- [ ] Higgsfield/Grok Imagine은 구성된 capability command의 인증·모델·출력 provenance preflight가 성공할 때만 `IMAGE_GENERATOR` 역할 후보가 된다. silent substitute는 없다.
- [ ] Ollama clerical adapter는 planner/worker/reviewer/image/visual role 요청을 거부한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_providers.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/providers.py projects/ai-team/tests/test_autonomous_loop_providers.py && git commit -m "기능(재와별): 강한 모델과 OS 쓰기 격리 추가"
git push
```

---

### Task 5: 입력 snapshot·planner·INBOX 멱등 처리

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/planner.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_planner.py`

**Interfaces:** `InputBundle.capture(repo, integration_sha, prompt_file)`, `Planner.needs_session()`, `Planner.plan()`, `ProcessedInboxStore.contains()/record()`.

- [ ] `InputBundle` 순서 테스트를 먼저 만든다: `DIRECTIVES.md → AGENTS.md → nearest CLAUDE.md → 명시적 prompt_file → root INBOX → exact integration STATUS → GAME_WORKLOG.md/ORDERS.md(존재 시) → root DESIGN/대상 기능 명세`. source checkout과 불변 release의 PROMPT가 달라도 CLI가 넘긴 exact prompt path/hash만 사용하며, 각 안정 snapshot SHA가 planner·worker·reviewer bootstrap에 포함돼야 한다.
- [ ] STATUS mirror가 integration SHA/content hash 중 하나라도 stale이면 거부하고 `git show <fixed-sha>:docs/STATUS.md`를 읽는 테스트를 통과시킨다.
- [ ] INBOX에 schema-versioned TaskSpec JSON이 있으면 planner session을 생략하고, 자유형 지시가 있을 때만 새 frontier planner 한 번을 호출한다. idle/이미 처리한 input hash는 session 없이 반환한다.
- [ ] planner 출력의 malformed JSON, 누락 필드, 중복 task ID, 위험 경로/명령/ref ID를 contracts validator로 거부한다.
- [ ] processed INBOX hash/지시 ID는 삭제가 아니라 atomic state에 기록하며 같은 입력은 다음 바퀴에 재실행하지 않는다. INBOX가 바뀌면 새 snapshot으로 처리한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_planner.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/planner.py projects/ai-team/tests/test_autonomous_loop_planner.py && git commit -m "기능(재와별): 안정 입력과 멱등 작업 계획 추가"
git push
```

---

### Task 6: 적응형 배정·작업 lifecycle·STOP drain

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/coordinator.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_coordinator.py`

**Interfaces:** `Coordinator.preflight()`, `poll()`, `run_lap()`, `run_task()`, `drain()`, `publish_status()`.

- [ ] 한 TaskSpec이면 한 worker+다른 provider reviewer, 독립 TaskSpec이면 최대 3개 worker, 겹치는 작업은 AI 호출 전에 직렬화되는 adapter 테스트를 먼저 만든다.
- [ ] provider 선택에 role, requested/effective model, availability, circuit, `selection_reason`을 기록한다. 같은 provider 자기검토를 거부하고 frontier provider가 둘 미만이면 candidate를 보존하되 통합하지 않는다.
- [ ] 각 실행 전 `TaskAssignment`를 확정해 manifest에 저장하고 candidate ref/clone root/worker/reviewer requested model을 fencing token과 결박한다. 실행 중 배정을 바꾸면 새 attempt를 요구한다.
- [ ] `PLANNED`부터 `MERGED`까지 TaskLease heartbeat와 fencing을 결합한다. 인프라 실패만 지수 backoff 최대 2회, 연속 3회면 900초 circuit open, 재시작 뒤에도 open을 유지하고 half-open 한 세션만 허용한다.
- [ ] `run_lap()` 경계의 예상 밖 예외는 `CoordinatorCrashBreaker`에 기록하고 raw traceback은 redacted log에만 남긴다. 차단 중에는 새 AI 세션 없이 exit 75를 반환하고 STOP polling은 계속된다.
- [ ] integration race가 발생하면 옛 review를 만료시키고 최신 base에서 merge/STATUS/검사/검토를 모두 재실행한다. 작업 실패와 integration race에는 provider 불이익을 주지 않는다.
- [ ] STOP 전에는 세션 0개/exit 20, 실행 중 STOP은 신규 작업·재시도 0개, 현재 프로세스 deadline drain, 미검토 candidate 미통합, COMMITTING intent만 복구, exit 20을 검증한다.
- [ ] idle poll은 바퀴로 세지 않고, 계수된 바퀴는 서로 다른 fresh session ID를 가진 비영속 AI 세션 하나 이상을 포함해야 한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_coordinator.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/coordinator.py projects/ai-team/tests/test_autonomous_loop_coordinator.py && git commit -m "기능(재와별): 적응형 병렬 배정과 안전 drain 추가"
git push
```

---

### Task 7: 게임 실행·시각 증거·경쟁력 품질 게이트

**Files:**

- Create: `projects/ai-team/scripts/autonomous_loop/quality.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_quality.py`

**Interfaces:** `QualityRunner.run_automated_tests()`, `run_game_e2e_twice()`, `collect_visual_evidence()`, `inspect_sprite_sheet()`, `validate_asset_consumption()`, `score_review()`.

- [ ] `TestCommand.argv`를 `shell=False`로 실행하고 cwd 경계를 확인한다. 명령 삽입, timeout, 비영 exit, staging residue를 각각 실패시키는 테스트를 통과시킨다.
- [ ] Unity fixture adapter가 오너 Unity PID를 종료하지 않고 전용 lock, `unity_meas`, 격리 Library/cache를 쓰는지 검사한다. 프로젝트가 Unity가 아니면 명시적 N/A 근거를 남긴다.
- [ ] 새 E2E는 깨끗한 상태에서 연속 두 번 통과해야 한다. 테스트를 일부러 깨뜨린 negative control이 품질 게이트를 실패시키는지 확인한다.
- [ ] PNG와 `.meta`, 소비 코드/참조, 미사용·낡은 asset, cell size, frame 간격/center, PPU, 게임 내 크기, alpha/crop, 흰색 잔여 비율, 애니메이션 흔들림을 hand-checked fixture로 검사한다.
- [ ] screenshot/frame sheet 원본 bytes와 hash를 `ProviderRequest.attachments`로 reviewer에게 전달하고 reviewer가 받은 attachment hash를 응답 schema에 되돌려야 승인한다.
- [ ] 이미지 provenance에 Higgsfield/Grok Imagine, effective model, prompt hash, seed, license가 없거나 실제 게임에 연결되지 않으면 실패한다.
- [ ] 미오(디자인)·마루(게임)·별이(이미지품질) 역할 평가와 서로 다른 frontier provider 평가를 분리 기록하고 둘 다 통과해야 한다.
- [ ] 비교작 1~3개와 적용 가능한 rubric을 요구한다. critical 0, 각 적용 항목 4/5 이상, N/A 가중치 재정규화 뒤 총점 85 이상만 승인한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_quality.py' -v`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_loop/quality.py projects/ai-team/tests/test_autonomous_loop_quality.py && git commit -m "기능(재와별): 실제 게임과 시각 품질 게이트 추가"
git push
```

---

### Task 8: 체크포인트·exact-tip 검토·CLI 연결

**Files:**

- Create: `projects/ai-team/scripts/autonomous_game_loop.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_integration.py`
- Modify: `projects/ai-team/scripts/README.md`

**Interfaces:** CLI `preflight`, `run-lap --repo-root REPO_PATH --prompt-file PROMPT_PATH`, `smoke --repo-root FIXTURE_PATH --evidence-root EVIDENCE_PATH --laps 2 --max-turns 4 --deadline 300`, `status`, `install-launchd`; 내부 `execute_verified_task()`.

- [ ] 호출 순서 테스트를 먼저 만든다: 읽기→한 작업 구현→자동검사→exact-file checkpoint commit→실제 게임/육안→필요한 수정의 새 검사·새 커밋→최신 integration에 merge+STATUS 합성→전체 재검사/E2E→다른 provider exact-tip review→WAL→CAS.
- [ ] 자동검사 전 commit, 첫 checkpoint 전 시각 실행, 검토 뒤 tip 변경, review SHA와 CAS SHA 불일치가 각각 실패하는 negative test를 만든다.
- [ ] worker clone에서 허용 경로만 stage하고 즉시 checkpoint commit한다. 시각 확인 뒤 변경이 생기면 regression test와 두 번째 commit을 요구한다.
- [ ] 코디네이터가 최신 integration base에 game result와 STATUS를 합성한 `integration_candidate_sha`를 만든다. reviewer는 그 exact SHA, input/test/visual evidence hash와 실제 수신 attachment hash를 승인해야 한다.
- [ ] master sync도 동일한 exact-tip 품질 경로를 사용한다. integration ref가 움직이면 승인 만료 후 처음부터 재구성한다.
- [ ] CLI 옵션과 exit-code mapping을 구현하고 `install-launchd`는 Task 11의 배포 모듈로 forwarding만 하도록 둔다.
- [ ] scripts README에 명령, 로그, 소유 ref, root/master 비수정 경계를 기록한다.

Run: `python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_integration.py' -v`

Run: `python3 projects/ai-team/scripts/autonomous_game_loop.py --help`

- [ ] 통과 후 커밋·push한다.

```bash
git add -- projects/ai-team/scripts/autonomous_game_loop.py projects/ai-team/tests/test_autonomous_loop_integration.py projects/ai-team/scripts/README.md && git commit -m "기능(재와별): exact-tip 검토와 루프 CLI 연결"
git push
```

---

### Task 9: 얇은 무한 셸 루프·설정·5절 지시서

**Files:**

- Modify: `loop/loop.sh`
- Modify: `loop/env.sh`
- Modify: `loop/PROMPT.md`
- Modify: `loop/test_loop_agent.sh`

**Launcher contract:** `loop.sh [TARGET_REPO]`; 기본 target은 source checkout root다. 배포본에서는 plist가 `/Users/junholee/ai_lab`을 첫 인자로 넘긴다. `DEPLOY_ROOT`는 script 위치, `TARGET_REPO`는 인자 canonical path이며 절대 섞지 않는다. CLI 호출은 `run-lap --repo-root "$TARGET_REPO" --prompt-file "$DEPLOY_ROOT/PROMPT.md"`다.

- [ ] fixture에 env/PROMPT/coordinator stub을 포함하고 경로에 공백을 넣는다. TARGET_REPO/prompt 인자 보존, STOP 선검사, idle 비계수, 매 바퀴 새 PID/session, 최대 바퀴, fatal 전달, cooldown 중 STOP을 동작으로 검증한다.
- [ ] 기존 셸에서 기대한 이유로 RED인지 확인한 뒤 provider 직접 호출이 없는 얇은 반복기로 교체한다.
- [ ] `10/75`는 계수하지 않고 짧은 STOP-aware poll, `20`/STOP은 drain 뒤 0, fatal은 그대로 전달한다. 고정 sleep 중에도 STOP을 확인한다.
- [ ] env에 모든 Global Constraints 설정과 절대 PATH를 둔다. 단일 `LOOP_AGENT`/구형 Sonnet/default weak model을 제거하고 Python preflight가 정수·범위를 fail-closed 검증한다.
- [ ] PROMPT는 정확히 다섯 절이다. 안전 지침·입력 순서, manifest/write set, Higgsfield/Grok Imagine, 정량 rubric, 자기검토 금지를 담는다.
- [ ] 4절 순서는 `읽기 → 하나만 만들기 → 자동검사 → 화면 보기 전 exact-file 체크포인트 → 실제 실행·육안 → 필요 시 새 검사·커밋 → 통합 재검사 → 다른 provider exact-tip 검토`로 고정한다.
- [ ] root/master/타 worktree/coordinator state/log 쓰기, STATUS 직접 수정, resume/continue, 범위 확대를 금지한다.

```bash
bash -n loop/loop.sh loop/env.sh loop/test_loop_agent.sh
bash loop/test_loop_agent.sh
git add -- loop/loop.sh loop/env.sh loop/PROMPT.md loop/test_loop_agent.sh && git commit -m "기능(재와별): 새 세션 병렬 셸 루프와 지시서 연결"
git push
```

---

### Task 10: integration STATUS 보드·원본 문서 보존·AI 정책 정합화

**Files:**

- Modify: `loop/board.py`
- Modify: `loop/test_board.py`
- Modify: `loop/board.html`
- Modify: `docs/DESIGN.md`
- Modify: `docs/STATUS.md`
- Modify: `docs/feedback/INBOX.md`
- Create: `docs/archive/legacy_loop_docs_20260823/DESIGN.md`
- Create: `docs/archive/legacy_loop_docs_20260823/STATUS.md`
- Create: `docs/archive/legacy_loop_docs_20260823/INBOX.md`
- Create: `docs/archive/legacy_loop_docs_20260823/README.md`
- Modify: `DIRECTIVES.md`
- Modify: `AGENTS.md`
- Modify: `CLAUDE.md`
- Modify: `projects/ashes-to-stars/CLAUDE.md`
- Modify: `docs/AI_LAB_SYSTEM_ARCHITECTURE.md`

- [ ] board가 exact integration SHA의 STATUS만 정본으로 쓰고 동일 SHA/hash mirror만 cache hit하는 테스트를 먼저 만든다. stale mirror는 즉시 폐기하며 root STATUS는 integration 미생성 때만 초기 템플릿 fallback이다.
- [ ] `apply_decision()`은 INBOX/board decision만 기록하고 root STATUS hash를 보존한다. `start_loop()`는 TARGET_REPO를 넘기고, PID 탐지는 배포 launcher/Python coordinator를 모두 찾는다.
- [ ] `logs/YYYY-MM-DD/<run-id>/run.json`에서 여러 active task/provider/effective model, failure kind, circuit, 재검 시각, 실행별 용량, 디스크 경고, STATUS 미갱신 사유를 표시한다. 구형 로그는 읽기 fallback만 유지한다.
- [ ] HOLD UI는 coordinator가 소비하는 `no-new-work` drain 상태로 연결하거나 제거한다. 표시만 되고 무시되는 상태를 금지한다.
- [ ] 기존 `tee -a "$MAIN_LOG"`·내장 “상시 폴리싱” 문자열 기대를 실제 구조화 상태 동작 테스트로 바꾼다.
- [ ] `bba0f17b^`의 DESIGN/STATUS/INBOX 전체 원문을 세 archive 파일에 byte-exact 보존하고 full 40자 commit/blob SHA와 `git cat-file -e`, hash/복구 명령을 README에 기록한다. 빈 운영 템플릿에는 옛 내용을 섞지 않는다.
- [ ] DESIGN은 오너 전용, STATUS는 `## 다음 할 일`, INBOX는 `## 대기 중`을 포함한다. INBOX/DESIGN은 루프가 자동 수정·삭제하지 않는다.
- [ ] 상위 지침을 좁은 예외로 정리한다. 일반 Ollama/haiku 정책은 유지하되 이 게임 루프는 frontier 모델·integration-only·dated logs를 따른다. 옛 1-session/autopilot/Grok Imagine 금지/“Suri만 master 쓰기” 문구는 역사적 범위로 한정한다.

```bash
python3 loop/test_board.py
python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_*.py'
git add -- loop/board.py loop/test_board.py loop/board.html docs/DESIGN.md docs/STATUS.md docs/feedback/INBOX.md docs/archive/legacy_loop_docs_20260823/DESIGN.md docs/archive/legacy_loop_docs_20260823/STATUS.md docs/archive/legacy_loop_docs_20260823/INBOX.md docs/archive/legacy_loop_docs_20260823/README.md DIRECTIVES.md AGENTS.md CLAUDE.md projects/ashes-to-stars/CLAUDE.md docs/AI_LAB_SYSTEM_ARCHITECTURE.md && git commit -m "문서(재와별): 다중 AI 상태 원천과 기존 기억 보존"
git push
```

---

### Task 11: smoke fixture·불변 release·비활성 launchd 배포·README

**Files:**

- Modify: `loop/com.ailab.autonomous_loop.plist`
- Create: `projects/ai-team/scripts/autonomous_loop/deployment.py`
- Create: `projects/ai-team/scripts/install_autonomous_loop_launchd.py`
- Create: `projects/ai-team/scripts/create_autonomous_loop_smoke_fixture.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_launchd.py`
- Create: `projects/ai-team/tests/test_autonomous_loop_smoke_fixture.py`
- Modify: `README.md`

**Deployment contract:** source repo `/Users/junholee/ai_lab`은 object DB 읽기만 한다. installer 인자는 `--source-repo`, `--release-sha`, `--target-repo`, `--no-load`다. release는 `/Users/junholee/Library/Application Support/AI Lab Autonomous Loop/releases/<40-char-sha>/`, current는 그 release를 가리키는 원자 symlink, 설치 plist는 `/Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist`다.

- [ ] tracked plist는 이 기계의 완성된 절대경로를 이미 담고 installer가 수정하지 않는다. ProgramArguments는 `/Users/junholee/Library/Application Support/AI Lab Autonomous Loop/current/loop/loop.sh`, `/Users/junholee/ai_lab`; WorkingDirectory는 target repo다. RunAtLoad=true, KeepAlive.SuccessfulExit=false, ThrottleInterval=60, 절대 PATH, 정적 bootstrap stdout/stderr 경로를 검사한다.
- [ ] 배포 manifest에 `loop.sh`, `env.sh`, `PROMPT.md`, Python entrypoint와 `autonomous_loop/*.py` 전체, source plist hash를 명시한다. `_shared` 없이 release 디렉터리만으로 preflight/help가 실행돼야 한다.
- [ ] installer는 `git show RELEASE_SHA:REPO_RELATIVE_PATH`로 검증된 commit에서만 추출한다. staging hash 검증→불변 `releases/RELEASE_SHA` publish→plist temp lint→로그 부모 생성/쓰기 canary→설치 plist 원자 교체→임시 symlink rename으로 current 전환 순서를 테스트한다.
- [ ] 중간 실패 시 이전 plist/current가 그대로이고 새 불완전 release는 active가 아니다. 이전 release는 rollback용으로 보존한다. source worktree status/canary는 전후 동일하다.
- [ ] source commit의 plist bytes와 설치본이 동일하고 mode 0644인지 확인한다. installer와 CLI subcommand는 같은 `deployment.install()`만 호출한다.
- [ ] 설치기는 launchd가 현재 등록돼 있으면 거부하고 `bootstrap`, `load`, `kickstart`를 어떤 경로에서도 호출하지 않는다. bootstrap 로그 부모는 미리 만들며 날짜/run-id 로그는 wrapper/coordinator가 별도로 만든다.
- [ ] fixture builder는 작은 게임 UI JSON, stdlib PNG renderer, tests, DESIGN/STATUS/INBOX와 두 lap의 schema-versioned task manifests를 만든다. lap 1은 단일 HUD task, lap 2는 독립 캐릭터/scene 두 task와 겹치는 세 번째 task를 포함한다.
- [ ] fixture renderer가 실제 PNG를 만들고 negative control이 실패하는지, 배포 경로에 공백이 있어도 release launcher가 명시한 fixture TARGET_REPO와 PROMPT만 사용하는지 검사한다.
- [ ] README에 만든 파일 목록, foreground, 설치, 켜기(`bootstrap`), STOP drain, 등록 해제(`bootout`), 재개, 상태, integration STATUS, 날짜/run-id 로그, 회로·디스크 보류, 실패 증거 수동 정리, smoke 조회법을 한국어로 기록한다. 정상 운영 `pkill`은 제거한다.

```bash
python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_launchd.py' -v
python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_smoke_fixture.py' -v
plutil -lint loop/com.ailab.autonomous_loop.plist
git add -- loop/com.ailab.autonomous_loop.plist projects/ai-team/scripts/autonomous_loop/deployment.py projects/ai-team/scripts/install_autonomous_loop_launchd.py projects/ai-team/scripts/create_autonomous_loop_smoke_fixture.py projects/ai-team/tests/test_autonomous_loop_launchd.py projects/ai-team/tests/test_autonomous_loop_smoke_fixture.py README.md && git commit -m "기능(재와별): 검증 release와 비활성 launchd 배포 추가"
git push
```

---

### Task 12: 독립 코드 검토·전체 회귀·두 실제 smoke 바퀴·설치

**Files:**

- Create: `docs/verification/autonomous-loop-smoke-2026-08-23.md`
- Runtime only: `/Users/junholee/ai_lab/logs/YYYY-MM-DD/<run-id>/`
- Runtime only: `/Users/junholee/ai_lab/output/cache/autonomous_loop/`
- External installed artifacts: release/current 경로와 설치 plist
- 결함 발견 시만 Tasks 1–11 소유 파일과 해당 regression test 수정

- [ ] 요구사항 검토자와 코드 품질 검토자를 서로 다른 subagent로 실행한다. 발견 사항은 재현 regression test를 먼저 실패시킨 뒤 fixer가 수정·관련 검사·커밋하고 scoped re-review를 통과시킨다.
- [ ] fake-provider 전체 경로에서 단일 task, 두 독립 병렬 task, 겹치는 세 번째 task의 호출 전 직렬화, self-review 거부, STOP drain, WAL crash, master 이동 재검토를 확인한다.
- [ ] 전체 자동 검사를 새로 실행한다.

```bash
bash -n loop/loop.sh loop/env.sh loop/test_loop_agent.sh
bash loop/test_loop_agent.sh
python3 loop/test_board.py
python3 -m unittest discover -s projects/ai-team/tests -p 'test_autonomous_loop_*.py' -v
python3 -m unittest discover -s projects/ai-team/tests -p 'test_*.py'
python3 projects/ai-team/harness/check_all.py
```

- [ ] fixture를 `mktemp -d` 아래 생성하고 실제 provider preflight를 통과시킨다. production coordinator/adapters를 그대로 쓰며 evidence root는 fixture 밖 `/Users/junholee/ai_lab/logs`로 고정한다.

```bash
SMOKE_FIXTURE_DIR="$(mktemp -d /tmp/ashes-stars-loop-smoke.XXXXXX)"
python3 projects/ai-team/scripts/create_autonomous_loop_smoke_fixture.py --destination "$SMOKE_FIXTURE_DIR"
python3 projects/ai-team/scripts/autonomous_game_loop.py smoke --repo-root "$SMOKE_FIXTURE_DIR" --evidence-root /Users/junholee/ai_lab/logs --laps 2 --max-turns 4 --deadline 300
```

- [ ] 1바퀴는 한 worker+다른 reviewer, 2바퀴는 두 독립 worker 병렬 실행과 겹치는 세 번째 task의 AI 호출 전 배제를 확인한다. 실제 강한 provider가 둘 미만이면 성공을 꾸미지 않고 인프라 보류하며 설치하지 않는다.
- [ ] fixture를 제거한 뒤에도 두 run의 `run.json`, task manifest, fresh session/effective model, base/source/game/status/integration SHA, tests 원문, PNG/visual hash, reviewer decision, CAS 결과가 evidence root에 남고 hash가 맞는지 확인한다.
- [ ] smoke 성공 뒤 외부 동시 변경이 없는 짧은 검증 구간에서 root/master canary를 비교한다. 타 AI ref/worktree 변경은 audit만 하고, 루프 소유 ref 외 쓰기 흔적이 있으면 실패한다.
- [ ] smoke 결과와 full SHA/hash/로그 경로를 날짜별 검증 보고서에 기록하고 커밋·push한다. smoke에서 코드 결함을 고쳤다면 보고서 커밋 전에 전체 회귀와 하네스를 다시 실행한다.

```bash
git add -- docs/verification/autonomous-loop-smoke-2026-08-23.md && git commit -m "검증(재와별): 자율 루프 실제 두 바퀴 증거 기록"
git push
```

- [ ] 검증 보고서까지 포함한 HEAD에서 전체 회귀와 하네스를 다시 통과시킨 뒤 `RELEASE_SHA=$(git rev-parse HEAD)`를 고정한다. 같은 SHA를 최초 `autonomous/integration` bootstrap 후보로 exact CAS한다. 이미 ref가 있으면 expected old를 명시하고 전체 sync/review 경로를 거친다. integration은 checkout하지 않는다.
- [ ] 고정한 RELEASE_SHA를 installer에 명시해 `--no-load`로 설치한다. installer는 source/target repo를 쓰지 않는다.

```bash
python3 projects/ai-team/scripts/install_autonomous_loop_launchd.py --source-repo /Users/junholee/ai_lab --release-sha "$(git rev-parse HEAD)" --target-repo /Users/junholee/ai_lab --no-load
plutil -lint loop/com.ailab.autonomous_loop.plist
plutil -lint /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
cmp -s loop/com.ailab.autonomous_loop.plist /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
shasum -a 256 loop/com.ailab.autonomous_loop.plist /Users/junholee/Library/LaunchAgents/com.ailab.autonomous_loop.plist
if launchctl print "gui/$(id -u)/com.ailab.autonomous_loop" >/dev/null 2>&1; then echo "ERROR: service unexpectedly loaded" >&2; exit 1; fi
```

Expected: 두 plist lint `OK`, bytes/hash 동일, launchd 미등록, loop/coordinator 프로세스 없음.

- [ ] 최종 전체 회귀를 설치 후 한 번 더 실행하고 local/upstream SHA 동일성을 확인한다.

```bash
python3 -m unittest discover -s projects/ai-team/tests -p 'test_*.py'
python3 projects/ai-team/harness/check_all.py
git status --short --branch
git push
git rev-parse HEAD
git rev-parse @{u}
```

---

## 명세 추적표

| 설계 절 | 구현 작업 |
|---|---|
| 1 목적·합격 기준 | 7, 8, 9, 12 |
| 2 지침 권위·읽기 순서 | 5, 9, 10 |
| 3 전체 구조·바퀴 의미 | 2, 6, 8, 9 |
| 4 역할·모델 정책 | 4, 5, 6 |
| 5 작업 명세·충돌 방지 | 1, 3, 5, 6 |
| 6 상태 기계·원자성 | 2, 3, 6, 8 |
| 7 구현·검사·통합 순서 | 3, 7, 8, 9 |
| 8 게임·이미지 품질 | 4, 7, 12 |
| 9 토큰 절약 | 1, 4, 5, 6 |
| 10 실패·정지·보안·보존 | 2, 3, 4, 6 |
| 11 로그·관찰 가능성 | 2, 6, 10, 12 |
| 12 launchd | 9, 11, 12 |
| 13 테스트 전략 | 1–12 |
| 14 문서·데이터 이행 | 10, 11 |
| 15 최종 인수 조건 | 12 |

## 완료 판정

- 모든 Task checkbox와 명세 추적표에 실제 증거가 있다.
- production 코드에 `danger-full-access`, `bypassPermissions`, session resume/continue가 없고 테스트는 실제 호출 argv/side effect로 이를 증명한다.
- requested/effective model, worker/reviewer 분리, exact reviewed SHA, evidence hash, WAL/CAS 결과를 구조화 로그에서 재검증할 수 있다.
- 루프가 master·사용자 작업트리·타 AI ref/worktree·공용 Git config/hooks를 쓰지 않았고, 합법적 외부 변경은 audit로 구분된다.
- 두 실제 smoke 바퀴가 성공하고 로그가 fixture 밖에 보존된다.
- source/설치 plist는 동일하지만 서비스는 미등록·미실행이며, 다음 로그인 또는 사용자의 명시적 bootstrap 때만 시작한다.
