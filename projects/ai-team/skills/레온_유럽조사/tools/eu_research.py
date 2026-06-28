#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""레온 — 유럽 시장 조사관.

유럽 지수(DAX·유로스톡스)와 EUR/GBP 환율을 수집해
output/research/region_eu.json 에 저장한다. ECB 거시는 키/소스 추가 시 확장.
"""

from __future__ import annotations

import argparse
import sys
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

INDEX_SYMBOLS = {"독일DAX": "^GDAXI", "영국FTSE": "^FTSE", "프랑스CAC": "^FCHI", "러시아MOEX": "IMOEX.ME", "오스트리아ATX": "^ATX"}


def collect() -> dict:
    payload = {
        "indices": research.indices(INDEX_SYMBOLS),
        "fx": research.fx("EUR", "GBP", "RUB"),
        "web_issues": research.web_brief(
            "오늘 유럽 증시 주요 뉴스를 국가별로 정리하라: 영국·프랑스·독일·러시아·오스트리아. "
            "각 국가 1~2줄, 지수·핵심 이슈 위주로 간결히. ECB·영란은행(BoE) 정책도 포함."
        ),
        "note": "ECB 정책·유럽 거시는 키/소스 추가 시 확장 예정",
    }
    research.save_region("eu", payload)
    return payload


def brief_text(p: dict) -> str:
    lines = ["🇪🇺 유럽 시장 브리프"]
    idx = [f"{k} {v['close']}" for k, v in (p.get("indices") or {}).items() if v]
    lines.append("📈 " + " / ".join(idx) if idx else "📈 지수 조회 실패(소스 일시 장애)")
    fx = p.get("fx", {})
    if fx.get("EUR"):
        lines.append(f"💱 EUR/USD {1/fx['EUR']:.4f}" if fx["EUR"] else "")
    web = p.get("web_issues")
    if web:
        lines.append("🔎 웹 이슈\n" + web)
    return "\n".join([ln for ln in lines if ln])


def main() -> None:
    ap = argparse.ArgumentParser(description="레온 유럽 시장 조사")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--print", action="store_true")
    args = ap.parse_args()
    payload = collect()
    txt = brief_text(payload)
    if args.print or not args.send:
        print(txt)
    if args.send:
        send(txt)


if __name__ == "__main__":
    main()
