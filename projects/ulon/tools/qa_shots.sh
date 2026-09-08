#!/bin/bash
# 검수용 화면 근거 — 배치모드에서 오프스크린 렌더(-nographics 없이). 에디터를 닫고 돌려라.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="$ROOT/unity/Logs/qa_shots.log"
exec "$UNITY" -batchmode -quit -projectPath $ROOT/unity -executeMethod Ulon.Editor.QaShots.Run -logFile "$LOG"
