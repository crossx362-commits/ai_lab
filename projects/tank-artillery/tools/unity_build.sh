#!/usr/bin/env bash
# Unity 배치 빌드. 에디터를 열지 않고 Tankfall 바이너리를 만든다.
#
#   ./tools/unity_build.sh              # 플랫폼에 맞춰 자동 빌드 (Win: .exe / Mac: .app)
#   ./tools/unity_build.sh run          # 빌드 후 -autoshot 실행
#   UNITY_VER=6000.6.0f1 ./tools/unity_build.sh
#
# 종료 코드: 0 성공, 1 빌드 실패, 2 Unity 없음.
set -u
cd "$(dirname "$0")/.."
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

