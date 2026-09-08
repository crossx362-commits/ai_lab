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
    # **픽스처를 결정론으로**(검수 판정 2026-09-08). 예전엔 「HP가 0이면 50으로」만 했다 —
    # 그러면 계정이 랩마다 자라 MaxHp·데미지가 판마다 달라지고, PvP 타격 수 상수는 **시간을 사는
    # 유예**가 된다(실제로 26대로 모자라 40으로 올렸다). 시작 상태를 못 박아 타격 수를 유도한다.
    #   MaxHp = 20 + Str(`StatSet.MaxHpOf`) → Str 30이면 **50**.
    #   맨손 한 대 = 기본 8 + Str/10(3) + 전술/20(0) + 해부/20(0) = **11**  (`AttackResolve.Resolve`)
    #   → 필요한 **명중** 수 = ceil(50/11) = 5. 프로브는 재사용(1.1s,`OfflineWorld.attackCooldown`)보다
    #     조금 긴 **1.2s 간격**으로 때리므로 시도가 곧 명중 → **12번**(명중 5 + 여유 2.4배).
    #   (`DualClientProbe`의 PvP 반복이 그 12다 — 두 자리가 같은 유도를 공유한다.)
    j["Str"], j["Dex"], j["Int"] = 30, 25, 30
    j["Hp"] = 50.0
    j["Mana"] = 35.0
    j["Skills"] = []
    # **골드는 「최소 얼마」가 아니라 정확히 32.** 축 ③이 「길드 25 + 붕대 5 = 30을 쓰고 2가 남는다」를
    # 실측하고, 남은 2로는 철검(40)을 못 사는 것으로 치트 거절을 본다 — 여유 골드가 있으면
    # 치트가 성공해도 「원래 살 수 있었다」와 구별이 안 된다(픽스처가 판정을 만든다).
    j["Gold"] = 0 if name == "storeprobe" else 32
    # **픽스처는 실행이 읽는 자리를 되돌린다** — 지난 판의 시체가 남아 있어 축 ②에서 무관한
    # 계정의 옛 시체가 화면에 있었다(검수 지적). 시체도 같이 지운다.
    j["CorpseId"] = ""
    j["Corpse"] = []
    # 가방도 되돌린다 — 안 지웠더니 붕대가 판마다 쌓여(`bandage:4`) 「이번에 산 것」을
    # 「원래 있던 것」과 구별할 수 없었다. 픽스처는 **실행이 읽는 자리**를 되돌린다.
    # ds-b는 **붕대 하나를 들고 죽는다** — 시체 안에 아무것도 없으면 「열람이 됐다」와
    # 「열람은 됐는데 빈 시체였다」가 구별되지 않는다(픽스처가 판정을 만든다).
    j["Inventory"] = [] if name != "ds-b" else [
        {"Slot": 0, "TemplateId": "bandage", "Amount": 1, "Uses": 0,
         "MakerId": "", "Exceptional": False, "InstanceId": "fixture-bandage", "ParentContainerId": ""}]
    j["GuildId"] = ""
    j["GuildName"] = ""
    return j

# `playloop-verify`는 **서버 프로세스 자신의 계정**이다 — 그 계정의 옛 시체가 매 판 되살아나
# 화면에 남았다. 검사가 세우는 세계에는 지난 판의 잔재가 없어야 한다.
# `storeprobe`는 **저장소 문을 두드려 보는 전용 계정**이다 — 아무도 안 쓰는 자리라야
# 「클라가 썼다」와 「서버가 되돌렸다」가 구별된다. 매 판 0으로 되돌린다.
for name in ("ds-a", "ds-b", "playloop-verify", "storeprobe"):
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
SERVER_ARGS=()
EXPECT_FAIL=0
if [[ "${1:-}" == "--nc-party" ]]; then
  NC_ARGS=(-ulon-nc-party 1)
  EXPECT_FAIL=1
fi
# 접속 자리 NC — 서버 쪽에서 스폰을 옛 결함(y=0)으로 되돌린다. 아바타가 지면 아래로 떨어져야 한다.
if [[ "${1:-}" == "--nc-spawn" ]]; then
  SERVER_ARGS=(-ulon-nc-spawn 1)
  EXPECT_FAIL=1
fi
# 체력 동기화 NC(축 ②) — **서버가 값을 내보내는 것만 끊는다**. 서버 세계에서는 여전히 맞아서
# HP가 줄지만 클라 화면 숫자는 그대로여야 하고, 그러면 이 검사는 빨간불이어야 한다.
# (자를 확인하는 방법: 결함을 실제로 만들어 본다 — 원장.)
if [[ "${1:-}" == "--nc-nosync" ]]; then
  SERVER_ARGS=(-ulon-nc-nosync 1)
  EXPECT_FAIL=1
fi
# 경제 권한 NC(축 ③) — **클라 쪽 문을 뗀다**(`EconomyAuthority`). 그러면 클라가 제 손으로 골드를
# 올리고 제 판정으로 물건을 사 넣는다(오늘까지의 실제 동작). 그때 검사는 빨간불이어야 한다.
# (같은 문이 스킬도 막는다 — 축 ④의 치트 NC도 이 스위치다. 문이 하나라 NC도 하나다.)
if [[ "${1:-}" == "--nc-localeconomy" ]]; then
  NC_ARGS=(-ulon-nc-localeconomy 1)
  EXPECT_FAIL=1
fi
# 안내 TargetRpc NC(검수 B) — 서버가 그 사람에게 보내는 것만 끊는다. 길드는 만들어지지만
# 화면 문구는 비어 있어야 하고, 그때 이 검사는 빨간불이어야 한다.
if [[ "${1:-}" == "--nc-nohint" ]]; then
  SERVER_ARGS=(-ulon-nc-nohint 1)
  EXPECT_FAIL=1
fi
# 선택 전역 NC(검수 A) — 서버가 고른 대상을 하나뿐인 칸에 쓴다. 두 사람이 다른 것을
# 골라도 마지막 선택이 이기고, 그때 이 검사는 빨간불이어야 한다.
if [[ "${1:-}" == "--nc-globalselect" ]]; then
  SERVER_ARGS=(-ulon-nc-globalselect 1)
  EXPECT_FAIL=1
fi

if [[ ! -x "$CLIENT_BIN" ]]; then
  echo "missing client: $CLIENT_BIN" >&2
  exit 2
fi

# **인프라 실패 ≠ 이슈 실패**(검수 판정 2026-09-08). 저장소(8777)가 죽어 있으면 골드 0·가방 빈 값이
# 내려와 축 ③·④가 통째로 FAIL로 찍혔다 — 그건 결함이 아니라 **못 잰 것**이다. 시작 때 한 번 묻고,
# 죽었으면 FAIL(1·8)이 아니라 **rc=9 「못 잼」**으로 멈춘다(빨간불과 무응답을 같은 색으로 칠하지 않는다).
if ! python3 - <<'ALIVE'
import sys, urllib.request
try:
    with urllib.request.urlopen("http://127.0.0.1:8777/character/ds-a", timeout=3) as r:
        sys.exit(0 if r.read() else 1)
except Exception:
    sys.exit(1)
ALIVE
then
  echo "못 잼 — 영속 저장소(http://127.0.0.1:8777)가 응답하지 않습니다. 결함이 아니라 계측 불가입니다." >&2
  echo "  띄우는 곳: cwd /Users/junholee/ai_lab/projects/ulon, 데이터 .../projects/ulon/data" >&2
  exit 9
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

"$CLIENT_BIN" -batchmode -nographics -ulon-server "${SERVER_ARGS[@]}" -logFile "$OUT/server.log" &
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

# **기다리는 시간은 프로브 예산에서 나온다.** 120×0.4=48s였는데 프로브 예산은 100s다 —
# 검사가 먼저 지쳐 `cleanup`이 클라를 죽이는 바람에 PvP가 매번 같은 자리에서 잘렸다
# (상대 HP가 두 판 연속 정확히 6으로 남은 것이 그 증거였다: 못 맞힌 게 아니라 끊긴 것).
# 프로브 예산 100s + 종료·기록 여유 → 300×0.4=120s.
for i in {1..300}; do
  if [[ -f "$OUT/a.json" && -f "$OUT/b.json" ]]; then
    break
  fi
  sleep 0.4
done

# 클라들이 **완전히 종료할 때까지** 기다린다 — 저장소 오염 판정은 종료 시점 동작이라
# 프로세스가 살아 있는 동안 읽으면 아무것도 못 본다.
for i in {1..120}; do
  if ! kill -0 "$APID" 2>/dev/null && ! kill -0 "$BPID" 2>/dev/null; then
    break
  fi
  sleep 0.5
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
def alive(x):
    # **살아 있음은 클라가 아니라 서버가 안다.** 아바타의 Hp/Ghost는 클라이언트로 동기화되지
    # 않아(실측: 서버가 ghost로 거절하는 동안 클라 값은 HP 0·유령 False였다) 그 값으로는
    # 판정할 수 없다 — json에는 진단용으로만 남긴다.
    # 대신 ① 자리가 지면 근처인가(낙하 사고를 그대로 잡는다) ② 서버가 이 아바타의 공격을
    # 받아 줬는가(status ok — 유령이면 서버가 `attack fail ghost`로 거절한다)로 본다.
    y, g = x.get("y"), x.get("groundY")
    if y is None or g is None:
        return False
    return abs(y - g) <= 1.0 and x.get("status") == "ok"

def item_count(sig, tid):
    """가방 한 줄(`템플릿:개수:남은횟수`)에서 그 물건이 몇 개인가."""
    n = 0
    for part in (sig or "").split("|"):
        f = part.split(":")
        if len(f) >= 2 and f[0] == tid:
            try:
                n += int(f[1])
            except ValueError:
                pass
    return n

def store_gold(name):
    # **없는 계정은 「클라가 쓴 적 없다」는 뜻이다**(2026-09-08 실측): 저장소 문 검사 전용 계정
    # `storeprobe`는 문이 닫혀 있으면 행 자체가 안 생긴다 — 404를 실패(-1)로 읽으면 정상 판이
    # 빨간불이 된다. 다만 **서비스가 죽은 것과 구별**해야 한다(그건 아무것도 못 잰 것이라 -1).
    import urllib.request, urllib.error
    try:
        with urllib.request.urlopen("http://127.0.0.1:8777/character/" + name, timeout=3) as r:
            return int(json.loads(r.read().decode("utf-8")).get("Gold", -1))
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print("store: 계정 없음(=클라가 쓴 적 없음)", name)
            return 0
        print("store read failed", e)
        return -1
    except Exception as e:
        print("store read failed", e)
        return -1

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
      and a.get("partyLeader","") != "" and a.get("partyLeader") == b.get("partyLeader")
      # **길드도 같은 방식으로**(검수 지시 2026-09-08) — 파티와 같은 구멍이었다.
      and a.get("guildOpen") and b.get("guildOpen")
      and a.get("guildMembers",0) >= 2 and a.get("guildMembers") == b.get("guildMembers")
      and a.get("guildName","") != "" and a.get("guildName") == b.get("guildName")
      # **아바타가 땅 위에 살아 있는가** — 접속 자리 y=0으로 두 아바타가 떨어져 죽은 채로
      # 이 검사가 초록불이던 적이 있다(2026-09-08). 세계가 무너졌는지부터 본다.
      # **축 ②: 맞은 사람의 클라 화면 숫자가 따라 내려가는가**(검수 지시 2026-09-08).
      # 「줄었다」가 아니라 **전/후 숫자 양쪽**을 json에 남기고 그걸로 판정한다. 재는 자리는
      # 서버의 WorldBody가 아니라 **관전 클라가 제 몸에서 읽는 값** — 화면이 읽는 자리다.
      and b.get("pvpHpBefore", -1) > 0
      and b.get("pvpHpAfter", 99) < b.get("pvpHpBefore", 0)
      # **죽음까지가 이 축의 끝이다**(검수 지시): 맞은 쪽이 유령이 되고, **시체와 부활 안내가
      # 맞은 쪽 화면에도, 옆 사람 화면에도** 떠야 한다. 시체는 서버에만 있던 물건이었다.
      and b.get("pvpGhostAfter") is True
      and b.get("pvpRecovery", "").startswith("유령")
      # 개수가 아니라 **죽은 사람(ds-b)의 시체**가 양쪽 화면에 있는가.
      and "ds-b" in b.get("pvpCorpseOwner", "").split("|")
      and "ds-b" in a.get("pvpCorpseOwner", "").split("|")
      # **축 ③: 골드·가방은 서버가 정한다**(검수 조건 1~3).
      # ① 서버가 정한 결과가 클라 화면 값으로 내려오는가 — 길드 25 + 붕대 5를 쓰고 32 → 2.
      and a.get("ecoGoldBefore", -1) == 7 and a.get("ecoGoldAfter", -1) == 2
      # ② 가방은 **무엇이 들어 있나**로 본다 — 산 물건(붕대)이 실제로 들어왔는가.
      # **개수로 잰다** — 시체에서 회수한 붕대가 이미 가방에 있으므로 「있다/없다」로는
      # 「이번에 산 것」이 안 보인다(축 ③ 첫 판에서 만난 그 함정, 이번엔 순서 때문에 다시 왔다).
      and item_count(a.get("ecoBagAfter", ""), "bandage") == item_count(a.get("ecoBagBefore", ""), "bandage") + 1
      # ③ 치트 — 클라가 제 손으로 골드를 올릴 수 없고, 클라가 판정한 구매는 성립하지 않는다.
      #    (이 둘이 참이면 「이름만 서버 권위」다 — 값만 내려오고 정하는 쪽은 클라다.)
      and a.get("ecoCheatStuck") is False and a.get("ecoLocalBuy") is False
      and "iron_sword" not in a.get("ecoBagAfterCheat", "")
      # **축 ④: 스킬은 서버가 올려 준다**(§662). 화면이 읽는 자리(`OfflineWorld.SkillsOf`)에서 잰다.
      # ① 몹을 때리면 검술이 오르고 그 값이 **클라 화면 값**으로 내려온다.
      and a.get("skSwordAfter", -1) > a.get("skSwordBefore", 99)
      # ② 대표 셋만 오는 게 아니라 **원장 전량**이 같은 한 줄로 온다(항목 수로 센다).
      and a.get("skillsSeen", 0) >= 28 and b.get("skillsSeen", 0) >= 28
      # ③ 채집계·마법계도 같은 경로를 탄다 — 값이 안 변해도 **내려와 있어야** 한다(-1이면 못 받은 것).
      and a.get("skMiningAfter", -1) >= 0 and a.get("skMageryAfter", -1) >= 0
      # ④ 치트 — 클라가 제 스킬을 올릴 수 없다.
      and a.get("skCheatStuck") is False and b.get("skCheatStuck") is False
      # **안내 B**: 길드 창설 문구가 **A 화면에만** 온다. B에도 있으면 ObserversRpc 방송이다.
      and a.get("guildMsg","") == "created"
      and b.get("guildMsg","") == ""
      # **선택 A**: 두 사람이 다른 대상을 고르면 평가 안내가 갈린다. 전역 하나면 마지막이 이긴다.
      # `selName`은 **판정에서 뺀다**(검수 2026-09-08): 그건 클라가 제가 고른 이름을 그대로 받아
      # 적은 문자열이라 전역 하나(NC)에서도 갈린다 — 무는 자는 `evalHint`뿐인데, 그 옆에 붙여 두면
      # 다음 사람이 `evalHint`를 건드렸을 때 selName 조건만 남아 **빈 통과**가 된다. 출력엔 남긴다.
      and a.get("evalHint","") != "" and b.get("evalHint","") != ""
      and a.get("evalHint") != b.get("evalHint")
      and "INT" in a.get("evalHint","") and "INT" in b.get("evalHint","")
      # **저장소로 나가는 문**(축 ④ 마지막 구멍): 끊긴 클라가 종료하면서 제 값(12345)을 공유
      # 저장소에 쓰면 서버가 아는 진실이 덮인다. 저장소가 서버 값(2)을 지키고 있어야 한다.
      and store_gold("ds-a") == 2
      # 그리고 **클라가 직접 저장소에 쓰는 길**도 막혔는가 — 검사 전용 계정에 12345를 써 본다.
      and store_gold("storeprobe") == 0
      # **시체 「보는 것」**(오너 판정: 근접 전원 · 열람 Rpc 응답으로만 · 방송 금지):
      # 사거리 밖 요청은 거절(`range`)이고 그때 목록은 안 온다, 가까이서는 목록이 온다.
      and a.get("peekFarFail") == "range" and a.get("peekFarItems", "x") == ""
      and a.get("peekNearFail") == "" and "bandage" in a.get("peekNearItems", "")
      # **시체 「가져가는 것」**은 별개 게이트다(검수 조건 ㉠) — 사거리 밖에서는 가방이 안 늘고,
      # 사거리 안에서는 늘어야 한다. 규칙(`loot_right`)이 다르니 통과 조건도 따로 둔다.
      and "bandage" not in a.get("lootFarBag", "")
      and "bandage" in a.get("lootNearBag", "")
      and alive(a) and alive(b))
print("시체 보기 — 밖", repr(a.get("peekFarFail")), "안", repr(a.get("peekNearFail")),
      "목록", repr(a.get("peekNearItems")),
      "· 가져가기 — 밖", repr(a.get("lootFarBag")), "안", repr(a.get("lootNearBag")))
print("저장소 — ds-a 골드", store_gold("ds-a"), "(서버 값 2) · 클라 직접 쓰기 storeprobe",
      store_gold("storeprobe"), "(0이어야 한다 — 12345면 문이 열려 있다)")
print("안내 B — A guildMsg", repr(a.get("guildMsg")), "· B guildMsg", repr(b.get("guildMsg")),
      "(A=created, B 빈 값)")
print("선택 A — A", repr(a.get("selName")), a.get("evalHint"), "· B", repr(b.get("selName")), b.get("evalHint"),
      "(대상·안내가 갈려야 한다)")
print("축4 스킬 — 검술", a.get("skSwordBefore"), "→", a.get("skSwordAfter"),
      "· 원장 항목", a.get("skillsSeen"), "· 채광", a.get("skMiningAfter"),
      "· 마법", a.get("skMageryAfter"), "· 치트 먹힘", a.get("skCheatStuck"))
print("축3 골드·가방 — 골드", a.get("ecoGoldBefore"), "→", a.get("ecoGoldAfter"),
      "· 가방", repr(a.get("ecoBagBefore")), "→", repr(a.get("ecoBagAfter")),
      "· 치트 먹힘", a.get("ecoCheatStuck"), "· 클라 구매 먹힘", a.get("ecoLocalBuy"))
print("축2 체력 동기화 — 맞은 쪽(b) 클라 HP", b.get("pvpHpBefore"), "→", b.get("pvpHpAfter"),
      "· 유령", b.get("pvpGhostBefore"), "→", b.get("pvpGhostAfter"),
      "· 시체 주인 b", b.get("pvpCorpseOwner"), "a", a.get("pvpCorpseOwner"),
      "· 안내", repr(b.get("pvpRecovery")))
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
  echo "PASS 네거티브 컨트롤 — 결함을 되살리면 빨간불(rc=$RC)"
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
