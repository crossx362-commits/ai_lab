#!/usr/bin/env python3
"""Save character/skills/inventory to Postgres, restart persist, reload."""
from __future__ import annotations

import hashlib
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


def source_sha256() -> str:
    return hashlib.sha256(PERSIST.read_bytes()).hexdigest()


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
            if (
                code == 200
                and body.get("driver") == driver
                and body.get("source_sha256") == source_sha256()
            ):
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
    log = ROOT / "data" / "persist.log"
    log.parent.mkdir(parents=True, exist_ok=True)
    fh = open(log, "a", encoding="utf-8")
    return subprocess.Popen(
        [exe, str(PERSIST)],
        cwd=str(PERSIST.parent),
        env=env,
        stdout=fh,
        stderr=subprocess.STDOUT,
        start_new_session=True,
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
            {"Slot": 1, "TemplateId": "iron_sword", "Amount": 1, "Uses": 12, "MakerId": "pg-reconnect"},
            {"Slot": 2, "TemplateId": "warden_crest", "Amount": 1, "Uses": 0, "MakerId": ""},
            {"Slot": 3, "TemplateId": "captain_sigil", "Amount": 1, "Uses": 0, "MakerId": ""},
            {"Slot": 4, "TemplateId": "hex_seal", "Amount": 1, "Uses": 5, "MakerId": "hexarc"},
        ],
    }

    # Reuse postgres persist on 8777. Restart only when missing or not postgres.
    proc = None
    try:
        code, body = http("GET", "/health")
        if (
            code != 200
            or body.get("driver") != "postgres"
            or body.get("source_sha256") != source_sha256()
        ):
            raise RuntimeError("need start")
        print("reuse persist", body)
    except Exception:
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
        inv = {i["TemplateId"]: i for i in saved.get("Inventory", [])}
        if skills.get(0) != 1.5 or inv.get("iron_sword", {}).get("Amount") != 1 or inv.get("iron_ore", {}).get("Amount") != 3:
            raise SystemExit(f"PUT skills/inv mismatch {saved}")
        if (
            inv.get("iron_sword", {}).get("Uses") != 12
            or inv.get("iron_sword", {}).get("MakerId") != "pg-reconnect"
            or inv.get("warden_crest", {}).get("Amount") != 1
            or inv.get("captain_sigil", {}).get("Amount") != 1
            or inv.get("hex_seal", {}).get("Amount") != 1
            or inv.get("hex_seal", {}).get("Uses") != 5
            or inv.get("hex_seal", {}).get("MakerId") != "hexarc"
        ):
            raise SystemExit(f"PUT new items/uses/maker mismatch {saved}")

        # Reconnect = persist process restart; schema already has uses/maker_id.
        if proc is not None:
            stop(proc.pid)
            try:
                proc.wait(timeout=3)
            except Exception:
                pass
        else:
            subprocess.run(["pkill", "-f", "projects/ulon/server/persist.py"], check=False)
            time.sleep(0.4)
        proc = start_persist()
        wait_health("postgres")
        code, loaded = http("GET", f"/character/{ACCOUNT}")
        if code != 200:
            raise SystemExit(f"GET after restart failed {code} {loaded}")
        skills = {s["Id"]: s["Value"] for s in loaded.get("Skills", [])}
        inv = {i["TemplateId"]: i for i in loaded.get("Inventory", [])}
        ok = (
            loaded.get("Name") == "검사"
            and loaded.get("Hp") == 41
            and abs(loaded.get("X", 0) - 4.5) < 0.01
            and skills.get(0) == 1.5
            and skills.get(1) == 0.4
            and inv.get("iron_ore", {}).get("Amount") == 3
            and inv.get("iron_sword", {}).get("Amount") == 1
            and inv.get("iron_sword", {}).get("Uses") == 12
            and inv.get("iron_sword", {}).get("MakerId") == "pg-reconnect"
            and inv.get("warden_crest", {}).get("Amount") == 1
            and inv.get("captain_sigil", {}).get("Amount") == 1
            and inv.get("hex_seal", {}).get("Amount") == 1
            and inv.get("hex_seal", {}).get("Uses") == 5
            and inv.get("hex_seal", {}).get("MakerId") == "hexarc"
        )
        print("reloaded", json.dumps(loaded, ensure_ascii=False))
        if not ok:
            raise SystemExit("reconnect restore mismatch")
        print("PASS postgres reconnect restore items+maker")
        # Leave postgres persist running for reuse.
        proc = None
    finally:
        if proc is not None:
            stop(proc.pid)


if __name__ == "__main__":
    main()
