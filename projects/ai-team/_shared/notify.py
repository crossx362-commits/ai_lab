"""Telegram notification and current ai-team daemon status helpers."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.request
from datetime import datetime


ACTIVE_DAEMONS = {
    "youngsuk": "telegram_receiver.py",
    "youngsuk_schedule": "schedule_manager.py",
    "somi": "somi_kis_reporter.py",
    "yewon_monitor": "harness_monitor.py",
}


def send(msg: str, silent: bool = False) -> bool:
    """Send a Telegram message when credentials are configured."""
    if os.getenv("SUPPRESS_TELEGRAM") == "true":
        print(f"[Telegram suppressed] {msg[:100]}")
        return True

    token = os.getenv("TELEGRAM_BOT_TOKEN")
    chat_id = os.getenv("TELEGRAM_CHAT_ID")
    if not token or not chat_id:
        print("[Telegram] Missing TELEGRAM_BOT_TOKEN or TELEGRAM_CHAT_ID")
        return False

    try:
        payload = json.dumps(
            {
                "chat_id": chat_id,
                "text": msg[:4096],
                "parse_mode": "HTML",
                "disable_notification": silent,
            }
        ).encode("utf-8")
        req = urllib.request.Request(
            f"https://api.telegram.org/bot{token}/sendMessage",
            data=payload,
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=10) as response:
            result = json.loads(response.read().decode("utf-8"))
        if result.get("ok"):
            print(f"[Telegram] Sent {len(msg)} chars")
            return True
        print(f"[Telegram] API error: {result}")
        return False
    except Exception as exc:
        print(f"[Telegram] {exc}")
        return False


def _find_pids(script_name: str) -> list[str]:
    script_file = script_name.lower()
    if sys.platform == "win32":
        ps = (
            "Get-CimInstance Win32_Process | "
            "Where-Object { $_.Name -match '^python' -and $_.CommandLine -and "
            f"$_.CommandLine.ToLower().Contains('{script_file}') }} | "
            "Select-Object -ExpandProperty ProcessId"
        )
        try:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command", ps],
                capture_output=True,
                text=True,
                timeout=5,
                creationflags=subprocess.CREATE_NO_WINDOW,
            )
            return [pid for pid in result.stdout.split() if pid.isdigit()]
        except Exception:
            return []

    try:
        result = subprocess.run(["pgrep", "-f", script_file], capture_output=True, text=True, timeout=5)
        return [pid for pid in result.stdout.split() if pid.isdigit()]
    except Exception:
        return []


def agent_status() -> dict[str, str]:
    return {
        name: ",".join(pids) if (pids := _find_pids(script)) else "down"
        for name, script in ACTIVE_DAEMONS.items()
    }


def status_report() -> str:
    lines = [f"Agent Status ({datetime.now().strftime('%Y-%m-%d %H:%M')})"]
    for name, state in agent_status().items():
        marker = "UP" if state != "down" else "DOWN"
        lines.append(f"- {name}: {marker} {state}")
    return "\n".join(lines)


def report(agent: str, action: str, detail: str = "") -> None:
    msg = f"[{agent}] {action}"
    if detail:
        msg += f"\n{detail}"
    send(msg, silent=True)


telegram = send
status = agent_status
