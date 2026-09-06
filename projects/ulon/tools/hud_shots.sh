#!/bin/bash
# HUD가 찍힌 화면 증거 — 스탠드얼론을 띄워 ScreenCapture로 찍는다(창이 잠깐 뜬다).
# HUD는 IMGUI라 편집기 오프스크린 렌더(qa_shots)에 안 나온다. **매 랩 돌리지 마라** — HUD를 건드린 랩에서만.
set -euo pipefail
ROOT=/Users/junholee/ai_lab/projects/ulon
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.FrameProbeBuild.Run -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$ROOT/builds/frameprobe/Ulon.app/Contents/MacOS/Ulon" -hudshots -shotdir "$ROOT/builds/qa" -logFile "$ROOT/unity/Logs/hud_shots.log"
ls -la "$ROOT/builds/qa"/hud_*.png
