#!/bin/bash
# 매 바퀴마다 헤드리스 세션을 "새로" 연다. 대화를 이어 붙이지 않는다.
#
# 운영 규칙 (오너 지시, PROMPT.md와 함께 매 바퀴 적용):
# - 상시 폴리싱: 코드 구멍 다음 이터레이션에는 UI·아트 폴리싱을 섞는다. 영지 화면·EstateYard는
#   docs/GAME_SPEC_ESTATE_BUILD.md대로. 할로우 나이트 화풍 강제는 취소.
# - 중복 리소스 재생성 금지 — 이미 있는 에셋은 다시 뽑지 않는다.

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# 자가검사 모드는 저장소 인자를 먹지 않는다 — 판정 함수만 쓰고 끝낸다(아래 후크).
SELFTEST_INFRA=""
if [ "${1:-}" = "--self-test-infra" ]; then SELFTEST_INFRA="${2:-}"; set --; fi
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"

if [ -f "$DEPLOY_ROOT/env.sh" ]; then
  # shellcheck source=/dev/null
  source "$DEPLOY_ROOT/env.sh"
fi

MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
COOLDOWN="${LOOP_COOLDOWN:-10}"
MAX_TURNS="${LOOP_MAX_TURNS:-30}"
PROMPT_FILE="$DEPLOY_ROOT/PROMPT.md"
STOP_FILE="$TARGET_REPO/loop/STOP"
AGENT_FILE="$TARGET_REPO/loop/agent"
STATUS_FILE="$TARGET_REPO/docs/STATUS.md"
MAIN_LOG="$TARGET_REPO/logs/loop_main.log"
GROK_MODEL="${LOOP_GROK_MODEL:-grok-4.6}"
OPENCODE_MODEL="${LOOP_OPENCODE_MODEL:-opencode/mimo-v2.5-free}"
# 회의 20260827-081437 채택 #3 — GameFullCheck 전수는 4바퀴마다 1회. 0 이면 끈다.
FULLCHECK_EVERY="${LOOP_FULLCHECK_EVERY:-4}"
FULLCHECK_METHOD="${LOOP_FULLCHECK_METHOD:-AshesToStars.GameFullCheck.Run}"

PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
HEARTBEAT_SECONDS="${LOOP_HEARTBEAT_SECONDS:-30}"
RECOVERY_RETRY_SECONDS="${LOOP_RECOVERY_RETRY_SECONDS:-900}"
RECOVERY_PROVIDERS="${LOOP_RECOVERY_PROVIDERS:-codex,claude,grok,opencode}"
RUNTIME_STATE_TOOL="$DEPLOY_ROOT/runtime_state.py"
RUNTIME_STATE_FILE="${LOOP_RUNTIME_STATE_FILE:-$TARGET_REPO/loop/runtime_state.json}"
RECOVERY_CONTEXT_FILE="$TARGET_REPO/loop/recovery_context.log"
# GameFullCheck 주기 카운터 — loop/ 에만 영속 (STATUS.md 금지). git 미추적 런타임 파일.
FULLCHECK_COUNT_FILE="$TARGET_REPO/loop/fullcheck_lap.count"
BOARD_PY="${LOOP_BOARD_PY:-$DEPLOY_ROOT/board.py}"
PROVIDER_ERROR_PATTERN="${LOOP_PROVIDER_ERROR_PATTERN:-organization has disabled|authentication required|please (log|sign) in|로그인.*필요|executable not found}"
# 공급자 인프라 장애 — 「인프라 실패 ≠ 이슈 실패」(CLAUDE.md 가드레일). 이런 바퀴는 우리 코드가
# 틀린 게 아니라 상대 서버가 죽은 것이므로 STATUS 미갱신으로 세면 안 된다. 2026-08-26 22:23·22:38
# 두 바퀴가 이 오류로 죽어 「미갱신 3회」에 걸려 루프 전체가 정상 종료했다.
INFRA_PATTERN="${LOOP_INFRA_PATTERN:-Endpoint is unavailable|Unexpected server error|UnknownError|Upstream request failed|502 Bad Gateway|503 Service|504 Gateway|overloaded_error|Internal server error}"

if [ ! -f "$PROMPT_FILE" ]; then
  PROMPT_FILE="$TARGET_REPO/loop/PROMPT.md"
fi

pick_agent() {
  if [ -n "${LOOP_AGENT:-}" ]; then
    echo "$LOOP_AGENT"
    return
  fi
  if [ -f "$AGENT_FILE" ]; then
    tr -d '[:space:]' < "$AGENT_FILE"
    return
  fi
  echo "${LOOP_PROVIDERS:-grok}" | awk -F, '{print $1}' | tr -d '[:space:]'
}

find_bin() {
  local name="$1"
  case "$name" in
    grok)
      if [ -n "${LOOP_GROK_BIN:-}" ] && [ -x "${LOOP_GROK_BIN}" ]; then echo "$LOOP_GROK_BIN"; return; fi
      ;;
    codex)
      if [ -n "${LOOP_CODEX_BIN:-}" ] && [ -x "${LOOP_CODEX_BIN}" ]; then echo "$LOOP_CODEX_BIN"; return; fi
      ;;
    claude)
      if [ -n "${LOOP_CLAUDE_BIN:-}" ] && [ -x "${LOOP_CLAUDE_BIN}" ]; then echo "$LOOP_CLAUDE_BIN"; return; fi
      ;;
    opencode)
      if [ -n "${LOOP_OPENCODE_BIN:-}" ] && [ -x "${LOOP_OPENCODE_BIN}" ]; then echo "$LOOP_OPENCODE_BIN"; return; fi
      ;;
  esac
  command -v "$name" 2>/dev/null || true
}

if ! [[ "$MAX_LOOPS" =~ ^[0-9]+$ && "$COOLDOWN" =~ ^[0-9]+$ && "$MAX_TURNS" =~ ^[0-9]+$ \
    && "$PROVIDER_RETRY_SECONDS" =~ ^[0-9]+$ && "$RECOVERY_RETRY_SECONDS" =~ ^[0-9]+$ \
    && "$HEARTBEAT_SECONDS" =~ ^[1-9][0-9]*$ ]]; then
  echo "루프 횟수·대기 시간은 0 이상, heartbeat는 1 이상의 정수여야 합니다." >&2
  exit 1
fi
if ! [[ "$FULLCHECK_EVERY" =~ ^[0-9]+$ ]]; then
  echo "LOOP_FULLCHECK_EVERY는 0 이상의 정수여야 합니다." >&2
  exit 1
fi

mkdir -p "$TARGET_REPO/logs" "$TARGET_REPO/loop" "$TARGET_REPO/docs/feedback"
export PYTHONUNBUFFERED=1
COUNT=0

wait_with_stop() {
  local remaining="$1" step
  while [ "$remaining" -gt 0 ]; do
    if [ -f "$STOP_FILE" ]; then
      runtime_set owner_stopped "$(pick_agent)" "오너 STOP" 0
      return 1
    fi
    runtime_heartbeat
    step="$HEARTBEAT_SECONDS"
    [ "$remaining" -lt "$step" ] && step="$remaining"
    sleep "$step"
    remaining=$((remaining - step))
  done
  runtime_heartbeat
  return 0
}

status_stamp() {
  if [ -f "$STATUS_FILE" ]; then
    cksum "$STATUS_FILE" | awk '{print $1"-"$2}'
  else
    echo none
  fi
}

# 공식 사용량 API로만 확인한다. 프롬프트를 보내지 않는다. echo: ok | quota | error | unknown
usage_check() {
  local name="$1"
  python3 "$BOARD_PY" usage "$name" 2>/dev/null | python3 -c '
import json, re, sys
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print("unknown"); sys.exit()
err = d.get("error")
if err and re.search(r"로그인.*(?:없음|만료|필요)|login.*(?:expired|required)|authentication required", str(err), re.I):
    print("error"); sys.exit()
remain = d.get("remain_pct")
if d.get("ok") and remain is not None and remain <= 0:
    print("quota"); sys.exit()
if err:
    print("unknown"); sys.exit()
print("ok")
'
}

runtime_set() {
  local phase="$1" provider="${2:-}" reason="${3:-}" retry_at="${4:-0}"
  python3 "$RUNTIME_STATE_TOOL" --path "$RUNTIME_STATE_FILE" set "$phase" \
    --provider "$provider" --reason "$reason" --retry-at "$retry_at" >/dev/null
}

runtime_heartbeat() {
  python3 "$RUNTIME_STATE_TOOL" --path "$RUNTIME_STATE_FILE" heartbeat >/dev/null
}

# 공급자 서버 장애로 죽은 바퀴인가 — 마지막 40줄만 본다(작업 중 인용한 문구를 장애로 오독하지
# 않기 위해서다. 실제 장애는 세션 끝에서 터진다).
detect_infra_failure_in_log() {
  local logfile="$1"
  [ -f "$logfile" ] || return 1
  tail -40 "$logfile" | grep -qiE "$INFRA_PATTERN"
}

# GameFullCheck 전수 주기 — 회의 20260827-081437 채택 #3.
# 카운터는 프로세스 재시작 뒤에도 loop/fullcheck_lap.count 로 이어진다.
fullcheck_lap_read() {
  local n=""
  if [ -f "$FULLCHECK_COUNT_FILE" ]; then
    n="$(tr -d '[:space:]' < "$FULLCHECK_COUNT_FILE" || true)"
  fi
  case "$n" in
    ''|*[!0-9]*) echo 0 ;;
    *) echo "$n" ;;
  esac
}

fullcheck_lap_write() {
  mkdir -p "$(dirname "$FULLCHECK_COUNT_FILE")"
  printf '%s\n' "$1" > "$FULLCHECK_COUNT_FILE"
}

# 성공 바퀴 번호가 N의 배수일 때만 run_selfcheck.sh 로 전수를 돌린다.
# Unity 부재는 비치명 스킵 — 사유를 랩 로그에 남기고 루프는 계속.
maybe_run_game_fullcheck() {
  local lap="$1" rc=0 wrap_out
  [ "$FULLCHECK_EVERY" -gt 0 ] || return 0
  [ "$lap" -gt 0 ] || return 0
  [ $((lap % FULLCHECK_EVERY)) -eq 0 ] || return 0

  echo "GameFullCheck 전수: ${lap}바퀴 — ${FULLCHECK_EVERY}바퀴마다 1회 ($FULLCHECK_METHOD)" \
    | tee -a "$MAIN_LOG" "$LAP_LOG"

  if [ ! -f "$DEPLOY_ROOT/run_selfcheck.sh" ]; then
    echo "GameFullCheck 스킵 — run_selfcheck.sh 없음: $DEPLOY_ROOT/run_selfcheck.sh" \
      | tee -a "$MAIN_LOG" "$LAP_LOG"
    return 0
  fi

  wrap_out="$LAP_DIR/fullcheck-wrap-$LAP_ID.log"
  bash "$DEPLOY_ROOT/run_selfcheck.sh" "$FULLCHECK_METHOD" \
    --project "$TARGET_REPO/projects/ashes-to-stars/unity_meas" \
    --log "$TARGET_REPO/projects/ashes-to-stars/results/game_fullcheck.log" \
    > "$wrap_out" 2>&1 || rc=$?
  if [ -f "$wrap_out" ]; then
    cat "$wrap_out" >> "$LAP_LOG"
  fi

  if [ "$rc" -eq 0 ]; then
    echo "GameFullCheck PASS" | tee -a "$MAIN_LOG" "$LAP_LOG"
    return 0
  fi

  if [ -f "$wrap_out" ] && grep -Eq 'Unity 에디터를 찾지 못했다|Unity 가 없다|Unity 가 실행 파일이 아니다' "$wrap_out"; then
    echo "GameFullCheck 스킵 — Unity 없음" | tee -a "$MAIN_LOG" "$LAP_LOG"
    grep -E '\[run_selfcheck\]' "$wrap_out" | head -5 | tee -a "$MAIN_LOG" "$LAP_LOG" || true
    return 0
  fi

  echo "GameFullCheck 실패 (exit $rc) — 루프는 계속" | tee -a "$MAIN_LOG" "$LAP_LOG"
  return 0
}


# last_test_report.json HEAD 재실행 — 회의 20260827-073515 채택 #2.
# 성공 바퀴 뒤, 리포트가 없거나 `head` 가 현재 HEAD 와 다르면 refresh_test_report.sh 를 부른다.
# Unity 부재는 비치명 스킵. GameSweep FAIL 도 루프는 계속(GameFullCheck 와 같은 약속).
# GameFullCheck 는 여기서 부르지 않는다.
maybe_refresh_test_report() {
  local report="$DEPLOY_ROOT/last_test_report.json"
  local root rc=0 wrap_out existing head
  local main_log="${MAIN_LOG:-/dev/null}"
  local lap_log="${LAP_LOG:-$main_log}"
  [ "${LOOP_REFRESH_TEST_REPORT:-1}" = "1" ] || return 0
  root="$(cd "$DEPLOY_ROOT/.." && pwd)"
  head="$(git -C "$root" rev-parse HEAD 2>/dev/null || true)"
  if [ -z "$head" ]; then
    echo "last_test_report 갱신 스킵 — git HEAD 없음" | tee -a "$main_log" "$lap_log"
    return 0
  fi
  if [ -f "$report" ]; then
    existing="$(python3 -c '
import json, sys
p = sys.argv[1]
try:
    with open(p, "r", encoding="utf-8-sig") as f:
        d = json.load(f)
    print((d.get("head") or "") if isinstance(d, dict) else "")
except Exception:
    print("")
' "$report")"
    if [ "$existing" = "$head" ]; then
      echo "last_test_report.json 이미 현재 HEAD" | tee -a "$main_log" "$lap_log"
      return 0
    fi
  fi

  echo "last_test_report.json HEAD 재실행 ($head)" | tee -a "$main_log" "$lap_log"
  if [ ! -f "$DEPLOY_ROOT/refresh_test_report.sh" ]; then
    echo "last_test_report 스킵 — refresh_test_report.sh 없음" | tee -a "$main_log" "$lap_log"
    return 0
  fi

  wrap_out="${LAP_DIR:-$root/projects/ashes-to-stars/results}/refresh-wrap.log"
  mkdir -p "$(dirname "$wrap_out")"
  bash "$DEPLOY_ROOT/refresh_test_report.sh" \
    --report "$report" \
    --project "$TARGET_REPO/projects/ashes-to-stars/unity_meas" \
    --log "$root/projects/ashes-to-stars/results/refresh_test_report.log" \
    > "$wrap_out" 2>&1 || rc=$?
  if [ -f "$wrap_out" ] && [ -n "${LAP_LOG:-}" ]; then
    cat "$wrap_out" >> "$LAP_LOG" || true
  fi

  if [ "$rc" -eq 0 ]; then
    echo "last_test_report 갱신 완료" | tee -a "$main_log" "$lap_log"
    return 0
  fi
  if [ -f "$wrap_out" ] && grep -Eq 'Unity 에디터를 찾지 못했다|Unity 가 없다|Unity 가 실행 파일이 아니다' "$wrap_out"; then
    echo "last_test_report 스킵 — Unity 없음" | tee -a "$main_log" "$lap_log"
    return 0
  fi
  echo "last_test_report 갱신 실패 (exit $rc) — 루프는 계속" | tee -a "$main_log" "$lap_log"
  return 0
}

# 자가검사용 후크 — `bash loop/loop.sh --self-test-infra <로그파일>`이면 판정만 하고 끝낸다
# (종료 0=공급자 장애 · 1=아니다). 네거티브 컨트롤 없는 통과를 만들지 않기 위한 장치다.
if [ -n "$SELFTEST_INFRA" ]; then
  detect_infra_failure_in_log "$SELFTEST_INFRA" && exit 0 || exit 1
fi

run_session() {
  local agent="$1"
  local bin="$2"
  local prompt_file="${3:-$PROMPT_FILE}"
  local header="${4:-너는 비대화형 자율 루프다. 별도 승인 질문 없이 구현한다. 대화를 이어 붙이지 않는다. loop/PROMPT.md 다섯 절을 따른다.}"

  case "$agent" in
    grok)
      cd "$TARGET_REPO" || return 3
      "$bin" \
        --cwd "$TARGET_REPO" \
        --model "$GROK_MODEL" \
        --prompt-file "$prompt_file" \
        --always-approve \
        --permission-mode auto \
        --max-turns "$MAX_TURNS" \
        --no-subagents \
        --output-format plain
      ;;
    codex)
      {
        printf '%s\n\n' "$header"
        if [ -f "$prompt_file" ]; then cat "$prompt_file"; fi
      } | "$bin" exec --ephemeral --ignore-user-config \
            --sandbox danger-full-access \
            --skip-git-repo-check \
            -
      ;;
    claude)
      "$bin" -p "$(printf '%s\n\n' "$header"; [ -f "$prompt_file" ] && cat "$prompt_file")" \
        --permission-mode acceptEdits \
        --no-session-persistence \
        --output-format text
      ;;
    opencode)
      # 비대화형 단발 세션. resume 없음. 권한·MCP는 저장소 opencode.json이 결정한다.
      local prompt
      prompt="$(printf '%s\n\n' "$header"; [ -f "$prompt_file" ] && cat "$prompt_file")"
      cd "$TARGET_REPO" || return 3
      "$bin" run \
        ${OPENCODE_MODEL:+-m "$OPENCODE_MODEL"} \
        "$prompt"
      ;;
    *)
      echo "알 수 없는 실행기: $agent" >&2
      return 2
      ;;
  esac
}

current_head() {
  git -C "$TARGET_REPO" rev-parse HEAD 2>/dev/null || echo "nogit"
}

classify_lap() {
  local logfile="$1" exit_code="$2"
  python3 "$RUNTIME_STATE_TOOL" --path "$RUNTIME_STATE_FILE" classify \
    --log "$logfile" --exit-code "$exit_code"
}

provider_error_in_log() {
  local logfile="$1"
  [ -f "$logfile" ] && tail -80 "$logfile" | grep -qiE "$PROVIDER_ERROR_PATTERN"
}

select_recovery_agent() {
  local failed_agent="$1" logfile="$2" skip_failed=0 candidate bin
  provider_error_in_log "$logfile" && skip_failed=1
  if [ "$skip_failed" = "0" ]; then
    bin="$(find_bin "$failed_agent")"
    if [ -n "$bin" ]; then
      echo "$failed_agent"
      return 0
    fi
  fi
  local IFS=','
  for candidate in $RECOVERY_PROVIDERS; do
    candidate="$(printf '%s' "$candidate" | tr -d '[:space:]')"
    [ -z "$candidate" ] && continue
    [ "$skip_failed" = "1" ] && [ "$candidate" = "$failed_agent" ] && continue
    bin="$(find_bin "$candidate")"
    if [ -n "$bin" ]; then
      echo "$candidate"
      return 0
    fi
  done
  return 1
}

write_recovery_context() {
  local failed_agent="$1" exit_code="$2" logfile="$3"
  {
    echo "실패 실행기: $failed_agent"
    echo "종료 코드: $exit_code"
    echo "현재 HEAD: $(current_head)"
    echo
    echo "오류 로그 끝 80줄:"
    tail -80 "$logfile" 2>/dev/null || true
  } > "$RECOVERY_CONTEXT_FILE"
}

wait_for_context_change() {
  local failed_head="$1" failed_agent="${2:-}" wait_kind="${3:-head}"
  local delay="$RECOVERY_RETRY_SECONDS" availability bin
  [ "$delay" -gt 0 ] || delay=1
  while [ "$(current_head)" = "$failed_head" ]; do
    if [ "$wait_kind" = "usage" ]; then
      availability="$(usage_check "$failed_agent")"
      if [ "$availability" = "ok" ]; then
        runtime_set running "$failed_agent" "인증/사용량 조회 회복" 0
        return 0
      fi
      if [ "$availability" = "quota" ]; then
        runtime_set quota_wait "$failed_agent" "인증 회복 뒤 사용량 한도" \
          "$(( $(date +%s) + PROVIDER_RETRY_SECONDS ))"
        return 0
      fi
    elif [ "$wait_kind" = "executable" ]; then
      bin="$(find_bin "$failed_agent")"
      if [ -n "$bin" ]; then
        runtime_set running "$failed_agent" "실행기 경로 회복" 0
        return 0
      fi
    fi
    echo "같은 오류 지문은 AI를 다시 부르지 않음 — ${delay}초 뒤 로컬 상태만 재확인" \
      | tee -a "$MAIN_LOG" "${LAP_LOG:-$MAIN_LOG}"
    wait_with_stop "$delay" || return 1
  done
  runtime_set running "$(pick_agent)" "코드 변경 감지 후 복구 재개" 0
  return 0
}

run_recovery_once() {
  local failed_agent="$1" exit_code="$2" logfile="$3" wait_kind="${4:-head}"
  local failed_head fingerprint recovery_agent recovery_bin recovery_rc after_head
  failed_head="$(current_head)"
  fingerprint="$(python3 "$RUNTIME_STATE_TOOL" --path "$RUNTIME_STATE_FILE" fingerprint \
    --provider "$failed_agent" --exit-code "$exit_code" --log "$logfile" \
    --context-version "$failed_head")"
  runtime_set recovering "$failed_agent" "오류 복구: $fingerprint" 0

  if ! python3 "$RUNTIME_STATE_TOOL" --path "$RUNTIME_STATE_FILE" claim "$fingerprint"; then
    wait_for_context_change "$failed_head" "$failed_agent" "$wait_kind"
    return $?
  fi

  recovery_agent="$(select_recovery_agent "$failed_agent" "$logfile")"
  if [ -z "$recovery_agent" ]; then
    echo "사용 가능한 복구 실행기 없음 — AI 호출 없이 대기" | tee -a "$MAIN_LOG" "$logfile"
    wait_for_context_change "$failed_head" "$failed_agent" "$wait_kind"
    return $?
  fi
  recovery_bin="$(find_bin "$recovery_agent")"
  write_recovery_context "$failed_agent" "$exit_code" "$logfile"
  echo "오류 지문 최초 1회 복구: $recovery_agent" | tee -a "$MAIN_LOG" "$logfile"
  run_session "$recovery_agent" "$recovery_bin" "$RECOVERY_CONTEXT_FILE" \
    "너는 자율 루프 오류 복구 세션이다. 새 기능을 만들지 않는다. 오류의 근본 원인과 직접 관련된 파일만 수정하고 재현 테스트를 통과시켜라. 성공하면 고친 파일만 즉시 커밋하고 게임 개발 작업은 시작하지 마라." \
    >> "$logfile" 2>&1
  recovery_rc=$?
  runtime_heartbeat
  after_head="$(current_head)"
  if [ "$recovery_rc" -eq 0 ] && [ "$after_head" != "$failed_head" ]; then
    echo "복구 커밋 확인 — 새 원본으로 재기동" | tee -a "$MAIN_LOG" "$logfile"
    return 75
  fi
  echo "복구가 새 커밋을 만들지 못함(code=$recovery_rc) — 동일 지문 재호출 금지" \
    | tee -a "$MAIN_LOG" "$logfile"
  wait_for_context_change "$failed_head" "$failed_agent" "$wait_kind"
  return $?
}

# 단위 테스트가 함수만 쓰도록 (LOOP_SOURCE_ONLY=1 source). 본 루프는 돌리지 않는다.
if [ "${LOOP_SOURCE_ONLY:-0}" = "1" ]; then
  return 0 2>/dev/null || exit 0
fi

STARTUP_AGENT="$(pick_agent)"
if [ ! -f "$RUNTIME_STATE_TOOL" ]; then
  echo "런타임 상태 도구를 찾지 못했다: $RUNTIME_STATE_TOOL" >&2
  exit 1
fi
runtime_set running "$STARTUP_AGENT" "루프 시작" 0
echo "자율 개발 루프 시작: target=$TARGET_REPO agent=$STARTUP_AGENT max=${MAX_LOOPS:-0}"

while true; do
  if [ -f "$STOP_FILE" ]; then
    runtime_set owner_stopped "$(pick_agent)" "오너 STOP" 0
    echo "STOP 확인 — 새 바퀴를 시작하지 않고 정상 종료합니다."
    exit 0
  fi
  if [ "$MAX_LOOPS" -gt 0 ] && [ "$COUNT" -ge "$MAX_LOOPS" ]; then
    echo "지정된 ${MAX_LOOPS}바퀴를 마쳤습니다."
    exit 0
  fi

  AGENT="$(pick_agent)"
  USAGE_STATE="$(usage_check "$AGENT")"
  if [ "$USAGE_STATE" = "quota" ]; then
    runtime_set quota_wait "$AGENT" "사용량 한도" "$(( $(date +%s) + PROVIDER_RETRY_SECONDS ))"
    echo "$AGENT 사용량 회복 대기 — ${PROVIDER_RETRY_SECONDS}초 뒤 무료 조회" | tee -a "$MAIN_LOG"
    wait_with_stop "$PROVIDER_RETRY_SECONDS" || exit 0
    continue
  fi
  if [ "$USAGE_STATE" = "error" ]; then
    DATE_DIR="$(date +%Y-%m-%d)"
    LAP_DIR="$TARGET_REPO/logs/$DATE_DIR"
    LAP_LOG="$LAP_DIR/recovery-login-$AGENT.log"
    mkdir -p "$LAP_DIR"
    echo "$AGENT authentication required: usage API 로그인 없음" > "$LAP_LOG"
    run_recovery_once "$AGENT" 77 "$LAP_LOG" usage
    RECOVERY_RESULT=$?
    [ "$RECOVERY_RESULT" -eq 75 ] && exit 75
    continue
  fi

  BIN="$(find_bin "$AGENT")"
  if [ -z "$BIN" ]; then
    DATE_DIR="$(date +%Y-%m-%d)"
    LAP_DIR="$TARGET_REPO/logs/$DATE_DIR"
    LAP_LOG="$LAP_DIR/recovery-missing-$AGENT.log"
    mkdir -p "$LAP_DIR"
    echo "$AGENT executable not found" > "$LAP_LOG"
    run_recovery_once "$AGENT" 127 "$LAP_LOG" executable
    RECOVERY_RESULT=$?
    [ "$RECOVERY_RESULT" -eq 75 ] && exit 75
    continue
  fi
  runtime_set running "$AGENT" "개발 바퀴 실행" 0

  DATE_DIR="$(date +%Y-%m-%d)"
  LAP_ID="$(date +%Y%m%d-%H%M%S)-$((COUNT + 1))"
  LAP_DIR="$TARGET_REPO/logs/$DATE_DIR"
  LAP_LOG="$LAP_DIR/lap-$LAP_ID.log"
  mkdir -p "$LAP_DIR"

  echo "바퀴 시작: $LAP_ID agent=$AGENT" | tee -a "$MAIN_LOG" "$LAP_LOG"
  BEFORE="$(status_stamp)"

  # 새 세션 — resume/continue 없음
  run_session "$AGENT" "$BIN" >> "$LAP_LOG" 2>&1
  RESULT=$?
  AFTER="$(status_stamp)"

  echo "바퀴 종료: $LAP_ID code=$RESULT" | tee -a "$MAIN_LOG" "$LAP_LOG"

  # 회의 20260827-081437 채택 #2 — 랩 종료 전 커밋 가드 재실행.
  # 빈 인덱스는 통과, 타 세션 스테이징은 존중, 낡은 스냅만 차단.
  if ! (cd "$TARGET_REPO" && bash "$DEPLOY_ROOT/commit_guard.sh" --lap-end) >> "$LAP_LOG" 2>&1; then
    echo "랩 종료 가드 실패 — 공유 인덱스에 낡은 스냅. 다음 맨몸 커밋이 되돌릴 수 있다" | tee -a "$MAIN_LOG" "$LAP_LOG"
  fi

  LAP_CLASS="$(classify_lap "$LAP_LOG" "$RESULT")"
  if [ "$LAP_CLASS" = "quota" ]; then
    runtime_set quota_wait "$AGENT" "사용량 한도" "$(( $(date +%s) + PROVIDER_RETRY_SECONDS ))"
    echo "$AGENT 사용량 회복 대기 — ${PROVIDER_RETRY_SECONDS}초 뒤 무료 조회" \
      | tee -a "$MAIN_LOG" "$LAP_LOG"
    wait_with_stop "$PROVIDER_RETRY_SECONDS" || exit 0
    continue
  fi

  if [ "$RESULT" -ne 0 ]; then
    echo "세션 비정상 종료 (code=$RESULT) — 오류 복구 우선" | tee -a "$MAIN_LOG" "$LAP_LOG"
    run_recovery_once "$AGENT" "$RESULT" "$LAP_LOG"
    RECOVERY_RESULT=$?
    [ "$RECOVERY_RESULT" -eq 75 ] && exit 75
    continue
  fi

  if [ "$AFTER" = "$BEFORE" ]; then
    echo "STATUS.md 갱신 없음" | tee -a "$MAIN_LOG" "$LAP_LOG"
    run_recovery_once "$AGENT" 70 "$LAP_LOG"
    RECOVERY_RESULT=$?
    [ "$RECOVERY_RESULT" -eq 75 ] && exit 75
    continue
  fi

  if [ "$RESULT" -eq 0 ]; then
    COUNT=$((COUNT + 1))
    runtime_set running "$AGENT" "정상 바퀴 완료" 0
    # 회의 20260827-081437 채택 #3 — 성공 바퀴만 세고, 4바퀴마다 GameFullCheck 전수.
    FC_LAP="$(fullcheck_lap_read)"
    FC_LAP=$((FC_LAP + 1))
    fullcheck_lap_write "$FC_LAP"
    maybe_run_game_fullcheck "$FC_LAP"
    maybe_refresh_test_report
    # 자가학습 회의 — N바퀴마다 역할별 병렬 회의를 소집한다 (오너 2026-08-23).
    # 어떤 에이전트든 loop/COUNCIL_NOW 파일을 만들면 다음 바퀴 끝에 즉시 소집된다.
    if [ -f "$TARGET_REPO/loop/COUNCIL_NOW" ]; then
      rm -f "$TARGET_REPO/loop/COUNCIL_NOW"
      echo "즉시 회의 소집 신호 감지" | tee -a "$MAIN_LOG" "$LAP_LOG"
      bash "$DEPLOY_ROOT/council.sh" "$TARGET_REPO" >> "$LAP_LOG" 2>&1 || true
    fi
    # 속도 레인 적립분(autonomous/integration)을 master로 흡수
    bash "$DEPLOY_ROOT/merge_integration.sh" "$TARGET_REPO" >> "$MAIN_LOG" 2>&1 || true
  fi

  if [ -f "$STOP_FILE" ]; then
    runtime_set owner_stopped "$AGENT" "오너 STOP" 0
    echo "현재 바퀴 완료 후 STOP 확인 — 정상 종료합니다." | tee -a "$MAIN_LOG"
    exit 0
  fi

  wait_with_stop "$COOLDOWN" || exit 0
done
