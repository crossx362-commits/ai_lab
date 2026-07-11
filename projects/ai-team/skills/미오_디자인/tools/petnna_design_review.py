#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""미오 — 펫나 디자인 리뷰어 (주 1회 스크린샷 기반 UX·시각 품질 리뷰).

주 1회(기본 월요일 11:00):
1. 데스크톱(1440x900)·모바일(390x844) 풀페이지 스크린샷 촬영
2. 구독 클로드가 스크린샷 + 디자인 기준(projects/petnna/DESIGN.md 있으면 그것,
   없으면 일반 UX 휴리스틱 + references/awesome-design-md 참고)을 읽고 리뷰
3. 개선 항목(JSON)을 공유 백로그(output/qa/petnna/backlog.json)에 적재
   → 수리가 QA 이슈 없을 때 집어 브랜치 구현(자동 병합 없음, 항상 사람 검토)
4. 리뷰 보고서 + 텔레그램 요약. 모르는 디자인 트렌드/근거는 웹서치.
"""

from __future__ import annotations

import argparse
import http.server
import json
import os
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
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared.backlog import touches_db_auth  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "design"

BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"
SLOT_STATE = PROJECT_ROOT / "output" / "cache" / "mio_slots.json"
PORT = int(os.getenv("MIO_PORT", "8936"))
REVIEW_WEEKDAY = int(os.getenv("MIO_WEEKDAY", "0"))  # 0=월요일


class _SilentHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


def take_screenshots() -> list[Path]:
    from playwright.sync_api import sync_playwright

    handler = lambda *a, **kw: _SilentHandler(*a, directory=str(PETNNA_ROOT), **kw)  # noqa: E731
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", PORT), handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    shots = []
    stamp = datetime.now().strftime("%Y%m%d")
    (OUT_DIR / "shots").mkdir(parents=True, exist_ok=True)
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch()
            for label, (w, h) in {"desktop": (1440, 900), "mobile": (390, 844)}.items():
                ctx = browser.new_context(viewport={"width": w, "height": h})
                page = ctx.new_page()
                page.goto(f"http://127.0.0.1:{PORT}/index.html", wait_until="load", timeout=30000)
                page.wait_for_timeout(2500)
                path = OUT_DIR / "shots" / f"{stamp}_{label}.png"
                page.screenshot(path=str(path), full_page=True)
                shots.append(path)
                ctx.close()
            browser.close()
    finally:
        srv.shutdown()
        srv.server_close()
    return shots


def add_backlog_items(items: list[dict], source: str, itype: str) -> int:
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        data = {"items": []}
    existing = {i.get("title") for i in data["items"]}
    added = 0
    for it in items:
        title = (it.get("title") or "").strip()
        if not title or title in existing:
            continue
        detail = (it.get("detail") or "")[:500]
        # DB/인증 접촉 과제는 수리가 병합할 수 없다 — 자동 루프 밖(보류)으로 적재한다.
        db_auth = touches_db_auth(title, detail)
        data["items"].append({
            "id": f"{source}_{datetime.now():%Y%m%d}_{added}",
            "title": title[:120],
            "detail": detail,
            "priority": it.get("priority") if it.get("priority") in ("P1", "P2", "P3") else "P3",
            "type": itype, "source": source,
            "status": "보류" if db_auth else "대기",
            "gate": "DB/인증" if db_auth else "",
            "created": datetime.now().isoformat(),
        })
        added += 1
    BACKLOG.parent.mkdir(parents=True, exist_ok=True)
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return added


def _assigned_tasks() -> list[dict]:
    """회의가 미오에게 배정한 대기 중 디자인 과제 — 이번 리뷰에서 반영할 지침."""
    try:
        items = json.loads(BACKLOG.read_text(encoding="utf-8"))["items"]
    except Exception:
        return []
    return [i for i in items
            if i.get("owner") == "미오" and i.get("status") == "대기" and i.get("type") == "디자인"]


def _close_tasks(task_ids: list[str]) -> None:
    if not task_ids:
        return
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        return
    for i in data["items"]:
        if i.get("id") in task_ids:
            i["status"] = "완료"
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")


def review(do_send: bool) -> None:
    print(f"[{datetime.now()}] 🎨 미오 디자인 리뷰 시작")
    shots = take_screenshots()
    assigned = _assigned_tasks()
    design_md = PETNNA_ROOT / "DESIGN.md"
    ref = (f"디자인 기준 문서 {design_md} 를 Read로 읽고 그 기준으로 평가하라."
           if design_md.exists() else
           "일반 UX 휴리스틱(시각 위계·CTA 가시성·여백·타이포·일관성·모바일 터치 영역)으로 평가하고, "
           "필요하면 references/awesome-design-md/ 의 실사이트 디자인 시스템을 참고하라.")
    # 1단계: 자유 형식 리뷰 (JSON 강제 없음 — 스키마를 걸면 오히려 "오너에게 보고하는
    # 디자이너" 대화체로 이탈하는 경우가 잦았다, 2026-07-09 나무에서 확인).
    charter = ""
    if assigned:
        charter = ("\n[회의가 이번 리뷰에 배정한 과제 — 반드시 다루어라]\n"
                   + "\n".join(f"- {t['title']}: {t.get('detail', '')}" for t in assigned) + "\n")
    ok, analysis = run_claude(
        "너는 시니어 프로덕트 디자이너다. 펫나(펫 케어 플랫폼 SPA)의 현재 화면을 리뷰하라.\n"
        f"스크린샷을 Read 도구로 열어 실제로 보라: {', '.join(str(s) for s in shots)}\n"
        f"{ref}\n"
        "모르는 디자인 트렌드·근거는 웹서치로 확인하라.\n"
        f"{charter}\n"
        "실행 가능하고 CSS/HTML 수준에서 구현 가능한 것 위주로 3~6개 제안하라. 코드는 수정하지 마라.",
        PROJECT_ROOT, timeout=600, allowed_tools="Read,WebSearch,WebFetch",
        permission_mode="acceptEdits")
    if not ok or not analysis:
        print(f"[미오] 리뷰 실패: {analysis[-200:] if analysis else '응답 없음'}")
        if do_send:
            send("🎨 미오 — 이번 주 디자인 리뷰 실패(리뷰 단계), 다음 주기 재시도", silent=True)
        return

    # 2단계: 별도의 단순 추출 호출로 위 리뷰에서 항목만 JSON화.
    extracted = llm_text(
        "다음은 앱 디자인 리뷰다. 여기서 실제로 제안된 구체적 개선 항목들만 뽑아 JSON으로 "
        "정리하라. 새로운 내용을 추가하지 말고 리뷰 내용만 반영하라.\n\n"
        f"[리뷰]\n{analysis[:6000]}\n\n"
        "출력은 반드시 JSON 객체 하나: "
        '{"items": [{"title": "개선 제목(한국어, 구체적으로)", "detail": "무엇을 어떻게 바꿀지 + 근거", '
        '"priority": "P2 또는 P3"}]}. 구체적 개선 제안이 없으면 items를 빈 배열로 하라.',
        json_mode=True, task="coding",
    )
    parsed = extract_json(extracted) if extracted else None
    if isinstance(parsed, dict):
        items = parsed.get("items")
    elif isinstance(parsed, list):
        # extract_json()은 텍스트에서 먼저 발견되는 대괄호 쌍을 우선 시도하므로
        # {"items":[...]}의 안쪽 배열만 반환되는 경우가 있다 — 그대로 받아들인다.
        items = parsed
    else:
        items = None
    if not isinstance(items, list):
        print(f"[미오] 리뷰 실패/파싱 불가: {extracted[-200:] if extracted else '응답 없음'}")
        if do_send:
            send("🎨 미오 — 이번 주 디자인 리뷰 실패(응답 파싱 불가), 다음 주기 재시도", silent=True)
        return
    added = add_backlog_items(items, source="미오", itype="디자인")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    report = OUT_DIR / f"review_{datetime.now():%Y%m%d}.md"
    lines = [f"# 펫나 디자인 리뷰 — {datetime.now():%Y-%m-%d} (미오)", ""]
    for it in items:
        lines.append(f"- [{it.get('priority','P3')}] {it.get('title')}\n  - {it.get('detail','')}")
    lines += ["", f"스크린샷: {', '.join(s.name for s in shots)}",
              f"백로그 신규 적재: {added}건 → 수리가 브랜치 구현(자동 병합 없음)"]
    report.write_text("\n".join(lines), encoding="utf-8")
    # 배정 과제는 리뷰가 실제로 산출물을 남긴 뒤에만 닫는다(실패 시 대기로 남아 다음 주기 재시도).
    _close_tasks([t["id"] for t in assigned])
    print(f"리뷰 완료 — 제안 {len(items)}건, 백로그 적재 {added}건, {report}")
    if do_send:
        msg = [f"🎨 미오 디자인 리뷰 — 제안 {len(items)}건 (백로그 적재 {added})"]
        msg += [f"· {it.get('title')}" for it in items[:4]]
        msg.append(f"📄 {report}")
        send("\n".join(msg), silent=True)


def daemon() -> None:
    if petnna_single_machine_guard("미오"):
        return
    slots = os.getenv("MIO_SLOTS", "11:00").split(",")
    # 배정 과제(회의·예원이 백로그에 올린 것)는 주간 전체 리뷰 요일까지 기다리면 최대 6일
    # 방치될 수 있다(2026-07-11 발견 — 오너 "왜 다 노냐" 지적). 배정 과제가 있으면 요일과
    # 무관하게 이 주기마다 즉시 처리, 없으면 기존대로 주 1회 전체 트렌드 리뷰만 수행.
    assigned_poll = int(os.getenv("MIO_ASSIGNED_POLL_SEC", "3600"))
    with ProcessLock("mio_design_review_daemon"):  # 중복 데몬 기동 방지(상시 보유, 이 이름 전용)
        print(f"[{datetime.now()}] 미오 데몬 시작 — 매주 요일 {REVIEW_WEEKDAY} 전체리뷰 "
              f"{','.join(slots)} + 배정과제는 {assigned_poll}s마다 확인")
        last_assigned_check = 0.0
        while True:
            try:
                due_full = (datetime.now().weekday() == REVIEW_WEEKDAY
                            and due_slot(slots, SLOT_STATE, weekdays_only=False))
                due_assigned = (not due_full and _assigned_tasks()
                                 and time.time() - last_assigned_check > assigned_poll)
                if due_full or due_assigned:
                    # "mio_design_review"(daemon 접미사 없음)는 실행 구간에만 짧게 잡는
                    # 비치명적 락 — 예원 워치독의 수동 디스패치와 실행 시점이 겹쳐도
                    # 데몬이 죽지 않고 이번 주기를 건너뛴다(2026-07-11).
                    with advisory_lock("mio_design_review") as got:
                        if got:
                            if due_assigned:
                                print(f"[{datetime.now()}] 배정 과제 발견 — 요일 무관 즉시 리뷰")
                            review(do_send=True)
                            last_assigned_check = time.time()
                        else:
                            print(f"[{datetime.now()}] 다른 실행이 진행 중 — 이번 주기 건너뜀")
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 미오 오류: {e}")
                try:
                    send(f"⚠️ 미오 디자인 리뷰 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(600)


def main() -> None:
    ap = argparse.ArgumentParser(description="미오 — 펫나 디자인 리뷰어")
    ap.add_argument("--once", action="store_true", help="리뷰 1회(요일 무관)")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--daemon", action="store_true")
    args = ap.parse_args()
    if args.daemon:
        daemon()
    else:
        # 수동 실행도 데몬의 실행구간 락과 상호배제(비치명적) — 예원 워치독의 자동
        # 디스패치와 데몬 자체 주기가 겹쳐도 스크린샷·백로그 동시쓰기 경합을 막는다.
        with advisory_lock("mio_design_review") as got:
            if got:
                review(do_send=args.send)
            else:
                print("다른 실행이 진행 중 — 건너뜀")


if __name__ == "__main__":
    main()
