#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""New AutoDev loop.

Director → local queue → Grok Worker → local verify → seed if empty.
No v1 migrate, no launchd, no runner_entry.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_config() -> dict[str, Any]:
    cfg = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
    cfg["_repo_root"] = str(REPO)
    for key in ("project_root", "design_file", "handoff_file", "state_file"):
        cfg[key] = str((REPO / cfg[key]).resolve())
    return cfg


def load_profile(cfg: dict[str, Any]) -> dict[str, Any]:
    name = str(cfg.get("active_project") or "ashes-to-stars")
    path = HERE / "profiles" / f"{name}.json"
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}
    return data if isinstance(data, dict) else {}


def new_state() -> dict[str, Any]:
    return {
        "version": 3,
        "goal": "",
        "tasks": [],
        "completed": [],
        "blocked": [],
        "next_task_number": 1,
        "stats": {"grok_calls": 0, "codex_calls": 0, "director_calls": 0, "tasks_done": 0, "tasks_blocked": 0},
        "updated_at": now_iso(),
    }


def load_state(cfg: dict[str, Any]) -> dict[str, Any]:
    p = Path(cfg["state_file"])
    if not p.exists():
        st = new_state()
        save_state(cfg, st)
        return st
    try:
        st = json.loads(p.read_text(encoding="utf-8"))
        if not isinstance(st, dict):
            raise ValueError("bad state")
        st.setdefault("tasks", [])
        st.setdefault("completed", [])
        st.setdefault("blocked", [])
        st.setdefault("stats", {})
        st.setdefault("next_task_number", 1)
        return st
    except Exception:
        st = new_state()
        save_state(cfg, st)
        return st


def save_state(cfg: dict[str, Any], st: dict[str, Any]) -> None:
    p = Path(cfg["state_file"])
    p.parent.mkdir(parents=True, exist_ok=True)
    st["updated_at"] = now_iso()
    tmp = p.with_suffix(p.suffix + ".tmp")
    tmp.write_text(json.dumps(st, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(p)


def extract_json(text: str) -> Any:
    text = text or ""
    m = re.search(r"```json\s*(.*?)\s*```", text, re.S)
    raw = m.group(1) if m else text
    start = raw.find("{")
    end = raw.rfind("}")
    if start < 0 or end <= start:
        return None
    try:
        return json.loads(raw[start:end + 1])
    except Exception:
        return None


def grok_exe() -> str | None:
    pinned = os.environ.get("AUTODEV_REAL_GROK", "").strip()
    if pinned and Path(pinned).exists():
        return pinned
    wrapper = REPO / "output" / "autodev_v2" / "runtime_bin" / "grok"
    if wrapper.exists():
        return str(wrapper)
    return shutil.which("grok")


def grok_run(prompt: str, cwd: Path, max_turns: int, allow_edits: bool) -> tuple[int, str]:
    exe = grok_exe()
    if not exe:
        return 127, "grok CLI를 찾을 수 없습니다."
    cmd = [exe, "--single", prompt, "--cwd", str(cwd), "--output-format", "plain", "--max-turns", str(max_turns)]
    if allow_edits:
        cmd.append("--always-approve")
    try:
        r = subprocess.run(
            cmd, cwd=cwd, capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=900,
        )
        out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
        return r.returncode, out
    except subprocess.TimeoutExpired:
        return 124, "TIMEOUT"
    except Exception as e:
        return 125, f"{type(e).__name__}: {e}"


def next_id(st: dict[str, Any]) -> str:
    n = int(st.get("next_task_number") or 1)
    st["next_task_number"] = n + 1
    return f"T{n:04d}"


def live_tasks(st: dict[str, Any]) -> list[dict[str, Any]]:
    live = {"pending", "working", "waiting_verification", "waiting_dependency"}
    return [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") in live]


def seed_if_empty(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    if live_tasks(st):
        return False
    spec = load_profile(cfg).get("seed_tasks") or []
    if not isinstance(spec, list) or not spec:
        return False
    added = 0
    for raw in spec:
        if not isinstance(raw, dict) or not raw.get("title"):
            continue
        task = {
            "id": next_id(st),
            "title": str(raw.get("title"))[:200],
            "goal": str(raw.get("goal") or raw.get("title"))[:800],
            "area": str(raw.get("area") or "systems"),
            "done_when": list(raw.get("done_when") or []),
            "priority": int(raw.get("priority") or 50),
            "depends_on": [],
            "verify_mode": str(raw.get("verify_mode") or "compile"),
            "status": "pending",
            "created_at": now_iso(),
        }
        st.setdefault("tasks", []).append(task)
        added += 1
    if added:
        profile = load_profile(cfg)
        st["goal"] = st.get("goal") or str(profile.get("goal") or "자율 루프 전진")
        st["last_director_provider"] = "seed"
        print(f"[SEED] 씨앗 작업 {added}개 투입", flush=True)
    return added > 0


def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
    prompt = f"""당신은 AutoDev Director다. 코드는 수정하지 마라.
플레이 루프를 앞으로 밀어라: 영지 → 편성 → 전투 → 보상.
STATUS/아트/폴리싱은 작업이 아니다.
JSON만 출력:
{{"goal":"한 줄","tasks":[{{"title":"...","goal":"...","area":"estate|formation|raid|character|combat","done_when":["조건"],"priority":80}}]}}
현재 완료 {len(st.get("completed") or [])} / 막힘 {len(st.get("blocked") or [])} / 큐 {len(st.get("tasks") or [])}
목표: {st.get("goal") or load_profile(cfg).get("goal") or "플레이 가능"}
"""
    code, out = grok_run(prompt, Path(cfg["_repo_root"]), int(cfg.get("grok_director_max_turns") or 2), False)
    st.setdefault("stats", {})["grok_calls"] = int(st["stats"].get("grok_calls") or 0) + 1
    st["stats"]["director_calls"] = int(st["stats"].get("director_calls") or 0) + 1
    if code != 0:
        print(f"[DIRECTOR] 실패 rc={code}: {out[-400:]}", flush=True)
        return seed_if_empty(cfg, st)
    parsed = extract_json(out)
    rows = parsed.get("tasks") if isinstance(parsed, dict) else None
    if not isinstance(rows, list) or not rows:
        return seed_if_empty(cfg, st)
    if parsed.get("goal"):
        st["goal"] = str(parsed["goal"])[:500]
    for raw in rows[: int(cfg.get("max_tasks_per_director_batch") or 6)]:
        if not isinstance(raw, dict) or not raw.get("title"):
            continue
        st["tasks"].append({
            "id": next_id(st),
            "title": str(raw.get("title"))[:200],
            "goal": str(raw.get("goal") or raw.get("title"))[:800],
            "area": str(raw.get("area") or "systems"),
            "done_when": list(raw.get("done_when") or []),
            "priority": int(raw.get("priority") or 50),
            "depends_on": [],
            "verify_mode": str(raw.get("verify_mode") or "compile"),
            "status": "pending",
            "created_at": now_iso(),
        })
    print("[DIRECTOR] 작업 생성", flush=True)
    return True


def next_ready(st: dict[str, Any]) -> dict[str, Any] | None:
    ready = [t for t in st.get("tasks", []) if isinstance(t, dict) and t.get("status") == "pending"]
    if not ready:
        return None
    ready.sort(key=lambda t: (-int(t.get("priority") or 0), str(t.get("id"))))
    return ready[0]


def verify(cfg: dict[str, Any], task: dict[str, Any]) -> tuple[str, str]:
    checker = REPO / "projects/ai-team/skills/마루_게임개발/tools/game_compile_check.py"
    project = Path(cfg["project_root"])
    if checker.exists():
        r = subprocess.run(
            ["python3", str(checker), str(project)],
            capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180,
        )
        out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
        if r.returncode == 0:
            return "pass", out[-3000:]
        return "fail", out[-3000:]
    return "pass", "compile checker 없음"


def worker(cfg: dict[str, Any], st: dict[str, Any], task: dict[str, Any]) -> str:
    task["status"] = "working"
    save_state(cfg, st)
    done = "; ".join(str(x) for x in (task.get("done_when") or [])[:4])
    prompt = f"""당신은 AutoDev Worker다. 이 작업만 최소 수정으로 구현하고 종료하라.
제목: {task.get("title")}
목표: {task.get("goal")}
완료 조건: {done or "관련 코드가 동작한다"}
영역: {task.get("area")}
git commit/push 금지. Unity 프로세스 종료 금지.
"""
    project = Path(cfg["project_root"])
    code, out = grok_run(prompt, project, int(cfg.get("grok_worker_max_turns") or 6), True)
    st["stats"]["grok_calls"] = int(st.get("stats", {}).get("grok_calls") or 0) + 1
    if code not in (0,):
        task["last_error"] = out[-1500:]
        task["status"] = "pending"
        print(f"[WORKER] rc={code}", flush=True)
        return "retry"
    status, vout = verify(cfg, task)
    print(f"[VERIFY] {status}", flush=True)
    if status == "pass":
        task["status"] = "done"
        st["tasks"] = [t for t in st["tasks"] if t.get("id") != task.get("id")]
        st.setdefault("completed", []).append({k: task.get(k) for k in ("id", "title", "goal", "area")})
        st["stats"]["tasks_done"] = int(st["stats"].get("tasks_done") or 0) + 1
        return "done"
    task["last_error"] = vout[-1500:]
    task["status"] = "pending"
    return "fail"


def print_status(st: dict[str, Any]) -> None:
    live = live_tasks(st)
    print(
        "[STATUS] 큐 %s · 완료 %s · 막힘 %s"
        % (len(live), len(st.get("completed") or []), len(st.get("blocked") or [])),
        flush=True,
    )


def run_loop() -> int:
    cfg = load_config()
    st = load_state(cfg)
    seed_if_empty(cfg, st)
    save_state(cfg, st)
    pause = int(cfg.get("supervisor_pause_seconds") or 15)
    while True:
        print_status(st)
        task = next_ready(st)
        if task is None:
            if not director_fill(cfg, st):
                seed_if_empty(cfg, st)
            save_state(cfg, st)
            if next_ready(st) is None:
                print(f"[LOOP] 할 일 없음. {pause}초 후 다시 시도", flush=True)
                time.sleep(pause)
            continue
        worker(cfg, st, task)
        save_state(cfg, st)
        time.sleep(1)


def main() -> int:
    print("[LOOP] AutoDev 새 루프 시작", flush=True)
    try:
        return run_loop()
    except KeyboardInterrupt:
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
