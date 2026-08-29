#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 localhost dashboard.

One source of truth:
- dashboard starts exactly one long-lived `engine.py` process
- engine state: output/autodev_v2/html_engine.json
- heartbeat: output/autodev_v2/engine_heartbeat.json
- log: output/autodev_v2/engine.log
- game queue: configured state.json

Update copies only AutoDev infrastructure from origin/master. Game work is untouched.
"""
from __future__ import annotations

import atexit
import hashlib
import io
import json
import os
import re
import secrets
import shutil
import signal
import socket
import subprocess
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
CONTROL_PROTOCOL = 7
CONTROL_FILES = (
    "engine.py", "runner_entry.py", "runner.py", "autodev.py",
    "functional_verify.py", "config.json", "start.py",
)
INFRA_PATHS = (
    "projects/autodev-v2",
    ".github/workflows/autodev-v2-tests.yml",
    "projects/ashes-to-stars/unity/Assets/Editor/AutoDevAcceptance/AutoDevAcceptanceRunner.cs",
)
QUOTA_FILES = {
    "grok": OUTPUT / "grok_quota_exhausted.json",
    "codex": OUTPUT / "codex_quota_exhausted.json",
}
QUOTA_COOLDOWNS = {"grok": 3600, "codex": 300}
ERROR_RE = re.compile(r"(error|exception|traceback|failed|failure|timeout|blocked|quota|402|rc=[1-9]|실패|오류|막힘|한도\s*소진)", re.I)
LEGACY_RE = re.compile(r"projects/autodev-v2/(?:start|runner_entry)\.py(?:\s|$)")


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


def control_fingerprint() -> str:
    h = hashlib.sha256()
    for name in CONTROL_FILES:
        p = HERE / name
        h.update(name.encode())
        try: h.update(p.read_bytes())
        except OSError: h.update(b"MISSING")
    return h.hexdigest()[:16]


def pid_alive(pid: int) -> bool:
    if pid <= 1: return False
    try:
        os.kill(pid, 0); return True
    except ProcessLookupError: return False
    except PermissionError: return True
    except OSError: return False


def process_rows() -> list[tuple[int, str]]:
    if os.name == "nt": return []
    try:
        r = subprocess.run(["ps", "-Ao", "pid=,command="], capture_output=True, text=True,
                           timeout=4, encoding="utf-8", errors="replace")
    except Exception:
        return []
    out = []
    for line in r.stdout.splitlines():
        m = re.match(r"\s*(\d+)\s+(.*)$", line)
        if m: out.append((int(m.group(1)), m.group(2)))
    return out


def legacy_pids() -> list[int]:
    repo = str(REPO).replace("\\", "/")
    found = []
    for pid, cmd in process_rows():
        norm = cmd.replace("\\", "/")
        if repo in norm and LEGACY_RE.search(norm): found.append(pid)
    return sorted(set(found))


def engine_info() -> dict[str, Any]:
    st = read_json(ENGINE_STATE)
    try: pid = int(st.get("pid", 0) or 0)
    except Exception: pid = 0
    running = bool(pid and pid_alive(pid))
    fp = str(st.get("control_fingerprint", ""))
    protocol = int(st.get("control_protocol", 0) or 0)
    current_fp = control_fingerprint()
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
    try: ts = float(data.get("detected_at", 0) or 0)
    except Exception: ts = 0.0
    age = max(0, int(time.time() - ts)) if ts else None
    cooldown = QUOTA_COOLDOWNS[provider]
    active = bool(ts and age is not None and age < cooldown)
    return {"detected": bool(ts), "active": active,
            "remaining_seconds": max(0, cooldown - int(age or 0)) if active else 0,
            "reason": str(data.get("reason", ""))}


def find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe: return exe
    for p in (f"/opt/homebrew/bin/{name}", f"/usr/local/bin/{name}", str(Path.home()/".local/bin"/name)):
        if os.path.isfile(p) and os.access(p, os.X_OK): return p
    return None


def cli_status(name: str) -> dict[str, Any]:
    exe = find_cli(name)
    if not exe: return {"installed": False, "path": "", "version": ""}
    version = ""
    for args in (["--version"], ["version"]):
        try:
            r = subprocess.run([exe, *args], capture_output=True, text=True, timeout=4,
                               encoding="utf-8", errors="replace")
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                version = text.splitlines()[0][:160]; break
        except Exception: pass
    return {"installed": True, "path": exe, "version": version}


def tail_lines(path: Path, limit: int = 800) -> list[str]:
    try: return path.read_text(encoding="utf-8", errors="replace").splitlines()[-limit:]
    except Exception: return []


def server_alive(port: int) -> bool:
    try:
        with socket.create_connection((HOST, port), timeout=0.3): return True
    except OSError: return False


def norm_blocked(x: Any) -> dict[str, str]:
    if not isinstance(x, dict): return {"id":"", "title":str(x), "reason":"원인 기록 없음"}
    reason = x.get("last_error") or x.get("error") or x.get("reason") or "원인 기록 없음"
    return {"id":str(x.get("id","")), "title":str(x.get("title") or x.get("goal") or "이름 없는 작업"), "reason":str(reason)[-2200:]}


def friendly_stage(hb: dict[str, Any], running: bool) -> str:
    if not running: return "쉬는 중"
    stage = str(hb.get("stage", "")).lower()
    if "codex" in stage: return "Codex가 코드를 고치는 중"
    if "grok" in stage or stage == "director": return "Grok이 생각하거나 코드를 고치는 중"
    if "waiting_verification" in stage: return "Unity 검증을 기다리는 중"
    if "verify" in stage: return "고친 기능을 실제로 검사 중"
    if "bootstrap" in stage or "preflight" in stage or "starting" in stage: return "개발 엔진 준비 중"
    return str(hb.get("message", ""))[:100] or "개발 작업 진행 중"


def kill_group(pid: int, sig: int) -> None:
    try:
        if os.name != "nt": os.killpg(os.getpgid(pid), sig)
        else: os.kill(pid, sig)
    except Exception: pass


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
        except Exception: pass
        print(text, flush=True)

    def _kill_legacy(self) -> None:
        for pid in legacy_pids(): kill_group(pid, signal.SIGTERM)
        time.sleep(0.3)
        for pid in legacy_pids(): kill_group(pid, signal.SIGKILL)

    def stop(self) -> dict[str, Any]:
        info = engine_info()
        pid = info.get("pid")
        if pid:
            self.log(f"[CONTROL] 중지 요청 PID {pid}")
            kill_group(int(pid), signal.SIGTERM)
            deadline = time.time() + 7
            while pid_alive(int(pid)) and time.time() < deadline: time.sleep(0.1)
            if pid_alive(int(pid)): kill_group(int(pid), signal.SIGKILL)
        self._kill_legacy()
        deadline = time.time() + 2
        while time.time() < deadline:
            now = engine_info()
            if not now["running"] and not now["legacy_pids"]: break
            time.sleep(0.1)
        now = engine_info()
        if now["running"] or now["legacy_pids"]:
            return {"ok":False, "message":"엔진 프로세스를 완전히 끄지 못했습니다."}
        for p in (HEARTBEAT,):
            try: p.unlink()
            except OSError: pass
        self.log("[CONTROL] 완전히 중지됨")
        return {"ok":True, "message":"자율 개발을 멈췄습니다."}

    def start(self) -> dict[str, Any]:
        info = engine_info()
        if info["running"] and not info["stale"] and not info["legacy_pids"]:
            return {"ok":True, "message":f"이미 정상 실행 중입니다. PID {info['pid']}"}
        if info["running"] or info["legacy_pids"]:
            r = self.stop()
            if not r.get("ok"): return r
        env = os.environ.copy()
        env["PYTHONUNBUFFERED"] = "1"
        env["PATH"] = f"{Path.home()/'.local/bin'}:/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
        try:
            log = ENGINE_LOG.open("a", encoding="utf-8")
            p = subprocess.Popen([sys.executable, str(ENGINE_FILE)], cwd=REPO,
                                 stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                                 env=env, start_new_session=True)
            log.close()
        except Exception as e:
            return {"ok":False, "message":f"엔진 시작 실패: {e}"}
        self.log(f"[CONTROL] 단일 엔진 시작 PID {p.pid}")
        deadline = time.time() + 8
        while time.time() < deadline:
            if p.poll() is not None:
                tail = "\n".join(tail_lines(ENGINE_LOG, 20)[-12:])
                return {"ok":False, "message":f"엔진이 시작 중 종료됐습니다. rc={p.returncode}\n{tail[-1600:]}"}
            st = read_json(ENGINE_STATE); hb = read_json(HEARTBEAT)
            if int(st.get("pid",0) or 0) == p.pid and int(hb.get("pid",0) or 0) == p.pid:
                return {"ok":True, "message":f"개발 시작 완료 · PID {p.pid}"}
            time.sleep(0.15)
        return {"ok":True, "message":f"엔진 PID {p.pid} 실행 중 · 준비 상태를 계속 확인합니다."}

    def recover(self) -> dict[str, Any]:
        self.log("[SELF-HEAL] 완전 정리 후 단일 엔진으로 재시작")
        stopped = self.stop()
        if not stopped.get("ok"): return stopped
        return self.start()

    def _update_control_files(self) -> tuple[bool, str]:
        try:
            r = subprocess.run(["git","-c","core.hooksPath=/dev/null","fetch","origin","master"],
                               cwd=REPO, capture_output=True, text=True, timeout=180,
                               encoding="utf-8", errors="replace")
            if r.returncode != 0: return False, (r.stderr or r.stdout or "git fetch 실패")[-1800:]
            arc = subprocess.run(["git","archive","--format=tar","origin/master",*INFRA_PATHS],
                                 cwd=REPO, capture_output=True, timeout=60)
            if arc.returncode != 0: return False, arc.stderr.decode("utf-8","replace")[-1800:]
            with tarfile.open(fileobj=io.BytesIO(arc.stdout), mode="r:") as tf:
                tf.extractall(REPO)
            return True, "AutoDev 시스템 파일 최신화 완료"
        except Exception as e:
            return False, f"{type(e).__name__}: {e}"

    def update(self) -> dict[str, Any]:
        was_running = engine_info()["running"]
        self.log("[UPDATE 1/3] 엔진 정리")
        if was_running or legacy_pids():
            r = self.stop()
            if not r.get("ok"): return r
        self.clear_quota("codex")
        self.log("[UPDATE 2/3] 게임 파일은 그대로 두고 AutoDev 시스템만 최신화")
        ok, msg = self._update_control_files()
        if not ok:
            if was_running: self.start()
            return {"ok":False, "message":msg}
        self.log("[UPDATE 3/3] 새 대시보드로 교체")
        try:
            log = SERVER_LOG.open("a", encoding="utf-8")
            subprocess.Popen([sys.executable, str(HERE/"restart_server.py"), str(os.getpid()), str(PORT), "1" if was_running else "0"],
                             cwd=REPO, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                             start_new_session=True, env=os.environ.copy())
            log.close()
        except Exception as e:
            if was_running: self.start()
            return {"ok":False, "message":f"화면 재시작 준비 실패: {e}"}
        return {"ok":True, "message":"AutoDev 시스템 업데이트 완료. 게임 작업은 건드리지 않았습니다.", "restarting":True}

    def clear_quota(self, provider: str) -> dict[str, Any]:
        p = QUOTA_FILES.get(provider)
        if p is None: return {"ok":False,"message":"알 수 없는 AI"}
        try:
            if p.exists(): p.unlink()
            return {"ok":True,"message":f"{provider.upper()} 한도를 다음 호출에서 다시 확인합니다."}
        except Exception as e: return {"ok":False,"message":str(e)}

    def _cli(self, force: bool=False) -> dict[str, Any]:
        if force or not self._cli_cache or time.time()-self._cli_checked>30:
            self._cli_cache={"grok":cli_status("grok"),"codex":cli_status("codex")}; self._cli_checked=time.time()
        return self._cli_cache

    def refresh_codex_meter(self) -> dict[str, Any]:
        return refresh_codex_usage()

    def status(self) -> dict[str, Any]:
        now=time.time(); info=engine_info(); hb=read_json(HEARTBEAT); sp=configured_state_path(); st=read_json(sp) if sp.exists() else {}
        tasks=st.get("tasks") if isinstance(st.get("tasks"),list) else []
        completed=st.get("completed") if isinstance(st.get("completed"),list) else []
        blocked_raw=st.get("blocked") if isinstance(st.get("blocked"),list) else []
        blocked=[norm_blocked(x) for x in blocked_raw[-8:]]
        current=next((x for x in tasks if isinstance(x,dict) and x.get("status")=="working"),None)
        if current is None: current=next((x for x in tasks if isinstance(x,dict) and x.get("status") in {"pending","waiting_verification","waiting_dependency"}), tasks[0] if tasks else {})
        try: hb_at=float(hb.get("heartbeat_at",0) or 0)
        except Exception: hb_at=0
        try: out_at=float(hb.get("last_output_at",0) or 0)
        except Exception: out_at=0
        hb_age=int(max(0,now-hb_at)) if hb_at else None
        quiet=int(max(0,now-out_at)) if info["running"] and out_at else None
        errs=[]
        for line in reversed(tail_lines(ENGINE_LOG,500)):
            if ERROR_RE.search(line): errs.append({"time":"로그","text":line[-1600:]})
            if len(errs)>=6: break
        errs.reverse()
        if not sp.exists(): errs.append({"time":"상태","text":f"상태 파일 없음: {sp}"})
        if info["stale"]: errs.append({"time":"버전","text":"실행 중 엔진이 현재 AutoDev 시스템 버전과 다릅니다. 자동 복구를 누르세요."})
        if info["legacy_pids"]: errs.append({"time":"엔진","text":f"예전 AutoDev 프로세스가 남아 있습니다: {info['legacy_pids']}"})
        if info["running"] and (hb_age is None or hb_age>20): errs.append({"time":"심박","text":"엔진 PID는 살아 있지만 심박이 20초 이상 없습니다. 자동 복구가 필요합니다."})
        cli=self._cli(); gq=quota_status("grok"); cq=quota_status("codex"); usage=cached_codex_usage(); stats=st.get("stats") if isinstance(st.get("stats"),dict) else {}
        issue=len(blocked)+len(errs)+(1 if gq["active"] or cq["active"] else 0)
        return {
            "ok":True,"checked_at":now,"running":info["running"],"pid":info["pid"],"started_at":info["started_at"],
            "quiet_seconds":quiet,"heartbeat_age":hb_age,"stage":friendly_stage(hb,info["running"]),"last_log":str(hb.get("message",""))[-800:],
            "goal":str(st.get("goal","")),"current":current if isinstance(current,dict) else {},
            "queue_count":len(tasks),"completed_count":len(completed),"blocked_count":len(blocked_raw),"blocked_items":blocked,
            "recent_errors":errs,"issue_count":issue,"engine":info,"control_version":CONTROL_PROTOCOL,
            "stats":{k:int(stats.get(k,0) or 0) for k in ("grok_calls","codex_calls","director_calls","tasks_done","tasks_blocked")},
            "grok_quota":gq,"codex_quota":cq,"codex_usage":usage,"grok_cli":cli.get("grok",{}),"codex_cli":cli.get("codex",{}),
            "git":{"branch":"master","head":control_fingerprint(),"dirty_count":0},"state_file":str(sp),
        }

    def log_rows(self, after:int)->dict[str,Any]:
        lines=tail_lines(ENGINE_LOG,1000); rows=[]
        for i,line in enumerate(lines,1):
            if i<=after: continue
            m=re.match(r"(\d{2}:\d{2}:\d{2})\s+(.*)$",line)
            rows.append({"seq":i,"time":m.group(1) if m else "","text":m.group(2) if m else line})
        return {"rows":rows[-500:],"last_seq":len(lines)}


CTRL=Controller(); TOKEN=secrets.token_urlsafe(24)

class Handler(BaseHTTPRequestHandler):
    server_version="AutoDevELI5/7"
    def log_message(self,fmt,*args): return
    def _json(self,obj,code=200):
        data=json.dumps(obj,ensure_ascii=False).encode(); self.send_response(code); self.send_header("Content-Type","application/json; charset=utf-8"); self.send_header("Cache-Control","no-store"); self.send_header("Content-Length",str(len(data))); self.end_headers(); self.wfile.write(data)
    def _auth(self):
        q=urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query); supplied=self.headers.get("X-AutoDev-Token","") or q.get("token",[""])[0]; return secrets.compare_digest(supplied,TOKEN)
    def do_GET(self):
        parsed=urllib.parse.urlparse(self.path)
        if parsed.path=="/":
            supplied=urllib.parse.parse_qs(parsed.query).get("token",[""])[0]
            if not secrets.compare_digest(supplied,TOKEN):
                self.send_response(302); self.send_header("Location",f"/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"); self.end_headers(); return
            data=HTML_FILE.read_bytes(); self.send_response(200); self.send_header("Content-Type","text/html; charset=utf-8"); self.send_header("Cache-Control","no-store"); self.send_header("Content-Length",str(len(data))); self.end_headers(); self.wfile.write(data); return
        if not self._auth(): self._json({"ok":False,"message":"인증 오류"},403); return
        if parsed.path=="/api/status": self._json(CTRL.status()); return
        if parsed.path=="/api/logs":
            try: after=int(urllib.parse.parse_qs(parsed.query).get("after",["0"])[0])
            except Exception: after=0
            self._json(CTRL.log_rows(after)); return
        self._json({"ok":False,"message":"없는 주소"},404)
    def do_POST(self):
        if not self._auth(): self._json({"ok":False,"message":"인증 오류"},403); return
        path=urllib.parse.urlparse(self.path).path
        if path=="/api/start": self._json(CTRL.start()); return
        if path=="/api/stop": self._json(CTRL.stop()); return
        if path=="/api/recover": self._json(CTRL.recover()); return
        if path=="/api/update":
            r=CTRL.update(); self._json(r)
            if r.get("restarting"): threading.Timer(.4,lambda:os._exit(0)).start()
            return
        if path=="/api/quota/codex/clear": self._json(CTRL.clear_quota("codex")); return
        if path=="/api/quota/grok/clear": self._json(CTRL.clear_quota("grok")); return
        if path=="/api/usage/codex/refresh": self._json(CTRL.refresh_codex_meter()); return
        if path=="/api/open-repo":
            try: subprocess.Popen(["open",str(REPO)]); self._json({"ok":True,"message":"폴더를 열었습니다."})
            except Exception as e: self._json({"ok":False,"message":str(e)},500)
            return
        self._json({"ok":False,"message":"없는 기능"},404)


def write_server_state(): write_json(SERVER_STATE,{"pid":os.getpid(),"port":PORT,"token":TOKEN,"protocol":CONTROL_PROTOCOL})
def cleanup():
    try:
        if read_json(SERVER_STATE).get("pid")==os.getpid(): SERVER_STATE.unlink()
    except Exception: pass

def open_existing_if_any()->bool:
    old=read_json(SERVER_STATE)
    try: port=int(old.get("port",PORT)); token=str(old.get("token","")); pid=int(old.get("pid",0) or 0)
    except Exception: return False
    if token and pid_alive(pid) and server_alive(port):
        webbrowser.open(f"http://{HOST}:{port}/?token={urllib.parse.quote(token)}&r={int(time.time())}"); return True
    return False

def main()->int:
    if open_existing_if_any(): return 0
    try: server=ThreadingHTTPServer((HOST,PORT),Handler)
    except OSError: webbrowser.open(f"http://{HOST}:{PORT}/"); return 0
    write_server_state(); atexit.register(cleanup)
    url=f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"
    resume=os.environ.pop("AUTODEV_RESUME_ENGINE","0")=="1"; refresh=os.environ.pop("AUTODEV_REFRESH_CODEX_USAGE","0")=="1"
    if resume: threading.Timer(.8,CTRL.start).start()
    if refresh or (not cached_codex_usage() and find_cli("codex")): threading.Thread(target=CTRL.refresh_codex_meter,daemon=True).start()
    threading.Timer(.3,lambda:webbrowser.open(url)).start()
    try: server.serve_forever(poll_interval=.25)
    finally: server.server_close()
    return 0
if __name__=="__main__": raise SystemExit(main())
