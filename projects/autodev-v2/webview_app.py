#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 localhost dashboard server.

ELI5 원칙:
- 사용자는 '지금 잘 도는가 / 뭘 하는가 / 얼마나 남았나 / 뭐가 막혔나'만 먼저 본다.
- 모든 숫자는 실제 PID/state/git/quota/CLI에서 다시 읽는다.
- Codex 5시간/주간 사용량은 공식 app-server rate-limit 응답을 사용한다.
"""
from __future__ import annotations

import atexit
import json
import os
import re
import secrets
import shlex
import shutil
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

from codex_usage import cached_codex_usage, refresh_codex_usage

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
HTML_FILE = HERE / "dashboard.html"
SERVER_STATE = OUTPUT / "html_server.json"
ENGINE_STATE = OUTPUT / "html_engine.json"
CONFIG_FILE = HERE / "config.json"
SERVER_LOG = Path.home() / "Library" / "Logs" / "AutoDevV2-HTML.log"
QUOTA_FILES = {
    "grok": OUTPUT / "grok_quota_exhausted.json",
    "codex": OUTPUT / "codex_quota_exhausted.json",
}
QUOTA_COOLDOWNS = {"grok": 3600, "codex": 300}
HOST = "127.0.0.1"
PORT = int(os.environ.get("AUTODEV_HTML_PORT", "8765"))
ERROR_RE = re.compile(
    r"(error|exception|traceback|failed|failure|timeout|blocked|quota|402|rc=[1-9]|실패|오류|막힘|한도\s*소진)",
    re.IGNORECASE,
)


def read_json(path: Path) -> dict[str, Any]:
    try:
        v = json.loads(path.read_text(encoding="utf-8"))
        return v if isinstance(v, dict) else {}
    except Exception:
        return {}


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
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


def engine_info() -> dict[str, Any]:
    data = read_json(ENGINE_STATE)
    try:
        pid = int(data.get("pid", 0) or 0)
    except Exception:
        pid = 0
    if pid and pid_alive(pid):
        return {
            "running": True,
            "pid": pid,
            "started_at": float(data.get("started_at", 0) or 0),
        }
    if ENGINE_STATE.exists():
        try:
            ENGINE_STATE.unlink()
        except OSError:
            pass
    return {"running": False, "pid": None, "started_at": 0.0}


def configured_state_path() -> Path:
    cfg = read_json(CONFIG_FILE)
    raw = str(cfg.get("state_file", "output/autodev_v2/ashes-to-stars/state.json"))
    p = Path(raw)
    return p if p.is_absolute() else (REPO / p).resolve()


def state_path() -> Path | None:
    configured = configured_state_path()
    if configured.exists():
        return configured
    for p in (OUTPUT / "ashes-to-stars" / "state.json", OUTPUT / "ashes_to_stars" / "state.json"):
        if p.exists():
            return p
    try:
        hits = sorted(OUTPUT.glob("*/state.json"))
        return hits[0] if hits else None
    except Exception:
        return None


def quota_status(provider: str) -> dict[str, Any]:
    path = QUOTA_FILES[provider]
    data = read_json(path)
    try:
        ts = float(data.get("detected_at", 0) or 0)
    except Exception:
        ts = 0.0
    age = max(0, int(time.time() - ts)) if ts else None
    cooldown = QUOTA_COOLDOWNS[provider]
    active = bool(ts and age is not None and age < cooldown)
    return {
        "detected": bool(ts),
        "active": active,
        "remaining_seconds": max(0, cooldown - int(age or 0)) if active else 0,
        "reason": str(data.get("reason", "")),
    }


def find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (
        f"/opt/homebrew/bin/{name}", f"/usr/local/bin/{name}",
        str(Path.home() / ".local" / "bin" / name),
    ):
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
            r = subprocess.run([exe, *args], cwd=REPO, capture_output=True, text=True,
                               encoding="utf-8", errors="replace", timeout=4)
            out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and out:
                version = out.splitlines()[0][:160]
                break
        except Exception:
            pass
    return {"installed": True, "path": exe, "version": version}


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.35):
            return True
    except OSError:
        return False


def norm_blocked(item: Any) -> dict[str, Any]:
    if not isinstance(item, dict):
        return {"id": "", "title": str(item), "reason": "원인을 기록하지 못했습니다."}
    reason = item.get("last_error") or item.get("error") or item.get("reason") or item.get("blocked_reason") or item.get("message") or "원인을 기록하지 못했습니다."
    return {
        "id": str(item.get("id", "")),
        "title": str(item.get("title") or item.get("goal") or "이름 없는 작업"),
        "reason": str(reason)[-2500:],
    }


def friendly_stage(text: str, running: bool) -> str:
    s = (text or "").lower()
    if not running:
        return "쉬는 중"
    if "codex" in s:
        return "Codex가 코드를 고치는 중"
    if "grok" in s:
        return "Grok이 코드를 고치는 중"
    if "verify" in s or "검증" in s or "compile" in s or "build" in s:
        return "고친 코드가 제대로 되는지 검사 중"
    if "director" in s or "계획" in s or "task" in s and "생성" in s:
        return "다음에 할 일을 고르는 중"
    if "git" in s:
        return "변경된 코드를 정리하는 중"
    return "개발 작업 진행 중"


def launch_replacement_server(resume_engine: bool) -> tuple[bool, str]:
    try:
        SERVER_LOG.parent.mkdir(parents=True, exist_ok=True)
        env = os.environ.copy()
        env["AUTODEV_RESUME_ENGINE"] = "1" if resume_engine else "0"
        env["PYTHONUNBUFFERED"] = "1"
        command = "sleep 1; exec " + shlex.quote(sys.executable) + " " + shlex.quote(str(HERE / "webview_app.py"))
        log = SERVER_LOG.open("a", encoding="utf-8")
        subprocess.Popen(["/bin/sh", "-c", command], cwd=REPO, stdin=subprocess.DEVNULL,
                         stdout=log, stderr=subprocess.STDOUT, start_new_session=True, env=env)
        log.close()
        return True, ""
    except Exception as e:
        return False, f"{type(e).__name__}: {e}"


class Controller:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self.proc: subprocess.Popen[str] | None = None
        self.logs: deque[dict[str, Any]] = deque(maxlen=3000)
        self.seq = 0
        self.last_exit: int | None = None
        self._cli_cache: dict[str, Any] = {}
        self._cli_checked = 0.0

    def log(self, text: str) -> None:
        with self.lock:
            self.seq += 1
            self.logs.append({"seq": self.seq, "time": time.strftime("%H:%M:%S"), "ts": time.time(), "text": text.rstrip()})

    def running(self) -> bool:
        return bool(engine_info()["running"])

    def _reader(self, proc: subprocess.Popen[str]) -> None:
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                self.log(line.rstrip("\n"))
        except Exception as e:
            self.log(f"[HTML] 로그 읽기 오류: {type(e).__name__}: {e}")
        finally:
            rc = proc.poll()
            if rc is None:
                try:
                    rc = proc.wait(timeout=2)
                except Exception:
                    rc = None
            with self.lock:
                if self.proc is proc:
                    self.proc = None
                self.last_exit = rc
            state = read_json(ENGINE_STATE)
            if int(state.get("pid", 0) or 0) == proc.pid:
                try:
                    ENGINE_STATE.unlink()
                except OSError:
                    pass
            self.log(f"[HTML] AutoDev 종료 rc={rc}")

    def start(self) -> dict[str, Any]:
        info = engine_info()
        if info["running"]:
            return {"ok": True, "message": f"이미 개발 중입니다. PID {info['pid']}"}
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env["PATH"] = f"{Path.home() / '.local/bin'}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
        try:
            p = subprocess.Popen([sys.executable, str(HERE / "start.py")], cwd=REPO,
                                 stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                 encoding="utf-8", errors="replace", bufsize=1, env=env,
                                 start_new_session=True)
        except Exception as e:
            self.log(f"[HTML] 시작 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}
        with self.lock:
            self.proc = p
            self.last_exit = None
        write_json(ENGINE_STATE, {"pid": p.pid, "started_at": time.time()})
        self.log(f"[HTML] AutoDev 시작 PID {p.pid}")
        threading.Thread(target=self._reader, args=(p,), daemon=True).start()
        return {"ok": True, "message": "자율 개발을 시작했습니다."}

    def stop(self) -> dict[str, Any]:
        info = engine_info()
        pid = info.get("pid")
        if not pid:
            return {"ok": True, "message": "이미 쉬고 있습니다."}
        self.log(f"[HTML] AutoDev 중지 요청 PID {pid}")
        try:
            try:
                os.killpg(int(pid), signal.SIGTERM)
            except ProcessLookupError:
                pass
            deadline = time.time() + 5
            while pid_alive(int(pid)) and time.time() < deadline:
                time.sleep(0.1)
            if pid_alive(int(pid)):
                try:
                    os.killpg(int(pid), signal.SIGKILL)
                except ProcessLookupError:
                    pass
                deadline = time.time() + 2
                while pid_alive(int(pid)) and time.time() < deadline:
                    time.sleep(0.1)
            if pid_alive(int(pid)):
                return {"ok": False, "message": f"프로세스 {pid}를 끄지 못했습니다."}
            try:
                ENGINE_STATE.unlink()
            except OSError:
                pass
            with self.lock:
                if self.proc is not None and self.proc.pid == pid:
                    self.proc = None
            self.log(f"[HTML] AutoDev 중지 완료 PID {pid}")
            return {"ok": True, "message": "자율 개발을 멈췄습니다."}
        except Exception as e:
            self.log(f"[HTML] 중지 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def clear_quota(self, provider: str) -> dict[str, Any]:
        p = QUOTA_FILES.get(provider)
        if p is None:
            return {"ok": False, "message": "알 수 없는 AI입니다."}
        try:
            if p.exists():
                p.unlink()
            self.log(f"[QUOTA] {provider} 오래된 한도 기록 삭제")
            return {"ok": True, "message": f"{provider.upper()} 한도를 다음 실제 호출에서 다시 확인합니다."}
        except Exception as e:
            return {"ok": False, "message": str(e)}

    def _cli(self, force: bool = False) -> dict[str, Any]:
        if force or time.time() - self._cli_checked > 30 or not self._cli_cache:
            self._cli_cache = {"grok": cli_status("grok"), "codex": cli_status("codex")}
            self._cli_checked = time.time()
        return self._cli_cache

    def refresh_codex_meter(self) -> dict[str, Any]:
        self.log("[UPDATE] Codex 5시간/주간 실제 사용량 확인 중")
        snap = refresh_codex_usage()
        if snap.get("ok"):
            names = ", ".join(f"{x.get('name')} {x.get('remaining_percent')}% 남음" for x in snap.get("windows", []))
            self.log("[UPDATE] Codex 사용량 확인 완료 · " + names)
        else:
            self.log("[UPDATE] Codex 사용량 확인 실패 · " + str(snap.get("error", "알 수 없는 이유")))
        return snap

    def update(self) -> dict[str, Any]:
        was_running = self.running()
        if was_running:
            stopped = self.stop()
            if not stopped.get("ok"):
                return {"ok": False, "message": "업데이트 전에 개발 엔진을 멈추지 못했습니다."}
        self.clear_quota("codex")
        self.log("[UPDATE] 최신 코드 확인 중")
        try:
            r = subprocess.run(["git", "-c", "core.hooksPath=/dev/null", "pull", "--ff-only"],
                               cwd=REPO, capture_output=True, text=True, encoding="utf-8",
                               errors="replace", timeout=120)
            out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            for line in out.splitlines()[-80:]:
                self.log("[UPDATE] " + line)
            if r.returncode != 0:
                if was_running:
                    self.start()
                return {"ok": False, "message": out[-1200:] or "코드 업데이트 실패"}

            self._cli(force=True)
            self.refresh_codex_meter()
            snap = self.status()
            self.log(f"[UPDATE] 전체 상태 새로 읽음 · 남은 일 {snap['queue_count']} / 완료 {snap['completed_count']} / 막힘 {snap['blocked_count']}")
            ok, err = launch_replacement_server(was_running)
            if not ok:
                if was_running:
                    self.start()
                return {"ok": False, "message": "코드는 최신이지만 화면 재시작 실패: " + err}
            return {"ok": True, "message": "최신 코드와 실제 상태를 모두 다시 읽었습니다.", "restarting": True}
        except Exception as e:
            if was_running:
                self.start()
            self.log(f"[UPDATE] 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def recent_errors(self, limit: int = 10) -> list[dict[str, Any]]:
        with self.lock:
            rows = [x for x in self.logs if ERROR_RE.search(str(x.get("text", "")))]
        result: list[dict[str, Any]] = []
        seen: set[str] = set()
        for row in reversed(rows):
            text = str(row.get("text", "")).strip()
            key = text[-500:]
            if not text or key in seen:
                continue
            seen.add(key)
            result.append({"time": row.get("time", ""), "text": text[-1800:]})
            if len(result) >= limit:
                break
        result.reverse()
        return result

    def status(self) -> dict[str, Any]:
        now = time.time()
        expected = configured_state_path()
        sp = state_path()
        state_exists = bool(sp and sp.exists())
        st = read_json(sp) if state_exists and sp else {}
        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked_raw = st.get("blocked") if isinstance(st.get("blocked"), list) else []
        blocked = [norm_blocked(x) for x in blocked_raw[-8:]]
        current = next((x for x in tasks if isinstance(x, dict) and x.get("status") == "working"), None)
        if current is None:
            current = next((x for x in tasks if isinstance(x, dict) and x.get("status") == "pending"), tasks[0] if tasks else {})

        try:
            branch = subprocess.run(["git", "branch", "--show-current"], cwd=REPO, capture_output=True, text=True, timeout=3).stdout.strip()
            head = subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=REPO, capture_output=True, text=True, timeout=3).stdout.strip()
            dirty = len(subprocess.run(["git", "status", "--porcelain"], cwd=REPO, capture_output=True, text=True, timeout=4).stdout.splitlines())
        except Exception:
            branch, head, dirty = "?", "?", -1

        info = engine_info()
        with self.lock:
            last_log = self.logs[-1] if self.logs else None
            last_exit = self.last_exit
        state_mtime = sp.stat().st_mtime if state_exists and sp else 0
        last_activity = max(float(last_log.get("ts", 0)) if last_log else 0, state_mtime, float(info.get("started_at", 0) or 0))
        quiet_seconds = int(max(0, now - last_activity)) if info["running"] and last_activity else None
        last_text = str(last_log.get("text", "")) if last_log else ""

        recent_errors = self.recent_errors()
        if not state_exists:
            recent_errors.append({"time": "상태", "text": f"개발 상태 파일을 찾지 못했습니다: {expected}"})
        elif not st:
            recent_errors.append({"time": "상태", "text": f"개발 상태 파일을 읽지 못했습니다: {sp}"})

        cli = self._cli()
        grok_q = quota_status("grok")
        codex_q = quota_status("codex")
        usage = cached_codex_usage()
        issue_count = len(blocked) + len(recent_errors)
        if grok_q["active"] or codex_q["active"]:
            issue_count += 1
        if not cli.get("grok", {}).get("installed") or not cli.get("codex", {}).get("installed"):
            issue_count += 1
        if last_exit not in (None, 0) and not info["running"]:
            issue_count += 1

        return {
            "ok": True,
            "checked_at": now,
            "running": info["running"],
            "pid": info["pid"],
            "started_at": info["started_at"],
            "last_exit": last_exit,
            "last_activity_at": last_activity or None,
            "quiet_seconds": quiet_seconds,
            "stage": friendly_stage(last_text, info["running"]),
            "last_log": last_text[-800:],
            "goal": str(st.get("goal", "")),
            "current": current if isinstance(current, dict) else {},
            "queue_count": len(tasks),
            "completed_count": len(completed),
            "blocked_count": len(blocked_raw),
            "blocked_items": blocked,
            "recent_errors": recent_errors,
            "issue_count": issue_count,
            "stats": {k: int(stats.get(k, 0) or 0) for k in ("grok_calls", "codex_calls", "director_local_calls", "tasks_done", "tasks_blocked")},
            "grok_quota": grok_q,
            "codex_quota": codex_q,
            "codex_usage": usage,
            "grok_cli": cli.get("grok", {}),
            "codex_cli": cli.get("codex", {}),
            "git": {"branch": branch or "?", "head": head or "?", "dirty_count": dirty},
            "state_file": str(sp) if sp else str(expected),
        }

    def log_rows(self, after: int) -> dict[str, Any]:
        with self.lock:
            rows = [x for x in self.logs if int(x["seq"]) > after]
            return {"rows": rows[-500:], "last_seq": self.seq}


CTRL = Controller()
TOKEN = secrets.token_urlsafe(24)


class Handler(BaseHTTPRequestHandler):
    server_version = "AutoDevELI5/4"

    def log_message(self, fmt: str, *args: Any) -> None:
        return

    def _json(self, obj: Any, code: int = 200) -> None:
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _authorized(self) -> bool:
        parsed = urllib.parse.urlparse(self.path)
        query = urllib.parse.parse_qs(parsed.query)
        supplied = self.headers.get("X-AutoDev-Token", "") or query.get("token", [""])[0]
        return secrets.compare_digest(supplied, TOKEN)

    def do_GET(self) -> None:
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/":
            supplied = urllib.parse.parse_qs(parsed.query).get("token", [""])[0]
            if not secrets.compare_digest(supplied, TOKEN):
                self.send_response(302)
                self.send_header("Location", f"/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                return
            try:
                data = HTML_FILE.read_bytes()
            except Exception as e:
                data = f"<h1>화면 파일을 읽지 못했습니다</h1><pre>{e}</pre>".encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return
        if not self._authorized():
            self._json({"ok": False, "message": "대시보드 인증 오류"}, 403); return
        if parsed.path == "/api/status":
            self._json(CTRL.status()); return
        if parsed.path == "/api/logs":
            try:
                after = int(urllib.parse.parse_qs(parsed.query).get("after", ["0"])[0])
            except Exception:
                after = 0
            self._json(CTRL.log_rows(after)); return
        self._json({"ok": False, "message": "없는 주소입니다."}, 404)

    def do_POST(self) -> None:
        if not self._authorized():
            self._json({"ok": False, "message": "대시보드 인증 오류"}, 403); return
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/start": self._json(CTRL.start()); return
        if path == "/api/stop": self._json(CTRL.stop()); return
        if path == "/api/update":
            result = CTRL.update(); self._json(result)
            if result.get("restarting"):
                threading.Timer(0.35, lambda: os._exit(0)).start()
            return
        if path == "/api/quota/codex/clear": self._json(CTRL.clear_quota("codex")); return
        if path == "/api/quota/grok/clear": self._json(CTRL.clear_quota("grok")); return
        if path == "/api/open-repo":
            try:
                subprocess.Popen(["open", str(REPO)]); self._json({"ok": True, "message": "폴더를 열었습니다."})
            except Exception as e:
                self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/clear-logs":
            with CTRL.lock: CTRL.logs.clear()
            self._json({"ok": True, "message": "로그를 비웠습니다."}); return
        self._json({"ok": False, "message": "없는 기능입니다."}, 404)


def write_server_state() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write_json(SERVER_STATE, {"pid": os.getpid(), "port": PORT, "token": TOKEN})


def cleanup() -> None:
    try:
        if SERVER_STATE.exists() and read_json(SERVER_STATE).get("pid") == os.getpid():
            SERVER_STATE.unlink()
    except Exception:
        pass


def open_existing_if_any() -> bool:
    old = read_json(SERVER_STATE)
    try:
        port = int(old.get("port", PORT) or PORT)
    except Exception:
        port = PORT
    token = str(old.get("token", ""))
    if token and server_alive(port):
        webbrowser.open(f"http://{HOST}:{port}/?token={urllib.parse.quote(token)}&r={int(time.time())}")
        return True
    return False


def main() -> int:
    if open_existing_if_any():
        return 0
    try:
        server = ThreadingHTTPServer((HOST, PORT), Handler)
    except OSError:
        webbrowser.open(f"http://{HOST}:{PORT}/")
        return 0
    write_server_state()
    atexit.register(cleanup)
    url = f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"
    CTRL.log(f"[HTML] 대시보드 {url}")

    resume = os.environ.pop("AUTODEV_RESUME_ENGINE", "0").strip() == "1"
    if resume and not CTRL.running():
        CTRL.log("[UPDATE] 업데이트 전 개발 상태를 다시 이어갑니다.")
        threading.Timer(1.0, CTRL.start).start()

    if not cached_codex_usage() and find_cli("codex"):
        threading.Thread(target=CTRL.refresh_codex_meter, daemon=True).start()

    threading.Timer(0.45, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever(poll_interval=0.5)
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        cleanup()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
