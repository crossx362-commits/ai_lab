#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PG_BIN="$(brew --prefix postgresql@16)/bin"
DATA="/opt/homebrew/var/postgresql@16"
LOG="$ROOT/data/pg.log"
export PATH="$PG_BIN:$PATH"
mkdir -p "$ROOT/data"

if pg_ctl -D "$DATA" status >/dev/null 2>&1; then
  echo "[ulon] postgres already running"
else
  pg_ctl -D "$DATA" -l "$LOG" start
fi

for i in {1..40}; do
  if pg_isready -h 127.0.0.1 -p 5432 >/dev/null 2>&1; then
    break
  fi
  sleep 0.15
done

createuser -h 127.0.0.1 -s ulon 2>/dev/null || true
createdb -h 127.0.0.1 -O ulon ulon 2>/dev/null || createdb -h 127.0.0.1 ulon 2>/dev/null || true
psql -h 127.0.0.1 -p 5432 -U ulon -d ulon -v ON_ERROR_STOP=1 -f "$ROOT/server/schema.sql"
echo "[ulon] postgres ready postgresql://ulon@127.0.0.1:5432/ulon"
