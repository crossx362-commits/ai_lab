#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Install a local AutoDev v2 WebView app into ~/Applications.

The .app is created locally, so future use is a normal Finder/Dock click.
A dedicated venv keeps pywebview away from system Python.
"""
from __future__ import annotations

import plistlib
import shutil
import subprocess
import sys
import venv
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
VENV = REPO / "output" / "autodev_v2" / "webview_venv"
APP = Path.home() / "Applications" / "AutoDev v2.app"
CONTENTS = APP / "Contents"
MACOS = CONTENTS / "MacOS"


def shlex_quote(value: str) -> str:
    import shlex
    return shlex.quote(value)


def run(cmd: list[str]) -> None:
    print("$", " ".join(cmd))
    subprocess.run(cmd, check=True)


def ensure_venv() -> Path:
    py = VENV / "bin" / "python"
    if not py.exists():
        VENV.parent.mkdir(parents=True, exist_ok=True)
        print("WebView 전용 Python 환경 생성 중...")
        venv.EnvBuilder(with_pip=True, clear=False).create(VENV)
    print("pywebview 확인/설치 중...")
    run([str(py), "-m", "pip", "install", "--disable-pip-version-check", "--quiet", "pywebview>=5,<7"])
    return py


def write_app(py: Path) -> None:
    if APP.exists():
        shutil.rmtree(APP)
    MACOS.mkdir(parents=True, exist_ok=True)

    launcher = MACOS / "AutoDev v2"
    launcher.write_text(
        "#!/bin/zsh\n"
        "export PATH=\"$HOME/.local/bin:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$PATH\"\n"
        f"exec {shlex_quote(str(py))} {shlex_quote(str(HERE / 'webview_app.py'))} "
        ">> \"$HOME/Library/Logs/AutoDevV2-WebView.log\" 2>&1\n",
        encoding="utf-8",
    )
    launcher.chmod(0o755)

    info = {
        "CFBundleName": "AutoDev v2",
        "CFBundleDisplayName": "AutoDev v2",
        "CFBundleIdentifier": "com.ailab.autodev-v2",
        "CFBundleVersion": "2.0",
        "CFBundleShortVersionString": "2.0",
        "CFBundlePackageType": "APPL",
        "CFBundleExecutable": "AutoDev v2",
        "LSMinimumSystemVersion": "12.0",
        "NSHighResolutionCapable": True,
        "NSRequiresAquaSystemAppearance": False,
    }
    with (CONTENTS / "Info.plist").open("wb") as f:
        plistlib.dump(info, f)


def main() -> int:
    if sys.platform != "darwin":
        print("macOS에서만 설치할 수 있습니다.")
        return 2
    try:
        py = ensure_venv()
        write_app(py)
        print(f"\n설치 완료: {APP}")
        print("이제 터미널 명령 없이 응용 프로그램에서 AutoDev v2를 실행하시면 됩니다.")
        subprocess.Popen(["open", str(APP)])
        return 0
    except subprocess.CalledProcessError as e:
        print(f"설치 실패 rc={e.returncode}")
        return e.returncode or 1
    except Exception as e:
        print(f"설치 실패: {type(e).__name__}: {e}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
