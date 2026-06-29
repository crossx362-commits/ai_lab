#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""행크 — 미국 시장 조사관.

미국 지수(S&P·나스닥·VIX)와 USD 강도(주요 통화 대비)를 수집해
output/research/region_us.json 에 저장한다. FRED 등 거시 키 미보유 시
지수/환율 위주로 수집하고, 키가 추가되면 거시 지표를 확장한다.
"""

from __future__ import annotations

import argparse
import os
import sys
import time
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
from _shared import growth  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

INDEX_SYMBOLS = {"S&P500": "^GSPC", "나스닥": "^IXIC", "VIX": "^VIX"}


def collect() -> dict:
    macro = {}
    if os.getenv("FRED_API_KEY", "").strip():
        macro = {
            "미국채10년": research.fred_latest("DGS10"),
            "CPI": research.fred_latest("CPIAUCSL"),
            "연방기금금리": research.fred_latest("FEDFUNDS"),
        }
    payload = {
        "indices": research.indices(INDEX_SYMBOLS),
        "fx": research.fx("EUR", "JPY", "KRW"),  # USD 강도 가늠
        "macro": macro,
        "web_issues": research.web_brief(
            "어제 미국 증시 S&P500·나스닥·다우 종가와 등락률, VIX, 주요 이슈를 "
            "3줄 이내로 요약하라. 한국 증시에 영향 줄 만한 포인트 중심."
        ),
        "note": "FRED 거시는 FRED_API_KEY 보유 시 자동 수집",
    }
    research.save_region("us", payload)
    return payload


def _macro_line(p: dict) -> str:
    m = p.get("macro") or {}
    bits = [f"{k} {v}" for k, v in m.items() if v]
    return " · ".join(bits)


def brief_text(p: dict) -> str:
    lines = ["🇺🇸 미국 시장 브리프"]
    idx = [f"{k} {v['close']}" for k, v in (p.get("indices") or {}).items() if v]
    lines.append("📈 " + " / ".join(idx) if idx else "📈 지수 조회 실패(소스 일시 장애)")
    fx = p.get("fx", {})
    if fx.get("KRW"):
        lines.append(f"💱 USD/KRW {fx['KRW']:.1f}")
    ml = _macro_line(p)
    if ml:
        lines.append("🏦 " + ml)
    web = p.get("web_issues")
    if web:
        lines.append("\n🔎 웹 이슈\n" + web)
    return "\n".join(lines)


def main() -> None:
    ap = argparse.ArgumentParser(description="행크 미국 시장 조사")
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--print", action="store_true")
    ap.add_argument("--daemon", action="store_true", help="정기 실행 데몬 모드 (매 30분)")
    args = ap.parse_args()

    if args.daemon:
        with ProcessLock("hank_us_research"):
            print(f"[{datetime.now()}] 행크 데몬 시작 (30분 주기)")
            while True:
                try:
                    payload = collect()
                    txt = brief_text(payload)
                    send(txt)
                    print(f"[{datetime.now()}] 미국 브리프 전송 완료")
                    growth.record("hank_us", role="미국 시장조사", data="지수·환율·금리·웹이슈",
                                  judgment="브리프 작성", result="전송", good="4시간 주기",
                                  bad="한국 영향 연결 강화 여지",
                                  scores={"fit": 19, "evidence": 18, "efficiency": 18, "risk": 16, "brevity": 8})
                except Exception as e:
                    send(f"⚠️ 행크 오류: {e}")
                    print(f"[{datetime.now()}] 오류: {e}")
                time.sleep(int(os.getenv("RESEARCH_INTERVAL_SEC", "14400")))  # 기본 4시간(과다 알림 완화)
        return

    payload = collect()
    txt = brief_text(payload)
    if args.print or not args.send:
        print(txt)
    if args.send:
        send(txt)


if __name__ == "__main__":
    main()
