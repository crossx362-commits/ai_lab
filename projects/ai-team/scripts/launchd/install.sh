#!/bin/bash
# launchd 에이전트 설치 및 시작
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LAUNCH_AGENTS="$HOME/Library/LaunchAgents"
AGENTS=(com.ailab.signal com.ailab.dave com.ailab.leo com.ailab.youngsuk)

# 기존 실행 중인 프로세스 정리
echo "기존 프로세스 정리 중..."
pkill -f "upbit_auto_trader.py" 2>/dev/null || true
pkill -f "leo_aggressive_trader.py" 2>/dev/null || true
pkill -f "market_signal.py" 2>/dev/null || true
pkill -f "market_pulse.py" 2>/dev/null || true
pkill -f "telegram_receiver.py" 2>/dev/null || true
pkill -f "start_trading_team.py" 2>/dev/null || true
rm -f /tmp/ailab_locks/*.lock 2>/dev/null || true
sleep 2

# 기존 등록된 에이전트 제거
for agent in "${AGENTS[@]}"; do
    launchctl unload "$LAUNCH_AGENTS/$agent.plist" 2>/dev/null || true
done

# plist 파일 복사 및 등록
mkdir -p "$HOME/ai_lab/output/trading_logs"
for agent in "${AGENTS[@]}"; do
    cp "$SCRIPT_DIR/$agent.plist" "$LAUNCH_AGENTS/"
    launchctl load "$LAUNCH_AGENTS/$agent.plist"
    echo "✅ $agent 등록 완료"
done

echo ""
echo "✅ 모든 에이전트 설치 완료"
echo "상태 확인: launchctl list | grep com.ailab"
echo "중지:      $SCRIPT_DIR/uninstall.sh"
