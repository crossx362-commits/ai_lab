#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 localhost HTML dashboard server.

표준 라이브러리만 사용한다.
- 브라우저에서 시작/중지/업데이트/상태/오류 확인
- 엔진 PID를 파일에 기록해 대시보드 재시작 뒤에도 실제 실행 상태를 추적
- 상태 API는 매 요청마다 PID/state/git/quota를 실제 소스에서 다시 읽는다
- 업데이트는 Git hook을 우회하고 Codex quota cache를 재확인 상태로 만든다
- 업데이트 버튼 한 번으로 엔진 중지 -> git pull -> 전체 상태 재수집 -> 대시보드 재시작
- 업데이트 전 엔진이 실행 중이었다면 새 서버에서 자동 재개
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
QUOTA_COOLDOWNS = {
    "grok": 3600,
    "codex": 300,
}
HOST = "127.0.0.1"
PORT = int(os.environ.get("AUTODEV_HTML_PORT", "8765"))
ERROR_RE = re.compile(
    r"(error|exception|traceback|failed|failure|timeout|blocked|quota|402|rc=[1-9]|실패|오류|막힘|한도\s*소진)",
    re.IGNORECASE,
)


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else {}
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


def engine_pid() -> int | None:
    data = read_json(ENGINE_STATE)
    try:
        pid = int(data.get("pid", 0) or 0)
    except Exception:
        pid = 0
    if pid and pid_alive(pid):
        return pid
    if ENGINE_STATE.exists():
        try:
            ENGINE_STATE.unlink()
        except OSError:
            pass
    return None


def configured_state_path() -> Path:
    cfg = read_json(CONFIG_FILE)
    raw = str(cfg.get("state_file", "output/autodev_v2/ashes-to-stars/state.json"))
    p = Path(raw)
    return p if p.is_absolute() else (REPO / p).resolve()


def state_path() -> Path | None:
    configured = configured_state_path()
    if configured.exists():
        return configured
    for p in (
        OUTPUT / "ashes-to-stars" / "state.json",
        OUTPUT / "ashes_to_stars" / "state.json",
    ):
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
    ts = float(data.get("detected_at", 0) or 0)
    age = max(0, int(time.time() - ts)) if ts else None
    cooldown = int(QUOTA_COOLDOWNS[provider])
    active = bool(ts and age is not None and age < cooldown)
    remaining = max(0, cooldown - int(age or 0)) if active else 0
    return {
        "detected": bool(ts),
        "active": active,
        "age_seconds": age,
        "cooldown_seconds": cooldown,
        "remaining_seconds": remaining,
        "reason": str(data.get("reason", "")),
        "source_file": str(path),
    }


def find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (
        f"/usr/local/bin/{name}",
        f"/opt/homebrew/bin/{name}",
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
            r = subprocess.run(
                [exe, *args], cwd=REPO, capture_output=True, text=True,
                encoding="utf-8", errors="replace", timeout=4,
            )
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                version = text.splitlines()[0][:160]
                break
        except Exception:
            continue
    return {"installed": True, "path": exe, "version": version}


def cleanup_expired_quota(provider: str) -> bool:
    q = quota_status(provider)
    if not q.get("detected") or q.get("active"):
        return False
    try:
        QUOTA_FILES[provider].unlink()
        return True
    except OSError:
        return False


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.35):
            return True
    except OSError:
        return False


def norm_blocked(item: Any) -> dict[str, Any]:
    if not isinstance(item, dict):
        return {"id": "", "title": str(item), "reason": "상세 원인 없음"}
    reason = (
        item.get("last_error")
        or item.get("error")
        or item.get("reason")
        or item.get("blocked_reason")
        or item.get("message")
        or "상세 원인 없음"
    )
    return {
        "id": str(item.get("id", "")),
        "title": str(item.get("title") or item.get("goal") or "이름 없는 작업"),
        "reason": str(reason)[-3000:],
        "attempts": int(item.get("attempts", item.get("tries", 0)) or 0),
        "status": str(item.get("status", "blocked")),
        "updated_at": str(item.get("updated_at") or item.get("blocked_at") or ""),
    }


def launch_replacement_server(resume_engine: bool) -> tuple[bool, str]:
    try:
        SERVER_LOG.parent.mkdir(parents=True, exist_ok=True)
        env = os.environ.copy()
        env["AUTODEV_RESUME_ENGINE"] = "1" if resume_engine else "0"
        env["PYTHONUNBUFFERED"] = "1"
        command = (
            "sleep 1; exec "
            + shlex.quote(sys.executable)
            + " "
            + shlex.quote(str(HERE / "webview_app.py"))
        )
        log = SERVER_LOG.open("a", encoding="utf-8")
        subprocess.Popen(
            ["/bin/sh", "-c", command],
            cwd=REPO,
            stdin=subprocess.DEVNULL,
            stdout=log,
            stderr=subprocess.STDOUT,
            start_new_session=True,
            env=env,
        )
        log.close()
        return True, ""
    except Exception as e:
        return False, f"{type(e).__name__}: {e}"


class Controller:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self.proc: subprocess.Popen[str] | None = None
        self.logs: deque[dict[str, Any]] = deque(maxlen=2500)
        self.seq = 0
        self.last_exit: int | None = None

    def log(self, text: str) -> None:
        with self.lock:
            self.seq += 1
            self.logs.append({
                "seq": self.seq,
                "time": time.strftime("%H:%M:%S"),
                "text": text.rstrip(),
            })

    def running(self) -> bool:
        return engine_pid() is not None

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
                    self.last_exit = rc
                    self.proc = None
            state = read_json(ENGINE_STATE)
            if int(state.get("pid", 0) or 0) == proc.pid:
                try:
                    ENGINE_STATE.unlink()
                except OSError:
                    pass
            self.log(f"[HTML] AutoDev 종료 rc={rc}")

    def start(self) -> dict[str, Any]:
        with self.lock:
            existing = engine_pid()
            if existing:
                return {"ok": True, "message": f"이미 실행 중입니다. PID {existing}"}
            env = os.environ.copy()
            env["PYTHONUNBUFFERED"] = "1"
            env["PATH"] = (
                f"{Path.home() / '.local/bin'}:/opt/homebrew/bin:/usr/local/bin:"
                "/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
            )
            try:
                p = subprocess.Popen(
                    [sys.executable, str(HERE / "start.py")],
                    cwd=REPO,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    bufsize=1,
                    env=env,
                    start_new_session=True,
                )
            except Exception as e:
                self.log(f"[HTML] 시작 실패: {type(e).__name__}: {e}")
                return {"ok": False, "message": str(e)}
            self.proc = p
            self.last_exit = None
            write_json(ENGINE_STATE, {"pid": p.pid, "started_at": time.time()})
            self.log(f"[HTML] AutoDev 시작 PID {p.pid}")
            threading.Thread(target=self._reader, args=(p,), daemon=True).start()
            return {"ok": True, "message": f"시작됨 PID {p.pid}"}

    def stop(self) -> dict[str, Any]:
        pid = engine_pid()
        if not pid:
            return {"ok": True, "message": "이미 중지되어 있습니다."}

        self.log(f"[HTML] AutoDev 중지 요청 PID {pid}")
        try:
            try:
                os.killpg(pid, signal.SIGTERM)
            except ProcessLookupError:
                pass

            deadline = time.time() + 5
            while pid_alive(pid) and time.time() < deadline:
                time.sleep(0.1)
            if pid_alive(pid):
                try:
                    os.killpg(pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                deadline = time.time() + 2
                while pid_alive(pid) and time.time() < deadline:
                    time.sleep(0.1)

            if pid_alive(pid):
                return {"ok": False, "message": f"PID {pid} 종료 실패"}

            try:
                ENGINE_STATE.unlink()
            except OSError:
                pass
            with self.lock:
                if self.proc is not None and self.proc.pid == pid:
                    self.proc = None
            self.log(f"[HTML] AutoDev 중지 완료 PID {pid}")
            return {"ok": True, "message": "중지했습니다."}
        except Exception as e:
            self.log(f"[HTML] 중지 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def clear_quota(self, provider: str) -> dict[str, Any]:
        if provider not in QUOTA_FILES:
            return {"ok": False, "message": "알 수 없는 provider"}
        p = QUOTA_FILES[provider]
        try:
            if p.exists():
                p.unlink()
            self.log(f"[QUOTA] {provider} 한도 대기 기록 해제")
            return {"ok": True, "message": f"{provider.upper()} 한도 대기 기록을 해제했습니다. 다음 실제 호출에서 재확인합니다."}
        except Exception as e:
            return {"ok": False, "message": str(e)}

    def update(self) -> dict[str, Any]:
        resume_engine = self.running()
        if resume_engine:
            stopped = self.stop()
            if not stopped.get("ok"):
                return {"ok": False, "message": "AutoDev 중지 실패: " + str(stopped.get("message", ""))}

        # 업데이트 자체가 전체 실시간 갱신 트리거다.
        # Codex는 짧은 보호 상태를 지우고 다음 실제 호출에서 현재 한도를 재확인한다.
        self.clear_quota("codex")
        if cleanup_expired_quota("grok"):
            self.log("[UPDATE] 만료된 Grok quota 기록 정리")

        self.log("[UPDATE] AutoDev 중지/실제 PID 확인 완료")
        self.log("[UPDATE] Git hook 없이 fast-forward 업데이트")
        try:
            r = subprocess.run(
                ["git", "-c", "core.hooksPath=/dev/null", "pull", "--ff-only"],
                cwd=REPO,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=120,
            )
            out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            for line in out.splitlines()[-100:]:
                self.log("[UPDATE] " + line)
            if r.returncode != 0:
                if resume_engine:
                    self.log("[UPDATE] 실패해 기존 AutoDev를 다시 시작합니다.")
                    self.start()
                return {"ok": False, "message": out[-1500:] or "업데이트 실패"}

            # pull 직후 현재 시스템 상태를 실제 소스에서 다시 읽어 로그에 남긴다.
            snap = self.status()
            self.log(
                "[UPDATE] 실시간 상태 재수집 완료 · "
                f"queue={snap['queue_count']} done={snap['completed_count']} "
                f"blocked={snap['blocked_count']} git={snap['git']['branch']} "
                f"grok={'wait' if snap['grok_quota']['active'] else 'ready'} "
                f"codex={'wait' if snap['codex_quota']['active'] else 'ready'}"
            )

            ok, err = launch_replacement_server(resume_engine)
            if not ok:
                if resume_engine:
                    self.start()
                self.log("[UPDATE] 대시보드 재시작 예약 실패: " + err)
                return {"ok": False, "message": "코드는 업데이트됐지만 대시보드 재시작 실패: " + err}

            self.log("[UPDATE] 새 코드로 대시보드 재시작 예약")
            return {
                "ok": True,
                "message": "전체 실시간 갱신 완료. 새 코드/상태로 대시보드를 다시 엽니다.",
                "restarting": True,
                "resume_engine": resume_engine,
            }
        except Exception as e:
            if resume_engine:
                self.start()
            self.log(f"[UPDATE] 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def recent_errors(self, limit: int = 12) -> list[dict[str, Any]]:
        with self.lock:
            candidates = [x for x in self.logs if ERROR_RE.search(str(x.get("text", "")))]
        seen: set[str] = set()
        result: list[dict[str, Any]] = []
        for row in reversed(candidates):
            text = str(row.get("text", "")).strip()
            key = text[-500:]
            if not text or key in seen:
                continue
            seen.add(key)
            result.append({"time": row.get("time", ""), "text": text[-2500:]})
            if len(result) >= limit:
                break
        result.reverse()
        return result

    def status(self) -> dict[str, Any]:
        # 중요: 캐시된 dashboard 값을 쓰지 않고 매번 실제 소스에서 다시 읽는다.
        checked_at = time.time()
        expected_state = configured_state_path()
        sp = state_path()
        state_exists = sp is not None and sp.exists()
        st = read_json(sp) if state_exists and sp else {}
        state_read_ok = bool(st) if state_exists else False

        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked_raw = st.get("blocked") if isinstance(st.get("blocked"), list) else []
        blocked_items = [norm_blocked(x) for x in blocked_raw[-8:]]

        current = next(
            (x for x in tasks if isinstance(x, dict) and x.get("status") == "working"),
            None,
        )
        if current is None:
            current = next(
                (x for x in tasks if isinstance(x, dict) and x.get("status") == "pending"),
                tasks[0] if tasks else {},
            )

        try:
            branch = subprocess.run(
                ["git", "branch", "--show-current"], cwd=REPO,
                capture_output=True, text=True, timeout=4,
                encoding="utf-8", errors="replace",
            ).stdout.strip()
            dirty = len(subprocess.run(
                ["git", "status", "--porcelain"], cwd=REPO,
                capture_output=True, text=True, timeout=6,
                encoding="utf-8", errors="replace",
            ).stdout.splitlines())
            head = subprocess.run(
                ["git", "rev-parse", "--short", "HEAD"], cwd=REPO,
                capture_output=True, text=True, timeout=4,
                encoding="utf-8", errors="replace",
            ).stdout.strip()
        except Exception:
            branch, dirty, head = "?", -1, "?"

        recent_errors = self.recent_errors()
        if not state_exists:
            recent_errors.append({"time": "STATE", "text": f"상태파일 없음: {expected_state}"})
        elif not state_read_ok:
            recent_errors.append({"time": "STATE", "text": f"상태파일 읽기 실패/빈 상태: {sp}"})

        grok_q = quota_status("grok")
        codex_q = quota_status("codex")
        pid = engine_pid()
        running = pid is not None
        grok_cli = cli_status("grok")
        codex_cli = cli_status("codex")
        with self.lock:
            last_exit = self.last_exit

        issue_count = len(blocked_items) + len(recent_errors)
        if grok_q.get("active"):
            issue_count += 1
        if codex_q.get("active"):
            issue_count += 1
        if not grok_cli.get("installed"):
            issue_count += 1
        if not codex_cli.get("installed"):
            issue_count += 1
        if last_exit not in (None, 0) and not running:
            issue_count += 1

        return {
            "ok": True,
            "checked_at": checked_at,
            "running": running,
            "pid": pid,
            "last_exit": last_exit,
            "goal": str(st.get("goal", "")),
            "current": current if isinstance(current, dict) else {},
            "queue_count": len(tasks),
            "completed_count": len(completed),
            "blocked_count": len(blocked_raw),
            "blocked_items": blocked_items,
            "recent_errors": recent_errors,
            "issue_count": issue_count,
            "stats": {
                k: int(stats.get(k, 0) or 0)
                for k in (
                    "grok_calls", "codex_calls", "director_calls",
                    "director_local_calls", "tasks_done", "tasks_blocked",
                )
            },
            "grok_quota": grok_q,
            "codex_quota": codex_q,
            "grok_cli": grok_cli,
            "codex_cli": codex_cli,
            "git": {"branch": branch or "?", "dirty_count": dirty, "head": head or "?"},
            "state_file": str(sp) if sp else str(expected_state),
            "state_exists": state_exists,
            "state_read_ok": state_read_ok,
        }

    def log_rows(self, after: int) -> dict[str, Any]:
        with self.lock:
            rows = [x for x in self.logs if int(x["seq"]) > after]
            return {"rows": rows[-500:], "last_seq": self.seq}


CTRL = Controller()
TOKEN = secrets.token_urlsafe(24)


class Handler(BaseHTTPRequestHandler):
    server_version = "AutoDevHTML/3"

    def log_message(self, fmt: str, *args: Any) -> None:
        return

    def _json(self, obj: Any, code: int = 200) -> None:
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")
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
            query = urllib.parse.parse_qs(parsed.query)
            supplied = query.get("token", [""])[0]
            if not secrets.compare_digest(supplied, TOKEN):
                self.send_response(302)
                self.send_header("Location", f"/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}")
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                return
            try:
                html = HTML_FILE.read_text(encoding="utf-8")
            except Exception as e:
                html = f"<h1>dashboard.html 읽기 실패</h1><pre>{e}</pre>"
            data = html.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
            self.send_header("Pragma", "no-cache")
            self.send_header("Expires", "0")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return

        if not self._authorized():
            self._json({"ok": False, "error": "unauthorized", "message": "대시보드 인증 오류"}, 403)
            return
        if parsed.path == "/api/status":
            self._json(CTRL.status())
            return
        if parsed.path == "/api/logs":
            query = urllib.parse.parse_qs(parsed.query)
            try:
                after = int(query.get("after", ["0"])[0])
            except Exception:
                after = 0
            self._json(CTRL.log_rows(after))
            return
        self._json({"ok": False, "error": "not found"}, 404)

    def do_POST(self) -> None:
        if not self._authorized():
            self._json({"ok": False, "error": "unauthorized", "message": "대시보드 인증 오류"}, 403)
            return
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/start":
            self._json(CTRL.start()); return
        if path == "/api/stop":
            self._json(CTRL.stop()); return
        if path == "/api/update":
            result = CTRL.update()
            self._json(result)
            if result.get("restarting"):
                threading.Timer(0.35, lambda: os._exit(0)).start()
            return
        if path == "/api/quota/codex/clear":
            self._json(CTRL.clear_quota("codex")); return
        if path == "/api/quota/grok/clear":
            self._json(CTRL.clear_quota("grok")); return
        if path == "/api/open-repo":
            try:
                subprocess.Popen(["open", str(REPO)])
                self._json({"ok": True})
            except Exception as e:
                self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/clear-logs":
            with CTRL.lock:
                CTRL.logs.clear()
            self._json({"ok": True}); return
        self._json({"ok": False, "error": "not found"}, 404)


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
    port = int(old.get("port", PORT) or PORT)
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
        webbrowser.open(f"http://{HOST}:{PORT}/?r={int(time.time())}")
        return 0

    write_server_state()
    atexit.register(cleanup)
    url = f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"
    CTRL.log(f"[HTML] 대시보드 {url}")

    resume = os.environ.pop("AUTODEV_RESUME_ENGINE", "0").strip() == "1"
    if resume and not CTRL.running():
        CTRL.log("[UPDATE] 업데이트 전 실행 상태를 복원합니다.")
        threading.Timer(1.0, CTRL.start).start()

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
