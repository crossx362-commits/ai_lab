#!/bin/bash
# 저장소 원본을 직접 실행하는 launchd plist 등록.
#   bash loop/deploy_launchd.sh --register-only  # 실행 상태를 바꾸지 않고 plist만 설치
#   bash loop/deploy_launchd.sh --no-start       # 이전 호환 별칭
#   bash loop/deploy_launchd.sh                  # 단일 제어 경로로 시작
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
AGENT="${LOOP_LAUNCH_AGENTS_DIR:-$HOME/Library/LaunchAgents}"
LABEL=com.ailab.autonomous_loop
SPEED_LABEL=com.ailab.speedlane

case "${1:-}" in
  --register-only|--no-start)
    mkdir -p "$AGENT"
    cp "$ROOT/loop/com.ailab.autonomous_loop.plist" "$AGENT/$LABEL.plist"
    cp "$ROOT/loop/com.ailab.speedlane.plist" "$AGENT/$SPEED_LABEL.plist"
    echo "plist 등록 완료(실행 상태 유지): $AGENT/$LABEL.plist"
    ;;
  '')
    exec bash "$ROOT/loop/control.sh" start
    ;;
  *)
    echo "사용법: bash loop/deploy_launchd.sh [--register-only|--no-start]" >&2
    exit 2
    ;;
esac
