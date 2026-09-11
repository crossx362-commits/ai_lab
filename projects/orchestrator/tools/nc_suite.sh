#!/bin/bash
# 네거티브 컨트롤 스위트 — 게이트가 "빨간불을 낼 줄 아는지"를 매번 확인한다.
#
# 통과(초록불)만 확인하는 검증은 검증이 아니다. 여기서는 **일부러 틀린 입력**을 넣고
# 시스템이 정확한 이유로 막는지를 본다. 모델을 부르지 않으므로 비용 없이 반복 가능하다.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"
cd "$HERE"

PASS=0
FAIL=0
TASKS=()

mkcfg() {  # mkcfg <파일> <셸명령> [timeout]
  python3 - "$1" "$2" "${3:-60}" <<'PY'
import json, pathlib, sys
out, script, timeout = sys.argv[1], sys.argv[2], int(sys.argv[3])
cfg = json.loads(pathlib.Path("config.json").read_text())
cfg["agents"]["nc"] = {"type": "script", "bin": "/bin/sh", "args": ["-c", script], "timeout_sec": timeout}
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

echo
echo "=== 결과: PASS=$PASS FAIL=$FAIL ==="
if [[ ${#TASKS[@]} -gt 0 ]]; then
  echo "정리 중: task ${TASKS[*]}"
  for t in "${TASKS[@]}"; do ./autodev clean --task "$t" --delete-branch >/dev/null 2>&1; done
fi
rm -f state/nc_*.json
[[ $FAIL -eq 0 ]]
