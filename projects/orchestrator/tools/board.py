#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""오케스트레이터 보드 — 지금·다음·막힘을 한 화면에서 본다.

    python3 tools/board.py        → http://127.0.0.1:8767

지휘보드(loop/board.py)의 규칙을 그대로 따른다:
1. **한 화면이 철칙.** 스크롤 없이 지금·다음·막힘이 보여야 한다.
2. 칸·제목은 짧은 한국어. 내부 코드 이름을 그대로 올리지 않는다.
3. 오너에게 일을 시키지 않는다 — 화면이 하는 일은 보여주기·명령 받기·세우기뿐이다.

읽는 것: state/orch.sqlite3 · BOARD.md · state/STOP · logs/
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
DB = ROOT / "state" / "orch.sqlite3"
BOARD = ROOT / "BOARD.md"
LOGDIR = ROOT / "logs"
STOP = ROOT / "state" / "STOP"
LOGS = ROOT / "logs"

HOST = os.getenv("BOARD_HOST", "127.0.0.1")
PORT = int(os.getenv("BOARD_PORT", "8767"))

VERDICT_COLOR = {"PASS": "ok", "FAILED": "bad", "UNKNOWN": "warn", None: "mute"}
STATUS_COLOR = {"DONE": "ok", "RUNNING": "run", "BLOCKED": "bad",
                "STOPPED": "warn", "REVIEW": "warn", "INTERRUPTED": "warn"}


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


def nc_plan_ids() -> set:
    """시험판 계획의 번호. **그 계획이 낳은 하위 Task는 목표 문구에 [NC]가 없다** —
    목표만 보고 걸렀더니 "첫 번째"·"두 번째" 같은 시험 부스러기가 목록과 사용량에 새어 나왔다."""
    return {r["id"] for r in rows("SELECT id, goal FROM plans") if is_noise(r["goal"])}


def is_hidden(row, nc_plans: set | None = None) -> bool:
    if is_noise(row["goal"]) or row["status"] == "ARCHIVED":
        return True
    keys = row.keys() if hasattr(row, "keys") else row
    return "plan_id" in keys and row["plan_id"] in (nc_plans if nc_plans is not None else nc_plan_ids())


_MEM_CACHE = {"ts": 0.0, "val": None}


def mem_state() -> dict:
    """메모리는 재는 데 1초쯤 걸린다 — 3초마다 새로 재면 보드가 기계를 더 느리게 만든다.
    10초 캐시로 둔다(보드가 문제의 일부가 되면 안 된다)."""
    import sys
    if time.time() - _MEM_CACHE["ts"] < 10 and _MEM_CACHE["val"]:
        return _MEM_CACHE["val"]
    sys.path.insert(0, str(ROOT))
    try:
        from orch_core import memory
        snap = memory.sample(record=False)
        a = memory.assess(snap)
        val = {"state": a.state, "line": snap.line(), "reasons": a.reasons,
               "procs": {k: round(v) for k, v in sorted(snap.procs.items(), key=lambda x: -x[1])}}
    except Exception as e:
        val = {"state": "UNKNOWN", "line": f"메모리를 재지 못했다: {e}", "reasons": [], "procs": {}}
    _MEM_CACHE.update(ts=time.time(), val=val)
    return val


def provider_state() -> list[dict]:
    """저장된 Provider 상태를 **읽기만** 한다.

    보드가 직접 인증을 물으면(3초마다) CLI를 두드려 기계를 더 느리게 만든다.
    그래서 여기서는 마지막 검사 결과만 보여주고 **언제 잰 것인지**를 같이 적는다 —
    오래된 값을 지금 값인 척하지 않는 것이 이 보드의 규칙이다.
    """
    f = ROOT / "state" / "providers.json"
    if not f.is_file():
        return []
    try:
        d = json.loads(f.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return []
    out = []
    now = time.time()
    for name, r in d.items():
        if name.startswith("nc"):    # 시험용 — 작업 목록의 [NC]와 같은 원칙으로 숨긴다
            continue
        cool = r.get("cooldown_until", 0) or 0
        out.append({
            "name": name,
            "state": r.get("state", "?"),
            "detail": (r.get("detail") or "")[:60],
            "checked": ago(r.get("checked_at", 0)),
            "cool_min": int((cool - now) / 60) if cool > now else 0,
            "usable": r.get("state") in ("AVAILABLE", "LIMITED") and cool <= now,
        })
    return out


def log_tail(task_id: int, n: int = 14) -> list[str]:
    """그 Task의 가장 최근 로그 꼬리. 무엇을 하는 중인지 화면에서 바로 보이게."""
    # stdout만 보면 안 된다 — 한도·인증 오류는 stderr에만 있었다(2026-09-11 실전).
    cands = sorted([*LOGDIR.glob(f"task{task_id:04d}-*.stdout.log"),
                    *LOGDIR.glob(f"task{task_id:04d}-*.stderr.log")],
                   key=lambda p: p.stat().st_mtime, reverse=True)
    for f in cands:
        try:
            lines = [ln for ln in f.read_text(encoding="utf-8", errors="replace").splitlines() if ln.strip()]
        except OSError:
            continue
        if lines:
            return [f.name] + lines[-n:]
    return []


def task_detail(task_id: int) -> dict:
    t = rows("SELECT * FROM tasks WHERE id=?", (task_id,))
    if not t:
        return {"error": f"task {task_id} 없음"}
    t = dict(t[0])
    at = [dict(a) for a in rows("SELECT * FROM attempts WHERE task_id=? ORDER BY n", (task_id,))]
    pr = [dict(p) for p in rows(
        "SELECT kind,status,exit_code,started_at,ended_at FROM processes WHERE task_id=?"
        " ORDER BY id DESC LIMIT 8", (task_id,))]
    for p in pr:
        p["took"] = round((p["ended_at"] or time.time()) - p["started_at"], 1)
    return {"task": t, "attempts": at, "procs": pr, "log": log_tail(task_id)}


def gather() -> dict:
    ncp = nc_plan_ids()
    tasks = [t for t in rows("SELECT * FROM tasks ORDER BY id DESC LIMIT 120")
             if not is_hidden(t, ncp)][:14]
    running = [t for t in tasks if t["status"] == "RUNNING"]
    blocked = [t for t in tasks if t["status"] in ("BLOCKED", "STOPPED", "INTERRUPTED", "REVIEW")]
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
        "SELECT u.agent agent, COUNT(*) n, SUM(u.ok) ok, SUM(u.duration_s) secs,"
        " SUM(u.tokens) toks, SUM(CASE WHEN u.tokens_src='measured' THEN 1 ELSE 0 END) tn"
        " FROM usage u JOIN tasks t ON t.id=u.task_id"
        " WHERE t.status<>'ARCHIVED' AND t.goal NOT LIKE '[NC]%' AND t.goal NOT LIKE '[병렬]%'"
        "   AND (t.plan_id IS NULL OR t.plan_id NOT IN"
        "        (SELECT id FROM plans WHERE goal LIKE '[NC]%' OR goal LIKE '[병렬]%'))"
        " GROUP BY u.agent"
    )
    counted = rows("SELECT status, goal, plan_id FROM tasks")
    real = [t for t in counted if not is_hidden(t, ncp)]
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
        "mem": mem_state(),
        "providers": provider_state(),
        "now": datetime.now().strftime("%H:%M:%S"),
    }


PAGE = """<!doctype html><html lang=ko><meta charset=utf-8>
<title>오케스트레이터 보드</title>
<meta name=viewport content="width=device-width,initial-scale=1">
<style>
:root{--bg:#0f1115;--card:#171a21;--line:#252a34;--fg:#e6e9ef;--mute:#8b93a3;
      --ok:#3fb950;--bad:#f85149;--warn:#d29922;--run:#58a6ff}
*{box-sizing:border-box}
/* 한 화면 철칙을 헤더 높이 추정(calc(100vh - 46px))에 걸어두면, 글꼴·패딩이 조금만 바뀌어도
   몇 px씩 넘쳐 스크롤이 생긴다(실측 7px 초과). 숫자를 고치지 말고 가정을 없앤다 —
   body를 세로 flex로 두고 격자가 남은 높이를 먹게 한다. */
body{margin:0;background:var(--bg);color:var(--fg);font:13px/1.5 -apple-system,BlinkMacSystemFont,"Apple SD Gothic Neo",sans-serif;
     height:100vh;display:flex;flex-direction:column;overflow:hidden}
header{display:flex;align-items:center;gap:12px;padding:10px 16px;border-bottom:1px solid var(--line);position:sticky;top:0;background:var(--bg);z-index:2}
h1{font-size:15px;margin:0;font-weight:600}
/* 한 화면 철칙: 화면 높이를 다 쓰되 스크롤은 만들지 않는다.
   넓은 화면에서 아래 절반이 비던 것을 고쳤다 — 남는 높이는 '최근 작업'이 먹는다. */
.grid{display:grid;gap:10px;padding:10px 16px;
      grid-template-columns:minmax(240px,1fr) minmax(320px,1.6fr) minmax(240px,1.1fr);
      grid-template-rows:auto minmax(0,1fr) auto;flex:1;min-height:0;
      grid-template-areas:"now tasks phases" "stuck tasks prov" "cmd tasks usage"}
@media (max-width:900px){body{height:auto;overflow:auto}.grid{flex:none;grid-template-columns:1fr;
      grid-template-areas:"now" "tasks" "stuck" "prov" "phases" "cmd" "usage"}}
#c-now{grid-area:now}#c-tasks{grid-area:tasks}#c-phases{grid-area:phases}
#c-stuck{grid-area:stuck}#c-cmd{grid-area:cmd}#c-usage{grid-area:usage}#c-prov{grid-area:prov}
/* 상세는 한 화면 철칙을 깨지 않도록 덮어서 띄운다 — 목록이 밀려나면 전체가 안 보인다. */
#ov{position:fixed;inset:0;background:rgba(8,10,14,.82);display:none;z-index:9;padding:28px}
#ov.on{display:flex;justify-content:center}
#ovbox{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 18px;
       width:min(880px,100%);max-height:100%;overflow:auto}
pre{background:#0c0e13;border:1px solid var(--line);border-radius:8px;padding:8px 10px;
    margin:6px 0 0;overflow:auto;font-size:11px;line-height:1.45;max-height:240px}
tr.click{cursor:pointer}tr.click:hover td{background:#1c212b}
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
  <div class=card id=c-prov><h2>Provider</h2><div id=prov>…</div></div>
  <div class=card id=c-usage><h2>사용량 · 메모리</h2><div id=usage>…</div></div>
</div>
<div id=ov onclick="if(event.target.id==='ov')closeOv()"><div id=ovbox>…</div></div>
<script>
const E=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const SC={DONE:'ok',RUNNING:'run',BLOCKED:'bad',STOPPED:'warn',REVIEW:'warn',INTERRUPTED:'warn'};
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
    `<tr class=click onclick="detail(${t.id})"><td class=n>#${t.id}</td><td class="n ${SC[t.status]||'mute'}">${E(t.status)}</td>
     <td class="n ${VC[t.verdict]||'mute'}">${E(t.verdict||'-')}</td><td class="n mute">${t.attempts}회</td>
     <td class=g title="${E(t.goal)}">${E(t.goal)}</td></tr>`).join('')+'</table>';

  document.getElementById('phases').innerHTML='<ul>'+d.phases.map(p=>{
    const done=p.includes('완료'), wait=p.includes('대기');
    return `<li class="${done?'ok':(wait?'mute':'run')}">${E(p)}</li>`;}).join('')+'</ul>';

  document.getElementById('cmds').innerHTML=d.commands.length?
    '<ul>'+d.commands.map(c=>`<li>${E(c)}</li>`).join('')+'</ul>':'<div class=mute>없음</div>';

  const PC={AVAILABLE:'ok',LIMITED:'warn',RATE_LIMITED:'warn',AUTH_REQUIRED:'bad',
            UNAVAILABLE:'bad',ERROR:'bad',DISABLED:'mute'};
  document.getElementById('prov').innerHTML = d.providers.length
    ? '<table>'+d.providers.map(p=>
        `<tr><td class=n>${p.usable?'<span class=ok>✓</span>':'<span class=bad>✗</span>'} ${E(p.name)}</td>
         <td class="n ${PC[p.state]||'mute'}">${E(p.state)}</td>
         <td class="n mute">${p.cool_min?p.cool_min+'분 후':E(p.checked)}</td></tr>`).join('')+'</table>'
      + '<div class=mute style="margin-top:6px;font-size:11px">마지막 검사 기준 · 갱신: orch providers --refresh</div>'
    : '<div class=mute>아직 검사한 적 없다 — orch providers</div>';

  const mc={GREEN:'ok',YELLOW:'warn',RED:'bad'}[d.mem.state]||'mute';
  const memHtml=`<div><span class="pill ${mc}">${E(d.mem.state)}</span> <span class=mute>${E(d.mem.line)}</span></div>`
    + (Object.keys(d.mem.procs).length? `<div class=mute style="margin:4px 0 8px">`
        + Object.entries(d.mem.procs).map(([k,v])=>`${E(k)} ${v}MB`).join(' · ') + '</div>' : '');
  document.getElementById('usage').innerHTML=memHtml+(d.usage.length?'<table>'+d.usage.map(u=>
    `<tr><td>${E(u.agent)}</td><td class=mute>${u.n}회</td><td class=ok>성공 ${u.ok||0}</td>
     <td class=mute>${Math.round(u.secs||0)}초</td>
     <td class=mute title="CLI가 스스로 보고한 값만 센다 — 어림수는 넣지 않는다">${
       u.tn ? (u.toks||0).toLocaleString()+'토큰'+(u.tn<u.n?` (${u.tn}/${u.n}회만 보고)`:'')
            : '토큰 보고 없음'}</td></tr>`).join('')+'</table>':'');
}
const VC2={PASS:'ok',FAILED:'bad',UNKNOWN:'warn',REJECTED:'bad',APPROVE:'ok'};
async function detail(id){
  const b=document.getElementById('ovbox');
  b.innerHTML='<div class=mute>읽는 중…</div>';
  document.getElementById('ov').classList.add('on');
  const d=await (await fetch('/api/task/'+id)).json();
  if(d.error){ b.innerHTML=`<div class=bad>${E(d.error)}</div>`; return; }
  const t=d.task;
  const att=d.attempts.map(a=>
    `<tr><td class=n>시도 ${a.n}</td><td class=n>${E(a.agent||'-')}</td>
     <td class="n ${VC2[a.status]||'mute'}">${E(a.status)}</td>
     <td class=n mute>${a.changed_files??'-'}파일</td>
     <td class=g title="${E(a.reason||'')}">${E(a.reason||'')}</td></tr>`).join('');
  const prc=d.procs.map(p=>
    `<tr><td class=n>${E(p.kind)}</td><td class="n ${p.status==='EXITED'?'mute':'warn'}">${E(p.status)}</td>
     <td class=n mute>exit ${p.exit_code??'-'}</td><td class=n mute>${p.took}초</td></tr>`).join('');
  b.innerHTML=`
    <div style="display:flex;gap:10px;align-items:center">
      <div class=big>#${t.id} ${E(t.goal)}</div><span style=flex:1></span>
      <button onclick=closeOv()>닫기</button></div>
    <div class=mute style="margin:6px 0">
      <span class="pill ${SC[t.status]||'mute'}">${E(t.status)}</span>
      <span class="pill ${VC2[t.verdict]||'mute'}">${E(t.verdict||'-')}</span>
      ${E(t.branch||'-')} · ${E((t.commit_hash||'').slice(0,10)||'커밋 없음')} · 시도 ${t.attempts}회</div>
    ${t.done_criteria?`<div class=mute style="margin:6px 0"><b>완료 조건</b> ${E(t.done_criteria)}</div>`:''}
    ${t.reason?`<div style="margin:6px 0"><b>판정</b> ${E(t.reason)}</div>`:''}
    ${t.review?`<div style="margin:6px 0"><b>리뷰</b> ${E(t.review)}</div>`:''}
    <h2 style="margin-top:12px">시도</h2><table>${att||'<tr><td class=mute>없음</td></tr>'}</table>
    <h2 style="margin-top:12px">프로세스</h2><table>${prc||'<tr><td class=mute>없음</td></tr>'}</table>
    <h2 style="margin-top:12px">로그 꼬리</h2>
    ${d.log.length?`<div class=mute style=font-size:11px>${E(d.log[0])}</div>
       <pre>${E(d.log.slice(1).join('\\n'))}</pre>`:'<div class=mute>로그 없음</div>'}`;
}
function closeOv(){ document.getElementById('ov').classList.remove('on'); }
document.addEventListener('keydown',e=>{ if(e.key==='Escape')closeOv(); });
async function act(a){ await fetch('/'+a,{method:'POST'}); load(); }
async function send(e){ e.preventDefault(); const i=document.getElementById('cmd');
  if(!i.value.trim())return false;
  await fetch('/command',{method:'POST',body:i.value}); i.value=''; load(); return false; }
load(); setInterval(()=>{ if(!document.getElementById('ov').classList.contains('on')) load(); },3000);
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
        elif path.startswith("/api/task/"):
            try:
                tid = int(path.rsplit("/", 1)[-1])
            except ValueError:
                self._send(404, "없음"); return
            self._send(200, json.dumps(task_detail(tid), ensure_ascii=False, default=str),
                       "application/json; charset=utf-8")
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
