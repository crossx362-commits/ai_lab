#!/bin/bash
# 프레임 시간 기준선 — Development 스탠드얼론을 빌드해 한 번 실행한다(창이 잠깐 뜬다).
# **매 랩 돌리지 마라.** 기준선을 갱신할 때만. 결과: builds/qa/frame_times.md
set -euo pipefail
ROOT=/Users/junholee/ai_lab/projects/ulon
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
OUT="$ROOT/builds/qa/frame_times.md"
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.FrameProbeBuild.Run -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$ROOT/builds/frameprobe/Ulon.app/Contents/MacOS/Ulon" -frameprobe -frameout "$OUT" -logFile "$ROOT/unity/Logs/frame_probe_run.log"
cat "$OUT"
