#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 execution entrypoint.

Runtime integrations:
- Grok/Codex subprocess output streams live.
- Shared heartbeat contract keeps dashboard/engine/runner in sync.
- Stopping AutoDev also stops the currently active provider child process.
- Unity may be open while implementation proceeds; completion waits for acceptance.
- Task completion records only that task's delta, not the owner's pre-existing dirty tree.
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
from runtime_contract import CONTROL_PROTOCOL

AUTODEV = runner.AUTODEV
ORIGINAL_WORKER_PROMPT = AUTODEV.worker_prompt
ORIGINAL_VERIFY_TASK = AUTODEV.verify_task
ORIGINAL_CANDIDATE_FILES = AUTODEV.candidate_files
ORIGINAL_FINISH_TASK = AUTODEV.finish_task
ORIGINAL_SAFE_EXECUTE = runner.safe_execute_one
ORIGINAL_NEXT_READY = runner.next_ready
ORIGINAL_DIRECTOR_FILL = runner.director_fill
_ACTIVE_CHECKPOINTS: dict[str, dict[str, Any]] = {}
_ACTIVE_DELTA_OVERRIDES: dict[str, set[str]] = {}
_LAST_CFG: dict[str, Any] | None = None

PROJECT_AREAS: dict[str, tuple[str, ...]] = {
    "estate": ("estate", "estatescreen", "territory", "영지"),
    "formation": ("formation", "w3party", "party formation", "편성"),
    "raid": ("raid", "bossbattle", "boss battle", "레이드", "보스전"),
    "fusion": ("fusion", "merge", "combine", "합성"),
    "class_change": ("class change", "job change", "promotion", "전직"),
}
# Project-specific areas must win before broad generic categories such as combat/character.
runner.AREA_KEYWORDS = {**PROJECT_AREAS, **runner.AREA_KEYWORDS}

ANCHORS: dict[str, tuple[str, ...]] = {
    "estate": ("EstateScreen", "Estate"),
    "formation": ("W3Party", "Formation"),
    "raid": ("BossBattle", "Raid"),
    "fusion": ("Fusion", "Merge", "Combine"),
    "class_change": ("ClassChange", "JobChange", "Promotion"),
}

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
HEARTBEAT = OUTPUT / "engine_heartbeat.json"
_HB_LOCK = threading.RLock()
_PROVIDER_LOCK = threading.RLock()
_ACTIVE_PROVIDER: subprocess.Popen[str] | None = None
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
            "engine_protocol": CONTROL_PROTOCOL,
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
        if p.poll() is not None:
            return
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


def _shutdown(signum: int, _frame: Any) -> None:
    set_runtime(stage="stopping", message="중지 요청을 받아 AI 자식 프로세스까지 정리 중", provider="local", output=True)
    with _PROVIDER_LOCK:
        child = _ACTIVE_PROVIDER
    if child is not None:
        _terminate_process(child)
    raise SystemExit(128 + int(signum))


def stream_process(cmd: list[str], cwd: Path, timeout: int, env: dict[str, str], tag: str) -> tuple[int, str]:
    """Stream output and emit a visible pulse every 5s while the provider is quiet."""
    global _ACTIVE_PROVIDER
    print(f"\n[{tag}] 시작", flush=True)
    provider = "codex" if "codex" in tag.lower() else "grok" if "grok" in tag.lower() else "local"
    set_runtime(stage=tag, message="작업 시작", provider=provider, output=True)
    started = time.monotonic()
    last_pulse = started
    lines: list[str] = []
    q: queue.Queue[str | None] = queue.Queue()
    p: subprocess.Popen[str] | None = None
    try:
        p = subprocess.Popen(
            cmd, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, encoding="utf-8", errors="replace", bufsize=1, env=env,
            start_new_session=(os.name != "nt"),
        )
        with _PROVIDER_LOCK:
            _ACTIVE_PROVIDER = p
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
    finally:
        with _PROVIDER_LOCK:
            if _ACTIVE_PROVIDER is p:
                _ACTIVE_PROVIDER = None


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


def _task_text(task: dict[str, Any]) -> str:
    return " ".join([
        str(task.get("title", "")), str(task.get("goal", "")),
        " ".join(str(x) for x in task.get("done_when", [])),
    ]).lower()


def candidate_files(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> list[str]:
    """Add Ashes-to-Stars filename anchors before the generic rg/recent-diff candidates."""
    base = ORIGINAL_CANDIDATE_FILES(cfg, task, verify_text)
    maxn = int(cfg.get("max_candidate_files", 5))
    text = _task_text(task)
    area = str(task.get("area") or runner.infer_area(task))
    terms: list[str] = list(ANCHORS.get(area, ()))
    if "영지" in text or "estate" in text:
        terms += ["EstateScreen"]
    if "편성" in text or "w3party" in text or "formation" in text:
        terms += ["W3Party"]
    if "레이드" in text or "보스전" in text or "bossbattle" in text or "boss battle" in text:
        terms += ["BossBattle"]

    anchors: list[str] = []
    if terms:
        root = Path(cfg["_repo_root"])
        project = Path(cfg["project_root"])
        lowered = tuple(x.lower() for x in terms)
        try:
            for path in project.rglob("*.cs"):
                rel = str(path.resolve().relative_to(root)).replace("\\", "/")
                probe = rel.lower()
                if any(term in probe for term in lowered):
                    anchors.append(rel)
        except Exception:
            pass

    out: list[str] = []
    for rel in anchors + base:
        if rel not in out:
            out.append(rel)
        if len(out) >= maxn:
            break
    return out


def verify_task(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[str, str]:
    set_runtime(stage="verify", message=f"{task.get('id','')} 로컬/Unity 검증 중", provider="local", output=True)
    status, base = ORIGINAL_VERIFY_TASK(cfg, task)
    if status != "pass":
        return status, base
    if not FV.requires_functional(cfg, task):
        return status, base

    tid = str(task.get("id", ""))
    override = _ACTIVE_DELTA_OVERRIDES.get(tid)
    cp = _ACTIVE_CHECKPOINTS.get(tid)
    delta = set(override) if override is not None else runner.task_delta_paths(Path(cfg["_repo_root"]), cp) if cp else None
    fstatus, functional = FV.verify_functional(cfg, task, delta_paths=delta)
    joined = (base.rstrip() + "\n\n" + functional.rstrip()).strip()
    return fstatus, joined


def finish_task(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], verify_out: str) -> None:
    """Record only this task's delta, excluding owner changes that existed before it started."""
    tid = str(task.get("id", ""))
    override = _ACTIVE_DELTA_OVERRIDES.get(tid)
    cp = _ACTIVE_CHECKPOINTS.get(tid)
    if override is not None:
        delta = set(override)
    elif cp is not None:
        delta = runner.task_delta_paths(Path(cfg["_repo_root"]), cp)
    else:
        delta = {str(x) for x in task.get("implementation_delta_files", [])}

    ORIGINAL_FINISH_TASK(cfg, st, task, verify_out)
    for item in reversed(st.get("completed", [])):
        if isinstance(item, dict) and str(item.get("id", "")) == tid:
            item["changed_files"] = sorted(delta)[:30]
            item.pop("verification_only", None)
            item.pop("implementation_delta_files", None)
            item.pop("verification_retry_at", None)
            item.pop("wait_reason", None)
            break
    AUTODEV.save_state(cfg, st)


def _mark_verification_wait(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], reason: str) -> None:
    retry = max(30, int(cfg.get("functional_verify_wait_seconds", 120)))
    task["status"] = "waiting_verification"
    task["verification_retry_at"] = time.time() + retry
    task["last_error"] = "Unity 실제 기능 검증 대기: " + reason[-2400:]
    task["wait_reason"] = task["last_error"]
    AUTODEV.save_state(cfg, st)
    set_runtime(stage="waiting_verification", message=task["last_error"], provider="local", output=True)
    print(f"[FUNCTIONAL] {task.get('id')} · 구현 보존 · {retry}초 후 검증만 다시 확인 · {reason[:700]}", flush=True)


def _mark_capacity_wait(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], reason: str) -> None:
    retry = max(30, int(cfg.get("functional_verify_wait_seconds", 120)))
    task["status"] = "waiting_verification_capacity"
    task["verification_retry_at"] = time.time() + retry
    task["last_error"] = reason
    task["wait_reason"] = reason
    AUTODEV.save_state(cfg, st)
    set_runtime(stage="waiting_verification", message=reason, provider="local")


def _waiting_count(st: dict[str, Any]) -> int:
    return sum(1 for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "waiting_verification")


def _waiting_cap(cfg: dict[str, Any] | None = None) -> int:
    cfg = cfg or _LAST_CFG or {}
    return max(1, int(cfg.get("max_waiting_verification_tasks", 2)))


def _verification_only_pass(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any]) -> tuple[bool, str]:
    """Retry acceptance without calling Grok/Codex. False means repair is needed."""
    tid = str(task.get("id", ""))
    ready, reason = FV.environment_ready(cfg)
    if not ready:
        _mark_verification_wait(cfg, st, task, reason)
        return True, "waiting_verification"

    delta = {str(x) for x in task.get("implementation_delta_files", [])}
    _ACTIVE_DELTA_OVERRIDES[tid] = delta
    try:
        status, out = verify_task(cfg, task)
    except FV.FunctionalVerificationWait as e:
        _mark_verification_wait(cfg, st, task, str(e))
        return True, "waiting_verification"
    finally:
        _ACTIVE_DELTA_OVERRIDES.pop(tid, None)

    print(f"\n[VERIFY-ONLY] {status.upper()}\n{out[-2500:]}")
    if status == "pass":
        _ACTIVE_DELTA_OVERRIDES[tid] = delta
        try:
            AUTODEV.finish_task(cfg, st, task, out)
        finally:
            _ACTIVE_DELTA_OVERRIDES.pop(tid, None)
        return True, "done"

    task["verification_only"] = False
    task["status"] = "pending"
    task["last_error"] = "보류했던 Unity 기능 검증 실패. 구현을 보존한 채 수리 필요.\n" + out[-2400:]
    task.pop("wait_reason", None)
    task.pop("verification_retry_at", None)
    AUTODEV.save_state(cfg, st)
    return False, task["last_error"]


def safe_execute_one(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any], run_stats: dict[str, int]) -> str:
    global _LAST_CFG
    _LAST_CFG = cfg
    set_runtime(stage="task", message=f"{task.get('id','')} {task.get('title','')} 시작", provider="local", output=True)
    needs = FV.requires_functional(cfg, task)

    if needs and bool(task.get("verification_only")):
        handled, outcome = _verification_only_pass(cfg, st, task)
        if handled:
            return outcome
        # Acceptance really failed, so continue below into the normal repair worker.

    if needs:
        ready, reason = FV.environment_ready(cfg)
        implement_locked = bool(cfg.get("implement_while_unity_locked", True))
        if not ready and not implement_locked:
            _mark_verification_wait(cfg, st, task, reason)
            return "waiting_verification"
        if not ready and implement_locked and _waiting_count(st) >= _waiting_cap(cfg):
            _mark_capacity_wait(
                cfg, st, task,
                f"Unity 검증 대기 작업이 {_waiting_cap(cfg)}개라 새 구현을 잠시 보류합니다. 기존 검증이 끝나면 자동 재개합니다.",
            )
            return "waiting_verification"

    root = Path(cfg["_repo_root"])
    outer_cp = runner.checkpoint(root)
    tid = str(task.get("id", ""))
    if needs:
        _ACTIVE_CHECKPOINTS[tid] = outer_cp
    try:
        outcome = ORIGINAL_SAFE_EXECUTE(cfg, st, task, run_stats)
        set_runtime(stage="task_done" if outcome == "done" else "task_result",
                    message=f"{tid} 결과: {outcome}", provider="local", output=True)
        return outcome
    except FV.FunctionalVerificationWait as e:
        # The implementation is valuable. Do not roll it back merely because Unity
        # is open/unavailable. Hold completion and retry acceptance without AI later.
        delta = runner.task_delta_paths(root, outer_cp)
        task["implementation_delta_files"] = sorted(delta)
        task["verification_only"] = True
        _mark_verification_wait(cfg, st, task, str(e))
        return "waiting_verification"
    finally:
        _ACTIVE_CHECKPOINTS.pop(tid, None)


def next_ready(st: dict[str, Any]) -> dict[str, Any] | None:
    now = time.time()
    cap = _waiting_cap()
    waiting = _waiting_count(st)
    for task in st.get("tasks", []):
        if not isinstance(task, dict):
            continue
        status = task.get("status")
        try:
            retry_at = float(task.get("verification_retry_at", 0) or 0)
        except Exception:
            retry_at = 0.0
        if status == "waiting_verification" and retry_at <= now:
            task["status"] = "pending"
            task.pop("wait_reason", None)
            print(f"[FUNCTIONAL] {task.get('id')} Unity 기능 검증 재시도 가능", flush=True)
        elif status == "waiting_verification_capacity" and retry_at <= now and waiting < cap:
            task["status"] = "pending"
            task.pop("wait_reason", None)
            print(f"[FUNCTIONAL] {task.get('id')} 대기 슬롯이 생겨 구현 재개 가능", flush=True)
    return ORIGINAL_NEXT_READY(st)


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    global _LAST_CFG
    _LAST_CFG = cfg
    waiting = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "waiting_verification"]
    capacity = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "waiting_verification_capacity"]
    pending = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "pending"]
    if len(waiting) >= _waiting_cap(cfg) or capacity or (waiting and not pending):
        runner._LAST_DIRECTOR_META = {"status": "provider_wait", "cloud_used": 0}
        set_runtime(stage="waiting_verification", message="Unity 완료 검증 대기 중 · 새 계획 생성 보류", provider="local")
        print("[FUNCTIONAL] 검증 대기 큐를 먼저 비우기 위해 새 Director 계획을 만들지 않습니다.", flush=True)
        return False
    set_runtime(stage="director", message="Grok Director가 다음 작업을 계획 중", provider="grok", output=True)
    return ORIGINAL_DIRECTOR_FILL(cfg, st)


def main() -> int:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    if os.name != "nt":
        signal.signal(signal.SIGTERM, _shutdown)
        signal.signal(signal.SIGINT, _shutdown)
    threading.Thread(target=heartbeat_loop, daemon=True).start()
    set_runtime(stage="starting", message="Grok Director + Supervisor 시작", provider="local", output=True)
    AUTODEV.stream_process = stream_process
    AUTODEV.codex_call = codex_call
    AUTODEV.worker_prompt = worker_prompt
    AUTODEV.candidate_files = candidate_files
    AUTODEV.verify_task = verify_task
    AUTODEV.finish_task = finish_task
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
