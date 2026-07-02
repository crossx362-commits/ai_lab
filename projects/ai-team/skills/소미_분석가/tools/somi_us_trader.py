#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""somi_us_trader.py — 미장 모의 자동매매 데몬 (야간, 맥 담당).

근거(2026-07-02 미국 전이검증, backtest_us.py): 문턱 48~52 창·보유 5일·SPY>MA20
국면에서 24개월 PF 1.53·+73%·MDD -34%(샤프 2.7). 국내와 역방향으로 55↑는
블로우오프 전패 — 게이트는 하한+상한 '창'(기본 48~54) 구조.

설계: 국내 모의와 동일하게 KIS 모의서버가 아닌 내부 원장(USD) 체결.
시세·점수는 야후 일봉(당일 진행중 봉 포함)·backtest._score_levels 재사용.
⚠️ 모의 전용 — 실거래 주문 경로 없음. 청산 기록은 somi_us_closed.json(국내 학습과 분리).

세션: 미 정규장 KST 22:30~05:00(서머타임, SOMI_US_SESSION로 조정). 장외엔 대기.

실행:
  python somi_us_trader.py --daemon     # 데몬 (10분 발굴 주기)
  python somi_us_trader.py --scan       # 1회 스캔(체결 없이 후보 출력)
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

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))
sys.path.insert(0, str(_here))

from _shared.env import load_env      # noqa: E402
from _shared.notify import send       # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env()

import backtest as bt                 # noqa: E402 — 점수 산식 단일 소스
from backtest_us import _yahoo_bars, us_regime_map, UNIVERSE_US  # noqa: E402

CACHE = ROOT / "output" / "cache"
LEDGER = CACHE / "somi_paper_us.json"          # USD 원장 {cash, positions{sym:{qty,avg,ts,stop,target}}}
CLOSED = CACHE / "somi_us_closed.json"         # 청산 기록(미국 전용 — 국내 학습과 분리)

START_CASH = float(os.getenv("SOMI_US_PAPER_CASH", "10000"))       # USD
GATE_LO = int(os.getenv("SOMI_US_GATE_LO", "48"))
GATE_HI = int(os.getenv("SOMI_US_GATE_HI", "54"))                  # 55↑ 블로우오프 차단(검증)
MAX_SLOTS = int(os.getenv("SOMI_US_SLOTS", "3"))
STOP_PCT = float(os.getenv("SOMI_US_STOP_PCT", "3"))               # 하드손절 -3%
TARGET_PCT = float(os.getenv("SOMI_US_TARGET_PCT", "8"))           # 목표 +8%
MAX_HOLD_D = int(os.getenv("SOMI_US_MAX_HOLD_DAYS", "7"))          # 시간청산(달력일≈거래 5일)
SLIP = float(os.getenv("SOMI_US_SLIP", "0.0005"))                  # 편도 슬리피지
FEE = float(os.getenv("SOMI_US_FEE", "0.0025"))                    # KIS 해외 편도 수수료(보수)
SESSION = os.getenv("SOMI_US_SESSION", "22:30-05:00")              # KST, 자정 걸침
SCAN_MIN = int(os.getenv("SOMI_US_SCAN_MIN", "10"))


def _in_session(now: datetime | None = None) -> bool:
    now = now or datetime.now()
    lo, hi = SESSION.split("-")
    hm = now.strftime("%H:%M")
    # 자정 걸침: 22:30~24:00(월~금 개장일 밤) 또는 00:00~05:00(화~토 새벽)
    if hm >= lo:
        return now.weekday() <= 4          # 월~금 밤
    if hm <= hi:
        return 1 <= now.weekday() <= 5     # 화~토 새벽
    return False


def _load(p: Path, default):
    try:
        return json.loads(p.read_text(encoding="utf-8")) if p.exists() else default
    except Exception:
        return default


def _save(p: Path, d) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    tmp = p.with_name(p.name + ".tmp")
    tmp.write_text(json.dumps(d, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, p)


def _ledger() -> dict:
    d = _load(LEDGER, {})
    return d if d else {"cash": START_CASH, "positions": {}}


def _record_close(sym: str, name: str, pos: dict, exit_px: float, reason: str) -> float:
    """청산 기록 — 실비용(왕복 수수료+슬리피지) 차감한 순수익률 저장."""
    entry = pos["avg"]
    gross = (exit_px - entry) / entry * 100 if entry else 0.0
    cost = (FEE * 2 + SLIP * 2) * 100
    ret = gross - cost
    log = _load(CLOSED, [])
    log.append({"symbol": sym, "name": name, "entry": entry, "exit": exit_px,
                "qty": pos["qty"], "ret_pct": round(ret, 2), "gross_ret_pct": round(gross, 2),
                "reason": reason, "score": pos.get("score"), "market": "US",
                "ts_open": pos.get("ts", ""), "ts_close": datetime.now().strftime("%Y-%m-%d %H:%M")})
    _save(CLOSED, log)
    return ret


def scan_candidates(regime: dict) -> list[dict]:
    """유니버스 스캔 — 게이트 창(GATE_LO~GATE_HI) 통과 후보. 당일 진행중 봉 포함 점수."""
    out = []
    today_ok = regime.get(max(regime), True)   # 최신 SPY 국면
    if not today_ok:
        return []                              # 하락국면 — 신규 진입 없음(검증 조건과 동일)
    for sym, name in UNIVERSE_US.items():
        try:
            bars = _yahoo_live(sym)   # 진행중 당일 봉 포함(장중 누적 기준 점수)
            if len(bars) < 25:
                continue
            score, entry, stop, target = bt._score_levels(bars, len(bars) - 1)
            if GATE_LO <= score <= GATE_HI:
                out.append({"symbol": sym, "name": name, "score": score, "price": bars[-1]["c"]})
        except Exception:
            continue
        time.sleep(0.2)
    return sorted(out, key=lambda x: x["score"], reverse=True)


def _yahoo_live(sym: str) -> list[dict]:
    """진행중 당일 봉 포함 일봉(점수는 장중 누적 기준 — 보수적)."""
    import urllib.request
    url = f"https://query1.finance.yahoo.com/v8/finance/chart/{sym}?range=6mo&interval=1d"
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=15) as r:
        d = json.loads(r.read())
    res = d["chart"]["result"][0]
    ts = res.get("timestamp") or []
    q = res["indicators"]["quote"][0]
    bars = []
    for i in range(len(ts)):
        c, o, h, lo, v = (q["close"][i], q["open"][i], q["high"][i], q["low"][i], q["volume"][i])
        if all(x is not None for x in (c, o, h, lo, v)):
            bars.append({"date": datetime.fromtimestamp(ts[i]).strftime("%Y%m%d"),
                         "o": o, "h": h, "l": lo, "c": c, "v": v, "val": c * v * 1350})
    return bars


def buy(cands: list[dict]) -> list[str]:
    led = _ledger()
    held = led["positions"]
    done = []
    for c in cands:
        if len(held) >= MAX_SLOTS or c["symbol"] in held:
            continue
        budget = led["cash"] / (MAX_SLOTS - len(held))
        fill = c["price"] * (1 + SLIP)
        qty = int(budget // fill)
        if qty < 1:
            continue
        led["cash"] -= fill * qty
        held[c["symbol"]] = {"qty": qty, "avg": round(fill, 2), "score": c["score"],
                             "ts": datetime.now().strftime("%Y-%m-%d %H:%M"),
                             "stop": round(fill * (1 - STOP_PCT / 100), 2),
                             "target": round(fill * (1 + TARGET_PCT / 100), 2)}
        done.append(f"🇺🇸 {c['name']}({c['symbol']}) {qty}주 @ ${fill:,.2f} (점수 {c['score']})")
    if done:
        _save(LEDGER, led)
    return done


def manage() -> list[str]:
    """보유 관리 — 하드손절/목표/시간청산."""
    led = _ledger()
    out = []
    for sym in list(led["positions"]):
        pos = led["positions"][sym]
        name = UNIVERSE_US.get(sym, sym)
        try:
            cur = _yahoo_live(sym)[-1]["c"]
        except Exception:
            continue
        reason = None
        if cur <= pos["stop"]:
            reason = "stop"
        elif cur >= pos["target"]:
            reason = "target"
        else:
            try:
                opened = datetime.strptime(pos["ts"], "%Y-%m-%d %H:%M")
                if (datetime.now() - opened).days >= MAX_HOLD_D:
                    reason = "timeout"
            except Exception:
                pass
        if reason:
            fill = cur * (1 - SLIP)
            led["cash"] += fill * pos["qty"]
            ret = _record_close(sym, name, pos, fill, reason)
            del led["positions"][sym]
            emoji = "🟢" if ret > 0 else "🔴"
            out.append(f"{emoji} {name}({sym}) {reason} 청산 @ ${fill:,.2f} ({ret:+.2f}%)")
    if out:
        _save(LEDGER, led)
    return out


def daemon() -> None:
    with ProcessLock("somi_us_trader"):
        print(f"[{datetime.now()}] 🇺🇸 미장 모의 데몬 시작 (세션 {SESSION} KST, 게이트 {GATE_LO}~{GATE_HI})")
        regime, regime_ts = {}, 0.0
        last_scan = None
        while True:
            if not _in_session():
                time.sleep(120)
                continue
            if time.time() - regime_ts > 3600:      # 국면은 1시간 캐시
                try:
                    regime = us_regime_map(3)
                    regime_ts = time.time()
                except Exception as e:
                    print(f"국면 조회 실패: {e}")
            try:
                sold = manage()
                bought = []
                now = datetime.now()
                if not last_scan or (now - last_scan).total_seconds() >= SCAN_MIN * 60:
                    last_scan = now
                    bought = buy(scan_candidates(regime))
                if sold or bought:
                    led = _ledger()
                    send("🇺🇸 [소미 미장 모의]\n" + "\n".join(sold + bought)
                         + f"\n💵 현금 ${led['cash']:,.0f} · 보유 {len(led['positions'])}종목")
            except Exception as e:
                print(f"[{datetime.now()}] 사이클 오류: {e}")
            time.sleep(60)


def main() -> None:
    if "--daemon" in sys.argv:
        daemon()
    elif "--scan" in sys.argv:
        regime = us_regime_map(3)
        cands = scan_candidates(regime)
        print(f"국면 상승={regime.get(max(regime))} / 게이트 {GATE_LO}~{GATE_HI} / 후보 {len(cands)}")
        for c in cands:
            print(f"  {c['name']}({c['symbol']}) 점수 {c['score']} @ ${c['price']:,.2f}")
    else:
        led = _ledger()
        print(json.dumps(led, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
