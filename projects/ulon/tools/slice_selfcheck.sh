#!/bin/bash
set -euo pipefail
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="/Users/junholee/ai_lab/projects/ulon/unity/Logs/slice_selfcheck_titles.log"
exec "$UNITY" -batchmode -nographics -quit -projectPath /Users/junholee/ai_lab/projects/ulon/unity -executeMethod Ulon.Editor.SliceSelfCheck.Run -logFile "$LOG"
