#!/bin/bash
# 매 바퀴마다 헤드리스 세션을 "새로" 연다. 대화를 이어 붙이지 않는다.
#
# 운영 규칙 (오너 지시, PROMPT.md와 함께 매 바퀴 적용):
# - 상시 폴리싱: 코드 구멍 다음 이터레이션에는 UI·아트 폴리싱을 섞는다. 영지 화면·EstateYard는
#   docs/GAME_SPEC_ESTATE_BUILD.md대로. 할로우 나이트 화풍 강제는 취소.
# - 중복 리소스 재생성 금지 — 이미 있는 에셋은 다시 뽑지 않는다.

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"

if [ -f "$DEPLOY_ROOT/env.sh" ]; then
  # shellcheck source=/dev/null
  source "$DEPLOY_ROOT/env.sh"
fi

MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
COOLDOWN="${LOOP_COOLDOWN:-10}"
MAX_TURNS="${LOOP_MAX_TURNS:-30}"
MAX_FAILS="${LOOP_MAX_FAILS:-3}"
PROMPT_FILE="$DEPLOY_ROOT/PROMPT.md"
STOP_FILE="$TARGET_REPO/loop/STOP"
AGENT_FILE="$TARGET_REPO/loop/agent"
STATUS_FILE="$TARGET_REPO/docs/STATUS.md"
MAIN_LOG="$TARGET_REPO/logs/loop_main.log"
GROK_MODEL="${LOOP_GROK_MODEL:-grok-4.6}"
OPENCODE_MODEL="${LOOP_OPENCODE_MODEL:-opencode/x-preview-f-free}"
COUNCIL_EVERY="${LOOP_COUNCIL_EVERY:-4}"

# Claude↔Grok 사용량 자동전환 (오너 지시 2026-08-25). claude로 시작, 소진되면 grok, grok도
# 소진되면 다시 claude. 코드 오류·테스트 실패로는 전환하지 않는다 — 소진 판정은 오직
# board.py의 공식 사용량 API(claude_usage/grok_usage)와 랩 로그의 사후 폴백뿐이다.
AUTO_SWITCH="${LOOP_AUTO_SWITCH:-1}"
PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
MAX_PROVIDER_FAILURES="${MAX_PROVIDER_FAILURES:-6}"
PROVIDER_STATE_FILE="$TARGET_REPO/loop/provider.state"
BOARD_PY="$DEPLOY_ROOT/board.py"
EXHAUST_PATTERN="${LOOP_EXHAUST_PATTERN:-usage limit|quota exceeded|rate limit exceeded|out of credits|사용량.*(소진|초과)|로그인.*필요|please (log|sign) in|authentication required}"

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

if ! [[ "$MAX_LOOPS" =~ ^[0-9]+$ && "$COOLDOWN" =~ ^[0-9]+$ && "$MAX_TURNS" =~ ^[0-9]+$ ]]; then
  echo "LOOP_MAX_LOOPS/LOOP_COOLDOWN/LOOP_MAX_TURNS는 0 이상의 정수여야 합니다." >&2
  exit 1
fi

mkdir -p "$TARGET_REPO/logs" "$TARGET_REPO/loop" "$TARGET_REPO/docs/feedback"
export PYTHONUNBUFFERED=1
COUNT=0
FAILS=0

wait_with_stop() {
  local remaining="$1"
  while [ "$remaining" -gt 0 ]; do
    [ -f "$STOP_FILE" ] && return 1
    sleep 1
    remaining=$((remaining - 1))
  done
  return 0
}

status_stamp() {
  if [ -f "$STATUS_FILE" ]; then
    cksum "$STATUS_FILE" | awk '{print $1"-"$2}'
  else
    echo none
  fi
}

other_provider() {
  case "$1" in
    claude) echo grok ;;
    grok) echo claude ;;
    *) echo "$1" ;;
  esac
}

# 공식 사용량 API(board.py usage)로 소진 여부를 확인한다. echo: ok | exhausted | unknown
# unknown은 "조회 자체가 실패"(네트워크/키체인 일시 오류) — 소진으로 오판하지 않고
# 이전 provider를 그대로 쓴다(fail-open).
usage_check() {
  local name="$1"
  python3 "$BOARD_PY" usage "$name" 2>/dev/null | python3 -c '
import json, sys
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print("unknown"); sys.exit()
err = d.get("error")
if err and "로그인 없음" in err:
    print("exhausted"); sys.exit()
remain = d.get("remain_pct")
if d.get("ok") and remain is not None and remain <= 0:
    print("exhausted"); sys.exit()
if err:
    print("unknown"); sys.exit()
print("ok")
'
}

provider_state_read_current() {
  python3 -c '
import json, sys
path = sys.argv[1]
try:
    with open(path, encoding="utf-8") as f:
        cur = (json.load(f) or {}).get("current")
except Exception:
    cur = None
print(cur or "claude")
' "$PROVIDER_STATE_FILE"
}

provider_state_write() {
  # provider_state_write <current> <reason>
  python3 -c '
import json, sys, time
path, current, reason = sys.argv[1:4]
try:
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
except Exception:
    d = {}
d["current"] = current
d["last_switch_at"] = time.strftime("%Y-%m-%d %H:%M:%S")
d["last_switch_reason"] = reason
with open(path, "w", encoding="utf-8") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
' "$PROVIDER_STATE_FILE" "$1" "$2"
}

provider_state_mark_retry() {
  # provider_state_mark_retry <name> — 그 실행기가 소진 감지된 시각을 기록만 한다(참고용).
  python3 -c '
import json, sys, time
path, name = sys.argv[1:3]
try:
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
except Exception:
    d = {}
d[f"{name}_retry_after"] = time.strftime("%Y-%m-%d %H:%M:%S")
with open(path, "w", encoding="utf-8") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
' "$PROVIDER_STATE_FILE" "$1"
}

# 랩 로그에서 사후 소진 신호를 찾는다(usage_check가 못 잡는 세션 도중 소진 대비 폴백).
detect_exhaustion_in_log() {
  local logfile="$1"
  [ -f "$logfile" ] && grep -qiE "$EXHAUST_PATTERN" "$logfile"
}

run_session() {
  local agent="$1"
  local bin="$2"
  local header="너는 비대화형 자율 루프다. 별도 승인 질문 없이 구현한다. 대화를 이어 붙이지 않는다. loop/PROMPT.md 다섯 절을 따른다."

  case "$agent" in
    grok)
      cd "$TARGET_REPO" || return 3
      "$bin" \
        --cwd "$TARGET_REPO" \
        --model "$GROK_MODEL" \
        --prompt-file "$PROMPT_FILE" \
        --always-approve \
        --permission-mode auto \
        --max-turns "$MAX_TURNS" \
        --no-subagents \
        --output-format plain
      ;;
    codex)
      {
        printf '%s\n\n' "$header"
        if [ -f "$PROMPT_FILE" ]; then cat "$PROMPT_FILE"; fi
      } | "$bin" exec --ephemeral --ignore-user-config \
            --sandbox danger-full-access \
            --skip-git-repo-check \
            -
      ;;
    claude)
      "$bin" -p "$(printf '%s\n\n' "$header"; [ -f "$PROMPT_FILE" ] && cat "$PROMPT_FILE")" \
        --permission-mode acceptEdits \
        --no-session-persistence \
        --output-format text
      ;;
    opencode)
      # 비대화형 단발 세션. resume 없음. 권한·MCP는 저장소 opencode.json이 결정한다.
      local prompt
      prompt="$(printf '%s\n\n' "$header"; [ -f "$PROMPT_FILE" ] && cat "$PROMPT_FILE")"
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

STARTUP_AGENT="$(pick_agent)"
if [ "$AUTO_SWITCH" = "1" ] && { [ "$STARTUP_AGENT" = "claude" ] || [ "$STARTUP_AGENT" = "grok" ]; }; then
  echo "자율 개발 루프 시작: target=$TARGET_REPO agent=auto(claude<->grok, 시작=$(provider_state_read_current)) max=${MAX_LOOPS:-0}"
else
  BIN="$(find_bin "$STARTUP_AGENT")"
  if [ -z "$BIN" ]; then
    echo "실행기를 찾지 못했다: $STARTUP_AGENT" >&2
    exit 1
  fi
  echo "자율 개발 루프 시작: target=$TARGET_REPO agent=$STARTUP_AGENT max=${MAX_LOOPS:-0}"
fi

PROVIDER_WAIT_ROUNDS=0

while true; do
  if [ -f "$STOP_FILE" ]; then
    echo "STOP 확인 — 새 바퀴를 시작하지 않고 정상 종료합니다."
    exit 0
  fi
  if [ "$MAX_LOOPS" -gt 0 ] && [ "$COUNT" -ge "$MAX_LOOPS" ]; then
    echo "지정된 ${MAX_LOOPS}바퀴를 마쳤습니다."
    exit 0
  fi

  AGENT="$(pick_agent)"
  if [ "$AUTO_SWITCH" = "1" ] && { [ "$AGENT" = "claude" ] || [ "$AGENT" = "grok" ]; }; then
    STATE_CURRENT="$(provider_state_read_current)"
    CHECK1="$(usage_check "$STATE_CURRENT")"
    if [ "$CHECK1" = "exhausted" ]; then
      OTHER="$(other_provider "$STATE_CURRENT")"
      CHECK2="$(usage_check "$OTHER")"
      if [ "$CHECK2" = "exhausted" ]; then
        PROVIDER_WAIT_ROUNDS=$((PROVIDER_WAIT_ROUNDS + 1))
        echo "양쪽 다 소진($STATE_CURRENT·$OTHER, ${PROVIDER_WAIT_ROUNDS}/${MAX_PROVIDER_FAILURES}) — ${PROVIDER_RETRY_SECONDS}초 대기 후 재확인" | tee -a "$MAIN_LOG"
        provider_state_mark_retry "$STATE_CURRENT"
        provider_state_mark_retry "$OTHER"
        if [ "$PROVIDER_WAIT_ROUNDS" -ge "$MAX_PROVIDER_FAILURES" ]; then
          echo "양쪽 소진 대기 ${MAX_PROVIDER_FAILURES}회 초과 — 정상 종료합니다(사용량 확인 자체가 고장났을 가능성 포함)." | tee -a "$MAIN_LOG"
          touch "$STOP_FILE"
          exit 0
        fi
        wait_with_stop "$PROVIDER_RETRY_SECONDS" || exit 0
        continue
      fi
      echo "$STATE_CURRENT 소진 감지 — $OTHER(으)로 전환" | tee -a "$MAIN_LOG"
      provider_state_write "$OTHER" "usage_check: $STATE_CURRENT exhausted"
      AGENT="$OTHER"
    else
      AGENT="$STATE_CURRENT"
    fi
    PROVIDER_WAIT_ROUNDS=0
  fi
  BIN="$(find_bin "$AGENT")"
  if [ -z "$BIN" ]; then
    echo "실행기를 찾지 못했다: $AGENT" | tee -a "$MAIN_LOG"
    exit 1
  fi

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

  EXHAUSTED_THIS_LAP=0
  if [ "$AUTO_SWITCH" = "1" ] && { [ "$AGENT" = "claude" ] || [ "$AGENT" = "grok" ]; } \
      && detect_exhaustion_in_log "$LAP_LOG"; then
    EXHAUSTED_THIS_LAP=1
    NEXT_PROVIDER="$(other_provider "$AGENT")"
    echo "랩 로그에서 소진 신호 감지 — $AGENT 소진, 다음 바퀴부터 $NEXT_PROVIDER (FAILS 미증가)" | tee -a "$MAIN_LOG" "$LAP_LOG"
    provider_state_write "$NEXT_PROVIDER" "log-detect: $AGENT exhaustion phrase in $LAP_ID"
  fi

  if [ "$EXHAUSTED_THIS_LAP" = "1" ]; then
    : # 코드 오류가 아니라 소진이므로 FAILS/COUNT를 건드리지 않는다 — 다음 바퀴가 전환을 반영한다.
  elif [ "$AFTER" = "$BEFORE" ]; then
    echo "STATUS.md 갱신 없음" | tee -a "$MAIN_LOG" "$LAP_LOG"
    FAILS=$((FAILS + 1))
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      echo "STATUS.md 미갱신이 ${MAX_FAILS}회 — 정상 종료합니다(재기동 루프 방지)." | tee -a "$MAIN_LOG" "$LAP_LOG"
      touch "$STOP_FILE"
      exit 0
    fi
  elif [ "$RESULT" -eq 0 ]; then
    COUNT=$((COUNT + 1))
    FAILS=0
    # 자가학습 회의 — N바퀴마다 역할별 병렬 회의를 소집한다 (오너 2026-08-23).
    # 어떤 에이전트든 loop/COUNCIL_NOW 파일을 만들면 다음 바퀴 끝에 즉시 소집된다.
    if [ -f "$TARGET_REPO/loop/COUNCIL_NOW" ]; then
      rm -f "$TARGET_REPO/loop/COUNCIL_NOW"
      echo "즉시 회의 소집 신호 감지" | tee -a "$MAIN_LOG" "$LAP_LOG"
      bash "$DEPLOY_ROOT/council.sh" "$TARGET_REPO" >> "$LAP_LOG" 2>&1 || true
    fi
    if [ "$COUNT" -gt 0 ] && [ "$COUNCIL_EVERY" -gt 0 ] && [ $((COUNT % COUNCIL_EVERY)) -eq 0 ]; then
      echo "회의 소집: ${COUNT}바퀴 완료 — 역할 병렬 회의 시작" | tee -a "$MAIN_LOG" "$LAP_LOG"
      bash "$DEPLOY_ROOT/council.sh" "$TARGET_REPO" >> "$LAP_LOG" 2>&1 \
        || echo "회의 실패 — 루프는 계속" | tee -a "$MAIN_LOG" "$LAP_LOG"
    fi
    # 속도 레인 적립분(autonomous/integration)을 master로 흡수
    bash "$DEPLOY_ROOT/merge_integration.sh" "$TARGET_REPO" >> "$MAIN_LOG" 2>&1 || true
  else
    echo "세션 비정상 종료 (code=$RESULT)" | tee -a "$MAIN_LOG" "$LAP_LOG"
    FAILS=$((FAILS + 1))
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      exit "$RESULT"
    fi
  fi

  if [ -f "$STOP_FILE" ]; then
    echo "현재 바퀴 완료 후 STOP 확인 — 정상 종료합니다." | tee -a "$MAIN_LOG"
    exit 0
  fi

  wait_with_stop "$COOLDOWN" || exit 0
done
