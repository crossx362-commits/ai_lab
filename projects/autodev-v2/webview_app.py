#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 localhost HTML dashboard server.

No pywebview dependency. The already-installed macOS app launcher can keep
calling this file; it opens a normal browser at a localhost-only dashboard.
"""
from __future__ import annotations

import atexit
import json
import os
import secrets
import signal
import socket
import subprocess
import sys
import threading
import time
import urllib.parse
import webbrowser
from collections import deque
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
HTML_FILE = HERE / "dashboard.html"
SERVER_STATE = OUTPUT / "html_server.json"
QUOTA_FILES = {
    "grok": OUTPUT / "grok_quota_exhausted.json",
    "codex": OUTPUT / "codex_quota_exhausted.json",
}
PORT = int(os.environ.get("AUTODEV_HTML_PORT", "8765"))
HOST = "127.0.0.1"
COOLDOWN_SECONDS = 3600


def read_json(path: Path) -> dict[str, Any]:
    try:
        v = json.loads(path.read_text(encoding="utf-8"))
        return v if isinstance(v, dict) else {}
    except Exception:
        return {}


def state_path() -> Path | None:
    for p in (OUTPUT / "ashes-to-stars" / "state.json", OUTPUT / "ashes_to_stars" / "state.json"):
        if p.exists():
            return p
    hits = sorted(OUTPUT.glob("*/state.json"))
    return hits[0] if hits else None


def quota_status(path: Path) -> dict[str, Any]:
    d = read_json(path)
    ts = float(d.get("detected_at", 0) or 0)
    age = max(0, int(time.time() - ts)) if ts else None
    return {
        "detected": bool(ts),
        "active": bool(ts and age is not None and age < COOLDOWN_SECONDS),
        "age_seconds": age,
        "reason": str(d.get("reason", "")),
    }


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.35):
            return True
    except OSError:
        return False


class Controller:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self.proc: subprocess.Popen[str] | None = None
        self.logs: deque[dict[str, Any]] = deque(maxlen=2200)
        self.seq = 0
        self.last_exit: int | None = None

    def log(self, text: str) -> None:
        with self.lock:
            self.seq += 1
            self.logs.append({"seq": self.seq, "time": time.strftime("%H:%M:%S"), "text": text.rstrip()})

    def running(self) -> bool:
        return self.proc is not None and self.proc.poll() is None

    def _reader(self, proc: subprocess.Popen[str]) -> None:
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                self.log(line.rstrip("\n"))
        finally:
            rc = proc.poll()
            if rc is None:
                try:
                    rc = proc.wait(timeout=2)
                except Exception:
                    rc = None
            with self.lock:
                if self.proc is proc:
                    self.last_exit = rc
                    self.proc = None
            self.log(f"[HTML] AutoDev 종료 rc={rc}")

    def start(self) -> dict[str, Any]:
        with self.lock:
            if self.running():
                return {"ok": True, "message": "이미 실행 중입니다."}
            env = os.environ.copy()
            env["PYTHONUNBUFFERED"] = "1"
            env["PATH"] = f"{Path.home() / '.local/bin'}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
            try:
                p = subprocess.Popen(
                    [sys.executable, str(HERE / "start.py")], cwd=REPO,
                    stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                    text=True, encoding="utf-8", errors="replace", bufsize=1,
                    env=env, start_new_session=True,
                )
            except Exception as e:
                self.log(f"[HTML] 시작 실패: {type(e).__name__}: {e}")
                return {"ok": False, "message": str(e)}
            self.proc = p
            self.last_exit = None
            self.log(f"[HTML] AutoDev 시작 PID {p.pid}")
            threading.Thread(target=self._reader, args=(p,), daemon=True).start()
            return {"ok": True, "message": f"시작됨 PID {p.pid}"}

    def stop(self) -> dict[str, Any]:
        with self.lock:
            p = self.proc
            if p is None or p.poll() is not None:
                self.proc = None
                return {"ok": True, "message": "이미 중지되어 있습니다."}
            pid = p.pid
        try:
            os.killpg(pid, signal.SIGTERM)
            try:
                p.wait(timeout=5)
            except subprocess.TimeoutExpired:
                os.killpg(pid, signal.SIGKILL)
            self.log(f"[HTML] AutoDev 중지 PID {pid}")
            return {"ok": True, "message": "중지했습니다."}
        except Exception as e:
            self.log(f"[HTML] 중지 실패: {e}")
            return {"ok": False, "message": str(e)}

    def update(self) -> dict[str, Any]:
        if self.running():
            return {"ok": False, "message": "업데이트 전에 AutoDev를 중지하세요."}
        self.log("[UPDATE] Git hook 없이 fast-forward 업데이트")
        try:
            r = subprocess.run(
                ["git", "-c", "core.hooksPath=/dev/null", "pull", "--ff-only"],
                cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120,
            )
            out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            for line in out.splitlines()[-100:]:
                self.log("[UPDATE] " + line)
            return {"ok": r.returncode == 0, "message": out[-1500:] or "업데이트 완료", "restart_recommended": r.returncode == 0}
        except Exception as e:
            self.log(f"[UPDATE] 실패: {e}")
            return {"ok": False, "message": str(e)}

    def status(self) -> dict[str, Any]:
        sp = state_path()
        st = read_json(sp) if sp else {}
        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked = st.get("blocked") if isinstance(st.get("blocked"), list) else []
        current = next((x for x in tasks if x.get("status") == "working"), None)
        if current is None:
            current = next((x for x in tasks if x.get("status") == "pending"), tasks[0] if tasks else {})
        try:
            branch = subprocess.run(["git", "branch", "--show-current"], cwd=REPO, capture_output=True, text=True, timeout=4).stdout.strip()
            dirty = len(subprocess.run(["git", "status", "--porcelain"], cwd=REPO, capture_output=True, text=True, timeout=6).stdout.splitlines())
        except Exception:
            branch, dirty = "?", -1
        with self.lock:
            p = self.proc
            running = self.running()
            return {
                "running": running, "pid": p.pid if running and p else None, "last_exit": self.last_exit,
                "goal": str(st.get("goal", "")), "current": current or {},
                "queue_count": len(tasks), "completed_count": len(completed), "blocked_count": len(blocked),
                "stats": {k: int(stats.get(k, 0) or 0) for k in ("grok_calls", "codex_calls", "director_calls", "director_local_calls", "tasks_done", "tasks_blocked")},
                "grok_quota": quota_status(QUOTA_FILES["grok"]), "codex_quota": quota_status(QUOTA_FILES["codex"]),
                "git": {"branch": branch or "?", "dirty_count": dirty},
            }

    def log_rows(self, after: int) -> dict[str, Any]:
        with self.lock:
            rows = [x for x in self.logs if int(x["seq"]) > after]
            return {"rows": rows[-500:], "last_seq": self.seq}


CTRL = Controller()
TOKEN = secrets.token_urlsafe(24)


class Handler(BaseHTTPRequestHandler):
    server_version = "AutoDevHTML/2"

    def log_message(self, fmt: str, *args: Any) -> None:
        return

    def _json(self, obj: Any, code: int = 200) -> None:
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _authorized(self) -> bool:
        parsed = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(parsed.query)
        supplied = self.headers.get("X-AutoDev-Token", "") or (q.get("token", [""])[0])
        return secrets.compare_digest(supplied, TOKEN)

    def do_GET(self) -> None:
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/":
            try:
                html = HTML_FILE.read_text(encoding="utf-8")
            except Exception as e:
                html = f"<h1>dashboard.html 읽기 실패</h1><pre>{e}</pre>"
            data = html.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers(); self.wfile.write(data); return
        if not self._authorized():
            self._json({"ok": False, "error": "unauthorized"}, 403); return
        if parsed.path == "/api/status": self._json(CTRL.status()); return
        if parsed.path == "/api/logs":
            q = urllib.parse.parse_qs(parsed.query)
            try: after = int(q.get("after", ["0"])[0])
            except Exception: after = 0
            self._json(CTRL.log_rows(after)); return
        self._json({"ok": False, "error": "not found"}, 404)

    def do_POST(self) -> None:
        if not self._authorized():
            self._json({"ok": False, "error": "unauthorized"}, 403); return
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/start": self._json(CTRL.start()); return
        if path == "/api/stop": self._json(CTRL.stop()); return
        if path == "/api/update": self._json(CTRL.update()); return
        if path == "/api/open-repo":
            try:
                subprocess.Popen(["open", str(REPO)])
                self._json({"ok": True})
            except Exception as e: self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/clear-logs":
            with CTRL.lock: CTRL.logs.clear()
            self._json({"ok": True}); return
        self._json({"ok": False, "error": "not found"}, 404)


def write_server_state() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    SERVER_STATE.write_text(json.dumps({"pid": os.getpid(), "port": PORT, "token": TOKEN}, ensure_ascii=False), encoding="utf-8")


def cleanup() -> None:
    try:
        if SERVER_STATE.exists() and read_json(SERVER_STATE).get("pid") == os.getpid(): SERVER_STATE.unlink()
    except Exception: pass


def open_existing_if_any() -> bool:
    old = read_json(SERVER_STATE)
    port = int(old.get("port", PORT) or PORT)
    token = str(old.get("token", ""))
    if token and server_alive(port):
        webbrowser.open(f"http://{HOST}:{port}/?token={urllib.parse.quote(token)}")
        return True
    return False


def main() -> int:
    if open_existing_if_any(): return 0
    try:
        server = ThreadingHTTPServer((HOST, PORT), Handler)
    except OSError:
        webbrowser.open(f"http://{HOST}:{PORT}/")
        return 0
    write_server_state(); atexit.register(cleanup)
    url = f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}"
    threading.Timer(0.4, lambda: webbrowser.open(url)).start()
    CTRL.log(f"[HTML] 대시보드 {url}")
    try: server.serve_forever(poll_interval=0.5)
    except KeyboardInterrupt: pass
    finally: server.server_close(); cleanup()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
