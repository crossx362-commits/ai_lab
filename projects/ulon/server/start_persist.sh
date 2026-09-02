#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PORT="${ULON_PERSIST_PORT:-8777}"
PIDFILE="$ROOT/data/persist.pid"
LOG="$ROOT/data/persist.log"
PY="$ROOT/server/.venv/bin/python"
mkdir -p "$ROOT/data"

if curl -sf "http://127.0.0.1:${PORT}/health" >/dev/null; then
  echo "[ulon] persist already http://127.0.0.1:${PORT}"
  exit 0
fi

if [[ ! -x "$PY" ]]; then
  PY="python3"
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
