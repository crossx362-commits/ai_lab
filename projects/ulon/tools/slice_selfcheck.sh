#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
# 동시 실행 오귀속 방지(검수 세션과 로그 공유 사고, 2026-09-06): SELFCHECK_LOG로 파일을 분리한다.
LOG="${SELFCHECK_LOG:-$ROOT/unity/Logs/slice_selfcheck_titles.log}"
exec "$UNITY" -batchmode -nographics -quit -projectPath $ROOT/unity -executeMethod Ulon.Editor.SliceSelfCheck.Run -logFile "$LOG"
