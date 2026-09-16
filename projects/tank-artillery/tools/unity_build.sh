#!/usr/bin/env bash
# Unity 배치 빌드. 에디터를 열지 않고 Tankfall.exe 를 만든다.
#
#   ./tools/unity_build.sh              # Windows 64 개발(Build/Tankfall.exe)
#   ./tools/unity_build.sh run          # 빌드 후 -autoshot 실행
#   UNITY_VER=6000.6.0f1 ./tools/unity_build.sh
#
# 종료 코드: 0 성공, 1 빌드 실패, 2 Unity.exe 없음.
set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
PROJ="$ROOT/unity"
OUT_DIR="$PROJ/Build"
LOG="${TANKFALL_BUILD_LOG:-$ROOT/tools/.unity_build.log}"
UNITY_VER="${UNITY_VER:-6000.6.0f1}"

find_unity() {
  if [ -n "${UNITY_EXE:-}" ] && [ -x "$UNITY_EXE" ]; then echo "$UNITY_EXE"; return; fi
  local cands=(
    "/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "/mnt/c/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "C:/Program Files/Unity/Hub/Editor/$UNITY_VER/Editor/Unity.exe"
    "$HOME/Unity/Hub/Editor/$UNITY_VER/Editor/Unity"
    "/Applications/Unity/Hub/Editor/$UNITY_VER/Unity.app/Contents/MacOS/Unity"
  )
  local p
  for p in "${cands[@]}"; do
    [ -x "$p" ] || [ -f "$p" ] && { echo "$p"; return; }
  done
  return 1
}

UNITY=$(find_unity) || {
  echo "❌ Unity $UNITY_VER 없음. UNITY_EXE=/path/to/Unity.exe 를 주거나 Hub에 $UNITY_VER 설치."
  exit 2
}

echo "Unity: $UNITY"
echo "Project: $PROJ"
echo "Log: $LOG"
mkdir -p "$OUT_DIR" "$(dirname "$LOG")"

"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJ" \
  -executeMethod Tankfall.EditorTools.BuildScript.BuildWindows \
  -logFile "$LOG"
RC=$?

if [ $RC -ne 0 ]; then
  echo "❌ Unity 종료 $RC — 로그 끝:"
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi

EXE="$OUT_DIR/Tankfall.exe"
if [ ! -f "$EXE" ]; then
  echo "❌ 산출물 없음: $EXE"
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi

echo "✅ $EXE  ($(du -h "$EXE" | awk '{print $1}'))"

if [ "${1:-}" = "run" ]; then
  echo "실행: $EXE -autoshot"
  "$EXE" -autoshot -nographics -batchmode \
    -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
    -logFile "$ROOT/tools/.unity_run.log" || exit 1
fi
