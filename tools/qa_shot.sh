#!/bin/bash
# 재와 별 — 시각 QA 도구
#
# 지정한 화면을 실제 창으로 띄우고, 지정한 프레임에서 PNG를 찍고, 스스로 닫는다.
#
#   ./tools/qa_shot.sh                      # 기본: dungeon, 300프레임
#   GAME_START=hunt ./tools/qa_shot.sh      # 필드 전투 화면
#   ./tools/qa_shot.sh boss 600             # 보스전을 600프레임에서
#   ./tools/qa_shot.sh --skip-build hunt    # 코드 변경이 없으면 빌드 생략(빠름)
#
# 왜 이게 있어야 하나:
#   이 프로젝트는 "500체 700fps PASS"를 낸 화면이 **텅 비어 있던** 전례가 있다.
#   수치 검증은 전부 통과했는데 스프라이트를 하나도 안 그리고 있었다. 그리고
#   「파티원 5명 중 4명이 몹 스프라이트」도 수치로는 못 잡았다.
#   **그림은 그림으로만 확인된다.** 매 이터레이션마다 자기가 만든 화면을 직접 봐야 한다.
#
# 왜 프레임으로 지정하나:
#   시간(초)은 기계마다 로딩 속도가 달라 같은 순간을 못 잡는다. 프레임은 결정적이다.
#
# ⚠️ 에디터가 열려 있으면 배치 빌드가 프로젝트 락으로 죽는다(exit 21). 먼저 확인한다.

set -euo pipefail
cd "$(dirname "$0")/.."

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity}"
# 기본은 원본. 에디터가 원본을 잡고 있으면 사본으로 우회할 수 있다(CLAUDE.md §3) —
# 그때는 `GAME_PROJ=.../unity_meas ./tools/qa_shot.sh …`.
# 경로를 고정해 두면 **에디터가 열려 있는 동안 시각 검증이 통째로 막힌다.** 이 프로젝트는
# 화면 확인이 완료 기준이라, 막히는 순간 완료 판정을 못 한다(2026-08-15 실제로 막혔다).
PROJ="${GAME_PROJ:-$PWD/projects/ashes-to-stars/unity}"
# ⚠️ 출력 경로는 `PlayableScenesBuilder.BuildGame`이 **하드코딩**한다(`../../build_game`).
#    `-buildPath` 인자를 주면 받는 줄 알고 build_qa로 지정했다가, 빌드는 성공했는데
#    실행본이 없다는 상태가 됐다(실측 2026-08-15). 빌더를 고치지 않는 이유는 다른
#    검증 도구들이 이미 이 경로를 전제로 돌고 있어서다 — 여기가 맞추는 편이 안전하다.
BUILD="$PWD/projects/ashes-to-stars/build_game"
# 실행 파일 이름을 하드코딩하지 않는다 — 유니티가 `AshesToStars`가 아니라 `unity`로
# 만들어 두는 경우가 있다(실측: build_game/AshesToStars.app/Contents/MacOS/unity).
# `.app` 안의 실행 가능한 파일을 그때그때 찾는다.
find_app() { find "$BUILD" -maxdepth 4 -type f -perm +111 -path "*.app/Contents/MacOS/*" 2>/dev/null | head -1; }
APP="$(find_app)"
SHOTS="${GAME_SHOT_DIR:-$PWD/output/qa/ashes-to-stars/shots}"

SKIP_BUILD=0
[ "${1:-}" = "--skip-build" ] && { SKIP_BUILD=1; shift; }

MODE="${1:-${GAME_START:-dungeon}}"
FRAME="${2:-${GAME_SHOT_FRAME:-300}}"

mkdir -p "$SHOTS"

# ── 1. 에디터 락 확인. 남의 Unity를 죽이지 않는다(오너가 연 것일 수 있다)
if [ -f "$PROJ/Temp/UnityLockfile" ] && pgrep -f "Unity.app/Contents/MacOS/Unity" | grep -qv VBCS 2>/dev/null; then
  echo "⚠️ 유니티 에디터가 프로젝트를 잡고 있다 — 배치 빌드는 exit 21로 죽는다."
  echo "   에디터를 닫고 다시 실행하거나, --skip-build로 기존 빌드를 쓴다."
  [ "$SKIP_BUILD" = "0" ] && exit 21
fi

# ── 2. 빌드
# ⚠️ `--skip-build`인데 실행본이 없으면 **빌드하지 않고 실패로 말한다.**
#    처음엔 `|| [ ! -x "$APP" ]`로 자동 빌드하게 짰는데, 그러면 --skip-build가 의도를
#    잃고 에디터 락 가드까지 건너뛴다(실측: 락이 걸린 채 빌드가 돌아 exit 1).
#    "빌드하지 말라"는 지시는 "없으면 만들어라"보다 강해야 한다.
if [ "$SKIP_BUILD" = "1" ] && [ -z "$APP" ]; then
  echo "❌ --skip-build인데 실행본이 없다 (탐색: $BUILD/*.app/Contents/MacOS/)"
  echo "   먼저 빌드하려면 --skip-build 없이 실행할 것(에디터를 닫아야 한다)"
  exit 1
fi

if [ "$SKIP_BUILD" = "0" ]; then
  echo "▶ 빌드 중… (수 분)"
  "$UNITY" -batchmode -quit -nographics -logFile "$BUILD.log" \
    -projectPath "$PROJ" -executeMethod PlayableScenesBuilder.BuildGame \
    -buildPath "$BUILD" || { echo "❌ 빌드 실패 — $BUILD.log 확인"; tail -20 "$BUILD.log"; exit 1; }
  echo "✅ 빌드 완료"
else
  echo "▶ 빌드 생략(--skip-build) — 기존 실행본 사용"
fi

APP="$(find_app)"      # 빌드 직후 다시 찾는다 — 처음 실행 때는 위에서 못 찾았다
[ -n "$APP" ] && [ -x "$APP" ] || { echo "❌ 실행본을 못 찾았다 (탐색 경로: $BUILD/*.app/Contents/MacOS/)"; exit 1; }
echo "▶ 실행본: $APP"

# ── 3. 실제 창을 띄우고 찍고 닫는다
#    -batchmode를 쓰지 않는다. 화면을 확인하는 게 목적이라 실제로 렌더돼야 한다.
OUT="$SHOTS/qa_${MODE}.png"
rm -f "$OUT"
echo "▶ 실행: mode=$MODE frame=$FRAME"

GAME_START="$MODE" GAME_SHOT_DIR="$SHOTS" GAME_SHOT_FRAME="$FRAME" GAME_SHOT_SEC="${GAME_SHOT_SEC:-}" \
  "$APP" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 &
PID=$!

# 캡처 + 종료까지 기다린다. 안 끝나면 강제 종료 — 루프가 여기서 매달리면 안 된다.
for i in $(seq 1 60); do
  kill -0 $PID 2>/dev/null || break
  sleep 1
done
kill -0 $PID 2>/dev/null && { echo "⚠️ 시간 초과 — 강제 종료"; kill -9 $PID 2>/dev/null || true; }

# ── 4. 판정. 파일이 없거나 비면 **실패로 말한다** — 조용히 넘어가면 QA가 아니다
if [ ! -f "$OUT" ]; then
  echo "❌ 스크린샷이 안 생겼다: $OUT"
  echo "   모드 이름이 맞는지 확인(dungeon|hunt|boss|raid|party), 프레임이 너무 이른지 확인"
  exit 1
fi
SIZE=$(stat -f%z "$OUT")
echo "✅ $OUT (${SIZE} bytes)"
[ "$SIZE" -lt 10000 ] && echo "⚠️ 파일이 지나치게 작다 — 화면이 비어 있을 수 있다. 반드시 열어볼 것"
exit 0
