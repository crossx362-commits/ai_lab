#!/bin/bash
# 예원 자가점검·복구 서비스 설치(매일 08:00). 더블클릭 실행 — 로그인 셸에서 python 자동 감지.
set -u
PY="$(command -v python3 || command -v python)"
LA="$HOME/Library/LaunchAgents"
PLIST="$LA/com.ailab.yewon_selfheal.plist"
STATUS="/Users/junholee/ai_lab/output/cache/yewon_selfheal_install.txt"
SCRIPT="/Users/junholee/ai_lab/projects/ai-team/skills/예원_CEO/tools/yewon_self_heal.py"
mkdir -p "$LA" "/Users/junholee/ai_lab/output/bot_logs" "$(dirname "$STATUS")"

cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>com.ailab.yewon_selfheal</string>
    <key>ProgramArguments</key>
    <array>
        <string>${PY}</string>
        <string>${SCRIPT}</string>
    </array>
    <key>StartCalendarInterval</key>
    <dict><key>Hour</key><integer>8</integer><key>Minute</key><integer>0</integer></dict>
    <key>RunAtLoad</key><false/>
    <key>WorkingDirectory</key><string>/Users/junholee/ai_lab</string>
    <key>EnvironmentVariables</key>
    <dict><key>PYTHONUTF8</key><string>1</string></dict>
    <key>StandardOutPath</key><string>/Users/junholee/ai_lab/output/bot_logs/yewon_selfheal.out.log</string>
    <key>StandardErrorPath</key><string>/Users/junholee/ai_lab/output/bot_logs/yewon_selfheal.err.log</string>
</dict>
</plist>
EOF

launchctl unload "$PLIST" 2>/dev/null
launchctl load -w "$PLIST" 2>/tmp/yewon_selfheal_load.err
RC=$?
LIST="$(launchctl list 2>/dev/null | grep yewon_selfheal || echo 'NOT_LISTED')"
{
  echo "ts=$(date '+%Y-%m-%d %H:%M:%S')"
  echo "python=${PY}"
  echo "load_rc=${RC}"
  echo "launchctl_list=${LIST}"
} > "$STATUS"

echo ""
echo "==================================================="
echo " 예원 자가점검 서비스 설치 완료 (매일 08:00)"
echo "  python : ${PY}"
echo "  결과   : ${LIST}"
echo "==================================================="
echo " 이 창은 닫으셔도 됩니다."
sleep 2
