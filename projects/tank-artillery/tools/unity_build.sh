#!/usr/bin/env bash
# Unity 배치 빌드. 에디터를 열지 않고 Tankfall 바이너리를 만든다.
#
#   ./tools/unity_build.sh              # 플랫폼에 맞춰 자동 빌드 (Win: .exe / Mac: .app)
#   ./tools/unity_build.sh run          # 빌드 후 -autoshot 실행
#   UNITY_VER=6000.6.0f1 ./tools/unity_build.sh
#
# 종료 코드: 0 성공, 1 빌드 실패, 2 Unity 없음.
set -u

# ══════════════════════════════════════════════════════════════════════
#  자기 복사본에서 다시 실행한다 — `verify.sh` 와 **같은 가드**다 (2026-09-19)
#
#  bash 는 스크립트를 **실행하며 읽는다.** 유니티 배치 빌드는 2~3분 걸리므로, 그 사이 누가 이 파일을
#  고치면 **바이트 오프셋이 밀려** 엉뚱한 줄이 실행된다. `verify.sh` 에서 실제로 그 사고가 났고
#  (측정이 rc=1 로 끝났다) 거기는 이미 막았는데 **여기는 안 막혀 있었다** — 같은 함정이 남아 있으면
#  언젠가 같은 사고가 난다. 인수인계에 "가드 없음"으로 적어 두느니 그냥 옮긴다.
#
#  ⚠️ 복사본에서는 `$0` 이 임시 경로라 `dirname "$0"/..` 가 `/` 가 된다 — 원본 경로를
#     `TANKFALL_BUILD_SELF` 로 넘기고 **그걸로** 저장소를 찾는다.
#  ⚠️ 복사본은 **자기가 지운다**(EXIT trap). 부모는 `exec` 로 넘어가 trap 을 걸 수 없다.
#  ⚠️ 복사 실패 시에는 원본으로 그냥 간다(경고만) — 빌드를 못 하는 것보다 낫다.
# ══════════════════════════════════════════════════════════════════════
if [ -z "${TANKFALL_BUILD_REENTRY:-}" ]; then
  _bself="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  _bcopy="$(mktemp -t tankfall_build_run 2>/dev/null || echo "")"
  if [ -n "$_bcopy" ] && cp "$_bself" "$_bcopy" 2>/dev/null; then
    export TANKFALL_BUILD_REENTRY=1
    export TANKFALL_BUILD_SELF="$_bself"
    exec bash "$_bcopy" "$@"
  fi
  [ -n "$_bcopy" ] && rm -f "$_bcopy"
  echo "⚠️ 자기 복사 실패 — 원본에서 그대로 돈다. **빌드 중 이 파일을 편집하지 마라.**" >&2
else
  trap 'rm -f "$0"' EXIT
fi

# ⚠️ `$0` 이 아니라 원본 경로를 본다(위 복사본 재실행 때문에).
cd "$(dirname "${TANKFALL_BUILD_SELF:-$0}")/.."
ROOT="$(pwd)"
PROJ="$ROOT/unity"
OUT_DIR="$PROJ/Build"
LOG="${TANKFALL_BUILD_LOG:-$ROOT/tools/.unity_build.log}"
UNITY_VER="${UNITY_VER:-6000.6.0f1}"
UNAME="$(uname -s 2>/dev/null || echo Unknown)"

if [ "$UNAME" = "Darwin" ]; then
  HUB_DIR="/Applications/Unity/Hub/Editor"
  if [ ! -d "$HUB_DIR/$UNITY_VER" ]; then
    AUTO_VER="$(ls -1 "$HUB_DIR" 2>/dev/null | grep -E '^6000\.' | sort -V | tail -1)"
    [ -n "$AUTO_VER" ] && UNITY_VER="$AUTO_VER"
  fi
  BUILD_METHOD="Tankfall.EditorTools.BuildScript.BuildMac"
  OUT_BIN="$OUT_DIR/Tankfall.app"
  RUN_EXE="$OUT_DIR/Tankfall.app/Contents/MacOS/unity"
  [ -f "$RUN_EXE" ] || RUN_EXE="$OUT_DIR/Tankfall.app/Contents/MacOS/Tankfall"
else
  BUILD_METHOD="Tankfall.EditorTools.BuildScript.BuildWindows"
  OUT_BIN="$OUT_DIR/Tankfall.exe"
  RUN_EXE="$OUT_BIN"
fi

find_unity() {
  if [ -n "${UNITY_EXE:-}" ] && [ -x "$UNITY_EXE" ]; then echo "$UNITY_EXE"; return; fi
  local cands=(
    "/Applications/Unity/Hub/Editor/$UNITY_VER/Unity.app/Contents/MacOS/Unity"
    "/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "/mnt/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "C:/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "$HOME/Unity/Hub/Editor/$UNITY_VER/Editor/Unity"
  )
  local p
  for p in "${cands[@]}"; do
    [ -x "$p" ] || [ -f "$p" ] && { echo "$p"; return; }
  done
  return 1
}

UNITY=$(find_unity) || {
  echo "❌ Unity $UNITY_VER 없음. UNITY_EXE=/path/to/Unity 를 주거나 Hub에 $UNITY_VER 설치."
  exit 2
}

echo "OS: $UNAME"
echo "Unity: $UNITY"
echo "Project: $PROJ"
echo "Target Method: $BUILD_METHOD"
echo "Log: $LOG"
mkdir -p "$OUT_DIR" "$(dirname "$LOG")"

"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJ" \
  -executeMethod "$BUILD_METHOD" \
  -logFile "$LOG"
RC=$?

if [ $RC -ne 0 ]; then
  echo "❌ Unity 종료 $RC — 로그 끝:"
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi

if [ ! -e "$OUT_BIN" ]; then
  echo "❌ 산출물 없음: $OUT_BIN"
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi

echo "✅ $OUT_BIN  ($(du -sh "$OUT_BIN" | awk '{print $1}'))"

if [ "${1:-}" = "run" ] || [ "${1:-}" = "ui" ]; then
  # ⚠️ -nographics 를 붙이지 마라. 로그에는 "스크린샷 8/8 확인" 이 그대로 찍히는데
  #    실제 PNG 는 **전부 새까맣다**(맥에서 확인). 렌더 타깃이 없으니 ScreenCapture 가 빈 버퍼를 뜬다.
  #    "로그 통과 ≠ 그림이 나왔다" — 스크린샷은 반드시 열어봐라(하네스 규칙, CLAUDE.md).
  MODE_ARG="-autoshot"
  [ "${1:-}" = "ui" ] && MODE_ARG="-uiselftest"
  echo "실행: $RUN_EXE $MODE_ARG (창 있음 — -nographics 금지)"
  "$RUN_EXE" $MODE_ARG -batchmode \
    -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
    -logFile "$ROOT/tools/.unity_run.log" || exit 1
fi

