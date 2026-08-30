#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Shared control-plane contract for AutoDev v2."""
from __future__ import annotations

import hashlib
from pathlib import Path

CONTROL_PROTOCOL = 9
CONTROL_FILES = (
    "runtime_contract.py",
    "engine.py",
    "boot.py",
    "loop.py",
    "procutil.py",
    "webview_app.py",
    "config.json",
    "start.py",
)


def control_fingerprint(base: Path) -> str:
    h = hashlib.sha256()
    for name in CONTROL_FILES:
        p = base / name
        h.update(name.encode("utf-8"))
        try:
            h.update(p.read_bytes())
        except OSError:
            h.update(b"MISSING")
    return h.hexdigest()[:16]
