#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Old dashboard exits first, then this helper starts the new dashboard on a free port."""
from __future__ import annotations

import os
import socket
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
LOG = Path.home() / "Library" / "Logs" / "AutoDevV2-HTML.log"
HOST = "127.0.0.1"


def pid_alive(pid: int) -> bool:
    if pid <= 1:
        return False
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def port_open(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.25):
            return True
    except OSError:
        return False


def log(text: str) -> None:
    try:
        LOG.parent.mkdir(parents=True, exist_ok=True)
        with LOG.open("a", encoding="utf-8") as f:
            f.write(time.strftime("%Y-%m-%d %H:%M:%S") + " [RESTART] " + text + "\n")
    except Exception:
        pass


def main() -> int:
    if len(sys.argv) < 4:
        return 2
    old_pid = int(sys.argv[1])
    port = int(sys.argv[2])
    resume = sys.argv[3] == "1"

    deadline = time.time() + 20
    while time.time() < deadline:
        if not pid_alive(old_pid) and not port_open(port):
            break
        time.sleep(0.15)
    else:
        log(f"old server/port did not release: pid={old_pid} port={port}")
        return 3

    env = os.environ.copy()
    env["AUTODEV_RESUME_ENGINE"] = "1" if resume else "0"
    env["AUTODEV_REFRESH_CODEX_USAGE"] = "1"
    env["PYTHONUNBUFFERED"] = "1"
    log(f"starting new dashboard, resume_engine={resume}")
    os.chdir(REPO)
    os.execve(sys.executable, [sys.executable, str(HERE / "webview_app.py")], env)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
