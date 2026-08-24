#!/bin/bash
# 레포 loop/ → Application Support 배포 + launchd 등록
#   bash loop/deploy_launchd.sh           # 배포 + 즉시 시작
#   bash loop/deploy_launchd.sh --no-start  # 배포·plist만. 지금은 안 켠다 (오너 명세 [5])
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$HOME/Library/Application Support/AI Lab Autonomous Loop"
AGENT="$HOME/Library/LaunchAgents"
LABEL=com.ailab.autonomous_loop
SPEED_LABEL=com.ailab.speedlane
uid="$(id -u)"

NO_START=0
if [ "${1:-}" = "--no-start" ]; then
  NO_START=1
fi

mkdir -p "$APP" "$AGENT"
cp "$ROOT/loop/loop.sh" "$APP/loop.sh"
cp "$ROOT/loop/env.sh" "$APP/env.sh"
cp "$ROOT/loop/PROMPT.md" "$APP/PROMPT.md"
cp "$ROOT/loop/agent_runner.py" "$APP/agent_runner.py"
cp "$ROOT/loop/council.sh" "$APP/council.sh"
cp "$ROOT/loop/SPEED_PROMPT.md" "$APP/SPEED_PROMPT.md"
cp "$ROOT/loop/TASKS.json" "$APP/TASKS.json"
cp "$ROOT/loop/speed_lane.sh" "$APP/speed_lane.sh"
cp "$ROOT/loop/merge_integration.sh" "$APP/merge_integration.sh"
chmod +x "$APP/loop.sh" "$APP/council.sh" "$APP/speed_lane.sh" "$APP/merge_integration.sh"
cp "$ROOT/loop/com.ailab.autonomous_loop.plist" "$AGENT/$LABEL.plist"

if [ -f "$ROOT/loop/com.ailab.speedlane.plist" ]; then
  cp "$ROOT/loop/com.ailab.speedlane.plist" "$AGENT/$SPEED_LABEL.plist"
fi

# 메인 루프는 항상 내린 뒤, --no-start 가 아니면 다시 올린다.
# 속도 레인은 --no-start 에서 건드리지 않는다(메인만 시험할 때 레인 작업을 죽이지 않기 위해).
launchctl bootout "gui/$uid/$LABEL" 2>/dev/null || true

if [ "$NO_START" -eq 1 ]; then
  echo "배포 완료(시작 안 함): $APP"
  echo "등록된 plist: $AGENT/$LABEL.plist  (RunAtLoad=true · 다음 로그인에 시작)"
  echo "켤 때: bash $ROOT/loop/deploy_launchd.sh"
  echo "끌 때: touch $ROOT/loop/STOP"
  exit 0
fi

if [ -f "$AGENT/$SPEED_LABEL.plist" ]; then
  launchctl bootout "gui/$uid/$SPEED_LABEL" 2>/dev/null || true
fi
launchctl bootstrap "gui/$uid" "$AGENT/$LABEL.plist"
launchctl enable "gui/$uid/$LABEL"
if [ -f "$AGENT/$SPEED_LABEL.plist" ]; then
  launchctl bootstrap "gui/$uid" "$AGENT/$SPEED_LABEL.plist"
  launchctl enable "gui/$uid/$SPEED_LABEL"
fi
echo "배포 완료: $APP"
echo "상태: launchctl print gui/$uid/$LABEL | head"
