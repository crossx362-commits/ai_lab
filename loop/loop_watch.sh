#!/bin/bash
# 시간별 루프 생존 감시 — 메인·속도레인 죽으면 자동 재기동 (오너 2026-08-24)
set -uo pipefail
R=/Users/junholee/ai_lab
LOG="$R/logs/loop_watch.log"
UID_N=$(id -u)
log(){ echo "[$(date '+%m-%d %H:%M')] $*" >> "$LOG"; }
if [ ! -f "$R/loop/STOP" ] && ! pgrep -f "AI Lab Autonomous Loop/loop.sh" >/dev/null; then
  launchctl kickstart "gui/$UID_N/com.ailab.autonomous_loop" && log "메인 루프 꺼짐 → 재기동"
fi
if [ ! -f "$R/loop/STOP_LANE" ] && ! pgrep -f "speed_lane.sh" >/dev/null; then
  launchctl kickstart "gui/$UID_N/com.ailab.speedlane" && log "속도 레인 꺼짐 → 재기동"
fi
NEW=$(find "$R/logs/$(date +%Y-%m-%d)" -name 'lap-*.log' -mmin -120 2>/dev/null | wc -l | tr -d ' ')
[ "$NEW" = "0" ] && log "경고: 최근 2시간 새 바퀴 없음 (멈춤 의심)"
