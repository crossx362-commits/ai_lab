#!/bin/bash
# 성능 계측 보고서(게이트 없음) — 숫자를 먼저 본다. 에디터를 닫고 돌려라.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="$ROOT/unity/Logs/perf_report.log"
exec "$UNITY" -batchmode -quit -projectPath $ROOT/unity -executeMethod Ulon.Editor.PerfReport.Run -logFile "$LOG"
