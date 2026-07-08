#!/usr/bin/env python3
"""Terminate duplicate ai-team Python daemon processes.

Keeps the oldest process for each known daemon script and stops later duplicates.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


AI_TEAM = Path(__file__).resolve().parents[1]
ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.notify import CONTINUOUS_DAEMONS, send  # noqa: E402


def _python_processes() -> list[dict]:
    if sys.platform != "win32":
        result = subprocess.run(
            ["ps", "-axo", "pid=,lstart=,command="],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=10,
        )
        rows = []
        for line in result.stdout.splitlines():
            parts = line.strip().split(maxsplit=6)
            if len(parts) < 7:
                continue
            pid, command = parts[0], parts[6]
            if pid.isdigit() and "python" in command.lower():
                rows.append({"pid": int(pid), "command": command, "start": " ".join(parts[1:6])})
        return rows

    ps = (
        "Get-CimInstance Win32_Process | "
        "Where-Object { $_.Name -match '^python' -and $_.CommandLine } | "
        "Select-Object ProcessId,CreationDate,CommandLine | ConvertTo-Json -Compress"
    )
    result = subprocess.run(
        ["powershell", "-NoProfile", "-Command", ps],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=10,
    )
    if not result.stdout.strip():
        return []
    import json

    data = json.loads(result.stdout)
    if isinstance(data, dict):
        data = [data]
    return [
        {
            "pid": int(row["ProcessId"]),
            "command": row.get("CommandLine") or "",
            "start": row.get("CreationDate") or "",
        }
        for row in data
    ]


def find_duplicates() -> list[tuple[str, int, str]]:
    rows = _python_processes()
    removed: list[tuple[str, int, str]] = []
    for name, script in CONTINUOUS_DAEMONS.items():
        matches = [row for row in rows if script.lower() in row["command"].lower()]
        if len(matches) <= 1:
            continue
        matches.sort(key=lambda row: str(row.get("start") or ""))
        for row in matches[1:]:
            removed.append((name, row["pid"], "duplicate"))
    return removed


def terminate(pid: int) -> str:
    if sys.platform == "win32":
        result = subprocess.run(["taskkill", "/F", "/PID", str(pid)], capture_output=True, text=True)
    else:
        result = subprocess.run(["kill", str(pid)], capture_output=True, text=True)
    return "terminated" if result.returncode == 0 else "failed"


def format_removed_message(removed: list[tuple[str, int, str]]) -> str | None:
    if not removed:
        return None
    quiet_labels = ("시그널", "signal", "market_signal")
    visible = [item for item in removed if not any(q in item[0].lower() for q in quiet_labels)]
    if not visible:
        return None
    lines = ["중복 프로세스 정리 완료"]
    lines.extend(f"- {label}: pid {pid} {status}" for label, pid, status in visible)
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--notify", action="store_true")
    args = parser.parse_args()

    duplicates = find_duplicates()
    results = []
    for label, pid, _ in duplicates:
        status = "dry-run" if args.dry_run else terminate(pid)
        results.append((label, pid, status))

    message = format_removed_message(results)
    if message:
        print(message)
        if args.notify:
            send(message, silent=True)
    elif not results:
        print("No duplicate ai-team daemon processes found.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
