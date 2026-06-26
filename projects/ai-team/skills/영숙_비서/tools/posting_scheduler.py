"""
Youngsuk posting schedule helper.

This file is kept as a compatibility entry point for harness checks and older
schedule commands. The current ai-team has no active content-upload agents, so
it only reports that there is nothing to register.
"""

from __future__ import annotations

import datetime as _dt
import sys


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def run_schedule(target_date: _dt.date | None = None) -> int:
    date = target_date or _dt.date.today()
    print(f"[Youngsuk] {date.isoformat()} has no active upload posting schedule.")
    return 0


def register_recurring_schedule() -> int:
    print("[Youngsuk] No recurring upload schedule is registered for the current agent roster.")
    return 0


if __name__ == "__main__":
    if "--register-recurring" in sys.argv:
        raise SystemExit(register_recurring_schedule())
    raise SystemExit(run_schedule())
