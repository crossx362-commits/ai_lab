#!/bin/bash
# 예원 하네스 워치독 설치(상시, KeepAlive). 더블클릭 실행 — 로그인 셸에서 python 자동 감지.
# 재부팅·크래시에도 launchd가 워치독을 되살리고, 워치독이 나머지 상시 데몬을 복구한다.
set -u
PY="$(command -v python3 || command -v python)"
LA="$HOME/Library/LaunchAgents"
PLIST="$LA/com.ailab.yewon_monitor.plist"
STATUS="/Users/junholee/ai_lab/output/cache/yewon_monitor_install.txt"
SCRIPT="/Users/junholee/ai_lab/projects/ai-team/skills/예원_CEO/tools/harness_monitor.py"
mkdir -p "$LA" "/Users/junholee/ai_lab/output/bot_logs" "$(dirname "$STATUS")"

# nohup 등 기존 수동 인스턴스 정리 — launchd가 단일 소유자가 되도록
pkill -f "$SCRIPT" 2>/dev/null

cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.ailab.yewon_monitor</string>
    <key>ProgramArguments</key>
    <array>
        <string>${PY}</string>
        <string>-u</string>
        <string>${SCRIPT}</string>
    </array>
    <key>RunAtLoad</key><true/>
    <key>KeepAlive</key><true/>
    <key>WorkingDirectory</key><string>/Users/junholee/ai_lab</string>
    <key>EnvironmentVariables</key>
    <dict><key>HOME</key><string>${HOME}</string><key>PYTHONUTF8</key><string>1</string></dict>
    <key>StandardOutPath</key><string>/Users/junholee/ai_lab/output/bot_logs/com.ailab.yewon_monitor.out.log</string>
    <key>StandardErrorPath</key><string>/Users/junholee/ai_lab/output/bot_logs/com.ailab.yewon_monitor.err.log</string>
</dict>
</plist>
EOF

launchctl unload "$PLIST" 2>/dev/null
launchctl load -w "$PLIST" 2>/tmp/yewon_monitor_load.err
RC=$?
LIST="$(launchctl list 2>/dev/null | grep yewon_monitor || echo 'NOT_LISTED')"
{
  echo "ts=$(date '+%Y-%m-%d %H:%M:%S')"
  echo "python=${PY}"
  echo "load_rc=${RC}"
  echo "launchctl_list=${LIST}"
} > "$STATUS"

echo ""
echo "==================================================="
echo " 예원 하네스 워치독 설치 완료 (상시 KeepAlive)"
echo "  python : ${PY}"
echo "  결과   : ${LIST}"
echo "==================================================="
echo " 이 창은 닫으셔도 됩니다."
sleep 2
