#!/usr/bin/env python3
"""Run a command in its own process group; kill the group when the deadline hits.

Exit 124 on timeout (GNU timeout convention). Usage:
    run_timeout.py <seconds> <cmd> [args...]
"""
from __future__ import annotations

import os
import signal
import subprocess
import sys
import time


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: run_timeout.py <seconds> <cmd> [args...]", file=sys.stderr)
        return 2
    try:
        timeout = float(sys.argv[1])
    except ValueError:
        print("timeout must be a number of seconds", file=sys.stderr)
        return 2
    cmd = sys.argv[2:]
    proc = subprocess.Popen(
        cmd,
        start_new_session=True,
        stdin=subprocess.DEVNULL,
    )
    deadline = time.time() + timeout
    while proc.poll() is None:
        if time.time() >= deadline:
            try:
                os.killpg(proc.pid, signal.SIGTERM)
            except ProcessLookupError:
                break
            try:
                proc.wait(timeout=8)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(proc.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                proc.wait()
            return 124
        time.sleep(1)
    return int(proc.returncode or 0)


if __name__ == "__main__":
    raise SystemExit(main())
