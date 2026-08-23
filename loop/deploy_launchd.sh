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
chmod +x "$APP/loop.sh"
cp "$ROOT/loop/com.ailab.autonomous_loop.plist" "$AGENT/$LABEL.plist"

# 재등록 (이미 떠 있으면 bootout 후 bootstrap)
uid="$(id -u)"
launchctl bootout "gui/$uid/$LABEL" 2>/dev/null || true
launchctl bootstrap "gui/$uid" "$AGENT/$LABEL.plist"
launchctl enable "gui/$uid/$LABEL"
echo "배포 완료: $APP"
echo "상태: launchctl print gui/$uid/$LABEL | head"
