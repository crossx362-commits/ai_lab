#!/usr/bin/env bash
# SIM 레이어 검증 — 유니티를 켜지 않고 돌린다.
#
#   ./tools/verify.sh          전체
#   ./tools/verify.sh sdf      지형·성능만
#   ./tools/verify.sh play     §7-6 세 문제만
#   ./tools/verify.sh battle   AI 자동 대전(명중률·한 판 길이)
#   ./tools/verify.sh compile  컴파일만 (유니티 Play 가능 여부)
#   ./tools/verify.sh game     게임 빌드 자체검사 10종 (빌드 1회 + 전부 실행 · all 에는 안 물려 있다)
#   ./tools/verify.sh dead     죽은 멤버 **후보** 목록 (판정 아님 — 전수 grep 으로 확인할 것)
#   ./tools/verify.sh turn|shell|nice   원작 시스템 라이브러리 단위 검증
#   ./tools/verify.sh blast    ExplosionResolver 계약 — 경계 20 케이스(예측 ↔ 그릇)
#   ./tools/verify.sh guide    미사일 초기 유도·자세 제어 단계
#   ./tools/verify.sh aimove   AI 이동(구덩이·장판 탈출·보급)
#   ./tools/verify.sh shellpick 탄종 선택(지속피해 중복 평가·독 저항)
#
# ── 이 파일의 «누구 자리인가» (2026-09-19 검수 결정) ─────────────────
#  같은 저장소에 여러 세션이 동시에 붙는다. 소유는 **파일이 아니라 목적**으로 가른다.
#
#   · **밸런스 담당 소유** — `BattleSimVerify` 와 그 **실행 경로**:
#     인자 파싱(`TANKFALL_MAP/SEED/ROW/AERO`)·시드·스위치·`balance_guard`·게이트 판정.
#     **측정을 흔드는 것 전부.** 여기를 고치면 어제 표와 오늘 표를 비교할 수 없게 된다.
#   · **공용** — `compile_check`·`manifest_check` 처럼 **«빌드가 되는가»를 재는 부분.**
#     그래픽·에셋 작업자가 자기 변경을 확인하려면 당연히 필요하다. 막지 마라.
#
#  ⚠️ 가드를 세울 때: **가드는 «자기 자신»을 막을 때 쓴다.** 그 가드의 존재를 모르는 남을
#     막으면 그건 가드가 아니라 «설명 없는 거부»다(`Build/.frozen` 을 안 만든 이유).
#
# 상수를 바꾸면 반드시 이걸 돌려라. 특히 CeilingCollapse.MinThickness,
# TankGroundProbe.StepHeight/WalkStep 은 바꾸면 탱크가 갖힌다(명세 §7-6).
set -u

# ══════════════════════════════════════════════════════════════════════
#  🚨 **자기 복사본에서 다시 실행한다** (2026-09-19)
#
#  bash 는 스크립트를 **실행하며 읽는다.** 그래서 오래 도는 게이트(`battle` 은 6분)가 도는 중에
#  누가 이 파일을 편집하면 **바이트 오프셋이 밀려** 엉뚱한 줄이 실행된다. 실제로 그랬다 —
#  4:4 판 길이 측정이 도는 동안 `game` 서브커맨드를 추가했더니 끝에서
#  `line 174: pid: unbound variable` 이 나고 **rc=1** 로 끝났다(측정 출력은 이미 찍힌 뒤라
#  표는 멀쩡했지만, 그건 운이었다).
#
#  인수인계에 「실행 중인 verify.sh 를 편집하지 마라」가 **이미 있었는데도** 났다.
#  지침 한 줄로 못 막은 것이니 **가드로 바꾼다** — bash 가 읽는 실체를 편집 대상이 아니게 만든다.
#  누가 도중에 원본을 고쳐도 복사본은 그대로라 오프셋이 안 밀린다.
#
#  ⚠️ 복사본에서는 `$0` 이 임시 경로라 `dirname "$0"/..` 가 `/` 가 된다.
#     그래서 원본 경로를 `TANKFALL_VERIFY_SELF` 로 넘기고 **그걸로** 저장소를 찾는다.
#  ⚠️ 복사본은 **자기가 지운다**(EXIT trap). 부모는 `exec` 로 넘어가므로 trap 을 걸 수 없다.
#  ⚠️ 복사에 실패하면 **그냥 원본으로 간다** — 게이트를 못 돌리는 것보다 낫다(경고만 찍는다).
# ══════════════════════════════════════════════════════════════════════
if [ -z "${TANKFALL_VERIFY_REENTRY:-}" ]; then
  _vself="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  _vcopy="$(mktemp -t tankfall_verify_run 2>/dev/null || echo "")"
  if [ -n "$_vcopy" ] && cp "$_vself" "$_vcopy" 2>/dev/null; then
    export TANKFALL_VERIFY_REENTRY=1
    export TANKFALL_VERIFY_SELF="$_vself"
    exec bash "$_vcopy" "$@"
  fi
  [ -n "$_vcopy" ] && rm -f "$_vcopy"
  echo "⚠️ 자기 복사 실패 — 원본에서 그대로 돈다. **도는 동안 이 파일을 편집하지 마라.**" >&2
else
  # 복사본이다. 다 돌면 스스로 지운다.
  trap 'rm -f "$0"' EXIT
fi

# ⚠️ `$0` 이 아니라 원본 경로를 본다(위 복사본 재실행 때문에).
cd "$(dirname "${TANKFALL_VERIFY_SELF:-$0}")/.."
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
  check_mod 'JsonUtility'                 'com.unity.modules.jsonserialize' 'JSON'
  check_mod 'OnGUI|GUILayout|GUI\.'       'com.unity.modules.imgui'          'IMGUI'
  [ $bad -eq 0 ] && echo "  ✅ 코드가 쓰는 모듈이 전부 manifest 에 있다" || RC=1
}

# ══════════════════════════════════════════════════════════════════════
#  착탄 효과 규칙이 **한 곳에만** 사는가 (2026-09-19)
#
#  🚨 같은 규칙이 게임(`BattleDemo`)과 하네스(`BattleSimVerify`)에 따로 적혀 있었고 **갈렸다** —
#     독은 하네스만 «자기 제외», 속박은 하네스만 «적만». 즉 **모든 밸런스 표가 게임과 다른 게임을
#     재고 있었다.** 컴파일로는 절대 안 잡히고, 승률이 조금 이상해 보일 뿐이라 몇 달도 갈 수 있다.
#  → 규칙을 `Sim/ShellEffects.cs` 의 `ImpactRules` 하나로 올렸고, **여기서 재발을 막는다**:
#     두 착탄 경로가 상태이상을 **직접** 걸면 실패. 규칙은 `ImpactRules` 를 거쳐야 한다.
#
#  ⚠️ `tools/ShellPickVerify.cs` 같은 **단위 검증**은 `StatusEffects` 를 직접 불러도 된다 —
#     그건 규칙을 적용하는 게 아니라 그 클래스 자체를 재는 것이다. 그래서 착탄 경로 두 파일만 본다.
# ══════════════════════════════════════════════════════════════════════
rules_check() {
  echo "### 착탄 효과 규칙 — 게임과 하네스가 같은 함수를 보는가"
  local bad=0
  for f in "$VIEW/BattleDemo.cs" tools/BattleSimVerify.cs; do
    # `_status.Poison(` · `status.Root(` 같은 **직접 적용**을 찾는다(선언·주석은 제외).
    local hits
    hits="$(grep -nE '(^|[^A-Za-z_.])_?status\.(Poison|Root|Burn)\(' "$f" | grep -v '^\s*//' | grep -v '///' || true)"
    if [ -n "$hits" ]; then
      echo "  ❌ $f 가 상태이상을 직접 건다 — 규칙은 ImpactRules 하나여야 한다"
      echo "$hits" | sed 's/^/       /'
      bad=1
    fi
  done
  # 대조군: 두 파일이 실제로 ImpactRules 를 부르고 있는가(안 부르면 위 검사는 «항상 초록»이다)
  local uses=0
  grep -q 'ImpactRules\.ApplyToHit' "$VIEW/BattleDemo.cs" && uses=$((uses + 1))
  grep -q 'ImpactRules\.ApplyToHit' tools/BattleSimVerify.cs && uses=$((uses + 1))
  if [ "$uses" -ne 2 ]; then
    echo "  ❌ 두 착탄 경로 중 ImpactRules 를 안 부르는 곳이 있다 (부르는 곳 $uses/2)"; bad=1
  fi
  [ $bad -eq 0 ] && echo "  ✅ 착탄 효과 규칙이 ImpactRules 한 곳에만 있다 (게임·하네스 둘 다 호출)" || RC=1
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
  for m in UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.InputLegacyModule UnityEngine.TextRenderingModule UnityEngine.ScreenCaptureModule UnityEngine.PhysicsModule UnityEngine.ScriptingModule UnityEngine.ImageConversionModule UnityEngine.AudioModule UnityEngine.ParticleSystemModule UnityEngine.JSONSerializeModule; do
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
    ./tools/unity_build.sh >/dev/null 2>&1
    local brc=$?
    # ⚠️ rc=3 은 **동결**이다(`unity/Build/.frozen`). "빌드 실패"로 뭉뚱그리면
    #    다음 사람이 컴파일 에러를 찾으러 로그를 뒤진다 — 원인이 전혀 다르다.
    if [ $brc -eq 3 ]; then
      echo "  🧊 빌드가 동결돼 있다 — 게이트를 돌릴 수 없다. 해동하거나 TANKFALL_SKIP_BUILD=1 로 기존 빌드를 재라."
      RC=1; return
    elif [ $brc -ne 0 ]; then
      echo "  ❌ 빌드 실패(rc=$brc) — tools/.unity_build.log 를 봐라"; RC=1; return
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


# ══════════════════════════════════════════════════════════════════════
#  🛑 밸런스 측정 가드 (2026-09-19)
#
#  오너 지시: **"밸런스 네트웍은 내가 하라고 하기전까지 하지마라"**.
#  `BattleSimVerify` 는 12×12 매치업 승률표를 다시 뜨는 **그 측정**이다 —
#  §2-5-0(판 길이)·난이도 사다리가 전부 그 표를 근거로 서 있다.
#
#  🚨 그런데 **`verify.sh all` 이 그걸 포함하고 있었다.** 즉 「전부 돌려라」가
#     곧 금지된 측정이었다. 실제로 이 가드를 만든 계기도 그것이다 —
#     다른 게이트를 확인하다 명령줄에 `battle` 을 **의도 없이 끼워 넣었다**
#     (파일을 안 쓰는 도구라 피해는 0 이었지만, 막을 수 있는 사고였다).
#
#  ⇒ 문서 규칙이 아니라 **코드로** 막는다(`Build/.frozen` 과 같은 발상).
#     허가가 오면 `TANKFALL_BALANCE_OK=1` 로 연다. 우회 스위치는 이것 하나뿐이다.
# ══════════════════════════════════════════════════════════════════════
balance_guard() {
  if [ "${TANKFALL_BALANCE_OK:-}" = "1" ]; then return 0; fi
  echo "  🛑 밸런스 측정(BattleSimVerify)은 **의도 확인**을 요구한다."
  echo "     사유: 6분짜리 측정이고, 이 표가 §2-5-0·난이도 사다리·M4 의 «기준선»이다 —"
  # ⚠️ 이 문구에 백틱을 쓰지 마라 — 큰따옴표 안의 백틱은 **명령 치환**이라 그 자리에서 실행된다.
  #    (2026-09-19: 여기에 \`verify.sh all\` 이라고 썼다가 가드가 전체 게이트를 부를 뻔했다.)
  echo "           'verify.sh all' 에 실려 «실수로» 다시 뜨면 어제 표와 비교할 근거가 사라진다."
  echo "     (2026-09-18 오너 동결은 09-19 해제됐다. 이건 «금지»가 아니라 «찍어서 부르는가»를 묻는 것이다.)"
  # ⚠️ 안내에 `$0` 을 쓰지 마라 — 자기 복사본 재실행 가드 때문에 **임시 경로**가 찍힌다.
  echo "     의도한 것이라면: TANKFALL_BALANCE_OK=1 ${TANKFALL_VERIFY_SELF:-./tools/verify.sh} battle"
  return 1
}

case "${1:-all}" in
  compile) compile_check; manifest_check; rules_check ;;
  # 죽은 멤버 **후보**만 낸다(판정 아님). rc 에 영향 주지 않는다 — 처분은 사람이 정한다.
  dead)    python3 tools/deadscan.py ;;
  # ⚠️ `all` 에 넣지 않았다 — 유니티 빌드 1회(수 분)가 붙어서 SIM 게이트의 짧은 왕복을 죽인다.
  #    기능 커밋 전에 `verify.sh all` 과 **둘 다** 돌려라. 빌드를 아끼려면 TANKFALL_SKIP_BUILD=1.
  game)    game_check ;;
  sdf)     run_console SdfVerify      $SIM/*.cs tools/SdfVerify.cs ;;
  play)    run_console GameplayVerify $SIM/*.cs tools/GameplayVerify.cs; echo
    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  pregame) run_console PregameVerify $SIM/*.cs tools/PregameVerify.cs ;;
  ball)    run_console BallisticsVerify $SIM/*.cs tools/BallisticsVerify.cs ;;
  blast)   run_console ExplosionResolverVerify $SIM/*.cs tools/ExplosionResolverVerify.cs ;;   # ExplosionResolver 계약(예측 ↔ 그릇)
  # ⚠️ **막힌 것을 rc=0 으로 말하지 마라.** 사람이 `battle` 을 «찍어서» 불렀는데 0 을 받으면
  #    「측정했고 통과」로 읽는다 — 오늘 「내 턴 평균 0.0초」와 같은 거짓말이다.
  #    빌드 동결이 rc=3 을 쓰듯 여기도 **«막힘» 전용 코드**를 준다(1=실패와 구별된다).
  battle)  balance_guard || exit 3
           run_console BattleSimVerify $SIM/*.cs tools/BattleSimVerify.cs ;;
  turn)    run_console TurnOrderVerify    $SIM/*.cs tools/TurnOrderVerify.cs ;;
  shell)   run_console ShellEffectsVerify $SIM/*.cs tools/ShellEffectsVerify.cs ;;
  nice)    run_console NiceShotVerify     $SIM/*.cs tools/NiceShotVerify.cs ;;
  map)     run_console MapVerify          $SIM/*.cs tools/MapVerify.cs ;;
  hit)     run_console TerrainHitVerify   $SIM/*.cs tools/TerrainHitVerify.cs ;;
  guide)   run_console GuidanceVerify     $SIM/*.cs tools/GuidanceVerify.cs ;;
  aimove)  run_console AiMoveVerify      $SIM/*.cs tools/AiMoveVerify.cs ;;
  shellpick) run_console ShellPickVerify  $SIM/*.cs tools/ShellPickVerify.cs ;;
  all)
    compile_check; manifest_check; rules_check; echo
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
    # ⚠️ `all` 에서도 가드를 탄다 — 「전부 돌려라」가 금지된 측정을 끌고 들어오면 안 된다.
    #    막히면 **건너뛰고 나머지 결과는 그대로 살린다**(여기서 rc 를 1 로 만들면 «막혔다»가 «실패»로 읽힌다).
    balance_guard && run_console BattleSimVerify  $SIM/*.cs tools/BattleSimVerify.cs
    echo ;;
  # ⚠️ `$0` 이 아니라 원본 경로 — 자기 복사본 재실행 가드 때문에 임시 경로가 찍힌다(balance_guard 와 같은 이유).
  *) echo "사용: ${TANKFALL_VERIFY_SELF:-./tools/verify.sh} [all|game|dead|sdf|play|ball|battle|turn|shell|nice|map|hit|guide|aimove|shellpick|compile]"; exit 2 ;;
esac

echo
[ $RC -eq 0 ] && echo "=== 전체 통과 ===" || echo "=== 실패 있음 ==="
exit $RC
