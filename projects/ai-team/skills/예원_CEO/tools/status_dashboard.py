#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""status_dashboard.py — 예원: 시스템 현황 대시보드 (로컬 웹페이지).

에이전트 상태·하네스 점검·모의 계좌/성과·튜닝·조사 신선도·학습 인사이트를
한 페이지에서 30초 자동 갱신으로 보여준다. stdlib만 사용(의존성 없음), 읽기 전용.

실행:
  python status_dashboard.py            # http://localhost:8890
  python status_dashboard.py --port N
"""
from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.env import load_env            # noqa: E402
from _shared.notify import agent_status, _AGENT_LABELS  # noqa: E402

load_env(str(ROOT))

CACHE = ROOT / "output" / "cache"
PORT = 8890


def _j(path: Path, default):
    try:
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default
    except Exception:
        return default


def _age(path: Path) -> str:
    if not path.exists():
        return "없음"
    m = (time.time() - path.stat().st_mtime) / 60
    if m < 60:
        return f"{int(m)}분 전"
    if m < 60 * 24:
        return f"{m/60:.1f}시간 전"
    return f"{m/1440:.1f}일 전"


def collect() -> dict:
    d: dict = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

    # 에이전트 상태
    try:
        d["agents"] = [{"key": k, "label": _AGENT_LABELS.get(k, k), "state": v}
                       for k, v in agent_status().items()]
    except Exception as e:
        d["agents_error"] = str(e)

    # 하네스 최근 결과
    h = _j(ROOT / "reports" / "status" / "harness_latest.json", {})
    d["harness"] = {"ts": h.get("timestamp", "-"), "overall": h.get("overall", "-"),
                    "checks": [{"name": c.get("name"), "status": c.get("status"),
                                "message": (c.get("message") or "")[:160]}
                               for c in h.get("checks", [])]}

    # 거래 모드 + 모의 계좌
    d["mode"] = _j(CACHE / "trade_mode.json", {}).get("mode", "?")
    paper = _j(CACHE / "somi_paper.json", {})
    d["paper"] = {"cash": paper.get("cash"), "positions": paper.get("positions", {})}

    # 청산 성과 요약 + 최근 거래
    trades = [t for t in _j(CACHE / "somi_closed_trades.json", [])
              if t.get("source") != "backtest_seed"]
    if trades:
        rets = [t.get("ret_pct", 0) for t in trades]
        wins = [r for r in rets if r > 0]
        d["perf"] = {"n": len(trades), "winrate": round(len(wins) / len(rets) * 100, 1),
                     "avg": round(sum(rets) / len(rets), 2), "sum": round(sum(rets), 1)}
    else:
        d["perf"] = {"n": 0}
    d["recent_trades"] = [{k: t.get(k) for k in ("ts_close", "name", "ret_pct", "reason", "score")}
                          for t in trades[-10:]][::-1]

    # 튜닝 파라미터 + 최근 이력
    tun = _j(CACHE / "somi_tuning.json", {})
    d["tuning"] = {"params": tun.get("params", {}),
                   "recommend_min_score": tun.get("recommend_min_score"),
                   "history": [{"ts": x.get("ts"), "notes": x.get("notes", [])}
                               for x in tun.get("history", [])[-3:]][::-1]}

    # 오늘 발굴/제안
    prop = _j(CACHE / "somi_proposals.json", {})
    d["proposals"] = {"ts": prop.get("ts", "-"),
                      "items": [{"name": p.get("name"), "score": p.get("score"),
                                 "verdict": p.get("verdict"), "change": p.get("change")}
                                for p in (prop.get("items") or [])[:8]]}

    # 조사 산출물 신선도
    rs = ROOT / "output" / "research"
    d["research"] = [{"file": f, "age": _age(rs / f)}
                     for f in ("region_us.json", "region_asia.json", "region_eu.json",
                               "market_brief.json", "issue_impact.json")]

    # 학습 인사이트
    ins = _j(ROOT / "output" / "growth" / "insights.json", {})
    d["insights"] = {"ts": ins.get("ts", "-"), "stats": ins.get("stats", {}),
                     "list": ins.get("insights", [])[:5]}

    # 배포 상태 (워치독 git 자동배포)
    head = _j(CACHE / "watchdog_git_head.json", {})
    d["deploy"] = {"head": (head.get("head") or "")[:8], "ts": head.get("ts", "-")}
    return d


HTML = """<!DOCTYPE html>
<html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>AI Lab 현황</title>
<style>
:root{--bg:#0f1420;--card:#1a2233;--line:#2a3550;--tx:#dce4f2;--dim:#8494b3;--ok:#3ddc84;--warn:#ffb347;--bad:#ff5c72;--acc:#5ea0ff}
*{box-sizing:border-box;margin:0}
body{background:var(--bg);color:var(--tx);font:14px/1.5 -apple-system,'Malgun Gothic',sans-serif;padding:16px}
h1{font-size:18px;margin-bottom:2px}
#ts{color:var(--dim);font-size:12px;margin-bottom:14px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:12px}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px}
.card h2{font-size:13px;color:var(--acc);margin-bottom:8px;letter-spacing:.5px}
table{width:100%;border-collapse:collapse;font-size:13px}
td,th{padding:3px 6px;text-align:left;border-bottom:1px solid var(--line)}
th{color:var(--dim);font-weight:600;font-size:11px}
.ok{color:var(--ok)}.warn{color:var(--warn)}.bad{color:var(--bad)}.dim{color:var(--dim)}
.pill{display:inline-block;padding:1px 8px;border-radius:10px;font-size:11px;background:#243050}
.num{text-align:right;font-variant-numeric:tabular-nums}
.big{font-size:20px;font-weight:700}
.kv{display:flex;gap:14px;flex-wrap:wrap}.kv div{min-width:70px}
.kv .l{font-size:11px;color:var(--dim)}
</style></head><body>
<h1>🧭 AI Lab 시스템 현황</h1>
<div id="ts">불러오는 중…</div>
<div class="grid" id="grid"></div>
<script>
const S={up:s=>/^\\d/.test(s)||s==='scheduled'||s.startsWith('sched'),
  cls:s=>/^\\d/.test(s)?'ok':(s==='scheduled'||s.startsWith('sched'))?'ok':'bad',
  txt:s=>/^\\d/.test(s)?'실행중 '+s:(s==='scheduled')?'예약 대기':s.startsWith('sched')?s.replace('sched:','')+'개 잡 예약':'중지'};
function card(t,b){return `<div class="card"><h2>${t}</h2>${b}</div>`}
function esc(x){return String(x??'-').replace(/</g,'&lt;')}
async function load(){
 let d;try{d=await (await fetch('/api/status')).json()}catch(e){document.getElementById('ts').textContent='서버 응답 없음 — 재시도 중';return}
 window._last=d.ts;
 document.getElementById('ts').innerHTML='갱신 '+d.ts+' · 다음 갱신 <span id="cd">15</span>초 · 모드 '+(d.mode==='paper'?'🧪 모의':'🔴 실거래')+' · 배포 '+esc(d.deploy.head);
 const g=[];
 // 에이전트
 g.push(card('에이전트',(d.agents||[]).map(a=>`<span class="pill" style="margin:2px"><span class="${S.cls(a.state)}">●</span> ${esc(a.label)} <span class="dim">${S.txt(a.state)}</span></span>`).join(' ')));
 // 하네스
 const hc=d.harness.checks.map(c=>`<tr><td class="${c.status==='OK'?'ok':c.status==='WARN'?'warn':'bad'}">${c.status}</td><td>${esc(c.name)}</td><td class="dim">${esc(c.message)}</td></tr>`).join('');
 g.push(card('하네스 점검 <span class="dim">('+esc(d.harness.ts)+')</span>',`<table>${hc}</table>`));
 // 모의 계좌
 const pos=Object.entries(d.paper.positions||{});
 g.push(card('모의 계좌',`<div class="kv"><div><div class="l">예수금</div><div class="big">${d.paper.cash!=null?Number(d.paper.cash).toLocaleString()+'원':'원장 미생성'}</div></div><div><div class="l">보유</div><div class="big">${pos.length}종목</div></div></div>`+
  (pos.length?`<table><tr><th>종목</th><th class="num">수량</th><th class="num">평단</th></tr>${pos.map(([s,p])=>`<tr><td>${esc(s)}</td><td class="num">${p.qty}</td><td class="num">${Number(p.avg).toLocaleString()}</td></tr>`).join('')}</table>`:'')));
 // 성과
 const pf=d.perf.n?`<div class="kv"><div><div class="l">청산</div><div class="big">${d.perf.n}건</div></div><div><div class="l">승률</div><div class="big">${d.perf.winrate}%</div></div><div><div class="l">평균</div><div class="big ${d.perf.avg>=0?'ok':'bad'}">${d.perf.avg>0?'+':''}${d.perf.avg}%</div></div><div><div class="l">누적</div><div class="big ${d.perf.sum>=0?'ok':'bad'}">${d.perf.sum>0?'+':''}${d.perf.sum}%p</div></div></div>`:'<span class="dim">청산 거래가 아직 없습니다 — 첫 체결부터 집계</span>';
 const rt=d.recent_trades.length?`<table><tr><th>청산</th><th>종목</th><th class="num">순수익</th><th>사유</th></tr>${d.recent_trades.map(t=>`<tr><td class="dim">${esc(t.ts_close)}</td><td>${esc(t.name)}</td><td class="num ${t.ret_pct>=0?'ok':'bad'}">${t.ret_pct>0?'+':''}${esc(t.ret_pct)}%</td><td class="dim">${esc(t.reason)}</td></tr>`).join('')}</table>`:'';
 g.push(card('모의 성과',pf+rt));
 // 발굴/제안
 const pr=d.proposals.items.length?`<table><tr><th>종목</th><th class="num">점수</th><th>판정</th><th class="num">등락</th></tr>${d.proposals.items.map(p=>`<tr><td>${esc(p.name)}</td><td class="num">${esc(p.score)}</td><td class="${p.verdict==='buy'?'ok':'dim'}">${esc(p.verdict)}</td><td class="num">${esc(p.change)}</td></tr>`).join('')}</table>`:'<span class="dim">후보 없음</span>';
 g.push(card('발굴 후보 <span class="dim">('+esc(d.proposals.ts)+')</span>',pr));
 // 튜닝
 const tp=d.tuning.params;
 g.push(card('튜닝 파라미터',`<div class="kv">${Object.entries(tp).map(([k,v])=>`<div><div class="l">${esc(k)}</div><div class="big">${esc(v)}</div></div>`).join('')}${d.tuning.recommend_min_score?`<div><div class="l">한별 추천점수</div><div class="big">${esc(d.tuning.recommend_min_score)}</div></div>`:''}</div>`+
  (d.tuning.history.length?`<div style="margin-top:8px">${d.tuning.history.map(h=>`<div class="dim" style="font-size:12px">· ${esc(h.ts)} — ${esc((h.notes||[]).join(', '))}</div>`).join('')}</div>`:'')));
 // 조사 신선도
 g.push(card('조사 산출물 신선도',`<table>${d.research.map(r=>`<tr><td>${esc(r.file)}</td><td class="num ${r.age.includes('일')?'warn':'dim'}">${esc(r.age)}</td></tr>`).join('')}</table>`));
 // 인사이트
 g.push(card('학습 인사이트 <span class="dim">('+esc(d.insights.ts)+')</span>',`<div class="dim" style="font-size:12px">표본 ${esc(d.insights.stats.n??0)}건</div>`+(d.insights.list||[]).map(i=>`<div style="margin-top:4px">· ${esc(i)}</div>`).join('')));
 document.getElementById('grid').innerHTML=g.join('');
}
load();setInterval(load,15000);
let cd=15;setInterval(()=>{cd=cd>1?cd-1:15;const el=document.getElementById('cd');if(el)el.textContent=cd},1000);
document.addEventListener('visibilitychange',()=>{if(!document.hidden)load()}); // 탭 복귀 시 즉시 갱신
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802
        if self.path.startswith("/api/status"):
            body = json.dumps(collect(), ensure_ascii=False).encode("utf-8")
            ctype = "application/json; charset=utf-8"
        elif self.path == "/" or self.path.startswith("/index"):
            body = HTML.encode("utf-8")
            ctype = "text/html; charset=utf-8"
        else:
            self.send_response(404)
            self.end_headers()
            return
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt, *args):  # 요청 로그 소음 억제
        pass


def main() -> None:
    port = PORT
    if "--port" in sys.argv:
        port = int(sys.argv[sys.argv.index("--port") + 1])
    srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"[대시보드] http://localhost:{port} 서빙 시작")
    srv.serve_forever()


if __name__ == "__main__":
    main()
