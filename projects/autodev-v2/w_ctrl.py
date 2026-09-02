#!/usr/bin/env python3
# -*- coding: utf-8 -*-
from __future__ import annotations

import atexit
import io
import json
import os
import re
import secrets
import shutil
import signal
import socket
import sys
import tarfile
import threading
import time
import urllib.parse
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

from codex_usage import cached_codex_usage, refresh_codex_usage
from procutil import popen_retry, run_retry
from runtime_contract import CONTROL_PROTOCOL, control_fingerprint

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
HTML_FILE = HERE / "dashboard.html"
SERVER_STATE = OUTPUT / "html_server.json"
ENGINE_STATE = OUTPUT / "html_engine.json"
HEARTBEAT = OUTPUT / "engine_heartbeat.json"
ENGINE_LOG = OUTPUT / "engine.log"
CONFIG_FILE = HERE / "config.json"
SERVER_LOG = Path.home() / "Library" / "Logs" / "AutoDevV2-HTML.log"
ENGINE_FILE = HERE / "engine.py"
HOST = "127.0.0.1"
PORT = int(os.environ.get("AUTODEV_HTML_PORT", "8765"))
INFRA_PATHS = (
    "projects/autodev-v2",
    ".github/workflows/autodev-v2-tests.yml",
    "projects/ashes-to-stars/unity/Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs",
)
QUOTA_FILES = {"grok": OUTPUT / "grok_quota_exhausted.json", "codex": OUTPUT / "codex_quota_exhausted.json"}
ERROR_RE = re.compile(r"(error|exception|traceback|failed|failure|timeout|blocked|quota|402|rc=[1-9]|실패|오류|막힘)", re.I)
LEGACY_RE = re.compile(r"projects/autodev-v2/(?:start|runner_entry|engine|loop|migrate_v1)\.py")


def read_json(path: Path) -> dict[str, Any]:
    try:
        obj = json.loads(path.read_text(encoding="utf-8"))
        return obj if isinstance(obj, dict) else {}
    except Exception:
        return {}


def write_json(path: Path, obj: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def pid_alive(pid: int) -> bool:
    if pid <= 1:
        return False
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False


def process_rows() -> list[tuple[int, str]]:
    try:
        r = run_retry(["ps", "-Ao", "pid=,command="], capture_output=True, text=True, timeout=4, encoding="utf-8", errors="replace")
    except Exception:
        return []
    out = []
    for line in r.stdout.splitlines():
        m = re.match(r"\s*(\d+)\s+(.*)$", line)
        if m:
            out.append((int(m.group(1)), m.group(2)))
    return out


def legacy_pids() -> list[int]:
    repo = str(REPO).replace("\\", "/")
    found = []
    for pid, cmd in process_rows():
        norm = cmd.replace("\\", "/")
        if repo in norm and LEGACY_RE.search(norm) and "webview_app.py" not in norm:
            found.append(pid)
    return sorted(set(found))


def engine_info() -> dict[str, Any]:
    st = read_json(ENGINE_STATE)
    try:
        pid = int(st.get("pid", 0) or 0)
    except Exception:
        pid = 0
    running = bool(pid and pid_alive(pid))
    fp = str(st.get("control_fingerprint", ""))
    protocol = int(st.get("control_protocol", 0) or 0)
    current_fp = control_fingerprint(HERE)
    stale = bool(running and (protocol != CONTROL_PROTOCOL or not fp or fp != current_fp))
    legacy = legacy_pids()
    return {
        "running": running, "pid": pid if running else None,
        "started_at": float(st.get("started_at", 0) or 0),
        "control_fingerprint": fp, "current_fingerprint": current_fp,
        "control_protocol": protocol, "stale": stale,
        "legacy_pids": legacy, "duplicate": bool(legacy),
        "status": str(st.get("status", "")), "exit_code": st.get("exit_code"),
    }


def configured_state_path() -> Path:
    cfg = read_json(CONFIG_FILE)
    raw = str(cfg.get("state_file", "output/autodev_v2/ashes-to-stars/state.json"))
    p = Path(raw)
    return p if p.is_absolute() else (REPO / p).resolve()


def quota_status(provider: str) -> dict[str, Any]:
    data = read_json(QUOTA_FILES[provider])
    try:
        ts = float(data.get("detected_at", 0) or 0)
    except Exception:
        ts = 0.0
    age = max(0, int(time.time() - ts)) if ts else None
    cooldown = 3600 if provider == "grok" else 300
    active = bool(ts and age is not None and age < cooldown)
    return {"detected": bool(ts), "active": active, "remaining_seconds": max(0, cooldown - int(age or 0)) if active else 0, "reason": str(data.get("reason", ""))}


def find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (f"/opt/homebrew/bin/{name}", f"/usr/local/bin/{name}", str(Path.home() / ".local/bin" / name)):
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def cli_status(name: str) -> dict[str, Any]:
    exe = find_cli(name)
    if not exe:
        return {"installed": False, "path": "", "version": ""}
    version = ""
    for args in (["--version"], ["version"]):
        try:
            r = run_retry([exe, *args], capture_output=True, text=True, timeout=4, encoding="utf-8", errors="replace")
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                version = text.splitlines()[0][:160]
                break
        except Exception:
            pass
    return {"installed": True, "path": exe, "version": version}


def tail_lines(path: Path, limit: int = 800) -> list[str]:
    try:
        return path.read_text(encoding="utf-8", errors="replace").splitlines()[-limit:]
    except Exception:
        return []


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.3):
            return True
    except OSError:
        return False


def kill_group(pid: int, sig: int) -> None:
    try:
        if os.name != "nt":
            os.killpg(os.getpgid(pid), sig)
        else:
            os.kill(pid, sig)
    except Exception:
        pass


def do_reap() -> dict[str, Any]:
    import reap
    return reap.reap(keep_pids={os.getpid()})


class Controller:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self._cli_cache: dict[str, Any] = {}
        self._cli_checked = 0.0
        OUTPUT.mkdir(parents=True, exist_ok=True)

    def log(self, text: str) -> None:
        stamp = time.strftime("%H:%M:%S")
        try:
            with self.lock, ENGINE_LOG.open("a", encoding="utf-8") as f:
                f.write(f"{stamp} {text}\n")
        except Exception:
            pass
        print(text, flush=True)

    def stop(self) -> dict[str, Any]:
        info = engine_info()
        pid = info.get("pid")
        if pid:
            self.log(f"[CONTROL] 중지 요청 PID {pid}")
            kill_group(int(pid), signal.SIGTERM)
            deadline = time.time() + 7
            while pid_alive(int(pid)) and time.time() < deadline:
                time.sleep(0.1)
            if pid_alive(int(pid)):
                kill_group(int(pid), signal.SIGKILL)
        try:
            do_reap()
        except Exception:
            pass
        for pid in legacy_pids():
            kill_group(pid, signal.SIGKILL)
        now = engine_info()
        if now["running"]:
            return {"ok": False, "message": "엔진 프로세스를 완전히 끄지 못했습니다."}
        try:
            HEARTBEAT.unlink()
        except OSError:
            pass
        self.log("[CONTROL] 완전히 중지됨")
        return {"ok": True, "message": "자율 개발을 멈춠습니다."}

    def start(self) -> dict[str, Any]:
        info = engine_info()
        if info["running"] and not info["stale"] and not info["legacy_pids"]:
            return {"ok": True, "message": f"이미 정상 실행 중입니다. PID {info['pid']}"}
        if info["running"] or info["legacy_pids"]:
            r = self.stop()
            if not r.get("ok"):
                return r
        try:
            do_reap()
        except Exception:
            pass
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env["PATH"] = f"{Path.home()/'.local/bin'}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:" + env.get("PATH", "")
        try:
            log = ENGINE_LOG.open("a", encoding="utf-8")
            p = popen_retry(
                [sys.executable, str(ENGINE_FILE)], cwd=REPO,
                stdin=open(os.devnull, "r"),
                stdout=log, stderr=log,
                env=env, start_new_session=True,
            )
        except Exception as e:
            return {"ok": False, "message": f"엔진 시작 실패: {e}"}
        self.log(f"[CONTROL] 단일 엔진 시작 PID {p.pid}")
        deadline = time.time() + 8
        while time.time() < deadline:
            if p.poll() is not None:
                tail = "\n".join(tail_lines(ENGINE_LOG, 20)[-12:])
                return {"ok": False, "message": f"엔진이 시작 중 종료됐습니다. rc={p.returncode}\n{tail[-1600:]}"}
            st = read_json(ENGINE_STATE)
            hb = read_json(HEARTBEAT)
            if int(st.get("pid", 0) or 0) == p.pid and int(hb.get("pid", 0) or 0) == p.pid:
                return {"ok": True, "message": f"개발 시작 완료 · PID {p.pid}"}
            time.sleep(0.15)
        return {"ok": True, "message": f"엔진 PID {p.pid} 실행 중 · 준비 상태를 계속 확인합니다."}

    def recover(self) -> dict[str, Any]:
        stopped = self.stop()
        if not stopped.get("ok"):
            return stopped
        return self.start()

    def update(self) -> dict[str, Any]:
        was_running = engine_info()["running"]
        if was_running or legacy_pids():
            r = self.stop()
            if not r.get("ok"):
                return r
        try:
            r = run_retry(["git", "-c", "core.hooksPath=/dev/null", "fetch", "origin", "master"], cwd=REPO, capture_output=True, text=True, timeout=180, encoding="utf-8", errors="replace")
            if r.returncode != 0:
                return {"ok": False, "message": (r.stderr or r.stdout or "git fetch 실패")[-1800:]}
            arc = run_retry(["git", "archive", "--format=tar", "origin/master", *INFRA_PATHS], cwd=REPO, capture_output=True, timeout=60)
            if arc.returncode != 0:
                return {"ok": False, "message": arc.stderr.decode("utf-8", "replace")[-1800:]}
            with tarfile.open(fileobj=io.BytesIO(arc.stdout), mode="r:") as tf:
                tf.extractall(REPO)
            log = SERVER_LOG.open("a", encoding="utf-8")
            popen_retry([sys.executable, str(HERE / "restart_server.py"), str(os.getpid()), str(PORT), "1" if was_running else "0"], cwd=REPO, stdout=log, stderr=log, start_new_session=True, env=os.environ.copy())
        except Exception as e:
            if was_running:
                self.start()
            return {"ok": False, "message": f"{type(e).__name__}: {e}"}
        return {"ok": True, "message": "AutoDev 시스템 업데이트 완료.", "restarting": True}

    def status(self) -> dict[str, Any]:
        now = time.time()
        info = engine_info()
        hb = read_json(HEARTBEAT)
        sp = configured_state_path()
        st = read_json(sp) if sp.exists() else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked_raw = st.get("blocked") if isinstance(st.get("blocked"), list) else []
        current = next((x for x in tasks if isinstance(x, dict) and x.get("status") == "working"), None)
        if current is None:
            current = next((x for x in tasks if isinstance(x, dict) and x.get("status") in {"pending", "waiting_verification"}), tasks[0] if tasks else {})
        try:
            hb_at = float(hb.get("heartbeat_at", 0) or 0)
        except Exception:
            hb_at = 0
        hb_age = int(max(0, now - hb_at)) if hb_at else None
        if time.time() - self._cli_checked > 30:
            self._cli_cache = {"grok": cli_status("grok"), "codex": cli_status("codex")}
            self._cli_checked = time.time()
        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        errs = []
        for line in reversed(tail_lines(ENGINE_LOG, 400)):
            if ERROR_RE.search(line):
                errs.append({"time": "로그", "text": line[-1600:]})
            if len(errs) >= 6:
                break
        errs.reverse()
        if info["stale"]:
            errs.append({"time": "버전", "text": "실행 중 엔진이 현재 AutoDev 시스템 버전과 다릅니다."})
        if info["running"] and hb_age is not None and hb_age > 180:
            errs.append({"time": "심박", "text": "엔진 PID는 살아 있지만 심박이 3분 이상 없습니다."})
        return {
            "ok": True, "checked_at": now, "running": info["running"], "pid": info["pid"], "started_at": info["started_at"],
            "quiet_seconds": None, "heartbeat_age": hb_age, "stage": str(hb.get("message", "") or "개발 중")[:100],
            "last_log": str(hb.get("message", ""))[-800:], "goal": str(st.get("goal", "")),
            "current": current if isinstance(current, dict) else {},
            "queue_count": len(tasks), "completed_count": len(completed), "blocked_count": len(blocked_raw),
            "blocked_items": [], "recent_errors": errs, "issue_count": len(errs) + len(blocked_raw),
            "engine": info, "control_version": CONTROL_PROTOCOL,
            "stats": {k: int(stats.get(k, 0) or 0) for k in ("grok_calls", "codex_calls", "director_calls", "tasks_done", "tasks_blocked")},
            "grok_quota": quota_status("grok"), "codex_quota": quota_status("codex"), "codex_usage": cached_codex_usage(),
            "grok_cli": self._cli_cache.get("grok", {}), "codex_cli": self._cli_cache.get("codex", {}),
            "git": {"branch": "master", "head": control_fingerprint(HERE), "dirty_count": 0}, "state_file": str(sp),
        }

    def log_rows(self, after: int) -> dict[str, Any]:
        lines = tail_lines(ENGINE_LOG, 1000)
        rows = []
        for i, line in enumerate(lines, 1):
            if i <= after:
                continue
            m = re.match(r"(\d{2}:\d{2}:\d{2})\s+(.*)$", line)
            rows.append({"seq": i, "time": m.group(1) if m else "", "text": m.group(2) if m else line})
        return {"rows": rows[-500:], "last_seq": len(lines)}
