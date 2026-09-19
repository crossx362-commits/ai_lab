#!/usr/bin/env bash
# SIM 레이어 검증 — 유니티를 켜지 않고 돌린다.
#
#   ./tools/verify.sh          전체
#   ./tools/verify.sh sdf      지형·성능만
#   ./tools/verify.sh play     §7-6 세 문제만
#   ./tools/verify.sh battle   AI 자동 대전(명중률·한 판 길이)
#   ./tools/verify.sh compile  컴파일만 (유니티 Play 가능 여부)
#   ./tools/verify.sh game     게임 빌드 자체검사 10종 (빌드 1회 + 전부 실행 · all 에는 안 물려 있다)
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

# ══════════════════════════════════════════════════════════════════════
#  게임 빌드 자체검사 10종 (verify.sh game)
#
#  왜 따로 있나: 이 열 개는 **유니티 빌드 안에서만** 도는 검사다(SIM 하네스로는 못 잰다 —
#  화면·저장·연출·입력이 걸려 있다). 그런데 여태 `verify.sh` 에 물려 있지 않아서
#  **사람이 손으로 하나씩** 돌려야 했고, 그래서 빠뜨려도 아무도 몰랐다.
#
#  ⚠️ 스크린샷을 찍는 모드(-uiselftest · -practiceselftest)는 반드시 `open -a` 로 띄운다.
#     바이너리를 직접 부르면 캡처가 전부 실패한다(2026-09-18 실측). `-nographics` 도 금지다
#     (PNG 가 새까맣게 나오는데 로그는 통과로 찍힌다).
#  ⚠️ 나머지 여덟은 화면을 안 찍으므로 `-batchmode -logFile` 로 직접 불러 **진짜 rc** 를 받는다.
#     `open` 은 앱의 종료 코드를 안 돌려주므로, 찍는 둘만 로그의 ❌/✅ 로 판정한다.
#  ⚠️ 타임아웃이 곧 실패다. 자체검사가 멈춰 선 것을 "아직 도는 중"으로 두면 게이트가 영영 안 끝난다.
# ══════════════════════════════════════════════════════════════════════
GAME_TIMEOUT="${TANKFALL_GAME_TIMEOUT:-240}"
GAME_LOGDIR="${TANKFALL_GAME_LOGDIR:-/tmp/tankfall_game}"

# $1 = 플래그(-ultselftest 등) · $2 = shot 이면 open -a 경로
run_game_selftest() {
  local flag="$1" mode="${2:-batch}" log="$GAME_LOGDIR/${1#-}.log" rc=0
  rm -f "$log"
  if [ "$mode" = "shot" ]; then
    rm -rf "$GAME_LOGDIR/${1#-}_shots"; mkdir -p "$GAME_LOGDIR/${1#-}_shots"
    open -W -a "$PWD/$GAME_APP" --args "$flag" -shotdir "$GAME_LOGDIR/${1#-}_shots" \
         -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "$log" &
    local pid=$!
    local waited=0
    while kill -0 "$pid" 2>/dev/null; do
      [ "$waited" -ge "$GAME_TIMEOUT" ] && { kill "$pid" 2>/dev/null; pkill -f "$GAME_APP" 2>/dev/null; rc=124; break; }
      sleep 2; waited=$((waited + 2))
    done
    wait "$pid" 2>/dev/null || true
    # open 은 앱의 종료 코드를 안 준다 — 로그로 판정한다.
    if [ "$rc" -eq 0 ]; then
      grep -q "❌" "$log" 2>/dev/null && rc=1
      grep -q "✅" "$log" 2>/dev/null || rc=1      # ✅ 가 아예 없으면 도중에 죽은 것이다
    fi
  else
    # ⚠️ stdout/stderr 를 버린다 — `-logFile` 을 줘도 플레이어가 memorysetup 수십 줄을 그대로 뱉어서
    #    결과 줄(✅/❌)이 그 안에 묻힌다. 판정에 쓰는 건 rc 와 로그 파일이지 stdout 이 아니다.
    "$GAME_EXE" "$flag" -batchmode -logFile "$log" >/dev/null 2>&1 &
    local pid=$!
    local waited=0
    while kill -0 "$pid" 2>/dev/null; do
      [ "$waited" -ge "$GAME_TIMEOUT" ] && { kill "$pid" 2>/dev/null; rc=124; break; }
      sleep 2; waited=$((waited + 2))
    done
    if [ "$rc" -ne 124 ]; then wait "$pid"; rc=$?; fi
  fi

  if [ "$rc" -eq 0 ]; then
    # ⚠️ `cut -c` 로 자르지 마라 — macOS 의 cut 은 **바이트**로 잘라서 한글이 깨진 채 찍힌다(실측).
    printf '  ✅ %-20s %s\n' "$flag" "$(grep -m1 '✅' "$log" 2>/dev/null | sed 's/^\[Tankfall\] *//')"
  elif [ "$rc" -eq 124 ]; then
    echo "  ❌ $flag — ${GAME_TIMEOUT}초 안에 안 끝났다(멈춰 섰거나 종료를 안 부른다). 로그: $log"
    RC=1
  else
    echo "  ❌ $flag — rc=$rc"
    grep -m3 '❌' "$log" 2>/dev/null | sed 's/^/       /'
    echo "       로그: $log"
    RC=1
  fi
}

game_check() {
  echo "### 게임 자체검사 10종 — 빌드해서 실제로 돌린다"
  if [ "$UNAME" = "Darwin" ]; then
    GAME_APP="unity/Build/Tankfall.app"; GAME_EXE="$PWD/$GAME_APP/Contents/MacOS/unity"
    [ -x "$GAME_EXE" ] || GAME_EXE="$PWD/$GAME_APP/Contents/MacOS/Tankfall"
  else
    GAME_APP="unity/Build/Tankfall.exe"; GAME_EXE="$PWD/$GAME_APP"
  fi
  # 판별용 손잡이: 다른 빌드를 재거나, **빨간불이 실제로 뜨는지** 확인할 때 쓴다
  # (`TANKFALL_GAME_EXE=/usr/bin/false ./tools/verify.sh game` → 여덟 개가 전부 ❌ 로 떠야 정상이다).
  [ -n "${TANKFALL_GAME_EXE:-}" ] && GAME_EXE="$TANKFALL_GAME_EXE"
  mkdir -p "$GAME_LOGDIR"

  if [ "${TANKFALL_SKIP_BUILD:-0}" = "1" ]; then
    echo "  · 빌드 건너뜀(TANKFALL_SKIP_BUILD=1) — **지금 작업 트리가 아니라 옛 빌드를 잰다**"
  else
    echo "  · 유니티 배치 빌드 (몇 분 걸린다 · 건너뛰려면 TANKFALL_SKIP_BUILD=1)"
    if ! ./tools/unity_build.sh >/dev/null 2>&1; then
      echo "  ❌ 빌드 실패 — tools/.unity_build.log 를 봐라"; RC=1; return
    fi
  fi
  [ -x "$GAME_EXE" ] || { echo "  ❌ 실행 파일이 없다: $GAME_EXE"; RC=1; return; }

  for f in -supplyselftest -ultselftest -impairselftest -climateselftest \
           -boomselftest -rosterselftest -gameselftest -settingsselftest; do
    run_game_selftest "$f" batch
  done
  # 화면을 찍는 둘 — open -a 로만
  run_game_selftest -practiceselftest shot
  run_game_selftest -uiselftest shot
}

case "${1:-all}" in
  compile) compile_check; manifest_check ;;
  # ⚠️ `all` 에 넣지 않았다 — 유니티 빌드 1회(수 분)가 붙어서 SIM 게이트의 짧은 왕복을 죽인다.
  #    기능 커밋 전에 `verify.sh all` 과 **둘 다** 돌려라. 빌드를 아끼려면 TANKFALL_SKIP_BUILD=1.
  game)    game_check ;;
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
  *) echo "사용: $0 [all|game|sdf|play|ball|battle|turn|shell|nice|map|hit|guide|aimove|shellpick|compile]"; exit 2 ;;
esac

echo
[ $RC -eq 0 ] && echo "=== 전체 통과 ===" || echo "=== 실패 있음 ==="
exit $RC
