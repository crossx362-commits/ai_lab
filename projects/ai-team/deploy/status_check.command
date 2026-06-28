#!/bin/bash
# 실제 맥에서 에이전트/데몬 상태 확인 → 파일로 기록. 더블클릭 실행.
set -u
PY="$(command -v python3 || command -v python)"
ROOT="/Users/junholee/ai_lab"
OUT="$ROOT/output/cache/status_check.txt"
mkdir -p "$(dirname "$OUT")"
cd "$ROOT" || exit 1
{
  echo "ts=$(date '+%Y-%m-%d %H:%M:%S')"
  echo "--- 영숙 프로세스(pgrep) ---"
  pgrep -fl "telegram_receiver.py" || echo "telegram_receiver: 없음(중지)"
  echo "--- agent_controller 상태 ---"
  "$PY" "$ROOT/projects/ai-team/skills/영숙_비서/tools/agent_controller.py" 영숙 status 2>&1
  echo "--- notify.agent_status ---"
  "$PY" -c "import sys; sys.path.insert(0,'$ROOT/projects/ai-team'); from _shared.notify import status_report; print(status_report())" 2>&1
} > "$OUT" 2>&1
echo ""
cat "$OUT"
echo "=== 이 창은 닫으셔도 됩니다 ==="
sleep 2
