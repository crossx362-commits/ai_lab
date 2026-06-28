#!/bin/bash
# 영숙(텔레그램 비서) 데몬 재시작 — 새 거래모드/신호 코드 반영. 더블클릭 실행.
set -u
PY="$(command -v python3 || command -v python)"
ROOT="/Users/junholee/ai_lab"
AC="$ROOT/projects/ai-team/skills/영숙_비서/tools/agent_controller.py"
STATUS="$ROOT/output/cache/youngsuk_restart_status.txt"
mkdir -p "$(dirname "$STATUS")"

cd "$ROOT" || exit 1
{
  echo "ts=$(date '+%Y-%m-%d %H:%M:%S')"
  echo "python=${PY}"
  echo "--- stop ---"
  "$PY" "$AC" 영숙 stop 2>&1
  # 잔여 프로세스 강제 정리(새 코드 확실히 로드)
  pkill -f "telegram_receiver.py" 2>/dev/null && echo "pkill: 잔여 telegram_receiver 종료" || echo "pkill: 잔여 없음"
  sleep 2
  echo "--- start ---"
  "$PY" "$AC" 영숙 start 2>&1
  sleep 1
  echo "--- status ---"
  "$PY" "$AC" 영숙 status 2>&1
} > "$STATUS" 2>&1

echo ""
echo "==================================================="
echo " 영숙(텔레그램 비서) 재시작 완료"
cat "$STATUS"
echo "==================================================="
echo " 이 창은 닫으셔도 됩니다."
sleep 2
