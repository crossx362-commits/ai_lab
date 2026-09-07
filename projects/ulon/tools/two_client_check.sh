#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CLIENT_BIN="$ROOT/builds/client/UlonClient.app/Contents/MacOS/Ulon"
OUT="$ROOT/builds/check"
mkdir -p "$OUT"
rm -f "$OUT"/a.json "$OUT"/b.json "$OUT"/server.log "$OUT"/a.log "$OUT"/b.log
# **검사 계정을 매 판 살아 있는 상태로 되돌린다.**
# 지난 판의 저장 상태가 이번 판정을 만든다 — 실제로 옛 낙하 사고(스폰 y 0 = 지면 10m 아래)로
# ds-a가 「유령(HP 0)」으로 저장돼 있어서, 스폰을 고친 뒤에도 파티 초대가 계속 ghost로 거절됐다.
# 파일을 지우면 안 된다: 헤드리스에는 캐릭터 생성 화면이 없어 새 계정이 HP 0으로 남는다.
python3 - "$ROOT/builds/client/data/accounts" <<'FIX'
import json, pathlib, sys, urllib.request, urllib.error
d = pathlib.Path(sys.argv[1])
PERSIST = "http://127.0.0.1:8777/character/"

def normalize(j):
    j["Ghost"] = False
    if float(j.get("Hp", 0)) <= 0:
        j["Hp"] = 50.0
    return j

for name in ("ds-a", "ds-b"):
    # **상태 원장은 파일이 아니라 persist 서비스다**(2026-09-08 실측). 파일만 고쳤을 때는
    # 서버가 HTTP로 옛 유령 상태를 읽어 와 파티 초대가 계속 ghost로 거절됐다 — 원장이 둘이면 갈린다.
    try:
        with urllib.request.urlopen(PERSIST + name, timeout=3) as r:
            j = normalize(json.loads(r.read().decode("utf-8")))
        req = urllib.request.Request(PERSIST + name, data=json.dumps(j).encode("utf-8"),
                                     method="PUT", headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=3):
            pass
        print("fixture ready(persist):", name, "Hp", j["Hp"], "Ghost", j["Ghost"])
        continue
    except Exception as e:
        print("persist fixture skipped:", name, e)
    f = d / (name + ".json")
    if not f.exists():
        print("fixture missing:", f)
        continue
    j = normalize(json.loads(f.read_text(encoding="utf-8-sig")))
    f.write_text(json.dumps(j, indent=2, ensure_ascii=False), encoding="utf-8")
    print("fixture ready(file):", name, "Hp", j["Hp"], "Ghost", j["Ghost"])
FIX

# 파티 네거티브 컨트롤 — 초대 RPC만 끊고 돌린다. 그때는 파티가 안 생겨야(=FAIL이어야) 정상이다.
NC_ARGS=()
EXPECT_FAIL=0
if [[ "${1:-}" == "--nc-party" ]]; then
  NC_ARGS=(-ulon-nc-party 1)
  EXPECT_FAIL=1
fi

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

"$CLIENT_BIN" -batchmode -nographics -ulon-client -ulon-check -ulon-role attacker -ulon-account ds-a "${NC_ARGS[@]}" -ulon-out "$OUT/a.json" -logFile "$OUT/a.log" &
APID=$!
sleep 0.6
"$CLIENT_BIN" -batchmode -nographics -ulon-client -ulon-check -ulon-role observer -ulon-account ds-b "${NC_ARGS[@]}" -ulon-out "$OUT/b.json" -logFile "$OUT/b.log" &
BPID=$!

for i in {1..120}; do
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

set +e
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
      and b.get("hpAfter", 99) < b.get("hpBefore", 0)
      # 효과가 **두 클라이언트 모두**에 도착했는가 — 공격자 자신에게도, 옆 사람에게도.
      # 소스 게이트는 「부르도록 적혀 있다」까지만 본다(검수 지시 2026-09-07).
      and a.get("vfx",0) > 0 and a.get("sfx",0) > 0
      and b.get("vfx",0) > 0 and b.get("sfx",0) > 0
      # **파티가 온라인에서 만들어지고 두 클라가 같은 상태를 보는가**(검수 랩 2026-09-08).
      # 「초대 버튼이 그려진다」가 아니라 **양쪽 json이 같아지는 것**으로 판정한다.
      and a.get("partyOpen") and b.get("partyOpen")
      and a.get("partyMembers",0) >= 2 and a.get("partyMembers") == b.get("partyMembers")
      and a.get("partyLeader","") != "" and a.get("partyLeader") == b.get("partyLeader"))
print("PASS" if ok else "FAIL", a, b)
sys.exit(0 if ok else 5)
PY
RC=$?
set -e
if [[ "$EXPECT_FAIL" -eq 1 ]]; then
  if [[ "$RC" -eq 0 ]]; then
    echo "FAIL 파티 네거티브 컨트롤 — 초대 RPC를 끊었는데도 통과했다. 이 검사는 아무것도 막고 있지 않다." >&2
    exit 8
  fi
  echo "PASS 파티 네거티브 컨트롤 — 초대를 끊으면 빨간불(rc=$RC)"
  exit 0
fi
if [[ "$RC" -ne 0 ]]; then
  exit "$RC"
fi

for log in "$OUT/server.log" "$OUT/a.log" "$OUT/b.log"; do
  if grep -q "expected to be initialized but was not" "$log"; then
    echo "FAIL uninitialized FishNet NetworkObject in $log" >&2
    grep "expected to be initialized but was not" "$log" >&2
    exit 7
  fi
done
