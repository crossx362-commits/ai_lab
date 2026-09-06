#!/bin/bash
# HUD가 찍힌 화면 증거 — 스탠드얼론을 띄워 ScreenCapture로 찍는다(창이 잠깐 뜬다).
# HUD는 IMGUI라 편집기 오프스크린 렌더(qa_shots)에 안 나온다. **매 랩 돌리지 마라** — HUD를 건드린 랩에서만.
set -euo pipefail
ROOT=/Users/junholee/ai_lab/projects/ulon
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.FrameProbeBuild.Run -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$ROOT/builds/frameprobe/Ulon.app/Contents/MacOS/Ulon" -hudshots -shotdir "$ROOT/builds/qa" -logFile "$ROOT/unity/Logs/hud_shots.log"
# 그려진 조작 수단 기록은 게이트가 읽는다 — builds/는 git 무시라 추적 경로로 복사한다.
{ echo "# 기록 $(date -u +%Y-%m-%dT%H:%M:%SZ) · HEAD $(git -C "$ROOT" rev-parse --short HEAD) · 이 파일은 hud_shots.sh가 만든다(손으로 채우지 마라)"
  cat "$ROOT/builds/qa/hud_controls.txt"; } > "$ROOT/docs/hud_controls.txt"
ls -la "$ROOT/builds/qa"/hud_*.png
