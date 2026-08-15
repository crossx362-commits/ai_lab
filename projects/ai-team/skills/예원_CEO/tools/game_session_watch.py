#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 재와 별 개발 세션 감시.

오너 지시(2026-08-15): "예원이가 여기 세션도 관리해".

왜 필요한가: 세션 감시를 감시 세션의 크론으로 돌리고 있었는데, 그건 **그 세션이 죽으면
같이 사라진다**. 실제로 2026-08-14 밤에 개발 세션이 프로세스 종료로 죽고 그래픽 세션은
지시를 못 읽은 채 11시간 반 방치됐고, 아무도 그걸 알리지 않았다. 예원은 launchd 상주라
세션 수명과 무관하게 산다.

⚠️ **예원은 멈춘 클로드 세션을 깨울 수 없다.** 크로스 세션 메시지는 돌고 있는 세션에만
전달되고, 유휴 세션을 기동시키는 프로그램적 수단이 없다(2026-08-15 실측). 그래서 이
도구는 **감지하고 알리는 것까지**만 한다 — 못 하는 일을 하는 척하지 않는다.

점검 3종:
1. **정체** — 세션 기록·게임 파일·커밋이 모두 STALL_MIN 넘게 안 움직였는가
2. **생성물 표류** — `art/out_ai/`는 새것인데 `Resources/props/`가 낡았는가
   (2026-08-15 실측: 오전 생성물 39장이 게임에 하나도 안 들어가 있었다)
3. **측정 공회전** — W3 플레이어가 도는데 결과 CSV가 오래 안 갱신되는가

경보는 **문제가 있을 때만** 보낸다(§ '이상 없음'을 매번 보내면 경보가 소음이 된다).
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
sys.path.insert(0, str(PROJECT_ROOT / "projects" / "ai-team"))

from _shared.env import load_env   # noqa: E402
from _shared.telegram import send  # noqa: E402

load_env(str(PROJECT_ROOT))

GAME = PROJECT_ROOT / "projects" / "ashes-to-stars"
TRANSCRIPTS = Path.home() / ".claude" / "projects" / "-Users-junholee-ai-lab"
REGISTRY = PROJECT_ROOT / ".claude" / "game_sessions.txt"   # 감시 전용 — 훅 옵트인 파일과 분리(2026-08-15)
STATE = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars" / "session_watch.json"

STALL_MIN = 12      # 이만큼 아무것도 안 움직이면 정체로 본다
# 25분이었는데 2026-08-15에 두 세션이 턴 하나에서 매달린 채 26분간 아무것도 안 했는데도
# 임계 미달로 조용했다. 사람이 "멈춘 것 같다"고 먼저 알아챈 뒤에야 확인한 셈 — 감시가
# 사람보다 늦으면 감시가 아니다. 세션이 정상 작업 중이면 12분 안에 뭐라도 쓴다(측정
# 백그라운드 실행 중에도 결과 파일·커밋이 움직인다).
MEAS_STALL_MIN = 15  # 측정이 도는데 결과가 이만큼 안 나오면 공회전 의심
REALERT_H = 3       # 같은 경보를 이 시간 안에 다시 보내지 않는다


def _age_min(p: Path) -> float:
    """파일이 마지막으로 바뀐 뒤 흐른 분. 없으면 아주 큰 값."""
    try:
        return (time.time() - p.stat().st_mtime) / 60
    except Exception:
        return 1e9


def _newest(paths) -> float:
    ages = [_age_min(p) for p in paths]
    return min(ages) if ages else 1e9


def _sh(cmd: str) -> str:
    try:
        return subprocess.run(cmd, shell=True, cwd=PROJECT_ROOT, capture_output=True,
                              text=True, timeout=20, encoding="utf-8").stdout.strip()
    except Exception:
        return ""


def _sessions() -> list[str]:
    try:
        return [ln.strip() for ln in REGISTRY.read_text(encoding="utf-8").splitlines()
                if ln.strip() and not ln.startswith("#")]
    except Exception:
        return []


def _should_alert(key: str) -> bool:
    """같은 사유를 REALERT_H 안에 반복 발송하지 않는다."""
    try:
        st = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        st = {}
    last = st.get(key, 0)
    if time.time() - last < REALERT_H * 3600:
        return False
    st[key] = time.time()
    try:
        STATE.parent.mkdir(parents=True, exist_ok=True)
        STATE.write_text(json.dumps(st), encoding="utf-8")
    except Exception:
        pass   # 기록 실패해도 경보 자체는 보낸다 — 침묵보다 중복이 낫다
    return True


def check() -> list[str]:
    issues: list[str] = []

    # ── 1. 세션 정체
    sids = _sessions()
    if not sids:
        issues.append("세션 등록 목록이 비어 있다 — .claude/game_sessions.txt 확인")
    else:
        # ⚠️ **세션마다 따로 본다.** 예전엔 대화·게임파일·커밋을 min()으로 뭉쳐서 봤는데,
        #    그러면 **감시 대상이 아닌 활동이 정체를 가린다** — 2026-08-15에 두 세션이
        #    26분째 매달려 있는데도 감시 세션이 방금 만든 커밋(8분 전) 때문에 "이상 없음"이
        #    나왔다. 커밋·게임파일은 누가 만들었는지 구분되지 않으므로 **정체 판정의 근거로
        #    쓰지 않는다.** 그 세션이 실제로 말을 했는지(자기 기록 파일)만이 그 세션의 생존 신호다.
        for sid in sids:
            age = _age_min(TRANSCRIPTS / f"{sid}.jsonl")
            if age > STALL_MIN:
                issues.append(
                    f"세션 정체 — `{sid[:8]}`가 {age:.0f}분째 기록 없음. "
                    f"턴이 매달렸을 수 있다(그 창에서 Esc로 취소해야 대기 메시지가 처리된다). "
                    f"예원은 멈춘 세션을 깨울 수 없다")

    # ── 2. 생성물 표류: 만들었는데 게임에 안 넣었다
    made = _newest(list((GAME / "art" / "out_ai").glob("*.png")))
    applied = _newest(list((GAME / "unity" / "Assets" / "Resources" / "props").glob("*.png")))
    if made < 1e9 and applied - made > 60:
        issues.append(f"생성물 표류 — art/out_ai는 {made:.0f}분 전 갱신인데 "
                      f"Resources/props는 {applied:.0f}분 전이다. 만든 게 게임에 안 들어갔다")

    # ── 3. 측정 공회전: 플레이어는 도는데 결과가 안 나온다
    if _sh("ps ax | grep -c '[W]3.app'") not in ("", "0"):
        res = _newest(list((GAME / "results").glob("w3_*")))
        if res > MEAS_STALL_MIN:
            issues.append(f"측정 공회전 의심 — W3 플레이어는 도는데 결과가 {res:.0f}분째 무갱신")

    return issues


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--send", action="store_true")
    args = ap.parse_args()

    issues = check()
    if not issues:
        print("[세션감시] 이상 없음")
        return

    for i in issues:
        print(f"[세션감시] ⚠️ {i}")
    if args.send:
        fresh = [i for i in issues if _should_alert(i[:24])]
        if fresh:
            send("🎮 재와 별 세션 감시\n\n" + "\n\n".join(f"⚠️ {i}" for i in fresh))


if __name__ == "__main__":
    main()
