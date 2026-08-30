#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PATH = ROOT / "projects/autodev-v2/runner_entry.py"


def replace_once(text: str, old: str, new: str, label: str) -> str:
    n = text.count(old)
    if n != 1:
        raise RuntimeError(f"{label}: expected one match, got {n}")
    return text.replace(old, new, 1)


def main() -> None:
    text = PATH.read_text(encoding="utf-8")
    text = replace_once(text, "import json\n", "import base64\nimport json\n", "base64 import")
    text = replace_once(
        text,
        'HEARTBEAT = OUTPUT / "engine_heartbeat.json"\n',
        'HEARTBEAT = OUTPUT / "engine_heartbeat.json"\nHOLD_DIR = OUTPUT / "verification_holds"\n',
        "hold dir",
    )
    marker = '''def set_runtime(*, stage: str | None = None, message: str | None = None,\n'''
    helpers = r'''def _hold_path(tid: str) -> Path:
    safe = "".join(ch if ch.isalnum() or ch in "_-" else "_" for ch in tid) or "TASK"
    return HOLD_DIR / f"{safe}.json"


def _checkpoint_json(cp: dict[str, Any]) -> dict[str, Any]:
    snapshots: dict[str, Any] = {}
    for rel, snap in cp.get("snapshots", {}).items():
        existed, data = snap
        snapshots[str(rel)] = {
            "existed": bool(existed),
            "data_b64": base64.b64encode(data).decode("ascii") if data is not None else None,
        }
    return {
        "dirty": sorted(str(x) for x in cp.get("dirty", set())),
        "untracked": sorted(str(x) for x in cp.get("untracked", set())),
        "staged": sorted(str(x) for x in cp.get("staged", set())),
        "snapshots": snapshots,
    }


def _checkpoint_from_json(raw: dict[str, Any]) -> dict[str, Any] | None:
    try:
        snapshots: dict[str, tuple[bool, bytes | None]] = {}
        for rel, item in raw.get("snapshots", {}).items():
            if not isinstance(item, dict):
                return None
            encoded = item.get("data_b64")
            data = base64.b64decode(encoded.encode("ascii")) if isinstance(encoded, str) else None
            snapshots[str(rel)] = (bool(item.get("existed")), data)
        return {
            "dirty": {str(x) for x in raw.get("dirty", [])},
            "untracked": {str(x) for x in raw.get("untracked", [])},
            "staged": {str(x) for x in raw.get("staged", [])},
            "snapshots": snapshots,
        }
    except Exception:
        return None


def save_hold_checkpoint(tid: str, cp: dict[str, Any]) -> None:
    path = _hold_path(tid)
    path.parent.mkdir(parents=True, exist_ok=True)
    _atomic_json(path, _checkpoint_json(cp))


def load_hold_checkpoint(tid: str) -> dict[str, Any] | None:
    path = _hold_path(tid)
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None
    return _checkpoint_from_json(raw) if isinstance(raw, dict) else None


def clear_hold_checkpoint(tid: str) -> None:
    try:
        _hold_path(tid).unlink()
    except OSError:
        pass


'''
    text = replace_once(text, marker, helpers + marker, "checkpoint helpers")

    old_finish = '''    override = _ACTIVE_DELTA_OVERRIDES.get(tid)\n    cp = _ACTIVE_CHECKPOINTS.get(tid)\n    if override is not None:\n        delta = set(override)\n    elif cp is not None:\n        delta = runner.task_delta_paths(Path(cfg["_repo_root"]), cp)\n    else:\n        delta = {str(x) for x in task.get("implementation_delta_files", [])}\n'''
    new_finish = '''    override = _ACTIVE_DELTA_OVERRIDES.get(tid)\n    held_cp = load_hold_checkpoint(tid)\n    cp = _ACTIVE_CHECKPOINTS.get(tid)\n    if override is not None:\n        delta = set(override)\n    elif held_cp is not None:\n        delta = runner.task_delta_paths(Path(cfg["_repo_root"]), held_cp)\n    elif cp is not None:\n        delta = runner.task_delta_paths(Path(cfg["_repo_root"]), cp)\n    else:\n        delta = {str(x) for x in task.get("implementation_delta_files", [])}\n'''
    text = replace_once(text, old_finish, new_finish, "finish full hold delta")
    text = replace_once(
        text,
        '    AUTODEV.save_state(cfg, st)\n\n\ndef _mark_verification_wait',
        '    AUTODEV.save_state(cfg, st)\n    clear_hold_checkpoint(tid)\n\n\ndef _mark_verification_wait',
        "clear hold on finish",
    )

    old_outcome = '''        outcome = ORIGINAL_SAFE_EXECUTE(cfg, st, task, run_stats)\n        set_runtime(stage="task_done" if outcome == "done" else "task_result",\n                    message=f"{tid} 결과: {outcome}", provider="local", output=True)\n        return outcome\n'''
    new_outcome = '''        outcome = ORIGINAL_SAFE_EXECUTE(cfg, st, task, run_stats)\n        if outcome == "blocked":\n            held_cp = load_hold_checkpoint(tid)\n            if held_cp is not None:\n                restored = runner.rollback_checkpoint(root, held_cp)\n                clear_hold_checkpoint(tid)\n                for item in reversed(st.get("blocked", [])):\n                    if isinstance(item, dict) and str(item.get("id", "")) == tid:\n                        item["hold_rollback_files"] = restored[:30]\n                        item.pop("verification_only", None)\n                        item.pop("implementation_delta_files", None)\n                        break\n                AUTODEV.save_state(cfg, st)\n        set_runtime(stage="task_done" if outcome == "done" else "task_result",\n                    message=f"{tid} 결과: {outcome}", provider="local", output=True)\n        return outcome\n'''
    text = replace_once(text, old_outcome, new_outcome, "rollback persistent hold on block")

    old_wait = '''        delta = runner.task_delta_paths(root, outer_cp)\n        task["implementation_delta_files"] = sorted(delta)\n        task["verification_only"] = True\n'''
    new_wait = '''        held_cp = load_hold_checkpoint(tid)\n        if held_cp is None:\n            save_hold_checkpoint(tid, outer_cp)\n            held_cp = outer_cp\n        delta = runner.task_delta_paths(root, held_cp)\n        task["implementation_delta_files"] = sorted(delta)\n        task["verification_only"] = True\n'''
    text = replace_once(text, old_wait, new_wait, "persist original hold checkpoint")
    PATH.write_text(text, encoding="utf-8")
    print("persistent verification hold checkpoint patch applied")


if __name__ == "__main__":
    main()
