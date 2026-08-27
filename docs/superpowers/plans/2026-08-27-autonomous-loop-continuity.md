# 재와 별 자율 개발 루프 연속성 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 오너의 한 번의 시작 지시로 자율 개발 루프가 즉시 실행되고, 사용량 한도에는 무료 대기하며, 오류에는 중복 AI 호출 없이 복구한 뒤 계속 진행하게 한다.

**Architecture:** `control.sh`이 시작·중단의 단일 진입점이 되고 launchd는 저장소 원본을 직접 실행한다. `runtime_state.py`가 `running|quota_wait|recovering|owner_stopped` 상태, heartbeat, 오류 지문을 원자적으로 관리하며 `loop.sh`과 `loop_watch.sh`은 이 상태만 공유한다. 정상 개발은 바퀴당 AI 한 번이고 council·speed lane·건강한 board keeper의 자동 AI 호출은 기본 0회다.

**Tech Stack:** Bash 3.2(macOS), Python 3 표준 라이브러리, launchd plist, `unittest`, 기존 셸 테스트

**Spec:** `docs/superpowers/specs/2026-08-27-autonomous-loop-continuity-design.md`

## Global Constraints

- 운영 플랫폼은 macOS(`darwin`)뿐이다.
- 운영 우선순위는 코인 최소화 > 연속 실행 > 속도다.
- `STOP`은 오너의 명시적 중단 경로만 생성할 수 있다.
- 사용량 대기·heartbeat·감시에는 AI CLI를 호출하지 않는다.
- 정상 개발은 한 바퀴에 AI 세션 하나만 호출한다.
- `_shared/`와 `projects/ai-team/harness/`는 수정하지 않는다.
- 기존 작업 트리의 사용자 변경을 보존하고 대상 파일의 기존 diff 위에 좁게 패치한다.
- `git add`와 `git commit`은 파일을 명시해 한 호흡으로 실행한다.
- 구현 전 `loop/STOP_LANE`을 만들고 speed lane 프로세스의 실제 종료를 확인한다. 메인 루프의 기존 `loop/STOP`은 테스트·배포 완료 전까지 유지한다.

---

## 파일 책임 지도

- `loop/runtime_state.py`: 런타임 JSON 원자 쓰기, heartbeat, 오류 분류·지문·복구 1회 claim
- `loop/test_runtime_state.py`: 상태 파일과 오류 분류 네거티브 컨트롤
- `loop/control.sh`: 모든 AI·보드가 부르는 `start|stop|status` 명령
- `loop/test_control.sh`: STOP/HOLD 해제, 멱등 시작, 실제 시작 확인 실패 검증
- `loop/loop.sh`: 정상 lap, 무료 quota wait, 1회 recovery lap을 실행하는 상태 머신
- `loop/test_loop_continuity.sh`: 한도 대기 0 AI 호출, 오류 복구 1회, STOP 비생성 검증
- `loop/env.sh`: 자동 회의 0, 긴 무료 재확인 간격, heartbeat 간격의 기본값
- `loop/loop_watch.sh`: 상태+heartbeat 기반 재개 판정
- `loop/test_loop_watch.sh`: quota wait 비재시작, running 정체 복구, owner stop 존중
- `loop/deploy_launchd.sh`: 저장소 원본 plist 등록과 speed lane 상시 중단
- `loop/com.ailab.autonomous_loop.plist`: 저장소 `loop/loop.sh` 직접 실행
- `loop/com.ailab.speedlane.plist`: `RunAtLoad=false`, 상시 재기동 제거
- `loop/board.py`, `loop/test_board.py`: 보드 재개를 `control.sh start`로 통일
- `loop/board_keeper.sh`, `loop/test_board_keeper.sh`: 건강한 정기 개선 제거, 오류 지문당 수리 AI 1회
- `loop/README.md`: 단일 시작·중단·상태·비용 정책
- `.gitignore`: `loop/runtime_state.json`, 복구 입력·claim 런타임 파일 제외

---

### Task 1: 런타임 상태와 오류 지문

**Files:**
- Create: `loop/runtime_state.py`
- Create: `loop/test_runtime_state.py`
- Modify: `.gitignore:3-16`

**Interfaces:**
- Consumes: JSON 파일 경로, provider 이름, phase, 로그 파일, 종료 코드
- Produces: `read_state(path) -> dict`, `update_state(path, **changes) -> dict`, `classify_failure(log_tail, exit_code) -> str`, `error_fingerprint(provider, exit_code, log_tail, context_version) -> str`, `claim_recovery(path, fingerprint) -> bool`
- CLI: `runtime_state.py --path PATH set PHASE --provider NAME --reason TEXT --retry-at EPOCH`, `heartbeat`, `get FIELD`, `classify --log FILE --exit-code N`, `claim FINGERPRINT`

- [ ] **Step 1: 실패 테스트 작성**

```python
class RuntimeStateTests(unittest.TestCase):
    def test_update_is_atomic_and_preserves_recovery_claims(self):
        state.update_state(self.path, phase="quota_wait", provider="claude")
        self.assertTrue(state.claim_recovery(self.path, "abc"))
        self.assertFalse(state.claim_recovery(self.path, "abc"))
        self.assertEqual(state.read_state(self.path)["phase"], "quota_wait")

    def test_usage_limit_is_quota_but_org_access_denial_is_error(self):
        self.assertEqual(state.classify_failure("usage limit reached", 1), "quota")
        self.assertEqual(
            state.classify_failure("organization has disabled subscription access", 1),
            "error",
        )

    def test_success_is_not_failure(self):
        self.assertEqual(state.classify_failure("", 0), "ok")
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `python3 -m unittest loop/test_runtime_state.py -v`  
Expected: `ModuleNotFoundError` 또는 필요한 함수가 없어 FAIL

- [ ] **Step 3: 최소 구현 작성**

```python
DEFAULT = {
    "phase": "owner_stopped",
    "provider": "",
    "heartbeat_at": 0,
    "reason": "",
    "retry_at": 0,
    "last_error_fingerprint": "",
    "recovery_claims": [],
}

QUOTA_RE = re.compile(
    r"usage limit|quota exceeded|rate limit exceeded|out of credits|사용량.*(?:소진|초과)",
    re.I,
)

def classify_failure(log_tail: str, exit_code: int) -> str:
    if exit_code == 0:
        return "ok"
    return "quota" if QUOTA_RE.search(log_tail) else "error"
```

`update_state`는 같은 디렉터리의 임시 파일에 UTF-8 JSON을 쓴 뒤 `os.replace`로 교체한다. `claim_recovery`는 최근 지문 최대 32개를 유지하고 처음 본 지문에만 `True`를 반환한다.

- [ ] **Step 4: 표적 테스트 통과 확인**

Run: `python3 -m unittest loop/test_runtime_state.py -v`  
Expected: 모든 테스트 `ok`

- [ ] **Step 5: 런타임 파일 ignore 확인**

Run: `git check-ignore loop/runtime_state.json loop/recovery_context.log`  
Expected: 두 경로 모두 출력

- [ ] **Step 6: 독립 커밋**

```bash
git add -- .gitignore loop/runtime_state.py loop/test_runtime_state.py
git commit -m "자율루프: 런타임 상태와 오류 지문 추가" -- .gitignore loop/runtime_state.py loop/test_runtime_state.py
```

---

### Task 2: 루프가 한도·오류 때문에 영구 종료하지 않게 변경

**Files:**
- Create: `loop/test_loop_continuity.sh`
- Modify: `loop/loop.sh:24-63,115-127,141-248,407-613`
- Modify: `loop/env.sh:4-65`
- Modify: `loop/test_loop_agent.sh:1-160`

**Interfaces:**
- Consumes: Task 1의 `runtime_state.py` CLI
- Produces: `runtime_set`, `runtime_heartbeat`, `classify_lap`, `wait_for_quota`, `select_recovery_agent`, `run_recovery_session`
- 상태 전이: `running -> quota_wait -> running`, `running -> recovering -> running`

- [ ] **Step 1: 한도와 오류 연속성 실패 테스트 작성**

`test_loop_continuity.sh`에 가짜 AI 실행기를 만들고 다음을 검증한다.

```bash
# quota 모드는 첫 호출만 usage limit을 내고, 무료 usage 조회 파일이 ok로 바뀐 뒤 두 번째 호출에서
# STATUS.md를 갱신한다. LOOP_MAX_LOOPS=1로 끝낸다.
if [ "$(cat "$TEST_ROOT/ai_calls")" -ne 2 ]; then
  echo "FAIL: 한도 전 1회 + 회복 후 정상 1회가 아니다"; exit 1
fi
test ! -e "$TEST_ROOT/loop/STOP"
test "$(python3 "$TEST_ROOT/loop/runtime_state.py" --path "$STATE" get phase)" = running

# 동일 일반 오류를 세 번 주입해도 STOP은 없어야 하며 recovery claim은 하나여야 한다.
test ! -e "$TEST_ROOT/loop/STOP"
test "$(cat "$TEST_ROOT/recovery_calls")" -eq 1
```

- [ ] **Step 2: 기존 종료 동작 때문에 실패하는지 확인**

Run: `bash loop/test_loop_continuity.sh`  
Expected: 기존 `MAX_FAILS` 분기가 `STOP`을 만들거나 종료하여 FAIL

- [ ] **Step 3: 실행기 선택을 단일 provider로 단순화**

`LOOP_PROVIDERS_CHAIN`, `other_provider`, 정상 개발용 provider 회전을 제거한다. `pick_agent`가 고른 provider를 유지하고, `usage_check` 결과가 `exhausted`이면 `quota_wait`로만 전이한다. 기본값은 다음으로 고정한다.

```bash
export LOOP_COUNCIL_EVERY="${LOOP_COUNCIL_EVERY:-0}"
export PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
export LOOP_HEARTBEAT_SECONDS="${LOOP_HEARTBEAT_SECONDS:-30}"
export LOOP_RECOVERY_RETRY_SECONDS="${LOOP_RECOVERY_RETRY_SECONDS:-900}"
```

- [ ] **Step 4: 무료 대기와 heartbeat 구현**

```bash
wait_with_heartbeat() {
  local remaining="$1"
  while [ "$remaining" -gt 0 ]; do
    [ -f "$STOP_FILE" ] && return 1
    runtime_heartbeat
    step="$HEARTBEAT_SECONDS"
    [ "$remaining" -lt "$step" ] && step="$remaining"
    sleep "$step"
    remaining=$((remaining - step))
  done
}
```

한도 로그를 판정하면 `runtime_set quota_wait` 후 `usage_check`만 호출한다. 시험 프롬프트나 다른 AI CLI는 호출하지 않는다. 회복되면 `runtime_set running` 후 같은 provider로 새 정상 lap을 한 번 연다.

- [ ] **Step 5: 일반 오류의 1회 복구 구현**

오류 로그 끝 80줄과 provider·exit code·현재 Git HEAD로 지문을 만든다. 코드가 바뀌면 새 정보로
판정하되 같은 HEAD의 동일 오류는 다시 과금하지 않는다. 먼저 결정론적 로컬 진단을 하고, 코드
판단이 필요하며 Ollama가 살아 있으면 짧은 오류 문맥으로 로컬 진단을 한 번 수행한다. 실제 파일
수정 도구가 필요한 경우에만 같은 provider가 실행 가능하면 같은 provider, 실행기 자체 오류면
설치된 `codex`, `claude`, `grok`, `opencode` 중 현재 provider가 아닌 첫 실행기를 복구용으로 한
번만 사용한다. 복구 프롬프트는 아래 고정 머리말과 오류 문맥만 포함한다.

```text
너는 자율 루프 오류 복구 세션이다. 새 기능을 만들지 않는다.
아래 오류의 근본 원인을 직접 확인하고, 관련 파일만 수정하고, 재현 테스트를 통과시켜라.
성공하면 고친 파일만 즉시 커밋하라. 게임 개발 작업은 시작하지 마라.
```

이미 claim된 동일 지문이면 AI를 부르지 않고 `LOOP_RECOVERY_RETRY_SECONDS` 동안 heartbeat만 갱신한다. 루프 본체 수정 성공 시 현재 프로세스는 exit 75로 끝내고 launchd가 저장소 새 원본을 다시 실행하게 한다. 이 종료는 `STOP`을 만들지 않는다.

- [ ] **Step 6: 자동 다중 AI 호출 제거**

바퀴 수 기반 `COUNCIL_EVERY` 실행 분기를 제거하고 `COUNCIL_NOW` 명시 신호만 유지한다. `merge_integration.sh`은 AI를 호출하지 않으므로 남아 있는 승인 커밋 정리를 위해 유지한다.

- [ ] **Step 7: 표적 테스트 통과 확인**

Run: `bash loop/test_loop_agent.sh && bash loop/test_loop_continuity.sh && bash loop/test_infra_detect.sh`  
Expected: 세 스크립트 모두 exit 0, 테스트 중 `STOP` 생성 없음

- [ ] **Step 8: 독립 커밋**

```bash
git add -- loop/env.sh loop/loop.sh loop/test_loop_agent.sh loop/test_loop_continuity.sh
git commit -m "자율루프: 한도 대기와 오류 복구 후 계속 진행" -- loop/env.sh loop/loop.sh loop/test_loop_agent.sh loop/test_loop_continuity.sh
```

---

### Task 3: 어느 AI에서든 쓰는 단일 시작 명령

**Files:**
- Create: `loop/control.sh`
- Create: `loop/test_control.sh`
- Modify: `loop/deploy_launchd.sh:1-75`
- Modify: `loop/com.ailab.autonomous_loop.plist:8-38`
- Modify: `loop/com.ailab.speedlane.plist:8-29`

**Interfaces:**
- Consumes: `control.sh start [claude|codex|grok|opencode]`, `control.sh stop`, `control.sh status`
- Produces: launchd 단일 PID, 저장소 원본 실행, JSON 런타임 상태

- [ ] **Step 1: 멱등 시작 실패 테스트 작성**

가짜 `launchctl`이 `bootout`, `bootstrap`, `kickstart`, `print` 호출을 기록하게 하고 다음을 검증한다.

```bash
touch "$TEST_ROOT/loop/STOP" "$TEST_ROOT/loop/HOLD"
LOOP_CONTROL_ROOT="$TEST_ROOT" LOOP_LAUNCHCTL_BIN="$TEST_ROOT/bin/launchctl" \
  bash "$TEST_ROOT/loop/control.sh" start codex
test ! -e "$TEST_ROOT/loop/STOP"
test ! -e "$TEST_ROOT/loop/HOLD"
test "$(cat "$TEST_ROOT/loop/agent")" = codex
grep -q 'bootstrap' "$TEST_ROOT/launchctl.calls"

# 두 번째 start는 기존 PID를 확인하고 bootstrap 횟수를 늘리지 않는다.
before="$(grep -c bootstrap "$TEST_ROOT/launchctl.calls")"
LOOP_CONTROL_ROOT="$TEST_ROOT" LOOP_LAUNCHCTL_BIN="$TEST_ROOT/bin/launchctl" \
  bash "$TEST_ROOT/loop/control.sh" start codex
after="$(grep -c bootstrap "$TEST_ROOT/launchctl.calls")"
test "$before" = "$after"
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `bash loop/test_control.sh`  
Expected: `loop/control.sh` 부재로 FAIL

- [ ] **Step 3: `control.sh` 최소 구현**

`start`는 STOP/HOLD 해제, 선택 provider 기록, `deploy_launchd.sh --register-only`, 현재 서비스 확인, 필요 시 bootstrap/kickstart, 최대 15초 heartbeat/PID 확인을 수행한다. `stop`만 STOP을 만들고 현재 바퀴의 정상 종료를 기다린다. `status`는 phase·provider·launchd PID를 출력한다.

```bash
case "$ACTION" in
  start) start_loop "${2:-}" ;;
  stop) stop_loop ;;
  status) show_status ;;
  *) echo "사용법: bash loop/control.sh <start|stop|status> [provider]" >&2; exit 2 ;;
esac
```

- [ ] **Step 4: launchd가 저장소 원본을 직접 실행하게 수정**

`com.ailab.autonomous_loop.plist`의 두 번째 ProgramArgument를 `/Users/junholee/ai_lab/loop/loop.sh`로 바꾼다. `deploy_launchd.sh`는 Application Support에 실행 스크립트를 복사하지 않고 plist만 LaunchAgents에 설치한다. `--register-only`는 실행 상태를 바꾸지 않는다.

speed lane plist는 `RunAtLoad=false`로 바꾸고 `KeepAlive`를 제거한다. 배포 시 기존 `com.ailab.speedlane`을 실제로 bootout하되 `STOP_LANE`은 유지한다.

- [ ] **Step 5: 표적 테스트와 plist 문법 확인**

Run: `bash loop/test_control.sh && plutil -lint loop/com.ailab.autonomous_loop.plist loop/com.ailab.speedlane.plist`  
Expected: 테스트 PASS, 두 plist 모두 `OK`

- [ ] **Step 6: 독립 커밋**

```bash
git add -- loop/control.sh loop/test_control.sh loop/deploy_launchd.sh loop/com.ailab.autonomous_loop.plist loop/com.ailab.speedlane.plist
git commit -m "자율루프: 단일 시작 명령과 원본 직접 실행" -- loop/control.sh loop/test_control.sh loop/deploy_launchd.sh loop/com.ailab.autonomous_loop.plist loop/com.ailab.speedlane.plist
```

---

### Task 4: 상태 기반 감시와 보드 재개 통일

**Files:**
- Modify: `loop/loop_watch.sh:1-89`
- Modify: `loop/test_loop_watch.sh:1-end`
- Modify: `loop/board.py:2228-2260`
- Modify: `loop/test_board.py:2165-2182`

**Interfaces:**
- Consumes: Task 1 런타임 상태, Task 3 `control.sh start`
- Produces: quota wait 비개입, running 정체 시 동일 제어 경로 재개, 보드 `/api/loop continue`의 단일 경로 호출

- [ ] **Step 1: watcher 네거티브 컨트롤 추가**

```bash
# 2시간 된 lap이어도 quota_wait heartbeat가 최근이면 launchctl 호출 0회
python3 "$TEST_ROOT/loop/runtime_state.py" --path "$STATE" set quota_wait --provider claude
python3 "$TEST_ROOT/loop/runtime_state.py" --path "$STATE" heartbeat
DRY_RUN=1 LOOP_WATCH_ROOT="$TEST_ROOT" bash "$TEST_ROOT/loop/loop_watch.sh"
! grep -q 'DRY:.*kickstart' "$TEST_ROOT/logs/loop_watch.log"

# running heartbeat가 임계값을 넘으면 control.sh start가 정확히 1회
```

- [ ] **Step 2: 기존 watcher가 quota 상태를 모르므로 실패하는지 확인**

Run: `bash loop/test_loop_watch.sh`  
Expected: 오래된 lap만 보고 재기동하여 FAIL

- [ ] **Step 3: watcher를 phase+heartbeat 판정으로 변경**

`quota_wait`와 `recovering`의 heartbeat가 임계값 안이면 정상으로 기록한다. `running` heartbeat가 오래됐거나 서비스가 사라진 경우 직접 launchctl 조합을 복제하지 않고 `control.sh start`를 호출한다. `STOP` 또는 `owner_stopped`는 건드리지 않는다.

- [ ] **Step 4: 보드 재개를 control.sh로 통일**

```python
def start_loop() -> int:
    result = subprocess.run(
        ["bash", str(HERE / "control.sh"), "start"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=20,
        check=False,
    )
    if result.returncode != 0:
        raise ValueError(result.stderr.strip() or "루프 시작 확인 실패")
    pids = find_loop_pids()
    if not pids:
        raise ValueError("루프 시작 뒤 PID 확인 실패")
    return pids[0]
```

- [ ] **Step 5: 표적 테스트 통과 확인**

Run: `bash loop/test_loop_watch.sh && python3 loop/test_board.py`  
Expected: watcher PASS, board suite 마지막 줄 `OK`

- [ ] **Step 6: 독립 커밋**

```bash
git add -- loop/loop_watch.sh loop/test_loop_watch.sh loop/board.py loop/test_board.py
git commit -m "자율루프: 상태 기반 감시와 보드 재개 통일" -- loop/loop_watch.sh loop/test_loop_watch.sh loop/board.py loop/test_board.py
```

---

### Task 5: 보드 지킴이의 중복 AI 비용 제거

**Files:**
- Modify: `loop/board_keeper.sh:19-122`
- Create: `loop/test_board_keeper.sh`

**Interfaces:**
- Consumes: 보드 검사 실패 목록을 정렬·결합한 오류 지문
- Produces: 건강할 때 AI 호출 0회, 동일 실패 지문 AI 호출 최대 1회, 실패 내용 변경 시 새 1회

- [ ] **Step 1: 비용 회귀 테스트 작성**

```bash
# 건강한 검사를 48회 실행해도 fake opencode 호출은 0회다.
for _ in $(seq 1 48); do run_keeper healthy; done
test ! -e "$TEST_ROOT/opencode.calls"

# 같은 실패를 두 번 실행해도 수리 AI는 첫 번째 한 번만 호출된다.
run_keeper broken_state_api
run_keeper broken_state_api
test "$(wc -l < "$TEST_ROOT/opencode.calls")" -eq 1
```

- [ ] **Step 2: 기존 정기 개선·시간 기반 재호출 때문에 실패하는지 확인**

Run: `bash loop/test_board_keeper.sh`  
Expected: 48번째 건강 실행 또는 반복 실패에서 opencode가 추가 호출되어 FAIL

- [ ] **Step 3: 정기 개선 제거와 오류 지문 claim 구현**

`BOARD_KEEPER_IMPROVE_EVERY`, count 파일, `MODE=improve` 경로를 제거한다. 실패 목록을 정렬한 SHA-256 지문으로 만들고 Task 1의 `claim_recovery`를 사용한다. 처음 본 실패 지문만 기존 fix 프롬프트를 한 번 실행하고, 동일 지문은 무료 검증 결과만 갱신한다.

- [ ] **Step 4: 표적 테스트 통과 확인**

Run: `bash loop/test_board_keeper.sh && python3 loop/test_board.py`  
Expected: AI 호출 수 검증 PASS, board suite `OK`

- [ ] **Step 5: 독립 커밋**

```bash
git add -- loop/board_keeper.sh loop/test_board_keeper.sh
git commit -m "보드지킴이: 정기 AI 개선과 중복 수리 제거" -- loop/board_keeper.sh loop/test_board_keeper.sh
```

---

### Task 6: 문서·전체 검증·실서비스 재개

**Files:**
- Modify: `loop/README.md:1-75`
- Modify: `CLAUDE.md`의 하네스 가드레일 원장

**Interfaces:**
- Consumes: Tasks 1-5의 최종 명령과 상태 의미
- Produces: 사람과 모든 AI가 동일하게 실행할 `bash loop/control.sh start [provider]`

- [ ] **Step 1: 운영 문서 갱신**

README의 시작 명령은 아래 하나로 통일한다.

```bash
bash loop/control.sh start           # 직전 실행기
bash loop/control.sh start codex     # 실행기 지정
bash loop/control.sh status
bash loop/control.sh stop
```

자동 council·speed lane·건강한 board keeper 개선은 기본 중단임을 기록한다. 가드레일에는 2026-08-27 증상(Claude 접근 차단 후 3회 실패·STOP), 근본 원인(오류 분류와 종료 정책·배포본 분리), 수리(상태 머신·원본 실행·비용 중복 제거), 테스트 경로를 한 줄로 남긴다.

- [ ] **Step 2: 모든 loop 표적 테스트 실행**

Run:

```bash
bash loop/test_control.sh
bash loop/test_loop_agent.sh
bash loop/test_loop_continuity.sh
bash loop/test_infra_detect.sh
bash loop/test_loop_watch.sh
bash loop/test_board_keeper.sh
python3 -m unittest loop/test_runtime_state.py -v
python3 loop/test_board.py
```

Expected: 모든 명령 exit 0, board suite 마지막 줄 `OK`

- [ ] **Step 3: 저장소 전체 회귀 실행**

Run: `python3 projects/ai-team/harness/check_all.py`  
Expected: exit 0

- [ ] **Step 4: diff와 시크릿·런타임 파일 점검**

Run:

```bash
git diff --check
git status --short
git diff --name-only
git ls-files loop/runtime_state.json loop/recovery_context.log loop/STOP loop/STOP_LANE
```

Expected: diff 오류 없음, 런타임 네 경로 출력 없음, 사용자 기존 변경이 보존됨

- [ ] **Step 5: 문서 커밋**

```bash
git add -- loop/README.md CLAUDE.md
git commit -m "문서: 자율 루프 연속 실행 가드레일 기록" -- loop/README.md CLAUDE.md
```

- [ ] **Step 6: launchd 배포와 비용 경로 중단 확인**

Run:

```bash
touch loop/STOP_LANE
bash loop/deploy_launchd.sh --register-only
launchctl list | grep -E 'com\.ailab\.(autonomous_loop|speedlane|loopwatch|boardkeeper)'
ps aux | grep '[s]peed_lane.sh'
```

Expected: speed lane PID 없음, main plist 등록, loopwatch·boardkeeper 등록 유지

- [ ] **Step 7: 통제된 한 바퀴 실측**

선택 provider의 무료 사용량 조회가 `ok`일 때만 launchd 메인 서비스를 내리고 저장소 원본으로
한 바퀴를 직접 실행한다.

```bash
launchctl bootout gui/$(id -u)/com.ailab.autonomous_loop 2>/dev/null || true
rm -f loop/STOP loop/HOLD
LOOP_MAX_LOOPS=1 bash loop/loop.sh "$(pwd)"
```

Expected: phase가 `running`, lap 로그 한 개 생성, 해당 바퀴의 AI 세션 호출 1회, council·speed lane·board keeper AI 호출 0회

- [ ] **Step 8: 무한 루프 재개와 실제 생존 확인**

Run:

```bash
bash loop/control.sh start
launchctl print gui/$(id -u)/com.ailab.autonomous_loop | sed -n '1,80p'
bash loop/control.sh status
```

Expected: launchd `state = running` 또는 quota 대기 heartbeat 활성, STOP/HOLD 없음, 저장소 원본 `loop.sh` 경로 표시

- [ ] **Step 9: 최종 구현 커밋 상태 확인**

Run: `git log --oneline -8 && git status --short`  
Expected: Tasks 1-6 커밋이 보이고 사용자 소유의 기존 변경만 남음
