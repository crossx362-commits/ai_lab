#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Inspect and kill stale AutoDev processes.

Keeps the current dashboard / the caller. Does not touch Unity.
"""
from __future__ import annotations

import os
import re
import signal
import subprocess
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]

KEEP_NAMES = (
    "webview_app.py",
    "dashboard.html",
    "restart_server.py",
)

KILL_MARKERS = (
    "projects/autodev-v2/engine.py",
    "projects/autodev-v2/start.py",
    "projects/autodev-v2/runner.py",
    "projects/autodev-v2/runner_entry.py",
    "projects/autodev-v2/loop.py",
    "projects/autodev-v2/boot.py",
    "projects/autodev-v2/migrate_v1.py",
    "projects/autodev-v2/preflight.py",
    "autodev-v2/engine.py",
    "autodev-v2/runner_entry.py",
    "output/autodev_v2/runtime_bin/grok",
    "output/autodev_v2/runtime_bin/codex",
)

UNITY_MARKERS = ("Unity.app", "Unity", "UnityHub")


def _rows() -> list[tuple[int, int, str]]:
    try:
        r = subprocess.run(
            ["ps", "-Ao", "pid=,ppid=,command="],
            capture_output=True, text=True, timeout=5,
            encoding="utf-8", errors="replace",
        )
    except Exception:
        return []
    out: list[tuple[int, int, str]] = []
    for line in r.stdout.splitlines():
        m = re.match(r"\s*(\d+)\s+(\d+)\s+(.*)$", line)
        if m:
            out.append((int(m.group(1)), int(m.group(2)), m.group(3)))
    return out


def _norm(cmd: str) -> str:
    return cmd.replace("\\", "/")


def is_unity(cmd: str) -> bool:
    n = _norm(cmd)
    return any(x in n for x in UNITY_MARKERS) and "autodev-v2" not in n.lower()


def is_keep(cmd: str, pid: int, keep_pids: set[int]) -> bool:
    if pid in keep_pids:
        return True
    n = _norm(cmd)
    return any(name in n for name in KEEP_NAMES)


def is_target(cmd: str) -> bool:
    n = _norm(cmd)
    repo = str(REPO).replace("\\", "/")
    if is_unity(n):
        return False
    if any(m in n for m in KILL_MARKERS):
        return True
    if "autodev-v2" in n and any(name in n for name in KEEP_NAMES):
        return False
    if repo in n and "autodev-v2" in n and ("python" in n.lower() or "grok" in n.lower()):
        return True
    return False


def classify(keep_pids: set[int] | None = None) -> dict[str, list[dict[str, str | int]]]:
    keep_pids = set(keep_pids or [])
    keep_pids.update({os.getpid(), os.getppid()})
    stale: list[dict[str, str | int]] = []
    kept: list[dict[str, str | int]] = []
    for pid, ppid, cmd in _rows():
        row = {"pid": pid, "ppid": ppid, "cmd": cmd[:240]}
        if is_keep(cmd, pid, keep_pids):
            kept.append(row)
            continue
        if is_target(cmd):
            stale.append(row)
    return {"stale": stale, "kept": kept}


def _kill(pid: int, sig: int) -> None:
    try:
        if os.name != "nt":
            try:
                os.killpg(os.getpgid(pid), sig)
                return
            except Exception:
                pass
        os.kill(pid, sig)
    except Exception:
        pass


def reap(keep_pids: set[int] | None = None) -> dict[str, object]:
    found = classify(keep_pids)
    killed: list[int] = []
    for row in found["stale"]:
        pid = int(row["pid"])
        _kill(pid, signal.SIGTERM)
        killed.append(pid)
    if killed:
        time.sleep(0.4)
        alive = {pid for pid, _, _ in _rows()}
        for pid in killed:
            if pid in alive:
                _kill(pid, signal.SIGKILL)
        time.sleep(0.2)
    still = classify(keep_pids)["stale"]
    print("[REAP] stale %s · killed %s · remain %s" % (
        len(found["stale"]), killed, [x["pid"] for x in still]
    ), flush=True)
    return {
        "ok": not still,
        "found": found["stale"],
        "killed": killed,
        "remain": still,
    }


def main() -> int:
    result = reap()
    print("found=%s killed=%s remain=%s" % (
        len(result["found"]), result["killed"], [x["pid"] for x in result["remain"]]
    ))
    return 0 if result["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
