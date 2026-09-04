#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PORT="${ULON_PERSIST_PORT:-8777}"
PIDFILE="$ROOT/data/persist.pid"
LOG="$ROOT/data/persist.log"
PY="$ROOT/server/.venv/bin/python"
mkdir -p "$ROOT/data"

if [[ ! -x "$PY" ]]; then
  PY="python3"
fi

SOURCE_SHA256="$(shasum -a 256 "$ROOT/server/persist.py" | awk '{print $1}')"
HEALTH="$(curl -sf "http://127.0.0.1:${PORT}/health" || true)"
if [[ -n "$HEALTH" ]] && "$PY" -c 'import json,sys; raise SystemExit(0 if json.loads(sys.argv[1]).get("source_sha256") == sys.argv[2] else 1)' "$HEALTH" "$SOURCE_SHA256"; then
  echo "[ulon] persist already current http://127.0.0.1:${PORT}"
  exit 0
fi

if [[ -n "$HEALTH" ]]; then
  RUNNING_PID="$(lsof -nP -iTCP:"${PORT}" -sTCP:LISTEN -t | head -1)"
  RUNNING_COMMAND="$(ps -p "$RUNNING_PID" -o command= 2>/dev/null || true)"
  if [[ -z "$RUNNING_PID" || "$RUNNING_COMMAND" != *"$ROOT/server/persist.py"* ]]; then
    echo "[ulon] port ${PORT} is owned by another process" >&2
    exit 1
  fi
  kill -TERM "$RUNNING_PID"
  for i in {1..20}; do
    if ! kill -0 "$RUNNING_PID" 2>/dev/null; then
      break
    fi
    sleep 0.1
  done
  if kill -0 "$RUNNING_PID" 2>/dev/null; then
    echo "[ulon] stale persist failed to stop pid=${RUNNING_PID}" >&2
    exit 1
  fi
fi

export DATABASE_URL="${DATABASE_URL:-postgresql://ulon@127.0.0.1:5432/ulon}"
nohup "$PY" "$ROOT/server/persist.py" >>"$LOG" 2>&1 &
echo $! >"$PIDFILE"

ok=0
for i in {1..40}; do
  if curl -sf "http://127.0.0.1:${PORT}/health" >/dev/null; then
    ok=1
    break
  fi
  sleep 0.15
done
if [[ "$ok" -ne 1 ]]; then
  echo "[ulon] persist failed to start" >&2
  tail -n 40 "$LOG" >&2 || true
  exit 1
fi
echo "[ulon] persist ready http://127.0.0.1:${PORT} pid=$(cat "$PIDFILE")"
