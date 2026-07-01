#!/usr/bin/env python3
"""somi_signal_engine.py — 소미 매수신호 자동 푸시 (신호→원터치 승인 체결의 '신호' 절반).

흐름:
  1) make_proposals() 로 후보 분석·채점 → 임계 점수 이상만 선별
  2) 예산 기준 수량 계산 → pending_signals.json 에 대기 신호 저장(만료시각 포함)
  3) 텔레그램으로 "🚨 매수신호 ... 승인하려면 'ㅇㅋ'" 푸시

⚠️ 이 엔진은 주문을 체결하지 않는다(승인 푸시 전용). 체결은 사용자가 텔레그램에서
   'ㅇㅋ'/'승인'으로 직접 방아쇠를 당길 때만 telegram_receiver 주문 실행기로 이뤄진다.
   모의 자동매수는 소미제안(somi_trade_advisor)이 단일 실행자로 담당한다(중복 매수 방지).

실행:
  python somi_signal_engine.py --scan                  # 1회 스캔·푸시
  python somi_signal_engine.py --daemon --interval 600 # 데몬(10분 간격)
"""
from __future__ import annotations

import os
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

KST = timezone(timedelta(hours=9))

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))
sys.path.insert(0, str(_here))

from _shared.env import load_env          # noqa: E402
from _shared.notify import send           # noqa: E402
from _shared.process import ProcessLock   # noqa: E402

load_env()

# 신호 저장소 — telegram_receiver 가 승인 시 읽는 공유 파일
SIGNALS_FILE = PROJECT_ROOT / "output" / "cache" / "pending_signals.json"
BUDGET = int(os.getenv("SOMI_BUDGET_PER_TRADE", "1000000"))
MIN_SCORE = int(os.getenv("SOMI_SIGNAL_MIN_SCORE", "0"))   # 0=advisor 기본(GOOD_SCORE) 사용
TOP_N = int(os.getenv("SOMI_SIGNAL_TOP", "3"))
EXPIRE_MIN = int(os.getenv("SOMI_SIGNAL_EXPIRE_MIN", "30"))

# 거래 세션(평일, KST). 정규장 09:00~15:30 + 시간외 단일가(코스피 ~20:00) 포함.
# 형식: "HH:MM-HH:MM,HH:MM-HH:MM" — 환경변수로 조절 가능.
SESSION_WINDOWS = os.getenv("SOMI_SESSION_WINDOWS", "08:30-08:40,09:00-20:00")


def _parse_windows(spec: str) -> list[tuple[int, int]]:
    out = []
    for part in spec.split(","):
        part = part.strip()
        if "-" not in part:
            continue
        a, b = part.split("-", 1)
        try:
            ah, am = map(int, a.split(":")); bh, bm = map(int, b.split(":"))
            out.append((ah * 60 + am, bh * 60 + bm))
        except Exception:
            continue
    return out


def _in_session(now: datetime | None = None) -> bool:
    """평일이며 거래 세션 시간대 안인지(KST). 주말은 항상 False."""
    now = now or datetime.now(KST)
    if now.weekday() >= 5:   # 토(5)/일(6)
        return False
    mins = now.hour * 60 + now.minute
    return any(lo <= mins <= hi for lo, hi in _parse_windows(SESSION_WINDOWS))


def _save_signals(signals: list[dict]) -> None:
    import json
    SIGNALS_FILE.parent.mkdir(parents=True, exist_ok=True)
    payload = {"ts": datetime.now().isoformat(timespec="seconds"), "signals": signals}
    SIGNALS_FILE.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _build_signals(proposals: list[dict], budget: int) -> list[dict]:
    signals = []
    expires = (datetime.now() + timedelta(minutes=EXPIRE_MIN)).isoformat(timespec="seconds")
    for i, p in enumerate(proposals, 1):
        entry = int(p.get("entry") or 0)
        if entry <= 0:
            continue
        qty = max(1, budget // entry)
        signals.append({
            "id": i,
            "symbol": p["symbol"], "name": p.get("name", p["symbol"]),
            "qty": int(qty), "entry": entry,
            "stop": int(p.get("stop") or 0), "target": int(p.get("target") or 0),
            "score": p.get("score"), "expires": expires,
        })
    return signals


def _trade_mode() -> str:
    """거래 모드 — trade_mode.json 우선, 없으면 KIS_PAPER 환경변수."""
    import json
    f = PROJECT_ROOT / "output" / "cache" / "trade_mode.json"
    try:
        return json.loads(f.read_text(encoding="utf-8")).get("mode", "")
    except Exception:
        return ""


def _is_bear() -> bool:
    """KOSPI·KOSDAQ 중 하나라도 하락 국면인지. 하락장에선 후보를 더 넓게 검색한다
    (매수를 막지는 않음 — 하락장에도 기회 종목을 찾는다)."""
    try:
        from market_regime import stable_regime, KOSPI_PROXY, KOSDAQ_PROXY
        return "bear" in (stable_regime(KOSPI_PROXY).get("regime"),
                          stable_regime(KOSDAQ_PROXY).get("regime"))
    except Exception:
        return False


def _format_push(signals: list[dict]) -> str:
    m = _trade_mode()
    if m == "live":
        mode = "🔴실거래"
    elif m == "paper":
        mode = "🧪모의"
    else:
        mode = "🧪모의" if os.getenv("KIS_PAPER", "false").lower() in ("1", "true", "yes") else "💵실거래"
    lines = [f"🚨 [소미] 매수신호 {len(signals)}건 ({mode}, {EXPIRE_MIN}분 내 유효)"]
    for s in signals:
        sc = f" 점수{s['score']}" if s.get("score") is not None else ""
        lines.append(
            f"\n{s['id']}) {s['name']}({s['symbol']}){sc}\n"
            f"   진입 {s['entry']:,} · 손절 {s['stop']:,} · 목표 {s['target']:,} · {s['qty']:,}주"
        )
    lines.append(
        "\n\n👉 승인: 'ㅇㅋ'(1번) 또는 '승인 2' / 전부 보류: '패스'\n"
        "   (승인하면 그 종목만 즉시 매수, 손절·목표 자동 감시)"
    )
    return "\n".join(lines)


def scan(budget: int = BUDGET, top: int = TOP_N, do_send: bool = True,
         session_only: bool = False) -> str:
    """후보 스캔→신호 선별→저장→푸시. 반환=요약 문자열.
    session_only=True 면 거래 세션(평일 장중·시간외) 밖에서는 스캔하지 않는다."""
    # 가드레일: 모의(paper) 운영 중엔 승인푸시 금지 — 자동매수 단일 실행자는 소미제안(trade_advisor).
    # 데몬이 자동재시작돼도 이 가드로 무해화하고, 남은 대기신호를 비워 옛 'ㅇㅋ' 오체결도 막는다.
    if os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}:
        _save_signals([])
        msg = "⏸️ [소미] 모의(paper) 모드 — 신호 푸시 중지(자동매수는 소미제안 담당)"
        print(msg)
        return msg
    if session_only and not _in_session():
        msg = "⏸️ [소미] 거래 세션 외 — 스캔 생략"
        print(msg)
        return msg
    from somi_trade_advisor import make_proposals
    # 하락 국면이면 후보 풀을 넓혀 더 많은 종목을 검색(기회 종목 발굴). 매수는 막지 않음.
    base_cand = int(os.getenv("SOMI_SIGNAL_CANDIDATES", "20"))
    cand = int(os.getenv("SOMI_SIGNAL_CANDIDATES_BEAR", str(base_cand * 2))) if _is_bear() else base_cand
    kwargs = {"candidate_limit": cand}
    # 한별(퀀트) 튜닝 추천을 우선 반영 — 없으면 env(MIN_SCORE)
    eff_min = MIN_SCORE
    try:
        import json as _json
        tune = _json.loads((PROJECT_ROOT / "output" / "cache" / "somi_tuning.json").read_text(encoding="utf-8"))
        rec = tune.get("recommend_min_score")
        if isinstance(rec, (int, float)):
            eff_min = int(rec)
    except Exception:
        pass
    if eff_min > 0:
        kwargs["min_score"] = eff_min
    proposals = make_proposals(**kwargs)
    if not proposals:
        _save_signals([])
        msg = "📭 [소미] 현재 매수신호 없음 (조건 충족 종목 없음)"
        print(msg)
        return msg

    signals = _build_signals(proposals[:top], budget)
    if not signals:
        _save_signals([])
        return "📭 [소미] 유효 신호 없음 (진입가 산출 실패)"

    # 체결하지 않고 푸시만 — 사용자 'ㅇㅋ' 승인 시 telegram_receiver가 체결.
    # (모의 자동매수는 소미제안이 단일 실행자로 담당)
    _save_signals(signals)
    push = _format_push(signals)
    print(push)
    if do_send:
        send(push)
    return f"신호 {len(signals)}건 푸시 완료"


def _daemon(interval: int, budget: int, top: int, session_only: bool = True) -> None:
    print(f"[소미 신호엔진] 데몬 시작 — {interval}s 간격, 세션게이트={session_only}")
    while True:
        try:
            scan(budget=budget, top=top, do_send=True, session_only=session_only)
        except Exception as e:
            print(f"[소미 신호엔진] 스캔 오류: {e}")
        time.sleep(max(60, interval))


def main() -> None:
    import argparse
    p = argparse.ArgumentParser(description="소미 매수신호 엔진 (체결 안 함, 신호만)")
    p.add_argument("--scan", action="store_true", help="1회 스캔·푸시")
    p.add_argument("--daemon", action="store_true", help="데몬 모드")
    p.add_argument("--interval", type=int, default=600, help="데몬 스캔 간격(초)")
    p.add_argument("--budget", type=int, default=BUDGET, help="1종목 예산(원)")
    p.add_argument("--top", type=int, default=TOP_N, help="최대 신호 개수")
    p.add_argument("--no-send", action="store_true", help="텔레그램 전송 안 함(테스트)")
    p.add_argument("--session-only", action="store_true",
                   help="거래 세션(평일 장중·시간외~20:00) 밖이면 스캔 생략")
    p.add_argument("--no-session", action="store_true",
                   help="데몬에서 세션 게이트 끄고 상시 스캔")
    args = p.parse_args()

    if args.daemon:
        # 데몬은 항상 세션 게이트(평일 장중·시간외만 스캔). 강제 상시는 --no-session.
        with ProcessLock("somi_signal_engine"):
            _daemon(args.interval, args.budget, args.top, session_only=not args.no_session)
    else:
        # 1회 스캔도 락으로 보호 — launchd가 10분 주기로 띄울 때 이전 스캔과 겹치면 건너뜀
        try:
            with ProcessLock("somi_signal_engine"):
                scan(budget=args.budget, top=args.top, do_send=not args.no_send,
                     session_only=args.session_only)
        except Exception:
            print("[signal] 이전 스캔 진행 중 — 이번 실행 건너뜀", file=sys.stderr)


if __name__ == "__main__":
    main()
