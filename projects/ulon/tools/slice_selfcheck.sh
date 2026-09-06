#!/bin/bash
set -euo pipefail
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
# 동시 실행 오귀속 방지(검수 세션과 로그 공유 사고, 2026-09-06): SELFCHECK_LOG로 파일을 분리한다.
LOG="${SELFCHECK_LOG:-/Users/junholee/ai_lab/projects/ulon/unity/Logs/slice_selfcheck_titles.log}"
exec "$UNITY" -batchmode -nographics -quit -projectPath /Users/junholee/ai_lab/projects/ulon/unity -executeMethod Ulon.Editor.SliceSelfCheck.Run -logFile "$LOG"
