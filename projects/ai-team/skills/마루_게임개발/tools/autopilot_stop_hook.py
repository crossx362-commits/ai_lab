#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Stop 훅 — 재와 별 개발 세션이 지시서를 다 끝내기 전에 멈추지 않게 한다.

왜: 지시문으로 "물어보지 말고 계속 진행하세요"를 세 번 보냈지만 세 번 다 세션이
자기 턴을 끝내고 멈췄다. 프롬프트로는 안 되는 것이라 하네스로 강제한다.

동작: 세션이 턴을 끝내려 할 때
  1. 이 세션이 **옵트인 목록에 있는가** — 없으면 아무것도 안 한다(그냥 멈춘다)
  2. `ORDERS.md`가 있고, 그 지시서로 아직 상한을 안 썼는가
  3. 둘 다 맞으면 `decision: block`으로 되돌려보내 다음 작업을 잇게 한다

⚠️ 안전장치 (이게 없으면 토큰을 무한히 태운다):
  - **옵트인 세션만** — 이 저장소엔 펫나 자동화·감시 세션도 돌고 있어서, 전역으로 걸면
    관계없는 세션까지 붙잡는다. `autopilot_sessions.txt`에 적힌 세션만 대상.
  - **연속 상한 MAX_CONTINUES** — 넘으면 조용히 놓아준다.
  - **지시서가 바뀌면 카운터 초기화** — 새 회의가 새 지시서를 내면 새 예산을 준다.
    (지시서가 그대로인데 계속 도는 것이 바로 막고 싶은 폭주다)
  - 상태 파일이 깨졌거나 읽기 실패하면 **놓아준다**(fail-open) — 훅이 세션을 인질로
    잡는 것이 세션이 멈추는 것보다 나쁘다.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]   # tools→마루_게임개발→skills→ai-team→projects→ai_lab
GAME_QA = ROOT / "output" / "qa" / "ashes-to-stars"
ORDERS = GAME_QA / "ORDERS.md"
OPT_IN = ROOT / ".claude" / "autopilot_sessions.txt"
STATE = GAME_QA / "autopilot_state.json"

MAX_CONTINUES = 20       # 같은 지시서로 이어붙일 수 있는 최대 턴 수


def _release() -> None:
    """세션을 그냥 멈추게 둔다."""
    sys.exit(0)


def main() -> None:
    try:
        payload = json.loads(sys.stdin.read() or "{}")
    except Exception:
        _release()

    sid = str(payload.get("session_id") or "")
    if not sid or not ORDERS.exists() or not OPT_IN.exists():
        _release()

    # 옵트인 확인. 세션 id는 도구마다 'local_' 접두가 붙었다 안 붙었다 하므로 부분일치로 본다.
    try:
        listed = [ln.strip() for ln in OPT_IN.read_text(encoding="utf-8").splitlines()
                  if ln.strip() and not ln.startswith("#")]
    except Exception:
        _release()
    if not any(sid in ln or ln in sid for ln in listed):
        _release()

    # 세션이 스스로 빠져나갈 문. 이게 없으면 상한(20턴)을 다 태우기 전엔 못 멈춘다 —
    # 지시서를 정말 다 끝낸 세션까지 붙잡는 것은 자동진행이 아니라 감금이다.
    try:
        if ORDERS.read_text(encoding="utf-8").lstrip().startswith("# 완료"):
            _release()
    except Exception:
        _release()

    st = ORDERS.stat()
    sig = f"{int(st.st_mtime)}:{st.st_size}"

    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        state = {}
    rec = state.get(sid) or {}
    count = int(rec.get("count", 0)) if rec.get("sig") == sig else 0   # 새 지시서면 예산 초기화

    if count >= MAX_CONTINUES:
        print(json.dumps({"systemMessage":
                          f"[자동진행] 같은 지시서로 {MAX_CONTINUES}턴을 썼다 — 여기서 멈춘다. "
                          "새 회의가 ORDERS.md를 갱신하면 다시 이어진다."}))
        sys.exit(0)

    state[sid] = {"sig": sig, "count": count + 1}
    try:
        STATE.parent.mkdir(parents=True, exist_ok=True)
        STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")
    except Exception:
        _release()   # 카운터를 못 쓰면 상한이 없는 것과 같다 — 그러면 붙잡지 않는다

    print(json.dumps({
        "decision": "block",
        "reason": (
            f"작업지시서(`output/qa/ashes-to-stars/ORDERS.md`)가 아직 남아 있다. "
            f"멈추지 말고 다음 항목을 이어서 진행하라. ({count + 1}/{MAX_CONTINUES}턴째)\n"
            "- 지시서를 읽고 **아직 안 끝난 첫 항목**을 집어 실행한다.\n"
            "- 각 항목의 '통과 기준·검증'을 충족해야 끝난 것이다. 코드를 넣은 것만으로는 아니다.\n"
            "- 항목을 끝내면 **자기가 고친 파일만** add해서 즉시 커밋한다.\n"
            "- 지목된 코드가 실제와 다르면(회의 결론은 가설이다) 오진 사유를 적고 그 항목을 종결한 뒤 다음으로 간다.\n"
            "- 정말 전부 끝났으면 ORDERS.md 맨 위에 `# 완료` 한 줄을 남겨라 — 그러면 이 훅이 놓아준다.\n"
            "- 오너 판단이 필요한 항목([오너 판단 필요])은 건너뛰고 다음 항목으로 간다."
        )}))


if __name__ == "__main__":
    main()
