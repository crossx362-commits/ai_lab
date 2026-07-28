#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""봄이 — 펫나 QA 검수관 (상시 품질 감시탑).

projects/petnna 를 로컬 서빙 후 Playwright(Chromium)로 자동 순찰:
콘솔/JS 오류, 리소스 404, 깨진 이미지, 접근성 기초(alt·라벨·버튼명),
모바일 가로스크롤, SEO 기초(title·meta·h1), 잔여 임시문구를 점검한다.

이전 순찰 결과와 비교해 신규/해결/반복 문제를 구분하고,
P0/P1 은 즉시 텔레그램 긴급 알림, 전체 보고서는 output/qa/petnna/ 에 저장.

데몬 모드: 매일 정기 순찰(BOMI_QA_SLOTS) + petnna 파일 변경 감지 시 재검수.
읽기 중심 검수만 수행 — 폼 실제 제출·데이터 변경 없음.
"""

from __future__ import annotations

import argparse
import hashlib
import http.server
import json
import os
import re
import sys
import threading
import time
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.process import ProcessLock, advisory_lock, petnna_single_machine_guard  # noqa: E402
from _shared.utils import due_slot  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
QA_DIR = PROJECT_ROOT / "output" / "qa" / "petnna"
STATE_PATH = QA_DIR / "qa_state.json"
SLOT_STATE = PROJECT_ROOT / "output" / "cache" / "bomi_qa_slots.json"
PORT = int(os.getenv("BOMI_QA_PORT", "8933"))
VIEWPORTS = {"desktop(1440x900)": (1440, 900), "mobile(390x844)": (390, 844)}
PRIORITY_ORDER = {"P0": 0, "P1": 1, "P2": 2, "P3": 3}
REPEAT_THRESHOLD = 3  # 같은 문제 3회 이상 = 반복 장애

# AppLogger.getErrorLogs()는 원래 진짜 오류(global_error 등) 전용이었는데,
# 케어위젯 실사용 계측(회의_202607162027_3, care-widget-instrumentation.js)이
# 같은 파이프라인을 재사용해 'widget_view'/'widget_click' 타입도 여기 섞여 들어온다.
# 이건 오류가 아니라 순수 사용 데이터이므로 QA 이슈로 오탐 처리하면 안 된다.
APP_LOG_NON_ISSUE_TYPES = {"widget_view", "widget_click"}


# ── 로컬 서버 ──────────────────────────────────────────────

class _SilentHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):  # 순찰 요청 로그 억제
        pass


def start_server(port: int) -> http.server.ThreadingHTTPServer:
    handler = lambda *a, **kw: _SilentHandler(*a, directory=str(PETNNA_ROOT), **kw)  # noqa: E731
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return srv


# ── 브라우저 내 DOM 감사 스크립트 ─────────────────────────

_DOM_AUDIT_JS = """() => {
  const r = {};
  r.title = document.title || "";
  const md = document.querySelector('meta[name="description"]');
  r.metaDescription = md ? (md.content || "") : "";
  r.lang = document.documentElement.getAttribute("lang") || "";
  r.h1Count = document.querySelectorAll("h1").length;
  const vis = el => { const s = getComputedStyle(el); const b = el.getBoundingClientRect();
    return s.display !== "none" && s.visibility !== "hidden" && b.width > 0 && b.height > 0; };
  r.imgsNoAlt = [...document.querySelectorAll("img:not([alt])")].filter(vis)
    .slice(0, 20).map(i => i.src.split("/").pop());
  r.brokenImgs = [...document.images].filter(i => i.complete && i.naturalWidth === 0 && i.src)
    .slice(0, 20).map(i => i.getAttribute("src") || "(빈 src)");
  r.unnamedButtons = [...document.querySelectorAll("button, a[href], [role='button']")]
    .filter(vis)
    .filter(el => !(el.innerText || "").trim() && !el.getAttribute("aria-label")
      && !el.getAttribute("title") && !el.querySelector("img[alt]"))
    .slice(0, 20).map(el => el.outerHTML.slice(0, 100));
  r.unlabeledInputs = [...document.querySelectorAll("input:not([type=hidden]), select, textarea")]
    .filter(vis)
    .filter(el => !el.labels?.length && !el.getAttribute("aria-label")
      && !el.getAttribute("placeholder") && !el.getAttribute("aria-labelledby"))
    .slice(0, 20).map(el => el.outerHTML.slice(0, 100));
  r.hScroll = document.documentElement.scrollWidth > window.innerWidth + 1;
  r.scrollWidth = document.documentElement.scrollWidth;
  r.innerWidth = window.innerWidth;
  r.bodyText = (document.body.innerText || "").length;
  return r;
}"""


# ── 순찰 ───────────────────────────────────────────────────

def _finding(priority, ftype, url, env, title, detail="", evidence=""):
    return {"priority": priority, "type": ftype, "url": url, "env": env,
            "title": title, "detail": detail, "evidence": evidence}


def _fingerprint(f: dict) -> str:
    # 회의 결정(2026-07-08): 뷰포트(env)·유형 제외, "URL + 숫자 정규화 제목"으로 단일화
    # — 같은 근본 원인이 데스크톱/모바일로 이중 계상돼 브랜치 2개가 생기던 문제 방지.
    # (지문 체계 변경으로 기존 상태와 1회성 신규/해결 흔들림 발생 — 정상)
    norm = re.sub(r"\d+", "#", f["title"])
    return hashlib.md5(f"{f['url']}|{norm}".encode("utf-8")).hexdigest()[:12]


def find_pages() -> list[str]:
    """검수 대상: 루트의 독립 html 문서(현재 SPA는 index.html 하나)."""
    return sorted(p.name for p in PETNNA_ROOT.glob("*.html"))


def static_checks() -> list[dict]:
    """파일시스템 기반 정적 검사 — 로컬 참조 깨짐·잔여 임시문구."""
    findings = []
    for page in find_pages():
        html = (PETNNA_ROOT / page).read_text(encoding="utf-8", errors="replace")
        # 로컬 src/href 참조 대상 존재 확인
        for ref in re.findall(r'(?:src|href)=["\']([^"\']+)["\']', html):
            if re.match(r"^(https?:|//|#|data:|mailto:|tel:|javascript:)", ref):
                continue
            target = (PETNNA_ROOT / ref.split("?")[0].split("#")[0].lstrip("/"))
            if not target.exists():
                findings.append(_finding(
                    "P1", "링크", f"/{page}", "정적 검사",
                    f"존재하지 않는 로컬 리소스 참조: {ref}",
                    f"{page} 에서 참조하나 파일 없음 → 로딩 실패", ref))
        # 잔여 임시문구
        for marker in re.findall(r"(TODO|FIXME|lorem ipsum|placehold(?:er)?\.(?:it|com))", html, re.I):
            findings.append(_finding(
                "P3", "콘텐츠", f"/{page}", "정적 검사",
                f"임시 문구/더미 흔적: {marker}", "출시 전 정리 필요", marker))
    return findings


# 로그인 후 클릭 인터랙션 점검용 더미 반려동물(테오 E2E와 동일한 localStorage 우회)
_QA_PET = {
    "id": 990199, "name": "순찰견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "8", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}
# 클릭 순회 대상 탭(우체통 mailbox는 social 서브탭으로 라우팅 → tab-social에서 확인)
_TAB_SWEEP = ["mypet", "health", "walk", "saju", "social", "album", "shop", "settings", "mailbox"]
_MIN_TAB_CHARS = 40  # 이보다 적으면 빈 화면 의심


def interactive_checks(page, port: int, env_name: str) -> list[dict]:
    """로그인 후 실제 클릭 흐름 점검 — 탭 전환·주요 모달 열기가 오류 없이 동작하는지.
    비파괴 원칙: 탭 전환과 모달 open/close만 하고 저장·삭제·전송 등 쓰기는 절대 하지 않는다
    (앱은 실 Supabase에 연결돼 있어 쓰기 시 실 데이터 오염 위험)."""
    findings = []
    url = f"http://127.0.0.1:{port}/index.html"
    # 로그인 게이팅 우회 + 더미 펫 주입(goto 전 실행)
    page.add_init_script(
        "try{localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','qa_patrol@petna.co.kr');"
        "localStorage.setItem('petna_pets', %s);}catch(e){}" % json.dumps(json.dumps([_QA_PET]))
    )
    try:
        page.goto(url, wait_until="load", timeout=30000)
        page.wait_for_timeout(2200)  # SPA 초기 렌더 + 로그인 우회 반영 대기
    except Exception as e:
        return [_finding("P1", "기능", "/index.html", env_name,
                         "로그인 후 앱 진입 실패", str(e)[:150])]
    # 로그인 오버레이 숨기고 메인 강제 표시(자동 로그인 완료 흐름 대체)
    try:
        page.evaluate(
            "() => { const ov=document.getElementById('login-landing-overlay'); if(ov) ov.style.display='none';"
            " document.body.classList.add('logged-in');"
            " const h=document.querySelector('header'); if(h) h.style.display='block';"
            " const m=document.querySelector('main'); if(m) m.style.display='block'; }")
    except Exception:
        pass
    # 탭 순회 — switchTab 예외·빈 렌더·클릭 중 JS 오류 수집
    try:
        sweep = page.evaluate(
            "(tabs) => {"
            "  const res=[]; const errs=[];"
            "  window.addEventListener('error', e=>errs.push(String(e.message||e.error)));"
            "  if (typeof switchTab !== 'function') return {noSwitch:true, res:[], errs:[]};"
            "  for (const t of tabs) {"
            "    let threw=null;"
            "    try { switchTab(t); } catch(e){ threw=String(e).slice(0,150); }"
            "    const id = (t==='mailbox') ? 'tab-social' : ('tab-'+t);"
            "    const el=document.getElementById(id);"
            "    const chars = el ? (el.innerText||'').replace(/\\s+/g,'').length : -1;"
            "    res.push({tab:t, threw:threw, chars:chars});"
            "  }"
            "  try { switchTab('mypet'); } catch(e){}"
            "  return {res:res, errs:errs};"
            "}", _TAB_SWEEP)
    except Exception as e:
        return [_finding("P1", "기능", "/index.html", env_name,
                         "탭 클릭 순회 실행 실패", str(e)[:150])]
    if sweep.get("noSwitch"):
        return [_finding("P1", "기능", "/index.html", env_name,
                         "switchTab 함수 없음 — 탭 전환 불가", "앱 초기화 실패 의심")]
    for r in sweep["res"]:
        if r["threw"]:
            findings.append(_finding("P1", "기능", "/index.html", env_name,
                                     f"탭 클릭 오류: {r['tab']} 전환 중 예외", r["threw"]))
        elif r["chars"] != -1 and r["chars"] < _MIN_TAB_CHARS:
            findings.append(_finding("P1", "기능", "/index.html", env_name,
                                     f"탭 빈 화면: {r['tab']} 클릭 후 콘텐츠 없음",
                                     f"렌더 콘텐츠 {r['chars']}자 — 렌더 트리거 누락 의심"))
    for e in sweep["errs"][:5]:
        findings.append(_finding("P2", "기능", "/index.html", env_name,
                                 f"클릭 중 JS 오류: {str(e)[:120]}"))
    # 주요 모달 open→close (비파괴 — 열고 즉시 닫음, 저장 안 함)
    # 닫기는 클래스 기반(hidden 추가·flex 제거) + 인라인 display 클리어로 한다.
    # 인라인 display:none을 박으면 다음 모달의 class 기반 open(flex)을 눌러버려
    # '안열림' 오탐이 난다(앱은 정상). 열림 판정도 방금 연 모달 자신으로만 한다.
    try:
        modal_probe = page.evaluate(
            "() => {"
            "  const out={};"
            "  const targets=[['펫 등록','openPetRegistrationModal',null],"
            "    ['건강 기록','openHealthLogModal','health-log-modal'],"
            "    ['건강수첩','openMedicalRecordModal','medical-record-modal']];"
            "  const closeAll=()=>{"
            "    ['closeMedicalRecordModal','closeModal'].forEach(cf=>{if(typeof window[cf]==='function'){try{window[cf]();}catch(e){}}});"
            "    document.querySelectorAll('[id$=\"-modal\"]').forEach(m=>{m.classList.add('hidden');m.classList.remove('flex');if(m.style)m.style.display='';});"
            "  };"
            "  for (const [name, fn, mid] of targets) {"
            "    if (typeof window[fn] !== 'function') { out[name]='함수없음'; continue; }"
            "    closeAll();"
            "    try {"
            "      window[fn]();"
            "      let opened;"
            "      if (mid) { const el=document.getElementById(mid); opened = !!el && getComputedStyle(el).display!=='none' && el.offsetHeight>0; }"
            "      else { opened=[...document.querySelectorAll('[id$=\"-modal\"]')].some(m=>getComputedStyle(m).display!=='none'&&m.offsetHeight>0); }"
            "      out[name]=opened?'ok':'안열림';"
            "      closeAll();"
            "    } catch(e){ out[name]='오류'; }"
            "  }"
            "  return out;"
            "}")
    except Exception:
        modal_probe = {}
    for name, st in modal_probe.items():
        if st in ("안열림", "오류", "함수없음"):
            findings.append(_finding("P2", "기능", "/index.html", env_name,
                                     f"모달 열기 실패: {name} ({st})",
                                     "버튼 클릭해도 모달이 열리지 않음"))

    # 무반응 버튼 — onclick이 "지금 보고 있는 탭으로 이동"이라 눌러도 아무 일이 없는 버튼(2026-07-25 사고).
    # 오너 지적("급여 클릭해도 아무 반응 없다"): 마이펫 홈의 케어요약 급여 셀이 switchTab('mypet')이었는데,
    # 그 카드 자체가 마이펫 탭에 있어 제자리 이동 = 무반응이었다. 기존 점검은 '탭 전환이 되는가'·'모달이
    # 열리는가'만 봐서, 오류도 안 나고 화면도 안 비는 이 유형을 구조적으로 못 잡았다.
    try:
        # 현재 탭은 AppRouter(전역 const라 window 속성이 아님) 대신 DOM에서 읽는다 —
        # 보이는 .tab-content의 id가 'tab-<name>'이라 내부 변수에 의존하지 않고 판정 가능.
        dead = page.evaluate(
            "() => {"
            "  const vis=[...document.querySelectorAll('.tab-content')]"
            "    .find(e=>!e.classList.contains('hidden'));"
            "  const cur = vis ? vis.id.replace(/^tab-/,'') : '';"
            "  if (!cur) return [];"
            "  const out=[];"
            "  vis.querySelectorAll('button[onclick]').forEach(b=>{"
            "    const h=(b.getAttribute('onclick')||'').replace(/\\s/g,'');"
            "    const m=h.match(/^switchTab\\('([a-z]+)'\\);?$/i);"   # 단일 switchTab 호출만(뒤에 다른 동작이 붙으면 무반응 아님)
            "    if (m && m[1]===cur) out.push((b.innerText||'').trim().replace(/\\s+/g,' ').slice(0,24));"
            "  });"
            "  return out.slice(0,6);"
            "}")
    except Exception:
        dead = []
    for label in (dead or []):
        findings.append(_finding(
            "P2", "기능", "/index.html", env_name,
            f"무반응 버튼: '{label}' — 현재 탭으로 이동만 함",
            "onclick이 지금 보고 있는 탭으로의 switchTab이라 클릭해도 화면이 그대로다 — "
            "실제 동작(모달·폼 열기 등)으로 바꾸거나 다른 탭을 가리켜야 한다"))

    # 내비 도달성 — 데스크톱 헤더에만 있고 모바일 하단바에 없는 탭 탐지(2026-07-25 사고).
    # 설정 탭이 모바일에서 완전히 도달 불가(어디에도 switchTab('settings') 진입점 없음)
    # 상태로 방치됐는데, 탭 순회(_TAB_SWEEP)는 switchTab을 직접 호출하므로 "화면은
    # 정상 렌더"로 통과해 버렸다 — 렌더 가능 여부와 사용자가 갈 수 있는지는 다른 문제다.
    try:
        nav = page.evaluate(
            "() => {"
            "  const d=[...document.querySelectorAll('header nav .tab-btn')]"
            "    .map(b=>b.getAttribute('data-tab')).filter(Boolean);"
            "  const m=[...document.querySelectorAll('#mobile-navbar .mobile-tab-btn')]"
            "    .map(b=>b.getAttribute('data-tab')).filter(Boolean);"
            "  return {desktop:d, mobile:m};"
            "}")
    except Exception:
        nav = None
    if nav and nav.get("desktop") and nav.get("mobile"):
        orphan = [t for t in nav["desktop"] if t not in nav["mobile"]]
        if orphan:
            findings.append(_finding(
                "P2", "기능", "/index.html", env_name,
                f"모바일에서 도달 불가한 탭: {', '.join(orphan)}",
                "데스크톱 헤더엔 있으나 모바일 하단 네비에 없음 — 모바일 사용자는 진입 경로 없음"))

    findings.extend(collapsible_reachability_checks(page, env_name))
    findings.extend(layout_waste_checks(page, env_name))
    findings.extend(duplication_checks(page, env_name))
    return findings


# 접이식(disclosure) 도달성 점검 — 2026-07-28 회의 결정 2의 병합 게이트.
#
# 카드가 길어질 때마다 "덜 쓰는 항목은 접자"는 결론이 반복되는데, 접기는 조용히
# 기능을 없앨 수 있다: 패널만 숨기고 펼침 버튼을 빠뜨리면 그 입력은 렌더는 되지만
# 사용자가 영영 도달할 수 없다(2026-07-25 설정 탭이 모바일에서 진입점 0이던 사고와
# 같은 계열 — 그때도 "렌더된다"와 "갈 수 있다"를 구분하지 않아 QA를 통과했다).
#
# aria-expanded + aria-controls를 쓴 요소만 본다(WAI-ARIA disclosure 패턴).
# 이 규약을 지키는 접이식이면 새로 만들어도 자동으로 이 점검을 받는다.
# 클릭은 펼침/접힘 토글뿐이라 비파괴 원칙을 지킨다.
def collapsible_reachability_checks(page, env_name: str) -> list[dict]:
    findings = []
    for tab in _TAB_SWEEP:
        try:
            page.evaluate(f"() => {{ if (typeof switchTab==='function') switchTab('{tab}'); }}")
            page.wait_for_timeout(500)
            items = page.evaluate(
                "() => {"
                "  const vis = e => !!(e && e.offsetParent !== null);"
                "  const out = [];"
                "  document.querySelectorAll('[aria-expanded][aria-controls]').forEach(t => {"
                "    const panel = document.getElementById(t.getAttribute('aria-controls'));"
                "    if (!panel) { out.push({toggleId: t.id||'', missingPanel: true}); return; }"
                "    out.push({"
                "      toggleId: t.id || '',"
                "      label: (t.innerText||'').trim().replace(/\\s+/g,' ').slice(0,24),"
                "      toggleVisible: vis(t),"
                "      panelVisible: vis(panel),"
                # 패널이 안 보이는 이유가 '접혀서'인지 '조상이 통째로 숨겨져서'인지 가른다.
                # 다른 탭이 활성이거나 카드가 접혀 있으면 조상이 숨겨진 것이라 판정 대상이 아니다
                # (이 구분 없이 짰다가 탭 8개 전부에서 같은 토글을 '도달 불가'로 오탐했다).
                "      contextVisible: vis(panel.parentElement),"
                "      panelId: panel.id,"
                "      inputs: panel.querySelectorAll('button,input,select,textarea').length,"
                "    });"
                "  });"
                "  return out;"
                "}")
        except Exception:
            continue

        for it in items or []:
            where = it.get("toggleId") or it.get("label") or "(무명 토글)"
            if it.get("missingPanel"):
                findings.append(_finding(
                    "P2", "기능", f"/index.html#{tab}", env_name,
                    f"접이식 토글이 없는 패널을 가리킴: {where}",
                    "aria-controls가 존재하지 않는 id를 가리킨다 — 눌러도 아무것도 안 펼쳐진다"))
                continue
            # 조상이 통째로 숨겨진 맥락(다른 탭·접힌 카드)이면 판정하지 않는다.
            if not it.get("contextVisible"):
                continue
            # 보이는 맥락인데 패널이 접혀 있고 펼칠 버튼도 없으면 그 입력은 도달 불가다.
            if not it.get("panelVisible") and not it.get("toggleVisible"):
                findings.append(_finding(
                    "P1", "기능", f"/index.html#{tab}", env_name,
                    f"도달 불가 접이식: {where} → #{it.get('panelId')}",
                    "패널이 접혀 있는데 펼침 버튼도 화면에 없다 — 그 안의 입력에 갈 방법이 없다"))
                continue
            if not it.get("toggleVisible"):
                continue
            # 실제로 눌러서 펼쳐지는지 + 펼친 뒤 입력 요소가 보이는지 확인한다.
            try:
                res = page.evaluate(
                    "(id) => {"
                    "  const vis = e => !!(e && e.offsetParent !== null);"
                    "  const t = document.getElementById(id);"
                    "  if (!t) return null;"
                    "  const before = t.getAttribute('aria-controls');"
                    "  const wasOpen = vis(document.getElementById(before));"
                    "  t.click();"
                    "  return { wasOpen, before };"
                    "}", it.get("toggleId"))
                if not res:
                    continue
                page.wait_for_timeout(250)
                after = page.evaluate(
                    "(pid) => {"
                    "  const vis = e => !!(e && e.offsetParent !== null);"
                    "  const p = document.getElementById(pid);"
                    "  if (!p) return null;"
                    "  const inputs = [...p.querySelectorAll('button,input,select,textarea')];"
                    "  return { panelVisible: vis(p), visibleInputs: inputs.filter(vis).length };"
                    "}", res["before"])
            except Exception:
                continue
            if after is None:
                continue
            opened = after["panelVisible"] != res["wasOpen"]
            if not opened:
                findings.append(_finding(
                    "P2", "기능", f"/index.html#{tab}", env_name,
                    f"접이식 토글 무반응: {where}",
                    "펼침 버튼을 눌러도 패널의 표시 상태가 바뀌지 않는다"))
            elif after["panelVisible"] and after["visibleInputs"] == 0 and it.get("inputs", 0) > 0:
                findings.append(_finding(
                    "P2", "기능", f"/index.html#{tab}", env_name,
                    f"펼쳤는데 입력이 안 보임: {where}",
                    f"패널에 입력 요소가 {it.get('inputs')}개 있으나 펼친 뒤에도 하나도 보이지 않는다"))
    return findings


# 중복·유사 분류 점검 (2026-07-26 오너 지시로 자동화).
#
# 오늘 사람이 손으로 훑어 찾아낸 것을 기계가 재현한다:
#   1) 중복 element ID — 전 탭을 순회한 뒤 DOM 전체에서 같은 id가 2개 이상
#      (실제 발견: diary-entry-undefined ×3 → album.js가 id로 항목을 찾는 탓에
#       산책 일기를 여러 개 써도 항상 첫 번째만 피드에 발행되던 진짜 오작동)
#   2) id에 undefined/null/NaN이 박힌 것 — 위 사고의 근본 신호
#   3) 탭을 넘나드는 같은/포함 관계 제목 — 같은 이름이 두 탭에 있으면 사용자는
#      같은 기능으로 읽는다(실제 발견: '주간 산책 챌린지'가 산책·소셜 양쪽에
#      있었는데 하나는 내 목표, 하나는 이웃 랭킹이었다)
#
# P2(중복 ID·깨진 id)와 P3(제목 충돌)로 나눈다 — 전자는 기능 오작동으로 이어지고
# 후자는 이름 판단이라 사람이 정해야 한다.
def duplication_checks(page, env_name: str) -> list[dict]:
    findings = []
    titles_by_tab = {}
    for tab in _TAB_SWEEP:
        try:
            page.evaluate(f"() => {{ if (typeof switchTab==='function') switchTab('{tab}'); }}")
            page.wait_for_timeout(600)
            # 요청한 탭과 실제로 보이는 컨테이너가 다르면 건너뛴다 —
            # mailbox는 social의 서브탭이라 switchTab('mailbox')를 해도 tab-social이
            # 그대로 보인다. 이걸 안 걸러 social의 제목을 자기 자신과 비교해
            # '탭 간 충돌' 5건을 오탐했다(2026-07-26).
            res = page.evaluate(_TAB_TITLES_JS)
            if not res or res.get("tabId") != f"tab-{tab}":
                continue
            titles_by_tab[tab] = res.get("titles") or []
        except Exception:
            continue

    # 1)·2) DOM 전체 id 상태 — 전 탭을 이미 순회했으므로 모든 탭 내용이 DOM에 있다
    try:
        idinfo = page.evaluate(_DUP_ID_JS)
    except Exception:
        idinfo = None
    for dup in (idinfo or {}).get("dups", [])[:3]:
        findings.append(_finding(
            "P2", "기능", "/index.html", env_name,
            f"중복 element ID: '{dup['id']}' {dup['count']}개",
            "같은 id가 여러 개면 getElementById가 첫 번째만 잡는다 — id로 항목을 "
            "찾는 코드(삭제·공유 등)가 엉뚱한 대상에 동작할 수 있다"))
    for bad in (idinfo or {}).get("broken", [])[:3]:
        findings.append(_finding(
            "P2", "기능", "/index.html", env_name,
            f"id에 미정의 값이 들어감: '{bad}'",
            "템플릿이 `id=prefix-${obj.id}` 형태로 만드는데 원본에 id가 없다는 뜻 — "
            "그 항목들이 서로 구분되지 않는다"))

    # 3) 탭을 넘나드는 제목 충돌(같거나 한쪽이 다른 쪽을 포함)
    seen = []
    for tab, titles in titles_by_tab.items():
        for t in titles or []:
            for otab, ot in seen:
                if otab == tab:
                    continue
                if t == ot or (len(t) >= 6 and len(ot) >= 6 and (t in ot or ot in t)):
                    findings.append(_finding(
                        "P3", "UI", "/index.html", env_name,
                        f"탭 간 제목 충돌: '{t}' ({otab} ↔ {tab})",
                        "같은 이름이 두 탭에 있으면 사용자는 같은 기능으로 읽는다 — "
                        "실제로 다른 기능이라면 이름을 구분할 것"))
                    break
            seen.append((tab, t))
    # 같은 제목쌍이 여러 번 잡히지 않게 앞쪽 3건만
    return findings[:6]


_TAB_TITLES_JS = r"""
() => {
  const tab = [...document.querySelectorAll('.tab-content')].find(t => !t.classList.contains('hidden'));
  if (!tab) return { tabId: null, titles: [] };
  const vis = (el) => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0; };
  const out = new Set();
  tab.querySelectorAll('h2,h3,h4').forEach((h) => {
    if (!vis(h)) return;
    const t = (h.textContent || '').replace(/\s+/g, ' ').trim();
    // 이모지·기호를 걷어내고 글자만 비교한다(🏆 유무로 다른 제목이 되지 않게)
    const norm = t.replace(/[^가-힣a-zA-Z0-9 ]/g, '').replace(/\s+/g, ' ').trim();
    if (norm.length >= 4 && norm.length <= 24) out.add(norm);
  });
  return { tabId: tab.id, titles: [...out] };
}
"""

_DUP_ID_JS = r"""
() => {
  const counts = {};
  document.querySelectorAll('[id]').forEach((e) => { counts[e.id] = (counts[e.id] || 0) + 1; });
  const dups = Object.entries(counts).filter(([, n]) => n > 1)
      .map(([id, count]) => ({ id, count }));
  // null은 '없음' 선택지처럼 의도된 값일 수 있어 제외한다(poop-type-null 오탐).
  // undefined·NaN은 어떤 경우에도 의도적으로 넣지 않는다.
  const broken = Object.keys(counts).filter((id) => /(undefined|NaN)/.test(id));
  return { dups, broken };
}
"""


# 데스크톱 레이아웃 낭비·중복 구조 점검 (2026-07-26 오너 지시로 자동화).
#
# 오너가 반복해서 지적한 세 가지를 사람 눈 대신 기계가 잡는다:
#   1) 카드 폭은 넓은데 안의 콘텐츠가 절반도 안 차 오른쪽이 텅 빈 행
#      (예: 케어 요약 5칸을 max-w-xl로 묶어 1400px 카드의 1/3만 쓰던 것)
#   2) 같은 탭에 제목이 겹치는 카드 — 같은 데이터를 두 카드가 따로 묻는 신호
#      (예: '오늘의 기록' 옆에 '오늘의 컨디션 원탭 기록'이 따로 있던 것)
#   3) 카드 속 카드 — 감싼 카드 안에서 자식이 또 카드 테두리를 그려 이중 액자가 되는 것
#
# 전부 '보기 나쁨'이라 자동 수정은 하지 않고 P3로 보고만 한다(오탐 여지가 있어
# 자동 병합 대상으로 만들지 않는다 — 판단은 사람/디자이너 몫).
_WASTE_MIN_CARD_W = 700      # 이 폭 미만 카드는 애초에 여백이 남을 수 없다
_WASTE_FILL_RATIO = 0.5      # 콘텐츠가 카드 안쪽 폭의 이 비율 미만이면 낭비로 본다


def layout_waste_checks(page, env_name: str) -> list[dict]:
    findings = []
    try:
        page.set_viewport_size({"width": 1440, "height": 900})
    except Exception:
        return findings
    for tab in _TAB_SWEEP:
        try:
            page.evaluate(f"() => {{ if (typeof switchTab==='function') switchTab('{tab}'); }}")
            page.wait_for_timeout(700)
            res = page.evaluate(_LAYOUT_WASTE_JS)
        except Exception:
            continue
        for w in (res or {}).get("waste", [])[:2]:
            findings.append(_finding(
                "P3", "UI", f"/index.html#{tab}", env_name,
                f"[{tab}] 가로 여백 낭비: '{w['label']}' 콘텐츠가 카드 폭의 {w['fill']}%만 사용",
                f"카드 {w['cardW']}px 안에서 내용이 {w['contentW']}px — 넓은 화면에서 오른쪽이 비어 보인다"))
        for t in (res or {}).get("dupTitles", [])[:2]:
            findings.append(_finding(
                "P3", "UI", f"/index.html#{tab}", env_name,
                f"[{tab}] 제목이 겹치는 카드: '{t}'",
                "같은 데이터를 카드 두 장이 따로 묻고 있을 수 있다 — 병합 검토 대상"))
        for n in (res or {}).get("nested", [])[:2]:
            findings.append(_finding(
                "P3", "UI", f"/index.html#{tab}", env_name,
                f"[{tab}] 카드 속 카드: '{n}'",
                "감싼 카드 안에서 자식이 또 카드 테두리를 그려 이중 액자가 된다"))
    return findings


_LAYOUT_WASTE_JS = r"""
() => {
  const MINW = %d, RATIO = %s;
  const vis = (el) => { const r = el.getBoundingClientRect();
      return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden'; };
  const tab = [...document.querySelectorAll('.tab-content')].find(t => !t.classList.contains('hidden'));
  if (!tab) return { waste: [], dupTitles: [], nested: [] };
  const cards = [...tab.querySelectorAll('.card-modern')].filter(vis);

  // 1) 가로 여백 낭비: '넓은 부모 안에서 혼자 쪼그라든 콘텐츠 묶음'을 찾는다.
  //    카드 직계 자식만 보면 안 된다 — 실제 낭비는 자식 *안쪽*에서 생긴다
  //    (케어 요약은 카드 자식은 full-width인데 그 안 그리드만 max-w-xl로 좁았다.
  //     첫 버전이 직계만 봐서 네거티브 컨트롤을 통과시켰다, 2026-07-26).
  const waste = [];
  for (const c of cards) {
    const cr = c.getBoundingClientRect();
    if (cr.width < MINW) continue;
    const cs = getComputedStyle(c);
    const inner = cr.width - parseFloat(cs.paddingLeft || 0) - parseFloat(cs.paddingRight || 0);
    if (inner <= 0) continue;
    const label = (c.querySelector('h2,h3,h4')?.textContent || c.id || '(제목 없음)')
      .replace(/\s+/g, ' ').trim().slice(0, 30);
    for (const el of [...c.querySelectorAll('*')].filter(vis)) {
      if (el.children.length < 2) continue;              // 콘텐츠 묶음만(라벨·단일요소 제외)
      // 제목+부제로 이뤄진 헤더 블록은 좁은 게 정상이다(오른쪽은 버튼 자리이거나
      // 그냥 여백이어도 무방) — 2026-07-26 오탐 대부분이 이것이었다.
      // 높이로 거르려 했더니 케어 요약 그리드(45px)까지 죽어 정탐을 잃었다.
      // 제목 요소를 품고 있으면 헤더로 보고 건너뛴다 — 콘텐츠 덩어리는 제목을 안 품는다.
      if (el.querySelector('h1,h2,h3,h4')) continue;
      const p = el.parentElement;
      if (!p || !vis(p)) continue;
      const ew = el.getBoundingClientRect().width;
      const pw = p.getBoundingClientRect().width;
      if (pw < inner * 0.9) continue;                    // 부모가 이미 좁으면 의도된 컬럼
      if (ew >= pw * RATIO) continue;                    // 부모 폭을 충분히 쓰면 정상
      // 낭비의 단위는 '요소 하나'가 아니라 '행'이다. 같은 행(세로로 겹치는 형제)까지
      // 합쳐 **가로로 뻗은 범위(span)**가 부모 폭을 채우면 정상 레이아웃이다.
      //  · 그리드 N등분 칸 → 형제들과 합쳐 전폭 → 통과
      //  · justify-between 헤더(제목 좌 + 버튼 우) → 양끝이라 span은 전폭 → 통과
      // 폭 '합계'로 재면 justify-between은 합이 작아 오탐이고(2026-07-26 오탐 2건),
      // 세로 겹침을 안 보면 위아래 행까지 한 행으로 쳐서 진짜 낭비를 놓친다.
      const er = el.getBoundingClientRect();
      const row = [...p.children].filter(vis).map(s => s.getBoundingClientRect())
          .filter(r => r.bottom > er.top + 1 && r.top < er.bottom - 1);
      const spanL = Math.min(...row.map(r => r.left));
      const spanR = Math.max(...row.map(r => r.right));
      if ((spanR - spanL) >= pw * RATIO) continue;
      waste.push({ label, fill: Math.round(ew / inner * 100),
                   cardW: Math.round(cr.width), contentW: Math.round(ew) });
      break;                                             // 카드당 1건이면 충분
    }
  }

  // 2) 제목 중복(한쪽이 다른 쪽을 포함하는 관계도 중복으로 본다)
  //    card-merge 래퍼와 그 안의 카드는 '같은 제목 요소 하나'를 각각 집어오므로
  //    제목 텍스트가 아니라 **제목 요소 자체**로 먼저 중복을 제거해야 한다
  //    (안 그러면 병합 카드마다 자기 제목이 중복이라고 보고한다 — 실제 오탐 발생).
  const seenEls = new Set();
  const titles = [];
  for (const c of cards) {
    const h = c.querySelector('h2,h3,h4');
    if (!h || seenEls.has(h)) continue;
    seenEls.add(h);
    const t = (h.textContent || '').replace(/\s+/g, ' ').trim();
    if (t.length >= 4) titles.push(t);
  }
  const dupTitles = [];
  for (let i = 0; i < titles.length; i++) {
    for (let j = i + 1; j < titles.length; j++) {
      if (titles[i] === titles[j] || titles[i].includes(titles[j]) || titles[j].includes(titles[i])) {
        if (!dupTitles.includes(titles[i])) dupTitles.push(titles[i]);
      }
    }
  }

  // 3) 카드 속 카드 — card-merge 래퍼는 내부 테두리를 지우는 의도된 패턴이라 제외
  const nested = [];
  for (const c of cards) {
    if (c.classList.contains('card-merge') || c.closest('.card-merge')) continue;
    const inner = [...c.querySelectorAll('.card-modern')].filter(vis)
        .filter(x => !x.closest('.card-merge'));
    if (inner.length) {
      const outer = (c.querySelector('h2,h3,h4')?.textContent || c.id || '').replace(/\s+/g,' ').trim().slice(0,24);
      const innerT = (inner[0].querySelector('h2,h3,h4')?.textContent || '').replace(/\s+/g,' ').trim().slice(0,24);
      if (outer && innerT) nested.push(`${outer} > ${innerT}`);
    }
  }
  return { waste, dupTitles, nested };
}
""" % (_WASTE_MIN_CARD_W, _WASTE_FILL_RATIO)


def browser_patrol(port: int) -> list[dict]:
    from playwright.sync_api import sync_playwright

    findings = []
    shots_dir = QA_DIR / "shots"
    shots_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M")

    with sync_playwright() as p:
        browser = p.chromium.launch()
        for env_name, (w, h) in VIEWPORTS.items():
            ctx = browser.new_context(viewport={"width": w, "height": h})
            page = ctx.new_page()
            console_errors, page_errors, failed_reqs = [], [], []
            page.on("console", lambda m: console_errors.append(m.text) if m.type == "error" else None)
            page.on("pageerror", lambda e: page_errors.append(str(e)))
            page.on("response", lambda r: failed_reqs.append(f"{r.status} {r.url}")
                    if r.status >= 400 else None)

            for doc in find_pages():
                url = f"http://127.0.0.1:{port}/{doc}"
                console_errors.clear(); page_errors.clear(); failed_reqs.clear()
                try:
                    page.goto(url, wait_until="load", timeout=30000)
                    page.wait_for_timeout(2500)  # SPA 초기 렌더 대기
                except Exception as e:
                    findings.append(_finding("P0", "기능", f"/{doc}", env_name,
                                             "페이지 로드 실패", str(e)[:200]))
                    continue

                shot = shots_dir / f"{stamp}_{doc.replace('.html','')}_{w}x{h}.png"
                try:
                    page.screenshot(path=str(shot))
                except Exception:
                    shot = None

                for err in page_errors[:5]:
                    findings.append(_finding("P1", "기능", f"/{doc}", env_name,
                                             f"JS 런타임 오류: {err[:120]}",
                                             "초기 로드 중 발생 — 기능 미동작 가능",
                                             str(shot or "")))
                # 외부(https) 실패는 로컬 환경상 네트워크 요인일 수 있어 P2, 로컬은 P1
                for fr in failed_reqs[:10]:
                    local = "127.0.0.1" in fr
                    findings.append(_finding("P1" if local else "P2", "링크", f"/{doc}", env_name,
                                             f"리소스 응답 오류: {fr[:150]}",
                                             "로컬 파일 누락" if local else "외부 리소스 실패(추정 — 추가 확인 필요)"))
                for ce in console_errors[:5]:
                    if any(ce in f["title"] for f in findings):
                        continue
                    findings.append(_finding("P2", "기능", f"/{doc}", env_name,
                                             f"콘솔 오류: {ce[:120]}"))

                try:
                    a = page.evaluate(_DOM_AUDIT_JS)
                except Exception as e:
                    findings.append(_finding("P2", "기능", f"/{doc}", env_name,
                                             f"DOM 감사 실행 실패: {str(e)[:120]}"))
                    continue

                if a["hScroll"] and "mobile" in env_name:
                    findings.append(_finding("P1", "반응형", f"/{doc}", env_name,
                                             f"가로 스크롤 발생 (콘텐츠 {a['scrollWidth']}px > 화면 {a['innerWidth']}px)",
                                             "모바일에서 레이아웃 넘침", str(shot or "")))
                for src in a["brokenImgs"]:
                    findings.append(_finding("P2", "링크", f"/{doc}", env_name,
                                             f"깨진 이미지: {src[:120]}"))
                if a["imgsNoAlt"]:
                    findings.append(_finding("P2", "접근성", f"/{doc}", env_name,
                                             f"alt 없는 이미지 {len(a['imgsNoAlt'])}개",
                                             ", ".join(a["imgsNoAlt"][:5])))
                if a["unnamedButtons"]:
                    findings.append(_finding("P2", "접근성", f"/{doc}", env_name,
                                             f"접근 가능한 이름 없는 버튼/링크 {len(a['unnamedButtons'])}개",
                                             a["unnamedButtons"][0]))
                if a["unlabeledInputs"]:
                    findings.append(_finding("P2", "접근성", f"/{doc}", env_name,
                                             f"라벨 없는 입력 필드 {len(a['unlabeledInputs'])}개",
                                             a["unlabeledInputs"][0]))
                if not a["title"]:
                    findings.append(_finding("P3", "SEO", f"/{doc}", env_name, "페이지 title 비어 있음"))
                if not a["metaDescription"]:
                    findings.append(_finding("P3", "SEO", f"/{doc}", env_name, "meta description 없음"))
                if a["h1Count"] == 0:
                    findings.append(_finding("P3", "SEO", f"/{doc}", env_name, "H1 없음"))
                elif a["h1Count"] > 1:
                    findings.append(_finding("P3", "SEO", f"/{doc}", env_name, f"H1 {a['h1Count']}개 (중복)"))
                if not a["lang"]:
                    findings.append(_finding("P3", "접근성", f"/{doc}", env_name, "html lang 속성 없음"))
                if a["bodyText"] < 30:
                    findings.append(_finding("P1", "기능", f"/{doc}", env_name,
                                             "본문 텍스트가 거의 없음 — 빈 화면 의심",
                                             f"body 텍스트 {a['bodyText']}자", str(shot or "")))

                # 앱 자체 오류 수집기(AppLogger→localStorage) 흡수 — 핸들된 오류·스택까지 확보
                try:
                    # AppLogger는 const 선언이라 window 프로퍼티가 아님 — typeof로 접근
                    app_logs = page.evaluate(
                        "() => (typeof AppLogger !== 'undefined' && AppLogger.getErrorLogs) "
                        "? AppLogger.getErrorLogs().slice(0, 15) : []")
                except Exception:
                    app_logs = []
                seen_msgs = set()
                for lg in app_logs:
                    if lg.get("type") in APP_LOG_NON_ISSUE_TYPES:
                        continue  # 사용 계측 로그 — 오류 아님, QA 이슈로 만들지 않는다
                    # 타임스탬프·숫자 가변부 정규화 → 순찰 간 동일 오류로 지문 유지
                    msg = re.sub(r"\d+", "#", str(lg.get("message", ""))[:110])
                    key = f"{lg.get('type')}|{msg}"
                    if key in seen_msgs:
                        continue
                    seen_msgs.add(key)
                    pri = "P1" if lg.get("type") in ("global_error", "global_rejection") else "P2"
                    findings.append(_finding(
                        pri, "기능", f"/{doc}", env_name,
                        f"앱 오류로그[{lg.get('type')}]: {msg}",
                        (str(lg.get("stack", ""))[:300] or "스택 없음") + " — AppLogger 수집(순찰 세션)"))

            # 로그인 후 클릭 인터랙션 점검(탭 전환·모달 열기) — 정적 로드가 못 잡는 동작 오류 포착
            try:
                findings.extend(interactive_checks(page, port, env_name))
            except Exception as e:
                findings.append(_finding("P2", "기능", "/index.html", env_name,
                                         "인터랙션 점검 실행 오류", str(e)[:150]))
            ctx.close()
        browser.close()
    return findings


# ── 이전 결과 비교 · 보고 ─────────────────────────────────

def load_state() -> dict:
    try:
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {"findings": {}, "last_run": None}


def diff_and_save(findings: list[dict]) -> dict:
    state = load_state()
    prev = state.get("findings", {})
    now = {}
    for f in findings:
        fp = _fingerprint(f)
        f["id"] = fp
        f["seen_count"] = prev.get(fp, {}).get("seen_count", 0) + 1
        now[fp] = {"seen_count": f["seen_count"], "priority": f["priority"],
                   "title": f["title"], "url": f["url"],
                   # 수리(개발 에이전트)가 자동 수정 대상 선별에 쓰는 상세 필드
                   "type": f["type"], "env": f["env"], "detail": f.get("detail", "")}
    new_ids = [fp for fp in now if fp not in prev]
    resolved = [prev[fp] for fp in prev if fp not in now]
    repeated = [fp for fp in now if now[fp]["seen_count"] >= REPEAT_THRESHOLD]
    QA_DIR.mkdir(parents=True, exist_ok=True)
    STATE_PATH.write_text(json.dumps(
        {"findings": now, "last_run": datetime.now().isoformat()},
        ensure_ascii=False, indent=1), encoding="utf-8")
    return {"new": new_ids, "resolved": resolved, "repeated": repeated}


def counts(findings):
    c = {"P0": 0, "P1": 0, "P2": 0, "P3": 0}
    for f in findings:
        c[f["priority"]] += 1
    return c


def verdict(c) -> str:
    if c["P0"]:
        return "출시 보류"
    if c["P1"]:
        return "조건부 가능"
    return "출시 가능"


def write_report(findings: list[dict], delta: dict, pages: list[str]) -> Path:
    findings = sorted(findings, key=lambda f: (PRIORITY_ORDER[f["priority"]], f["type"]))
    c = counts(findings)
    now = datetime.now()
    lines = [
        f"# 펫나 QA 순찰 보고서 — {now:%Y-%m-%d %H:%M} (봄이)",
        "",
        "## 1. 전체 상태",
        f"- 판정: **{verdict(c)}**",
        f"- 검사 문서: {len(pages)}개 × 뷰포트 {len(VIEWPORTS)}종 (Chromium)",
        f"- 문제: P0 {c['P0']} / P1 {c['P1']} / P2 {c['P2']} / P3 {c['P3']}",
        f"- 신규 {len(delta['new'])} · 해결 {len(delta['resolved'])} · 반복(3회+) {len(delta['repeated'])}",
        "",
    ]
    urgent = [f for f in findings if f["priority"] in ("P0", "P1")]
    lines.append("## 2. 즉시 확인 (P0/P1)")
    if urgent:
        for f in urgent:
            lines.append(f"- [{f['priority']}][{f['type']}] {f['title']} — {f['url']} ({f['env']})")
    else:
        lines.append("- 현재 확인 범위에서는 발견되지 않음")
    lines.append("")
    lines.append("## 3. 전체 문제 목록")
    if not findings:
        lines.append("- 현재 확인 범위에서는 발견되지 않음")
    for i, f in enumerate(findings, 1):
        tag = " 🔁반복" if f["id"] in delta["repeated"] else (" 🆕신규" if f["id"] in delta["new"] else "")
        lines += [f"### [{i}] {f['title']}{tag}",
                  f"- 우선순위: {f['priority']} / 유형: {f['type']}",
                  f"- URL: {f['url']} / 환경: {f['env']}"]
        if f.get("detail"):
            lines.append(f"- 상세: {f['detail']}")
        if f.get("evidence"):
            lines.append(f"- 증거: {f['evidence']}")
        lines.append(f"- 재검수: 수정 후 `python {Path(__file__).name} --once` 재실행, 동일 항목 소멸 확인")
        lines.append("")
    lines.append("## 4. 이전 순찰 대비 변화")
    lines.append(f"- 해결됨: " + (", ".join(r["title"][:60] for r in delta["resolved"][:10]) or "없음"))
    lines.append(f"- 반복(3회+): {len(delta['repeated'])}건")
    lines.append("")
    lines.append("## 5. 미검수 영역 (추가 확인 필요)")
    lines.append("- 로그인 후 클릭 인터랙션(탭 전환·주요 모달 열기)은 더미 계정 우회로 자동 점검 중 — "
                 "단 폼 저장·삭제·전송 등 쓰기 흐름은 실 DB 오염 방지를 위해 비검수(비파괴 원칙)")
    lines.append("- Firefox/WebKit 크로스브라우저, Lighthouse 성능 점수 — 도구 추가 시 확장")
    QA_DIR.mkdir(parents=True, exist_ok=True)
    path = QA_DIR / f"report_{now:%Y%m%d_%H%M}.md"
    path.write_text("\n".join(lines), encoding="utf-8")
    return path


def summary_message(findings, delta, report_path) -> str:
    c = counts(findings)
    lines = [f"🧪 봄이 QA 순찰 — 펫나",
             f"판정: {verdict(c)} | P0 {c['P0']}·P1 {c['P1']}·P2 {c['P2']}·P3 {c['P3']}",
             f"신규 {len(delta['new'])} · 해결 {len(delta['resolved'])} · 반복 {len(delta['repeated'])}"]
    urgent = [f for f in findings if f["priority"] in ("P0", "P1")][:5]
    for f in urgent:
        lines.append(f"⚠️ [{f['priority']}] {f['title'][:80]} — {f['url']}")
    lines.append(f"📄 {report_path}")
    return "\n".join(lines)


def urgent_message(findings) -> str | None:
    urgent = sorted([f for f in findings if f["priority"] in ("P0", "P1")],
                    key=lambda f: PRIORITY_ORDER[f["priority"]])
    if not urgent:
        return None
    top = urgent[0]
    return ("🚨 [긴급 QA 알림] 펫나\n"
            f"문제: {top['title'][:120]}\n"
            f"영향: {top.get('detail') or '핵심 화면 동작 저해 가능'}\n"
            f"발생: {top['url']} ({top['env']})\n"
            f"우선순위: {top['priority']} (긴급 총 {len(urgent)}건)\n"
            "바로 확인: 보고서의 즉시 확인 섹션")


# ── 실행 ───────────────────────────────────────────────────

def _convene_council(topic: str, context: str, priority: str) -> None:
    """큰 이슈 → 전 에이전트 긴급 회의 소집 (비차단, 24h 중복 방지는 회의 쪽에서).

    stdout/stderr를 DEVNULL로 버리지 않는다 — 동시에 다른 안건 회의가 진행 중이면
    ProcessLock 충돌로 회의가 실제로 안 열린 채 조용히 종료되는데(2026-07-12 자동
    파이프라인 감사가 발견 — 어제 유휴디스패치를 제거하게 만든 'Popen 성공 ≠ 스크립트
    실행 성공' 패턴과 동일 계열), DEVNULL이면 이 트리거 쪽에서 그 사실을 알 방법이
    없다. bot_logs에 이어 써서 최소한 사후에 로그로 확인 가능하게 한다."""
    import subprocess
    council = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {"start_new_session": True}
    log_dir = PROJECT_ROOT / "output" / "bot_logs"
    out_f = err_f = None
    try:
        log_dir.mkdir(parents=True, exist_ok=True)
        out_f = open(log_dir / "petnna_council_trigger.out.log", "a", encoding="utf-8")
        err_f = open(log_dir / "petnna_council_trigger.err.log", "a", encoding="utf-8")
        print(f"[{datetime.now()}] === 봄이 회의 소집: {topic[:80]} ===", file=out_f, flush=True)
        subprocess.Popen([sys.executable, str(council), "--topic", topic[:200],
                          "--context", context[:1500], "--priority", priority],
                         cwd=str(PROJECT_ROOT), stdout=out_f, stderr=err_f, **nowin)
        print(f"[회의 소집] {topic[:80]}")
    except Exception as e:
        print(f"[회의 소집 실패] {e}")
    finally:
        if out_f:
            out_f.close()
        if err_f:
            err_f.close()


def patrol(do_send: bool) -> list[dict]:
    print(f"[{datetime.now()}] 🧪 봄이 순찰 시작 (포트 {PORT})")
    srv = start_server(PORT)
    try:
        findings = static_checks() + browser_patrol(PORT)
    finally:
        srv.shutdown()
        srv.server_close()  # 소켓까지 닫아야 같은 프로세스의 다음 순찰이 포트 재사용 가능
    # 동일 문제(지문 기준) 중복 제거 — 같은 리소스 오류가 여러 번 잡혀도 1건으로 보고
    seen, unique = set(), []
    for f in findings:
        fp = _fingerprint(f)
        if fp not in seen:
            seen.add(fp)
            unique.append(f)
    findings = unique
    delta = diff_and_save(findings)
    report = write_report(findings, delta, find_pages())
    print(f"[{datetime.now()}] 순찰 완료 — 문제 {len(findings)}건, 보고서 {report}")
    if do_send:
        msg = urgent_message(findings)
        if msg:
            send(msg)
        send(summary_message(findings, delta, report), silent=not msg)
        # 신규 P0/P1 = 큰 이슈 → 전 에이전트 긴급 회의
        urgent_new = [f for f in findings
                      if f["priority"] in ("P0", "P1") and f["id"] in delta["new"]]
        if urgent_new:
            top = urgent_new[0]
            _convene_council(f"긴급 QA: {top['title'][:120]}",
                             f"{top.get('detail','')} | URL {top['url']} ({top['env']}) | "
                             f"신규 P0/P1 총 {len(urgent_new)}건", top["priority"])
    return findings


def _tree_digest() -> str:
    """petnna 파일 변경 감지용 다이제스트 (경로+mtime+size)."""
    h = hashlib.md5()
    for p in sorted(PETNNA_ROOT.rglob("*")):
        if p.is_file() and "node_modules" not in p.parts and not p.name.startswith("."):
            st = p.stat()
            h.update(f"{p.relative_to(PETNNA_ROOT)}|{st.st_mtime_ns}|{st.st_size}".encode())
    return h.hexdigest()


def fleet_freshness_audit() -> None:
    """함대 산출물 신선도 감사 — '켜져는 있는데 아무것도 안 만드는' 죽은 데몬 감지
    (주식 시절 교훈: 프로세스 생존 ≠ 일하는 중. 산출물이 실제로 쌓이는지 봐야 한다)."""
    qa_base = PROJECT_ROOT / "output" / "qa" / "petnna"
    checks = [("봄이 QA 보고서", QA_DIR, 36), ("수리 개선 루프", qa_base / "dev", 36),
              ("테오 테스트 결과", qa_base / "tests", 36), ("백호 백엔드 감사", qa_base / "backend", 36),
              ("미오 디자인 리뷰", qa_base / "design", 8 * 24), ("나무 기획", qa_base / "product", 8 * 24)]
    stale = []
    for name, path, hours in checks:
        if not path.exists():
            # rglob()이 없는 경로에선 빈 제너레이터를 내놔 max(default=0)이 잡히고,
            # time.time()-0이 항상 기준을 넘겨 "디렉터리 미생성"을 "무산출 경보"로
            # 오판했다(2026-07-13 파이프라인 감사가 발견). 설치 초기처럼 아직 산출물이
            # 한 번도 안 쌓인 정상 상태를 죽은 데몬으로 잘못 알리는 것을 막는다.
            stale.append(f"- {name}: 디렉터리 미생성(아직 첫 산출물 없음)")
            continue
        newest = max((p.stat().st_mtime for p in path.rglob("*") if p.is_file()), default=0)
        if time.time() - newest > hours * 3600:
            stale.append(f"- {name}: {int((time.time() - newest) / 3600)}시간째 무산출 (기준 {hours}h)")
    if stale:
        send("🕯️ 봄이 — 함대 산출물 정체 감지 (죽은 데몬 의심, 로그 확인 필요)\n" + "\n".join(stale))
    else:
        print(f"[{datetime.now()}] 함대 신선도 감사: 전원 정상 산출 중")


def daemon() -> None:
    if petnna_single_machine_guard("봄이"):
        return
    slots = os.getenv("BOMI_QA_SLOTS", "09:20").split(",")
    poll = int(os.getenv("BOMI_QA_POLL_SEC", "300"))
    cooldown = int(os.getenv("BOMI_QA_COOLDOWN_SEC", "1800"))
    with ProcessLock("bomi_qa_patrol_daemon"):  # 중복 데몬 기동 방지(상시 보유, 이 이름 전용)
        print(f"[{datetime.now()}] 봄이 데몬 시작 — 정기 {','.join(slots)} + 변경 감지(폴링 {poll}s)")
        last_digest = _tree_digest()
        last_patrol = 0.0
        while True:
            try:
                digest = _tree_digest()
                changed = digest != last_digest
                # "bomi_qa_patrol"(daemon 접미사 없음)는 실행 구간에만 짧게 잡는 비치명적
                # 락 — 수동 --once 실행과 겹쳐도(포트 8933 바인딩·qa_state.json 동시쓰기
                # 방지) 데몬이 죽지 않는다. due_slot()은 호출 즉시 "오늘 실행됨"을
                # 기록해버리므로(부작용) 락 밖에서 먼저 부르면, 락을 못 잡아 patrol이
                # 스킵돼도 슬롯은 이미 소진돼 그날 정기 순찰(+ fleet_freshness_audit)이
                # 통째로 유실된다(2026-07-11 2차 파이프라인 감사가 발견 — 백호만 이 순서가
                # 맞았음, 대칭 맞춤).
                with advisory_lock("bomi_qa_patrol") as got:
                    if got:
                        slot = due_slot(slots, SLOT_STATE, weekdays_only=False)
                        if slot or (changed and time.time() - last_patrol > cooldown):
                            reason = f"정기({slot})" if slot else "변경 감지"
                            print(f"[{datetime.now()}] 순찰 트리거: {reason}")
                            patrol(do_send=True)
                            if slot:
                                fleet_freshness_audit()
                            last_patrol = time.time()
                            last_digest = _tree_digest()
                        elif changed:
                            last_digest = digest  # 쿨다운 내 변경은 기록만(다음 폴링서 재평가 안 하도록)
                    else:
                        print(f"[{datetime.now()}] 다른 실행이 진행 중 — 이번 주기 건너뜀")
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 순찰 오류: {e}")
                try:
                    send(f"⚠️ 봄이 QA 순찰 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(poll)


def main() -> None:
    ap = argparse.ArgumentParser(description="봄이 — 펫나 QA 순찰")
    ap.add_argument("--once", action="store_true", help="전체 순찰 1회")
    ap.add_argument("--send", action="store_true", help="결과 텔레그램 전송")
    ap.add_argument("--daemon", action="store_true", help="상시 데몬 (정기 + 변경 감지)")
    args = ap.parse_args()

    if args.daemon:
        daemon()
    else:
        with advisory_lock("bomi_qa_patrol") as got:
            if not got:
                print("다른 실행이 진행 중 — 건너뜀")
                return
            findings = patrol(do_send=args.send)
            c = counts(findings)
            print(f"판정: {verdict(c)} | {c}")


if __name__ == "__main__":
    main()
