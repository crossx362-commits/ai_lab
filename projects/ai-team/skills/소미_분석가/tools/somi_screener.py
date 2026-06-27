#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 유망 종목 발굴기 — KIS 거래량 순위 후보를 소미 점수로 채점해 상위 종목 추천."""

from __future__ import annotations

import argparse
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
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from somi_kis_reporter import KISClient, build_input_text  # noqa: E402
from short_covering_analyzer import parse_input_text, calculate_score  # noqa: E402

load_env(str(PROJECT_ROOT))

# ETF/ETN/레버리지/인버스/스팩 등 일반 종목이 아닌 상품 제외 키워드
_EXCLUDE_KEYWORDS = (
    "KODEX", "TIGER", "KBSTAR", "ARIRANG", "HANARO", "KOSEF", "SOL ", "ACE ",
    "PLUS ", "RISE ", "TIMEFOLIO", "KIWOOM", "히어로즈", "ETN", "인버스",
    "레버리지", "선물", "2X", "스팩",
)

# 유망/관찰 기준 — 소미 자체 매수판단 점수 컷을 그대로 사용
# (소미 리포트: 60점↑ '분할 관찰 가능', 40점↑ '관찰 우선', 미만 '신규 매수 보류')
GOOD_SCORE = 60   # 소미가 '분할 관찰 가능'으로 보는 유망 컷
WATCH_SCORE = 40  # 소미 '관찰 우선' (참고용)


def _is_excluded(name: str) -> bool:
    upper = (name or "").upper()
    return any(kw.upper() in upper for kw in _EXCLUDE_KEYWORDS)


def get_candidates(kis: KISClient, limit: int = 20) -> list[tuple[str, str]]:
    """KIS 거래대금 순위에서 ETF/ETN 등을 제외한 일반 종목 후보 목록.
    (거래대금 상위 = 유동성 큰 실종목 → 저가주·작전주 노이즈 최소화. 최종 유망 판정은 소미 분석이 함.)"""
    data = kis.get(
        "uapi/domestic-stock/v1/quotations/volume-rank",
        "FHPST01710000",
        {
            "FID_COND_MRKT_DIV_CODE": "J",
            "FID_COND_SCR_DIV_CODE": "20171",
            "FID_INPUT_ISCD": "0000",
            "FID_DIV_CLS_CODE": "0",
            "FID_BLNG_CLS_CODE": "3",
            "FID_TRGT_CLS_CODE": "111111111",
            "FID_TRGT_EXLS_CLS_CODE": "0000000000",
            "FID_INPUT_PRICE_1": "",
            "FID_INPUT_PRICE_2": "",
            "FID_VOL_CNT": "",
            "FID_INPUT_DATE_1": "",
        },
    )
    candidates: list[tuple[str, str]] = []
    for row in data.get("output") or []:
        code = str(row.get("mksc_shrn_iscd", "")).strip()
        name = str(row.get("hts_kor_isnm", "")).strip()
        if not code.isdigit() or not name or _is_excluded(name):
            continue
        candidates.append((code, name))
        if len(candidates) >= limit:
            break
    return candidates


def screen(kis: KISClient, candidates: list[tuple[str, str]]) -> list[dict]:
    """후보 종목을 소미 점수로 채점."""
    results = []
    for code, name in candidates:
        try:
            text = build_input_text(kis, "발굴", code, name)
            parsed = parse_input_text(text)
            score, grade, pos, neg = calculate_score(parsed)
            results.append(
                {
                    "code": code,
                    "name": name,
                    "score": score,
                    "grade": grade,
                    "change": parsed.get("change_pct", ""),
                    "pos": pos,
                    "neg": neg,
                }
            )
        except Exception as exc:
            print(f"[skip] {name}({code}): {exc}", file=sys.stderr)
        time.sleep(0.2)
    results.sort(key=lambda r: r["score"], reverse=True)
    return results


def _line(rank: int, r: dict) -> str:
    signal = r["pos"][0] if r["pos"] else (r["neg"][0] if r["neg"] else "특이 신호 없음")
    return (
        f"{rank}. {r['name']}({r['code']}) — {r['score']}점 / {r['grade']} / 등락 {r['change']}\n"
        f"   · {signal}"
    )


def format_report(results: list[dict], top_n: int = 5) -> str:
    header = f"[소미 유망종목 발굴 / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n"
    header += f"거래대금 상위 {len(results)}종목을 소미가 분석한 결과입니다.\n"
    if not results:
        return header + "\n분석 가능한 종목이 없습니다. (장 시간/데이터 확인 필요)"

    # 유망 = 소미 매수판단 '분할 관찰 가능'(60점↑). 관찰 = 40점↑.
    good = [r for r in results if r["score"] >= GOOD_SCORE]
    watch = [r for r in results if WATCH_SCORE <= r["score"] < GOOD_SCORE]

    lines = [header]
    if good:
        lines.append(f"\n✅ 유망 (소미 '분할 관찰 가능' 60점↑)")
        for i, r in enumerate(good[:top_n], start=1):
            lines.append(_line(i, r))
    else:
        best = results[0]
        lines.append(
            f"\n⚠️ 오늘 소미 기준 '유망(60점↑)' 종목은 없습니다."
            f" 최고점도 {best['name']} {best['score']}점({best['grade']})에 그칩니다."
        )

    if watch:
        lines.append(f"\n👀 관찰 (소미 '관찰 우선' 40~59점)")
        for i, r in enumerate(watch[:top_n], start=1):
            lines.append(_line(i, r))

    lines.append("\n※ 소미 점수(시장경보·지지선·급락·수급 반영) 기반 분석이며, 매수 지시가 아닙니다.")
    return "\n".join(lines)


def run(top_n: int = 5, candidate_limit: int = 20, do_send: bool = False) -> str:
    kis = KISClient()
    candidates = get_candidates(kis, candidate_limit)
    results = screen(kis, candidates)
    report = format_report(results, top_n)
    if do_send:
        send(report)
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description="소미 유망종목 발굴기 (거래대금 상위 → 소미 분석 판정)")
    parser.add_argument("--top", type=int, default=5, help="추천 종목 수")
    parser.add_argument("--candidates", type=int, default=20, help="분석할 거래대금 상위 후보 수")
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = parser.parse_args()
    report = run(args.top, args.candidates, args.send)
    print(report)


if __name__ == "__main__":
    main()
