#!/bin/bash
# 보드 지킴이 launchd 등록 — 30분마다 검증 (RunAtLoad 포함)
set -euo pipefail
AGENT="$HOME/Library/LaunchAgents"
LABEL=com.ailab.boardkeeper

mkdir -p "$AGENT"
cp "$(cd "$(dirname "$0")" && pwd)/com.ailab.boardkeeper.plist" "$AGENT/$LABEL.plist"
uid="$(id -u)"
launchctl bootout "gui/$uid/$LABEL" 2>/dev/null || true
launchctl bootstrap "gui/$uid" "$AGENT/$LABEL.plist"
launchctl enable "gui/$uid/$LABEL"
echo "지킴이 등록 완료 — 상태: launchctl list | grep boardkeeper"
