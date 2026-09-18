#!/usr/bin/env bash
# SIM 레이어 검증 — 유니티를 켜지 않고 돌린다.
#
#   ./tools/verify.sh          전체
#   ./tools/verify.sh sdf      지형·성능만
#   ./tools/verify.sh play     §7-6 세 문제만
#   ./tools/verify.sh battle   AI 자동 대전(명중률·한 판 길이)
#   ./tools/verify.sh compile  컴파일만 (유니티 Play 가능 여부)
#   ./tools/verify.sh turn|shell|nice   원작 시스템 라이브러리 단위 검증
#   ./tools/verify.sh guide    미사일 초기 유도·자세 제어 단계
#   ./tools/verify.sh aimove   AI 이동(구덩이·장판 탈출·보급)
#   ./tools/verify.sh shellpick 탄종 선택(지속피해 중복 평가·독 저항)
#
# 상수를 바꾸면 반드시 이걸 돌려라. 특히 CeilingCollapse.MinThickness,
# TankGroundProbe.StepHeight/WalkStep 은 바꾸면 탱크가 갖힌다(명세 §7-6).
set -u

cd "$(dirname "$0")/.."
SIM=unity/Assets/_Project/Scripts/Sim
VIEW=unity/Assets/_Project/Scripts/View

UNITY_VER="${UNITY_VER:-6000.6.0f1}"
UNAME="$(uname -s 2>/dev/null || echo Unknown)"

if [ "$UNAME" = "Darwin" ]; then
  # macOS 경로 탐색
  HUB_DIR="/Applications/Unity/Hub/Editor"
  if [ ! -d "$HUB_DIR/$UNITY_VER" ]; then
    # 요청 버전이 없으면 설치된 최신 버전으로 자동 폴백
    AUTO_VER="$(ls -1 "$HUB_DIR" 2>/dev/null | grep -E '^6000\.' | sort -V | tail -1)"
    [ -n "$AUTO_VER" ] && UNITY_VER="$AUTO_VER"
  fi
  UD="$HUB_DIR/$UNITY_VER/Unity.app/Contents"
  DOTNET="$UD/Resources/Scripting/DotNetSdk/dotnet"
  [ -x "$DOTNET" ] || DOTNET="$(which dotnet 2>/dev/null || echo "")"
  CSC="$UD/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
  [ -f "$CSC" ] || CSC="$(find "$UD/Resources/Scripting/DotNetSdk/sdk" -maxdepth 4 -path "*/Roslyn/bincore/csc.dll" 2>/dev/null | head -1)"
  COREREF="$(find "$UD/Resources/Scripting/DotNetSdk/shared/Microsoft.NETCore.App" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | sort -V | tail -1)"
  UE="$UD/Resources/Scripting/Managed/UnityEngine"
  NS="$UD/Resources/Scripting/NetStandard/ref/2.1.0/netstandard.dll"
  [ -f "$NS" ] || NS="$UD/Resources/Scripting/il2cpp/build/deploy/netstandard.dll"
else
  # Windows (Git Bash / MSYS2 / WSL) 경로
  UD="/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Data"
  [ -d "$UD" ] || UD="/mnt/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Data"
  DOTNET="/c/Program Files/dotnet/dotnet"
  [ -x "$DOTNET" ] || DOTNET="$(which dotnet 2>/dev/null || echo "")"
  CSC="$UD/DotNetSdkRoslyn/csc.dll"
  [ -f "$CSC" ] || CSC="$(find "$UD/DotNetSdk/sdk" -maxdepth 4 -path "*/Roslyn/bincore/csc.dll" 2>/dev/null | head -1)"
  COREREF="$(find "/c/Program Files/dotnet/shared/Microsoft.NETCore.App" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | sort -V | tail -1)"
  [ -d "$COREREF" ] || COREREF="/c/Program Files/dotnet/shared/Microsoft.NETCore.App/9.0.19"
  UE="$UD/Managed/UnityEngine"
  NS="$UD/NetStandard/ref/2.1.0/netstandard.dll"
  [ -f "$NS" ] || NS="$UD/il2cpp/build/deploy/netstandard.dll"
fi

[ -n "$CSC" ] && [ -f "$CSC" ] || { echo "❌ csc.dll 을 못 찾음 (UNITY_VER=$UNITY_VER, OS=$UNAME) — Unity 설치 확인"; exit 2; }
[ -n "$DOTNET" ] && [ -x "$DOTNET" ] || { echo "❌ dotnet 을 못 찾음 (UNITY_VER=$UNITY_VER, OS=$UNAME) — .NET 또는 Unity 설치 확인"; exit 2; }

# COREREF 버전 추출 (예: 8.0.21 -> 8, 9.0.19 -> 9)
NET_VER="$(basename "$COREREF" 2>/dev/null | cut -d. -f1)"
NET_VER="${NET_VER:-9}"

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
  [ -f "$OUT/$name.dll" ] || { echo "  ❌ 산출물 없음: $name"; RC=1; return 1; }
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s.0.0"}}}' "$NET_VER" "$NET_VER" > "$OUT/$name.runtimeconfig.json"
  "$DOTNET" "$OUT/$name.dll" || RC=1
}

# ⚠️ 2026-09-17 사고: 이 검사는 **asmdef 와 Packages/manifest.json 을 보지 않는다.**
#    모듈 DLL 을 직접 -r 로 물려주므로, 코드가 쓰는 엔진 모듈이 manifest 에 없어도 여기서는 통과한다.
#    ParticleSystem 을 쓰기 시작했을 때 이 검사는 ✅ 였는데 **실제 Unity 빌드는 CS1069 로 실패**했다.
#    그래서 아래 manifest_check 가 "코드가 쓰는 모듈이 manifest 에 있는가"를 따로 본다.
#    교훈: "compile 통과 ≠ 유니티에서 빌드된다".
manifest_check() {
  echo "### 모듈 매니페스트 — 코드가 쓰는 엔진 모듈이 패키지 목록에 있는가"
  local mf="unity/Packages/manifest.json"
  [ -f "$mf" ] || { echo "  ❌ $mf 없음"; RC=1; return 1; }
  local bad=0
  # 쓰는 타입 → 필요한 모듈 패키지
  check_mod() {   # $1=grep 패턴  $2=모듈 패키지명  $3=설명
    if grep -rqE "$1" $SIM $VIEW 2>/dev/null; then
      if ! grep -q "$2" "$mf"; then
        echo "  ❌ $3 를 쓰는데 $2 가 manifest 에 없다 — 빌드에서 CS1069 로 깨진다"
        bad=1
      fi
    fi
  }
  check_mod 'ParticleSystem'              'com.unity.modules.particlesystem' 'ParticleSystem'
  check_mod 'AudioSource|AudioClip'       'com.unity.modules.audio'          'Audio'
  check_mod 'LoadImage|EncodeToPNG'       'com.unity.modules.imageconversion' 'ImageConversion'
  check_mod 'ScreenCapture'               'com.unity.modules.screencapture'  'ScreenCapture'
  check_mod 'OnGUI|GUILayout|GUI\.'       'com.unity.modules.imgui'          'IMGUI'
  [ $bad -eq 0 ] && echo "  ✅ 코드가 쓰는 모듈이 전부 manifest 에 있다" || RC=1
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
  # ⚠️ ScriptingModule: Unity 6000.6 부터 RuntimeInitializeOnLoadMethodAttribute 의 기반 타입이
  #    CoreModule 이 아니라 여기서 PreserveAttribute 를 끌어온다(6000.3 에서는 안 걸렸다) — 버전 올릴 때마다 재확인.
  for m in UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.InputLegacyModule UnityEngine.TextRenderingModule UnityEngine.ScreenCaptureModule UnityEngine.PhysicsModule UnityEngine.ScriptingModule UnityEngine.ImageConversionModule UnityEngine.AudioModule UnityEngine.ParticleSystemModule; do
    [ -f "$UE/$m.dll" ] && b+=(-r:"$UE/$m.dll")
  done
  [ -f "$UE/Unity.Scripting.dll" ] && b+=(-r:"$UE/Unity.Scripting.dll")
  b+=($SIM/*.cs $VIEW/*.cs)
  if "$DOTNET" "$CSC" "${b[@]}" 2>&1 | head -15 && [ -f "$OUT/unityall.dll" ]; then
    echo "  ✅ Sim + View 가 유니티 조건으로 컴파일된다"
  else
    echo "  ❌ View 컴파일 실패 — 유니티에서 에러가 난다"; RC=1
  fi
}

case "${1:-all}" in
  compile) compile_check; manifest_check ;;
  sdf)     run_console SdfVerify      $SIM/*.cs tools/SdfVerify.cs ;;
  play)    run_console GameplayVerify $SIM/*.cs tools/GameplayVerify.cs; echo
    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  ball)    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  battle)  run_console BattleSimVerify $SIM/*.cs tools/BattleSimVerify.cs ;;
  turn)    run_console TurnOrderVerify    $SIM/*.cs tools/TurnOrderVerify.cs ;;
  shell)   run_console ShellEffectsVerify $SIM/*.cs tools/ShellEffectsVerify.cs ;;
  nice)    run_console NiceShotVerify     $SIM/*.cs tools/NiceShotVerify.cs ;;
  map)     run_console MapVerify          $SIM/*.cs tools/MapVerify.cs ;;
  hit)     run_console TerrainHitVerify   $SIM/*.cs tools/TerrainHitVerify.cs ;;
  guide)   run_console GuidanceVerify     $SIM/*.cs tools/GuidanceVerify.cs ;;
  aimove)  run_console AiMoveVerify      $SIM/*.cs tools/AiMoveVerify.cs ;;
  shellpick) run_console ShellPickVerify  $SIM/*.cs tools/ShellPickVerify.cs ;;
  all)
    compile_check; manifest_check; echo
    run_console SdfVerify      $SIM/*.cs tools/SdfVerify.cs; echo
    run_console GameplayVerify $SIM/*.cs tools/GameplayVerify.cs; echo
    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs; echo
    run_console TurnOrderVerify    $SIM/*.cs tools/TurnOrderVerify.cs; echo
    run_console ShellEffectsVerify $SIM/*.cs tools/ShellEffectsVerify.cs; echo
    run_console NiceShotVerify     $SIM/*.cs tools/NiceShotVerify.cs; echo
    run_console MapVerify          $SIM/*.cs tools/MapVerify.cs; echo
    run_console TerrainHitVerify   $SIM/*.cs tools/TerrainHitVerify.cs; echo
    run_console GuidanceVerify     $SIM/*.cs tools/GuidanceVerify.cs; echo
    run_console AiMoveVerify      $SIM/*.cs tools/AiMoveVerify.cs; echo
    run_console ShellPickVerify   $SIM/*.cs tools/ShellPickVerify.cs; echo
    run_console BattleSimVerify  $SIM/*.cs tools/BattleSimVerify.cs ;;
  *) echo "사용: $0 [all|sdf|play|ball|battle|turn|shell|nice|map|hit|guide|aimove|shellpick|compile]"; exit 2 ;;
esac

echo
[ $RC -eq 0 ] && echo "=== 전체 통과 ===" || echo "=== 실패 있음 ==="
exit $RC
