#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 localhost dashboard server.

The dashboard is intentionally dumb and file-backed:
- engine truth = OS process list + html_engine.json
- progress truth = engine_heartbeat.json
- logs truth = engine.log
- game work truth = configured state.json
This means restarting the dashboard cannot make it forget a running engine.
"""
from __future__ import annotations

import atexit
import json
import os
import re
import secrets
import shutil
import signal
import socket
import subprocess
import sys
import threading
import time
import urllib.parse
import webbrowser
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
HEARTBEAT = OUTPUT / "engine_heartbeat.json"
ENGINE_LOG = OUTPUT / "engine.log"
CONFIG_FILE = HERE / "config.json"
SERVER_LOG = Path.home() / "Library" / "Logs" / "AutoDevV2-HTML.log"
CONTROL_VERSION = "6"
HOST = "127.0.0.1"
PORT = int(os.environ.get("AUTODEV_HTML_PORT", "8765"))
QUOTA_FILES = {
    "grok": OUTPUT / "grok_quota_exhausted.json",
    "codex": OUTPUT / "codex_quota_exhausted.json",
}
QUOTA_COOLDOWNS = {"grok": 3600, "codex": 300}
ERROR_RE = re.compile(
    r"(error|exception|traceback|failed|failure|timeout|blocked|quota|402|rc=[1-9]|실패|오류|막힘|한도\s*소진)",
    re.IGNORECASE,
)
ENGINE_RE = re.compile(r"projects/autodev-v2/(?:start|runner_entry)\.py(?:\s|$)")


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


def git_head() -> str:
    try:
        r = subprocess.run(["git", "rev-parse", "--short=12", "HEAD"], cwd=REPO,
                           capture_output=True, text=True, timeout=4,
                           encoding="utf-8", errors="replace")
        return r.stdout.strip() if r.returncode == 0 else ""
    except Exception:
        return ""


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
    if os.name == "nt":
        return []
    try:
        r = subprocess.run(["ps", "-Ao", "pid=,command="], capture_output=True, text=True,
                           timeout=4, encoding="utf-8", errors="replace")
    except Exception:
        return []
    rows: list[tuple[int, str]] = []
    for line in r.stdout.splitlines():
        m = re.match(r"\s*(\d+)\s+(.*)$", line)
        if m:
            rows.append((int(m.group(1)), m.group(2)))
    return rows


def engine_pids() -> list[int]:
    found: set[int] = set()
    st = read_json(ENGINE_STATE)
    try:
        p = int(st.get("pid", 0) or 0)
        if p and pid_alive(p):
            found.add(p)
    except Exception:
        pass
    repo_norm = str(REPO).replace("\\", "/")
    for pid, cmd in process_rows():
        norm = cmd.replace("\\", "/")
        if repo_norm in norm and ENGINE_RE.search(norm):
            found.add(pid)
    return sorted(found)


def engine_info() -> dict[str, Any]:
    st = read_json(ENGINE_STATE)
    pids = engine_pids()
    running = bool(pids)
    current = git_head()
    engine_head = str(st.get("repo_head", ""))
    protocol = str(st.get("control_version", ""))
    stale = bool(running and (protocol != CONTROL_VERSION or not engine_head or (current and engine_head != current)))
    duplicate = len(pids) > 2  # normally parent start.py + child runner_entry.py
    return {
        "running": running,
        "pid": pids[0] if pids else None,
        "pids": pids,
        "started_at": float(st.get("started_at", 0) or 0),
        "repo_head": engine_head,
        "current_head": current,
        "control_version": protocol,
        "stale": stale,
        "duplicate": duplicate,
    }


def configured_state_path() -> Path:
    cfg = read_json(CONFIG_FILE)
    raw = str(cfg.get("state_file", "output/autodev_v2/ashes-to-stars/state.json"))
    p = Path(raw)
    return p if p.is_absolute() else (REPO / p).resolve()


def state_path() -> Path | None:
    configured = configured_state_path()
    if configured.exists():
        return configured
    return None


def quota_status(provider: str) -> dict[str, Any]:
    data = read_json(QUOTA_FILES[provider])
    try:
        ts = float(data.get("detected_at", 0) or 0)
    except Exception:
        ts = 0.0
    age = max(0, int(time.time() - ts)) if ts else None
    cooldown = QUOTA_COOLDOWNS[provider]
    active = bool(ts and age is not None and age < cooldown)
    return {
        "detected": bool(ts), "active": active,
        "remaining_seconds": max(0, cooldown - int(age or 0)) if active else 0,
        "reason": str(data.get("reason", "")),
    }


def find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (f"/opt/homebrew/bin/{name}", f"/usr/local/bin/{name}", str(Path.home() / ".local" / "bin" / name)):
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
                               timeout=4, encoding="utf-8", errors="replace")
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                version = text.splitlines()[0][:160]
                break
        except Exception:
            pass
    return {"installed": True, "path": exe, "version": version}


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.3):
            return True
    except OSError:
        return False


def tail_lines(path: Path, max_lines: int = 600) -> list[str]:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
        return text.splitlines()[-max_lines:]
    except Exception:
        return []


def norm_blocked(item: Any) -> dict[str, Any]:
    if not isinstance(item, dict):
        return {"id": "", "title": str(item), "reason": "원인을 기록하지 못했습니다."}
    reason = item.get("last_error") or item.get("error") or item.get("reason") or item.get("blocked_reason") or "원인을 기록하지 못했습니다."
    return {"id": str(item.get("id", "")), "title": str(item.get("title") or item.get("goal") or "이름 없는 작업"), "reason": str(reason)[-2500:]}


def friendly_stage(hb: dict[str, Any], running: bool) -> str:
    if not running:
        return "쉬는 중"
    stage = str(hb.get("stage", "")).lower()
    message = str(hb.get("message", ""))
    if "codex" in stage:
        return "Codex가 코드를 고치는 중"
    if "grok" in stage or stage == "director":
        return "Grok이 생각하거나 코드를 고치는 중"
    if "waiting_verification" in stage:
        return "Unity 검증이 가능해지기를 기다리는 중"
    if "verify" in stage:
        return "고친 코드가 실제로 되는지 검사 중"
    if stage == "task":
        return "작업을 준비하는 중"
    return message[:100] or "개발 작업 진행 중"


def kill_process_group(pid: int, sig: int) -> None:
    try:
        if os.name != "nt":
            os.killpg(os.getpgid(pid), sig)
        else:
            os.kill(pid, sig)
    except (ProcessLookupError, PermissionError, OSError):
        pass


class Controller:
    def __init__(self) -> None:
        self.lock = threading.RLock()
        self.last_exit: int | None = None
        self._cli_cache: dict[str, Any] = {}
        self._cli_checked = 0.0
        OUTPUT.mkdir(parents=True, exist_ok=True)

    def log(self, text: str) -> None:
        stamp = time.strftime("%H:%M:%S")
        try:
            with self.lock:
                with ENGINE_LOG.open("a", encoding="utf-8") as f:
                    f.write(f"{stamp} {text}\n")
        except Exception:
            pass
        print(text, flush=True)

    def running(self) -> bool:
        return bool(engine_info()["running"])

    def stop(self) -> dict[str, Any]:
        pids = engine_pids()
        if not pids:
            for p in (ENGINE_STATE, HEARTBEAT):
                try: p.unlink()
                except OSError: pass
            return {"ok": True, "message": "이미 쉬고 있습니다."}
        self.log("[HTML] AutoDev 중지 요청 · PID " + ",".join(map(str, pids)))
        groups: set[int] = set()
        for pid in pids:
            try:
                groups.add(os.getpgid(pid) if os.name != "nt" else pid)
            except Exception:
                groups.add(pid)
        for g in groups:
            try:
                if os.name != "nt": os.killpg(g, signal.SIGTERM)
                else: os.kill(g, signal.SIGTERM)
            except Exception: pass
        deadline = time.time() + 6
        while engine_pids() and time.time() < deadline:
            time.sleep(0.1)
        if engine_pids():
            for pid in engine_pids():
                kill_process_group(pid, signal.SIGKILL)
            time.sleep(0.4)
        left = engine_pids()
        if left:
            return {"ok": False, "message": "엔진을 끄지 못했습니다: " + ",".join(map(str, left))}
        for p in (ENGINE_STATE, HEARTBEAT):
            try: p.unlink()
            except OSError: pass
        self.log("[HTML] AutoDev 중지 완료")
        return {"ok": True, "message": "자율 개발을 멈췄습니다."}

    def start(self) -> dict[str, Any]:
        info = engine_info()
        if info["running"] and not info["stale"] and not info["duplicate"]:
            return {"ok": True, "message": f"이미 개발 중입니다. PID {info['pid']}"}
        if info["running"]:
            self.log("[SELF-HEAL] 구버전/중복 엔진을 정리하고 최신 엔진으로 교체합니다.")
            stopped = self.stop()
            if not stopped.get("ok"):
                return stopped
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env["PATH"] = f"{Path.home() / '.local/bin'}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
        try:
            log = ENGINE_LOG.open("a", encoding="utf-8")
            p = subprocess.Popen([sys.executable, str(HERE / "start.py")], cwd=REPO,
                                 stdout=log, stderr=subprocess.STDOUT, env=env,
                                 start_new_session=True)
            log.close()
        except Exception as e:
            self.log(f"[HTML] 시작 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}
        write_json(ENGINE_STATE, {
            "pid": p.pid, "started_at": time.time(),
            "repo_head": git_head(), "control_version": CONTROL_VERSION,
        })
        self.log(f"[HTML] AutoDev 시작 PID {p.pid} · {git_head()}")
        time.sleep(0.55)
        rc = p.poll()
        if rc is not None:
            self.last_exit = rc
            try: ENGINE_STATE.unlink()
            except OSError: pass
            tail = "\n".join(tail_lines(ENGINE_LOG, 30)[-12:])
            return {"ok": False, "message": f"엔진이 시작 직후 종료됐습니다. rc={rc}\n{tail[-1800:]}"}
        return {"ok": True, "message": "자율 개발을 시작했습니다."}

    def clear_quota(self, provider: str) -> dict[str, Any]:
        p = QUOTA_FILES.get(provider)
        if p is None:
            return {"ok": False, "message": "알 수 없는 AI입니다."}
        try:
            if p.exists(): p.unlink()
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
        self.log("[USAGE] Codex 5시간/주간 실제 사용량 확인 중")
        snap = refresh_codex_usage()
        if snap.get("ok"):
            names = ", ".join(f"{x.get('name')} {x.get('remaining_percent')}% 남음" for x in snap.get("windows", []))
            self.log("[USAGE] Codex 사용량 확인 완료 · " + names)
        else:
            self.log("[USAGE] Codex 사용량 확인 실패 · " + str(snap.get("error", "알 수 없는 이유")))
        return snap

    def _git_update(self) -> tuple[bool, str]:
        commands = [
            ["git", "-c", "core.hooksPath=/dev/null", "fetch", "origin", "master"],
            ["git", "-c", "core.hooksPath=/dev/null", "-c", "rebase.autoStash=true", "rebase", "origin/master"],
        ]
        output: list[str] = []
        for cmd in commands:
            r = subprocess.run(cmd, cwd=REPO, capture_output=True, text=True,
                               encoding="utf-8", errors="replace", timeout=180)
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if text: output.extend(text.splitlines())
            if r.returncode != 0:
                if "rebase" in cmd:
                    subprocess.run(["git", "rebase", "--abort"], cwd=REPO,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
                return False, "\n".join(output[-100:])
        return True, "\n".join(output[-100:])

    def update(self) -> dict[str, Any]:
        was_running = self.running()
        self.log("[UPDATE 1/4] 실제 엔진 상태 확인")
        if was_running:
            stopped = self.stop()
            if not stopped.get("ok"):
                return {"ok": False, "message": "업데이트 전에 엔진을 멈추지 못했습니다."}
        self.clear_quota("codex")
        self.log("[UPDATE 2/4] 작업 보존 + 최신 코드 받는 중")
        ok, out = self._git_update()
        for line in out.splitlines()[-80:]: self.log("[GIT] " + line)
        if not ok:
            if was_running: self.start()
            return {"ok": False, "message": out[-1800:] or "Git 업데이트 실패"}
        self.log("[UPDATE 3/4] 새 서버로 안전하게 인계")
        try:
            log = SERVER_LOG.open("a", encoding="utf-8")
            subprocess.Popen(
                [sys.executable, str(HERE / "restart_server.py"), str(os.getpid()), str(PORT), "1" if was_running else "0"],
                cwd=REPO, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                start_new_session=True, env=os.environ.copy(),
            )
            log.close()
        except Exception as e:
            if was_running: self.start()
            return {"ok": False, "message": "새 서버 인계 실패: " + str(e)}
        self.log("[UPDATE 4/4] 완료 · 새 코드로 서버가 교체됩니다")
        return {"ok": True, "message": "업데이트 완료. 새 코드로 갈아타는 중입니다.", "restarting": True}

    def recent_errors(self, max_items: int = 8) -> list[dict[str, str]]:
        result: list[dict[str, str]] = []
        seen: set[str] = set()
        for line in reversed(tail_lines(ENGINE_LOG, 500)):
            if not ERROR_RE.search(line):
                continue
            key = line[-500:]
            if key in seen: continue
            seen.add(key)
            result.append({"time": "로그", "text": line[-1800:]})
            if len(result) >= max_items: break
        result.reverse()
        return result

    def status(self) -> dict[str, Any]:
        now = time.time()
        info = engine_info()
        sp = state_path()
        expected = configured_state_path()
        state_exists = bool(sp and sp.exists())
        st = read_json(sp) if state_exists and sp else {}
        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked_raw = st.get("blocked") if isinstance(st.get("blocked"), list) else []
        blocked = [norm_blocked(x) for x in blocked_raw[-8:]]
        current = next((x for x in tasks if isinstance(x, dict) and x.get("status") == "working"), None)
        if current is None:
            current = next((x for x in tasks if isinstance(x, dict) and x.get("status") in {"pending","waiting_verification"}), tasks[0] if tasks else {})

        hb = read_json(HEARTBEAT)
        try: hb_at = float(hb.get("heartbeat_at", 0) or 0)
        except Exception: hb_at = 0.0
        try: output_at = float(hb.get("last_output_at", 0) or 0)
        except Exception: output_at = 0.0
        heartbeat_age = int(max(0, now - hb_at)) if hb_at else None
        quiet_seconds = int(max(0, now - output_at)) if info["running"] and output_at else None

        recent_errors = self.recent_errors()
        if not state_exists:
            recent_errors.append({"time": "상태", "text": f"개발 상태 파일을 찾지 못했습니다: {expected}"})
        if info["stale"]:
            recent_errors.append({"time": "버전", "text": f"구버전 엔진 감지 · 실행 {info['repo_head'] or '?'} / 현재 {info['current_head'] or '?'} · 시작 버튼을 누르면 자동 교체합니다."})
        if info["duplicate"]:
            recent_errors.append({"time": "엔진", "text": "중복 AutoDev 프로세스가 감지되었습니다. 시작 버튼을 누르면 하나로 정리합니다."})
        if info["running"] and (heartbeat_age is None or heartbeat_age > 20):
            recent_errors.append({"time": "심박", "text": "엔진 프로세스는 있지만 20초 넘게 심박이 없습니다. 구버전 또는 멈춘 엔진일 수 있습니다."})

        try:
            branch = subprocess.run(["git", "branch", "--show-current"], cwd=REPO, capture_output=True, text=True, timeout=3).stdout.strip()
            head = git_head()
            dirty = len(subprocess.run(["git", "status", "--porcelain"], cwd=REPO, capture_output=True, text=True, timeout=4).stdout.splitlines())
        except Exception:
            branch, head, dirty = "?", "?", -1

        cli = self._cli()
        grok_q = quota_status("grok")
        codex_q = quota_status("codex")
        usage = cached_codex_usage()
        issue_count = len(blocked) + len(recent_errors)
        if grok_q["active"] or codex_q["active"]: issue_count += 1
        if not cli.get("grok", {}).get("installed") or not cli.get("codex", {}).get("installed"): issue_count += 1
        last_message = str(hb.get("message", ""))

        return {
            "ok": True, "checked_at": now, "running": info["running"], "pid": info["pid"],
            "started_at": info["started_at"], "last_exit": self.last_exit,
            "last_activity_at": output_at or hb_at or None, "quiet_seconds": quiet_seconds,
            "heartbeat_age": heartbeat_age, "stage": friendly_stage(hb, info["running"]), "last_log": last_message[-800:],
            "goal": str(st.get("goal", "")), "current": current if isinstance(current, dict) else {},
            "queue_count": len(tasks), "completed_count": len(completed), "blocked_count": len(blocked_raw),
            "blocked_items": blocked, "recent_errors": recent_errors, "issue_count": issue_count,
            "stats": {k: int(stats.get(k, 0) or 0) for k in ("grok_calls", "codex_calls", "director_local_calls", "director_calls", "tasks_done", "tasks_blocked")},
            "grok_quota": grok_q, "codex_quota": codex_q, "codex_usage": usage,
            "grok_cli": cli.get("grok", {}), "codex_cli": cli.get("codex", {}),
            "git": {"branch": branch or "?", "head": head or "?", "dirty_count": dirty},
            "engine": info, "control_version": CONTROL_VERSION,
            "state_file": str(sp) if sp else str(expected),
        }

    def log_rows(self, after: int) -> dict[str, Any]:
        lines = tail_lines(ENGINE_LOG, 1000)
        total = len(lines)
        base = max(0, total - len(lines))
        rows = []
        for i, line in enumerate(lines, start=base + 1):
            if i <= after: continue
            m = re.match(r"(\d{2}:\d{2}:\d{2})\s+(.*)$", line)
            rows.append({"seq": i, "time": m.group(1) if m else "", "text": m.group(2) if m else line})
        return {"rows": rows[-500:], "last_seq": total}


CTRL = Controller()
TOKEN = secrets.token_urlsafe(24)


class Handler(BaseHTTPRequestHandler):
    server_version = "AutoDevELI5/6"

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
        q = urllib.parse.parse_qs(parsed.query)
        supplied = self.headers.get("X-AutoDev-Token", "") or q.get("token", [""])[0]
        return secrets.compare_digest(supplied, TOKEN)

    def do_GET(self) -> None:
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/":
            supplied = urllib.parse.parse_qs(parsed.query).get("token", [""])[0]
            if not secrets.compare_digest(supplied, TOKEN):
                self.send_response(302)
                self.send_header("Location", f"/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}")
                self.send_header("Cache-Control", "no-store")
                self.end_headers(); return
            try: data = HTML_FILE.read_bytes()
            except Exception as e: data = f"<h1>화면 파일을 읽지 못했습니다</h1><pre>{e}</pre>".encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers(); self.wfile.write(data); return
        if not self._authorized(): self._json({"ok": False, "message": "대시보드 인증 오류"}, 403); return
        if parsed.path == "/api/status": self._json(CTRL.status()); return
        if parsed.path == "/api/logs":
            try: after = int(urllib.parse.parse_qs(parsed.query).get("after", ["0"])[0])
            except Exception: after = 0
            self._json(CTRL.log_rows(after)); return
        self._json({"ok": False, "message": "없는 주소입니다."}, 404)

    def do_POST(self) -> None:
        if not self._authorized(): self._json({"ok": False, "message": "대시보드 인증 오류"}, 403); return
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/start": self._json(CTRL.start()); return
        if path == "/api/stop": self._json(CTRL.stop()); return
        if path == "/api/update":
            result = CTRL.update(); self._json(result)
            if result.get("restarting"): threading.Timer(0.4, lambda: os._exit(0)).start()
            return
        if path == "/api/quota/codex/clear": self._json(CTRL.clear_quota("codex")); return
        if path == "/api/quota/grok/clear": self._json(CTRL.clear_quota("grok")); return
        if path == "/api/open-repo":
            try: subprocess.Popen(["open", str(REPO)]); self._json({"ok": True, "message": "폴더를 열었습니다."})
            except Exception as e: self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/clear-logs":
            try: ENGINE_LOG.write_text("", encoding="utf-8")
            except Exception: pass
            self._json({"ok": True, "message": "로그를 비웠습니다."}); return
        self._json({"ok": False, "message": "없는 기능입니다."}, 404)


def write_server_state() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write_json(SERVER_STATE, {"pid": os.getpid(), "port": PORT, "token": TOKEN,
                              "control_version": CONTROL_VERSION, "repo_head": git_head()})


def cleanup() -> None:
    try:
        if SERVER_STATE.exists() and read_json(SERVER_STATE).get("pid") == os.getpid(): SERVER_STATE.unlink()
    except Exception:
        pass


def open_existing_if_any() -> bool:
    old = read_json(SERVER_STATE)
    try: port = int(old.get("port", PORT) or PORT)
    except Exception: port = PORT
    token = str(old.get("token", ""))
    if not (token and server_alive(port)):
        return False
    if str(old.get("control_version", "")) == CONTROL_VERSION:
        webbrowser.open(f"http://{HOST}:{port}/?token={urllib.parse.quote(token)}&r={int(time.time())}")
        return True
    # New code was launched while an old dashboard is still alive. Replace only the old dashboard.
    try:
        old_pid = int(old.get("pid", 0) or 0)
        if old_pid and old_pid != os.getpid(): os.kill(old_pid, signal.SIGTERM)
    except Exception:
        pass
    deadline = time.time() + 5
    while server_alive(port) and time.time() < deadline: time.sleep(0.1)
    return False


def main() -> int:
    if open_existing_if_any(): return 0
    try: server = ThreadingHTTPServer((HOST, PORT), Handler)
    except OSError:
        webbrowser.open(f"http://{HOST}:{PORT}/"); return 0
    write_server_state(); atexit.register(cleanup)
    url = f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"
    CTRL.log(f"[HTML] 대시보드 v{CONTROL_VERSION} · {git_head()} · {url}")

    resume = os.environ.pop("AUTODEV_RESUME_ENGINE", "0").strip() == "1"
    refresh_usage = os.environ.pop("AUTODEV_REFRESH_CODEX_USAGE", "0").strip() == "1"
    if resume:
        threading.Timer(0.8, CTRL.start).start()
    if refresh_usage or (not cached_codex_usage() and find_cli("codex")):
        threading.Thread(target=CTRL.refresh_codex_meter, daemon=True).start()
    threading.Timer(0.35, lambda: webbrowser.open(url)).start()
    try: server.serve_forever(poll_interval=0.25)
    finally: server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
