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
from _shared.process import ProcessLock  # noqa: E402
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
    lines.append("- 로그인 이후 화면·Supabase 연동 흐름 — 더미 계정 미설정으로 자동 검수 제외")
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
    """큰 이슈 → 전 에이전트 긴급 회의 소집 (비차단, 24h 중복 방지는 회의 쪽에서)."""
    import subprocess
    council = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {"start_new_session": True}
    try:
        subprocess.Popen([sys.executable, str(council), "--topic", topic[:200],
                          "--context", context[:1500], "--priority", priority],
                         cwd=str(PROJECT_ROOT),
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **nowin)
        print(f"[회의 소집] {topic[:80]}")
    except Exception as e:
        print(f"[회의 소집 실패] {e}")


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
        newest = max((p.stat().st_mtime for p in path.rglob("*") if p.is_file()), default=0)
        if time.time() - newest > hours * 3600:
            stale.append(f"- {name}: {int((time.time() - newest) / 3600)}시간째 무산출 (기준 {hours}h)")
    if stale:
        send("🕯️ 봄이 — 함대 산출물 정체 감지 (죽은 데몬 의심, 로그 확인 필요)\n" + "\n".join(stale))
    else:
        print(f"[{datetime.now()}] 함대 신선도 감사: 전원 정상 산출 중")


def daemon() -> None:
    if sys.platform == "win32" and os.getenv("PETNNA_AGENTS_ON_WINDOWS") != "true":
        print("펫나 에이전트는 맥 전용(이중 가동 방지)")
        return
    slots = os.getenv("BOMI_QA_SLOTS", "09:20").split(",")
    poll = int(os.getenv("BOMI_QA_POLL_SEC", "300"))
    cooldown = int(os.getenv("BOMI_QA_COOLDOWN_SEC", "1800"))
    with ProcessLock("bomi_qa_patrol"):
        print(f"[{datetime.now()}] 봄이 데몬 시작 — 정기 {','.join(slots)} + 변경 감지(폴링 {poll}s)")
        last_digest = _tree_digest()
        last_patrol = 0.0
        while True:
            try:
                slot = due_slot(slots, SLOT_STATE, weekdays_only=False)
                digest = _tree_digest()
                changed = digest != last_digest
                if slot or (changed and time.time() - last_patrol > cooldown):
                    reason = f"정기({slot})" if slot else "변경 감지"
                    print(f"[{datetime.now()}] 순찰 트리거: {reason}")
                    patrol(do_send=True)
                    if slot:
                        fleet_freshness_audit()
                    last_patrol = time.time()
                    last_digest = _tree_digest()
                elif changed:
                    last_digest = digest  # 쿨다운 내 변경은 기록만 (다음 폴링서 재평가 안 하도록)
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
        findings = patrol(do_send=args.send)
        c = counts(findings)
        print(f"판정: {verdict(c)} | {c}")


if __name__ == "__main__":
    main()
