#!/bin/bash
# 네거티브 컨트롤 스위트 — 게이트가 "빨간불을 낼 줄 아는지"를 매번 확인한다.
#
# 통과(초록불)만 확인하는 검증은 검증이 아니다. 여기서는 **일부러 틀린 입력**을 넣고
# 시스템이 정확한 이유로 막는지를 본다. 모델을 부르지 않으므로 비용 없이 반복 가능하다.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$HERE"

# 이 스위트는 **절대로** 실제 모델을 부르지 않는다. 부르려 하면 코드가 거부한다.
# (한 번 그런 사고가 났다 — 사다리가 무시돼 codex가 226초 돌았다. 규칙이 아니라 자물쇠로 막는다.)
export AUTODEV_NO_CLOUD=1

# STOP 플래그가 켜져 있으면 `autodev run`은 전부 거부된다 — 그건 맞는 동작이다.
# 여기서 우회하면 오너의 스톱을 시험 스크립트가 조용히 무력화하는 꼴이라 하지 않는다.
# 대신 34개 FAIL로 헷갈리게 두지 않고, 이유를 말하고 멈춘다(2026-09-11 실제로 헷갈렸다).
if [ -e "$HERE/state/STOP" ]; then
  echo "STOP 상태다 — 실행 시험을 돌릴 수 없다. 해제는 사람이: ./autodev resume"
  exit 2
fi
PASS=0
FAIL=0
TASKS=()
T0=$(date +%s)
LAST=$T0

# 케이스마다 걸린 시간을 찍는다. 20분짜리 침묵은 "도는 중"과 "멎음"을 구분할 수 없다.
lap() {
  local now; now=$(date +%s)
  printf '        (%ds, 누적 %ds)\n' "$((now-LAST))" "$((now-T0))"
  LAST=$now
}

mkcfg() {  # mkcfg <파일> <셸명령> [timeout]
  python3 - "$1" "$2" "${3:-60}" <<'PY'
import json, pathlib, sys
out, script, timeout = sys.argv[1], sys.argv[2], int(sys.argv[3])
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["agents"]["nc"] = {"type": "script", "bin": "/bin/sh", "args": ["-c", script], "timeout_sec": timeout}
cfg["reviewer"] = None   # 기본 시험판은 리뷰를 끈다(리뷰는 아래 전용 판에서 script 리뷰어로 시험한다)
pathlib.Path(out).write_text(json.dumps(cfg, ensure_ascii=False, indent=2))
PY
}

check() {  # check <이름> <기대verdict> <기대문구> <로그파일>
  local name="$1" want="$2" phrase="$3" log="$4"
  local got
  got=$(grep -E "^  verdict" "$log" | awk '{print $3}')
  if [[ "$got" == "$want" ]] && grep -q "$phrase" "$log"; then
    echo "  PASS  $name — verdict=$got"
    PASS=$((PASS+1))
  else
    echo "  FAIL  $name — verdict=$got (기대 $want), 문구 '$phrase' 확인 실패"
    echo "        로그: $log"
    FAIL=$((FAIL+1))
  fi
  # 보고 줄은 "  task     : 7" 형태다 — 콜론 뒤를 쓴다(필드 번호로 세다 한 번 틀렸다).
  local id
  id=$(grep -E "^  task " "$log" | head -1 | sed 's/.*: *//')
  if [[ -n "$id" ]]; then
    TASKS+=("$id")
  else
    echo "  WARN  $name — task 번호를 못 읽어 정리하지 못했다"
    FAIL=$((FAIL+1))
  fi
  lap
}

run_case() {  # run_case <이름> <셸명령> <기대verdict> <기대문구> [timeout]
  local name="$1" script="$2" want="$3" phrase="$4" tmo="${5:-60}"
  local cfg="state/nc_${name}.json" log="/tmp/nc_${name}.log"
  mkcfg "$cfg" "$script" "$tmo"
  AUTODEV_CONFIG="$HERE/$cfg" ./autodev run "[NC] $name" --agent nc >"$log" 2>&1
  check "$name" "$want" "$phrase" "$log"
}

echo "=== 네거티브 컨트롤 스위트 ==="

# 1) 아무것도 안 고치는 AI — rc=0을 성공으로 보면 안 된다
run_case no_change "exit 0" FAILED "파일을 전혀 고치지 않았다"

# 2) 허용 범위 밖 수정
run_case out_of_scope "echo x > Packages/hacked.json" FAILED "허용 범위 밖"

# 3) 검증 장치 변조 — 게이트를 지워 PASS를 만드는 길
run_case tamper "rm -f Assets/AutoDev/Editor/AutoDevCompileCheck.cs" FAILED "검증 장치 변조"

# 4) 깨진 C# — Unity가 실제로 막아야 한다
run_case broken_cs "printf 'class B { void X(){ int a = ; } }\n' > Assets/Game/Scripts/NcBroken.cs" FAILED "C# 컴파일 오류"

# 4a) 수리 루프 — 시도 1은 깨뜨리고 시도 2에 고치는 결정적 에이전트.
#     "실패 → 분석 → 재시도 → 통과"가 실제로 성사되는지를 모델 없이 증명한다.
rm -f /tmp/nc_repair_count
run_case repair_loop \
  "C=/tmp/nc_repair_count; N=\$(cat \$C 2>/dev/null || echo 0); N=\$((N+1)); echo \$N > \$C; \
   if [ \$N -ge 2 ]; then printf 'namespace SandboxGame { public static class NcRepair { public const int V = 1; } }\n' > Assets/Game/Scripts/NcRepair.cs; \
   else printf 'namespace SandboxGame { public static class NcRepair { public const int V = ; } }\n' > Assets/Game/Scripts/NcRepair.cs; fi" \
  PASS "시도 2/3"

# 4b) 실패하는 테스트 — 컴파일은 되지만 테스트가 막아야 한다
run_case failing_test \
  "printf 'using NUnit.Framework;\nnamespace SandboxGame.Tests { public class NcFail { [Test] public void AlwaysFails(){ Assert.AreEqual(1,2); } } }\n' > Assets/Tests/EditMode/NcFail.cs" \
  FAILED "테스트 1건 실패"

# 4c) 테스트 삭제로 통과 만들기 — 쓰는 것은 허용, 지우는 것은 금지
run_case delete_test "rm -f Assets/Tests/EditMode/SandboxEditModeTests.cs" FAILED "검증 장치 변조"

# 4d) 승격 — 1등급이 못 고치면 2등급으로 올라가고, 올라간 등급이 고친다.
#     사다리가 실제로 동작하는지를 모델 없이 본다(두 칸이 서로 다른 결과를 내도록 심는다).
cat > state/nc_ladder.json <<'JSON'
{}
JSON
python3 - <<'PY'
import json, pathlib
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["ladder"] = ["nc_low", "nc_high"]
cfg["reviewer"] = None
cfg["research_agent"] = None
cfg["agents"]["nc_low"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c",
    "printf 'namespace SandboxGame { public static class NcLadder { public const int V = ; } }\\n' > Assets/Game/Scripts/NcLadder.cs"]}
cfg["agents"]["nc_high"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c",
    "printf 'namespace SandboxGame { public static class NcLadder { public const int V = 2; } }\\n' > Assets/Game/Scripts/NcLadder.cs"]}
pathlib.Path("state/nc_ladder.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=2))
PY
AUTODEV_CONFIG="$HERE/state/nc_ladder.json" ./autodev run "[NC] ladder" >/tmp/nc_ladder.log 2>&1
check ladder PASS "담당: nc_high" /tmp/nc_ladder.log

# 4e) 최종 리뷰 — 게이트를 다 통과해도 리뷰가 반려하면 DONE이 아니다.
python3 - <<'PY'
import json, pathlib
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["ladder"] = ["nc_impl"]
cfg["research_agent"] = None
cfg["reviewer"] = "nc_reviewer"
cfg["agents"]["nc_impl"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c",
    "printf 'namespace SandboxGame { public static class NcReview { public const int V = 3; } }\\n' > Assets/Game/Scripts/NcReview.cs"]}
cfg["agents"]["nc_reviewer"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c",
    "printf '{\"verdict\":\"reject\",\"reasons\":[\"목표를 상수 하나로 때웠다\"],\"severity\":\"high\"}' > review.json"]}
pathlib.Path("state/nc_review.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=2))
# 리뷰가 파일을 안 만든 경우 = 승인이 아니라 판정 불가
cfg2 = json.loads(json.dumps(cfg))
cfg2["agents"]["nc_reviewer"]["args"] = ["-c", "exit 0"]
pathlib.Path("state/nc_review_silent.json").write_text(json.dumps(cfg2, ensure_ascii=False, indent=2))
PY
AUTODEV_CONFIG="$HERE/state/nc_review.json" ./autodev run "[NC] review_reject" >/tmp/nc_review_reject.log 2>&1
check review_reject REJECTED "리뷰 반려" /tmp/nc_review_reject.log
AUTODEV_CONFIG="$HERE/state/nc_review_silent.json" ./autodev run "[NC] review_silent" >/tmp/nc_review_silent.log 2>&1
check review_silent PASS "리뷰가 돌았는지 알 수 없다" /tmp/nc_review_silent.log
# 게이트는 통과했지만 리뷰를 확인 못 했으므로 DONE이면 안 된다
if grep -qE "^  status   : REVIEW" /tmp/nc_review_silent.log; then
  echo "  PASS  review_silent_status — DONE으로 올리지 않음"; PASS=$((PASS+1))
else echo "  FAIL  review_silent_status — 리뷰 미확인인데 DONE"; FAIL=$((FAIL+1)); fi

# 4f) 분해 — 계획 파일이 없거나 순환 의존이면 실패로 세운다
python3 - <<'PY'
import json, pathlib
base = json.loads(pathlib.Path("config.json").read_text())
def w(name, script):
    c = json.loads(json.dumps(base))
    c["planner"] = "nc_planner"
    c["reviewer"] = None
    c["research_agent"] = None
    c["ladder"] = ["nc_impl"]
    c["agents"]["nc_impl"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c",
        "N=$(ls Assets/Game/Scripts/NcPlan*.cs 2>/dev/null | wc -l | tr -d ' '); "
        "printf 'namespace SandboxGame { public static class NcPlan%s { public const int V = 1; } }\\n' \"$N\" > Assets/Game/Scripts/NcPlan$N.cs"]}
    c["agents"]["nc_planner"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "args": ["-c", script]}
    pathlib.Path(f"state/nc_{name}.json").write_text(json.dumps(c, ensure_ascii=False, indent=2))
w("plan_none", "exit 0")
w("plan_cycle", """printf '{"tasks":[{"key":"A","goal":"a","depends_on":["B"]},{"key":"B","goal":"b","depends_on":["A"]}]}' > plan.json""")
w("plan_ok", """printf '{"tasks":[{"key":"T1","goal":"첫 번째","done_criteria":"컴파일","depends_on":[],"risk":"low"},{"key":"T2","goal":"두 번째","done_criteria":"컴파일","depends_on":["T1"],"risk":"low"}]}' > plan.json""")
PY
for c in plan_none plan_cycle; do
  AUTODEV_CONFIG="$HERE/state/nc_$c.json" ./autodev plan "[NC] $c" >/tmp/nc_$c.log 2>&1
done
if grep -q "계획 파일이 만들어지지 않았다" /tmp/nc_plan_none.log; then
  echo "  PASS  plan_none — 계획 없음을 잡음"; PASS=$((PASS+1))
else echo "  FAIL  plan_none (로그: /tmp/nc_plan_none.log)"; FAIL=$((FAIL+1)); fi
if grep -q "순환 의존" /tmp/nc_plan_cycle.log; then
  echo "  PASS  plan_cycle — 순환 의존을 잡음"; PASS=$((PASS+1))
else echo "  FAIL  plan_cycle (로그: /tmp/nc_plan_cycle.log)"; FAIL=$((FAIL+1)); fi
AUTODEV_CONFIG="$HERE/state/nc_plan_ok.json" ./autodev plan "[NC] plan_ok" >/tmp/nc_plan_ok.log 2>&1
if grep -q "T2 .*←.*T1\|T2" /tmp/nc_plan_ok.log && grep -q "계획 .* (2개, 의존성 순)" /tmp/nc_plan_ok.log; then
  echo "  PASS  plan_ok — 2개로 분해·의존성 정렬"; PASS=$((PASS+1))
else echo "  FAIL  plan_ok (로그: /tmp/nc_plan_ok.log)"; FAIL=$((FAIL+1)); fi

# 4g) 계획 실행 — 의존성 순서대로 두 Task가 실제로 돌아야 한다
PLAN_ID=$(grep -oE "run-plan --plan [0-9]+" /tmp/nc_plan_ok.log | head -1 | grep -oE "[0-9]+")
if [[ -n "$PLAN_ID" ]]; then
  AUTODEV_CONFIG="$HERE/state/nc_plan_ok.json" ./autodev run-plan --plan "$PLAN_ID" >/tmp/nc_run_plan.log 2>&1
  for id in $(grep -E "^  task " /tmp/nc_run_plan.log | sed 's/.*: *//'); do TASKS+=("$id"); done
  if [[ $(grep -cE "^  status   : DONE" /tmp/nc_run_plan.log) -eq 2 ]]; then
    echo "  PASS  run_plan — 2개 Task 모두 DONE"; PASS=$((PASS+1))
  else echo "  FAIL  run_plan (로그: /tmp/nc_run_plan.log)"; FAIL=$((FAIL+1)); fi
else echo "  FAIL  run_plan — 계획 번호를 못 읽음"; FAIL=$((FAIL+1)); fi

# 5) CLI 부재 — 인프라 실패는 시도 미차감 UNKNOWN
mkcfg state/nc_missing.json "exit 0"
python3 - <<'PY'
import json, pathlib
p = pathlib.Path("state/nc_missing.json"); c = json.loads(p.read_text())
c["agents"]["nc"]["bin"] = "/usr/bin/definitely_not_here"
p.write_text(json.dumps(c, ensure_ascii=False, indent=2))
PY
AUTODEV_CONFIG="$HERE/state/nc_missing.json" ./autodev run "[NC] missing_cli" --agent nc >/tmp/nc_missing.log 2>&1
check missing_cli UNKNOWN "인프라 실패(시도 미차감)" /tmp/nc_missing.log

# 6) 에이전트 타임아웃 — 역시 미차감 UNKNOWN
run_case agent_timeout "sleep 30" UNKNOWN "인프라 실패(시도 미차감)" 3

# 7) 정상판(포지티브 컨트롤) — 빨간불만 잘 내고 초록불을 못 내면 그것도 고장이다
run_case good_cs \
  "printf 'namespace SandboxGame { public static class NcOk { public const int V = 1; } }\n' > Assets/Game/Scripts/NcOk.cs" \
  PASS "컴파일 OK 마커"

# 8) 메모리 규칙 — 판정은 순수 함수라 **진짜 압박을 만들지 않고** 시험할 수 있다.
#    (실제로 스왑을 채워 시험하면 그 시험이 기계를 죽인다. 그래서 입력만 넣는다.)
python3 - <<'PY' > /tmp/nc_memory.log 2>&1
import sys, pathlib
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import memory as M

def snap(free=60.0, used=0.0, total=6144.0, rate=None):
    s = M.Snapshot(ts=0, free_pct=free, swap_used_mb=used, swap_total_mb=total,
                   compressed_mb=0, wired_mb=0)
    s.swap_rate_mb_min = rate
    return s

cases = [
    ("green",        snap(),                         "GREEN"),
    ("free_yellow",  snap(free=20.0),                "YELLOW"),
    ("free_red",     snap(free=5.0),                 "RED"),
    ("swap_yellow",  snap(used=4000.0),              "YELLOW"),
    # 스왑이 꽉 차 있어도 **늘고 있지 않으면** RED가 아니다 — macOS 스왑은 과거 누적이라
    # 회수되지 않고 남는다. 여유 71%·증가 0인데 RED를 찍어 매 작업이 300초씩 기다린 적이 있다.
    ("swap_full_idle", snap(used=6000.0, rate=0.0),  "YELLOW"),
    ("swap_full_growing", snap(used=6000.0, rate=300.0), "RED"),
    ("rate_yellow",  snap(rate=80.0),                "YELLOW"),
    ("rate_red",     snap(rate=300.0),               "RED"),
    # 못 쟀을 때 초록이라고 하면 안 된다 — 모르는 것은 모른다고 한다.
    ("unmeasured",   snap(free=None),                "YELLOW"),
    # 최악이 이긴다: 여유는 넉넉해도 스왑이 터졌으면 RED
    ("worst_wins",   snap(free=5.0, used=6000.0),    "RED"),
]
bad = 0
for name, s, want in cases:
    got = M.assess(s).state
    if got != want:
        print(f"MISMATCH {name}: {got} != {want}"); bad += 1
# 슬롯 축소도 같은 규칙에서 나와야 한다
if M.effective_unity_slots("GREEN", 2) != 2: print("MISMATCH slots GREEN"); bad += 1
if M.effective_unity_slots("YELLOW", 2) != 1: print("MISMATCH slots YELLOW"); bad += 1
if M.effective_unity_slots("RED", 2) != 1: print("MISMATCH slots RED"); bad += 1
print("MEMORY_OK" if bad == 0 else f"MEMORY_BAD {bad}")
PY
if grep -q "^MEMORY_OK" /tmp/nc_memory.log; then
  echo "  PASS  memory_rules — 10판정+슬롯 전부 일치(못 잰 값은 GREEN 아님, 스왑 정체는 RED 아님)"; PASS=$((PASS+1))
else echo "  FAIL  memory_rules (로그: /tmp/nc_memory.log)"; FAIL=$((FAIL+1)); fi

# 9) 남의 Unity — 이름이 Unity라도 **다른 프로젝트**면 고르지 않는다.
#    (2026-09-11: 전역 pkill로 다른 세션의 Unity를 죽일 뻔했다. 선별을 코드로 시험한다.)
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
SANDBOX_UP="$HERE/sandbox/unity"
python3 -c "import time; time.sleep(25)" "$UNITY_BIN" -projectPath /Users/junholee/ai_lab-loop/projects/ulon/unity >/dev/null 2>&1 &
FOREIGN=$!
# 짝이 되는 포지티브: **내 프로젝트** 경로를 가진 것은 반드시 골라야 한다.
# (한쪽만 보면 "아무것도 안 고르는 선별기"도 통과한다.)
python3 -c "import time; time.sleep(25)" "$UNITY_BIN" -projectPath "$SANDBOX_UP" >/dev/null 2>&1 &
MINE=$!
sleep 1
python3 - "$FOREIGN" "$MINE" <<'PY' > /tmp/nc_unity_scope.log 2>&1
import sys, pathlib
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import config, safety
cfg = config.load(pathlib.Path("config.json"))
t = cfg.target("sandbox")
pids = [p for p, _ in safety.unity_procs(t.unity_project)]
foreign, mine = int(sys.argv[1]), int(sys.argv[2])
ok = foreign not in pids and mine in pids
print("SCOPE_OK" if ok else f"SCOPE_BAD foreign={foreign in pids} mine={mine in pids} {pids}")
PY
kill "$FOREIGN" "$MINE" 2>/dev/null
wait "$FOREIGN" "$MINE" 2>/dev/null
if grep -q "^SCOPE_OK" /tmp/nc_unity_scope.log; then
  echo "  PASS  unity_scope — 내 것만 고르고 남의 프로젝트는 제외"; PASS=$((PASS+1))
else echo "  FAIL  unity_scope (로그: /tmp/nc_unity_scope.log)"; FAIL=$((FAIL+1)); fi

# 10) 크래시 회수 — 죽은 판은 세워야 하고, **재시작이 완료를 만들면 안 된다**(§14).
#     진짜로 kill -9 하지 않고도 시험할 수 있게 별도 DB에 상태만 심는다.
MARK_PID=""
python3 -c "import time; time.sleep(25)" autodev_core.cli >/dev/null 2>&1 &   # 주인이 살아 있는 판
MARK_PID=$!
python3 -c "import time; time.sleep(25)" >/dev/null 2>&1 &                     # 같은 PID 자리의 '남'
IMPOSTOR=$!
DEAD_PID=$( (python3 -c "import os; print(os.getpid())") )                     # 이미 끝난 프로세스
sleep 1
python3 - "$MARK_PID" "$IMPOSTOR" "$DEAD_PID" <<'PY' > /tmp/nc_recover.log 2>&1
import pathlib, sys, time
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import db, recover
mark, impostor, dead = (int(x) for x in sys.argv[1:4])
p = pathlib.Path("state/nc_recover.sqlite3")
p.unlink(missing_ok=True)
conn = db.connect(p)

def mk(**kw):
    return db.create_task(conn, kw.pop("goal"), "sandbox", "nc", None, status="RUNNING", **kw)

t_dead = mk(goal="[NC] 주인이 죽은 판", owner_pid=dead, heartbeat=time.time())
t_live = mk(goal="[NC] 주인이 살아있는 판", owner_pid=mark, heartbeat=time.time())
t_reuse = mk(goal="[NC] PID 재사용", owner_pid=impostor, heartbeat=time.time())
# 컴파일까지 통과하고 커밋 직전에 죽은 판 — 여기가 제일 위험한 자리다.
t_pass = mk(goal="[NC] PASS 직후 크래시", owner_pid=dead, verdict="PASS", heartbeat=time.time())
# 주인 PID 기록이 없는 옛 판: 최근이면 건드리지 않는다(도는 중일 수 있다)
t_fresh = mk(goal="[NC] 옛 판이지만 최근", heartbeat=time.time())

got = {r["id"] for r in recover.recover(conn)}
bad = []
if t_dead not in got: bad.append("죽은 주인을 회수 못 함")
if t_live in got: bad.append("살아있는 주인을 고아로 오판")
if t_reuse not in got: bad.append("PID 재사용(남의 프로세스)을 살아있다고 오판")
if t_pass not in got: bad.append("PASS 표시된 크래시 판을 회수 못 함")
if t_fresh in got: bad.append("주인 미기록 최근 판을 성급히 회수")
for tid in (t_dead, t_pass):
    r = db.get_task(conn, tid)
    if r["status"] != "INTERRUPTED": bad.append(f"task {tid} status={r['status']}")
    if r["verdict"] == "PASS": bad.append(f"task {tid} 회수했는데 verdict가 아직 PASS")
if db.get_task(conn, t_live)["status"] != "RUNNING": bad.append("살아있는 판의 상태를 건드림")
# 어떤 경우에도 DONE은 나오지 않는다
if any(db.get_task(conn, i)["status"] == "DONE" for i in (t_dead, t_live, t_reuse, t_pass, t_fresh)):
    bad.append("회수가 DONE을 만들었다")
print("RECOVER_OK" if not bad else "RECOVER_BAD " + " / ".join(bad))
PY
kill "$MARK_PID" "$IMPOSTOR" 2>/dev/null
wait "$MARK_PID" "$IMPOSTOR" 2>/dev/null
rm -f state/nc_recover.sqlite3*
if grep -q "^RECOVER_OK" /tmp/nc_recover.log; then
  echo "  PASS  crash_recover — 죽은 판만 INTERRUPTED(DONE 경로 없음), PID 재사용도 잡음"; PASS=$((PASS+1))
else echo "  FAIL  crash_recover (로그: /tmp/nc_recover.log)"; FAIL=$((FAIL+1)); fi

# 11) Provider 독립성 — 「설치됐다」를 「쓸 수 있다」로 읽지 않는지, 장애를 코드 실패와
#     섞지 않는지, 능력으로 고르는지. 가짜 CLI를 만들어 실제로 물어본다.
FAKEBIN=$(mktemp -d)
cat > "$FAKEBIN/codex" <<'SH'
#!/bin/sh
echo "Not logged in. Run 'codex login' to authenticate."
exit 1
SH
chmod +x "$FAKEBIN/codex"
PATH="$FAKEBIN:$PATH" python3 - <<'PY' > /tmp/nc_providers.log 2>&1
import pathlib, sys, time
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import config, providers as P
cfg = config.load(pathlib.Path("config.json"))
bad = []

# ① 설치돼 있어도 로그아웃이면 AVAILABLE이 아니다 (PATH에 가짜 codex를 심어둠)
s = P.probe("codex", cfg.agent("codex"))
if s.state != P.AUTH_REQUIRED: bad.append(f"로그아웃 CLI를 {s.state}로 봄")
if s.usable: bad.append("AUTH_REQUIRED인데 쓸 수 있다고 봄")

# ② CLI 자체가 없으면 UNAVAILABLE
class Fake:
    type, bin, model, enabled = "codex", "definitely_not_here_xyz", None, True
    capabilities = ["CODING"]
if P.probe("ghost", Fake()).state != P.UNAVAILABLE: bad.append("없는 CLI를 UNAVAILABLE로 안 봄")

# ③ 실패 분류 — 장애 종류마다 처방이 다르다
# 해제 시각을 말해주면 그때까지 쉰다 — 기본 냉각으로 15분마다 두드리지 않는다
got = P.classify_failure("usage limit ... try again at 11:41 PM")
if not got or not (60 < got[1] <= 24*3600): bad.append(f"해제 시각 파싱 실패: {got}")
if P.classify_failure("rate limit, try again in 5 minutes")[1] != 300: bad.append("in 5 minutes 파싱 실패")
if P.classify_failure("429 too many requests")[1] != P.RATE_COOLDOWN: bad.append("시각 없을 때 기본값 아님")

for text, want in [
    ("Error: 429 rate limit exceeded", P.RATE_LIMITED),
    ("HTTP 401 Unauthorized", P.AUTH_REQUIRED),
    ("insufficient credit — please check billing", P.LIMITED),
]:
    got = P.classify_failure(text)
    if not got or got[0] != want: bad.append(f"{want}를 {got}로 분류")
# **가장 중요한 음성 대조**: 코드 오류를 Provider 장애로 오인하면 영원히 승계만 돈다
for text in ["Assets/Game/Scripts/X.cs(3,9): error CS1525: Invalid expression",
             "Test failed: Expected 1 but was 2"]:
    if P.classify_failure(text) is not None: bad.append(f"코드 오류를 Provider 장애로 오인: {text[:30]}")

# ④ 능력으로 고른다 — 이름이 아니라
st = {n: P.Status(n, P.AVAILABLE, caps=P.caps_of(cfg, n)) for n in cfg.raw["agents"]}
coding = P.pick(cfg, st, capability=P.CODING, prefer=cfg.ladder)
if "ollama" in coding: bad.append("CODING 능력이 없는 ollama가 뽑힘")
if coding[:1] != ["codex"]: bad.append(f"선호 순서 무시: {coding}")
if "ollama" not in P.pick(cfg, st, capability=P.LOCAL): bad.append("LOCAL 능력으로 ollama를 못 찾음")

# ⑤ 상태가 나쁘면 빠진다(냉각 중 포함)
st["codex"] = P.Status("codex", P.RATE_LIMITED, cooldown_until=time.time() + 600,
                       caps=P.caps_of(cfg, "codex"))
if "codex" in P.pick(cfg, st, capability=P.CODING, prefer=cfg.ladder):
    bad.append("RATE_LIMITED인데 뽑힘")

# ⑥ 클라우드 전부 불가 판정
down = {n: P.Status(n, P.AUTH_REQUIRED, caps=P.caps_of(cfg, n)) for n in cfg.raw["agents"]}
down["ollama"] = P.Status("ollama", P.AVAILABLE, caps=P.caps_of(cfg, "ollama"))
if not P.cloud_all_down(cfg, down): bad.append("클라우드 전멸을 못 알아봄")
if P.cloud_all_down(cfg, st): bad.append("한 곳이라도 살아있는데 전멸이라고 함")
print("PROV_OK" if not bad else "PROV_BAD " + " / ".join(bad))
PY
rm -rf "$FAKEBIN"
if grep -q "^PROV_OK" /tmp/nc_providers.log; then
  echo "  PASS  providers — 설치≠사용가능·장애 분류(코드 오류와 구분)·능력 기반 배정"; PASS=$((PASS+1))
else echo "  FAIL  providers (로그: /tmp/nc_providers.log)"; FAIL=$((FAIL+1)); fi

# 11a) 전부 막혔을 때 — 억지로 돌리지 말고 **보존**해야 한다(BLOCKED_CLOUD_REQUIRED).
mkcfg state/nc_blocked.json "exit 0"
AUTODEV_DISABLE_NC=1 AUTODEV_CONFIG="$HERE/state/nc_blocked.json" \
  ./autodev run "[NC] cloud_blocked" --agent nc >/tmp/nc_blocked.log 2>&1
BRC=$?
if grep -q "BLOCKED_CLOUD_REQUIRED로 보존" /tmp/nc_blocked.log && [[ $BRC -eq 3 ]]; then
  echo "  PASS  cloud_blocked — 억지로 안 돌리고 보존(rc=3)"; PASS=$((PASS+1))
else echo "  FAIL  cloud_blocked — rc=$BRC (로그: /tmp/nc_blocked.log)"; FAIL=$((FAIL+1)); fi
for id in $(grep -oE "task [0-9]+을" /tmp/nc_blocked.log | grep -oE "[0-9]+"); do
  ./autodev archive --task "$id" >/dev/null 2>&1
done

# 11b) 승계 — 앞 Provider가 한도를 맞으면 **시도를 태우지 않고** 다음 Provider가 이어받는다.
#      이어받을 때 인수인계 문서가 실제로 프롬프트에 들어가는지까지 본다.
python3 - <<'PY'
import json, pathlib
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["ladder"] = ["nc_p1", "nc_p2"]
cfg["reviewer"] = None
cfg["research_agent"] = None
cfg["agents"]["nc_p1"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "capabilities": ["CODING"],
    "args": ["-c", "printf 'namespace SandboxGame { public static class NcHand { public const int V = 1; } }\\n' > Assets/Game/Scripts/NcHand.cs; "
                   # 실전 재현: codex는 한도 오류를 **stderr로만** 냈다(2026-09-11 계획 7 T4).
                   # stdout만 보던 판정이 그것을 코드 실패로 오판해 시도 3번을 태웠다.
                   "echo \"ERROR: You've hit your usage limit.\" >&2; exit 1"]}
cfg["agents"]["nc_p2"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "capabilities": ["CODING"],
    "args": ["-c", "printf 'namespace SandboxGame { public static class NcHand2 { public const int V = 2; } }\\n' > Assets/Game/Scripts/NcHand2.cs"]}
pathlib.Path("state/nc_handoff.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=2))
PY
AUTODEV_CONFIG="$HERE/state/nc_handoff.json" ./autodev run "[NC] handoff" >/tmp/nc_handoff.log 2>&1
check handoff PASS "Provider 장애: nc_p1 = RATE_LIMITED" /tmp/nc_handoff.log  # stderr로만 온 한도 오류
HO_TASK=$(grep -E "^  task " /tmp/nc_handoff.log | head -1 | sed 's/.*: *//')
HO_PROMPT="logs/task$(printf '%04d' "${HO_TASK:-0}")-a2.prompt.txt"
if grep -q "인수인계: nc_p1 → nc_p2" /tmp/nc_handoff.log \
   && grep -q "시도 2/3" /tmp/nc_handoff.log \
   && [[ -f "$HO_PROMPT" ]] && grep -q "앞 담당의 작업이 이미 들어 있다" "$HO_PROMPT" \
   && grep -q "NcHand.cs" "$HO_PROMPT"; then
  echo "  PASS  handoff_context — 승계 프롬프트에 앞 담당의 diff·파일이 실려 감"; PASS=$((PASS+1))
else echo "  FAIL  handoff_context (프롬프트: $HO_PROMPT)"; FAIL=$((FAIL+1)); fi
# nc_* Provider 기록은 지운다(실제 Provider 캐시는 남긴다)
python3 - <<'PY'
import json, pathlib
p = pathlib.Path("state/providers.json")
if p.is_file():
    d = json.loads(p.read_text())
    p.write_text(json.dumps({k: v for k, v in d.items() if not k.startswith("nc")}, ensure_ascii=False, indent=2))
PY

# 11c) 리뷰어가 죽으면 — 「리뷰가 반려했다」와 「리뷰어가 못 돌았다」는 다른 일이다.
#      실전(계획 7 T3)에서 리뷰어가 한도에 걸렸는데 그냥 UNKNOWN으로 적혔다. 이제 승계한다.
python3 - <<'PYREV'
import json, pathlib
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["ladder"] = ["nc_impl"]; cfg["research_agent"] = None; cfg["reviewer"] = "nc_rev_dead"
cfg["agents"]["nc_impl"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "capabilities": ["CODING"],
    "args": ["-c", "printf 'namespace SandboxGame { public static class NcRevX { public const int V = 9; } }\\n' > Assets/Game/Scripts/NcRevX.cs"]}
cfg["agents"]["nc_rev_dead"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "capabilities": ["REVIEW"],
    "args": ["-c", "echo \"ERROR: You've hit your usage limit.\" >&2; exit 1"]}
cfg["agents"]["nc_rev_ok"] = {"type": "script", "bin": "/bin/sh", "timeout_sec": 60, "capabilities": ["REVIEW"],
    "args": ["-c", "printf '{\"verdict\":\"approve\",\"reasons\":[],\"severity\":\"low\"}' > review.json"]}
pathlib.Path("state/nc_revfail.json").write_text(json.dumps(cfg, ensure_ascii=False, indent=2))
PYREV
AUTODEV_CONFIG="$HERE/state/nc_revfail.json" ./autodev run "[NC] reviewer_failover" >/tmp/nc_revfail.log 2>&1
check reviewer_failover PASS "대체 리뷰어 nc_rev_ok" /tmp/nc_revfail.log
if grep -q "리뷰어 nc_rev_dead Provider 장애(RATE_LIMITED)" /tmp/nc_revfail.log \
   && grep -qE "^  status   : DONE" /tmp/nc_revfail.log; then
  echo "  PASS  reviewer_failover_state — 장애를 판정불가로 뭉개지 않고 승계"; PASS=$((PASS+1))
else echo "  FAIL  reviewer_failover_state (로그: /tmp/nc_revfail.log)"; FAIL=$((FAIL+1)); fi

# 12) 진행 없음 감지 — 「오래 걸린다」와 「멎었다」는 다르다.
#     라이선스 핸드셰이크에서 멎은 Unity를 10분 내내 기다린 적이 있다(2026-09-11).
python3 - <<'PYSTALL' > /tmp/nc_stall.log 2>&1
import sys, pathlib, tempfile
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import proc
d = pathlib.Path(tempfile.mkdtemp()); w = d / "fake.log"
bad = []
r = proc.run(["/bin/sh", "-c", f"echo start > {w}; sleep 60"], cwd=d, timeout=55,
             log_prefix=d / "a", watch_file=w, stall_sec=5)
if r.status != "STALLED": bad.append(f"멎은 것을 {r.status}로 봄")
if r.duration_s > 30: bad.append(f"타임아웃을 다 태움({r.duration_s:.0f}s)")
# 오탐 방지: 로그가 자라는 동안은 절대 죽이지 않는다
r2 = proc.run(["/bin/sh", "-c", f"for i in 1 2 3 4 5 6; do echo $i >> {w}; sleep 1; done"],
              cwd=d, timeout=30, log_prefix=d / "b", watch_file=w, stall_sec=3)
if r2.status != "EXITED": bad.append(f"일하는 것을 {r2.status}로 죽임")
# 로그가 아직 안 생겼다고 성급히 죽이지 않는다
r3 = proc.run(["/bin/sh", "-c", "sleep 4"], cwd=d, timeout=30,
              log_prefix=d / "c", watch_file=d / "never.log", stall_sec=2)
if r3.status != "EXITED": bad.append(f"로그 없는 것을 {r3.status}로 죽임")
print("STALL_OK" if not bad else "STALL_BAD " + " / ".join(bad))
PYSTALL
if grep -q "^STALL_OK" /tmp/nc_stall.log; then
  echo "  PASS  stall_watch — 멎은 것만 조기 종료(일하는 것·로그 없는 것은 그대로)"; PASS=$((PASS+1))
else echo "  FAIL  stall_watch (로그: /tmp/nc_stall.log)"; FAIL=$((FAIL+1)); fi
lap

# 13) Provider 대기 — 한도는 시간이 풀어주지만 **인증 만료는 영원히 안 풀린다**.
#     둘을 구분하지 못하면 사람 없는 밤에 무한정 기다리거나, 곧 풀릴 것을 포기한다.
FAKEB=$(mktemp -d)
printf '#!/bin/sh\necho "Not logged in."\nexit 1\n' > "$FAKEB/codex"; chmod +x "$FAKEB/codex"
PATH="$FAKEB:$PATH" python3 - <<'PYWAIT' > /tmp/nc_wait.log 2>&1
import sys, time, json, pathlib
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import config, providers as P, cli
bad = []
d = json.loads(pathlib.Path("config.json").read_text())
d["ladder"] = ["nc_w"]; d["reviewer"] = None; d["research_agent"] = None
d["agents"]["nc_w"] = {"type": "script", "bin": "/bin/sh", "args": ["-c", "exit 0"],
                       "timeout_sec": 60, "capabilities": ["CODING"]}
p = pathlib.Path("state/nc_wait.json"); p.write_text(json.dumps(d, ensure_ascii=False, indent=2))
cfg = config.load(p)

# ① 곧 풀릴 냉각이면 기다렸다가 깨어난다
P.mark("nc_w", P.RATE_LIMITED, "시험", 8)
if not cli.wait_for_provider(cfg, max_wait=180): bad.append("곧 풀릴 냉각을 기다리지 않음")

# ② 냉각이 대기 한도보다 길면 기다리지 않는다(무한 대기 금지, §18)
P.mark("nc_w", P.RATE_LIMITED, "시험", 3600)
t0 = time.time()
if cli.wait_for_provider(cfg, max_wait=30): bad.append("한도를 넘겨서까지 기다림")
if time.time() - t0 > 20: bad.append("세울 것을 오래 붙잡음")

# ③ 인증 만료는 시간이 풀어주지 않는다 — 기다리지 않고 세운다(로그아웃한 가짜 codex)
# 후보를 codex 하나로 좁힌다 — 다른 CODING 에이전트가 살아 있으면 "기다릴 이유 없음"이
# **정답**이라, 그대로 두면 이 시험이 아무것도 확인하지 못한다(한 번 그렇게 헛통과했다).
d2 = json.loads(json.dumps(d)); d2["ladder"] = ["codex"]
d2["agents"] = {"codex": dict(d2["agents"]["codex"], capabilities=["CODING"])}
p2 = pathlib.Path("state/nc_wait2.json"); p2.write_text(json.dumps(d2, ensure_ascii=False, indent=2))
cfg2 = config.load(p2)
P.mark("nc_w", P.AUTH_REQUIRED, "로그아웃", 0)
t0 = time.time()
if cli.wait_for_provider(cfg2, max_wait=120): bad.append("인증 만료인데 쓸 수 있다고 판단")
if time.time() - t0 > 60: bad.append("안 풀릴 것을 오래 기다림")

# ④ **냉각은 재검사로 덮이지 않는다** — 한도에 걸린 CLI도 login status는 "Logged in"이라
#    답한다(실측). force 재검사가 냉각을 지우면 곧바로 또 한도에 부딪힌다.
P.mark("codex", P.RATE_LIMITED, "시험", 1800)
st = P.probe_all(cfg2, force=True)
if st["codex"].usable: bad.append("force 재검사가 냉각을 덮었다")
P.mark("codex", P.AVAILABLE, "정리", 0)
print("WAIT_OK" if not bad else "WAIT_BAD " + " / ".join(bad))
PYWAIT
rm -rf "$FAKEB" state/nc_wait.json state/nc_wait2.json
python3 - <<'PYCLEAN'
import json, pathlib
p = pathlib.Path("state/providers.json")
if p.is_file():
    d = json.loads(p.read_text())
    p.write_text(json.dumps({k: v for k, v in d.items() if not k.startswith("nc")}, ensure_ascii=False, indent=2))
PYCLEAN
if grep -q "^WAIT_OK" /tmp/nc_wait.log; then
  echo "  PASS  provider_wait — 풀릴 것은 기다리고 안 풀릴 것·한도 밖은 세운다(냉각은 재검사로 안 덮임)"; PASS=$((PASS+1))
else echo "  FAIL  provider_wait (로그: /tmp/nc_wait.log)"; FAIL=$((FAIL+1)); fi
lap

# 14) 토큰 기록 — **어림수를 실측인 척하지 않는다**(§21).
#     codex만 스스로 보고한다(stderr). 보고하지 않는 CLI의 칸은 비어 있어야 하고,
#     화면에도 "보고 없음"이라고 적혀야 한다. 문자 수로 추정한 값을 같은 칸에 넣으면
#     비용 감각이 조용히 망가진다 — 틀린 숫자가 없는 것보다 나쁘다.
python3 - <<'PYTOK' > /tmp/nc_tok.log 2>&1
import sys, pathlib, tempfile, sqlite3
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core import db
from autodev_core.agents.base import parse_tokens
from autodev_core.config import AgentConfig
from autodev_core.agents.script import ScriptAgent
bad = []

# ① 실제 codex 형식(stderr, 천단위 쉼표)을 읽는다
def mk(name, sh):
    return ScriptAgent(AgentConfig(name=name, type="script", bin="/bin/sh", model=None,
        sandbox_mode="", timeout_sec=30, args=["-c", sh], extra_config=[],
        permission_mode="", capabilities=["CODING"], enabled=True))
d = pathlib.Path(tempfile.mkdtemp())
a = mk("nc_tok", "printf 'tokens used\\n30,845\\n' >&2")
r = a.run("x", worktree=d, log_prefix=d / "t")
if r.tokens != 30845: bad.append(f"stderr 보고를 못 읽음({r.tokens})")

# ② 아무 말 없는 CLI는 **None** — 지어내지 않는다
b = mk("nc_tok2", "echo '긴 출력만 잔뜩 내놓는다. 토큰 이야기는 안 한다.'")
r2 = b.run("x", worktree=d, log_prefix=d / "u")
if r2.tokens is not None: bad.append(f"보고 없는데 숫자를 만듦({r2.tokens})")

# ③ 형식이 깨지면 조용히 None(옛 형식으로 읽은 값을 지금 값인 척하지 않는다)
if parse_tokens("tokens used: many") is not None: bad.append("숫자 아닌 것을 숫자로 봄")
if parse_tokens("0 tokens used") is not None: bad.append("0을 실측으로 기록")

# ④ DB에 들어갈 때 출처가 갈린다
c = db.connect(pathlib.Path(tempfile.mkdtemp()) / "t.sqlite3")
db.record_usage(c, task_id=1, attempt_id=1, agent="a", model=None, ok=True,
                duration_s=1.0, prompt_chars=10, output_chars=10, tokens=30845)
db.record_usage(c, task_id=1, attempt_id=2, agent="b", model=None, ok=True,
                duration_s=1.0, prompt_chars=99999, output_chars=99999, tokens=None)
got = [(r["tokens"], r["tokens_src"]) for r in c.execute("SELECT tokens,tokens_src FROM usage ORDER BY id")]
if got != [(30845, "measured"), (None, "none")]: bad.append(f"출처 구분 실패: {got}")
print("TOK_OK" if not bad else "TOK_BAD " + " / ".join(bad))
PYTOK
if grep -q "^TOK_OK" /tmp/nc_tok.log; then
  echo "  PASS  token_usage — 보고한 것만 실측으로 기록(없으면 빈칸, 지어내지 않음)"; PASS=$((PASS+1))
else echo "  FAIL  token_usage (로그: /tmp/nc_tok.log)"; FAIL=$((FAIL+1)); fi
lap

# 15) 컨텍스트 상한 — 프롬프트가 커져서 **목표 문구가 사라지는** 것을 막는다.
#     조용히 자르면 AI는 전부 봤다고 믿는다. 자른 사실이 프롬프트 안에 적혀 있어야 한다.
python3 - <<'PYCTX' > /tmp/nc_ctx.log 2>&1
import sys, pathlib
sys.path.insert(0, str(pathlib.Path.cwd()))
from autodev_core.agents.base import CliAgent, clamp
from autodev_core.config import AgentConfig
bad = []
a = CliAgent(AgentConfig(name="x", type="script", bin="/bin/sh", model=None, sandbox_mode="",
    timeout_sec=1, args=[], extra_config=[], permission_mode="", capabilities=[], enabled=True))
big = a.build_prompt(goal="목표문구_고유표식", worktree="/w", allowed=["Assets/Game/**"],
    unity_version="6000.3.14f1", handoff="H" * 200000,
    failure={"n": 1, "verdict": "FAILED", "reason": "r", "errors": "E" * 500000},
    max_chars=20000)
if len(big) > 20000: bad.append(f"상한을 넘김({len(big)})")
if "목표문구_고유표식" not in big: bad.append("목표가 잘려나감(§6 요구사항 축소를 우리가 만듦)")
if "Assets/Game/**" not in big: bad.append("쓰기 허용 범위가 잘려나감")
if "자 생략" not in big: bad.append("**조용히** 잘랐다 — 자른 사실을 안 적음")
if "E" * 100 not in big: bad.append("오류 꼬리를 통째로 버림(진짜 실패 원인은 끝에 있다)")
# 오탐: 짧은 프롬프트는 건드리지 않는다
sm = a.build_prompt(goal="g", worktree="/w", allowed=["a"], unity_version="6000")
if "자 생략" in sm: bad.append("짧은 것까지 잘랐다")
# clamp 자체: 머리와 꼬리가 남는다
c = clamp("A" * 5000 + "Z" * 5000, 2000, "시험")
if not c.startswith("A") or not c.endswith("Z"): bad.append("머리·꼬리를 못 지킴")
print("CTX_OK" if not bad else "CTX_BAD " + " / ".join(bad))
PYCTX
if grep -q "^CTX_OK" /tmp/nc_ctx.log; then
  echo "  PASS  context_cap — 상한 안에서 자르되 목표·규칙은 남기고 자른 사실을 적는다"; PASS=$((PASS+1))
else echo "  FAIL  context_cap (로그: /tmp/nc_ctx.log)"; FAIL=$((FAIL+1)); fi
lap

echo
echo "=== 결과: PASS=$PASS FAIL=$FAIL · 소요 $(( $(date +%s) - T0 ))초 ==="
if [[ ${#TASKS[@]} -gt 0 ]]; then
  echo "정리 중: task ${TASKS[*]} (worktree 삭제라 수십 초 걸릴 수 있다)"
  for t in "${TASKS[@]}"; do ./autodev clean --task "$t" --delete-branch >/dev/null 2>&1; done
fi
rm -f state/nc_*.json
[[ $FAIL -eq 0 ]]
