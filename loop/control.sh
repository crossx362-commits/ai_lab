#!/bin/bash
# 자율 개발 루프의 유일한 시작·중단·상태 진입점.
set -uo pipefail

ROOT="${LOOP_CONTROL_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
LOOP_DIR="$ROOT/loop"
STATE_TOOL="$LOOP_DIR/runtime_state.py"
STATE_FILE="${LOOP_RUNTIME_STATE_FILE:-$LOOP_DIR/runtime_state.json}"
AGENT_FILE="$LOOP_DIR/agent"
STOP_FILE="$LOOP_DIR/STOP"
HOLD_FILE="$LOOP_DIR/HOLD"
DEPLOY="$LOOP_DIR/deploy_launchd.sh"
LAUNCHCTL="${LOOP_LAUNCHCTL_BIN:-launchctl}"
LAUNCH_AGENTS="${LOOP_LAUNCH_AGENTS_DIR:-$HOME/Library/LaunchAgents}"
LABEL="com.ailab.autonomous_loop"
SPEED_LABEL="com.ailab.speedlane"
UID_N="${LOOP_CONTROL_UID:-$(id -u)}"
DOMAIN="gui/$UID_N"
SERVICE="$DOMAIN/$LABEL"
SPEED_SERVICE="$DOMAIN/$SPEED_LABEL"
PLIST="$LAUNCH_AGENTS/$LABEL.plist"
WAIT_ATTEMPTS="${LOOP_CONTROL_WAIT_ATTEMPTS:-15}"
WAIT_SECONDS="${LOOP_CONTROL_WAIT_SECONDS:-1}"
STOP_WAIT_ATTEMPTS="${LOOP_CONTROL_STOP_WAIT_ATTEMPTS:-30}"

if ! [[ "$WAIT_ATTEMPTS" =~ ^[0-9]+$ && "$STOP_WAIT_ATTEMPTS" =~ ^[0-9]+$ \
    && "$WAIT_SECONDS" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
  echo "제어 대기 설정이 숫자가 아닙니다." >&2
  exit 2
fi

state_set() {
  local phase="$1" provider="${2:-}" reason="${3:-}"
  [ -f "$STATE_TOOL" ] || return 1
  python3 "$STATE_TOOL" --path "$STATE_FILE" set "$phase" \
    --provider "$provider" --reason "$reason" --retry-at 0 >/dev/null
}

state_get() {
  local field="$1"
  if [ -f "$STATE_TOOL" ]; then
    python3 "$STATE_TOOL" --path "$STATE_FILE" get "$field" 2>/dev/null || true
  fi
}

configured_agent() {
  local value="${1:-}"
  if [ -z "$value" ] && [ -n "${LOOP_AGENT:-}" ]; then
    value="$LOOP_AGENT"
  fi
  if [ -z "$value" ] && [ -f "$AGENT_FILE" ]; then
    value="$(tr -d '[:space:]' < "$AGENT_FILE")"
  fi
  if [ -z "$value" ]; then
    value="$(printf '%s' "${LOOP_PROVIDERS:-grok}" | awk -F, '{print $1}' | tr -d '[:space:]')"
  fi
  case "$value" in
    claude|codex|grok|opencode) printf '%s\n' "$value" ;;
    *) echo "지원하지 않는 AI: $value" >&2; return 2 ;;
  esac
}

write_agent() {
  local provider="$1" temporary="$AGENT_FILE.tmp.$$"
  mkdir -p "$LOOP_DIR"
  printf '%s\n' "$provider" > "$temporary"
  mv "$temporary" "$AGENT_FILE"
}

service_info() {
  "$LAUNCHCTL" print "$SERVICE" 2>/dev/null
}

service_loaded() {
  service_info >/dev/null
}

service_pid() {
  service_info | awk '/^[[:space:]]*pid = [1-9][0-9]*/ { print $3; exit }'
}

service_running() {
  [ -n "$(service_pid)" ]
}

start_confirmed() {
  local before_heartbeat="$1" attempt=0 current_heartbeat
  while [ "$attempt" -lt "$WAIT_ATTEMPTS" ]; do
    if service_running; then
      return 0
    fi
    current_heartbeat="$(state_get heartbeat_at)"
    case "$current_heartbeat" in
      ''|*[!0-9]*) current_heartbeat=0 ;;
    esac
    if [ "$current_heartbeat" -gt "$before_heartbeat" ]; then
      return 0
    fi
    attempt=$((attempt + 1))
    [ "$attempt" -lt "$WAIT_ATTEMPTS" ] && sleep "$WAIT_SECONDS"
  done
  return 1
}

start_loop() {
  local provider before_heartbeat pid
  provider="$(configured_agent "${1:-}")" || return $?
  before_heartbeat="$(state_get heartbeat_at)"
  case "$before_heartbeat" in
    ''|*[!0-9]*) before_heartbeat=0 ;;
  esac

  rm -f "$STOP_FILE" "$HOLD_FILE"
  write_agent "$provider"

  if ! bash "$DEPLOY" --register-only; then
    state_set recovering "$provider" "launchd 등록 실패" || true
    echo "시작 실패: launchd 등록을 완료하지 못했습니다." >&2
    return 1
  fi

  # 비용 정책상 speed lane은 항상 중단 상태를 유지한다.
  "$LAUNCHCTL" bootout "$SPEED_SERVICE" >/dev/null 2>&1 || true

  if service_running; then
    pid="$(service_pid)"
    echo "이미 실행 중: provider=$provider pid=$pid"
    return 0
  fi

  if ! service_loaded; then
    if ! "$LAUNCHCTL" bootstrap "$DOMAIN" "$PLIST"; then
      state_set recovering "$provider" "launchd bootstrap 실패" || true
      echo "시작 실패: launchd bootstrap 오류" >&2
      return 1
    fi
  fi
  "$LAUNCHCTL" enable "$SERVICE" >/dev/null 2>&1 || true
  if ! service_running; then
    if ! "$LAUNCHCTL" kickstart "$SERVICE"; then
      state_set recovering "$provider" "launchd kickstart 실패" || true
      echo "시작 실패: launchd kickstart 오류" >&2
      return 1
    fi
  fi

  if ! start_confirmed "$before_heartbeat"; then
    state_set recovering "$provider" "시작 뒤 PID/heartbeat 확인 실패" || true
    echo "시작 실패: PID 또는 heartbeat를 확인하지 못했습니다." >&2
    return 1
  fi

  pid="$(service_pid)"
  echo "시작 확인: provider=$provider${pid:+ pid=$pid}"
}

stop_loop() {
  local provider attempt=0
  provider="$(configured_agent "")" || provider=""
  mkdir -p "$LOOP_DIR"
  touch "$STOP_FILE"
  state_set owner_stopped "$provider" "오너 stop 명령" || true

  while [ "$attempt" -lt "$STOP_WAIT_ATTEMPTS" ]; do
    if ! service_running; then
      echo "중단 확인: owner_stopped"
      return 0
    fi
    attempt=$((attempt + 1))
    [ "$attempt" -lt "$STOP_WAIT_ATTEMPTS" ] && sleep "$WAIT_SECONDS"
  done
  echo "중단 신호 기록: 현재 바퀴가 끝나면 종료됩니다."
  return 0
}

show_status() {
  local phase provider heartbeat pid
  phase="$(state_get phase)"
  provider="$(state_get provider)"
  heartbeat="$(state_get heartbeat_at)"
  pid="$(service_pid)"
  echo "phase=${phase:-owner_stopped}"
  echo "provider=${provider:-$(configured_agent "" 2>/dev/null || true)}"
  echo "heartbeat_at=${heartbeat:-0}"
  echo "pid=${pid:-none}"
  [ -f "$STOP_FILE" ] && echo "stop=present" || echo "stop=absent"
}

case "${1:-}" in
  start) start_loop "${2:-}" ;;
  stop) stop_loop ;;
  status) show_status ;;
  *)
    echo "사용법: bash loop/control.sh <start|stop|status> [claude|codex|grok|opencode]" >&2
    exit 2
    ;;
esac
