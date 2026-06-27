#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""속보 감시 — 긴급 시장 이슈를 정기 보고와 별개로 즉시 텔레그램 보고.

웹검색(LLM grounding)으로 '지금 즉시 알릴 만한 중대 이슈'가 있는지 주기적으로
확인하고, 있으면 병합 보고를 기다리지 않고 바로 보낸다. 같은 이슈를 반복
보내지 않도록 쿨다운(기본 2시간)을 둔다.
"""

from __future__ import annotations

import argparse
import json
import sys
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
from _shared import research  # noqa: E402

load_env(str(PROJECT_ROOT))

STATE_FILE = PROJECT_ROOT / "output" / "cache" / "breaking_state.json"
COOLDOWN_SEC = 2 * 3600

QUERY = (
    "지금 한국·미국 증시에 투자자에게 즉시 알릴 만한 긴급 속보나 중대 이슈가 있는가? "
    "예: 주요 지수 급락(2% 이상)·서킷브레이커·사이드카 발동·대형 기업 악재·정책/금리 충격·"
    "지정학 리스크. 정말 중대한 건이 있으면 '[긴급] ' 으로 시작해 한두 줄로 요약하고, "
    "평범하거나 특별한 속보가 없으면 정확히 '없음' 한 단어만 출력하라."
)


def _load_state() -> dict:
    try:
        return json.loads(STATE_FILE.read_text(encoding="utf-8"))
    except Exception:
        return {}


def _save_state(d: dict) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")


def detect() -> str | None:
    r = (research.web_brief(QUERY, max_tokens=300) or "").strip()
    if not r:
        return None
    head = r.replace(" ", "")[:4]
    if head.startswith("없음") or r.strip() == "없음":
        return None
    if "긴급" not in r and "[긴급]" not in r:
        # 명시적 긴급 표식이 없으면 보수적으로 보류(오탐 방지)
        return None
    return r


def check_and_send(force: bool = False) -> bool:
    issue = detect()
    if not issue:
        print("속보 없음")
        return False
    key = issue[:40]
    state = _load_state()
    now = datetime.now()
    if not force and state.get("key") == key and state.get("ts"):
        try:
            if (now - datetime.fromisoformat(state["ts"])).total_seconds() < COOLDOWN_SEC:
                print("동일 속보 쿨다운 — 전송 생략")
                return False
        except Exception:
            pass
    msg = "🚨 [속보] " + issue.replace("[긴급]", "").strip()
    send(msg)
    _save_state({"key": key, "ts": now.isoformat()})
    print("속보 전송:", key)
    return True


def main() -> None:
    ap = argparse.ArgumentParser(description="속보 감시 (긴급 즉시 보고)")
    ap.add_argument("--send", action="store_true", help="감지 시 텔레그램 전송")
    ap.add_argument("--force", action="store_true", help="쿨다운 무시")
    args = ap.parse_args()
    if args.send:
        check_and_send(force=args.force)
    else:
        issue = detect()
        print(issue or "속보 없음")


if __name__ == "__main__":
    main()
