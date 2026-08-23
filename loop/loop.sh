#!/bin/bash
# 매 바퀴마다 헤드리스 세션을 "새로" 연다. 대화를 이어 붙이지 않는다.

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

run_session() {
  local agent="$1"
  local bin="$2"
  local header="너는 비대화형 자율 루프다. 별도 승인 질문 없이 구현한다. 대화를 이어 붙이지 않는다. loop/PROMPT.md 다섯 절을 따른다."

  case "$agent" in
    grok)
      "$bin" \
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
    *)
      echo "알 수 없는 실행기: $agent" >&2
      return 2
      ;;
  esac
}

AGENT="$(pick_agent)"
BIN="$(find_bin "$AGENT")"
if [ -z "$BIN" ]; then
  echo "실행기를 찾지 못했다: $AGENT" >&2
  exit 1
fi

echo "자율 개발 루프 시작: target=$TARGET_REPO agent=$AGENT max=${MAX_LOOPS:-0}"

while true; do
  if [ -f "$STOP_FILE" ]; then
    echo "STOP 확인 — 새 바퀴를 시작하지 않고 정상 종료합니다."
    exit 0
  fi
  if [ "$MAX_LOOPS" -gt 0 ] && [ "$COUNT" -ge "$MAX_LOOPS" ]; then
    echo "지정된 ${MAX_LOOPS}바퀴를 마쳤습니다."
    exit 0
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

  if [ "$AFTER" = "$BEFORE" ]; then
    echo "STATUS.md 갱신 없음" | tee -a "$MAIN_LOG" "$LAP_LOG"
    FAILS=$((FAILS + 1))
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      echo "STATUS.md 미갱신이 ${MAX_FAILS}회 — 실패 종료합니다." | tee -a "$MAIN_LOG" "$LAP_LOG"
      exit 1
    fi
  elif [ "$RESULT" -eq 0 ]; then
    COUNT=$((COUNT + 1))
    FAILS=0
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
