#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 역할별 상시 감사 — 회의를 기다리지 않고 각 역할이 따로 일한다.

오너 지시(2026-08-15): "모든 에이전트 개발진행에 참여해".

회의(`game_council.py`)는 6인이 **같은 안건**에 모여 하루 두 번 결론을 낸다. 이 도구는
같은 6인이 **각자 자기 렌즈로 상시 훑는다** — 회의가 놓친 것을 자기 영역에서 찾아
백로그에 쌓는다. 페르소나·근거 규칙·프롬프트는 `game_council`에서 그대로 가져온다
(같은 개념을 두 곳에 따로 두면 한쪽만 갱신돼 어긋난다 — 이 저장소가 반복해서 겪은 패턴).

⚠️ **유니티를 쓰는 역할은 여기 없다.** 지금 병목은 인원이 아니라 **유니티 락 하나**다.
빌드·플레이 검증이 필요한 점검을 여기 넣으면 개발 세션 빌드를 exit 21로 죽인다.
그래서 이 도구는 **읽기 전용 역할만** 돌린다 — 실행 검증은 마루(`game_build_verify.py`)와
개발 세션이 유니티 락을 쥔 채로 한다.

산출: 역할별 보고서 `output/qa/ashes-to-stars/agents/<역할>_<ts>.md` + 백로그 적재.
"""

from __future__ import annotations

import argparse
import json
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
PROJECT_ROOT = SCRIPT_DIR.parents[4]
sys.path.insert(0, str(PROJECT_ROOT / "projects" / "ai-team"))

from _shared.env import load_env        # noqa: E402
from _shared.telegram import send       # noqa: E402
from _shared.process import advisory_lock  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.llm import ollama  # noqa: E402

import importlib.util as _u             # noqa: E402
_spec = _u.spec_from_file_location("game_council", SCRIPT_DIR / "game_council.py")
_gc = _u.module_from_spec(_spec); _spec.loader.exec_module(_gc)   # 페르소나·규칙·상황수집 재사용

load_env(str(PROJECT_ROOT))

OUT_DIR = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars" / "agents"
BACKLOG = _gc.BACKLOG

# 역할별 상시 과제. 회의 안건("다음에 뭘 만드나")과 달리 **자기 영역을 훑는** 질문이다.
BEATS = {
    "정합성": "기획서의 ✅ 확정 중 코드에서 아직 성립하지 않는 것을 찾아라. 문서와 코드가 다르면 기획서가 맞다.",
    "구현":   "죽은 데이터·죽은 경로를 찾아라 — 설정·에셋·필드는 있는데 읽는 코드가 0곳인 것. 실제로 grep해서 확인하라.",
    "밸런스": "수치가 §18 앵커에서 유도된 것인지, 코드의 실제 값과 기획서 표가 일치하는지 대조하라.",
    "연출":   "500체 화면에서 규칙이 눈에 읽히는지 — 계열·역할·위험이 형태나 색으로 구분되는지 코드와 아트 스펙을 대조하라.",
    "검증":   "통과 기준에 네거티브 컨트롤이 없는 검증을 찾아라. '고쳤더니 좋아졌다'만 있고 '되돌리면 나빠진다'가 없는 것.",
    "우선순위": "지금 백로그와 ORDERS.md에서 순서가 잘못된 것을 찾아라 — 선행 조건이 안 끝났는데 뒤가 먼저 잡혀 있는 것.",
}


def _priority_ok(items) -> bool:
    """우선순위 역할의 로컬 결과가 쓸 만한지. 미달이면 클로드로 승격.

    `game_council._chair_ok`와 같은 원칙 — verify가 없는 항목은 검증 불가능한
    지시를 만들 뿐이다.
    """
    if not isinstance(items, list):
        return False
    for i in items:
        if not isinstance(i, dict) or not str(i.get("title", "")).strip():
            return False
        if not str(i.get("verify", "")).strip():
            return False
    return True


def _beat(lens: str, focus: str, situation: str) -> tuple[str, str, list]:
    """역할 하나가 자기 영역을 훑고 (보고서, 백로그항목) 반환.

    ⚠️ **"우선순위"만 로컬 모델 1순위다.** 나머지 5개 역할(정합성·구현·밸런스·연출·검증)은
    Read/Grep으로 코드를 직접 읽어야 하는 에이전틱 작업이라 로컬 완성 호출로 옮길 수 없다
    (도구 호출이 아니라 텍스트 생성 한 번이라서). 우선순위만 예외인 이유는 판단 재료가
    이미 `situation`(ORDERS.md·백로그·커밋 로그)에 다 들어 있어서 추가로 파일을 읽을
    필요가 없기 때문 — `game_council._chair`가 의견 텍스트만 보고 종합하는 것과 같은 조건이다.
    """
    if lens == "우선순위":
        try:
            local = ollama(
                f"너는 게임 개발팀의 '우선순위' 담당이다. {BEATS[lens]}\n\n"
                f"[현재 상태]\n{situation}\n\n"
                "[출력]\n## 발견\n<3줄 이내>\n\n## 과제\nJSON 배열, 없으면 []:\n"
                '[{"title":"...","detail":"...","priority":"P1|P2|P3","track":"개발|그래픽",'
                '"verify":"통과 기준 + 네거티브 컨트롤","needs_owner":false}]',
                task="coding", json_mode=False)
            items = extract_json(local) if local else None
            if _priority_ok(items):
                print("[역할감사] 우선순위 = 로컬 모델(gemma) — 클로드 호출 생략")
                return lens, (local or "").strip(), items
            print("[역할감사] 우선순위 로컬 결과 미달 — 클로드로 승격")
        except Exception as e:
            print(f"[역할감사] 우선순위 로컬 실패({e}) — 클로드로 승격")

    prompt = (
        f"너는 '재와 별(Ashes to Stars)' 개발팀의 **{lens}** 담당이다.\n"
        f"너의 렌즈: {focus}\n\n[이번 점검 과제]\n{BEATS[lens]}\n\n"
        f"[현재 상태 — 이미 수집됨]\n{situation}\n\n"
        "[읽을 것]\n"
        f"- 기획서(최상위 권위): {_gc.DESIGN} — ✅=오너 확정, 💡=제안, ⚠️=주의, 📌=폐기\n"
        "- 스펙: docs/GAME_SPEC_*.md · 아트: docs/GAME_ART_RESOURCES.md(§0-B가 물량 권위)\n"
        "- 함정 목록: docs/GAME_DEV_HANDOFF.md §5 — 여기 적힌 사고를 다시 내지 마라\n"
        "- 코드: projects/ashes-to-stars/unity/Assets/\n"
        "- 현재 지시서: output/qa/ashes-to-stars/ORDERS.md\n"
        + _gc.GROUND_RULES +
        "\n[출력]\n"
        "## 발견\n<3줄 이내. 각 줄 끝에 `파일:줄` 또는 '미확인'>\n\n"
        "## 과제\n"
        "마지막에 JSON 배열. 새로 만들 가치가 있는 것만, 없으면 빈 배열 `[]`:\n"
        '[{"title":"...","detail":"무엇을 어떻게","priority":"P1|P2|P3","track":"개발|그래픽",'
        '"verify":"통과 기준 + 네거티브 컨트롤","needs_owner":true|false}]\n'
        "이미 ORDERS.md나 백로그에 있는 것은 내지 마라(중복 적재 방지)."
    )
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=420,
                         allowed_tools="Read,Grep,Glob", permission_mode="plan")
    if not ok or not out:
        return lens, _gc.FAIL, []
    items = extract_json(out)
    return lens, out.strip(), (items if isinstance(items, list) else [])


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--roles", default="", help="쉼표 구분, 비우면 전원")
    args = ap.parse_args()

    roles = [r.strip() for r in args.roles.split(",") if r.strip()] or list(BEATS)
    unknown = [r for r in roles if r not in BEATS]
    if unknown:
        raise SystemExit(f"모르는 역할: {unknown} — 가능: {list(BEATS)}")

    with advisory_lock("game_agents") as got:
        if not got:
            print("[역할감사] 이미 진행 중 — 생략")
            return

        ts = datetime.now().strftime("%Y%m%d_%H%M")
        situation = _gc._situation()
        personas = {n: d for n, d in _gc.PERSONAS}

        with ThreadPoolExecutor(max_workers=3) as ex:
            results = [f.result() for f in
                       [ex.submit(_beat, r, personas[r], situation) for r in roles]]

        if all(rep == _gc.FAIL for _, rep, _ in results):
            # 전원 실패 = 이 점검 탓이 아니라 클로드 호출 자체가 전멸. 조용히 물러난다.
            print("[역할감사] 전원 실패(인프라) — 적재 없이 종료")
            return

        OUT_DIR.mkdir(parents=True, exist_ok=True)
        new_items = []
        for lens, report, items in results:
            if report == _gc.FAIL:
                continue
            (OUT_DIR / f"{lens}_{ts}.md").write_text(
                f"# {lens} 점검 — {ts}\n\n{report}\n", encoding="utf-8")
            for i in items:
                i["role"] = lens
            new_items += items

        if new_items:
            _gc._append_backlog(new_items, ts)

        head = "\n".join(f"- [{i.get('role')}] {i.get('title','')}" for i in new_items[:6])
        print(f"[역할감사] 완료 — 역할 {len(results)}개, 신규 과제 {len(new_items)}건")
        if new_items:
            send(f"🔍 재와 별 역할별 점검 — 신규 과제 {len(new_items)}건\n\n{head}")
        # 과제가 0건이면 알리지 않는다 — '이상 없음'을 매번 보내면 경보가 소음이 된다.


if __name__ == "__main__":
    main()
