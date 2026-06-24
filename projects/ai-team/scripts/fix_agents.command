#!/bin/bash
UID_VAL=$(id -u)
PLIST_DIR=~/ai_lab/projects/ai-team/scripts/launchd
LAUNCH_DIR=~/Library/LaunchAgents

echo "=== 전체 plist 업데이트 & 재시작 ==="

# 잘못된 위치 파일 정리
rm -rf ~/ai_lab/projects/reports 2>/dev/null && echo "✅ projects/reports 정리" || true

# 변경된 plist 리로드 (youngsuk, somi, kodari, harness)
for agent in youngsuk somi kodari; do
  launchctl unload "$LAUNCH_DIR/com.ailab.$agent.plist" 2>/dev/null
  cp "$PLIST_DIR/com.ailab.$agent.plist" "$LAUNCH_DIR/"
  launchctl load "$LAUNCH_DIR/com.ailab.$agent.plist"
  echo "✅ $agent plist 리로드"
done

# 하네스 plist 신규 등록
if ! launchctl list | grep -q com.ailab.harness; then
  cp "$PLIST_DIR/com.ailab.harness.plist" "$LAUNCH_DIR/"
  launchctl load "$LAUNCH_DIR/com.ailab.harness.plist"
  echo "✅ harness plist 등록"
fi

# 영숙 재시작 (ThrottleInterval 30s 반영)
echo "영숙 재시작..."
launchctl kickstart -k gui/$UID_VAL/com.ailab.youngsuk
sleep 10

echo ""
echo "=== 영숙 로그 ==="
tail -5 ~/ai_lab/output/trading_logs/youngsuk_daemon.out.log

echo ""
echo "=== 프로세스 상태 ==="
for script in upbit_auto_trader leo_aggressive_trader market_signal telegram_receiver agent_health_monitor; do
  pgrep -f "$script" > /dev/null 2>&1 && echo "✅ $script" || echo "❌ $script"
done

read -p "엔터로 닫기"
