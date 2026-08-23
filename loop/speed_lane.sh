#!/bin/bash
# 속도 레인 — agent_runner 기반 병렬 트랙 (오너 2026-08-23 개발 속도 향상 지시).
# 최대 3개 작업을 worktree 격리 + worker/reviewer로 동시 수행, 승분은 autonomous/integration.
# master 병합은 메인 루프의 각 바퀴가 끝날 때 merge_integration.sh가 시도한다.

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"
[ -f "$DEPLOY_ROOT/env.sh" ] && source "$DEPLOY_ROOT/env.sh"

STOP_FILE="$TARGET_REPO/loop/STOP_LANE"
MAIN_LOG="$TARGET_REPO/logs/speed_lane.log"
IDLE_WAIT="${LOOP_IDLE_WAIT:-60}"

log() { echo "[$(date '+%m-%d %H:%M:%S')] $*" | tee -a "$MAIN_LOG"; }

while true; do
  [ -f "$STOP_FILE" ] && { log "STOP_LANE 확인 — 정상 종료"; exit 0; }

  if [ ! -s "$TARGET_REPO/loop/TASKS.json" ]; then
    log "TASKS.json 비어 있음 — ${IDLE_WAIT}초 대기"
    sleep "$IDLE_WAIT"
    continue
  fi

  log "병렬 랩 시작 (mode=parallel, max=${LOOP_MAX_PARALLEL:-3})"
  LOOP_MODE=parallel \
  python3 "$DEPLOY_ROOT/agent_runner.py" \
      --repo-root "$TARGET_REPO" \
      --prompt-file "$DEPLOY_ROOT/SPEED_PROMPT.md" \
      --task-file "$TARGET_REPO/loop/TASKS.json" >> "$MAIN_LOG" 2>&1
  RC=$?
  # 0=완료 · 10=할 일 없음(idle) · 75=잠금/인프라 홀드
  case "$RC" in
    0)  bash "$DEPLOY_ROOT/merge_integration.sh" "$TARGET_REPO" >> "$MAIN_LOG" 2>&1 || true
        log "랩 완료 — integration 병합 시도" ;;
    10) log "할 일 없음 — ${IDLE_WAIT}초 대기"; sleep "$IDLE_WAIT" ;;
    75) log "홀드(잠금/제공자 없음) — ${IDLE_WAIT}초 대기"; sleep "$IDLE_WAIT" ;;
    *)  log "비정상 종료 rc=$RC — ${IDLE_WAIT}초 대기"; sleep "$IDLE_WAIT" ;;
  esac
done
