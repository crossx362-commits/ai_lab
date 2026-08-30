#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Shared control-plane contract for AutoDev v2.

Dashboard, engine and runner heartbeat must import these values instead of
copying protocol/fingerprint rules independently.
"""
from __future__ import annotations

import hashlib
from pathlib import Path

CONTROL_PROTOCOL = 8
CONTROL_FILES = (
    "runtime_contract.py",
    "engine.py",
    "runner_entry.py",
    "runner.py",
    "autodev.py",
    "functional_verify.py",
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
