#!/usr/bin/env bash
# SIM 레이어 검증 — 유니티를 켜지 않고 돌린다.
#
#   ./tools/verify.sh          전체
#   ./tools/verify.sh sdf      지형·성능만
#   ./tools/verify.sh play     §7-6 세 문제만
#   ./tools/verify.sh battle   AI 자동 대전(명중률·한 판 길이)
#   ./tools/verify.sh compile  컴파일만 (유니티 Play 가능 여부)
#   ./tools/verify.sh turn|shell|nice   원작 시스템 라이브러리 단위 검증
#
# 상수를 바꿨으면 반드시 이걸 돌려라. 특히 CeilingCollapse.MinThickness,
# TankGroundProbe.StepHeight/WalkStep 은 바꾸면 탱크가 갇힌다(명세 §7-6).
set -u

cd "$(dirname "$0")/.."
SIM=unity/Assets/_Project/Scripts/Sim
VIEW=unity/Assets/_Project/Scripts/View

UNITY_VER="${UNITY_VER:-6000.3.14f1}"
UD="/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Data"
DOTNET="/c/Program Files/dotnet/dotnet"
CSC="$UD/DotNetSdkRoslyn/csc.dll"
COREREF="/c/Program Files/dotnet/shared/Microsoft.NETCore.App/9.0.19"
UE="$UD/Managed/UnityEngine"
NS="$UD/NetStandard/ref/2.1.0/netstandard.dll"
[ -f "$NS" ] || NS="$UD/il2cpp/build/deploy/netstandard.dll"

OUT="${TMPDIR:-/tmp}/tankfall_verify"
mkdir -p "$OUT"
RC=0

run_console() {   # $1=출력이름  $2...=소스
  local name="$1"; shift
  rm -f "$OUT/$name.dll"
  local a=(-nologo -target:exe -nostdlib+ -optimize+ -out:"$OUT/$name.dll")
  for d in System.Runtime System.Console System.Private.CoreLib System.Runtime.Extensions System.Collections System.Memory; do
    [ -f "$COREREF/$d.dll" ] && a+=(-r:"$COREREF/$d.dll")
  done
  a+=("$@")
  "$DOTNET" "$CSC" "${a[@]}" || { echo "  ❌ 컴파일 실패: $name"; RC=1; return 1; }
  # ⚠️ 옛 빌드가 돌아 "최적화 됐다"고 오판한 적이 있다. 없으면 실행하지 않는다.
  [ -f "$OUT/$name.dll" ] || { echo "  ❌ 산출물 없음: $name"; RC=1; return 1; }
  printf '%s' '{"runtimeOptions":{"tfm":"net9.0","framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}' > "$OUT/$name.runtimeconfig.json"
  "$DOTNET" "$OUT/$name.dll" || RC=1
}

compile_check() {
  echo "### 컴파일 — 유니티에서 Play 가능한가"
  rm -f "$OUT/simonly.dll"
  local a=(-nologo -target:library -nostdlib+ -out:"$OUT/simonly.dll")
  for d in System.Runtime System.Private.CoreLib System.Collections System.Memory System.Runtime.Extensions; do
    a+=(-r:"$COREREF/$d.dll")
  done
  a+=($SIM/*.cs)
  if "$DOTNET" "$CSC" "${a[@]}" >/dev/null 2>&1 && [ -f "$OUT/simonly.dll" ]; then
    echo "  ✅ Sim 이 UnityEngine 없이 컴파일된다 (asmdef noEngineReferences 규칙 §4-1)"
  else
    echo "  ❌ Sim 이 UnityEngine 에 의존하고 있다 — SIM 레이어 규칙 위반"; RC=1
  fi

  rm -f "$OUT/unityall.dll"
  local b=(-nologo -target:library -nostdlib+ -out:"$OUT/unityall.dll" -r:"$NS")
  for m in UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.InputLegacyModule UnityEngine.TextRenderingModule UnityEngine.ScreenCaptureModule UnityEngine.PhysicsModule; do   # PhysicsModule: CreatePrimitive 의 Collider(위성탄 빔)
    b+=(-r:"$UE/$m.dll")
  done
  b+=($SIM/*.cs $VIEW/*.cs)
  if "$DOTNET" "$CSC" "${b[@]}" 2>&1 | head -15 && [ -f "$OUT/unityall.dll" ]; then
    echo "  ✅ Sim + View 가 유니티 조건으로 컴파일된다"
  else
    echo "  ❌ View 컴파일 실패 — 유니티에서 에러가 난다"; RC=1
  fi
}

case "${1:-all}" in
  compile) compile_check ;;
  sdf)     run_console SdfVerify      $SIM/*.cs tools/SdfVerify.cs ;;
  play)    run_console GameplayVerify $SIM/*.cs tools/GameplayVerify.cs; echo
    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  ball)    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  battle)  run_console BattleSimVerify $SIM/*.cs tools/BattleSimVerify.cs ;;
  turn)    run_console TurnOrderVerify    $SIM/*.cs tools/TurnOrderVerify.cs ;;      # 딜레이 턴제(원작 규칙)
  shell)   run_console ShellEffectsVerify $SIM/*.cs tools/ShellEffectsVerify.cs ;;   # 2번탄 고유 메커니즘
  nice)    run_console NiceShotVerify     $SIM/*.cs tools/NiceShotVerify.cs ;;       # 나이스샷·SS
  all)
    compile_check; echo
    run_console SdfVerify      $SIM/*.cs tools/SdfVerify.cs; echo
    run_console GameplayVerify $SIM/*.cs tools/GameplayVerify.cs; echo
    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs; echo
    run_console TurnOrderVerify    $SIM/*.cs tools/TurnOrderVerify.cs; echo
    run_console ShellEffectsVerify $SIM/*.cs tools/ShellEffectsVerify.cs; echo
    run_console NiceShotVerify     $SIM/*.cs tools/NiceShotVerify.cs; echo
    run_console BattleSimVerify  $SIM/*.cs tools/BattleSimVerify.cs ;;
  *) echo "사용: $0 [all|sdf|play|ball|battle|turn|shell|nice|compile]"; exit 2 ;;
esac

echo
[ $RC -eq 0 ] && echo "=== 전체 통과 ===" || echo "=== 실패 있음 ==="
exit $RC
