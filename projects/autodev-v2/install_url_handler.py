#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Register autodev://open on macOS without third-party packages.

Creates a tiny local app in ~/Applications. The app is not a background daemon.
macOS launches it only when the user opens autodev://open, then it starts the
localhost HTML dashboard if necessary and opens the dashboard in the browser.
"""
from __future__ import annotations

import os
import plistlib
import shutil
import stat
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
APP = Path.home() / "Applications" / "AutoDev URL Launcher.app"
CONTENTS = APP / "Contents"
MACOS = CONTENTS / "MacOS"
LAUNCHER = MACOS / "AutoDev URL Launcher"
BUNDLE_ID = "com.ailab.autodev-url-launcher"
URL = "http://127.0.0.1:8765/"


def q(s: str) -> str:
    import shlex
    return shlex.quote(s)


def write_launcher() -> None:
    if APP.exists():
        shutil.rmtree(APP)
    MACOS.mkdir(parents=True, exist_ok=True)

    script = f'''#!/bin/zsh
set -u
ROOT={q(str(REPO))}
LOG="$HOME/Library/Logs/AutoDevV2-HTML.log"
URL={q(URL)}
export PATH="$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$PATH"
PY="$(command -v python3 || true)"
if [ -z "$PY" ]; then exit 1; fi

# If the dashboard is already alive, just open it.
if /usr/bin/curl -fsS --max-time 1 "$URL" >/dev/null 2>&1; then
  /usr/bin/open "$URL"
  exit 0
fi

# No auto-update here. The HTML dashboard has an explicit Update button.
nohup "$PY" "$ROOT/projects/autodev-v2/webview_app.py" >>"$LOG" 2>&1 </dev/null &
for i in {{1..40}}; do
  if /usr/bin/curl -fsS --max-time 1 "$URL" >/dev/null 2>&1; then
    /usr/bin/open "$URL"
    exit 0
  fi
  sleep 0.2
done
exit 1
'''
    LAUNCHER.write_text(script, encoding="utf-8")
    LAUNCHER.chmod(LAUNCHER.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

    info = {
        "CFBundleName": "AutoDev URL Launcher",
        "CFBundleDisplayName": "AutoDev URL Launcher",
        "CFBundleIdentifier": BUNDLE_ID,
        "CFBundleVersion": "2.1",
        "CFBundleShortVersionString": "2.1",
        "CFBundlePackageType": "APPL",
        "CFBundleExecutable": "AutoDev URL Launcher",
        "LSUIElement": True,
        "LSMinimumSystemVersion": "12.0",
        "CFBundleURLTypes": [
            {
                "CFBundleURLName": "AutoDev v2",
                "CFBundleURLSchemes": ["autodev"],
                "CFBundleTypeRole": "Viewer",
            }
        ],
    }
    with (CONTENTS / "Info.plist").open("wb") as f:
        plistlib.dump(info, f)


def register() -> None:
    lsregister_candidates = [
        "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
    ]
    for p in lsregister_candidates:
        if Path(p).exists():
            subprocess.run([p, "-f", str(APP)], check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            break


def main() -> int:
    if sys.platform != "darwin":
        print("macOS에서만 사용할 수 있습니다.")
        return 2
    try:
        write_launcher()
        register()
        print(f"등록 완료: {APP}")
        print("즐겨찾기 주소: autodev://open")
        print("런처는 상시 실행되지 않습니다. autodev://open을 눌렀을 때만 실행됩니다.")
        return 0
    except Exception as e:
        print(f"등록 실패: {type(e).__name__}: {e}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
