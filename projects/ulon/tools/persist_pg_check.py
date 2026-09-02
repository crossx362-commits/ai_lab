#!/usr/bin/env python3
"""Save character/skills/inventory to Postgres, restart persist, reload."""
from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PERSIST = ROOT / "server" / "persist.py"
PORT = "8777"
BASE = f"http://127.0.0.1:{PORT}"
ACCOUNT = "pg-reconnect"
URL = "postgresql://ulon@127.0.0.1:5432/ulon"


def http(method: str, path: str, body: dict | None = None) -> tuple[int, dict]:
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=3) as resp:
            return resp.status, json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        raw = e.read().decode() if e.fp else "{}"
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, {"raw": raw}


def wait_health(driver: str, timeout: float = 8.0) -> dict:
    deadline = time.time() + timeout
    last = {}
    while time.time() < deadline:
        try:
            code, body = http("GET", "/health")
            if code == 200 and body.get("driver") == driver:
                return body
            last = body
        except Exception as e:
            last = {"error": str(e)}
        time.sleep(0.15)
    raise SystemExit(f"health wait failed {last}")


def start_persist() -> subprocess.Popen:
    env = os.environ.copy()
    env["DATABASE_URL"] = URL
    env["ULON_PERSIST_PORT"] = PORT
    py = ROOT / "server" / ".venv" / "bin" / "python"
    exe = str(py) if py.exists() else sys.executable
    return subprocess.Popen(
        [exe, str(PERSIST)],
        cwd=str(PERSIST.parent),
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )


def stop(pid: int | None) -> None:
    if not pid:
        return
    try:
        os.kill(pid, signal.SIGTERM)
    except OSError:
        return
    for _ in range(20):
        try:
            os.kill(pid, 0)
            time.sleep(0.1)
        except OSError:
            return


def main() -> None:
    snap = {
        "AccountId": ACCOUNT,
        "CharacterId": ACCOUNT,
        "Name": "검사",
        "X": 4.5,
        "Y": 0.1,
        "Z": 3.2,
        "Hp": 41,
        "Skills": [
            {"Id": 0, "Value": 1.5, "Lock": 0},
            {"Id": 1, "Value": 0.4, "Lock": 0},
            {"Id": 2, "Value": 0.2, "Lock": 0},
        ],
        "Inventory": [
            {"Slot": 0, "TemplateId": "iron_ore", "Amount": 3},
            {"Slot": 1, "TemplateId": "iron_sword", "Amount": 1},
        ],
    }

    # Replace whatever is on 8777 so this check owns the port.
    try:
        code, body = http("GET", "/health")
        if code == 200:
            subprocess.run(["pkill", "-f", "projects/ulon/server/persist.py"], check=False)
            time.sleep(0.4)
    except Exception:
        pass

    proc = start_persist()
    try:
        health = wait_health("postgres")
        print("health", health)
        code, saved = http("PUT", f"/character/{ACCOUNT}", snap)
        if code != 200:
            raise SystemExit(f"PUT failed {code} {saved}")
        if saved.get("Hp") != 41 or saved.get("Name") != "검사":
            raise SystemExit(f"PUT body mismatch {saved}")
        skills = {s["Id"]: s["Value"] for s in saved.get("Skills", [])}
        inv = {i["TemplateId"]: i["Amount"] for i in saved.get("Inventory", [])}
        if skills.get(0) != 1.5 or inv.get("iron_sword") != 1 or inv.get("iron_ore") != 3:
            raise SystemExit(f"PUT skills/inv mismatch {saved}")

        stop(proc.pid)
        proc.wait(timeout=3)
        proc = start_persist()
        wait_health("postgres")
        code, loaded = http("GET", f"/character/{ACCOUNT}")
        if code != 200:
            raise SystemExit(f"GET after restart failed {code} {loaded}")
        skills = {s["Id"]: s["Value"] for s in loaded.get("Skills", [])}
        inv = {i["TemplateId"]: i["Amount"] for i in loaded.get("Inventory", [])}
        ok = (
            loaded.get("Name") == "검사"
            and loaded.get("Hp") == 41
            and abs(loaded.get("X", 0) - 4.5) < 0.01
            and skills.get(0) == 1.5
            and skills.get(1) == 0.4
            and inv.get("iron_ore") == 3
            and inv.get("iron_sword") == 1
        )
        print("reloaded", json.dumps(loaded, ensure_ascii=False))
        if not ok:
            raise SystemExit("reconnect restore mismatch")
        print("PASS postgres reconnect restore")
    finally:
        stop(proc.pid if proc else None)


if __name__ == "__main__":
    main()
