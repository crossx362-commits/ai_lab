#!/bin/bash
# 매 바퀴 새 agent_runner.py 프로세스를 여는 자율 개발 루프.

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
IDLE_WAIT="${LOOP_IDLE_WAIT:-60}"
PYTHON_BIN="${LOOP_PYTHON:-/opt/homebrew/bin/python3}"
RUNNER="$DEPLOY_ROOT/agent_runner.py"
PROMPT_FILE="$DEPLOY_ROOT/PROMPT.md"
STOP_FILE="$TARGET_REPO/loop/STOP"
MAIN_LOG="$TARGET_REPO/logs/loop_main.log"

if [ ! -x "$PYTHON_BIN" ]; then
  PYTHON_BIN="$(command -v python3 2>/dev/null || true)"
fi
if [ -z "$PYTHON_BIN" ] || [ ! -f "$RUNNER" ] || [ ! -f "$PROMPT_FILE" ]; then
  echo "자율 루프 실행 파일이 빠졌습니다: python/agent_runner.py/PROMPT.md" >&2
  exit 1
fi
if ! [[ "$MAX_LOOPS" =~ ^[0-9]+$ && "$COOLDOWN" =~ ^[0-9]+$ && "$IDLE_WAIT" =~ ^[0-9]+$ ]]; then
  echo "LOOP_MAX_LOOPS/LOOP_COOLDOWN/LOOP_IDLE_WAIT는 0 이상의 정수여야 합니다." >&2
  exit 1
fi

mkdir -p "$TARGET_REPO/logs"
export PYTHONUNBUFFERED=1
COUNT=0

wait_with_stop() {
  local remaining="$1"
  while [ "$remaining" -gt 0 ]; do
    [ -f "$STOP_FILE" ] && return 1
    sleep 1
    remaining=$((remaining - 1))
  done
  return 0
}

echo "자율 개발 루프 시작: target=$TARGET_REPO mode=${LOOP_MODE:-auto} max=${MAX_LOOPS:-0}"

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

  echo "바퀴 시작: $LAP_ID"
  "$PYTHON_BIN" -u "$RUNNER" \
    --repo-root "$TARGET_REPO" \
    --prompt-file "$PROMPT_FILE" \
    > >(tee -a "$MAIN_LOG" "$LAP_LOG") 2>&1
  RESULT=$?

  if [ "$RESULT" -eq 0 ]; then
    COUNT=$((COUNT + 1))
  elif [ "$RESULT" -eq 10 ] || [ "$RESULT" -eq 75 ]; then
    echo "이번 poll은 계수하지 않습니다 (code=$RESULT)."
  elif [ "$RESULT" -eq 20 ]; then
    exit 0
  else
    echo "coordinator 치명 오류 (code=$RESULT)" >&2
    exit "$RESULT"
  fi

  if [ -f "$STOP_FILE" ]; then
    echo "현재 바퀴 완료 후 STOP 확인 — 정상 종료합니다."
    exit 0
  fi

  WAIT_SECONDS="$COOLDOWN"
  if [ "$RESULT" -eq 10 ] || [ "$RESULT" -eq 75 ]; then
    WAIT_SECONDS="$IDLE_WAIT"
  fi
  wait_with_stop "$WAIT_SECONDS" || exit 0
done
