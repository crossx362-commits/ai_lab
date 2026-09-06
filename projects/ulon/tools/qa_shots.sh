#!/bin/bash
# 검수용 화면 근거 — 배치모드에서 오프스크린 렌더(-nographics 없이). 에디터를 닫고 돌려라.
set -euo pipefail
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="/Users/junholee/ai_lab/projects/ulon/unity/Logs/qa_shots.log"
exec "$UNITY" -batchmode -quit -projectPath /Users/junholee/ai_lab/projects/ulon/unity -executeMethod Ulon.Editor.QaShots.Run -logFile "$LOG"
