#!/usr/bin/env python3
"""Lightweight repo/runtime harness for ai-team."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from datetime import datetime
from pathlib import Path


THIS = Path(__file__).resolve()
AI_TEAM = THIS.parents[1]
ROOT = AI_TEAM.parents[1]

sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(AI_TEAM))


def ok(msg: str) -> tuple[str, str]:
    return "OK", msg


def warn(msg: str) -> tuple[str, str]:
    return "WARN", msg


def fail(msg: str) -> tuple[str, str]:
    return "FAIL", msg


def read_json(path: Path):
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def age_text(path: Path) -> str:
    if not path.exists():
        return "missing"
    dt = datetime.fromtimestamp(path.stat().st_mtime)
    return dt.strftime("%m/%d %H:%M")


def find_python_pids(script_name: str) -> list[str]:
    cmd = (
        "Get-CimInstance Win32_Process | "
        "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
        f"$_.CommandLine.ToLower().Contains('{script_name.lower()}') }} | "
        "Select-Object -ExpandProperty ProcessId"
    )
    try:
        out = subprocess.run(
            ["powershell", "-NoProfile", "-Command", cmd],
            capture_output=True,
            text=True,
            timeout=5,
        ).stdout
        return [p for p in out.split() if p.isdigit()]
    except Exception:
        return []


def check_env():
    try:
        from _shared.env_loader import load_env

        load_env()
    except Exception as e:
        return fail(f"load_env failed: {e}")

    required = ["TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID", "GEMINI_API_KEY", "UPBIT_ACCESS_KEY", "UPBIT_SECRET_KEY"]
    missing = [k for k in required if not os.getenv(k)]
    if missing:
        return warn("missing env: " + ", ".join(missing))
    return ok("central env loaded")


def check_runtime():
    agents = {
        "youngsuk": "telegram_receiver.py",
        "hyunbin": "crypto_market_intelligence.py",
        "dave": "upbit_auto_trader.py",
        "leo": "leo_aggressive_trader.py",
    }
    parts = []
    for name, script in agents.items():
        pids = find_python_pids(script)
        parts.append(f"{name}={','.join(pids) if pids else 'down'}")
    down = [p for p in parts if p.endswith("down")]
    return (warn if down else ok)("; ".join(parts))


def check_schedule():
    base = AI_TEAM / "skills" / "영숙_비서" / "tools"
    schedules = base / "schedules.json"
    last_run = base / "last_run.json"
    try:
        data = read_json(schedules)
        items = data.get("schedules", [])
        enabled = [s for s in items if s.get("enabled", True)]
        read_json(last_run)
    except Exception as e:
        return fail(f"schedule json failed: {e}")
    return ok(f"enabled {len(enabled)}/{len(items)}, last_run {age_text(last_run)}")


def check_trading():
    intel = AI_TEAM / "reports" / "research" / "crypto_market_intel.json"
    dave_log = ROOT / "output" / "trading_logs" / "dave_daemon.out.log"
    leo_log = ROOT / "output" / "trading_logs" / "leo_daemon.out.log"
    if not intel.exists():
        return warn("missing crypto_market_intel.json")
    return ok(f"intel {age_text(intel)}, dave_log {age_text(dave_log)}, leo_log {age_text(leo_log)}")


def check_structure():
    required = [AI_TEAM / "_shared", AI_TEAM / "skills", AI_TEAM / "scripts", ROOT / "reports", ROOT / "output"]
    missing = [str(p.relative_to(ROOT)) for p in required if not p.exists()]
    if missing:
        return fail("missing dirs: " + ", ".join(missing))
    return ok("core dirs present")


def main() -> int:
    checks = {
        "env": check_env,
        "runtime": check_runtime,
        "schedule": check_schedule,
        "trading": check_trading,
        "structure": check_structure,
    }
    worst = 0
    for name, fn in checks.items():
        status, msg = fn()
        worst = max(worst, {"OK": 0, "WARN": 1, "FAIL": 2}[status])
        print(f"[{status}] {name}: {msg}")
    return 1 if worst == 2 else 0


if __name__ == "__main__":
    raise SystemExit(main())
