#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Compatibility wrapper for the old Pulse path.

The market-intelligence agent is now Signal (시그널_분석가). This file remains
only so old callers do not resurrect the removed Pulse implementation.
"""

from __future__ import annotations

import runpy
from pathlib import Path

SIGNAL_SCRIPT = Path(__file__).resolve().parents[2] / "시그널_분석가" / "tools" / "market_signal.py"

if __name__ == "__main__":
    runpy.run_path(str(SIGNAL_SCRIPT), run_name="__main__")
