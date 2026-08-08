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
· 실패가 있으면 실패 모듈·테스트 이름과 첫 단서를 텔레그램으로 보낸다.
  예원이 자동으로 고칠 수 없는 종류라(코드 판단) 사람에게 알리는 것이 맞다.
· 종료 코드: 실패 수(0이면 정상) — launchd 로그에서 바로 구분된다.
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
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402

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

    if args.send:
        lines = [f"· {mod} ({len(n)}건)" for mod, n in sorted(by_mod.items())]
        send(f"🧪 [하네스] 회귀 스위트 실패 {len(fails)}건 / 전체 {res['total']}개\n\n"
             + "\n".join(lines[:10])
             + "\n\n단독 실행하면 통과하는데 여기서만 실패한다면 그건 그 테스트의 버그가 "
               "아니라 **앞선 테스트가 남긴 전역 상태**입니다(2026-08-08 subprocess 오염 사례).")
    return len(fails)


if __name__ == "__main__":
    sys.exit(main())
