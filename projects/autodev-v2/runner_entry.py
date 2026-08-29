#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 execution entrypoint.

Runtime integrations:
- Grok/Codex subprocess output streams live.
- A heartbeat survives long provider silences so the dashboard can tell alive from dead.
- Gameplay/system tasks must pass task-specific Unity acceptance verification.
"""
from __future__ import annotations

import json
import os
import queue
import shutil
import signal
import subprocess
import tempfile
import threading
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

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
HEARTBEAT = OUTPUT / "engine_heartbeat.json"
_HB_LOCK = threading.RLock()
_RUNTIME: dict[str, Any] = {
    "stage": "starting",
    "message": "엔진 시작 중",
    "provider": "local",
    "last_output_at": 0.0,
}


def _atomic_json(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def set_runtime(*, stage: str | None = None, message: str | None = None,
                provider: str | None = None, output: bool = False) -> None:
    with _HB_LOCK:
        if stage is not None:
            _RUNTIME["stage"] = stage
        if message is not None:
            _RUNTIME["message"] = str(message)[-1200:]
        if provider is not None:
            _RUNTIME["provider"] = provider
        if output:
            _RUNTIME["last_output_at"] = time.time()
        payload = dict(_RUNTIME)
        payload.update({
            "heartbeat_at": time.time(),
            "pid": os.getpid(),
            "engine_protocol": 6,
        })
        try:
            _atomic_json(HEARTBEAT, payload)
        except Exception:
            pass


def heartbeat_loop() -> None:
    while True:
        set_runtime()
        time.sleep(5)


def _terminate_process(p: subprocess.Popen[str]) -> None:
    try:
        if os.name != "nt":
            os.killpg(p.pid, signal.SIGTERM)
        else:
            p.terminate()
        p.wait(timeout=2)
        return
    except Exception:
        pass
    try:
        if os.name != "nt":
            os.killpg(p.pid, signal.SIGKILL)
        else:
            p.kill()
    except Exception:
        pass


def stream_process(cmd: list[str], cwd: Path, timeout: int, env: dict[str, str], tag: str) -> tuple[int, str]:
    """Stream output and emit a visible pulse every 5s while the provider is quiet."""
    print(f"\n[{tag}] 시작", flush=True)
    provider = "codex" if "codex" in tag.lower() else "grok" if "grok" in tag.lower() else "local"
    set_runtime(stage=tag, message="작업 시작", provider=provider, output=True)
    started = time.monotonic()
    last_pulse = started
    lines: list[str] = []
    q: queue.Queue[str | None] = queue.Queue()
    try:
        p = subprocess.Popen(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace", bufsize=1, env=env,
            start_new_session=(os.name != "nt"),
        )
        assert p.stdout is not None

        def reader() -> None:
            try:
                for line in p.stdout:
                    q.put(line.rstrip("\n"))
            finally:
                q.put(None)

        threading.Thread(target=reader, daemon=True).start()
        stream_done = False
        while True:
            if time.monotonic() - started > timeout:
                _terminate_process(p)
                msg = f"TIMEOUT after {timeout}s"
                lines.append(msg)
                print(f"[{tag}] {msg}", flush=True)
                set_runtime(stage=tag, message=msg, provider=provider, output=True)
                return 124, "\n".join(lines)
            try:
                item = q.get(timeout=0.2)
            except queue.Empty:
                if p.poll() is not None and stream_done:
                    break
                if time.monotonic() - last_pulse >= 5:
                    last_output = float(_RUNTIME.get("last_output_at", 0) or 0)
                    quiet = int(max(0, time.time() - last_output)) if last_output else 0
                    pulse = f"계속 작업 중 · 마지막 실제 출력 {quiet}초 전"
                    print(f"[{tag}] {pulse}", flush=True)
                    set_runtime(stage=tag, message=pulse, provider=provider)
                    last_pulse = time.monotonic()
                continue
            if item is None:
                stream_done = True
                if p.poll() is not None:
                    break
                continue
            lines.append(item)
            print(f"[{tag}] {item}", flush=True)
            set_runtime(stage=tag, message=item, provider=provider, output=True)
            last_pulse = time.monotonic()
        rc = p.wait(timeout=5)
        set_runtime(stage="provider_done", message=f"{tag} 종료 rc={rc}", provider=provider, output=True)
        return rc, "\n".join(lines)
    except Exception as e:
        msg = f"{type(e).__name__}: {e}"
        set_runtime(stage="provider_error", message=msg, provider=provider, output=True)
        return 125, msg


def codex_call(cfg: dict[str, Any], st: dict[str, Any], prompt: str, cwd: Path) -> tuple[int, str]:
    exe = shutil.which("codex")
    if not exe:
        return 127, "codex CLI를 찾을 수 없습니다."

    fd, outpath = tempfile.mkstemp(prefix="autodev_v2_codex_", suffix=".txt")
    os.close(fd)
    try:
        cmd = [exe, "exec", "--skip-git-repo-check", "-o", outpath, prompt]
        st["stats"]["codex_calls"] = int(st["stats"].get("codex_calls", 0)) + 1
        rc, streamed = stream_process(
            cmd, cwd, timeout=900,
            env=AUTODEV.subscription_env(cfg, "codex"), tag="CODEX:fallback",
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
    set_runtime(stage="verify", message=f"{task.get('id','')} 로컬/Unity 검증 중", provider="local", output=True)
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
    set_runtime(stage="waiting_verification", message=task["last_error"], provider="local", output=True)
    print(f"[FUNCTIONAL] {task.get('id')} · {retry}초 후 다시 확인 · {reason[:700]}", flush=True)


def safe_execute_one(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], run_stats: dict[str, int]) -> str:
    set_runtime(stage="task", message=f"{task.get('id','')} {task.get('title','')} 시작", provider="local", output=True)
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
        outcome = ORIGINAL_SAFE_EXECUTE(cfg, st, task, run_stats)
        set_runtime(stage="task_done" if outcome == "done" else "task_result",
                    message=f"{tid} 결과: {outcome}", provider="local", output=True)
        return outcome
    except FV.FunctionalVerificationWait as e:
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
            print(f"[FUNCTIONAL] {task.get('id')} Unity 기능 검증 재시도 가능", flush=True)
    return ORIGINAL_NEXT_READY(st)


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    waiting = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "waiting_verification"]
    pending = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "pending"]
    if waiting and not pending:
        runner._LAST_DIRECTOR_META = {"status": "provider_wait", "cloud_used": 0}
        set_runtime(stage="waiting_verification", message="Unity 기능 검증 대기 중", provider="local")
        print("[FUNCTIONAL] Unity 기능 검증을 기다리는 작업이 있어 새 계획을 만들지 않습니다.", flush=True)
        return False
    set_runtime(stage="director", message="Grok Director가 다음 작업을 계획 중", provider="grok", output=True)
    return ORIGINAL_DIRECTOR_FILL(cfg, st)


def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    threading.Thread(target=heartbeat_loop, daemon=True).start()
    set_runtime(stage="starting", message="Grok Director + Supervisor 시작", provider="local", output=True)
    AUTODEV.stream_process = stream_process
    AUTODEV.codex_call = codex_call
    AUTODEV.worker_prompt = worker_prompt
    AUTODEV.verify_task = verify_task
    runner.safe_execute_one = safe_execute_one
    runner.next_ready = next_ready
    runner.director_fill = director_fill
    try:
        rc = runner.main()
        set_runtime(stage="stopped", message=f"엔진 종료 rc={rc}", provider="local", output=True)
        return rc
    except BaseException as e:
        set_runtime(stage="crashed", message=f"{type(e).__name__}: {e}", provider="local", output=True)
        raise


if __name__ == "__main__":
    raise SystemExit(main())
