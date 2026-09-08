#!/bin/bash
# 프레임 시간 기준선 — 스탠드얼론을 빌드해 한 번 실행한다(창이 잠깐 뜬다).
# **매 랩 돌리지 마라.** 기준선을 갱신할 때만.
#   bash tools/frame_probe.sh            → Development 빌드, builds/qa/frame_times.md
#   bash tools/frame_probe.sh --release  → 릴리스 빌드,     builds/qa/frame_times_release.md
# 릴리스로도 재는 이유: Development 빌드는 프로파일러 훅을 달고 돌아 p95 튐이 부풀 수 있다.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
if [ "${1:-}" = "--release" ]; then
  METHOD=Ulon.Editor.FrameProbeBuild.RunRelease
  APP="$ROOT/builds/frameprobe_release/Ulon.app"
  OUT="$ROOT/builds/qa/frame_times_release.md"
else
  METHOD=Ulon.Editor.FrameProbeBuild.Run
  APP="$ROOT/builds/frameprobe/Ulon.app"
  OUT="$ROOT/builds/qa/frame_times.md"
fi
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod "$METHOD" -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$APP/Contents/MacOS/Ulon" -frameprobe -frameout "$OUT" -logFile "$ROOT/unity/Logs/frame_probe_run.log"
cat "$OUT"
