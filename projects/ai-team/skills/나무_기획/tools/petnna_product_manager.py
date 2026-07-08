#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""나무 — 펫나 기획 PM (주 1회 기능 로드맵·트렌드 조사).

주 1회(기본 화요일 11:00):
1. 현재 기능 인벤토리(js 모듈·index.html 구조) 정리
2. 구독 클로드 + 웹서치로 펫 케어 플랫폼 트렌드·경쟁 서비스 기능 조사
3. 현재 기능 대비 갭 분석 → 구현 가능한 소규모 기능 제안(JSON)
4. 공유 백로그(output/qa/petnna/backlog.json)에 적재
   → 수리가 QA 이슈 없을 때 브랜치 구현(자동 병합 없음, 항상 사람 검토 후 병합)
5. 기획 보고서 + 텔레그램 요약. 코드 수정 없음(제안만).
"""

from __future__ import annotations

import argparse
import json
import os
import sys
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
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared.utils import due_slot  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "product"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"
SLOT_STATE = PROJECT_ROOT / "output" / "cache" / "namu_slots.json"
PLAN_WEEKDAY = int(os.getenv("NAMU_WEEKDAY", "1"))  # 1=화요일


def feature_inventory() -> str:
    mods = sorted(p.stem for p in (PETNNA_ROOT / "js").glob("*.js")
                  if p.name not in ("supabase-js.js", "tailwind.js", "leaflet.js",
                                    "chart.umd.min.js"))
    return ", ".join(mods)


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
        data["items"].append({
            "id": f"{source}_{datetime.now():%Y%m%d}_{added}",
            "title": title[:120],
            "detail": (it.get("detail") or "")[:500],
            "priority": it.get("priority") if it.get("priority") in ("P2", "P3") else "P3",
            "type": itype, "source": source, "status": "대기",
            "created": datetime.now().isoformat(),
        })
        added += 1
    BACKLOG.parent.mkdir(parents=True, exist_ok=True)
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return added


def plan(do_send: bool) -> None:
    print(f"[{datetime.now()}] 🌳 나무 기획 사이클 시작")
    ok, out = run_claude(
        "너는 펫 케어 플랫폼의 시니어 PM이다. 펫나(projects/petnna, 정적 SPA + Supabase, "
        "펫 힐링/케어 플랫폼: 건강 대시보드·산책·앨범·소셜·상점·지도·사주·게임 등)의 다음 개선을 기획하라.\n\n"
        f"[현재 기능 모듈]\n{feature_inventory()}\n\n"
        "[할 일]\n"
        "1. 웹서치로 2026년 펫 케어/펫테크 앱 트렌드와 대표 경쟁 서비스(펫프렌즈·핏펫·해외 Rover/Wag 등)의 "
        "인기 기능을 조사하라.\n"
        "2. 현재 기능 대비 갭을 분석하라. 필요하면 projects/petnna/의 코드·문서를 Read로 확인하라.\n"
        "3. '정적 SPA + Supabase' 제약 안에서 1~3일 규모로 구현 가능한 소기능 3~5개를 제안하라. "
        "대규모 인프라·네이티브 전용 기능은 제외.\n\n"
        "출력: 반드시 JSON 배열만. 각 항목 {\"title\": \"기능 제목(한국어)\", "
        "\"detail\": \"무엇을·왜(트렌드/경쟁 근거)·간단한 구현 방향\", \"priority\": \"P2|P3\"}. "
        "코드는 수정하지 마라.",
        PROJECT_ROOT, timeout=900, allowed_tools="Read,WebSearch,WebFetch",
        permission_mode="plan")
    items = extract_json(out) if ok else None
    if not isinstance(items, list):
        print(f"[나무] 기획 실패/파싱 불가: {out[-200:] if out else '응답 없음'}")
        if do_send:
            send("🌳 나무 — 이번 주 기획 사이클 실패(응답 파싱 불가), 다음 주기 재시도", silent=True)
        return
    added = add_backlog_items(items, source="나무", itype="기획")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    report = OUT_DIR / f"plan_{datetime.now():%Y%m%d}.md"
    lines = [f"# 펫나 기능 기획 — {datetime.now():%Y-%m-%d} (나무)", ""]
    for it in items:
        lines.append(f"- [{it.get('priority','P3')}] {it.get('title')}\n  - {it.get('detail','')}")
    lines += ["", f"백로그 신규 적재: {added}건 → 수리가 브랜치 구현(자동 병합 없음, 사람 검토 후 병합)"]
    report.write_text("\n".join(lines), encoding="utf-8")
    print(f"기획 완료 — 제안 {len(items)}건, 백로그 적재 {added}건, {report}")
    if do_send:
        msg = [f"🌳 나무 기획 — 기능 제안 {len(items)}건 (백로그 적재 {added})"]
        msg += [f"· {it.get('title')}" for it in items[:4]]
        msg.append(f"📄 {report}")
        send("\n".join(msg), silent=True)


def daemon() -> None:
    if sys.platform == "win32" and os.getenv("PETNNA_AGENTS_ON_WINDOWS") != "true":
        print("펫나 에이전트는 맥 전용(이중 가동 방지)")
        return
    slots = os.getenv("NAMU_SLOTS", "11:00").split(",")
    with ProcessLock("namu_product_manager"):
        print(f"[{datetime.now()}] 나무 데몬 시작 — 매주 요일 {PLAN_WEEKDAY}, {','.join(slots)}")
        while True:
            try:
                if datetime.now().weekday() == PLAN_WEEKDAY and \
                        due_slot(slots, SLOT_STATE, weekdays_only=False):
                    plan(do_send=True)
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 나무 오류: {e}")
                try:
                    send(f"⚠️ 나무 기획 사이클 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(600)


def main() -> None:
    ap = argparse.ArgumentParser(description="나무 — 펫나 기획 PM")
    ap.add_argument("--once", action="store_true", help="기획 1회(요일 무관)")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--daemon", action="store_true")
    args = ap.parse_args()
    if args.daemon:
        daemon()
    else:
        plan(do_send=args.send)


if __name__ == "__main__":
    main()
