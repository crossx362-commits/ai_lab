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
import os
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
    "평범하거나 특별한 속보가 없으면 정확히 '없음' 한 단어만 출력하라. "
    "단, 오늘이 아니라 어제·그 이전에 발생한 사건은 긴급이 아니다 — '없음'을 출력하라. "
    "데이터를 확인할 수 없거나 판단이 어려우면 추측하지 말고 '없음'을 출력하라."
)


_DROP_WORDS = ("급락", "폭락", "하락", "붕괴", "패닉", "크래시", "서킷", "사이드카", "곤두박질", "추락")
# 오탐 차단은 '실제 검증 가능한' 한국 지수(코스피/코스닥) 주장에만 적용.
# (나스닥·다우 등 해외 지수는 여기서 시세를 안 보므로 차단 트리거에서 제외 — 과차단 방지)
_INDEX_WORDS = ("코스피", "코스닥", "kospi", "kosdaq")
_FALSE_DROP_FLOOR = float(os.getenv("BREAKING_DROP_FLOOR", "-1.5"))  # 이보다 덜 빠지면 '지수급락' 오탐


def _index_reality() -> dict:
    """실제 지수 등락(코스피=KODEX200, 코스닥=KODEX코스닥150 대용). 검증·표기용."""
    out = {}
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from somi_kis_reporter import KISClient, num
        kis = KISClient()
        for label, code in (("코스피", "069500"), ("코스닥", "229200")):
            try:
                out[label] = num(kis.quote(code).get("prdy_ctrt"))
            except Exception:
                pass
    except Exception:
        pass
    return out


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
        print("⚠️ 웹검색 응답 없음 — '속보 없음'이 아니라 판단 불가(LLM/검색 실패 의심)")
        return None
    head = r.replace(" ", "")[:4]
    if head.startswith("없음") or r.strip() == "없음":
        return None
    if not r.startswith("[긴급]"):
        # '[긴급]'으로 시작하지 않으면 보류 — 본문 중간의 '긴급' 단어에 낚여
        # 잡문("실시간 데이터를 확인할 수 없습니다" 등)을 전송한 사고(2026-07-07 14:00) 방지
        return None
    return r


def check_and_send(force: bool = False) -> bool:
    issue = detect()
    if not issue:
        print("속보 없음")
        return False

    # 실제 지수 대조 — LLM 속보가 '지수 급락'을 주장하는데 실제론 안 빠졌으면 오탐 차단
    reality = _index_reality()
    low = issue.lower()
    claims_drop = any(w in issue for w in _DROP_WORDS) and any(w in low for w in _INDEX_WORDS)
    if claims_drop and reality:
        worst = min(reality.values())  # 가장 많이 빠진 지수
        if worst > _FALSE_DROP_FLOOR:
            snap = " · ".join(f"{k} {v:+.2f}%" for k, v in reality.items())
            print(f"⚠️ 오탐 차단 — 속보는 지수하락 주장이나 실제 {snap}")
            return False

    # 중복 억제: LLM이 매번 문구를 바꿔 써 '앞 40자' 키가 무력화 → 하루 3연발 사고(2026-07-08).
    # 지수급락류는 '날짜+카테고리'로 하루 1회, 그 외는 기존 앞 40자+쿨다운.
    now = datetime.now()
    if claims_drop:
        key = now.strftime("%Y-%m-%d") + ":지수급락"
    else:
        key = issue[:40]
    state = _load_state()
    if not force and state.get("key") == key and state.get("ts"):
        try:
            same_day = state["ts"][:10] == now.strftime("%Y-%m-%d")
            in_cooldown = (now - datetime.fromisoformat(state["ts"])).total_seconds() < COOLDOWN_SEC
            if (claims_drop and same_day) or in_cooldown:
                print("동일 속보 중복 — 전송 생략")
                return False
        except Exception:
            pass
    msg = "🚨 [속보] " + issue.replace("[긴급]", "").strip()
    if reality:  # 실제 지수 등락을 함께 표기 — 사용자가 사실 확인 가능
        msg += "\n\n📈 실제 지수: " + " · ".join(f"{k} {v:+.2f}%" for k, v in reality.items())
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
