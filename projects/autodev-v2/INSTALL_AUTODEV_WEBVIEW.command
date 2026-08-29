#!/bin/zsh
set -e
ROOT="$HOME/ai_lab"
if [ ! -d "$ROOT/.git" ]; then
  osascript -e 'display dialog "홈 폴더에 ~/ai_lab 저장소가 없습니다." buttons {"확인"} default button "확인" with icon stop'
  exit 1
fi
cd "$ROOT"
/usr/bin/git pull --ff-only || true
/usr/bin/python3 "$ROOT/projects/autodev-v2/install_mac_webview.py"
