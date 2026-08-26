#!/bin/bash
# 시간별 루프 생존 감시 — 메인·속도레인 죽으면 자동 재기동 (오너 2026-08-24)
# 2026-08-26 재설계 (오너 보드 STOP 「루프가 안 돈다」 처리 — 12:11~19:27 7시간 공백 사고):
#  - 판정 축을 pgrep에서 바퀴 활동 신호(lap 로그 최신 mtime)로 교체.
#    실측: macOS pgrep -f가 launchd 자식 중 특정 프로세스(procargs 미노출)를 못 찾음
#    (2026-08-26 — PID 12651 loop.sh 살아있는데 rc=1 · speed_lane.sh는 정상 매칭).
#    프로세스 탐지를 믿으면 살아있는 루프를 「꺼짐」으로 오판한다.
#  - fresh(lap ≤ STALE_MIN 분 갱신)면 프로세스 유무와 무관하게 정상 통과.
#  - stale이면 kickstart(살아있어도 멈춘 것이므로 재시작), 실패 시 bootout+bootstrap 폴백.
#    세션 상한 LOOP_SESSION_TIMEOUT=1800s(30분)이므로 STALE_MIN=90은 정상 장기 작업 오탐 여유.
#  - 매 실행마다 판정 근거를 loop_watch.log에 남긴다. 침묵은 진단 불능이다.
#  - DRY_RUN=1 이면 launchctl 호출 대신 명령만 로그에 기록 (테스트용 — 실서비스 무영향).
#  - LOOP_WATCH_ROOT 로 저장소 루트 오버라이드 가능 (샌드박스 테스트용 — 판정 로직 격리 실측).
set -uo pipefail

R="${LOOP_WATCH_ROOT:-/Users/junholee/ai_lab}"
LOG="$R/logs/loop_watch.log"
UID_N=$(id -u)
DRY="${DRY_RUN:-0}"
STALE_MIN="${WATCH_STALE_MIN:-90}"

log(){ echo "[$(date '+%m-%d %H:%M')] $*" >> "$LOG"; }
run(){
  if [ "$DRY" = "1" ]; then log "DRY: $*"; return 0; fi
  "$@"
}

# 서비스 재기동: kickstart → 실패 시 bootstrap 폴백. 성공/실패 반드시 로그.
revive(){
  local label="$1" plist="$2"
  if run launchctl kickstart "gui/$UID_N/$label"; then
    log "재기동: kickstart gui/$UID_N/$label OK"
    return 0
  fi
  log "재기동: kickstart 실패 → bootstrap 폴백"
  run launchctl bootout "gui/$UID_N/$label" 2>/dev/null
  if run launchctl bootstrap "gui/$UID_N" "$plist"; then
    log "재기동: bootstrap $label OK"
  else
    log "재기동 실패: $label — 다음 주기 재시도"
  fi
}

# 파일 mtime(초 전). 파일 없으면 -1.
age_of(){
  local f="$1" m now
  m=$(stat -f %m "$f" 2>/dev/null || true)
  [ -z "$m" ] && { echo -1; return; }
  now=$(date +%s)
  echo $((now - m))
}

# 최신 lap 로그 mtime(초 전). logs/ 전체를 훑어 자정 걸침·날짜 디렉터리 누락을 피한다.
lap_age(){
  local newest m now
  newest=$(find "$R/logs" -type f -name 'lap-*.log' -print0 2>/dev/null | xargs -0 stat -f %m 2>/dev/null | sort -n | tail -1)
  [ -z "$newest" ] && { echo -1; return; }
  now=$(date +%s)
  echo $((now - newest))
}

### 메인 루프 감시 — 활동 신호(lap mtime) 단일 축
if [ -f "$R/loop/STOP" ]; then
  log "메인: STOP 파일 있음 — 감시 건너뜀 (lap 나이 $(lap_age)s)"
else
  AGE=$(lap_age)
  if [ "$AGE" != "-1" ] && [ "$AGE" -le $((STALE_MIN * 60)) ]; then
    log "메인 정상: lap ${AGE}초 전 갱신 (< ${STALE_MIN}분)"
  else
    if [ "$AGE" = "-1" ]; then
      log "메인 꺼짐 추정: lap 로그 부재 → 재기동"
    else
      log "메인 멈춤 의심: lap ${AGE}초 (${STALE_MIN}분 초과) 무활동 → 재기동"
    fi
    revive com.ailab.autonomous_loop "$HOME/Library/LaunchAgents/com.ailab.autonomous_loop.plist"
  fi
fi

### 속도 레인 감시 — pgrep 정상 매칭 실측(2026-08-26)이므로 현행 조건 유지
if [ ! -f "$R/loop/STOP_LANE" ] && ! pgrep -f "speed_lane.sh" >/dev/null; then
  log "레인 꺼짐: speed_lane 프로세스 없음 → 재기동"
  revive com.ailab.speedlane "$HOME/Library/LaunchAgents/com.ailab.speedlane.plist"
else
  SAGE=$(age_of "$R/logs/speed_lane.log")
  if [ "$SAGE" = "-1" ]; then
    log "레인 정상: speed_lane.log 아직 없음"
  else
    log "레인 정상: speed_lane.log ${SAGE}초 전 갱신"
  fi
fi
