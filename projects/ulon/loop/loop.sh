#!/usr/bin/env bash
# 울온 자율 개발 루프.
# 한 바퀴마다 새 codex exec 세션. 대화를 이어 붙이지 않는다.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

# launchd는 터미널 PATH를 안 물려준다.
export PATH="$ROOT/loop/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:/Users/junholee/.unity/bin:/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS:/Applications/Blender.app/Contents/MacOS:/opt/homebrew/opt/postgresql@16/bin:${PATH:-}"
export HOME="${HOME:-/Users/junholee}"
export LANG="${LANG:-ko_KR.UTF-8}"
export LC_ALL="${LC_ALL:-en_US.UTF-8}"
export PYTHONUNBUFFERED=1
export PYTHONUTF8=1

# shellcheck disable=SC1091
source "$ROOT/loop/env.sh"
if [ -f "$ROOT/loop/env.local.sh" ]; then
  # shellcheck disable=SC1091
  source "$ROOT/loop/env.local.sh"
fi

LOCK="$ROOT/loop/.lock"
STOP="$ROOT/loop/STOP"
LOG_DIR="$ROOT/logs"
STATE="$LOG_DIR/loop_state.json"
HISTORY="$LOG_DIR/loop_history.jsonl"
PROMPT_FILE="$ROOT/loop/PROMPT.md"
CODEX_BIN="${CODEX_BIN:-/opt/homebrew/bin/codex}"
PYTHON_BIN="${PYTHON_BIN:-/opt/homebrew/bin/python3}"
BLENDER_BIN="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"

mkdir -p "$LOG_DIR"

if [ ! -x "$CODEX_BIN" ] && ! command -v codex >/dev/null 2>&1; then
  echo "codex CLI가 없습니다: $CODEX_BIN" >&2
  exit 1
fi
command -v codex >/dev/null 2>&1 && CODEX_BIN="$(command -v codex)"

# --- lock: 한 번에 한 루프만 ---
if [ -f "$LOCK" ]; then
  old_pid="$(tr -d ' \t\r\n' < "$LOCK" || true)"
  if [ -n "${old_pid:-}" ] && kill -0 "$old_pid" 2>/dev/null; then
    echo "이미 실행 중 (pid $old_pid). 종료합니다."
    exit 0
  fi
  echo "오래된 lock (pid ${old_pid:-?}) 을 지웁니다."
  rm -f "$LOCK"
fi
echo $$ > "$LOCK"
cleanup() {
  rm -f "$LOCK"
}
trap cleanup EXIT

now_iso() { date +"%Y-%m-%dT%H:%M:%S%z"; }
today() { date +"%Y-%m-%d"; }

write_state() {
  "$PYTHON_BIN" - "$STATE" <<'PY'
import json, os, sys
from pathlib import Path
path = Path(sys.argv[1])
prev = {}
if path.exists():
    try:
        prev = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        prev = {}
keys = [
    "loop", "started_at", "ended_at", "result", "consec_fail", "pid",
    "status", "current_task", "model", "wait_remaining_sec", "log_path",
    "fail_reason", "today",
]
update = {}
for k in keys:
    envk = "ST_" + k.upper()
    if envk in os.environ:
        update[k] = os.environ[envk]
# typed fields
for k in ("loop", "consec_fail", "pid", "wait_remaining_sec"):
    if k in update and update[k] != "":
        try:
            update[k] = int(update[k])
        except ValueError:
            pass
if "today" in update and isinstance(update["today"], str):
    try:
        update["today"] = json.loads(update["today"])
    except Exception:
        pass
prev.update(update)
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(prev, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
PY
}

append_history() {
  "$PYTHON_BIN" - "$HISTORY" <<'PY'
import json, os, sys
from pathlib import Path
path = Path(sys.argv[1])
row = {
    "loop": int(os.environ.get("H_LOOP", "0")),
    "started_at": os.environ.get("H_STARTED", ""),
    "ended_at": os.environ.get("H_ENDED", ""),
    "result": os.environ.get("H_RESULT", ""),
    "elapsed_sec": int(float(os.environ.get("H_ELAPSED", "0"))),
    "model": os.environ.get("H_MODEL", ""),
    "log_path": os.environ.get("H_LOG", ""),
}
path.parent.mkdir(parents=True, exist_ok=True)
with path.open("a", encoding="utf-8") as f:
    f.write(json.dumps(row, ensure_ascii=False) + "\n")
PY
}

read_prev() {
  "$PYTHON_BIN" - "$STATE" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
if not p.exists():
    print("-1")
    print("0")
    raise SystemExit
try:
    d = json.loads(p.read_text(encoding="utf-8"))
except Exception:
    print("-1"); print("0"); raise SystemExit
print(int(d.get("loop") if d.get("loop") is not None else -1))
print(int(d.get("consec_fail") or 0))
PY
}

pick_model() {
  ROOT="$ROOT" MODEL_LOW="$MODEL_LOW" MODEL_MID="$MODEL_MID" MODEL_HIGH="$MODEL_HIGH" \
  "$PYTHON_BIN" - <<'PY'
import json, os
from pathlib import Path
root = Path(os.environ["ROOT"])
low, mid, high = os.environ["MODEL_LOW"], os.environ["MODEL_MID"], os.environ["MODEL_HIGH"]
status = root / "docs" / "STATUS.md"
text = status.read_text(encoding="utf-8") if status.exists() else ""
if (not status.exists()) or ("시스템 상태" not in text):
    print(high)
    raise SystemExit
board = root / "docs" / "board.json"
if board.exists():
    try:
        data = json.loads(board.read_text(encoding="utf-8"))
    except Exception:
        data = {}
    cards = data.get("cards") or []
    for want in ("진행 중", "대기"):
        for c in cards:
            if c.get("status") != want:
                continue
            m = (c.get("model") or "").strip()
            if m:
                print(m)
                raise SystemExit
            d = c.get("difficulty") or "중"
            print({"하": low, "중": mid, "상": high}.get(d, mid))
            raise SystemExit
print(mid)
PY
}

count_today() {
  ROOT="$ROOT" HISTORY="$HISTORY" "$PYTHON_BIN" - <<'PY'
import json, os, subprocess
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo
today = datetime.now(ZoneInfo("Asia/Seoul")).strftime("%Y-%m-%d")
hist = Path(os.environ["HISTORY"])
loops = success = fail = 0
if hist.exists():
    for line in hist.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            row = json.loads(line)
        except Exception:
            continue
        ts = str(row.get("started_at") or "")
        if not ts.startswith(today):
            continue
        loops += 1
        r = row.get("result")
        if r == "success":
            success += 1
        elif r in ("fail", "timeout"):
            fail += 1
root = Path(os.environ["ROOT"])
commits = 0
try:
    out = subprocess.check_output(
        ["git", "log", "--since", today + " 00:00:00", "--pretty=%H", "--", "."],
        cwd=str(root), text=True, stderr=subprocess.DEVNULL,
    )
    commits = len([x for x in out.splitlines() if x.strip()])
except Exception:
    commits = 0
print(json.dumps({"loops": loops, "success": success, "fail": fail, "commits": commits}, ensure_ascii=False))
PY
}

prune_logs() {
  local days="${LOG_KEEP_DAYS:-14}"
  find "$LOG_DIR" -type f -name '*.log' -mtime +"$days" -delete 2>/dev/null || true
}

# 시작 상태 (bash 3.2 호환 — mapfile 없음)
PREV_OUT="$(read_prev)"
LOOP_NO="$(printf '%s\n' "$PREV_OUT" | sed -n '1p')"
CONSEC="$(printf '%s\n' "$PREV_OUT" | sed -n '2p')"
LOOP_NO="${LOOP_NO:-0}"
CONSEC="${CONSEC:-0}"

# STOP이 남아 있으면 새 바퀴를 시작하지 않는다 (사람이 해제할 때까지).
if [ -f "$STOP" ]; then
  ST_LOOP="$LOOP_NO" ST_STATUS="stopped_stop" ST_RESULT="stopped" \
  ST_PID="" ST_CURRENT_TASK="STOP 파일 있음. 정지 해제 후 재시작." \
  ST_WAIT_REMAINING_SEC=0 ST_TODAY="$(count_today)" \
  write_state
  echo "loop/STOP 이 있습니다. 현재 바퀴를 시작하지 않고 종료합니다."
  exit 0
fi

# 사전 확인
if [ ! -f "$PROMPT_FILE" ]; then
  echo "loop/PROMPT.md 가 없습니다." >&2
  exit 1
fi
if [ ! -x "$BLENDER_BIN" ]; then
  echo "blender 실행 파일이 없습니다: $BLENDER_BIN" >&2
  exit 1
fi

MAX_LOOPS="${MAX_LOOPS:-0}"
SLEEP_BETWEEN="${SLEEP_BETWEEN:-45}"
LOOP_TIMEOUT_MIN="${LOOP_TIMEOUT_MIN:-90}"
MAX_CONSEC_FAIL="${MAX_CONSEC_FAIL:-3}"
TIMEOUT_SEC=$(( LOOP_TIMEOUT_MIN * 60 ))

while true; do
  if [ -f "$STOP" ]; then
    ST_LOOP="$LOOP_NO" ST_STATUS="stopped_stop" ST_RESULT="stopped" \
    ST_PID="" ST_CURRENT_TASK="STOP — 현재 바퀴를 마친 뒤 정지" \
    ST_WAIT_REMAINING_SEC=0 ST_ENDED_AT="$(now_iso)" ST_TODAY="$(count_today)" \
    write_state
    echo "STOP 감지. 루프를 멈춥니다."
    exit 0
  fi

  if [ "$MAX_LOOPS" -gt 0 ] && [ $((LOOP_NO + 1)) -ge "$MAX_LOOPS" ]; then
    ST_LOOP="$LOOP_NO" ST_STATUS="stopped_stop" ST_RESULT="max_loops" \
    ST_CURRENT_TASK="MAX_LOOPS=$MAX_LOOPS 도달" ST_PID="" \
    ST_WAIT_REMAINING_SEC=0 ST_ENDED_AT="$(now_iso)" ST_TODAY="$(count_today)" \
    write_state
    echo "MAX_LOOPS=$MAX_LOOPS 에 도달했습니다."
    exit 0
  fi

  LOOP_NO=$((LOOP_NO + 1))
  STARTED="$(now_iso)"
  DAY="$(today)"
  DAY_LOG="$LOG_DIR/${DAY}.log"
  WHEEL_LOG="$LOG_DIR/loop_$(printf '%04d' "$LOOP_NO").log"
  MODEL="$(pick_model)"
  START_EPOCH="$(date +%s)"

  {
    echo "======== loop #$LOOP_NO start $STARTED model=$MODEL pid=$$ ========"
  } | tee -a "$DAY_LOG" "$WHEEL_LOG"

  ST_LOOP="$LOOP_NO" ST_STARTED_AT="$STARTED" ST_ENDED_AT="" \
  ST_RESULT="running" ST_CONSEC_FAIL="$CONSEC" ST_PID="$$" \
  ST_STATUS="running" ST_CURRENT_TASK="codex exec ($MODEL) — PROMPT.md" \
  ST_MODEL="$MODEL" ST_WAIT_REMAINING_SEC=0 ST_LOG_PATH="$WHEEL_LOG" \
  ST_FAIL_REASON="" ST_TODAY="$(count_today)" \
  write_state

  set +e
  "$PYTHON_BIN" "$ROOT/loop/run_timeout.py" "$TIMEOUT_SEC" \
    "$CODEX_BIN" exec \
      --model "$MODEL" \
      --cd "$ROOT" \
      --ephemeral \
      --dangerously-bypass-approvals-and-sandbox \
      --skip-git-repo-check \
      -c "model_reasoning_effort=\"medium\"" \
      "loop/PROMPT.md를 읽고 일하라." \
    >>"$WHEEL_LOG" 2>&1
  RC=$?
  set -e

  cat "$WHEEL_LOG" >> "$DAY_LOG"
  ENDED="$(now_iso)"
  END_EPOCH="$(date +%s)"
  ELAPSED=$((END_EPOCH - START_EPOCH))

  RESULT="success"
  STATUS_NOW="waiting"
  FAIL_REASON=""
  if [ "$RC" -eq 124 ]; then
    RESULT="timeout"
    FAIL_REASON="바퀴 타임아웃 ${LOOP_TIMEOUT_MIN}분 초과. 세션 강제 종료."
    CONSEC=$((CONSEC + 1))
  elif [ "$RC" -ne 0 ]; then
    RESULT="fail"
    FAIL_REASON="codex exec 종료 코드 $RC"
    CONSEC=$((CONSEC + 1))
  else
    CONSEC=0
  fi

  {
    echo "======== loop #$LOOP_NO end $ENDED result=$RESULT rc=$RC elapsed=${ELAPSED}s consec=$CONSEC ========"
    if [ -n "$FAIL_REASON" ]; then
      echo "사유: $FAIL_REASON"
    fi
  } | tee -a "$DAY_LOG" "$WHEEL_LOG"

  H_LOOP="$LOOP_NO" H_STARTED="$STARTED" H_ENDED="$ENDED" \
  H_RESULT="$RESULT" H_ELAPSED="$ELAPSED" H_MODEL="$MODEL" H_LOG="$WHEEL_LOG" \
  append_history

  if [ "$CONSEC" -ge "$MAX_CONSEC_FAIL" ]; then
    STATUS_NOW="stopped_fail"
    FAIL_REASON="${FAIL_REASON} / 연속 실패 ${CONSEC}회 (한도 ${MAX_CONSEC_FAIL}). 루프 정지."
    echo "$FAIL_REASON" | tee -a "$DAY_LOG" "$LOG_DIR/stopped_fail.txt"
    ST_LOOP="$LOOP_NO" ST_ENDED_AT="$ENDED" ST_RESULT="$RESULT" \
    ST_CONSEC_FAIL="$CONSEC" ST_PID="" ST_STATUS="$STATUS_NOW" \
    ST_CURRENT_TASK="$FAIL_REASON" ST_MODEL="$MODEL" \
    ST_WAIT_REMAINING_SEC=0 ST_LOG_PATH="$WHEEL_LOG" \
    ST_FAIL_REASON="$FAIL_REASON" ST_TODAY="$(count_today)" \
    write_state
    exit 1
  fi

  prune_logs

  if [ -f "$STOP" ]; then
    ST_LOOP="$LOOP_NO" ST_ENDED_AT="$ENDED" ST_RESULT="$RESULT" \
    ST_CONSEC_FAIL="$CONSEC" ST_PID="" ST_STATUS="stopped_stop" \
    ST_CURRENT_TASK="STOP — 현재 바퀴를 마친 뒤 정지" ST_MODEL="$MODEL" \
    ST_WAIT_REMAINING_SEC=0 ST_LOG_PATH="$WHEEL_LOG" \
    ST_FAIL_REASON="$FAIL_REASON" ST_TODAY="$(count_today)" \
    write_state
    echo "STOP 감지. 현재 바퀴를 마치고 멈춥니다."
    exit 0
  fi

  if [ "$MAX_LOOPS" -gt 0 ] && [ $((LOOP_NO + 1)) -ge "$MAX_LOOPS" ]; then
    ST_LOOP="$LOOP_NO" ST_ENDED_AT="$ENDED" ST_RESULT="$RESULT" \
    ST_CONSEC_FAIL="$CONSEC" ST_PID="" ST_STATUS="stopped_stop" \
    ST_CURRENT_TASK="MAX_LOOPS=$MAX_LOOPS 도달" ST_MODEL="$MODEL" \
    ST_WAIT_REMAINING_SEC=0 ST_LOG_PATH="$WHEEL_LOG" \
    ST_FAIL_REASON="$FAIL_REASON" ST_TODAY="$(count_today)" \
    write_state
    echo "MAX_LOOPS=$MAX_LOOPS 에 도달했습니다."
    exit 0
  fi

  ST_LOOP="$LOOP_NO" ST_ENDED_AT="$ENDED" ST_RESULT="$RESULT" \
  ST_CONSEC_FAIL="$CONSEC" ST_PID="$$" ST_STATUS="waiting" \
  ST_CURRENT_TASK="다음 바퀴까지 ${SLEEP_BETWEEN}초 대기" ST_MODEL="$MODEL" \
  ST_WAIT_REMAINING_SEC="$SLEEP_BETWEEN" ST_LOG_PATH="$WHEEL_LOG" \
  ST_FAIL_REASON="$FAIL_REASON" ST_TODAY="$(count_today)" \
  write_state

  remain="$SLEEP_BETWEEN"
  while [ "$remain" -gt 0 ]; do
    if [ -f "$STOP" ]; then
      ST_STATUS="stopped_stop" ST_CURRENT_TASK="STOP — 대기 중 정지" \
      ST_WAIT_REMAINING_SEC=0 ST_PID="" ST_ENDED_AT="$(now_iso)" \
      ST_TODAY="$(count_today)" write_state
      echo "STOP 감지. 대기 중 멈춥니다."
      exit 0
    fi
    ST_WAIT_REMAINING_SEC="$remain" write_state
    sleep 1
    remain=$((remain - 1))
  done
done
