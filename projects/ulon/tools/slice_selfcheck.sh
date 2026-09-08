#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
# 동시 실행 오귀속 방지(검수 세션과 로그 공유 사고, 2026-09-06): SELFCHECK_LOG로 파일을 분리한다.
LOG="${SELFCHECK_LOG:-$ROOT/unity/Logs/slice_selfcheck_titles.log}"

# **매 판을 커밋 상태에서 출발시킨다**(검수 판정 2026-09-08, 재현성의 뿌리).
# 왜: 씬은 실행마다 빌더가 고쳐서 저장한다 — 게이트가 재는 씬은 **커밋된 것이 아니라 누적된 것**이다.
# 그래서 이번 판이 초록인 이유가 「지난 판이 저장해 준 덕」일 수 있고, 실제로 그것 때문에 두 트리가
# 같은 커밋에서 다른 결과를 냈다(무텍스처 618). 손으로 되돌려 확인한 절차를 여기에 박는다.
# 지우는 것이 아니라 **백업하고 되돌린다** — 되돌린 판이 사람이 만든 것이었을 때 돌아갈 자리를 남긴다.
SCENE="unity/Assets/Game/Scenes/Bootstrap.unity"
if git -C "$ROOT" rev-parse --git-dir >/dev/null 2>&1; then
  if ! git -C "$ROOT" diff --quiet -- "$SCENE" 2>/dev/null; then
    BACKUP="$ROOT/builds/scene_backup"
    mkdir -p "$BACKUP"
    # 백업은 **한 벌만** 둔다 — 판마다 쌓이면 그게 잔재다(검수 2026-09-08).
    cp "$ROOT/$SCENE" "$BACKUP/Bootstrap.last.unity"
    git -C "$ROOT" checkout -- "$SCENE"
    echo "씬을 커밋 상태로 되돌렸습니다(백업: $BACKUP) — 게이트는 누적된 씬이 아니라 소스에서 출발한다."
  fi
  # 되돌렸는데도 차이가 남으면 절차가 안 선 것이다 — 조용히 넘어가지 않는다(0이면 실패와 같은 이유).
  if ! git -C "$ROOT" diff --quiet -- "$SCENE" 2>/dev/null; then
    echo "씬을 커밋 상태로 되돌리지 못했습니다: $SCENE" >&2
    exit 4
  fi
fi

exec "$UNITY" -batchmode -nographics -quit -projectPath $ROOT/unity -executeMethod Ulon.Editor.SliceSelfCheck.Run -logFile "$LOG"
