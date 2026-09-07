#!/bin/bash
# 실행 중인 게임에서 사람형이 실제로 움직이는가 — 스탠드얼론을 띄워 본 위치 차이를 잰다(창이 잠깐 뜬다).
# 편집기 샷은 애니메이션이 안 돌아 QaShots가 포즈를 손으로 입힌다 — 그건 게임의 증거가 아니다.
# 결과: builds/qa/idle_check.md + builds/qa/idle_01_hunt.png
set -euo pipefail
ROOT=/Users/junholee/ai_lab/projects/ulon
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
# 씬 명단을 먼저 덤프한다 — 실행 실측이 「무엇을 안 덮었는지」를 이름으로 적게 하려고.
"$UNITY" -batchmode -nographics -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.SliceSelfCheck.DumpSceneRoster -logFile "$ROOT/unity/Logs/scene_roster.log"
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.FrameProbeBuild.Run -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$ROOT/builds/frameprobe/Ulon.app/Contents/MacOS/Ulon" -idlecheck -shotdir "$ROOT/builds/qa" -idleout "$ROOT/builds/qa/idle_check.md" -logFile "$ROOT/unity/Logs/idle_check.log"
cat "$ROOT/builds/qa/idle_check.md"
