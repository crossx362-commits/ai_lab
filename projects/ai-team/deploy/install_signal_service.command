#!/bin/bash
# 소미 신호엔진 launchd 서비스 설치 (더블클릭 실행).
# 로그인 셸에서 실제 python 경로를 감지해 plist를 생성·로드한다.
set -u

PY="$(command -v python3 || command -v python)"
LA="$HOME/Library/LaunchAgents"
PLIST="$LA/com.ailab.somi_signal.plist"
STATUS="/Users/junholee/ai_lab/output/cache/signal_install_status.txt"
SCRIPT="/Users/junholee/ai_lab/projects/ai-team/skills/소미_분석가/tools/somi_signal_engine.py"
mkdir -p "$LA" "/Users/junholee/ai_lab/output/trading_logs" "$(dirname "$STATUS")"

cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.ailab.somi_signal</string>
    <key>ProgramArguments</key>
    <array>
        <string>${PY}</string>
        <string>${SCRIPT}</string>
        <string>--scan</string>
        <string>--session-only</string>
    </array>
    <key>StartInterval</key><integer>600</integer>
    <key>RunAtLoad</key><true/>
    <key>WorkingDirectory</key><string>/Users/junholee/ai_lab</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>PYTHONUTF8</key><string>1</string>
        <key>KIS_PAPER</key><string>true</string>
        <key>SOMI_BUDGET_PER_TRADE</key><string>1000000</string>
        <key>SOMI_SIGNAL_TOP</key><string>3</string>
        <key>SOMI_SESSION_WINDOWS</key><string>08:30-08:40,09:00-20:00</string>
    </dict>
    <key>StandardOutPath</key><string>/Users/junholee/ai_lab/output/trading_logs/somi_signal.out.log</string>
    <key>StandardErrorPath</key><string>/Users/junholee/ai_lab/output/trading_logs/somi_signal.err.log</string>
</dict>
</plist>
EOF

launchctl unload "$PLIST" 2>/dev/null
launchctl load -w "$PLIST" 2>/tmp/somi_signal_load.err
RC=$?
LIST="$(launchctl list 2>/dev/null | grep somi_signal || echo 'NOT_LISTED')"

{
  echo "ts=$(date '+%Y-%m-%d %H:%M:%S')"
  echo "python=${PY}"
  echo "load_rc=${RC}"
  echo "launchctl_list=${LIST}"
  echo "load_err=$(cat /tmp/somi_signal_load.err 2>/dev/null)"
} > "$STATUS"

echo ""
echo "==================================================="
echo " 소미 신호엔진 서비스 설치 완료"
echo "  python : ${PY}"
echo "  결과   : ${LIST}"
echo "==================================================="
echo " 이 창은 닫으셔도 됩니다."
sleep 2
