#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2 macOS WebView dashboard.

Terminal is hidden. This app starts/stops AutoDev v2, shows state/logs/quota
signals, and can fast-forward-update the repository from one window.
"""
from __future__ import annotations

import atexit
import json
import os
import signal
import subprocess
import sys
import threading
import time
from collections import deque
from pathlib import Path
from typing import Any

import webview

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
OUTPUT = REPO / "output" / "autodev_v2"
STATE_CANDIDATES = (
    OUTPUT / "ashes-to-stars" / "state.json",
    OUTPUT / "ashes_to_stars" / "state.json",
)
QUOTA_FILES = {
    "grok": OUTPUT / "grok_quota_exhausted.json",
    "codex": OUTPUT / "codex_quota_exhausted.json",
}
COOLDOWN_SECONDS = 3600


def _read_json(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except Exception:
        return {}


def _state_path() -> Path | None:
    for p in STATE_CANDIDATES:
        if p.exists():
            return p
    try:
        hits = sorted(OUTPUT.glob("*/state.json"))
        return hits[0] if hits else None
    except Exception:
        return None


def _quota_status(path: Path) -> dict[str, Any]:
    data = _read_json(path)
    detected = float(data.get("detected_at", 0) or 0)
    age = max(0, int(time.time() - detected)) if detected else None
    return {
        "detected": bool(detected),
        "active": bool(detected and age is not None and age < COOLDOWN_SECONDS),
        "age_seconds": age,
        "reason": str(data.get("reason", "")),
    }


class AutoDevAPI:
    def __init__(self) -> None:
        self._lock = threading.RLock()
        self._proc: subprocess.Popen[str] | None = None
        self._logs: deque[dict[str, Any]] = deque(maxlen=1800)
        self._seq = 0
        self._last_exit: int | None = None
        self._git_cache: tuple[float, dict[str, Any]] = (0.0, {})

    def _log(self, text: str) -> None:
        with self._lock:
            self._seq += 1
            self._logs.append({
                "seq": self._seq,
                "time": time.strftime("%H:%M:%S"),
                "text": text.rstrip(),
            })

    def _running(self) -> bool:
        return self._proc is not None and self._proc.poll() is None

    def _reader(self, proc: subprocess.Popen[str]) -> None:
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                self._log(line.rstrip("\n"))
        except Exception as e:
            self._log(f"[WEBVIEW] 로그 읽기 오류: {type(e).__name__}: {e}")
        finally:
            rc = proc.poll()
            if rc is None:
                try:
                    rc = proc.wait(timeout=2)
                except Exception:
                    rc = None
            with self._lock:
                if self._proc is proc:
                    self._last_exit = rc
                    self._proc = None
            self._log(f"[WEBVIEW] AutoDev 종료 rc={rc}")

    def start(self) -> dict[str, Any]:
        with self._lock:
            if self._running():
                return {"ok": True, "message": "이미 실행 중입니다."}

            self._log("[WEBVIEW] AutoDev v2 시작 요청")
            env = os.environ.copy()
            env["PYTHONUNBUFFERED"] = "1"
            env["PATH"] = (
                f"{Path.home() / '.local/bin'}:/opt/homebrew/bin:/usr/local/bin:"
                "/usr/bin:/bin:/usr/sbin:/sbin:" + env.get("PATH", "")
            )
            try:
                proc = subprocess.Popen(
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
                self._log(f"[WEBVIEW] 시작 실패: {type(e).__name__}: {e}")
                return {"ok": False, "message": str(e)}

            self._proc = proc
            self._last_exit = None
            threading.Thread(target=self._reader, args=(proc,), daemon=True).start()
            return {"ok": True, "message": f"시작됨 PID {proc.pid}"}

    def stop(self) -> dict[str, Any]:
        with self._lock:
            proc = self._proc
            if proc is None or proc.poll() is not None:
                self._proc = None
                return {"ok": True, "message": "이미 중지되어 있습니다."}
            pid = proc.pid

        self._log(f"[WEBVIEW] AutoDev 중지 요청 PID {pid}")
        try:
            if os.name != "nt":
                os.killpg(pid, signal.SIGTERM)
            else:
                proc.terminate()
            try:
                proc.wait(timeout=4)
            except subprocess.TimeoutExpired:
                if os.name != "nt":
                    os.killpg(pid, signal.SIGKILL)
                else:
                    proc.kill()
            return {"ok": True, "message": "중지했습니다."}
        except Exception as e:
            self._log(f"[WEBVIEW] 중지 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def update(self) -> dict[str, Any]:
        if self._running():
            return {"ok": False, "message": "업데이트 전 AutoDev를 먼저 중지하세요."}
        self._log("[WEBVIEW] git pull --ff-only")
        try:
            r = subprocess.run(
                ["git", "pull", "--ff-only"],
                cwd=REPO,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=120,
            )
            out = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            for line in out.splitlines()[-80:]:
                self._log("[UPDATE] " + line)
            return {
                "ok": r.returncode == 0,
                "message": out[-1200:] or ("업데이트 완료" if r.returncode == 0 else "업데이트 실패"),
            }
        except Exception as e:
            self._log(f"[UPDATE] 실패: {type(e).__name__}: {e}")
            return {"ok": False, "message": str(e)}

    def open_repo(self) -> dict[str, Any]:
        try:
            subprocess.Popen(["open", str(REPO)])
            return {"ok": True}
        except Exception as e:
            return {"ok": False, "message": str(e)}

    def clear_logs(self) -> dict[str, Any]:
        with self._lock:
            self._logs.clear()
        return {"ok": True}

    def logs(self, after: int = 0) -> dict[str, Any]:
        try:
            after_i = int(after)
        except Exception:
            after_i = 0
        with self._lock:
            rows = [x for x in self._logs if int(x["seq"]) > after_i]
            return {"rows": rows[-400:], "last_seq": self._seq}

    def _git_status(self) -> dict[str, Any]:
        now = time.time()
        cached_at, cached = self._git_cache
        if now - cached_at < 5 and cached:
            return cached
        try:
            b = subprocess.run(
                ["git", "branch", "--show-current"], cwd=REPO,
                capture_output=True, text=True, timeout=5,
                encoding="utf-8", errors="replace",
            ).stdout.strip()
            s = subprocess.run(
                ["git", "status", "--porcelain"], cwd=REPO,
                capture_output=True, text=True, timeout=8,
                encoding="utf-8", errors="replace",
            ).stdout.splitlines()
            result = {"branch": b or "?", "dirty_count": len(s)}
        except Exception:
            result = {"branch": "?", "dirty_count": -1}
        self._git_cache = (now, result)
        return result

    def status(self) -> dict[str, Any]:
        with self._lock:
            proc = self._proc
            running = self._running()
            pid = proc.pid if running and proc else None
            last_exit = self._last_exit

        sp = _state_path()
        st = _read_json(sp) if sp else {}
        stats = st.get("stats") if isinstance(st.get("stats"), dict) else {}
        tasks = st.get("tasks") if isinstance(st.get("tasks"), list) else []
        completed = st.get("completed") if isinstance(st.get("completed"), list) else []
        blocked = st.get("blocked") if isinstance(st.get("blocked"), list) else []

        current = None
        if tasks:
            current = next((t for t in tasks if t.get("status") == "working"), None)
            if current is None:
                current = next((t for t in tasks if t.get("status") == "pending"), tasks[0])

        return {
            "running": running,
            "pid": pid,
            "last_exit": last_exit,
            "state_file": str(sp) if sp else "",
            "goal": str(st.get("goal", "")),
            "current": current or {},
            "queue_count": len(tasks),
            "completed_count": len(completed),
            "blocked_count": len(blocked),
            "stats": {
                "grok_calls": int(stats.get("grok_calls", 0) or 0),
                "codex_calls": int(stats.get("codex_calls", 0) or 0),
                "director_calls": int(stats.get("director_calls", 0) or 0),
                "director_local_calls": int(stats.get("director_local_calls", 0) or 0),
                "tasks_done": int(stats.get("tasks_done", 0) or 0),
                "tasks_blocked": int(stats.get("tasks_blocked", 0) or 0),
            },
            "grok_quota": _quota_status(QUOTA_FILES["grok"]),
            "codex_quota": _quota_status(QUOTA_FILES["codex"]),
            "git": self._git_status(),
        }


HTML = r"""
<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>AutoDev v2</title>
<style>
:root{color-scheme:dark;--bg:#0b0f14;--panel:#121821;--panel2:#18212c;--line:#273342;--txt:#e7edf5;--muted:#8fa0b3;--good:#3ddc97;--bad:#ff6677;--warn:#ffc857;--accent:#72a7ff}
*{box-sizing:border-box} body{margin:0;background:var(--bg);color:var(--txt);font:14px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}
header{height:66px;display:flex;align-items:center;justify-content:space-between;padding:0 22px;border-bottom:1px solid var(--line);background:#0e141c}
.brand{font-weight:750;font-size:20px;letter-spacing:.2px}.sub{color:var(--muted);font-size:12px;margin-top:3px}
.actions{display:flex;gap:8px}button{border:1px solid var(--line);background:var(--panel2);color:var(--txt);border-radius:9px;padding:9px 14px;font-weight:650;cursor:pointer}
button:hover{border-color:#496078}button.primary{background:#1f5fff;border-color:#1f5fff}button.stop{background:#4a1d27;border-color:#77303e}
main{padding:18px;display:grid;gap:14px}.cards{display:grid;grid-template-columns:repeat(6,minmax(120px,1fr));gap:10px}
.card{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:13px;min-height:82px}.k{font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:.7px}.v{font-size:20px;font-weight:760;margin-top:8px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.good{color:var(--good)}.bad{color:var(--bad)}.warn{color:var(--warn)}.muted{color:var(--muted)}
.grid2{display:grid;grid-template-columns:1.05fr 1.95fr;gap:14px}.panel{background:var(--panel);border:1px solid var(--line);border-radius:12px;overflow:hidden}
.panel h3{margin:0;padding:12px 14px;border-bottom:1px solid var(--line);font-size:13px}.content{padding:14px}
.goal{font-size:15px;line-height:1.45}.task{margin-top:12px;padding:12px;border:1px solid var(--line);border-radius:10px;background:#0e141c}
.task .id{color:var(--accent);font-weight:700}.task .title{font-size:16px;font-weight:750;margin:5px 0}.task .desc{color:#b9c5d3;line-height:1.45;white-space:pre-wrap}
.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin-top:12px}.mini{background:#0e141c;border:1px solid var(--line);border-radius:9px;padding:9px}.mini b{display:block;font-size:17px;margin-top:4px}
.logbar{display:flex;justify-content:space-between;align-items:center;padding:9px 12px;border-bottom:1px solid var(--line)}.log{height:440px;overflow:auto;background:#080c11;padding:12px;font:12px ui-monospace,SFMono-Regular,Menlo,monospace;white-space:pre-wrap;line-height:1.48}.line{color:#c8d3df}.time{color:#59697a;margin-right:8px}
.toast{position:fixed;right:18px;bottom:18px;background:#1b2632;border:1px solid #3a4c5f;border-radius:10px;padding:11px 14px;max-width:440px;display:none;box-shadow:0 12px 34px #0008}
@media(max-width:1050px){.cards{grid-template-columns:repeat(3,1fr)}.grid2{grid-template-columns:1fr}.log{height:360px}}
</style>
</head>
<body>
<header>
  <div><div class="brand">AutoDev v2</div><div class="sub">재와별 자율 개발 · 로컬 Director 우선 · Grok/Codex 쿼터 보호</div></div>
  <div class="actions">
    <button onclick="doUpdate()">업데이트</button>
    <button onclick="openRepo()">폴더</button>
    <button class="stop" onclick="doStop()">중지</button>
    <button class="primary" onclick="doStart()">시작</button>
  </div>
</header>
<main>
  <section class="cards">
    <div class="card"><div class="k">ENGINE</div><div id="engine" class="v muted">확인중</div></div>
    <div class="card"><div class="k">GROK</div><div id="grok" class="v muted">확인중</div></div>
    <div class="card"><div class="k">CODEX</div><div id="codex" class="v muted">확인중</div></div>
    <div class="card"><div class="k">QUEUE</div><div id="queue" class="v">0</div></div>
    <div class="card"><div class="k">DONE</div><div id="done" class="v good">0</div></div>
    <div class="card"><div class="k">BLOCKED</div><div id="blocked" class="v">0</div></div>
  </section>

  <section class="grid2">
    <div class="panel">
      <h3>현재 개발</h3>
      <div class="content">
        <div class="k">현재 목표</div>
        <div id="goal" class="goal muted">아직 목표가 없습니다.</div>
        <div id="task" class="task">
          <div class="id">대기</div><div class="title">작업 없음</div><div class="desc">Director가 작업을 만들면 여기에 표시됩니다.</div>
        </div>
        <div class="stats">
          <div class="mini"><span class="k">Grok 호출</span><b id="sg">0</b></div>
          <div class="mini"><span class="k">Codex 호출</span><b id="sc">0</b></div>
          <div class="mini"><span class="k">Local Director</span><b id="sdl">0</b></div>
          <div class="mini"><span class="k">Git 변경</span><b id="dirty">0</b></div>
        </div>
        <div class="sub" id="branch" style="margin-top:12px"></div>
      </div>
    </div>

    <div class="panel">
      <div class="logbar"><b>실시간 로그</b><button onclick="clearLogs()">로그 지우기</button></div>
      <div id="log" class="log"></div>
    </div>
  </section>
</main>
<div id="toast" class="toast"></div>
<script>
let lastSeq=0;
const $=id=>document.getElementById(id);
function toast(t){let x=$('toast');x.textContent=t;x.style.display='block';setTimeout(()=>x.style.display='none',3500)}
function quotaText(q){if(q && q.active)return ['한도감지','bad']; if(q && q.detected)return ['재확인대기','warn']; return ['정상','good']}
async function refresh(){
  try{
    const s=await window.pywebview.api.status();
    $('engine').textContent=s.running?`실행중 ${s.pid||''}`:'중지'; $('engine').className='v '+(s.running?'good':'muted');
    let g=quotaText(s.grok_quota);$('grok').textContent=g[0];$('grok').className='v '+g[1];
    let c=quotaText(s.codex_quota);$('codex').textContent=c[0];$('codex').className='v '+c[1];
    $('queue').textContent=s.queue_count; $('done').textContent=s.completed_count; $('blocked').textContent=s.blocked_count;
    $('goal').textContent=s.goal||'아직 목표가 없습니다.';
    const t=s.current||{};
    $('task').innerHTML=t.id?`<div class="id">${esc(t.id)}</div><div class="title">${esc(t.title||'')}</div><div class="desc">${esc(t.goal||'')}</div>`:
      `<div class="id">대기</div><div class="title">작업 없음</div><div class="desc">Director가 작업을 만들면 여기에 표시됩니다.</div>`;
    $('sg').textContent=s.stats.grok_calls; $('sc').textContent=s.stats.codex_calls; $('sdl').textContent=s.stats.director_local_calls;
    $('dirty').textContent=s.git.dirty_count; $('branch').textContent=`Git: ${s.git.branch} · state: ${s.state_file||'없음'}`;
    const l=await window.pywebview.api.logs(lastSeq);
    if(l.rows && l.rows.length){
      let box=$('log'); const near=box.scrollHeight-box.scrollTop-box.clientHeight<80;
      for(const r of l.rows){let d=document.createElement('div');d.className='line';d.innerHTML=`<span class="time">${esc(r.time)}</span>${esc(r.text)}`;box.appendChild(d)}
      while(box.children.length>1000)box.removeChild(box.firstChild);
      if(near)box.scrollTop=box.scrollHeight;
    }
    lastSeq=l.last_seq||lastSeq;
  }catch(e){}
}
function esc(v){return String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]))}
async function doStart(){let r=await window.pywebview.api.start();toast(r.message||'시작');refresh()}
async function doStop(){let r=await window.pywebview.api.stop();toast(r.message||'중지');refresh()}
async function doUpdate(){let r=await window.pywebview.api.update();toast(r.message||'업데이트');refresh()}
async function openRepo(){await window.pywebview.api.open_repo()}
async function clearLogs(){await window.pywebview.api.clear_logs();$('log').innerHTML='';lastSeq=0}
window.addEventListener('pywebviewready',()=>{refresh();setInterval(refresh,1000)});
</script>
</body></html>
"""


def main() -> int:
    if sys.platform != "darwin":
        print("현재 WebView 앱은 macOS용입니다.")
        return 2

    api = AutoDevAPI()
    atexit.register(api.stop)
    window = webview.create_window(
        "AutoDev v2",
        html=HTML,
        js_api=api,
        width=1320,
        height=820,
        min_size=(980, 680),
        background_color="#0b0f14",
    )
    try:
        window.events.closing += lambda: api.stop()
    except Exception:
        pass
    webview.start(debug=False)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
