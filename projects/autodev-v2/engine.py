#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Single long-lived AutoDev engine process.

Clean boot: no v1 migrate, no launchd sync. Dashboard starts only this file.
"""
from __future__ import annotations

import json
import os
import time
from pathlib import Path

from runtime_contract import CONTROL_PROTOCOL, control_fingerprint

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
ENGINE_STATE = OUTPUT / "html_engine.json"
HEARTBEAT = OUTPUT / "engine_heartbeat.json"


def atomic_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def heartbeat(stage: str, message: str) -> None:
    atomic_json(HEARTBEAT, {
        "heartbeat_at": time.time(),
        "last_output_at": time.time(),
        "pid": os.getpid(),
        "engine_protocol": CONTROL_PROTOCOL,
        "stage": stage,
        "message": message,
        "provider": "local",
    })


def write_state(**extra) -> None:
    current = {}
    try:
        current = json.loads(ENGINE_STATE.read_text(encoding="utf-8"))
        if not isinstance(current, dict):
            current = {}
    except Exception:
        current = {}
    current.update({
        "pid": os.getpid(),
        "started_at": current.get("started_at") or time.time(),
        "control_protocol": CONTROL_PROTOCOL,
        "control_fingerprint": control_fingerprint(HERE),
    })
    current.update(extra)
    atomic_json(ENGINE_STATE, current)


def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write_state(status="starting", exit_code=None)
    heartbeat("starting", "AutoDev 엔진 시작 중")
    rc = 1
    try:
        import boot
        heartbeat("bootstrap", "Grok 루프 준비 중")
        ok, reason = boot.prepare()
        if not ok:
            heartbeat("startup_failed", reason)
            print(f"[ENGINE] startup failed: {reason}", flush=True)
            rc = 2
            return rc
        write_state(status="running")
        heartbeat("starting", "Grok Director + Supervisor 시작")
        rc = int(boot.run_supervisor())
        return rc
    except KeyboardInterrupt:
        rc = 130
        return rc
    except BaseException as e:
        try:
            heartbeat("crashed", f"{type(e).__name__}: {e}")
        except Exception:
            pass
        raise
    finally:
        try:
            write_state(status="stopped", exited_at=time.time(), exit_code=rc)
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
