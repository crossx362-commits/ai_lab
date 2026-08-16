#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — 올라마에 맡길 수 있는 일만 맡긴다.

오너 2026-08-16 「올라마에 분담 가능한일은 분담해서 해」.

로컬 무료 모델만 쓴다. `:cloud` 모델은 안 고른다(인증 403·유료 원격).
Unity 전투·게이트· consum 배선은 여기 맡기지 않는다 — 분류·카피·짧은 요약만.

    python3 loop/ollama_split.py classify
    python3 loop/ollama_split.py copy --kind hud
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(ROOT / "projects" / "ai-team"))

# e2b는 json_mode에서 빈 응답이 난다(CLAUDE.md). 분담은 12b 고정.
os.environ.setdefault("OLLAMA_MODEL", "gemma4:12b")
os.environ["AI_TEAM_ALLOW_OLLAMA_CLOUD"] = "false"

from _shared.llm import ollama  # noqa: E402


def _read(rel: str) -> str:
    p = ROOT / rel
    try:
        return p.read_text(encoding="utf-8")
    except OSError:
        return ""


def classify() -> dict:
    """큐·INBOX를 읽고 누가 집을지 가른다. 가설이 아니라 올라마 JSON + 코드 규칙."""
    status = _read("docs/STATUS.md")[:4000]
    inbox = _read("docs/feedback/INBOX.md")[:2500]
    prompt = (
        "재와 별 개발 분담. JSON만 출력.\n"
        "규칙: ollama=카피·분류·짧은 요약. grok=유니티 코드·게이트·전투. "
        "skip=V4 70% 외부테스터·경매 서버·침략 본게임·가챠.\n"
        f"STATUS:\n{status}\n---\nINBOX 앞부분:\n{inbox}\n"
        '형식: {"next":"제목","owner":"ollama|grok|skip","why":"한 줄"}'
    )
    raw = ollama(prompt, system="JSON only.", json_mode=True, task="blog", max_tokens=400)
    data = {"next": "", "owner": "grok", "why": "올라마 응답 없음 — 코드 트랙"}
    if raw:
        try:
            parsed = json.loads(raw)
            if isinstance(parsed, dict):
                data.update({k: parsed[k] for k in ("next", "owner", "why") if k in parsed})
        except json.JSONDecodeError:
            data["why"] = "올라마 JSON 파싱 실패 — 코드 트랙"
    owner = str(data.get("owner") or "grok").lower()
    if owner not in ("ollama", "grok", "skip"):
        owner = "grok"
    # 코드가 한 번 더 막는다 — 올라마가 skip을 grok으로 뒤집지 못하게.
    blob = f"{data.get('next','')} {data.get('why','')}"
    if any(k in blob for k in ("외부 테스터", "V4 70%", "경매장", "침략 본게임", "가챠")):
        owner = "skip"
    data["owner"] = owner
    data["model"] = os.environ.get("OLLAMA_MODEL", "")
    return data


def copy(kind: str) -> dict:
    """짧은 UI 문구. 코드에 넣을지 여부는 호출자가 소비처를 보고 정한다."""
    hints = {
        "hud": "전투 하단 초상 옆 스킬. 호버 이름 한 줄. 한국어 짧게.",
        "smith": "대장간 제작·강화 줄. 실패해도 장비는 남는다는 §11. 한 줄씩.",
        "escape": "전투 귀환 6초 시전. 피격 시 취소. 두루마리는 끝난 뒤에만 소모.",
    }
    hint = hints.get(kind, "게임 UI 짧은 한국어 문구 3개")
    prompt = (
        f"{hint}\nJSON만: {{\"lines\":[\"...\",\"...\",\"...\"]}} 각 줄 28자 이하."
    )
    raw = ollama(prompt, system="JSON only. Korean.", json_mode=True, task="blog", max_tokens=400)
    lines = []
    if raw:
        try:
            parsed = json.loads(raw)
            if isinstance(parsed, dict):
                lines = [str(x)[:40] for x in (parsed.get("lines") or []) if x]
        except json.JSONDecodeError:
            pass
    return {"kind": kind, "lines": lines[:6], "model": os.environ.get("OLLAMA_MODEL", "")}


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("classify")
    p = sub.add_parser("copy")
    p.add_argument("--kind", default="hud")
    args = ap.parse_args()
    if args.cmd == "classify":
        out = classify()
    else:
        out = copy(args.kind)
    print(json.dumps(out, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
