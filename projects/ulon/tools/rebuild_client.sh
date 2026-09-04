#!/bin/zsh
set -euo pipefail
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
exec "$UNITY" -batchmode -nographics -quit \
  -projectPath /Users/junholee/ai_lab/projects/ulon/unity \
  -executeMethod Ulon.Editor.DedicatedServer.BuildBoth \
  -logFile /Users/junholee/ai_lab/projects/ulon/builds/two_client_rebuild3.log
