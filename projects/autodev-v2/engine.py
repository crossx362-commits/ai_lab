#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Single long-lived AutoDev engine process.

The dashboard starts only this file. Startup checks and runner execution happen
inside the same process so PID/state/heartbeat all describe one engine.
"""
from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
ENGINE_STATE = OUTPUT / "html_engine.json"
HEARTBEAT = OUTPUT / "engine_heartbeat.json"
CONTROL_FILES = (
    "engine.py", "runner_entry.py", "runner.py", "autodev.py",
    "functional_verify.py", "config.json", "start.py",
)
CONTROL_PROTOCOL = 7


def atomic_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def control_fingerprint() -> str:
    h = hashlib.sha256()
    for name in CONTROL_FILES:
        p = HERE / name
        h.update(name.encode())
        try:
            h.update(p.read_bytes())
        except OSError:
            h.update(b"MISSING")
    return h.hexdigest()[:16]


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
        "control_fingerprint": control_fingerprint(),
    })
    current.update(extra)
    atomic_json(ENGINE_STATE, current)


def run_startup() -> tuple[bool, str]:
    heartbeat("bootstrap", "이전 루프 정리 및 실행 준비 중")
    mig = subprocess.run([sys.executable, str(HERE / "migrate_v1.py"), "--apply"], cwd=REPO)
    if mig.returncode != 0:
        return False, f"v1 정리 실패 rc={mig.returncode}"

    import start
    env = start.compat_env()
    if env is None:
        return False, "Grok CLI를 찾지 못했습니다."
    os.environ.clear()
    os.environ.update(env)

    heartbeat("preflight", "AI CLI와 안전장치 확인 중")
    audit = subprocess.run([sys.executable, str(HERE / "preflight.py")], cwd=REPO, env=env)
    if audit.returncode != 0:
        return False, f"preflight 실패 rc={audit.returncode}"
    return True, ""


def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write_state(status="starting", exit_code=None)
    heartbeat("starting", "AutoDev 엔진 시작 중")
    rc = 1
    try:
        ok, reason = run_startup()
        if not ok:
            heartbeat("startup_failed", reason)
            rc = 2
            return rc

        write_state(status="running")
        heartbeat("starting", "Grok Director + Supervisor 시작")
        import runner_entry
        old_argv = sys.argv[:]
        sys.argv = [str(HERE / "engine.py"), "run", "--continuous"]
        try:
            rc = int(runner_entry.main())
        finally:
            sys.argv = old_argv
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
