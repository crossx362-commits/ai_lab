#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Register autodev://open on macOS without third-party packages.

Creates a tiny local app in ~/Applications. The app is not a background daemon.
macOS launches it only when the user opens autodev://open, then it starts the
localhost HTML dashboard if necessary and opens the authenticated dashboard URL.
"""
from __future__ import annotations

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
BASE_URL = "http://127.0.0.1:8765/"


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
BASE_URL={q(BASE_URL)}
STATE="$ROOT/output/autodev_v2/html_server.json"
export PATH="$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$PATH"
PY="$(command -v python3 || true)"
if [ -z "$PY" ]; then exit 1; fi

open_dashboard() {{
  TARGET="$($PY - "$STATE" <<'PYCODE'
import json, sys, urllib.parse
from pathlib import Path
p = Path(sys.argv[1])
try:
    d = json.loads(p.read_text(encoding="utf-8"))
    port = int(d.get("port", 8765) or 8765)
    token = str(d.get("token", ""))
except Exception:
    port, token = 8765, ""
url = f"http://127.0.0.1:{{port}}/"
if token:
    url += "?token=" + urllib.parse.quote(token)
print(url)
PYCODE
)"
  /usr/bin/open "$TARGET"
}}

# Already alive: open the authenticated URL from html_server.json.
if /usr/bin/curl -fsS --max-time 1 "$BASE_URL" >/dev/null 2>&1; then
  open_dashboard
  exit 0
fi

# Not running: start only on demand. webview_app.py opens the tokenized browser URL itself.
nohup "$PY" "$ROOT/projects/autodev-v2/webview_app.py" >>"$LOG" 2>&1 </dev/null &

# If its own browser-open is delayed/blocked, fall back to opening after readiness.
for i in {{1..40}}; do
  if /usr/bin/curl -fsS --max-time 1 "$BASE_URL" >/dev/null 2>&1; then
    sleep 0.6
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
        "CFBundleVersion": "2.2",
        "CFBundleShortVersionString": "2.2",
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
    candidates = [
        "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/LaunchServices.framework/Support/lsregister",
    ]
    for p in candidates:
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
        print("상시 실행 없음. 즐겨찾기를 눌렀을 때만 AutoDev HTML 서버가 시작됩니다.")
        return 0
    except Exception as e:
        print(f"등록 실패: {type(e).__name__}: {e}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
