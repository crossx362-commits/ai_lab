#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
exec "$UNITY" -batchmode -nographics -quit \
  -projectPath $ROOT/unity \
  -executeMethod Ulon.Editor.DedicatedServer.BuildBoth \
  -logFile $ROOT/builds/two_client_rebuild3.log
