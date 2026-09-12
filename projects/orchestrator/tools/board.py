#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""오케스트레이터 보드 — 지금·다음·막힘을 한 화면에서 본다.

    python3 tools/board.py        → http://127.0.0.1:8767

지휘보드(loop/board.py)의 규칙을 그대로 따른다:
1. **한 화면이 철칙.** 스크롤 없이 지금·다음·막힘이 보여야 한다.
2. 칸·제목은 짧은 한국어. 내부 코드 이름을 그대로 올리지 않는다.
3. 오너에게 일을 시키지 않는다 — 화면이 하는 일은 보여주기·명령 받기·세우기뿐이다.

읽는 것: state/orch.sqlite3 · BOARD.md · state/STOP · logs/
쓰는 것: state/board_commands.sqlite3(영속 명령 접수) · state/STOP(세우기/풀기).
명령 실행은 별도 launchd 소비자 orch_core.board_commands가 담당한다.
"""
from __future__ import annotations

import html
import json
import os
import sqlite3
import sys
import time
import uuid
import threading
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
from orch_core.board_commands import Queue
from orch_core import board_metrics, board_quota, config, providers
QUOTA = board_quota.QuotaCache()
DB = ROOT / "state" / "orch.sqlite3"
BOARD = ROOT / "BOARD.md"
LOGDIR = ROOT / "logs"
REPORTS = ROOT / "docs" / "reports"     # 완료 보고(마크다운 + 스크린샷). 보드에서 읽는다.
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


def report_list() -> list[dict]:
    """docs/reports/*.md — 최신이 위. 제목은 첫 줄 '# '에서 읽는다."""
    if not REPORTS.is_dir():
        return []
    out = []
    for f in sorted(REPORTS.glob("*.md"), key=lambda p: p.stat().st_mtime, reverse=True):
        try:
            first = f.read_text(encoding="utf-8").splitlines()[0]
        except (OSError, IndexError):
            first = f.name
        out.append({"name": f.name, "title": first.lstrip("# ").strip() or f.name,
                    "when": ago(f.stat().st_mtime)})
    return out


def doing_of(proc) -> str:
    """살아 있는 프로세스 한 줄을 「누가 무엇을」로 옮긴다. 리뷰는 로그 이름에 review-<이름>이 박혀 있다."""
    kind = proc["kind"]
    sp = str(proc["stdout_path"] or "")
    if ".review-" in sp:
        who = sp.split(".review-", 1)[1].split(".", 1)[0]
        return f"{who} 리뷰 중"
    if kind == "agent":
        try:
            cmd = json.loads(proc["cmd"])
            who = pathlib_name(cmd[0]) if isinstance(cmd, list) and cmd else "?"
        except (json.JSONDecodeError, TypeError):
            who = "?"
        return f"{who} 구현 중"
    if kind == "unity":
        return "Unity 컴파일 중"
    if kind == "unity-test":
        return "Unity 테스트 중"
    return f"{kind} 실행 중"


def pathlib_name(p: str) -> str:
    return str(p).rsplit("/", 1)[-1]


_PROVIDER_REFRESH = threading.Lock()
_PROVIDER_ERROR = None
_PROVIDER_REFRESH_AT = 0.0


def refresh_providers():
    global _PROVIDER_REFRESH_AT
    if not _PROVIDER_REFRESH.acquire(blocking=False):
        return
    _PROVIDER_REFRESH_AT = time.time()
    def run():
        global _PROVIDER_ERROR
        try:
            providers.probe_all(config.load(), force=True)
            _PROVIDER_ERROR = None
        except Exception as exc:
            _PROVIDER_ERROR = f"AI 상태 확인 실패: {type(exc).__name__}"
        finally:
            _PROVIDER_REFRESH.release()
    threading.Thread(target=run, daemon=True).start()


def gather() -> dict:
    ncp = nc_plan_ids()
    all_tasks = [dict(t) for t in rows("SELECT * FROM tasks ORDER BY id DESC")]
    real = [t for t in all_tasks if not is_hidden(t,ncp)]
    task_by_id = {t['id']:t for t in real}
    tasks = real[:14]
    running = [t for t in real if t["status"] == "RUNNING"]
    blocked = [t for t in real if t["status"] in ("BLOCKED", "STOPPED", "INTERRUPTED", "REVIEW")]
    candidates = [dict(p) for p in rows("SELECT * FROM processes WHERE status='RUNNING' ORDER BY started_at DESC")
                  if p['task_id'] in task_by_id]
    cq = Queue().snapshot()
    for c in cq['active']:
        candidates.append({'pid':c['pid'], 'cmd':json.dumps(['codex','exec']), 'started_at':c['started'],
                           'kind':'command','command_id':c['id'],'goal':c['command'],'task_id':None})
    live = board_metrics.verified_processes(candidates)
    live_task_ids = {p.get('task_id') for p in live}
    for t in tasks:
        t['activity_confirmed'] = t['id'] in live_task_ids
    attempt_agents = {a['id']:a['agent'] for a in rows("SELECT id,agent FROM attempts")}
    activities = []
    for p in live:
        ai,stage = board_metrics.actor(p,attempt_agents)
        task = task_by_id.get(p.get('task_id'),{})
        ident = f"명령 #{p['command_id']}" if p.get('command_id') else f"작업 #{p['task_id']}"
        activities.append({'ai':ai,'stage':stage,'label':f'{ident} {stage}',
                           'task_id':p.get('task_id'),'command_id':p.get('command_id'),
                           'goal':p.get('goal') or task.get('goal',''), 'since':ago(p['started_at'])})
    try:
        configured = json.loads((ROOT/'config.json').read_text(encoding='utf-8')).get('agents',{})
    except (OSError,ValueError):
        configured = {}
    for name, acfg in configured.items():
        if os.getenv(f'ORCH_DISABLE_{name.upper()}'):
            acfg['enabled'] = False
    try:
        cached = json.loads((ROOT/'state/providers.json').read_text(encoding='utf-8'))
    except (OSError,ValueError):
        cached = {}
    ai_cards = board_metrics.ai_state(configured,cached,activities)
    if any(a['enabled'] and not a['fresh'] for a in ai_cards) and time.time()-_PROVIDER_REFRESH_AT>60:
        refresh_providers()
    overall = board_metrics.progress(real)
    targets = [{'target':name, **board_metrics.progress([t for t in real if t['target']==name])}
               for name in sorted({t['target'] for t in real})]

    cur = None
    if running:
        t = running[0]
        at = rows("SELECT * FROM attempts WHERE task_id=? ORDER BY n DESC LIMIT 1", (t["id"],))
        mine = [p for p in live if p["task_id"] == t["id"]]
        cur = {
            "id": t["id"], "goal": t["goal"], "branch": t["branch"] or "-",
            "attempt": at[0]["n"] if at else 0,
            "step": (at[0]["status"] if at else "-"),
            "who": (at[0]["agent"] if at else t["agent"]) or "-",
            "doing": [f"{doing_of(p)} ({ago(p['started_at'])})" for p in mine] or ["대기 — 떠 있는 프로세스 없음"],
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
    counted = all_tasks
    done = [t for t in real if t["status"] == "DONE" and t["verdict"] == "PASS"]

    import shutil
    free = shutil.disk_usage(str(ROOT)).free / (1024 ** 3)
    QUOTA.refresh()

    return {
        "stopped": STOP.exists(),
        "free_gb": free,
        "current": cur,
        "tasks": [dict(t) for t in tasks],
        "blocked": [dict(t) for t in blocked],
        "live": [dict(p) for p in live],
        "usage": [dict(u) for u in usage],
        "quota": QUOTA.snapshot(),
        "done": len(done),
        "total": len(real),
        "nc": len(counted) - len(real),
        "commands": board_section("명령")[:3],
        "command_queue": cq,
        "progress": overall,
        "target_progress": targets,
        "ai_cards": ai_cards,
        "activities": activities,
        "providers_refreshing": _PROVIDER_REFRESH.locked(),
        "providers_error": _PROVIDER_ERROR,
        "waiting": board_section("결정대기")[:4],
        "phases": board_section("단계"),
        "stuck": board_section("막힘")[:3],
        "execlog": board_section("실행")[:8],   # 대장의 진행 보고 — 최신이 위
        "mem": mem_state(),
        "providers": provider_state(),
        "reports": report_list(),
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
      grid-template-areas:"now tasks phases" "stuck tasks prov" "cmd exec usage"}
@media (max-width:900px){body{height:auto;overflow:auto}.grid{flex:none;grid-template-columns:1fr;
      grid-template-areas:"now" "tasks" "exec" "stuck" "prov" "phases" "cmd" "usage"}}
#c-now{grid-area:now}#c-tasks{grid-area:tasks}#c-phases{grid-area:phases}
#c-stuck{grid-area:stuck}#c-cmd{grid-area:cmd}#c-exec{grid-area:exec}#c-usage{grid-area:usage}#c-prov{grid-area:prov}
/* 상세는 한 화면 철칙을 깨지 않도록 덮어서 띄운다 — 목록이 밀려나면 전체가 안 보인다. */
#ov{position:fixed;inset:0;background:rgba(8,10,14,.82);display:none;z-index:9;padding:28px}
#ov.on{display:flex;justify-content:center}
#ovbox{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 18px;
       width:min(880px,100%);max-height:100%;overflow:auto}
#ovbox img{max-width:100%;border:1px solid var(--line);border-radius:8px;margin:6px 0}
#ovbox h1{font-size:18px;margin:4px 0 8px}#ovbox h2.md{margin-top:14px}#ovbox h3{font-size:13px;margin:10px 0 4px}
#ovbox p{margin:6px 0;line-height:1.5}#ovbox table.md td,#ovbox table.md th{padding:3px 8px;border-bottom:1px solid var(--line);font-size:12px}
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

/* 보드 v20260911-3: 계정 잔여 한도와 갱신 상태. 응답은 Cache-Control: no-store. */
:root{--bg:#0b1018;--card:#111a26;--line:#263449;--fg:#e9f1fc;--mute:#94a5bb;--ok:#52d5ab;--run:#73b5ff}
body{font-size:13px;background:radial-gradient(ellipse at 15% 0%,#162a43 0%,var(--bg) 55%)}
header{min-height:64px;flex-wrap:wrap;gap:10px;padding:12px 20px;background:#0d1522}
.brand small{display:block;color:var(--mute);font-size:10px;letter-spacing:1.2px;margin-top:2px}
h1{font-size:17px;letter-spacing:-.4px}
#disk{border:0}#clock{font-size:11px}
.grid{padding:14px 20px 20px;gap:12px;grid-template-columns:minmax(0,1.1fr) minmax(0,1.45fr) minmax(270px,1fr);
 grid-template-rows:170px minmax(190px,1fr) 220px;grid-template-areas:"progress progress prov" "now tasks prov" "cmd exec usage"}
#c-progress{grid-area:progress;background:linear-gradient(120deg,#182c43,#111c2b);border-color:#334d6a}
#c-phases,#c-stuck{display:none}
.card{border-radius:14px;padding:14px 16px;box-shadow:0 7px 24px #0002}
.card h2{letter-spacing:.03em;font-size:12px;margin-bottom:10px}
.card-head{display:flex;align-items:center;justify-content:space-between;gap:8px;flex-shrink:0}
.card-head h2{margin:0}.card-head+div{margin-top:10px}
.progress-content{display:flex;align-items:center;gap:24px;height:100%;overflow:hidden!important}
.ring{width:112px;height:112px;flex-shrink:0;border-radius:50%;background:conic-gradient(var(--ok) calc(var(--pct)*1%),#293b50 0);position:relative;display:grid;place-items:center}
.ring:before{content:"";position:absolute;inset:9px;background:#142237;border-radius:50%}
.ring strong{z-index:1;font-size:28px;letter-spacing:-1.5px;font-variant-numeric:tabular-nums}
.ring small{font-size:14px;letter-spacing:0}
.progress-copy{flex:1;min-width:0}.progress-copy h3{font-size:17px;margin:0 0 3px}.progress-copy p{margin:0;color:var(--mute);font-size:12px}
.counts{display:flex;gap:20px;margin-top:14px}.count strong{display:block;font-size:22px;line-height:1.1;font-variant-numeric:tabular-nums}.count span{font-size:11px;color:var(--mute)}
.target-list{width:29%;max-height:120px;overflow:auto;font-size:11px}.target-row{margin:0 0 8px}.target-label{display:flex;justify-content:space-between;gap:8px;margin-bottom:4px}
.track{height:5px;border-radius:8px;background:#293b50;overflow:hidden}.track span{display:block;height:100%;background:var(--ok);border-radius:8px}
#prov{display:flex;flex-direction:column;gap:8px}.ai-card{border:1px solid var(--line);border-radius:11px;padding:10px 12px;background:#101824}.ai-card.busy{border-color:#447fba;background:#152a40}
.ai-head{display:flex;align-items:center;gap:8px}.ai-avatar{display:grid;place-items:center;width:29px;height:29px;border-radius:9px;background:#23354b;color:#c8e3ff;font-weight:700;font-size:12px}
.ai-name{font-size:14px;font-weight:650;flex:1}.power{font-weight:700;font-size:11px;display:flex;align-items:center;gap:5px}.power:before{content:"";width:6px;height:6px;border-radius:50%;background:currentColor}.power.on{color:var(--ok)}.power.off{color:#9aa4b5}.power.unknown{color:#e2b46d}
.ai-work{font-size:11px;margin-top:6px;display:flex;gap:6px;flex-wrap:wrap}.ai-meta{font-size:10px;color:var(--mute);margin-top:5px}.busy .ai-avatar{background:#275c86}.live-dot{display:inline-block;width:7px;height:7px;border-radius:50%;background:var(--run);box-shadow:0 0 10px #73b5ff80;margin-right:6px}
.activity{border-bottom:1px solid var(--line);padding:8px 0 10px}.activity:first-child{padding-top:0}.activity:last-child{border-bottom:0}.activity h3{font-size:13px;margin:6px 0 4px;font-weight:550}.activity small{color:var(--mute);font-size:11px}
#tasks table td{padding:9px 5px}#tasks tr:last-child td{border-bottom:0}.state-chip{font-size:10px;border:1px solid currentColor;border-radius:6px;padding:2px 5px;white-space:nowrap}
button{background:#1a2b40;border-color:#344a65;font-size:11px}button:hover{background:#223c56;border-color:#6697c4}button:disabled{opacity:.5;cursor:wait}
#cmd-submit{background:#7bc2ff;border-color:#7bc2ff;color:#091623;font-weight:750;min-width:66px}
textarea{width:100%;min-height:58px;max-height:110px;resize:vertical;background:#0c1420;color:var(--fg);border:1px solid #3a5270;border-radius:9px;padding:9px 11px;font:inherit}
textarea:focus{outline:2px solid #73b5ff;outline-offset:1px}form{margin:8px 0;align-items:stretch}
#cmds{font-size:11px;max-height:48px}.command-chips{display:flex;gap:5px;flex-wrap:wrap;margin-top:5px}.command-chips button{padding:2px 6px;font-size:10px}
#cmd-feedback{font-size:11px;min-height:16px;color:var(--ok)}#board-mode{font-size:11px;color:var(--mute)}
#connection-status{font-size:10px}.connection-error{color:#ffbc86!important}
#usage table{font-size:10px}#execlog{font-size:11px}#ovbox{background:#111c2b;border-color:#39516e;overflow-wrap:anywhere}
.quota-row{margin:4px 0 10px}.quota-label{display:flex;justify-content:space-between;align-items:baseline;gap:8px}.quota-label strong{font-size:25px;font-variant-numeric:tabular-nums;line-height:1.2}.quota-row .track{margin:6px 0;height:6px}.quota-reset,.quota-meta{font-size:10px;color:var(--mute)}.quota-summary{font-size:11px;margin-bottom:4px}.quota-actions{display:flex;gap:8px;align-items:center;margin-top:6px}.quota-actions button{padding:2px 6px;font-size:10px}.quota-bucket{border-bottom:1px solid var(--line);padding:10px 0}.quota-bucket h3{margin:0 0 6px!important}.ai-quota{font-size:10px;color:var(--mute);margin-top:5px}
@media(max-width:1050px) and (min-width:901px){.grid{grid-template-columns:minmax(0,1fr) minmax(0,1.2fr) 260px}.target-list{display:none}.counts{gap:14px}.progress-content{gap:16px}.ring{width:96px;height:96px}}
@media(max-width:900px){header{position:relative;padding:12px;gap:8px}#disk,#clock{display:none}header button{padding:5px 8px}.brand{flex:1}.grid{padding:10px;display:grid;grid-template-columns:minmax(0,1fr);grid-template-rows:auto;grid-template-areas:"progress" "cmd" "now" "prov" "tasks" "exec" "usage";gap:10px}.card{min-height:100px}.progress-content{min-height:132px;gap:16px}.ring{width:98px;height:98px}.target-list{display:none}.counts{gap:18px}.ai-card{padding:12px}#c-cmd{min-height:198px}#prov{display:grid;grid-template-columns:repeat(2,minmax(0,1fr))}#ov{padding:12px}.ai-name{font-size:12px}.ai-head{gap:6px}.ai-avatar{width:23px;height:23px;font-size:10px}.power{font-size:10px}}
@media(max-height:790px) and (min-width:901px){.grid{grid-template-rows:145px minmax(120px,1fr) 180px}.card{padding:10px 13px}.ring{width:95px;height:95px}.ai-card{padding:7px 10px}.ai-meta{margin-top:3px}.card h2{margin-bottom:6px}.target-list{max-height:90px}textarea{min-height:45px}.ai-work{margin-top:3px}}

</style>
<header>
  <div class=brand><h1>오케스트레이터 보드</h1><small>명령부터 결과까지, 한곳에서</small></div>
  <span id=state class=pill>…</span>
  <span id=disk class="pill mute">…</span>
  <span style=flex:1></span>
  <button onclick="showPanel('phases','개발 단계')">단계</button>
  <button onclick="showPanel('stuck','막힘')">막힘</button>
  <button onclick="reports()" id=btn-rep>보고</button>
  <button onclick="act('stop')" class=danger>세우기</button>
  <button onclick="act('resume')">풀기</button>
  <span id=clock class=mute></span>
</header>
<div class=grid>
  <div class=card id=c-progress><div id=progress class=progress-content>진행률 확인 중…</div></div>
  <div class=card id=c-now><h2>지금 작업 중인 AI</h2><div id=now>…</div></div>
  <div class=card id=c-tasks><h2>최근 작업</h2><div id=tasks>…</div></div>
  <div class=card id=c-phases><h2>단계</h2><div id=phases>…</div></div>
  <div class=card id=c-stuck><h2>막힘</h2><div id=stuck>…</div></div>
  <div class=card id=c-cmd><div class=card-head><h2>보드에 명령하기</h2><button onclick="commandHistory()">명령 이력</button></div>
    <div id=board-mode>명령을 남기면 담당 AI가 접수하고 결과를 보고합니다.</div>
    <form onsubmit="return send(event)"><textarea id=cmd aria-label="명령" placeholder="무엇을 할까요? 대상과 원하는 결과를 적어주세요."></textarea><button id=cmd-submit>실행 요청</button></form>
    <div id=cmd-feedback role=status></div><div id=cmds>…</div>
  </div>
  <div class=card id=c-exec><h2>실행 · 보고</h2><div id=execlog>…</div></div>
  <div class=card id=c-prov><div class=card-head><h2>AI ON / OFF</h2><button id=refresh-ai onclick="refreshAI()">상태 확인</button></div><div id=prov>…</div><div id=connection-status class=mute></div></div>
  <div class=card id=c-usage><div class=card-head><h2>남은 사용량</h2><button id=quota-details onclick="quotaDetails()">전체 · 사용 기록</button></div><div id=quota>조회 중…</div><div id=usage hidden></div></div>
</div>
<div id=ov onclick="if(event.target.id==='ov')closeOv()"><div id=ovbox>…</div></div>
<script>
const E=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const STATUS_TEXT={DONE:"완료",RUNNING:"작업 중",TESTING:"검증 중",BLOCKED:"막힘",STOPPED:"중단",REVIEW:"리뷰",INTERRUPTED:"중단",BACKLOG:"대기",READY:"대기",FAILED:"실패",BLOCKED_CLOUD_REQUIRED:"AI 대기"};
const SC={DONE:'ok',RUNNING:'run',BLOCKED:'bad',STOPPED:'warn',REVIEW:'warn',INTERRUPTED:'warn'};
const VC={PASS:'ok',FAILED:'bad',UNKNOWN:'warn'};
async function load(){
  const response=await fetch('/api'); if(!response.ok)throw new Error('보드 응답 '+response.status);
  const d=await response.json();
  window.boardData=d;
  document.getElementById('clock').textContent=d.now;
  const st=document.getElementById('state');
  st.textContent=d.stopped?'세워짐':(d.activities.length?'작업 중':(d.current?'실행 확인 대기':'대기'));
  st.className='pill '+(d.stopped?'bad':(d.activities.length?'run':(d.current?'warn':'mute')));
  document.getElementById('disk').textContent=
    '여유 '+d.free_gb.toFixed(1)+'GB · 완료 '+d.done+'/'+d.total+' · 시험판 '+d.nc+'건 숨김';

  const pr=d.progress, percent=pr.percent;
  document.getElementById('progress').innerHTML=`
    <div class=ring style="--pct:${percent??0}" role=progressbar aria-label="등록 작업 완료율" aria-valuemin=0 aria-valuemax=100 ${percent===null?'':`aria-valuenow="${percent}"`}><strong>${percent===null?'—':percent}<small>${percent===null?'':'%'}</small></strong></div>
    <div class=progress-copy><h3>등록 작업 완료율</h3><p>검증 완료 ${pr.done} / 전체 ${pr.total}개</p>
      <div class=counts><div class=count><strong class=ok>${pr.done}</strong><span>완료</span></div><div class=count><strong class=run>${pr.running}</strong><span>진행 기록</span></div><div class=count><strong>${pr.waiting}</strong><span>대기</span></div><div class=count><strong class=warn>${pr.blocked}</strong><span>막힘·중단</span></div></div></div>
    <div class=target-list>${d.target_progress.map(t=>`<div class=target-row><div class=target-label><span>${E(t.target)}</span><span>${t.percent??'—'}% · ${t.done}/${t.total}</span></div><div class=track><span style="width:${t.percent??0}%"></span></div></div>`).join('')}</div>`;
  document.getElementById('now').innerHTML=d.activities.length ? d.activities.map(a=>`
    <div class=activity><span class="pill run"><span class=live-dot></span>${E((d.ai_cards.find(c=>c.name===a.ai)||{}).display_name||'검증 엔진')} · ${E(a.stage)}</span>
    <h3>${E(a.label)} · ${E(a.goal)}</h3><small>${E(a.since)} 시작 · 실행 프로세스 확인</small></div>`).join('') :
    (d.current?`<div class=activity><h3>작업 #${d.current.id} · ${E(d.current.goal)}</h3><p class=warn>실행 프로세스 확인 대기</p><small>개발 담당 ${E(d.current.who)} · 기록과 실제 상태를 대조 중</small></div>`:'<div class=mute>현재 실행 중인 AI 작업이 없습니다.</div>');

  const stuck=[...d.blocked.map(t=>`<li><span class="${SC[t.status]||'mute'}">#${t.id}</span> ${E(t.reason||t.goal).slice(0,60)}</li>`),
               ...d.stuck.map(s=>`<li class=mute>${E(s).slice(0,70)}</li>`)];
  document.getElementById('stuck').innerHTML = stuck.length?`<ul>${stuck.slice(0,6).join('')}</ul>`:'<div class=mute>없음</div>';

  document.getElementById('tasks').innerHTML='<table>'+d.tasks.map(t=>
    `<tr class=click onclick="detail(${t.id})"><td class=n>#${t.id}</td><td class="n ${SC[t.status]||'mute'}"><span class=state-chip>${E(t.status==='RUNNING'&&!t.activity_confirmed?'실행 확인':(STATUS_TEXT[t.status]||t.status))}</span></td>
     <td class="n ${VC[t.verdict]||'mute'}">${E(t.verdict||'-')}</td><td class="n mute">${E((d.ai_cards.find(a=>a.name===t.agent)||{}).display_name||t.agent||'-')}</td><td class="n mute">${t.attempts}회</td>
     <td class=g title="${E(t.goal)}">${E(t.goal)}</td></tr>`).join('')+'</table>';

  document.getElementById('phases').innerHTML='<ul>'+d.phases.map(p=>{
    const done=p.includes('완료'), wait=p.includes('대기');
    return `<li class="${done?'ok':(wait?'mute':'run')}">${E(p)}</li>`;}).join('')+'</ul>';

  document.getElementById('execlog').innerHTML=d.execlog.length?
    '<ul>'+d.execlog.map(x=>{const done=x.startsWith('[x]'), hold=x.startsWith('[-]');
      return `<li class="${done?'ok':(hold?'mute':'run')}">${E(x.replace(/^\\[.\\] /,''))}</li>`;}).join('')+'</ul>':'<div class=mute>없음</div>';
  const cq=d.command_queue;
  if(!document.getElementById('ov').classList.contains('on'))window.commandItems=cq.items; window.commandLabels=cq.labels;
  const pending=cq.counts.QUEUED||0;
  document.getElementById('cmds').innerHTML=`<span class="${cq.worker.online?'ok':'warn'}">● 명령 연결 ${cq.worker.online?'ON':'OFF'}</span> · ${E(cq.worker.detail||'실행기 확인 필요')} · 대기 ${pending}건`;
  const cp={ON:'on',OFF:'off',UNKNOWN:'unknown'};
  const quota=d.quota, shared=quota.buckets.find(b=>b.id==='codex');
  const quotaLine=a=>['codex','astra'].includes(a.name)?(shared?.windows.length?shared.windows.map(w=>`${w.period} ${w.remaining_percent===null?'미확인':w.remaining_percent+'% 남음'}`).join(' · ')+' · 계정 공통':'계정 잔여 한도 미확인'):(a.name==='ollama'?'로컬 실행 · 구독 한도 해당 없음':'잔여 한도 미확인');
  document.getElementById('prov').innerHTML=d.ai_cards.map(a=>`<div class="ai-card ${a.jobs.length?'busy':''}">
    <div class=ai-head><span class=ai-avatar>${E(a.display_name.slice(0,2))}</span><span class=ai-name>${E(a.display_name)}</span><span class="power ${cp[a.power]}">${a.power==='UNKNOWN'?'확인 필요':a.power}</span></div>
    <div class=ai-work><span class="${a.jobs.length?'run':'mute'}">${E(a.activity)}</span>${a.jobs.slice(0,2).map(j=>`<span>${E(j)}</span>`).join('')}</div>
    <div class=ai-meta>${E(a.model||'모델 자동')} · ${E(a.reason)}${a.age_sec===null?'':` · ${a.age_sec<60?'방금':Math.floor(a.age_sec/60)+'분 전'} 확인`}</div><div class=ai-quota>${E(quotaLine(a))}</div></div>`).join('')||'<div class=warn>AI 설정을 읽지 못했습니다.</div>';
  document.getElementById('refresh-ai').disabled=d.providers_refreshing;
  document.getElementById('refresh-ai').textContent=d.providers_refreshing?'확인 중…':'상태 확인';
  document.getElementById('connection-status').textContent=d.providers_error||'ON: 사용 가능 · OFF: 사용 불가 · 작업 중: 파란색';
  document.getElementById('quota').innerHTML=`<div class=quota-summary>Codex · Astra 계정 공통</div>${shared?.windows.length?shared.windows.map(w=>quotaWindow(shared,w)).join(''):'<p class=mute>잔여 한도 '+(quota.refreshing?'조회 중…':'미확인')+'</p>'}<div class=quota-meta>${E(quota.error||'계정 전체 기준 · 1분마다 자동 갱신')}</div><div class=quota-actions><span class=quota-meta>${quota.checked_at?'확인 '+quotaTime(quota.checked_at):'조회 기록 없음'}</span><button id=refresh-quota onclick="refreshQuota()" ${quota.refreshing?'disabled':''}>${quota.refreshing?'조회 중…':'새로고침'}</button></div>`;

  REPORTS=d.reports||[]; document.getElementById('btn-rep').textContent='보고'+(REPORTS.length?` ${REPORTS.length}`:'');
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
function quotaTime(ts){return new Date(ts*1000).toLocaleString('ko-KR',{timeZone:'Asia/Seoul',month:'numeric',day:'numeric',hour:'2-digit',minute:'2-digit',hour12:false})+' KST';}
function quotaWindow(bucket,w){
  const n=w.remaining_percent, cls=n===null?'mute':n<=10?'bad':n<=25?'warn':'ok';
  return `<div class=quota-row><div class=quota-label><span>${E(w.period)}</span><strong class=${cls}>${n===null?'미확인':n+'%'}${n===null?'':' <small style="font-size:11px">남음</small>'}</strong></div>${n===null?'':`<div class=track role=progressbar aria-label="${E(bucket.name+' '+w.period+' 남은 사용량')}" aria-valuemin=0 aria-valuemax=100 aria-valuenow="${n}"><span style="width:${n}%;background:var(--${cls})"></span></div>`}<div class=quota-reset>${n===null&&w.previous_remaining_percent!==null?'마지막 확인 '+w.previous_remaining_percent+'% · 현재 값 확인 필요<br>':''}${w.resets_at?'초기화 '+quotaTime(w.resets_at):'초기화 시각 미확인'}</div></div>`;
}
function quotaDetails(){
  const q=window.boardData?.quota;if(!q)return;
  document.getElementById('ov').classList.add('on');
  document.getElementById('ovbox').innerHTML=`<button onclick=closeOv()>닫기</button><h1>계정별 남은 사용량</h1><p class=mute>Codex 계정 조회 · ${q.checked_at?quotaTime(q.checked_at):'조회 기록 없음'} · ${q.fresh?'최근 조회':'현재 값 확인 필요'}</p>`+q.buckets.map(b=>`<section class=quota-bucket><h3>${E(b.name)}</h3>${b.windows.map(w=>quotaWindow(b,w)).join('')||'<p class=mute>기간별 잔여 한도 미제공</p>'}${b.credit_balance===null?'':`<p class=mute>추가 크레딧 잔액 ${E(b.credit_balance)}</p>`}</section>`).join('')+`<p class=mute>초기화 크레딧 ${q.reset_credits===null?'미확인':q.reset_credits+'개'}</p><p>Codex와 Astra는 이 계정 한도를 공통으로 사용합니다. Claude·Grok의 잔여 한도는 미확인입니다. Ollama는 로컬 실행으로 구독 한도가 적용되지 않습니다.</p><h2>보드 작업 사용 기록 · 메모리</h2><p class=mute>아래 호출·토큰 기록은 보드 작업 기록이며 계정 잔여 한도와 별도입니다.</p>`+document.getElementById('usage').innerHTML;
}
async function refreshQuota(){
  const b=document.getElementById('refresh-quota');b.disabled=true;
  try{const r=await fetch('/quota/refresh',{method:'POST'});if(!r.ok)throw new Error('사용량 조회 요청 실패');await load();}
  catch(e){b.textContent='조회 실패 · 재시도';b.disabled=false;}
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
let REPORTS=[];
async function reports(){
  const b=document.getElementById('ovbox'); document.getElementById('ov').classList.add('on');
  b.innerHTML=`<div style="display:flex;gap:10px;align-items:center"><div class=big>완료 보고</div>
    <span style=flex:1></span><button onclick=closeOv()>닫기</button></div>`
    +(REPORTS.length?'<table>'+REPORTS.map(r=>`<tr class=click onclick="report('${E(r.name)}')">
       <td>${E(r.title)}</td><td class="n mute">${E(r.when)}</td></tr>`).join('')+'</table>'
      :'<div class=mute>아직 보고가 없다 — docs/reports/*.md</div>');
}
// 아주 작은 마크다운: 제목·글머리·번호·굵게·코드·표·그림. 그 이상은 원문 그대로 보인다.
function md(src){
  const inl=t=>E(t).replace(/`([^`]+)`/g,'<code>$1</code>').replace(/\\*\\*([^*]+)\\*\\*/g,'<b>$1</b>')
     .replace(/!\\[([^\\]]*)\\]\\(([^)]+)\\)/g,(m,a,u)=>`<img alt="${a}" src="${u.startsWith('http')?u:'/reports/'+u.split('/').pop()}">`);
  const out=[]; const L=src.split('\\n'); let i=0, list=null;
  const flush=()=>{ if(list){ out.push(`</${list}>`); list=null; } };
  while(i<L.length){ const l=L[i];
    if(l.startsWith('```')){ flush(); const buf=[]; i++; while(i<L.length&&!L[i].startsWith('```')) buf.push(L[i++]); i++;
      out.push(`<pre>${E(buf.join('\\n'))}</pre>`); continue; }
    if(l.startsWith('|')){ flush(); const rows=[]; while(i<L.length&&L[i].startsWith('|')) rows.push(L[i++]);
      out.push('<table class=md>'+rows.filter(r=>!/^\\|\\s*-/.test(r)).map((r,k)=>'<tr>'+r.split('|').slice(1,-1)
        .map(c=>`<${k?'td':'th'}>${inl(c.trim())}</${k?'td':'th'}>`).join('')+'</tr>').join('')+'</table>'); continue; }
    let m;
    if((m=l.match(/^(#{1,3}) (.*)/))){ flush(); const h=m[1].length; out.push(`<h${h} class=md>${inl(m[2])}</h${h}>`); }
    else if((m=l.match(/^\\s*[-*] (.*)/))){ if(list!=='ul'){ flush(); out.push('<ul>'); list='ul'; } out.push(`<li>${inl(m[1])}</li>`); }
    else if((m=l.match(/^\\s*\\d+\\. (.*)/))){ if(list!=='ol'){ flush(); out.push('<ol>'); list='ol'; } out.push(`<li>${inl(m[1])}</li>`); }
    else if(l.trim()==='') flush();
    else { flush(); out.push(`<p>${inl(l)}</p>`); }
    i++; }
  flush(); return out.join('\\n');
}
async function report(name){
  const b=document.getElementById('ovbox'); document.getElementById('ov').classList.add('on');
  b.innerHTML='<div class=mute>읽는 중…</div>';
  const t=await (await fetch('/reports/'+encodeURIComponent(name))).text();
  b.innerHTML=`<div style="display:flex;gap:10px;align-items:center"><span class=mute>${E(name)}</span>
    <span style=flex:1></span><button onclick="reports()">목록</button><button onclick=closeOv()>닫기</button></div>`+md(t);
  b.scrollTop=0;
}
// 주소로 바로 열기: ?task=338 · ?report=파일.md (스크린샷·공유용)
window.addEventListener('load',()=>{ const q=new URLSearchParams(location.search);
  if(q.get('task')) detail(+q.get('task')); else if(q.get('report')) setTimeout(()=>report(q.get('report')),300); });
document.addEventListener('keydown',e=>{ if(e.key==='Escape')closeOv(); });
function showPanel(id,title){
  document.getElementById('ov').classList.add('on');
  document.getElementById('ovbox').innerHTML=`<button onclick=closeOv()>닫기</button><h1>${E(title)}</h1>`+document.getElementById(id).innerHTML;
}
async function refreshAI(){
  const b=document.getElementById('refresh-ai'); b.disabled=true;
  try{const r=await fetch('/providers/refresh',{method:'POST'});if(!r.ok)throw new Error('상태 확인 요청 실패');await load();}
  catch(e){document.getElementById('connection-status').textContent=e.message;b.disabled=false;}
}
async function commandHistory(before){
  const r=await fetch('/api/commands'+(before?'?before='+before:''));
  if(!r.ok)throw new Error('명령 이력을 읽지 못했습니다');
  const data=await r.json(); window.commandItems=data.items;
  const labels=window.commandLabels||{};
  document.getElementById('ov').classList.add('on');
  document.getElementById('ovbox').innerHTML=`<button onclick=closeOv()>닫기</button> <button onclick="commandHistory()">최신 명령</button><h1>명령 이력</h1>`+
    (data.items.length?'<table>'+data.items.map(c=>`<tr class=click onclick="commandDetail(${c.id})"><td class=n>#${c.id}</td><td class=n>${E(labels[c.status]||c.status)}</td><td>${E(c.command.slice(0,120))}</td></tr>`).join('')+'</table>':'<p>이전 명령이 없습니다.</p>')+
    (data.items.length===30?`<button onclick="commandHistory(${data.items.at(-1).id})">이전 명령 더 보기</button>`:'');
}
function commandDetail(id){
  const c=(window.commandItems||[]).find(x=>x.id===id); if(!c)return;
  let report={};try{report=JSON.parse(c.report)||{};}catch(e){}
  document.getElementById('ov').classList.add('on');
  document.getElementById('ovbox').innerHTML=`<button onclick=closeOv()>닫기</button> <button onclick="commandHistory()">명령 이력</button> <button onclick="followCommand(${c.id})">후속 명령 작성</button><h1>명령 #${c.id}</h1><pre>${E(c.command)}</pre><p>${E(c.summary||'접수됨')}</p><h2>담당자 검증 보고</h2><ul>${(Array.isArray(report.evidence)?report.evidence:[]).map(x=>`<li>${E(x)}</li>`).join('')}</ul>`;
}
function followCommand(id){
  const c=(window.commandItems||[]).find(x=>x.id===id);if(!c)return;
  closeOv();const input=document.getElementById('cmd');
  input.value=`이전 명령 #${id}: ${c.command}\n결과: ${c.summary}\n추가 요청: `;input.focus();input.scrollIntoView({block:'center',behavior:'smooth'});
}
async function act(a){ await fetch('/'+a,{method:'POST'}); load(); }
let pendingCommand=null;
async function send(e){ e.preventDefault(); const i=document.getElementById('cmd');
  if(!i.value.trim())return false;
  const b=document.getElementById('cmd-submit'), f=document.getElementById('cmd-feedback');
  if(!pendingCommand||pendingCommand.command!==i.value.trim())pendingCommand={command:i.value.trim(),request_id:crypto.randomUUID()};
  b.disabled=true;
  try{
    const r=await fetch('/command',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(pendingCommand)});
    const d=await r.json();if(!r.ok)throw new Error(d.error||'접수 실패');
    f.textContent=`명령 #${d.id} 접수됨`;i.value='';pendingCommand=null;await load();
  }catch(err){f.textContent='접수 확인 실패: '+err.message+' · 재시도해도 중복 접수하지 않습니다';}
  finally{b.disabled=false;}return false;
}
async function refreshBoard(){
  try{await load();}
  catch(e){const st=document.getElementById('state');st.textContent='보드 연결 끊김';st.className='pill warn';
    document.getElementById('connection-status').textContent='연결 확인 필요 · 표시된 정보는 마지막 수신 상태입니다';}
}
refreshBoard();setInterval(refreshBoard,3000);
</script></html>"""


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body, ctype="text/html; charset=utf-8"):
        data = body.encode("utf-8") if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = urlparse(self.path).path
        if path == "/":
            self._send(200, PAGE)
        elif path == "/api/commands":
            try:
                before = parse_qs(urlparse(self.path).query).get('before',[None])[0]
                items = Queue().recent(int(before) if before else None)
                self._send(200,json.dumps({'items':items},ensure_ascii=False),"application/json; charset=utf-8")
            except ValueError:
                self._send(400,'{"error":"잘못된 명령 번호"}',"application/json; charset=utf-8")
        elif path.startswith("/api/task/"):
            try:
                tid = int(path.rsplit("/", 1)[-1])
            except ValueError:
                self._send(404, "없음"); return
            self._send(200, json.dumps(task_detail(tid), ensure_ascii=False, default=str),
                       "application/json; charset=utf-8")
        elif path == "/api":
            self._send(200, json.dumps(gather(), ensure_ascii=False), "application/json; charset=utf-8")
        elif path.startswith("/reports/"):
            name = unquote(path[len("/reports/"):])
            f = (REPORTS / name)
            if ("/" in name or ".." in name or not f.is_file()
                    or f.suffix not in (".md", ".png", ".jpg", ".txt")):
                self._send(404, "없음"); return
            ctype = {".md": "text/markdown; charset=utf-8", ".txt": "text/plain; charset=utf-8",
                     ".png": "image/png", ".jpg": "image/jpeg"}[f.suffix]
            self._send(200, f.read_bytes(), ctype)
        else:
            self._send(404, "없음")

    def do_POST(self):
        path = urlparse(self.path).path
        if path in ("/providers/refresh", "/quota/refresh"):
            origin = self.headers.get("Origin")
            if origin and origin != "http://" + self.headers.get("Host", ""):
                self._send(403,"다른 사이트의 요청"); return
            if path == "/quota/refresh":
                QUOTA.refresh(force=True)
            else:
                refresh_providers()
            self._send(202,"ok")
        elif path == "/stop":
            STOP.parent.mkdir(parents=True, exist_ok=True)
            STOP.write_text(f"{time.time()}\n보드에서 세움\n", encoding="utf-8")
            self._send(200, "ok")
        elif path == "/resume":
            if STOP.exists():
                STOP.unlink()
            self._send(200, "ok")
        elif path == "/command":
            try:
                origin = self.headers.get("Origin")
                if origin and origin != "http://" + self.headers.get("Host", ""):
                    raise ValueError("다른 사이트에서 명령을 접수할 수 없습니다")
                n = int(self.headers.get("Content-Length", 0))
                if not 0 < n <= 100000:
                    raise ValueError("명령 크기가 올바르지 않습니다")
                text = self.rfile.read(n).decode("utf-8")
                payload = json.loads(text) if "application/json" in self.headers.get("Content-Type", "") else {"command": text, "request_id": self.headers.get("Idempotency-Key") or str(uuid.uuid4())}
                if not isinstance(payload, dict) or not isinstance(payload.get("command"), str) or not isinstance(payload.get("request_id"), str):
                    raise ValueError("command와 request_id 문자열이 필요합니다")
                ident = Queue().submit(payload["command"], payload["request_id"])
                self._send(202, json.dumps({"id": ident, "status": "ACCEPTED"}), "application/json; charset=utf-8")
            except (ValueError, UnicodeError) as e:
                self._send(400, json.dumps({"error": str(e)}, ensure_ascii=False), "application/json; charset=utf-8")
        else:
            self._send(404, "없음")

    def log_message(self, *a):
        pass


def main():
    srv = ThreadingHTTPServer((HOST, PORT), Handler)
    refresh_providers()
    print(f"오케스트레이터 보드: http://{HOST}:{PORT}  (Ctrl+C 종료)")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료")


if __name__ == "__main__":
    main()
