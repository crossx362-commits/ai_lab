#!/bin/bash
# launchd 에이전트 중지 및 제거
LAUNCH_AGENTS="$HOME/Library/LaunchAgents"
AGENTS=(com.ailab.hyunbin com.ailab.dave com.ailab.leo com.ailab.youngsuk com.ailab.cleanup)

for agent in "${AGENTS[@]}"; do
    launchctl unload "$LAUNCH_AGENTS/$agent.plist" 2>/dev/null && echo "✅ $agent 중지" || echo "⚠️  $agent 이미 중지됨"
    rm -f "$LAUNCH_AGENTS/$agent.plist"
done

echo ""
echo "✅ 모든 에이전트 제거 완료"
