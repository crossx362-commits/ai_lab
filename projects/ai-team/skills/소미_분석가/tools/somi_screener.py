#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 유망 종목 발굴기 — KIS 거래량 순위 후보를 소미 점수로 채점해 상위 종목 추천."""

from __future__ import annotations

import argparse
import json
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
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared import growth  # noqa: E402
from somi_kis_reporter import KISClient, build_input_text  # noqa: E402
from short_covering_analyzer import parse_input_text, calculate_score  # noqa: E402

load_env(str(PROJECT_ROOT))

# ETF/ETN/레버리지/인버스/스팩 등 일반 종목이 아닌 상품 제외 키워드
_EXCLUDE_KEYWORDS = (
    "KODEX", "TIGER", "KBSTAR", "ARIRANG", "HANARO", "KOSEF", "SOL ", "ACE ",
    "PLUS ", "RISE ", "TIMEFOLIO", "KIWOOM", "히어로즈", "ETN", "인버스",
    "레버리지", "선물", "2X", "스팩", "액티브", "플러스액티브",
)

# 유망/관찰 기준 — 소미 자체 매수판단 점수 컷을 그대로 사용
# (소미 리포트: 60점↑ '분할 관찰 가능', 40점↑ '관찰 우선', 미만 '신규 매수 보류')
GOOD_SCORE = 60   # 소미가 '분할 관찰 가능'으로 보는 유망 컷
WATCH_SCORE = 40  # 소미 '관찰 우선' (참고용)


def _is_excluded(name: str) -> bool:
    upper = (name or "").upper()
    return any(kw.upper() in upper for kw in _EXCLUDE_KEYWORDS)


# KIS volume-rank 발굴 축(FID_BLNG_CLS_CODE): 대형주 쏠림 방지를 위해 여러 축을 섞는다.
#   3=거래금액순(대형·유동성), 1=거래증가율(급증·신선), 0=평균거래량, 2=평균거래회전율(중소형 회전)
_RANK_AXES = ("3", "1", "2")


def _rank_candidates(kis: KISClient, blng_cls: str, limit: int) -> list[tuple[str, str]]:
    """단일 순위축(FID_BLNG_CLS_CODE)으로 후보 조회. ETF/ETN 등 제외."""
    data = kis.get(
        "uapi/domestic-stock/v1/quotations/volume-rank",
        "FHPST01710000",
        {
            "FID_COND_MRKT_DIV_CODE": "J",
            "FID_COND_SCR_DIV_CODE": "20171",
            "FID_INPUT_ISCD": "0000",
            "FID_DIV_CLS_CODE": "0",
            "FID_BLNG_CLS_CODE": blng_cls,
            "FID_TRGT_CLS_CODE": "111111111",
            "FID_TRGT_EXLS_CLS_CODE": "0000000000",
            "FID_INPUT_PRICE_1": "",
            "FID_INPUT_PRICE_2": "",
            "FID_VOL_CNT": "",
            "FID_INPUT_DATE_1": "",
        },
    )
    out: list[tuple[str, str, float]] = []
    for row in data.get("output") or []:
        code = str(row.get("mksc_shrn_iscd", "")).strip()
        name = str(row.get("hts_kor_isnm", "")).strip()
        if not code.isdigit() or not name or _is_excluded(name):
            continue
        try:
            chg = float(row.get("prdy_ctrt") or 0)
        except ValueError:
            chg = 0.0
        out.append((code, name, chg))
        if len(out) >= limit:
            break
    return out


def _rank_fluctuation(kis: KISClient, limit: int) -> list[tuple[str, str]]:
    """등락률 상위(상승률순) 강세주 축 — 오늘 오르는 리더를 발굴. 하락장에서도 VWAP 위·
    상승 모멘텀 종목을 후보에 넣어 진입 품질을 확보한다(거래량 축은 급락 패닉주도 섞임).
    과열 블로우오프(기본 ≥15%)는 고점권·손익비 열위라 추격금지, 미미한 상승(<2%)도 제외 —
    완만한 상승 리더(진입 여유 있는 자리)만. 밴드는 SOMI_FLUCT_MIN/MAX로 조정."""
    lo = float(os.getenv("SOMI_FLUCT_MIN", "2"))
    hi = float(os.getenv("SOMI_FLUCT_MAX", "15"))
    try:
        data = kis.get(
            "uapi/domestic-stock/v1/ranking/fluctuation", "FHPST01700000",
            {
                "fid_cond_mrkt_div_code": "J", "fid_cond_scr_div_code": "20170",
                "fid_input_iscd": "0000", "fid_rank_sort_cls_code": "0",
                "fid_input_cnt_1": "0", "fid_prc_cls_code": "0",
                "fid_input_price_1": "", "fid_input_price_2": "", "fid_vol_cnt": "",
                "fid_trgt_cls_code": "0", "fid_trgt_exls_cls_code": "0",
                "fid_div_cls_code": "0", "fid_rsfl_rate1": "", "fid_rsfl_rate2": "",
            },
        )
    except Exception as exc:
        print(f"[screener] 등락률 순위 조회 실패: {exc}", file=sys.stderr)
        return []
    out: list[tuple[str, str]] = []
    for row in data.get("output") or []:
        code = str(row.get("stck_shrn_iscd", "")).strip()
        name = str(row.get("hts_kor_isnm", "")).strip()
        try:
            chg = float(row.get("prdy_ctrt") or 0)
        except ValueError:
            chg = 0.0
        if not code.isdigit() or not name or _is_excluded(name) or not (lo <= chg < hi):
            continue
        out.append((code, name))
        if len(out) >= limit:
            break
    return out


# 유니버스 캐시 — 순위는 수분 내 안정. 3축 API 반복 호출을 TTL 동안 재사용(스크리너+advisor 15분 슬롯 부하 절감).
_UNIVERSE_CACHE = PROJECT_ROOT / "output" / "cache" / "somi_universe.json"
_UNIVERSE_TTL = int(os.getenv("SOMI_UNIVERSE_TTL", "300"))   # 초
_UNIVERSE_DEPTH = 40   # 축별 조회 깊이(넉넉히 한 번 받아 캐시 후 limit로 슬라이스 — 추가 API 호출 아님)


def get_candidates(kis: KISClient, limit: int = 20) -> list[tuple[str, str]]:
    """여러 순위축(거래금액·거래증가율·회전율)을 라운드로빈 병합한 후보 목록.
    대형주 한 축만 보면 매번 같은 초대형주만 나오므로, 축을 섞어 신선한 발굴을 확보한다.
    라운드로빈 순서는 결정적이라 넉넉히 캐시(TTL) 후 [:limit] 슬라이스해도 동일 결과.
    (ETF/작전주는 _is_excluded + 소미 점수 게이트가 걸러낸다. 최종 유망 판정은 소미 분석이 함.)"""
    try:
        if _UNIVERSE_CACHE.exists():
            c = json.loads(_UNIVERSE_CACHE.read_text(encoding="utf-8"))
            if time.time() - c.get("ts", 0) < _UNIVERSE_TTL and c.get("items"):
                return [tuple(x) for x in c["items"]][:limit]
    except Exception:
        pass
    lo = float(os.getenv("SOMI_FLUCT_MIN", "2"))
    hi = float(os.getenv("SOMI_FLUCT_MAX", "15"))
    vol_pools = [_rank_candidates(kis, ax, _UNIVERSE_DEPTH) for ax in _RANK_AXES]  # (code,name,chg)
    # 완만 상승주(진입 여유 있는 좋은 자리)를 거래량 축에서도 추출 — 등락률 순위(급등주)가 얇은 날 보강.
    rising_raw: list[tuple[str, str, float]] = []
    rseen: set[str] = set()
    for pool in vol_pools:
        for code, name, chg in pool:
            if lo <= chg < hi and code not in rseen:
                rseen.add(code)
                rising_raw.append((code, name, chg))
    rising = [(c, n) for c, n, _ in sorted(rising_raw, key=lambda x: x[2], reverse=True)]
    # 강세(상승률 순위)·완만 상승주를 맨 앞에 둬 우선 노출 — 하락장에서도 좋은 진입자리부터 발굴.
    pools = [_rank_fluctuation(kis, _UNIVERSE_DEPTH), rising]
    pools += [[(c, n) for c, n, _ in pool] for pool in vol_pools]  # (code,name)로 정규화
    merged: list[tuple[str, str]] = []
    seen: set[str] = set()
    for i in range(_UNIVERSE_DEPTH):                # 라운드로빈: 축별 i번째를 번갈아 뽑아 편향 최소화
        for pool in pools:
            if i < len(pool):
                code, name = pool[i]
                if code not in seen:
                    seen.add(code)
                    merged.append((code, name))
    try:
        _UNIVERSE_CACHE.write_text(
            json.dumps({"ts": time.time(), "items": merged}, ensure_ascii=False), encoding="utf-8")
    except Exception:
        pass
    return merged[:limit]


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
    """헌장 [유망종목 발굴] 형식 — 1~순위 / 제외 종목·이유 / 매수제안 전달 (최대 5)."""
    ts = datetime.now().strftime('%Y-%m-%d %H:%M')
    if not results:
        return f"[유망종목 발굴] {ts}\n- 분석 가능한 종목 없음 (장 시간/데이터 확인 필요)"
    ranked = results[:top_n]
    good = [r for r in results if r["score"] >= GOOD_SCORE]
    labels = ["1순위", "2순위", "3순위", "4순위", "5순위"]
    lines = [f"[유망종목 발굴] {ts}"]
    for i, r in enumerate(ranked):
        sig = r["pos"][0] if r["pos"] else (r["neg"][0] if r["neg"] else "특이신호 없음")
        lines.append(f"- {labels[i]}: {r['name']}({r['code']}) {r['score']}점/{r['grade']} — {sig}")
    excluded = results[top_n:top_n + 2]
    if excluded:
        ex = ", ".join(f"{r['name']}({r['score']}점)" for r in excluded)
        lines.append(f"- 제외한 종목과 이유: {ex} 등 — 점수·신호 부족")
    else:
        lines.append("- 제외한 종목과 이유: 해당 없음")
    if good:
        lines.append(f"- 매수 제안 에이전트로 넘길 종목: {', '.join(r['name'] for r in good[:top_n])} (60점↑)")
    else:
        best = results[0]
        lines.append(f"- 매수 제안 전달: 없음 (최고 {best['name']} {best['score']}점, 60점 미달)")
    return "\n".join(lines)


def run(top_n: int = 5, candidate_limit: int = 20, do_send: bool = False) -> str:
    kis = KISClient()
    candidates = get_candidates(kis, candidate_limit)
    results = screen(kis, candidates)
    report = format_report(results, top_n)
    # 발굴 종목 자동 관심등록 — 유망(GOOD_SCORE↑)만, 가격감시/정기보고가 즉시 추적
    try:
        from watchlist_manager import auto_register
        added = auto_register(results[:top_n], min_score=GOOD_SCORE)
        if added:
            report += f"\n- 관심종목 자동 등록: {', '.join(added)}"
    except Exception as exc:
        print(f"[watchlist] 자동 등록 실패: {exc}", file=sys.stderr)
    if do_send:
        send(report)
    strong = [r for r in results if r.get("score", 0) >= 60]
    growth.record(
        "somi_screener", role="유망종목 발굴",
        data=f"후보 {len(candidates)} 채점", judgment=f"유망(60+) {len(strong)}",
        result=f"상위 {min(top_n, len(results))} 보고",
        good="거래대금 상위 풀 채점", bad=("유망 0 — 관심권만" if not strong else ""),
        scores={"fit": 21, "evidence": 19, "efficiency": 18, "risk": 17, "brevity": 9},
    )
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description="소미 유망종목 발굴기 (거래대금 상위 → 소미 분석 판정)")
    parser.add_argument("--top", type=int, default=5, help="추천 종목 수")
    parser.add_argument("--candidates", type=int, default=20, help="분석할 거래대금 상위 후보 수")
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    parser.add_argument("--daemon", action="store_true", help="정기 자동 발굴 데몬 (평일 지정 시각 전송)")
    parser.add_argument("--times", default="09:30,15:50", help="HH:MM,HH:MM 발굴 전송 시각")
    args = parser.parse_args()

    if args.daemon:
        import time as _t
        from _shared.process import ProcessLock
        slots = [s.strip() for s in args.times.split(",") if s.strip()]
        last_fired: dict[str, str] = {}  # 시각슬롯 -> 마지막 전송일(중복 전송 방지)
        with ProcessLock("somi_screener"):
            print(f"[{datetime.now()}] 소미 발굴 데몬 시작 (평일 {','.join(slots)})")
            while True:
                now = datetime.now()
                hm, today = now.strftime("%H:%M"), now.strftime("%Y-%m-%d")
                if now.weekday() < 5 and hm in slots and last_fired.get(hm) != today:
                    try:
                        run(args.top, args.candidates, do_send=True)
                        last_fired[hm] = today
                        print(f"[{now}] 발굴 전송 완료 ({hm})")
                    except Exception as e:
                        send(f"⚠️ 소미 발굴 오류: {e}")
                        print(f"[{now}] 오류: {e}")
                _t.sleep(20)
        return

    report = run(args.top, args.candidates, args.send)
    print(report)


if __name__ == "__main__":
    main()
