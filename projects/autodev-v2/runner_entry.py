#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 execution entrypoint.

Adds two runtime integrations on top of runner.py:
- Codex fallback output is streamed live.
- Gameplay/system tasks must pass task-specific Unity acceptance verification.
"""
from __future__ import annotations

import os
import shutil
import tempfile
import time
from pathlib import Path
from typing import Any

import functional_verify as FV
import runner

AUTODEV = runner.AUTODEV
ORIGINAL_WORKER_PROMPT = AUTODEV.worker_prompt
ORIGINAL_VERIFY_TASK = AUTODEV.verify_task
ORIGINAL_SAFE_EXECUTE = runner.safe_execute_one
ORIGINAL_NEXT_READY = runner.next_ready
ORIGINAL_DIRECTOR_FILL = runner.director_fill
_ACTIVE_CHECKPOINTS: dict[str, dict[str, Any]] = {}


def codex_call(cfg: dict[str, Any], st: dict[str, Any], prompt: str, cwd: Path) -> tuple[int, str]:
    exe = shutil.which("codex")
    if not exe:
        return 127, "codex CLI를 찾을 수 없습니다."

    fd, outpath = tempfile.mkstemp(prefix="autodev_v2_codex_", suffix=".txt")
    os.close(fd)
    try:
        cmd = [exe, "exec", "--skip-git-repo-check", "-o", outpath, prompt]
        st["stats"]["codex_calls"] = int(st["stats"].get("codex_calls", 0)) + 1
        rc, streamed = AUTODEV.stream_process(
            cmd,
            cwd,
            timeout=900,
            env=AUTODEV.subscription_env(cfg, "codex"),
            tag="CODEX:fallback",
        )
        try:
            body = Path(outpath).read_text(encoding="utf-8", errors="replace").strip()
        except Exception:
            body = ""
        return rc, body or streamed
    finally:
        try:
            os.unlink(outpath)
        except OSError:
            pass


def worker_prompt(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> str:
    return ORIGINAL_WORKER_PROMPT(cfg, task, verify_text) + FV.worker_instructions(cfg, task)


def verify_task(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[str, str]:
    status, base = ORIGINAL_VERIFY_TASK(cfg, task)
    if status != "pass":
        return status, base
    if not FV.requires_functional(cfg, task):
        return status, base

    cp = _ACTIVE_CHECKPOINTS.get(str(task.get("id", "")))
    delta = runner.task_delta_paths(Path(cfg["_repo_root"]), cp) if cp else None
    fstatus, functional = FV.verify_functional(cfg, task, delta_paths=delta)
    joined = (base.rstrip() + "\n\n" + functional.rstrip()).strip()
    return fstatus, joined


def _mark_verification_wait(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], reason: str) -> None:
    retry = max(30, int(cfg.get("functional_verify_wait_seconds", 120)))
    task["status"] = "waiting_verification"
    task["verification_retry_at"] = time.time() + retry
    task["last_error"] = "Unity 실제 기능 검증 대기: " + reason[-2400:]
    task["wait_reason"] = task["last_error"]
    AUTODEV.save_state(cfg, st)
    print(f"[FUNCTIONAL] {task.get('id')} · {retry}초 후 다시 확인 · {reason[:700]}")


def safe_execute_one(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], run_stats: dict[str, int]) -> str:
    needs = FV.requires_functional(cfg, task)
    if needs:
        ready, reason = FV.environment_ready(cfg)
        if not ready:
            _mark_verification_wait(cfg, st, task, reason)
            return "waiting_verification"

    root = Path(cfg["_repo_root"])
    outer_cp = runner.checkpoint(root)
    tid = str(task.get("id", ""))
    attempts_before = (
        int(task.get("attempts_grok", 0) or 0),
        int(task.get("attempts_codex", 0) or 0),
    )
    if needs:
        _ACTIVE_CHECKPOINTS[tid] = outer_cp
    try:
        return ORIGINAL_SAFE_EXECUTE(cfg, st, task, run_stats)
    except FV.FunctionalVerificationWait as e:
        # Verification infrastructure disappeared after implementation began
        # (for example the user opened Unity). Never burn repair attempts for that.
        runner.rollback_checkpoint(root, outer_cp)
        task["attempts_grok"], task["attempts_codex"] = attempts_before
        _mark_verification_wait(cfg, st, task, str(e))
        return "waiting_verification"
    finally:
        _ACTIVE_CHECKPOINTS.pop(tid, None)


def next_ready(st: dict[str, Any]) -> dict[str, Any] | None:
    now = time.time()
    for task in st.get("tasks", []):
        if not isinstance(task, dict) or task.get("status") != "waiting_verification":
            continue
        try:
            retry_at = float(task.get("verification_retry_at", 0) or 0)
        except Exception:
            retry_at = 0.0
        if retry_at <= now:
            task["status"] = "pending"
            task.pop("wait_reason", None)
            print(f"[FUNCTIONAL] {task.get('id')} Unity 기능 검증 재시도 가능")
    return ORIGINAL_NEXT_READY(st)


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    waiting = [
        t for t in st.get("tasks", [])
        if isinstance(t, dict) and t.get("status") == "waiting_verification"
    ]
    pending = [
        t for t in st.get("tasks", [])
        if isinstance(t, dict) and t.get("status") == "pending"
    ]
    if waiting and not pending:
        runner._LAST_DIRECTOR_META = {"status": "provider_wait", "cloud_used": 0}
        print("[FUNCTIONAL] Unity 기능 검증을 기다리는 작업이 있어 새 계획을 만들지 않습니다.")
        return False
    return ORIGINAL_DIRECTOR_FILL(cfg, st)


def main() -> int:
    AUTODEV.codex_call = codex_call
    AUTODEV.worker_prompt = worker_prompt
    AUTODEV.verify_task = verify_task
    runner.safe_execute_one = safe_execute_one
    runner.next_ready = next_ready
    runner.director_fill = director_fill
    return runner.main()


if __name__ == "__main__":
    raise SystemExit(main())
