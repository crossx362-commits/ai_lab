#!/bin/bash
# 성능 계측 보고서(게이트 없음) — 숫자를 먼저 본다. 에디터를 닫고 돌려라.
set -euo pipefail
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="/Users/junholee/ai_lab/projects/ulon/unity/Logs/perf_report.log"
exec "$UNITY" -batchmode -quit -projectPath /Users/junholee/ai_lab/projects/ulon/unity -executeMethod Ulon.Editor.PerfReport.Run -logFile "$LOG"
