#!/bin/bash
# 레포 loop/ → Application Support 배포 + launchd 재등록
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$HOME/Library/Application Support/AI Lab Autonomous Loop"
AGENT="$HOME/Library/LaunchAgents"
LABEL=com.ailab.autonomous_loop

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

# 속도 레인 등록 (com.ailab.speedlane)
SPEED_LABEL=com.ailab.speedlane
if [ -f "$ROOT/loop/com.ailab.speedlane.plist" ]; then
  cp "$ROOT/loop/com.ailab.speedlane.plist" "$AGENT/$SPEED_LABEL.plist"
  launchctl bootout "gui/$uid/$SPEED_LABEL" 2>/dev/null || true
  launchctl bootstrap "gui/$uid" "$AGENT/$SPEED_LABEL.plist"
  launchctl enable "gui/$uid/$SPEED_LABEL"
fi

# 재등록 (이미 떠 있으면 bootout 후 bootstrap)
uid="$(id -u)"
launchctl bootout "gui/$uid/$LABEL" 2>/dev/null || true
launchctl bootstrap "gui/$uid" "$AGENT/$LABEL.plist"
launchctl enable "gui/$uid/$LABEL"
echo "배포 완료: $APP"
echo "상태: launchctl print gui/$uid/$LABEL | head"
