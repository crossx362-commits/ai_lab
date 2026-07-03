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
    # 계좌 평가액 스냅샷(포지션 모니터가 시세 반영해 기록) — 전체 금액 대비 수익
    acct = _j(CACHE / "somi_account.json", {})
    if not acct:  # 스냅샷 없으면 원장 현금 기준 폴백
        start = 10000000
        cash = paper.get("cash", start)
        acct = {"start": start, "value": cash, "ret": round((cash / start - 1) * 100, 2),
                "cash": cash, "pos_val": 0, "ts": "-"}
    d["account"] = acct

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

    # 오늘 발굴/제안 — 발굴 사유(reasons)·뉴스 판단까지 노출
    prop = _j(CACHE / "somi_proposals.json", {})
    d["proposals"] = {"ts": prop.get("ts", "-"),
                      "items": [{"name": p.get("name"), "score": p.get("score"),
                                 "verdict": p.get("verdict"), "change": p.get("change"),
                                 "reasons": (p.get("reasons") or [])[:3],
                                 "risks": (p.get("risks") or [])[:1],
                                 "news_reason": (p.get("news_reason") or "")[:90]}
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
<h1>🧭 AI Lab 시스템 현황</h1> <a href="/heatmap" style="font-size:13px;margin-left:8px">🔥 시장 열지도 →</a>
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
 // 모의 계좌 — 전체 금액 대비 평가액·수익 강조
 const pos=Object.entries(d.paper.positions||{});
 const ac=d.account||{};
 const acCls=ac.ret>=0?'ok':'bad';
 g.push(card('모의 계좌',
  `<div class="kv"><div><div class="l">계좌 평가액</div><div class="big ${acCls}">${Number(ac.value||0).toLocaleString()}원</div></div>`+
  `<div><div class="l">전체 수익률</div><div class="big ${acCls}">${ac.ret>0?'+':''}${ac.ret}%</div></div></div>`+
  `<div class="dim" style="font-size:12px;margin-top:4px">시작 ${Number(ac.start||10000000).toLocaleString()} → 현금 ${Number(ac.cash||0).toLocaleString()} + 주식 ${Number(ac.pos_val||0).toLocaleString()} · 보유 ${pos.length}종목</div>`+
  (pos.length?`<table style="margin-top:6px"><tr><th>종목</th><th class="num">수량</th><th class="num">평단</th></tr>${pos.map(([s,p])=>`<tr><td>${esc(s)}</td><td class="num">${p.qty}</td><td class="num">${Number(p.avg).toLocaleString()}</td></tr>`).join('')}</table>`:'')));
 // 성과
 const pf=d.perf.n?`<div class="kv"><div><div class="l">청산</div><div class="big">${d.perf.n}건</div></div><div><div class="l">승률</div><div class="big">${d.perf.winrate}%</div></div><div><div class="l">건당평균</div><div class="big ${d.perf.avg>=0?'ok':'bad'}">${d.perf.avg>0?'+':''}${d.perf.avg}%</div></div></div><div class="dim" style="font-size:11px;margin-top:2px">거래 품질 지표 — 실제 벌이는 위 계좌 평가액</div>`:'<span class="dim">청산 거래가 아직 없습니다 — 첫 체결부터 집계</span>';
 const rt=d.recent_trades.length?`<table><tr><th>청산</th><th>종목</th><th class="num">순수익</th><th>사유</th></tr>${d.recent_trades.map(t=>`<tr><td class="dim">${esc(t.ts_close)}</td><td>${esc(t.name)}</td><td class="num ${t.ret_pct>=0?'ok':'bad'}">${t.ret_pct>0?'+':''}${esc(t.ret_pct)}%</td><td class="dim">${esc(t.reason)}</td></tr>`).join('')}</table>`:'';
 g.push(card('모의 성과',pf+rt));
 // 발굴/제안 — 종목 행 아래 발굴 사유·리스크·뉴스 판단 표시
 const pr=d.proposals.items.length?`<table><tr><th>종목</th><th class="num">점수</th><th>판정</th><th class="num">등락</th></tr>${d.proposals.items.map(p=>{
  const why=[(p.reasons||[]).map(r=>'✚ '+esc(r)).join(' · '),
             (p.risks||[]).filter(r=>r&&r!=='뚜렷한 위험 신호 없음'&&r!=='뚜렷한 위험 없음').map(r=>'⚠ '+esc(r)).join(' · '),
             p.news_reason?('📰 '+esc(p.news_reason)):''].filter(Boolean).join('<br>');
  return `<tr><td>${esc(p.name)}</td><td class="num">${esc(p.score)}</td><td class="${p.verdict==='buy'?'ok':'dim'}">${esc(p.verdict)}</td><td class="num">${esc(p.change)}</td></tr>`+
         (why?`<tr><td colspan="4" class="dim" style="font-size:12px;padding-left:14px;border-bottom:1px solid var(--line)">${why}</td></tr>`:'');
 }).join('')}</table>`:'<span class="dim">후보 없음</span>';
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


HEATMAP_HTML = """<!DOCTYPE html>
<html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>시장 열지도 — 국장·미장</title>
<link rel="icon" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'><rect x='2' y='14' width='8' height='16' rx='1.5' fill='%23089981'/><rect x='12' y='6' width='8' height='24' rx='1.5' fill='%23f23645'/><rect x='22' y='2' width='8' height='28' rx='1.5' fill='%23089981'/></svg>">
<style>
:root{--bg:#0b0e14;--panel:#131722;--line:#1f2637;--tx:#e8edf5;--dim:#8b93a7;--up:#089981;--dn:#f23645;--acc:#4f7dff}
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{background:var(--bg);color:var(--tx);font:13px/1.45 -apple-system,'Segoe UI','Malgun Gothic',sans-serif;overflow:hidden;display:flex;flex-direction:column}
svg{vertical-align:-2px}
.hdr{display:flex;align-items:center;gap:14px;padding:10px 16px;background:var(--panel);border-bottom:1px solid var(--line);flex-wrap:wrap}
.logo{display:flex;align-items:center;gap:8px;font-size:16px;font-weight:800;letter-spacing:-.2px}
.logo a{color:var(--dim);font-size:12px;font-weight:500;text-decoration:none;margin-left:4px}
.logo a:hover{color:var(--acc)}
.seg{display:flex;background:#0d1119;border:1px solid var(--line);border-radius:9px;overflow:hidden}
.seg button{border:0;background:transparent;color:var(--dim);padding:7px 16px;font:600 13px inherit;cursor:pointer;font-family:inherit}
.seg button.on{background:var(--acc);color:#fff}
.seg.sm button{padding:5px 12px;font-size:12px}
.search{position:relative}
.search input{background:#0d1119;border:1px solid var(--line);border-radius:9px;color:var(--tx);padding:7px 12px 7px 32px;width:190px;font-family:inherit;font-size:13px;outline:none}
.search input:focus{border-color:var(--acc)}
.search svg{position:absolute;left:10px;top:8px;opacity:.5}
.meta{margin-left:auto;color:var(--dim);font-size:12px;text-align:right;line-height:1.35}
.sub{display:flex;align-items:center;gap:10px;padding:8px 16px;border-bottom:1px solid var(--line);flex-wrap:wrap;background:rgba(19,23,34,.55)}
.chip{display:inline-flex;align-items:baseline;gap:6px;background:#0d1119;border:1px solid var(--line);border-radius:8px;padding:4px 9px;margin:1px 0}
.chip .l{font-size:11px;color:var(--dim);font-weight:600}
.chip .v{font-size:12.5px;font-weight:700;font-variant-numeric:tabular-nums}
.chip .c{font-size:12px;font-weight:700;font-variant-numeric:tabular-nums}
.up{color:var(--up)}.dn{color:var(--dn)}.fl{color:var(--dim)}
.breadth{display:flex;align-items:center;gap:8px;margin-left:auto}
.bbar{width:170px;height:7px;border-radius:4px;overflow:hidden;display:flex;background:#2a2e39}
.bbar i{display:block;height:100%}
.bl{font-size:11px;color:var(--dim);font-variant-numeric:tabular-nums}
#wrap{flex:1;position:relative;margin:8px;min-height:0;overflow:hidden}
#map{position:absolute;inset:0}
.sec{position:absolute;overflow:hidden;border-radius:4px}
.sec .sh{position:absolute;left:0;top:0;right:0;height:18px;padding:2px 7px 0;font-size:10.5px;font-weight:700;color:#aab3c5;letter-spacing:.4px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;background:rgba(11,14,20,.35)}
.sec .sh b{font-weight:700}
.tile{position:absolute;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;overflow:hidden;cursor:pointer;border-radius:3px;box-shadow:inset 0 0 0 1px rgba(11,14,20,.85);transition:filter .1s}
.tile:hover{filter:brightness(1.22);z-index:5}
.tile.dimmed{opacity:.12}
.tile .tn{font-weight:800;line-height:1.1;text-shadow:0 1px 2px rgba(0,0,0,.45);white-space:nowrap;max-width:96%;overflow:hidden;text-overflow:ellipsis}
.tile .tc{font-weight:700;opacity:.96;line-height:1.15;text-shadow:0 1px 2px rgba(0,0,0,.4);font-variant-numeric:tabular-nums}
#tip{position:fixed;z-index:50;pointer-events:none;background:#171c29;border:1px solid #2b3350;border-radius:10px;padding:10px 13px;font-size:12.5px;display:none;box-shadow:0 8px 28px rgba(0,0,0,.55);min-width:190px}
#tip .t1{font-weight:800;font-size:13.5px;margin-bottom:2px}
#tip .t2{font-size:16px;font-weight:800;margin:2px 0 6px;font-variant-numeric:tabular-nums}
#tip .kv{display:flex;justify-content:space-between;gap:18px;color:var(--dim);margin-top:2px}
#tip .kv b{color:var(--tx);font-weight:600;font-variant-numeric:tabular-nums}
.foot{display:flex;align-items:center;gap:12px;padding:4px 14px;border-top:1px solid var(--line);background:var(--panel);color:var(--dim);font-size:11px;flex-wrap:wrap}
.legend{display:flex;align-items:center;gap:2px;margin-left:auto}
.legend i{display:block;width:34px;height:14px;border-radius:2px;font-style:normal;font-size:9.5px;text-align:center;line-height:14px;color:#fff;font-weight:700}
.mkl{position:absolute;font-size:11px;font-weight:800;color:#aab3c5;letter-spacing:.3px}
@media (max-width:760px){.search input{width:120px}.meta{display:none}.breadth{margin-left:0}}
</style></head><body>
<div class="hdr">
 <div class="logo">
  <svg width="20" height="20" viewBox="0 0 32 32"><rect x="2" y="14" width="8" height="16" rx="1.5" fill="#089981"/><rect x="12" y="6" width="8" height="24" rx="1.5" fill="#f23645"/><rect x="22" y="2" width="8" height="28" rx="1.5" fill="#089981"/></svg>
  시장 열지도 <a href="/">← 시스템 현황</a>
 </div>
 <div class="seg" id="mkt">
  <button id="t_all" class="on" onclick="setMkt('all')">🌏 전체</button>
  <button id="t_kr" onclick="setMkt('kr')">🇰🇷 국장</button>
  <button id="t_us" onclick="setMkt('us')">🇺🇸 미장</button>
 </div>
 <div class="seg sm" id="size">
  <button id="s_mcap" class="on" onclick="setSize('mcap')">시가총액</button>
  <button id="s_value" onclick="setSize('value')">거래대금</button>
 </div>
 <div class="search">
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#8b93a7" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg>
  <input id="q" placeholder="종목 검색…" oninput="setQuery(this.value)">
 </div>
 <div class="meta" id="meta">불러오는 중…</div>
</div>
<div class="sub">
 <span id="chips"></span>
 <div class="breadth"><span class="bl" id="bl_up">▲ 0</span><div class="bbar" id="bbar"></div><span class="bl" id="bl_dn">▼ 0</span></div>
</div>
<div id="wrap"><div id="map"></div></div>
<div id="tip"></div>
<div class="foot">
 <span>크기 = <span id="hint_size">시가총액</span> · 색 = 등락률 · 클릭 → 차트</span>
 <div class="legend" id="legend"></div>
</div>
<script>
let DATA={kr:[],us:[],indices:{kr:[],us:[]}},TS='-',cur='all',sizeMode='mcap',query='';
// ── 색: TradingView 팔레트 보간 (중립 → ±3%에서 포화) ──
const NC=[42,46,57],GC=[6,153,129],RC=[242,54,69];
function color(c){const t=Math.max(-1,Math.min(1,(c||0)/3));const M=t>=0?GC:RC,a=Math.abs(t);
 return 'rgb('+NC.map((n,i)=>Math.round(n+(M[i]-n)*a)).join(',')+')'}
// ── 스퀘리파이드 트리맵 ──
function squarify(items,x,y,w,h){
 items=items.filter(i=>i.v>0).sort((a,b)=>b.v-a.v);
 const out=[],total=items.reduce((s,i)=>s+i.v,0);
 if(!total||w<=2||h<=2)return out;
 const sc=w*h/total;let L=items.map(i=>({d:i.d,v:i.v*sc}));
 let cx=x,cy=y,cw=w,ch=h,row=[];
 const worst=(r,len)=>{const s=r.reduce((a,b)=>a+b.v,0);let mx=0,mn=1e18;
  for(const q of r){mx=Math.max(mx,q.v);mn=Math.min(mn,q.v)}
  return Math.max(len*len*mx/(s*s),(s*s)/(len*len*mn))};
 const flush=()=>{const s=row.reduce((a,b)=>a+b.v,0);if(!s){row=[];return}
  if(cw>=ch){const rw=s/ch;let off=0;
   for(const q of row){const hh=q.v/rw;out.push({d:q.d,x:cx,y:cy+off,w:rw,h:hh});off+=hh}cx+=rw;cw-=rw}
  else{const rh=s/cw;let off=0;
   for(const q of row){const ww=q.v/rh;out.push({d:q.d,x:cx+off,y:cy,w:ww,h:rh});off+=ww}cy+=rh;ch-=rh}
  row=[]};
 while(L.length){const len=Math.min(cw,ch);
  if(!row.length||worst(row.concat([L[0]]),len)<=worst(row,len))row.push(L.shift());
  else flush()}
 flush();return out}
// ── 포맷터 ──
const P=(m,p)=>m==='kr'?Number(p||0).toLocaleString()+'원':'$'+Number(p||0).toLocaleString();
const CAP=(m,v)=>!v?'—':(m==='kr'?(v>=1e12?(v/1e12).toFixed(1)+'조':Math.round(v/1e8).toLocaleString()+'억')
 :(v>=1e12?'$'+(v/1e12).toFixed(2)+'T':'$'+Math.round(v/1e9)+'B'));
const VAL=(m,v)=>!v?'—':(m==='kr'?Math.round(v/1e8).toLocaleString()+'억':'$'+(v>=1e9?(v/1e9).toFixed(1)+'B':Math.round(v/1e6)+'M'));
const sizeOf=x=>(sizeMode==='mcap'?(x.mcap||x.value):x.value)||1;
const esc=s=>String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;');
// ── 렌더 ──
function marketRows(){return cur==='all'?[...(DATA.kr||[]),...(DATA.us||[])]:(DATA[cur]||[])}
function renderMarket(mkt,rows,ox,oy,W,H,html){
 if(!rows.length||W<40||H<40)return;
 const by={};rows.forEach(x=>{(by[x.sector]=by[x.sector]||[]).push(x)});
 const secs=Object.entries(by).map(([name,items])=>{
  const tot=items.reduce((s,x)=>s+sizeOf(x),0);
  const wch=items.reduce((s,x)=>s+(x.change||0)*sizeOf(x),0)/(tot||1);
  return {name,items,tot,wch}});
 const secRects=squarify(secs.map(s=>({d:s,v:s.tot})),ox,oy,W,H);
 const G=2;
 for(const r of secRects){
  const s=r.d,cls=s.wch>0.02?'up':s.wch<-0.02?'dn':'fl';
  // 작은 섹터는 헤더 생략 — 18px 헤더가 타일 공간을 다 먹어 '짤림'처럼 보이던 문제
  const HD=(r.h<48||r.w<64)?0:16;
  const inner=squarify(s.items.map(x=>({d:x,v:sizeOf(x)})),0,0,Math.max(1,r.w-G*2),Math.max(1,r.h-HD-G*2));
  const tiles=inner.map(t=>{
   const x=t.d,dim=query&&!(x.name.toLowerCase().includes(query)||String(x.code).toLowerCase().includes(query));
   const fs=Math.max(9,Math.min(21,Math.sqrt(t.w*t.h)/5.2));
   const showN=t.w>34&&t.h>20,showC=t.w>36&&t.h>34;
   const lbl=(showN?'<div class="tn" style="font-size:'+(fs*0.72).toFixed(1)+'px">'+esc(x.name)+'</div>':'')+
             (showC?'<div class="tc" style="font-size:'+fs.toFixed(1)+'px">'+(x.change>0?'+':'')+x.change+'%</div>':'');
   return '<div class="tile'+(dim?' dimmed':'')+'" style="left:'+t.x.toFixed(1)+'px;top:'+t.y.toFixed(1)+
    'px;width:'+Math.max(1,t.w-1).toFixed(1)+'px;height:'+Math.max(1,t.h-1).toFixed(1)+'px;background:'+color(x.change)+
    '" data-c="'+esc(x.code)+'" data-m="'+mkt+'">'+lbl+'</div>'}).join('');
  html.push('<div class="sec" style="left:'+r.x.toFixed(1)+'px;top:'+r.y.toFixed(1)+'px;width:'+Math.max(1,r.w-1).toFixed(1)+
   'px;height:'+Math.max(1,r.h-1).toFixed(1)+'px">'+
   (HD?'<div class="sh">'+esc(s.name)+(r.w>=120?' <b class="'+cls+'">'+(s.wch>0?'+':'')+s.wch.toFixed(2)+'%</b>':'')+'</div>':'')+
   '<div style="position:absolute;left:'+G+'px;top:'+(HD||G)+'px;right:'+G+'px;bottom:'+G+'px">'+tiles+'</div></div>')}
}
function render(){
 renderSub();   // 칩/지표를 먼저 그려 상단 바 높이를 확정한 뒤 지도 영역을 측정(넘침 방지)
 const el=document.getElementById('map'),wrap=document.getElementById('wrap');
 const W=wrap.clientWidth,H=wrap.clientHeight,html=[];
 el.innerHTML='';
 if(cur==='all'){
  // 국장·미장 동시(한눈에) — 가로 여유 있으면 좌우, 아니면 상하 분할
  const LB=15,gap=8,vert=W>=H*1.1;
  const panes=vert?[['kr',0,0,(W-gap)/2,H],['us',(W-gap)/2+gap,0,(W-gap)/2,H]]
                  :[['kr',0,0,W,(H-gap)/2],['us',0,(H-gap)/2+gap,W,(H-gap)/2]];
  for(const [m,x,y,w,h] of panes){
   html.push('<div class="mkl" style="left:'+(x+2)+'px;top:'+y+'px">'+(m==='kr'?'🇰🇷 국장':'🇺🇸 미장')+'</div>');
   renderMarket(m,DATA[m]||[],x,y+LB,w,h-LB,html)}
 }else{
  renderMarket(cur,DATA[cur]||[],0,0,W,H,html)}
 el.innerHTML=html.join('')||'<div style="color:#8b93a7;padding:30px">데이터 수집 대기 중… (수집기 첫 실행 후 표시)</div>'}
function renderSub(){
 const rows=marketRows();
 const idx=cur==='all'?[...((DATA.indices||{}).kr||[]),...((DATA.indices||{}).us||[])]:((DATA.indices||{})[cur]||[]);
 document.getElementById('chips').innerHTML=idx.map(i=>{
  const cls=i.change>0?'up':i.change<0?'dn':'fl';
  return '<span class="chip"><span class="l">'+esc(i.name)+'</span><span class="v">'+Number(i.price||0).toLocaleString()+
   '</span><span class="c '+cls+'">'+(i.change>0?'+':'')+i.change+'%</span></span>'}).join(' ');
 const up=rows.filter(x=>x.change>0).length,dn=rows.filter(x=>x.change<0).length,fl=rows.length-up-dn;
 document.getElementById('bl_up').textContent='▲ '+up;
 document.getElementById('bl_dn').textContent='▼ '+dn;
 const T=rows.length||1;
 document.getElementById('bbar').innerHTML=
  '<i style="width:'+(up/T*100)+'%;background:var(--up)"></i>'+
  '<i style="width:'+(fl/T*100)+'%;background:#3a4055"></i>'+
  '<i style="width:'+(dn/T*100)+'%;background:var(--dn)"></i>';
 document.getElementById('meta').innerHTML='갱신 '+esc(TS)+'<br>1분마다 자동 새로고침';}
// ── 상호작용 ──
function setMkt(m){cur=m;for(const k of ['all','kr','us'])
 document.getElementById('t_'+k).className=m===k?'on':'';render()}
function setSize(s){sizeMode=s;document.getElementById('s_mcap').className=s==='mcap'?'on':'';
 document.getElementById('s_value').className=s==='value'?'on':'';
 document.getElementById('hint_size').textContent=s==='mcap'?'시가총액':'거래대금';render()}
function setQuery(v){query=v.trim().toLowerCase();render()}
const tip=document.getElementById('tip');
document.getElementById('map').addEventListener('mousemove',e=>{
 const t=e.target.closest('.tile');
 if(!t){tip.style.display='none';return}
 const m=t.dataset.m||cur;
 const x=(DATA[m]||[]).find(r=>String(r.code)===t.dataset.c);if(!x)return;
 const cls=x.change>0?'up':x.change<0?'dn':'fl';
 tip.innerHTML='<div class="t1">'+esc(x.name)+' <span style="color:#8b93a7;font-weight:500">'+esc(x.code)+'</span></div>'+
  '<div class="t2 '+cls+'">'+(x.change>0?'+':'')+x.change+'%</div>'+
  '<div class="kv"><span>현재가</span><b>'+P(m,x.price)+'</b></div>'+
  '<div class="kv"><span>시가총액</span><b>'+CAP(m,x.mcap)+'</b></div>'+
  '<div class="kv"><span>거래대금</span><b>'+VAL(m,x.value)+'</b></div>'+
  '<div class="kv"><span>섹터</span><b>'+esc(x.sector)+'</b></div>';
 tip.style.display='block';
 const tw=tip.offsetWidth,th=tip.offsetHeight;
 tip.style.left=Math.min(e.clientX+16,innerWidth-tw-10)+'px';
 tip.style.top=Math.min(e.clientY+16,innerHeight-th-10)+'px'});
document.getElementById('map').addEventListener('mouseleave',()=>tip.style.display='none');
document.getElementById('map').addEventListener('click',e=>{
 const t=e.target.closest('.tile');if(!t)return;
 const c=t.dataset.c,m=t.dataset.m||cur;
 window.open(m==='kr'?'https://finance.naver.com/item/main.naver?code='+c
  :'https://www.tradingview.com/symbols/'+encodeURIComponent(c.replace('^',''))+'/','_blank')});
// 범례
document.getElementById('legend').innerHTML=[-3,-2,-1,0,1,2,3].map(v=>
 '<i style="background:'+color(v)+'">'+(v>0?'+':'')+v+'%</i>').join('');
// ── 로드/리사이즈 ──
async function load(){
 try{const d=await (await fetch('/api/heatmap')).json();
  DATA={kr:d.kr||[],us:d.us||[],indices:d.indices||{kr:[],us:[]}};TS=d.ts||'-';render()}
 catch(e){document.getElementById('meta').textContent='수집 대기 중…'}}
let rz;addEventListener('resize',()=>{clearTimeout(rz);rz=setTimeout(render,150)});
document.addEventListener('visibilitychange',()=>{if(!document.hidden)load()});
load();setInterval(load,60000);
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802
        if self.path.startswith("/api/status"):
            body = json.dumps(collect(), ensure_ascii=False).encode("utf-8")
            ctype = "application/json; charset=utf-8"
        elif self.path.startswith("/api/heatmap"):
            body = json.dumps(_j(CACHE / "somi_heatmap.json", {"kr": [], "us": [], "ts": "-"}),
                              ensure_ascii=False).encode("utf-8")
            ctype = "application/json; charset=utf-8"
        elif self.path.startswith("/heatmap"):
            body = HEATMAP_HTML.encode("utf-8")
            ctype = "text/html; charset=utf-8"
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


def _heatmap_refresher() -> None:
    """열지도 데이터 백그라운드 갱신 — KIS/야후 조회는 여기서만(요청 스레드 비차단)."""
    import importlib
    interval = int(os.getenv("HEATMAP_INTERVAL", "180"))
    sys.path.insert(0, str(ROOT / "projects" / "ai-team" / "skills" / "소미_분석가" / "tools"))
    while True:
        try:
            hc = importlib.import_module("heatmap_collector")
            hc.build()
        except Exception as e:
            print(f"[대시보드] 열지도 수집 오류: {e}")
        time.sleep(interval)


def main() -> None:
    import threading
    port = PORT
    if "--port" in sys.argv:
        port = int(sys.argv[sys.argv.index("--port") + 1])
    threading.Thread(target=_heatmap_refresher, daemon=True).start()  # 열지도 자동 갱신
    srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"[대시보드] http://localhost:{port} 서빙 시작 (열지도: /heatmap)")
    srv.serve_forever()


if __name__ == "__main__":
    main()
