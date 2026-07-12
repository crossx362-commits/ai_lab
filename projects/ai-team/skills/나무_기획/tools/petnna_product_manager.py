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
from _shared.telegram import send  # noqa: E402
from _shared.process import ProcessLock, advisory_lock, petnna_single_machine_guard  # noqa: E402
from _shared.utils import due_slot  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared.backlog import touches_db_auth  # noqa: E402

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
    except FileNotFoundError:
        data = {"items": []}
    except Exception as e:
        # 파일은 있는데 파싱 실패(다른 프로세스의 non-atomic write 도중 읽었을 가능성) —
        # 빈 dict로 대체해 이 함수 끝에서 통째로 덮어쓰면 기존 백로그 전체가 소실된다
        # (자동 파이프라인 감사 도구가 발견, 2026-07-12: 6개 도구가 락 없이 backlog.json에
        # 동시 접근하는 경합의 가장 파괴적인 구체 사례). 이번 적재는 건너뛰고 다음 주기에
        # 재시도한다 — 기존 파일을 그대로 보존.
        print(f"[나무] 백로그 읽기 실패(파일 손상 가능) — 이번 적재는 건너뜀: {e}")
        return 0
    existing = {i.get("title") for i in data["items"]}
    # id 충돌 방지 — 날짜만으론(%Y%m%d) 같은 날 plan()이 두 번 불리면(수동 --once가 화요일
    # 정기 슬롯과 겹치거나, 향후 배정과제 자체폴링이 추가되는 경우) 서로 다른 항목이 같은
    # id를 가질 수 있다 — id로 조회하는 수리 dev_state의 attempts 오집계 위험(미오에서
    # 2026-07-12에 먼저 발견·수정된 것과 동일한 패턴이 나무에도 있었음 — 비대칭 방치).
    # "실제 존재하는 id와 안 겹칠 때까지 증가"로 근본 해결(미오와 동일 패턴).
    existing_ids = {i.get("id") for i in data["items"]}
    added = 0
    for it in items:
        title = (it.get("title") or "").strip()
        if not title or title in existing:
            continue
        detail = (it.get("detail") or "")[:500]
        # DB/인증 접촉 과제는 수리가 병합할 수 없다 — 자동 루프 밖(보류)으로 적재한다.
        db_auth = touches_db_auth(title, detail)
        stamp = datetime.now().strftime("%Y%m%d%H%M%S")
        new_id = f"{source}_{stamp}_{added}"
        while new_id in existing_ids:
            added += 1
            new_id = f"{source}_{stamp}_{added}"
        existing_ids.add(new_id)
        data["items"].append({
            "id": new_id,
            "title": title[:120],
            "detail": detail,
            "priority": it.get("priority") if it.get("priority") in ("P2", "P3") else "P3",
            "type": itype, "source": source,
            "status": "보류" if db_auth else "대기",
            "gate": "DB/인증" if db_auth else "",
            "created": datetime.now().isoformat(),
        })
        added += 1
    BACKLOG.parent.mkdir(parents=True, exist_ok=True)
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return added


def plan(do_send: bool) -> None:
    print(f"[{datetime.now()}] 🌳 나무 기획 사이클 시작")
    # 1단계: 자유 형식 리서치·분석 (JSON 강제 없음 — 스키마를 걸면 오히려
    # "오너에게 보고하는 PM" 대화체로 이탈하는 경우가 잦았다, 2026-07-09 확인).
    ok, analysis = run_claude(
        "너는 펫 케어 플랫폼의 시니어 PM이다. 펫나(projects/petnna, 정적 SPA + Supabase, "
        "펫 힐링/케어 플랫폼: 건강 대시보드·산책·앨범·소셜·상점·지도·사주·게임 등)의 다음 개선을 기획하라.\n\n"
        f"[현재 기능 모듈]\n{feature_inventory()}\n\n"
        "[할 일]\n"
        "1. 웹서치로 2026년 펫 케어/펫테크 앱 트렌드와 대표 경쟁 서비스(펫프렌즈·핏펫·해외 Rover/Wag 등)의 "
        "인기 기능을 조사하라.\n"
        "2. 현재 기능 대비 갭을 분석하라. 필요하면 projects/petnna/의 코드·문서를 Read로 확인하라.\n"
        "3. '정적 SPA + Supabase' 제약 안에서 1~3일 규모로 구현 가능한 소기능 3~5개를 제안하라. "
        "대규모 인프라·네이티브 전용 기능은 제외. 코드는 수정하지 마라.",
        PROJECT_ROOT, timeout=900, allowed_tools="Read,WebSearch,WebFetch",
        permission_mode="acceptEdits")
    if not ok or not analysis:
        print(f"[나무] 리서치 실패: {analysis[-200:] if analysis else '응답 없음'}")
        if do_send:
            send("🌳 나무 — 이번 주 기획 사이클 실패(리서치 단계), 다음 주기 재시도", silent=True)
        return

    # 2단계: 별도의 단순 추출 호출로 위 분석에서 항목만 JSON화.
    # 리서치와 포맷팅을 한 호출에 섞지 않아야 포맷 준수율이 오른다.
    extracted = llm_text(
        "다음은 펫 케어 앱 기능 기획 분석이다. 여기서 실제로 제안된 구체적 신규 기능들만 뽑아 "
        "JSON으로 정리하라. 새로운 내용을 추가하지 말고 분석 내용만 반영하라.\n\n"
        f"[분석]\n{analysis[:6000]}\n\n"
        "출력은 반드시 JSON 객체 하나: "
        '{"items": [{"title": "기능 제목(한국어)", "detail": "무엇을·왜·구현 방향", '
        '"priority": "P2 또는 P3"}]}. 구체적 기능 제안이 없으면 items를 빈 배열로 하라.',
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
        print(f"[나무] 기획 실패/파싱 불가: {extracted[-200:] if extracted else '응답 없음'}")
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
    if petnna_single_machine_guard("나무"):
        return
    slots = os.getenv("NAMU_SLOTS", "11:00").split(",")
    with ProcessLock("namu_product_manager_daemon"):  # 중복 데몬 기동 방지(상시 보유, 이 이름 전용)
        print(f"[{datetime.now()}] 나무 데몬 시작 — 매주 요일 {PLAN_WEEKDAY}, {','.join(slots)}")
        while True:
            try:
                # "namu_product_manager"(daemon 접미사 없음)는 실행 구간에만 짧게 잡는
                # 비치명적 락 — 수동 --once 실행과 겹쳐도 데몬이 죽지 않는다(자동 파이프라인
                # 감사 도구가 발견한 비대칭: 미오·테오·백호만 이 보호가 있었음, 2026-07-11).
                # due_slot()은 호출 즉시 "오늘 실행됨"을 기록하므로(부작용) 락 밖에서 먼저
                # 부르면, 락을 못 잡아 plan()이 스킵돼도 슬롯은 이미 소진돼 그 주 전체가
                # 유실된다(미오·테오·봄이에서 동일 패턴을 2차 감사가 발견 후 대칭 수정).
                with advisory_lock("namu_product_manager") as got:
                    if got:
                        if datetime.now().weekday() == PLAN_WEEKDAY and \
                                due_slot(slots, SLOT_STATE, weekdays_only=False):
                            plan(do_send=True)
                    else:
                        print(f"[{datetime.now()}] 다른 실행이 진행 중 — 이번 주기 건너뜀")
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
        with advisory_lock("namu_product_manager") as got:
            if got:
                plan(do_send=args.send)
            else:
                print("다른 실행이 진행 중 — 건너뜀")


if __name__ == "__main__":
    main()
