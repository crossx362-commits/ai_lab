#!/bin/bash
# 자율 루프 상태 감시. heartbeat와 launchd PID만 보고 단일 control 경로로 재개한다.
set -uo pipefail

R="${LOOP_WATCH_ROOT:-/Users/junholee/ai_lab}"
LOG="$R/logs/loop_watch.log"
STATE_TOOL="$R/loop/runtime_state.py"
STATE_FILE="${LOOP_RUNTIME_STATE_FILE:-$R/loop/runtime_state.json}"
CONTROL="$R/loop/control.sh"
DRY="${DRY_RUN:-0}"
STALE_SECONDS="${WATCH_STALE_SECONDS:-$(( ${WATCH_STALE_MIN:-90} * 60 ))}"

mkdir -p "$R/logs"

log() {
  echo "[$(date '+%m-%d %H:%M')] $*" >> "$LOG"
}

state_get() {
  python3 "$STATE_TOOL" --path "$STATE_FILE" get "$1" 2>/dev/null || true
}

control_pid() {
  bash "$CONTROL" status 2>/dev/null \
    | awk -F= '$1 == "pid" && $2 ~ /^[1-9][0-9]*$/ { print $2; exit }'
}

resume_main() {
  local provider="$1" force="$2"
  if [ "$DRY" = "1" ]; then
    log "DRY: LOOP_CONTROL_RESTART_STALE=$force bash $CONTROL ensure-running $provider"
    return 0
  fi
  if LOOP_CONTROL_RESTART_STALE="$force" \
      bash "$CONTROL" ensure-running "$provider" >> "$LOG" 2>&1; then
    log "메인 재개 확인: provider=$provider force=$force"
  else
    log "메인 재개 실패: provider=$provider force=$force — 다음 주기 재확인"
  fi
}

if ! [[ "$STALE_SECONDS" =~ ^[1-9][0-9]*$ ]]; then
  log "감시 설정 오류: WATCH_STALE_SECONDS=$STALE_SECONDS"
  exit 2
fi
if [ ! -f "$STATE_TOOL" ] || [ ! -f "$CONTROL" ]; then
  log "감시 구성 오류: runtime_state.py 또는 control.sh 없음"
  exit 1
fi

if [ -f "$R/loop/STOP" ]; then
  log "메인: 오너 STOP 있음 — 감시 건너뜀"
  exit 0
fi

PHASE="$(state_get phase)"
PROVIDER="$(state_get provider)"
HEARTBEAT="$(state_get heartbeat_at)"
[ -n "$PROVIDER" ] || PROVIDER="grok"
case "$HEARTBEAT" in
  ''|*[!0-9]*) HEARTBEAT=0 ;;
esac
NOW="$(date +%s)"
AGE=$((NOW - HEARTBEAT))
[ "$AGE" -ge 0 ] || AGE=0

case "$PHASE" in
  owner_stopped|'')
    log "메인: owner_stopped — 감시 건너뜀"
    exit 0
    ;;
  running|quota_wait|recovering)
    ;;
  *)
    log "메인: 알 수 없는 phase=$PHASE — 자동 시작하지 않음"
    exit 0
    ;;
esac

PID="$(control_pid)"
if [ -z "$PID" ]; then
  log "메인 서비스 없음: phase=$PHASE heartbeat=${AGE}초 → 단일 제어 경로 재개"
  resume_main "$PROVIDER" 0
  exit 0
fi

if [ "$AGE" -gt "$STALE_SECONDS" ]; then
  log "메인 heartbeat 정체: phase=$PHASE pid=$PID age=${AGE}초 → 원본 강제 재기동"
  resume_main "$PROVIDER" 1
  exit 0
fi

log "메인 정상: phase=$PHASE pid=$PID heartbeat=${AGE}초 전"
# speed lane은 비용 정책상 상시 중단이며 감시자가 재기동하지 않는다.
