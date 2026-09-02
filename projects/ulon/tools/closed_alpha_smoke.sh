#!/bin/zsh
# Closed Alpha 로컬 준비. 외부 배포/포트포워드는 하지 않는다.
# 오너 Unity를 죽이지 않는다. UDP 7770이 이미 열려 있으면 게임 서버는 이미 있는 것으로 본다.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/data/alpha_status.json"
mkdir -p "$ROOT/data/backups" "$ROOT/data/accounts"
touch "$ROOT/data/frozen.txt"

"$ROOT/server/start_postgres.sh"
"$ROOT/server/start_persist.sh"

READY="$(curl -sf http://127.0.0.1:8777/ready || curl -sf http://127.0.0.1:8777/health || true)"
if [[ -z "$READY" ]]; then
  echo "[ulon] persist /ready 실패" >&2
  exit 2
fi

STAMP="$(date -u +%Y%m%d_%H%M%S)"
BAK="$ROOT/data/backups/${STAMP}"
mkdir -p "$BAK"
if ls "$ROOT/data/accounts"/*.json >/dev/null 2>&1; then
  cp "$ROOT/data/accounts"/*.json "$BAK/"
fi
[[ -f "$ROOT/data/oplog.jsonl" ]] && cp "$ROOT/data/oplog.jsonl" "$BAK/"
[[ -f "$ROOT/data/frozen.txt" ]] && cp "$ROOT/data/frozen.txt" "$BAK/"

LAN="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo "127.0.0.1")"
GAME=0
if lsof -nP -iUDP:7770 >/dev/null 2>&1; then
  GAME=1
fi
CLIENT="$ROOT/builds/client/UlonClient.app/Contents/MacOS/Ulon"
HAS_CLIENT=0
[[ -x "$CLIENT" ]] && HAS_CLIENT=1

python3 - "$OUT" "$READY" "$BAK" "$LAN" "$GAME" "$HAS_CLIENT" <<'PY'
import json, sys
out, ready, bak, lan, game, client = sys.argv[1:7]
try:
    body = json.loads(ready)
except Exception:
    body = {"raw": ready, "ok": True}
status = {
    "ok": True,
    "persist": body,
    "backup": bak,
    "lan": lan,
    "gameUdp7770": game == "1",
    "clientBinary": client == "1",
    "connect": f"{lan}:7770",
    "note": "외부 배포 없음. 호스트는 에디터 호스트 또는 -ulon-server. 클라는 -ulon-client -ulon-host <lan> -ulon-account <id>",
}
open(out, "w", encoding="utf-8").write(json.dumps(status, ensure_ascii=False, indent=2) + "\n")
print(json.dumps(status, ensure_ascii=False, indent=2))
PY

echo
echo "[ulon] Closed Alpha 로컬 준비"
echo "  persist  http://127.0.0.1:8777"
echo "  postgres 127.0.0.1:5432"
echo "  게임     ${LAN}:7770  (지금 열림=${GAME})"
echo "  클라     $CLIENT -ulon-client -ulon-host ${LAN} -ulon-account 이름"
echo "  GM       플레이 중 F1"
echo "  백업     $BAK"
echo "  상태     $OUT"
