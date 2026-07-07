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
:root{--bg:#faf9f5;--card:#efe9de;--line:#e6dfd8;--tx:#141413;--dim:#6c6a64;--ok:#5db872;--warn:#d4a017;--bad:#c64545;--acc:#cc785c}
*{box-sizing:border-box;margin:0}
body{background:var(--bg);color:var(--tx);font:14px/1.55 Inter,-apple-system,'Malgun Gothic',sans-serif;padding:16px}
h1{font-family:Georgia,'Tiempos Headline',serif;font-weight:400;letter-spacing:-.3px;font-size:20px;margin-bottom:2px}
#ts{color:var(--dim);font-size:12px;margin-bottom:14px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:12px}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px}
.card h2{font-size:13px;color:var(--acc);margin-bottom:8px;letter-spacing:.5px;font-weight:600}
table{width:100%;border-collapse:collapse;font-size:13px}
td,th{padding:3px 6px;text-align:left;border-bottom:1px solid var(--line)}
th{color:var(--dim);font-weight:600;font-size:11px}
.ok{color:var(--ok)}.warn{color:var(--warn)}.bad{color:var(--bad)}.dim{color:var(--dim)}
.pill{display:inline-block;padding:1px 8px;border-radius:10px;font-size:11px;background:#e8e0d2}
.num{text-align:right;font-variant-numeric:tabular-nums}
.big{font-size:20px;font-weight:700}
.kv{display:flex;gap:14px;flex-wrap:wrap}.kv div{min-width:70px}
.kv .l{font-size:11px;color:var(--dim)}
</style></head><body>
<h1>🧭 AI Lab 시스템 현황</h1> <a href="/heatmap" target="_blank" style="font-size:13px;margin-left:8px">🔥 열지도 크게 보기 ↗</a>
<div id="ts">불러오는 중…</div>
<iframe src="/heatmap?embed=1" title="시장 열지도" style="width:100%;height:480px;border:1px solid var(--line);border-radius:10px;margin-bottom:12px;background:var(--card)"></iframe>
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
 // 에이전트 — 라벨 '<에이전트> (<세부>)'를 에이전트명으로 그룹핑(소미 8줄 → 소미 1줄+세부 pill)
 const _grp={};
 (d.agents||[]).forEach(a=>{const m=String(a.label).match(/^(.+?)\\s*\\((.+)\\)$/);const ag=m?m[1]:a.label;const sub=m?m[2]:'';(_grp[ag]=_grp[ag]||[]).push({sub,state:a.state})});
 g.push(card('에이전트',Object.entries(_grp).map(([ag,ds])=>`<div style="margin:5px 0"><b style="color:var(--acc)">${esc(ag)}</b> ${ds.map(x=>`<span class="pill" style="margin:2px"><span class="${S.cls(x.state)}">●</span> ${esc(x.sub||ag)} <span class="dim">${S.txt(x.state)}</span></span>`).join(' ')}</div>`).join('')));
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
<title>시장 열지도 — 국장·미장·크립토</title>
<link rel="icon" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'><rect x='2' y='14' width='8' height='16' rx='1.5' fill='%23089981'/><rect x='12' y='6' width='8' height='24' rx='1.5' fill='%23f23645'/><rect x='22' y='2' width='8' height='28' rx='1.5' fill='%23089981'/></svg>">
<style>
:root{--bg:#181715;--panel:#252320;--line:rgba(250,249,245,.1);--tx:#faf9f5;--dim:#a09d96;--up:#f23645;--dn:#3182f6;--acc:#cc785c}
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{background:var(--bg);color:var(--tx);font:13px/1.45 Inter,-apple-system,'Segoe UI','Malgun Gothic',sans-serif;overflow:hidden;display:flex;flex-direction:column}
svg{vertical-align:-2px}
.hdr{display:flex;align-items:center;gap:14px;padding:10px 16px;background:var(--panel);border-bottom:1px solid var(--line);flex-wrap:wrap}
.logo{display:flex;align-items:center;gap:8px;font-size:16px;font-weight:700;letter-spacing:-.2px}
.logo a{color:var(--dim);font-size:12px;font-weight:500;text-decoration:none;margin-left:4px}
.logo a:hover{color:var(--acc)}
.seg{display:flex;background:#1f1e1b;border:1px solid var(--line);border-radius:9px;overflow:hidden}
.seg button{border:0;background:transparent;color:var(--dim);padding:7px 16px;font:600 13px inherit;cursor:pointer;font-family:inherit}
.seg button.on{background:var(--acc);color:#fff}
.seg.sm button{padding:5px 12px;font-size:12px}
.search{position:relative}
.search input{background:#1f1e1b;border:1px solid var(--line);border-radius:9px;color:var(--tx);padding:7px 12px 7px 32px;width:190px;font-family:inherit;font-size:13px;outline:none}
.search input:focus{border-color:var(--acc)}
.search svg{position:absolute;left:10px;top:8px;opacity:.5}
.meta{margin-left:auto;color:var(--dim);font-size:12px;text-align:right;line-height:1.35}
.sub{display:flex;align-items:center;gap:10px;padding:8px 16px;border-bottom:1px solid var(--line);flex-wrap:wrap;background:rgba(37,35,32,.55)}
.chip{display:inline-flex;align-items:baseline;gap:6px;background:#1f1e1b;border:1px solid var(--line);border-radius:8px;padding:4px 9px;margin:1px 0}
.chip .l{font-size:11px;color:var(--dim);font-weight:600}
.chip .v{font-size:12.5px;font-weight:700;font-variant-numeric:tabular-nums}
.chip .c{font-size:12px;font-weight:700;font-variant-numeric:tabular-nums}
.up{color:var(--up)}.dn{color:var(--dn)}.fl{color:var(--dim)}
.breadth{display:flex;align-items:center;gap:8px;margin-left:auto}
.bbar{width:170px;height:7px;border-radius:4px;overflow:hidden;display:flex;background:#2a2822}
.bbar i{display:block;height:100%}
.bl{font-size:11px;color:var(--dim);font-variant-numeric:tabular-nums}
#wrap{flex:1;position:relative;margin:8px;min-height:0;overflow:hidden}
#map{position:absolute;inset:0}
.sec{position:absolute;overflow:hidden;border-radius:4px}
.sec .sh{position:absolute;left:0;top:0;right:0;height:18px;padding:2px 7px 0;font-size:10.5px;font-weight:700;color:#a09d96;letter-spacing:.4px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;background:rgba(24,23,21,.4)}
.sec .sh b{font-weight:700}
.tile{position:absolute;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;overflow:hidden;cursor:pointer;border-radius:3px;box-shadow:inset 0 0 0 1px rgba(24,23,21,.85);transition:filter .1s}
.tile:hover{filter:brightness(1.22);z-index:5}
.tile.dimmed{opacity:.12}
.tile .tn{font-weight:800;line-height:1.1;text-shadow:0 1px 2px rgba(0,0,0,.45);white-space:nowrap;max-width:96%;overflow:hidden;text-overflow:ellipsis}
.tile .tc{font-weight:700;opacity:.96;line-height:1.15;text-shadow:0 1px 2px rgba(0,0,0,.4);font-variant-numeric:tabular-nums}
#tip{position:fixed;z-index:50;pointer-events:none;background:#252320;border:1px solid rgba(250,249,245,.12);border-radius:10px;padding:10px 13px;font-size:12.5px;display:none;box-shadow:0 8px 28px rgba(0,0,0,.55);min-width:190px}
#tip .t1{font-weight:800;font-size:13.5px;margin-bottom:2px}
#tip .t2{font-size:16px;font-weight:800;margin:2px 0 6px;font-variant-numeric:tabular-nums}
#tip .kv{display:flex;justify-content:space-between;gap:18px;color:var(--dim);margin-top:2px}
#tip .kv b{color:var(--tx);font-weight:600;font-variant-numeric:tabular-nums}
.foot{display:flex;align-items:center;gap:12px;padding:4px 14px;border-top:1px solid var(--line);background:var(--panel);color:var(--dim);font-size:11px;flex-wrap:wrap}
.legend{display:flex;align-items:center;gap:2px;margin-left:auto}
.legend i{display:block;width:34px;height:14px;border-radius:2px;font-style:normal;font-size:9.5px;text-align:center;line-height:14px;color:#fff;font-weight:700}
.mkl{position:absolute;font-size:11px;font-weight:800;color:#a09d96;letter-spacing:.3px}
@media (max-width:760px){.search input{width:120px}.meta{display:none}.breadth{margin-left:0}}
</style></head><body>
<div class="hdr">
 <div class="logo">
  <svg width="20" height="20" viewBox="0 0 32 32"><rect x="2" y="14" width="8" height="16" rx="1.5" fill="#089981"/><rect x="12" y="6" width="8" height="24" rx="1.5" fill="#f23645"/><rect x="22" y="2" width="8" height="28" rx="1.5" fill="#089981"/></svg>
  시장 열지도 <a href="/" id="backlink">← 시스템 현황</a><script>if(location.search.indexOf('embed')>=0){var _b=document.getElementById('backlink');if(_b)_b.remove()}</script>
 </div>
 <div class="seg" id="mkt">
  <button id="t_all" class="on" onclick="setMkt('all')">🌏 전체</button>
  <button id="t_kr" onclick="setMkt('kr')">🇰🇷 국장</button>
  <button id="t_us" onclick="setMkt('us')">🇺🇸 미장</button>
  <button id="t_crypto" onclick="setMkt('crypto')">🪙 크립토</button>
 </div>
 <div class="seg sm" id="size">
  <button id="s_mcap" class="on" onclick="setSize('mcap')">시가총액</button>
  <button id="s_value" onclick="setSize('value')">거래대금</button>
 </div>
 <div class="search">
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#a09d96" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/></svg>
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
 <span>정렬 = <span id="hint_size">시가총액</span> · 색 = 등락률 · 클릭 → 차트</span>
 <div class="legend" id="legend"></div>
</div>
<script>
let DATA={kr:[],us:[],crypto:[],indices:{kr:[],us:[],crypto:[]}},TS='-',cur='all',sizeMode='mcap',query='';
// ── 색: 국내 관행(상승 빨강·하락 파랑) 팔레트 보간 (중립 → ±3%에서 포화) ──
const NC=[42,40,36],UPC=[242,54,69],DNC=[49,130,246];
function color(c){const t=Math.max(-1,Math.min(1,(c||0)/3));const M=t>=0?UPC:DNC,a=Math.abs(t);
 return 'rgb('+NC.map((n,i)=>Math.round(n+(M[i]-n)*a)).join(',')+')'}
// ── 포맷터 ──
// 통화: 미장만 USD, 국장·크립토(업비트 KRW)는 원화
const P=(m,p)=>m==='us'?'$'+Number(p||0).toLocaleString():Number(p||0).toLocaleString()+'원';
const CAP=(m,v)=>!v?'—':(m!=='us'?(v>=1e12?(v/1e12).toFixed(1)+'조':Math.round(v/1e8).toLocaleString()+'억')
 :(v>=1e12?'$'+(v/1e12).toFixed(2)+'T':'$'+Math.round(v/1e9)+'B'));
const VAL=(m,v)=>!v?'—':(m!=='us'?Math.round(v/1e8).toLocaleString()+'억':'$'+(v>=1e9?(v/1e9).toFixed(1)+'B':Math.round(v/1e6)+'M'));
const sizeOf=x=>(sizeMode==='mcap'?(x.mcap||x.value):x.value)||1;
const esc=s=>String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;');
// ── 렌더 ──
function marketRows(){return cur==='all'?[...(DATA.kr||[]),...(DATA.us||[]),...(DATA.crypto||[])]:(DATA[cur]||[])}
function renderMarket(mkt,rows,ox,oy,W,H,html){
 if(!rows.length||W<40||H<40)return;
 const G=2,HDR=15;
 // 섹터 그룹(입력 순서 유지) — 내부는 정렬키(시총/거래대금) 내림차순
 const by={};rows.forEach(x=>{(by[x.sector]=by[x.sector]||[]).push(x)});
 const secs=Object.entries(by).map(([name,items])=>({name,items:items.slice().sort((a,b)=>sizeOf(b)-sizeOf(a))}));
 // 모든 타일을 같은 크기 정사각형으로 — 변 s를 이분탐색해 세로(H)에 딱 맞는 최대값 채택
 const need=s=>{const cols=Math.max(1,Math.floor((W+G)/(s+G)));
  let h=0;for(const sc of secs)h+=HDR+Math.ceil(sc.items.length/cols)*(s+G)+G;return{cols,h}};
 let lo=8,hi=Math.min(W,H),s=lo;
 for(let i=0;i<26;i++){const mid=(lo+hi)/2;if(need(mid).h<=H){s=mid;lo=mid}else hi=mid}
 const cols=need(s).cols,side=Math.max(1,s-1),fs=Math.max(8,Math.min(15,s/4.4));
 let y=oy;
 for(const sc of secs){
  const avg=sc.items.reduce((a,x)=>a+(x.change||0),0)/sc.items.length;
  const cls=avg>0.02?'up':avg<-0.02?'dn':'fl';
  if(W>=80)html.push('<div style="position:absolute;left:'+ox+'px;top:'+y.toFixed(1)+'px;width:'+W+'px;height:'+HDR+
   'px;font-size:10.5px;font-weight:700;color:#a09d96;letter-spacing:.3px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">'+
   esc(sc.name)+' <b class="'+cls+'">'+(avg>0?'+':'')+avg.toFixed(2)+'%</b></div>');
  y+=HDR;
  sc.items.forEach((x,i)=>{
   const tx=ox+(i%cols)*(s+G),ty=y+Math.floor(i/cols)*(s+G);
   const dim=query&&!(x.name.toLowerCase().includes(query)||String(x.code).toLowerCase().includes(query));
   const showN=s>30,showC=s>22;
   const lbl=(showN?'<div class="tn" style="font-size:'+(fs*0.8).toFixed(1)+'px">'+esc(x.name)+'</div>':'')+
             (showC?'<div class="tc" style="font-size:'+fs.toFixed(1)+'px">'+(x.change>0?'+':'')+x.change+'%</div>':'');
   html.push('<div class="tile'+(dim?' dimmed':'')+'" style="left:'+tx.toFixed(1)+'px;top:'+ty.toFixed(1)+
    'px;width:'+side.toFixed(1)+'px;height:'+side.toFixed(1)+'px;background:'+color(x.change)+
    '" data-c="'+esc(x.code)+'" data-m="'+mkt+'">'+lbl+'</div>')});
  y+=Math.ceil(sc.items.length/cols)*(s+G)+G}
}
function render(){
 renderSub();   // 칩/지표를 먼저 그려 상단 바 높이를 확정한 뒤 지도 영역을 측정(넘침 방지)
 const el=document.getElementById('map'),wrap=document.getElementById('wrap');
 const W=wrap.clientWidth,H=wrap.clientHeight,html=[];
 el.innerHTML='';
 if(cur==='all'){
  // 국장·미장·크립토 동시(한눈에) — 가로 여유 있으면 3열, 아니면 3행
  const LB=15,gap=8,vert=W>=H*1.1;
  const MKL={kr:'🇰🇷 국장',us:'🇺🇸 미장',crypto:'🪙 크립토'};
  const mkts=['kr','us','crypto'].filter(m=>(DATA[m]||[]).length),n=mkts.length||1;
  const panes=vert?mkts.map((m,i)=>[m,(W-gap*(n-1))/n*i+gap*i,0,(W-gap*(n-1))/n,H])
                  :mkts.map((m,i)=>[m,0,(H-gap*(n-1))/n*i+gap*i,W,(H-gap*(n-1))/n]);
  for(const [m,x,y,w,h] of panes){
   html.push('<div class="mkl" style="left:'+(x+2)+'px;top:'+y+'px">'+MKL[m]+'</div>');
   renderMarket(m,DATA[m]||[],x,y+LB,w,h-LB,html)}
 }else{
  renderMarket(cur,DATA[cur]||[],0,0,W,H,html)}
 el.innerHTML=html.join('')||'<div style="color:#a09d96;padding:30px">데이터 수집 대기 중… (수집기 첫 실행 후 표시)</div>'}
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
function setMkt(m){cur=m;for(const k of ['all','kr','us','crypto'])
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
 tip.innerHTML='<div class="t1">'+esc(x.name)+' <span style="color:#a09d96;font-weight:500">'+esc(x.code)+'</span></div>'+
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
  :m==='crypto'?'https://upbit.com/exchange?code=CRIX.UPBIT.'+encodeURIComponent(c)
  :'https://www.tradingview.com/symbols/'+encodeURIComponent(c.replace('^',''))+'/','_blank')});
// 범례
document.getElementById('legend').innerHTML=[-3,-2,-1,0,1,2,3].map(v=>
 '<i style="background:'+color(v)+'">'+(v>0?'+':'')+v+'%</i>').join('');
// ── 로드/리사이즈 ──
async function load(){
 try{const d=await (await fetch('/api/heatmap')).json();
  DATA={kr:d.kr||[],us:d.us||[],crypto:d.crypto||[],indices:d.indices||{kr:[],us:[],crypto:[]}};TS=d.ts||'-';render()}
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
            body = json.dumps(_j(CACHE / "somi_heatmap.json", {"kr": [], "us": [], "crypto": [], "ts": "-"}),
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
    from _shared.process import ProcessLock
    port = PORT
    if "--port" in sys.argv:
        port = int(sys.argv[sys.argv.index("--port") + 1])
    # 단일 인스턴스 보장 — 락이 없어 재기동마다 좀비가 쌓여 에이전트 현황을 오염시켰다(2026-07-04).
    # 포트별로 락을 분리 — 상시 데몬(8890)과 프리뷰 인스턴스(다른 포트)가 충돌 없이 공존(포트당 단일 인스턴스는 유지).
    with ProcessLock(f"status_dashboard:{port}"):
        # 열지도 수집은 기본 포트(상시 데몬)만 — 프리뷰 인스턴스가 캐시를 중복 수집(더블 API 부하)하지 않게.
        if port == PORT:
            threading.Thread(target=_heatmap_refresher, daemon=True).start()  # 열지도 자동 갱신
        srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
        print(f"[대시보드] http://localhost:{port} 서빙 시작 (열지도: /heatmap)")
        srv.serve_forever()


if __name__ == "__main__":
    main()
