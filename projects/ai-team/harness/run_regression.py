#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""회귀 스위트 자동 실행 — 2026-08-08 (오너 지시 "지금까지 한거 자동화해").

왜 필요한가(실측)
-----------------
2026-08-08에 사람이 우연히 전체 스위트를 돌려보니 **307개 중 18개가 실패**하고 있었다.
언제부터 빨간지 아무도 몰랐다 — 이 저장소에는 회귀 스위트를 주기적으로 돌리는 잡이
하나도 없었기 때문이다(테오의 E2E는 브라우저 테스트라 별개다).

더 나쁜 것: 그 18개를 **단독 실행하면 전부 통과**했다. 원인은 `test_agent_controller_restart`가
프로세스 전역 `subprocess.run`을 복원 없이 덮어써서, 알파벳 순으로 뒤에 오는 모듈들이
가짜 subprocess를 받은 것이었다. 즉 **파일 단위로 돌리면 영원히 안 보이는 종류의 고장**이다.

그래서 이 잡은 반드시 `unittest discover`로 **스위트 전체를 한 프로세스에서** 돌린다.
파일별로 쪼개 돌리면 이 잡을 만든 이유 자체가 사라진다 — 그렇게 바꾸지 마라.

동작
----
· 실패가 없으면 조용하다(로그만). 오너 지시 §1-5 "경보는 못 고친 것만".
· 실패가 있으면 **먼저 자기수정을 시도한다**(`ai_team_self_heal.attempt`, 오너 지시
  2026-08-08 "문제 생기면 보고하지 않고 알아서 수정"). 격리 워크트리에서 고치고
  회귀 스위트가 100% 통과해야만, 그리고 안전망 코드(게이트·락·백로그 판별 등)를
  안 건드렸을 때만 master에 병합한다. 성공하면 텔레그램 없이 조용히 끝난다.
· 자기수정이 실패했거나 안전망을 건드려 검토가 필요하면, 실패 모듈·테스트 이름과
  자기수정 시도 결과를 텔레그램으로 보낸다.
· 종료 코드: 자기수정 후에도 남은 실패 수(0이면 정상) — launchd 로그에서 바로 구분된다.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
HARNESS_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(HARNESS_DIR))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
import ai_team_self_heal  # noqa: E402

load_env(str(PROJECT_ROOT))

TESTS_DIR = AI_TEAM_ROOT / "tests"
TIMEOUT_SEC = 900

_SUMMARY = re.compile(r"^Ran (\d+) tests? in", re.MULTILINE)
_FAILLINE = re.compile(r"^(?:FAIL|ERROR): (\S+) \(([\w.]+)\)", re.MULTILINE)


def run_suite() -> dict:
    """스위트 전체를 한 프로세스에서 돌린다(discover 고정 — 위 독스트링 참고)."""
    try:
        p = subprocess.run(
            [sys.executable, "-m", "unittest", "discover", "-s", str(TESTS_DIR), "-p", "test_*.py"],
            cwd=str(PROJECT_ROOT), capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=TIMEOUT_SEC)
    except subprocess.TimeoutExpired:
        return {"error": f"스위트가 {TIMEOUT_SEC}s 안에 안 끝남 — 무한 대기 테스트 의심"}
    out = (p.stdout or "") + (p.stderr or "")
    m = _SUMMARY.search(out)
    fails = [(mod, name) for name, mod in _FAILLINE.findall(out)]
    return {"total": int(m.group(1)) if m else 0, "ok": p.returncode == 0,
            "failures": fails, "tail": out.strip().splitlines()[-1:]}


def main() -> int:
    ap = argparse.ArgumentParser(description="회귀 스위트 자동 실행")
    ap.add_argument("--send", action="store_true", help="실패 시 텔레그램 보고")
    args = ap.parse_args()

    res = run_suite()
    stamp = f"[{datetime.now():%Y-%m-%d %H:%M}]"
    if "error" in res:
        print(f"{stamp} 🧪 회귀 스위트 실행 실패 — {res['error']}")
        if args.send:
            send(f"🧪 [하네스] 회귀 스위트 실행 실패\n\n{res['error']}")
        return 1

    if res["ok"]:
        print(f"{stamp} 🧪 회귀 스위트 {res['total']}개 전부 통과")
        return 0

    fails = res["failures"]
    by_mod: dict[str, list[str]] = {}
    for mod, name in fails:
        by_mod.setdefault(mod.split(".")[-1], []).append(name)
    print(f"{stamp} 🧪 회귀 스위트 {res['total']}개 중 {len(fails)}개 실패")
    for mod, names in sorted(by_mod.items()):
        print(f"  · {mod}: {len(names)}건 — {', '.join(names[:3])}")

    # 자기수정 시도(오너 지시 2026-08-08) — 실패를 텔레그램으로 보내기 전에 격리
    # 워크트리에서 먼저 고쳐본다. 성공하면(회귀 100% + 안전망 미접촉) 조용히 끝난다.
    summary = "\n".join(f"{mod}: {', '.join(names)}" for mod, names in sorted(by_mod.items()))
    heal = ai_team_self_heal.attempt(summary)
    if heal["healed"] and heal["committed"]:
        print(f"{stamp} 🔧 자기수정 성공 — {heal['reason']}")
        confirm = run_suite()
        if confirm.get("ok"):
            print(f"{stamp} 🧪 재확인: {confirm['total']}개 전부 통과")
            return 0
        # 매우 드문 경우(병합 직후 master에서 재확인이 다시 실패) — 조용히 넘어가지
        # 않는다. 자기수정이 "고쳤다고 착각한 채 조용히 전멸"하면 안 된다는 원칙 그대로.
        print(f"{stamp} ⚠️ 자기수정 병합했으나 master 재확인 실패 — 사람 검토 필요")
        heal["reason"] += " (master 재확인에서 다시 실패 — 검토 필요)"
    else:
        print(f"{stamp} 🔧 자기수정 미해결 — {heal['reason']}")

    if args.send:
        lines = [f"· {mod} ({len(n)}건)" for mod, n in sorted(by_mod.items())]
        send(f"🧪 [하네스] 회귀 스위트 실패 {len(fails)}건 / 전체 {res['total']}개\n\n"
             + "\n".join(lines[:10])
             + f"\n\n🔧 자기수정 시도: {heal['reason']}"
             + "\n\n단독 실행하면 통과하는데 여기서만 실패한다면 그건 그 테스트의 버그가 "
               "아니라 **앞선 테스트가 남긴 전역 상태**입니다(2026-08-08 subprocess 오염 사례).")
    return len(fails)


if __name__ == "__main__":
    sys.exit(main())
