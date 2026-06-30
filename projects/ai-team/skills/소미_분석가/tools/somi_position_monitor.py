#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 포지션 감시 — 보유 종목 수익률/목표가/손절가 점검 후 익절·손절.

모의(paper) 모드: 손절/목표/트레일링/시간초과 도달 시 자동 매도 체결.
실거래(live) 모드: 제안만 한다 — 매도는 사용자가 텔레그램에서 승인('소미 매도 …')해야 실행.
"""

from __future__ import annotations

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
sys.path.insert(0, str(SCRIPT_DIR))

import os  # noqa: E402
import time  # noqa: E402

from _shared.env import load_env  # noqa: E402
from _shared.notify import publish_report, send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared import growth  # noqa: E402
from somi_kis_reporter import KISClient, num, intraday_vwap  # noqa: E402
from somi_trade_advisor import (  # noqa: E402
    load_positions, log_closed_trade, remove_position, set_position_fields,
)

load_env(str(PROJECT_ROOT))

TRAIL_PROFIT_PCT = 5.0   # 목표가 전이라도 +5% 이상이면 분할익절 참고 제안
# 백테스트 재검증(40종목·다기간 12/24/30개월, 기준 60): 보유 18~25일이 견고한 고원.
# 7일은 승자를 일찍 놔줘 모멘텀 수익을 버림 — 20일이 절대수익 최상위·MDD 균형(24mo +134→+264%).
MAX_HOLD_DAYS = int(os.getenv("SOMI_MAX_HOLD_DAYS", "20"))


def _busdays_held(ts: str) -> int:
    """기록 시각(ts)부터 오늘까지 거래일(평일) 경과 수."""
    import numpy as np
    from datetime import datetime
    try:
        d0 = datetime.strptime(str(ts)[:10], "%Y-%m-%d").date()
    except Exception:
        return 0
    return int(np.busday_count(d0, datetime.now().date()))


def _is_paper() -> bool:
    """거래 모드 — trade_mode.json(텔레그램 토글) 우선, 없으면 KIS_PAPER 환경변수.
    명시적 'live'가 아니면 모의로 본다(기본 모의)."""
    import json
    f = PROJECT_ROOT / "output" / "cache" / "trade_mode.json"
    try:
        mode = json.loads(f.read_text(encoding="utf-8")).get("mode", "")
    except Exception:
        mode = ""
    if mode:
        return mode != "live"
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


def _atr(kis: KISClient, symbol: str, period: int = 14) -> float:
    """일봉 ATR(평균진폭) — 변동성 기반 손절폭 산출용. 실데이터만 사용(없으면 0)."""
    try:
        d = kis.daily_prices(symbol, period + 1)
    except Exception:
        return 0.0
    trs = []
    for i in range(len(d) - 1):
        h = num(d[i].get("stck_hgpr")); low = num(d[i].get("stck_lwpr")); pc = num(d[i + 1].get("stck_clpr"))
        if h and low and pc:
            trs.append(max(h - low, abs(h - pc), abs(low - pc)))
    return (sum(trs) / len(trs)) if trs else 0.0


def _paper_sell(symbol: str, reason: str, exit_price: float, sell_qty: int = 0,
                partial: bool = False, extra: dict | None = None) -> str:
    """모의 자동 매도. partial=True면 sell_qty만 매도하고 포지션 유지(분할익절),
    아니면 보유분 전량 매도 후 거래일지 마감·기록 정리."""
    from kis_trader import KISTrader

    trader = KISTrader()
    if not trader.paper:
        return ""
    held = next((int(h["qty"]) for h in trader.balance().get("holdings", []) if h["symbol"] == symbol), 0)
    if held <= 0:  # 원장에 이미 없음 → 메타만 정리(고스트 제거)
        remove_position(symbol)
        return " → (이미 청산됨, 기록 정리)"
    qty = min(sell_qty, held) if (partial and sell_qty) else held
    if qty <= 0:
        return ""
    try:
        res = trader.order(symbol, qty, "sell", 0)
    except Exception as exc:
        return f" → 자동 매도 실패: {exc}"
    sell_fill = res.get("price") or exit_price
    meta = load_positions().get(symbol, {})
    try:
        log_closed_trade(symbol, meta.get("name", symbol), num(meta.get("entry")),
                         sell_fill or num(meta.get("entry")), qty, reason,
                         meta.get("ts", ""), meta.get("score"), extra=extra)
    except Exception:
        pass
    if partial and qty < held:
        set_position_fields(symbol, {"qty": held - qty, "partial_taken": True})
        return f" → 🧪 분할익절 체결({qty}주, 잔여 {held - qty}주 보유)"
    remove_position(symbol)
    return f" → 🧪 모의 자동 매도 체결({qty}주)"


def _journal_extra(p: dict, max_up: float, max_dn: float, sell_reason: str,
                   profit: bool, stopped: bool) -> dict:
    """거래일지 마감 필드 — 진입점수 세부/최대상승·하락률/성공·실패원인/시간대/시장상태/테마뉴스."""
    cause = ""
    if profit:
        cause = "추세 지속·진입 타이밍 양호" if (p.get("entry_score") or 0) >= 70 else "수급/모멘텀 수익"
    elif stopped:
        cause = "손절 도달 — 진입 후 약세 전환"
    else:
        cause = "조기청산/시간초과 — 모멘텀 소멸"
    return {
        "entry_score": p.get("entry_score"), "risk_level": p.get("risk_level"),
        "dq_state": p.get("dq_state"), "score_mode": p.get("score_mode"),
        "buy_reason": p.get("buy_reason", ""), "sell_reason": sell_reason,
        "max_up_pct": round(max_up, 2), "max_dn_pct": round(max_dn, 2),
        "stopped": stopped, "took_profit": profit,
        "slot": p.get("slot"), "regime": p.get("regime"), "news": p.get("news"),
        "success_cause": cause if profit else "", "fail_cause": "" if profit else cause,
    }


def check_positions() -> list[str]:
    positions = load_positions()
    if not positions:
        return []
    if _is_paper():
        os.environ["KIS_PAPER"] = "true"   # _paper_sell의 KISTrader가 모의 계좌로 체결하도록
    kis = KISClient()
    alerts = []
    for symbol, p in positions.items():
        try:
            q = kis.quote(symbol)
        except Exception:
            continue
        cur = num(q.get("stck_prpr"))
        if not cur:
            continue
        entry = num(p.get("entry"))
        stop = num(p.get("stop"))
        target = num(p.get("target"))
        name = p.get("name", symbol)
        qty = int(p.get("qty") or 0)
        pnl = ((cur - entry) / entry * 100) if entry else 0
        won = lambda v: f"{int(v):,}원"
        paper = _is_paper()

        # 고저점 추적(거래일지 최대상승/하락률) — 실시간 현재가로만 갱신
        high_water = max(num(p.get("high_water")) or entry, cur)
        low_water = min(num(p.get("low_water")) or entry, cur)
        if high_water != num(p.get("high_water")) or low_water != num(p.get("low_water")):
            set_position_fields(symbol, {"high_water": high_water, "low_water": low_water})
        max_up = ((high_water - entry) / entry * 100) if entry else 0
        max_dn = ((low_water - entry) / entry * 100) if entry else 0

        # ATR 손절(변동성 기반) — 기본 -3%와 비교해 더 보수적인(높은) 손절가 사용
        atr = _atr(kis, symbol)
        atr_stop = entry - 2 * atr if atr else 0
        pct3_stop = entry * 0.97
        eff_stop = max(stop or 0, atr_stop, pct3_stop) if entry else stop
        # 트레일링 스탑: 고점 대비 -trail% (수익권 진입분만)
        trail_pct = num(p.get("trail_pct")) or 3.0
        trail_stop = high_water * (1 - trail_pct / 100)
        tp1 = num(p.get("tp1")) or (entry * 1.05)
        tp2 = num(p.get("tp2")) or max(target, entry * 1.08)
        partial_taken = bool(p.get("partial_taken"))

        # 조기청산 신호: 당일 분봉 VWAP 이탈(>1%) + 손실권, 또는 장대음봉
        vwap = intraday_vwap(kis.minute_chart(symbol))
        chg = num(q.get("prdy_ctrt"))
        early = (vwap and cur < vwap * 0.99 and pnl < 0) or (chg <= -5)

        def _clear(state, reason, hold_reason, risk, action):
            return (
                f"[청산 제안]\n"
                f"- 종목: {name}({symbol}) · 현재 {won(cur)} ({pnl:+.1f}%)\n"
                f"- 현재 상태: {state}\n"
                f"- 청산 이유: {reason}\n"
                f"- 더 보유할 이유: {hold_reason}\n"
                f"- 리스크: {risk}\n"
                f"- 최대 상승/하락: {max_up:+.1f}% / {max_dn:+.1f}%\n"
                f"- 사용자 승인 필요: 예{action}"
            )

        manual = "\n  (실거래: '소미 매도 {}' 로 승인)".format(name)

        # 우선순위: 손절 → 트레일링 → 2차익절 → 조기청산 → 1차분할익절 → 시간초과 → 보유
        if eff_stop and cur <= eff_stop:
            ex = _journal_extra(p, max_up, max_dn, "stop", profit=False, stopped=True)
            action = _paper_sell(symbol, "stop", cur, extra=ex) if paper else manual
            alerts.append(_clear("손절", f"손절가 {won(eff_stop)} 도달(ATR/-3% 보수적용)",
                                 "근거 약함 — 원칙 청산", "추가 하락 가능", action))
        elif partial_taken and cur <= trail_stop and pnl > 0:
            ex = _journal_extra(p, max_up, max_dn, "trailing", profit=True, stopped=False)
            action = _paper_sell(symbol, "trailing", cur, extra=ex) if paper else manual
            alerts.append(_clear("트레일링 청산", f"고점 {won(high_water)} 대비 -{trail_pct:.0f}% 이탈",
                                 "추세 재강화 시에만", "이익 반납 방지", action))
        elif (target and cur >= target) or cur >= tp2:
            ex = _journal_extra(p, max_up, max_dn, "target", profit=True, stopped=False)
            action = _paper_sell(symbol, "target", cur, extra=ex) if paper else manual
            alerts.append(_clear("2차 익절", f"목표/2차익절가({won(tp2)}) 도달",
                                 "초강세 지속 시 트레일링 전환", "되돌림 가능", action))
        elif early:
            ex = _journal_extra(p, max_up, max_dn, "early_exit", profit=(pnl > 0), stopped=False)
            action = _paper_sell(symbol, "early_exit", cur, extra=ex) if paper else manual
            why = "VWAP 이탈+손실권" if (vwap and cur < vwap * 0.99) else f"장대음봉 {chg:+.1f}%"
            alerts.append(_clear("조기청산", f"{why} — 모멘텀 약화",
                                 "신호 회복 시 재진입", "추세 이탈", action))
        elif not partial_taken and cur >= tp1 and qty >= 2:
            half = qty // 2
            ex = _journal_extra(p, max_up, max_dn, "tp1_partial", profit=True, stopped=False)
            action = _paper_sell(symbol, "tp1_partial", cur, sell_qty=half, partial=True, extra=ex) if paper else manual
            alerts.append(_clear("1차 분할익절", f"+5%({won(tp1)}) 도달 — {half}주 익절, 잔여 트레일링",
                                 "잔여분 추세 지속 기대", "급반락 시 잔여 반납", action))
        elif _busdays_held(p.get("ts", "")) >= MAX_HOLD_DAYS:
            ex = _journal_extra(p, max_up, max_dn, "timeout", profit=(pnl > 0), stopped=False)
            action = _paper_sell(symbol, "timeout", cur, extra=ex) if paper else manual
            alerts.append(_clear("시간초과 청산", f"보유 {MAX_HOLD_DAYS}거래일 경과",
                                 "신규 모멘텀 발생 시", "정체·기회비용", action))
        elif pnl >= TRAIL_PROFIT_PCT:
            alerts.append(_clear("보유 가능", f"수익 {pnl:+.1f}% — 트레일링 감시 중",
                                 "목표까지 추세 지속 기대", "급반락 시 트레일링 청산", ""))
    return alerts


def run(do_send: bool = False) -> str:
    alerts = check_positions()
    if not alerts:
        report = "보유 포지션: 익절/손절 신호 없음 (정상 감시 중)."
    else:
        report = f"[소미 포지션 점검 / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n\n" + "\n\n".join(alerts)
        if do_send:
            if _is_paper():
                publish_report("소미 포지션 점검", report)   # 모의: 자동청산 정보성 → 노션 링크
            else:
                send(report)   # 실거래: 매도 승인 요청은 급한 액션 → 텔레그램 인라인
    growth.record(
        "somi_position", role="포지션 익절/손절/시간초과 청산",
        data=f"보유 {len(load_positions())}종목", judgment=f"신호 {len(alerts)}건",
        result=("청산 신호 발생" if alerts else "보유 유지"),
        good="목표·손절·20일 기준 일관 적용", bad="",
        scores={"fit": 22, "evidence": 19, "efficiency": 18, "risk": 20, "brevity": 9},
    )
    return report


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="소미 보유 포지션 익절/손절 점검")
    parser.add_argument("--send", action="store_true", help="신호 있으면 텔레그램 전송")
    parser.add_argument("--daemon", action="store_true", help="데몬 모드 (장중 평일 N분 주기 자동 점검/청산)")
    args = parser.parse_args()

    if args.daemon:
        from _shared.utils import due_slot
        interval = int(os.getenv("SOMI_POSITION_INTERVAL", "300"))  # 점검 주기(자동 손절/익절)
        slots = os.getenv("SOMI_POSITION_SLOTS", "09:30,11:00,13:00,15:20").split(",")
        state = PROJECT_ROOT / "output" / "cache" / "somi_position_slots.json"
        with ProcessLock("somi_position_monitor"):
            print(f"[{datetime.now()}] 소미 포지션 감시 데몬 시작 (점검 {interval // 60}분 / 보고 슬롯 {','.join(slots)})")
            while True:
                now = datetime.now()
                if now.weekday() < 5 and 9 <= now.hour < 16:  # 평일 장중만
                    try:
                        # 자동 손절/익절은 매 틱 점검(체결), 보고(텔레그램/노션)는 정해진 시각에만
                        run(do_send=bool(due_slot(slots, state)))
                    except Exception as e:
                        send(f"⚠️ 소미 포지션 감시 오류: {e}")
                        print(f"[{now}] 오류: {e}")
                else:
                    print(f"[{now}] 장외 대기")
                time.sleep(interval)
        return

    print(run(args.send))


if __name__ == "__main__":
    main()
