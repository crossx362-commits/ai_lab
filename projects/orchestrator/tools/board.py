#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""오케스트레이터 보드 — 지금·다음·막힘을 한 화면에서 본다.

    python3 tools/board.py        → http://127.0.0.1:8767

지휘보드(loop/board.py)의 규칙을 그대로 따른다:
1. **한 화면이 철칙.** 스크롤 없이 지금·다음·막힘이 보여야 한다.
2. 칸·제목은 짧은 한국어. 내부 코드 이름을 그대로 올리지 않는다.
3. 오너에게 일을 시키지 않는다 — 화면이 하는 일은 보여주기·명령 받기·세우기뿐이다.

읽는 것: state/autodev.sqlite3 · BOARD.md · state/STOP · logs/
쓰는 것: BOARD.md(명령 추가) · state/STOP(세우기/풀기). **그 밖에는 아무것도 건드리지 않는다.**
"""
from __future__ import annotations

import html
import json
import os
import sqlite3
import sys
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parent.parent
DB = ROOT / "state" / "autodev.sqlite3"
BOARD = ROOT / "BOARD.md"
STOP = ROOT / "state" / "STOP"
LOGS = ROOT / "logs"

HOST = os.getenv("BOARD_HOST", "127.0.0.1")
PORT = int(os.getenv("BOARD_PORT", "8767"))

VERDICT_COLOR = {"PASS": "ok", "FAILED": "bad", "UNKNOWN": "warn", None: "mute"}
STATUS_COLOR = {"DONE": "ok", "RUNNING": "run", "BLOCKED": "bad", "STOPPED": "warn"}


def rows(q, args=()):
    if not DB.is_file():
        return []
    c = sqlite3.connect(f"file:{DB}?mode=ro", uri=True)
    c.row_factory = sqlite3.Row
    try:
        return c.execute(q, args).fetchall()
    except sqlite3.Error:
        return []
    finally:
        c.close()


def ago(ts):
    if not ts:
        return "-"
    d = time.time() - ts
    if d < 60:
        return f"{int(d)}초 전"
    if d < 3600:
        return f"{int(d // 60)}분 전"
    if d < 86400:
        return f"{int(d // 3600)}시간 전"
    return f"{int(d // 86400)}일 전"


def board_section(name: str) -> list[str]:
    if not BOARD.is_file():
        return []
    out, hit = [], False
    for ln in BOARD.read_text(encoding="utf-8").splitlines():
        if ln.startswith("## "):
            hit = ln[3:].strip() == name
            continue
        if hit and ln.startswith("- "):
            out.append(ln[2:].strip())
    return out


def add_command(text: str) -> None:
    """BOARD.md 「명령」 맨 위에 한 줄 붙인다. 파일 전체를 다시 쓰지 않는다."""
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M")
    line = f"- [{stamp}] {text.strip()}"
    lines = BOARD.read_text(encoding="utf-8").splitlines()
    for i, ln in enumerate(lines):
        if ln.strip() == "## 명령":
            lines.insert(i + 1, line)
            break
    else:
        lines += ["", "## 명령", line]
    BOARD.write_text("\n".join(lines) + "\n", encoding="utf-8")


def is_noise(goal: str) -> bool:
    """게이트를 시험하려고 일부러 실패시킨 판은 보드의 '막힘'이 아니다.

    이걸 섞으면 진짜 막힌 것이 시험용 빨간불에 묻힌다 — 화면을 못 믿게 되는 첫 단계다.
    """
    g = (goal or "").strip()
    return g.startswith("[NC]") or g.startswith("[병렬]")


def is_hidden(row) -> bool:
    return is_noise(row["goal"]) or row["status"] == "ARCHIVED"


def gather() -> dict:
    tasks = [t for t in rows("SELECT * FROM tasks ORDER BY id DESC LIMIT 120")
             if not is_hidden(t)][:14]
    running = [t for t in tasks if t["status"] == "RUNNING"]
    blocked = [t for t in tasks if t["status"] in ("BLOCKED", "STOPPED")]
    live = rows("SELECT * FROM processes WHERE status='RUNNING' ORDER BY started_at DESC LIMIT 6")

    cur = None
    if running:
        t = running[0]
        at = rows("SELECT * FROM attempts WHERE task_id=? ORDER BY n DESC LIMIT 1", (t["id"],))
        cur = {
            "id": t["id"], "goal": t["goal"], "branch": t["branch"] or "-",
            "attempt": at[0]["n"] if at else 0,
            "step": (at[0]["status"] if at else "-"),
            "since": ago(t["created_at"]),
        }

    # 사용량도 진짜 작업 기준으로 센다 — 시험판 호출까지 섞으면 비용 감각이 망가진다.
    usage = rows(
        "SELECT u.agent agent, COUNT(*) n, SUM(u.ok) ok, SUM(u.duration_s) secs"
        " FROM usage u JOIN tasks t ON t.id=u.task_id"
        " WHERE t.status<>'ARCHIVED' AND t.goal NOT LIKE '[NC]%' AND t.goal NOT LIKE '[병렬]%'"
        " GROUP BY u.agent"
    )
    counted = rows("SELECT status, goal FROM tasks")
    real = [t for t in counted if not is_hidden(t)]
    done = [t for t in real if t["status"] == "DONE"]

    import shutil
    free = shutil.disk_usage(str(ROOT)).free / (1024 ** 3)

    return {
        "stopped": STOP.exists(),
        "free_gb": free,
        "current": cur,
        "tasks": [dict(t) for t in tasks],
        "blocked": [dict(t) for t in blocked],
        "live": [dict(p) for p in live],
        "usage": [dict(u) for u in usage],
        "done": len(done),
        "total": len(real),
        "nc": len(counted) - len(real),
        "commands": board_section("명령")[:3],
        "waiting": board_section("결정대기")[:4],
        "phases": board_section("단계"),
        "stuck": board_section("막힘")[:3],
        "now": datetime.now().strftime("%H:%M:%S"),
    }


PAGE = """<!doctype html><html lang=ko><meta charset=utf-8>
<title>오케스트레이터 보드</title>
<meta name=viewport content="width=device-width,initial-scale=1">
<style>
:root{--bg:#0f1115;--card:#171a21;--line:#252a34;--fg:#e6e9ef;--mute:#8b93a3;
      --ok:#3fb950;--bad:#f85149;--warn:#d29922;--run:#58a6ff}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);font:13px/1.5 -apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo",sans-serif}
header{display:flex;align-items:center;gap:12px;padding:10px 16px;border-bottom:1px solid var(--line);position:sticky;top:0;background:var(--bg);z-index:2}
h1{font-size:15px;margin:0;font-weight:600}
/* 한 화면 철칙: 화면 높이를 다 쓰되 스크롤은 만들지 않는다.
   넓은 화면에서 아래 절반이 비던 것을 고쳤다 — 남는 높이는 '최근 작업'이 먹는다. */
.grid{display:grid;gap:10px;padding:10px 16px;
      grid-template-columns:minmax(240px,1fr) minmax(320px,1.6fr) minmax(240px,1.1fr);
      grid-template-rows:auto minmax(0,1fr) auto;height:calc(100vh - 46px);
      grid-template-areas:"now tasks phases" "stuck tasks phases" "cmd tasks usage"}
@media (max-width:900px){.grid{height:auto;grid-template-columns:1fr;
      grid-template-areas:"now" "tasks" "stuck" "phases" "cmd" "usage"}}
#c-now{grid-area:now}#c-tasks{grid-area:tasks}#c-phases{grid-area:phases}
#c-stuck{grid-area:stuck}#c-cmd{grid-area:cmd}#c-usage{grid-area:usage}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px;
      min-width:0;min-height:0;display:flex;flex-direction:column}
.card>div,.card>ul{overflow:auto;min-height:0}
.span2{grid-row:span 2}
.card h2{font-size:12px;margin:0 0 8px;color:var(--mute);font-weight:600;letter-spacing:.04em}
.big{font-size:15px;font-weight:600}
.mute{color:var(--mute)}
.ok{color:var(--ok)}.bad{color:var(--bad)}.warn{color:var(--warn)}.run{color:var(--run)}
.pill{display:inline-block;padding:1px 7px;border-radius:999px;border:1px solid var(--line);font-size:11px}
table{width:100%;border-collapse:collapse}
td{padding:3px 4px;border-bottom:1px solid var(--line);vertical-align:top}
td.g{width:100%;max-width:1px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
td.n{white-space:nowrap}
button{background:#222833;color:var(--fg);border:1px solid var(--line);border-radius:7px;padding:5px 11px;cursor:pointer;font:inherit}
button:hover{border-color:#3b4250}
button.danger{border-color:#5a2b2b;color:#ff9d97}
input{flex:1;min-width:120px;background:#0c0e13;border:1px solid var(--line);border-radius:7px;color:var(--fg);padding:6px 9px;font:inherit}
form{display:flex;gap:6px;margin-top:6px}
ul{margin:0;padding-left:16px}li{margin:2px 0}
</style>
<header>
  <h1>오케스트레이터 보드</h1>
  <span id=state class=pill>…</span>
  <span id=disk class="pill mute">…</span>
  <span style=flex:1></span>
  <button onclick="act('stop')" class=danger>세우기</button>
  <button onclick="act('resume')">풀기</button>
  <span id=clock class=mute></span>
</header>
<div class=grid>
  <div class=card id=c-now><h2>지금</h2><div id=now>…</div></div>
  <div class=card id=c-tasks><h2>최근 작업</h2><div id=tasks>…</div></div>
  <div class=card id=c-phases><h2>단계</h2><div id=phases>…</div></div>
  <div class=card id=c-stuck><h2>막힘</h2><div id=stuck>…</div></div>
  <div class=card id=c-cmd><h2>명령</h2><div id=cmds>…</div>
    <form onsubmit="return send(event)"><input id=cmd placeholder="명령을 적는다"><button>남기기</button></form>
  </div>
  <div class=card id=c-usage><h2>사용량</h2><div id=usage>…</div></div>
</div>
<script>
const E=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const SC={DONE:'ok',RUNNING:'run',BLOCKED:'bad',STOPPED:'warn'};
const VC={PASS:'ok',FAILED:'bad',UNKNOWN:'warn'};
async function load(){
  const d=await (await fetch('/api')).json();
  document.getElementById('clock').textContent=d.now;
  const st=document.getElementById('state');
  st.textContent=d.stopped?'세워짐':(d.current?'작업 중':'대기');
  st.className='pill '+(d.stopped?'bad':(d.current?'run':'mute'));
  document.getElementById('disk').textContent=
    '여유 '+d.free_gb.toFixed(1)+'GB · 완료 '+d.done+'/'+d.total+' · 시험판 '+d.nc+'건 숨김';

  document.getElementById('now').innerHTML = d.current
    ? `<div class=big>#${d.current.id} ${E(d.current.goal).slice(0,70)}</div>
       <div class=mute>시도 ${d.current.attempt} · ${E(d.current.step)} · ${E(d.current.branch)} · ${E(d.current.since)}</div>`
    : (d.live.length? `<div class=mute>프로세스 ${d.live.length}개 실행 중</div>`
                    : '<div class=mute>진행 중인 작업 없음</div>');

  const stuck=[...d.blocked.map(t=>`<li><span class="${SC[t.status]||'mute'}">#${t.id}</span> ${E(t.reason||t.goal).slice(0,60)}</li>`),
               ...d.stuck.map(s=>`<li class=mute>${E(s).slice(0,70)}</li>`)];
  document.getElementById('stuck').innerHTML = stuck.length?`<ul>${stuck.slice(0,6).join('')}</ul>`:'<div class=mute>없음</div>';

  document.getElementById('tasks').innerHTML='<table>'+d.tasks.map(t=>
    `<tr><td class=n>#${t.id}</td><td class="n ${SC[t.status]||'mute'}">${E(t.status)}</td>
     <td class="n ${VC[t.verdict]||'mute'}">${E(t.verdict||'-')}</td><td class="n mute">${t.attempts}회</td>
     <td class=g title="${E(t.goal)}">${E(t.goal)}</td></tr>`).join('')+'</table>';

  document.getElementById('phases').innerHTML='<ul>'+d.phases.map(p=>{
    const done=p.includes('완료'), wait=p.includes('대기');
    return `<li class="${done?'ok':(wait?'mute':'run')}">${E(p)}</li>`;}).join('')+'</ul>';

  document.getElementById('cmds').innerHTML=d.commands.length?
    '<ul>'+d.commands.map(c=>`<li>${E(c)}</li>`).join('')+'</ul>':'<div class=mute>없음</div>';

  document.getElementById('usage').innerHTML=d.usage.length?'<table>'+d.usage.map(u=>
    `<tr><td>${E(u.agent)}</td><td class=mute>${u.n}회</td><td class=ok>성공 ${u.ok||0}</td>
     <td class=mute>${Math.round(u.secs||0)}초</td></tr>`).join('')+'</table>':'<div class=mute>없음</div>';
}
async function act(a){ await fetch('/'+a,{method:'POST'}); load(); }
async function send(e){ e.preventDefault(); const i=document.getElementById('cmd');
  if(!i.value.trim())return false;
  await fetch('/command',{method:'POST',body:i.value}); i.value=''; load(); return false; }
load(); setInterval(load,3000);
</script></html>"""


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body, ctype="text/html; charset=utf-8"):
        data = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = urlparse(self.path).path
        if path == "/":
            self._send(200, PAGE)
        elif path == "/api":
            self._send(200, json.dumps(gather(), ensure_ascii=False), "application/json; charset=utf-8")
        else:
            self._send(404, "없음")

    def do_POST(self):
        path = urlparse(self.path).path
        if path == "/stop":
            STOP.parent.mkdir(parents=True, exist_ok=True)
            STOP.write_text(f"{time.time()}\n보드에서 세움\n", encoding="utf-8")
            self._send(200, "ok")
        elif path == "/resume":
            if STOP.exists():
                STOP.unlink()
            self._send(200, "ok")
        elif path == "/command":
            n = int(self.headers.get("Content-Length", 0))
            text = self.rfile.read(n).decode("utf-8", "replace")
            if text.strip():
                add_command(text)
            self._send(200, "ok")
        else:
            self._send(404, "없음")

    def log_message(self, *a):
        pass


def main():
    srv = ThreadingHTTPServer((HOST, PORT), Handler)
    print(f"오케스트레이터 보드: http://{HOST}:{PORT}  (Ctrl+C 종료)")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료")


if __name__ == "__main__":
    main()
