#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 반자동 매매 보조 — 발굴/점수화 → 매수 제안(진입·손절·목표·이유·위험).

원칙: 자동매수 절대 금지. 제안만 하고, 사용자가 텔레그램에서 승인해야만 매수 실행.
승인/매수/포지션 기록은 영숙 봇(telegram_receiver) + kis_trader가 담당.
"""

from __future__ import annotations

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
from somi_kis_reporter import KISClient, build_input_text  # noqa: E402
from short_covering_analyzer import parse_input_text, calculate_score, to_num  # noqa: E402
from somi_screener import get_candidates, GOOD_SCORE  # noqa: E402

load_env(str(PROJECT_ROOT))

PROPOSALS_FILE = PROJECT_ROOT / "output" / "cache" / "somi_proposals.json"
POSITIONS_FILE = PROJECT_ROOT / "output" / "cache" / "somi_positions.json"

STOP_PCT = 0.05    # 지지선 없을 때 기본 손절 -5%
TARGET_PCT = 0.10  # 저항선 없을 때 기본 목표 +10%


# ── 제안/포지션 저장소 ───────────────────────────────────────
def _load(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    except Exception:
        return {}


def _save(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def load_proposals() -> dict:
    return _load(PROPOSALS_FILE)


def get_proposal(key: str) -> dict | None:
    """종목명 또는 코드로 최근 제안 조회."""
    items = load_proposals().get("items", [])
    key = key.strip()
    for it in items:
        if key == it["symbol"] or key == it["name"] or key in it["name"]:
            return it
    return None


def load_positions() -> dict:
    return _load(POSITIONS_FILE)


def record_position(symbol: str, name: str, entry: float, stop: float, target: float, qty: int) -> None:
    pos = load_positions()
    pos[symbol] = {
        "name": name, "entry": entry, "stop": stop, "target": target,
        "qty": qty, "ts": datetime.now().strftime("%Y-%m-%d %H:%M"),
    }
    _save(POSITIONS_FILE, pos)


def remove_position(symbol: str) -> None:
    pos = load_positions()
    if symbol in pos:
        del pos[symbol]
        _save(POSITIONS_FILE, pos)


# ── 매수 제안 생성 ───────────────────────────────────────────
def _levels(parsed: dict) -> tuple[float, float, float]:
    """진입가/손절가/목표가 산출 (지지·저항 우선, 없으면 % 기반)."""
    entry = to_num(parsed.get("close"))
    support = to_num(parsed.get("support_line"))
    resistance = to_num(parsed.get("resistance_line"))
    stop = support if (support and support < entry) else round(entry * (1 - STOP_PCT))
    target = resistance if (resistance and resistance > entry) else round(entry * (1 + TARGET_PCT))
    return entry, stop, target


def analyze_candidate(kis: KISClient, code: str, name: str) -> dict | None:
    try:
        parsed = parse_input_text(build_input_text(kis, "제안", code, name))
    except Exception:
        return None
    score, grade, pos, neg = calculate_score(parsed)
    entry, stop, target = _levels(parsed)
    rr = (target - entry) / (entry - stop) if entry > stop else 0  # 손익비
    return {
        "symbol": code, "name": name, "score": score, "grade": grade,
        "change": parsed.get("change_pct", ""),
        "entry": entry, "stop": stop, "target": target, "rr": round(rr, 2),
        "reasons": pos[:3], "risks": neg[:3] or ["뚜렷한 위험 신호 없음"],
    }


def make_proposals(candidate_limit: int = 20, min_score: int = GOOD_SCORE) -> list[dict]:
    kis = KISClient()
    proposals = []
    for code, name in get_candidates(kis, candidate_limit):
        a = analyze_candidate(kis, code, name)
        if a and a["score"] >= min_score:
            proposals.append(a)
        time.sleep(0.2)
    proposals.sort(key=lambda x: x["score"], reverse=True)
    return proposals


def _fmt(p: dict) -> str:
    won = lambda v: f"{int(v):,}원"
    return (
        f"📈 {p['name']}({p['symbol']}) — {p['score']}점/{p['grade']} (등락 {p['change']})\n"
        f"  · 진입가 {won(p['entry'])} / 손절가 {won(p['stop'])} / 목표가 {won(p['target'])} (손익비 {p['rr']})\n"
        f"  · 매수 이유: {', '.join(p['reasons']) or '점수 상위'}\n"
        f"  · 위험 요소: {', '.join(p['risks'])}"
    )


def _is_paper() -> bool:
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


# 모의 모드 1회 실행 시 자동 매수할 최대 종목 수 (예산 소진 시 자동 중단)
PAPER_AUTO_MAX = int(os.getenv("SOMI_PAPER_AUTO_MAX", "3"))
SOMI_BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))


def _auto_buy_paper(proposals: list[dict]) -> list[str]:
    """모의 모드: 상위 미보유 종목을 승인 없이 자동 매수. (실거래에서는 호출 안 함)"""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:  # 안전장치: 실거래면 자동매수 금지
        return []
    held = load_positions()
    done = []
    for p in proposals:
        if len(done) >= PAPER_AUTO_MAX:
            break
        if p["symbol"] in held:
            continue
        entry = p.get("entry") or 1
        qty = max(1, int(SOMI_BUDGET // entry))
        try:
            trader.order(p["symbol"], qty, "buy", 0)
        except Exception as exc:
            done.append(f"⏭️ {p['name']} 매수 건너뜀: {exc}")
            continue
        record_position(p["symbol"], p["name"], entry, p["stop"], p["target"], qty)
        done.append(
            f"🧪 자동 매수(모의) — {p['name']}({p['symbol']}) {qty}주 @ ~{int(entry):,}원\n"
            f"   손절 {int(p['stop']):,} / 목표 {int(p['target']):,} 감시 시작"
        )
    return done


def run(candidate_limit: int = 20, do_send: bool = False) -> str:
    proposals = make_proposals(candidate_limit)
    _save(PROPOSALS_FILE, {
        "ts": datetime.now().strftime("%Y-%m-%d %H:%M"),
        "items": proposals,
    })
    header = f"[소미 매수 제안 / {datetime.now().strftime('%Y-%m-%d %H:%M')}]"
    if not proposals:
        report = f"{header}\n오늘은 소미 기준({GOOD_SCORE}점↑) 매수 제안 종목이 없습니다. 계속 감시 중."
    elif _is_paper():
        # 모의 모드: 승인 없이 자동 매수
        executed = _auto_buy_paper(proposals)
        body = "\n\n".join(executed) if executed else "신규 매수 없음 (이미 보유 중이거나 예수금 소진)"
        report = f"{header} 🧪 모의 자동매매\n\n{body}"
    else:
        body = "\n\n".join(_fmt(p) for p in proposals[:3])
        report = (
            f"{header}\n확률 높다고 판단된 종목입니다. 매수하려면 '소미 승인 <종목명>',\n"
            f"넘기려면 '패스'라고 답해줘요. (승인 없이는 절대 매수 안 함)\n\n{body}"
        )
    if do_send:
        send(report)
    return report


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="소미 매수 제안 (승인형, 자동매수 없음)")
    parser.add_argument("--propose", action="store_true", help="후보 분석 후 제안 생성")
    parser.add_argument("--candidates", type=int, default=20)
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = parser.parse_args()
    print(run(args.candidates, args.send))


if __name__ == "__main__":
    main()
