#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""테오 — 펫나 E2E 테스트 엔지니어.

핵심 사용자 흐름의 Playwright E2E 테스트를 자동으로 '작성'하고 '실행'한다.
- 테스트 작성: 구독 클로드(claude -p)가 미커버 흐름 1개씩 테스트 생성(하루 1개, 최대 TEO_MAX)
  → 2회 연속 통과(안정성) 확인 후에만 채택·커밋. 불안정하면 폐기(flaky 방지).
- 테스트 실행: 매일 정기 + petnna 변경 감지 시 전체 스위트 실행,
  실패는 텔레그램 보고 + output/qa/petnna/tests/results.json 기록(수리·사람이 소비).

테스트 계약: projects/petnna/tests/e2e/test_*.py 가 NAME(str)과 run(page, base_url)을 정의.
run은 Playwright sync Page로 흐름을 검증하고 실패 시 assert/예외로 죽는다. 앱 코드 수정 금지.
"""

from __future__ import annotations

import argparse
import hashlib
import http.server
import importlib.util
import json
import os
import subprocess
import sys
import threading
import time
import traceback
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
from _shared.cc import run_claude  # noqa: E402

load_env(str(PROJECT_ROOT))

PETNNA_ROOT = PROJECT_ROOT / "projects" / "petnna"
E2E_DIR = PETNNA_ROOT / "tests" / "e2e"
OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "tests"
RESULTS = OUT_DIR / "results.json"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"
SLOT_STATE = PROJECT_ROOT / "output" / "cache" / "teo_slots.json"
PORT = int(os.getenv("TEO_PORT", "8935"))
MAX_TESTS = int(os.getenv("TEO_MAX", "8"))
PER_TEST_TIMEOUT_MS = 30000

# 커버하고 싶은 핵심 흐름(생성 우선순위 힌트) — 실제 화면 구조는 클로드가 코드를 읽고 판단
CORE_FLOWS = [
    "홈 초기 로딩(본문 렌더·콘솔 심각 오류 없음)",
    "하단/상단 탭 네비게이션 전환",
    "로그인 화면 진입·폼 요소 표시",
    "설정 화면 진입",
    "지도(주변 장소) 화면 진입",
    "건강 대시보드 화면 진입",
    "상점(shop) 화면 진입",
    "모바일 뷰포트(390x844) 홈 레이아웃",
]


class _SilentHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


def start_server(port: int) -> http.server.ThreadingHTTPServer:
    handler = lambda *a, **kw: _SilentHandler(*a, directory=str(PETNNA_ROOT), **kw)  # noqa: E731
    srv = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return srv


def list_tests() -> list[Path]:
    E2E_DIR.mkdir(parents=True, exist_ok=True)
    return sorted(E2E_DIR.glob("test_*.py"))


def _load_test(path: Path):
    spec = importlib.util.spec_from_file_location(path.stem, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def run_suite(only: Path | None = None) -> dict:
    """전체(또는 단일) 테스트 실행 → {name: {ok, error, sec}}."""
    from playwright.sync_api import sync_playwright

    tests = [only] if only else list_tests()
    results = {}
    if not tests:
        return results
    srv = start_server(PORT)
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch()
            for path in tests:
                t0 = time.time()
                name = path.stem
                try:
                    mod = _load_test(path)
                    ctx = browser.new_context(viewport={"width": 1440, "height": 900})
                    page = ctx.new_page()
                    page.set_default_timeout(PER_TEST_TIMEOUT_MS)
                    mod.run(page, f"http://127.0.0.1:{PORT}/index.html")
                    ctx.close()
                    results[name] = {"ok": True, "name": getattr(mod, "NAME", name),
                                     "sec": round(time.time() - t0, 1)}
                except Exception:
                    results[name] = {"ok": False, "name": name,
                                     "error": traceback.format_exc(limit=3)[-800:],
                                     "sec": round(time.time() - t0, 1)}
            browser.close()
    finally:
        srv.shutdown()
        srv.server_close()
    return results


def save_results(results: dict) -> tuple[int, int]:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    prev = {}
    try:
        prev = json.loads(RESULTS.read_text(encoding="utf-8")).get("results", {})
    except Exception:
        pass
    # 연속 실패 카운트(3회 연속 불안정 = flaky 분류)
    for name, r in results.items():
        streak = prev.get(name, {}).get("fail_streak", 0)
        r["fail_streak"] = 0 if r["ok"] else streak + 1
    RESULTS.write_text(json.dumps(
        {"run_at": datetime.now().isoformat(), "results": results},
        ensure_ascii=False, indent=1), encoding="utf-8")
    passed = sum(1 for r in results.values() if r["ok"])
    return passed, len(results) - passed


def report(results: dict, do_send: bool) -> None:
    if not results:
        print("테스트 없음")
        return
    passed, failed = save_results(results)
    lines = [f"🧷 테오 E2E 결과 — 통과 {passed} / 실패 {failed}"]
    for name, r in results.items():
        if not r["ok"]:
            flaky = " (flaky 의심 3회+)" if r["fail_streak"] >= 3 else ""
            lines.append(f"❌ {r['name']}{flaky}\n{r['error'].splitlines()[-1][:150]}")
    print("\n".join(lines))
    if do_send and (failed or os.getenv("TEO_ALWAYS_REPORT") == "true"):
        send("\n".join(lines)[:3500])


# ── 테스트 자동 생성 ───────────────────────────────────────

def covered_flows() -> str:
    names = []
    for path in list_tests():
        try:
            names.append(getattr(_load_test(path), "NAME", path.stem))
        except Exception:
            names.append(path.stem)
    return ", ".join(names) if names else "(없음)"


def _backlog_task() -> dict | None:
    """회의가 테오에게 배정한 대기 중 테스트 과제 1건(우선순위·생성순)."""
    try:
        items = json.loads(BACKLOG.read_text(encoding="utf-8"))["items"]
    except Exception:
        return None
    mine = [i for i in items
            if i.get("owner") == "테오" and i.get("status") == "대기" and i.get("type") == "테스트"]
    mine.sort(key=lambda i: (i.get("priority", "P2"), i.get("created", "")))
    return mine[0] if mine else None


def _backlog_done(task_id: str) -> None:
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        return
    for i in data["items"]:
        if i.get("id") == task_id:
            i["status"] = "완료"
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")


def generate_test(do_send: bool) -> bool:
    existing = list_tests()
    if len(existing) >= MAX_TESTS:
        return False
    task = _backlog_task()
    if task:
        target = (f"[회의 배정 과제 — 이 흐름을 최우선으로 커버하라]\n"
                  f"- {task['title']}\n  {task.get('detail', '')}\n\n")
    else:
        target = (f"[커버 후보(우선순위 순, 미커버 중 택1)]\n"
                  + "\n".join(f"- {f}" for f in CORE_FLOWS) + "\n\n")
    prompt = (
        "너는 펫나(projects/petnna, 정적 SPA) E2E 테스트 엔지니어다. "
        "Playwright(Python sync) E2E 테스트 파일을 '하나만' 새로 만들어라.\n\n"
        f"[이미 커버된 흐름]\n{covered_flows()}\n\n"
        + target +
        "[계약 — 반드시 지켜라]\n"
        "- 파일 위치: projects/petnna/tests/e2e/test_<영문슬러그>.py (새 파일 1개만 생성)\n"
        "- 파일은 NAME = \"<흐름 이름(한국어)>\" 상수와 def run(page, base_url): 함수를 정의한다.\n"
        "- run은 playwright sync Page를 받아 page.goto(base_url)부터 흐름을 검증하고, 실패는 assert로 표현한다.\n"
        "- 앱 코드(index.html·js·css)는 절대 수정 금지. 테스트 파일 외 어떤 파일도 만들지/바꾸지 마라.\n"
        "- 외부 네트워크(수파베이스 등) 성공에 의존하지 마라 — 화면 구조·가시성 위주로 검증.\n"
        "- 셀렉터는 index.html/js를 실제로 읽고 존재하는 것만 사용. 불확실하면 더 안정적인 상위 요소로.\n"
        "- 대기는 page.wait_for_selector/expect 계열로, time.sleep 금지(2.5초 초기 렌더 대기 1회는 허용).\n"
        "- 마지막에 만든 파일 경로와 검증 내용을 1~2줄로 요약하라."
    )
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=900,
                         allowed_tools="WebSearch,WebFetch")
    new = [p for p in list_tests() if p not in existing]
    if not new:
        print(f"[테오] 테스트 생성 실패: {out[-200:]}")
        return False
    path = new[0]
    # 안정성 게이트: 2회 연속 통과해야 채택
    for i in range(2):
        r = run_suite(only=path)
        if not r or not list(r.values())[0]["ok"]:
            err = list(r.values())[0].get("error", "?")[-200:] if r else "실행 불가"
            path.unlink(missing_ok=True)
            print(f"[테오] 신규 테스트 불안정({i+1}회차 실패) → 폐기: {err}")
            return False
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    subprocess.run(["git", "add", str(path)], cwd=str(PROJECT_ROOT), capture_output=True, **nowin)
    name = getattr(_load_test(path), "NAME", path.stem)
    subprocess.run(["git", "commit", "-m", f"test(petnna): E2E '{name}' 추가 (테오 자동 생성)"],
                   cwd=str(PROJECT_ROOT), capture_output=True, **nowin)
    if task:  # 채택(2회 연속 통과)된 뒤에만 과제를 닫는다 — 폐기 시엔 대기로 남아 재시도
        _backlog_done(task["id"])
    print(f"[테오] 신규 테스트 채택: {path.name} — {name}")
    if do_send:
        send(f"🧷 테오 — 새 E2E 테스트 추가\n{name} ({path.name}), 2회 연속 통과 확인 후 채택")
    return True


# ── 실행 ───────────────────────────────────────────────────

def _tree_digest() -> str:
    h = hashlib.md5()
    for p in sorted(PETNNA_ROOT.rglob("*")):
        if p.is_file() and "node_modules" not in p.parts and not p.name.startswith("."):
            st = p.stat()
            h.update(f"{p.relative_to(PETNNA_ROOT)}|{st.st_mtime_ns}|{st.st_size}".encode())
    return h.hexdigest()


def daemon() -> None:
    if petnna_single_machine_guard("테오"):
        return
    slots = os.getenv("TEO_SLOTS", "10:00").split(",")
    poll = int(os.getenv("TEO_POLL_SEC", "300"))
    cooldown = int(os.getenv("TEO_COOLDOWN_SEC", "1200"))
    with ProcessLock("teo_test_engineer_daemon"):  # 중복 데몬 기동 방지(상시 보유, 이 이름 전용)
        print(f"[{datetime.now()}] 테오 데몬 시작 — 정기 {','.join(slots)} + 변경 감지")
        last_digest = _tree_digest()
        last_run = 0.0
        while True:
            try:
                slot = due_slot(slots, SLOT_STATE, weekdays_only=False)
                digest = _tree_digest()
                changed = digest != last_digest
                if slot:
                    # "teo_test_engineer"(daemon 접미사 없음)는 실행 구간에만 짧게 잡는
                    # 비치명적 락 — 예원 워치독의 수동 디스패치와 겹쳐도 데몬이 죽지 않는다.
                    with advisory_lock("teo_test_engineer") as got:
                        if got:
                            generate_test(do_send=True)  # 하루 1개 생성 시도(상한 도달 시 no-op)
                            report(run_suite(), do_send=True)
                            last_run = time.time()
                            last_digest = _tree_digest()
                        else:
                            print(f"[{datetime.now()}] 다른 실행이 진행 중 — 이번 주기 건너뜀")
                elif changed and time.time() - last_run > cooldown:
                    print(f"[{datetime.now()}] 변경 감지 → 스위트 실행")
                    with advisory_lock("teo_test_engineer") as got:
                        if got:
                            report(run_suite(), do_send=True)
                            last_run = time.time()
                            last_digest = _tree_digest()
                elif changed:
                    last_digest = digest
            except Exception as e:
                print(f"[{datetime.now()}] ⚠️ 테오 오류: {e}")
                try:
                    send(f"⚠️ 테오 테스트 엔진 오류: {str(e)[:200]}", silent=True)
                except Exception:
                    pass
            time.sleep(poll)


def main() -> None:
    ap = argparse.ArgumentParser(description="테오 — 펫나 E2E 테스트 엔지니어")
    ap.add_argument("--run", action="store_true", help="스위트 1회 실행")
    ap.add_argument("--gen", action="store_true", help="테스트 1개 생성 시도")
    ap.add_argument("--send", action="store_true", help="텔레그램 전송")
    ap.add_argument("--daemon", action="store_true")
    args = ap.parse_args()
    if args.daemon:
        daemon()
        return
    with advisory_lock("teo_test_engineer") as got:
        if not got:
            print("다른 실행이 진행 중 — 건너뜀")
            return
        if args.gen:
            generate_test(do_send=args.send)
        if args.run or not args.gen:
            report(run_suite(), do_send=args.send)


if __name__ == "__main__":
    main()
