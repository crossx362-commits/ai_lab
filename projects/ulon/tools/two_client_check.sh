#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CLIENT_BIN="$ROOT/builds/client/UlonClient.app/Contents/MacOS/Ulon"
OUT="$ROOT/builds/check"
mkdir -p "$OUT"
rm -f "$OUT"/a.json "$OUT"/b.json "$OUT"/server.log "$OUT"/a.log "$OUT"/b.log

if [[ ! -x "$CLIENT_BIN" ]]; then
  echo "missing client: $CLIENT_BIN" >&2
  exit 2
fi

for source_root in "$ROOT/unity/Assets/Game" "$ROOT/unity/Packages" "$ROOT/unity/ProjectSettings"; do
  newer="$(find "$source_root" -type f ! -name '.gitkeep' ! -name '.DS_Store' -newer "$CLIENT_BIN" -print -quit)"
  if [[ -n "$newer" ]]; then
    echo "stale client: source is newer than $CLIENT_BIN" >&2
    echo "newer source: $newer" >&2
    echo "rebuild with: $ROOT/tools/rebuild_client.sh" >&2
    exit 6
  fi
done

"$CLIENT_BIN" -batchmode -nographics -ulon-server -logFile "$OUT/server.log" &
SPID=$!
APID=""
BPID=""
cleanup() {
  kill "$SPID" "$APID" "$BPID" 2>/dev/null || true
}
trap cleanup EXIT

ready=0
for i in {1..120}; do
  if grep -q "Local server is started" "$OUT/server.log" 2>/dev/null; then
    ready=1
    break
  fi
  sleep 0.5
done
if [[ "$ready" -ne 1 ]]; then
  echo "server did not start" >&2
  tail -n 60 "$OUT/server.log" >&2 || true
  exit 3
fi

"$CLIENT_BIN" -batchmode -nographics -ulon-client -ulon-check -ulon-role attacker -ulon-account ds-a -ulon-out "$OUT/a.json" -logFile "$OUT/a.log" &
APID=$!
sleep 0.6
"$CLIENT_BIN" -batchmode -nographics -ulon-client -ulon-check -ulon-role observer -ulon-account ds-b -ulon-out "$OUT/b.json" -logFile "$OUT/b.log" &
BPID=$!

for i in {1..50}; do
  if [[ -f "$OUT/a.json" && -f "$OUT/b.json" ]]; then
    break
  fi
  sleep 0.4
done

echo "=== a.json ==="
cat "$OUT/a.json" 2>/dev/null || echo "(missing)"
echo
echo "=== b.json ==="
cat "$OUT/b.json" 2>/dev/null || echo "(missing)"
echo

python3 - "$OUT" <<'PY'
import json, sys, pathlib
root = pathlib.Path(sys.argv[1])
def load(p):
    return json.loads(pathlib.Path(p).read_text(encoding="utf-8-sig"))
try:
    a = load(root/"a.json")
    b = load(root/"b.json")
except Exception as e:
    print("FAIL read", e)
    sys.exit(4)
ok = (a.get("connected") and b.get("connected")
      and a.get("avatars",0) >= 2 and b.get("avatars",0) >= 2
      and a.get("mob") and b.get("mob")
      and a.get("hpAfter", 99) < a.get("hpBefore", 0)
      and b.get("hpAfter", 99) < b.get("hpBefore", 0))
print("PASS" if ok else "FAIL", a, b)
sys.exit(0 if ok else 5)
PY

for log in "$OUT/server.log" "$OUT/a.log" "$OUT/b.log"; do
  if grep -q "expected to be initialized but was not" "$log"; then
    echo "FAIL uninitialized FishNet NetworkObject in $log" >&2
    grep "expected to be initialized but was not" "$log" >&2
    exit 7
  fi
done
