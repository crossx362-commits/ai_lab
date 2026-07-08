#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""somi_crypto_trader.py — 가상화폐(업비트 KRW) 모의 자동매매 데몬 (24시간).

설계: 미장 모의(somi_us_trader.py)와 동형 — 내부 원장(KRW) 체결, 실거래 주문 경로 없음.
시세는 업비트 공개 API(인증 불필요: /v1/candles/days·/v1/ticker), 점수는
backtest._score_levels 재사용(신호 산식 단일 소스 가드레일).

⚠️ 게이트 문턱은 크립토 백테스트 미검증(2026-07-07 신설) — 모의 데이터 수집 단계.
   주식 눈금을 그대로 빌렸으므로(기본 50~70 창) 청산 표본이 쌓이면 전략랩/한별이 검증·보정.
   청산 기록은 somi_crypto_closed.json — 국내주식·미장 학습 데이터와 절대 혼합 금지(눈금 상이).

국면: BTC > MA20 상승 국면에만 신규 진입(미장 SPY>MA20과 동형). 24시간 장 — 세션 게이트 없음.
유니버스: KRW 마켓 24h 거래대금 상위 N(기본 20) 동적 선정.

실행:
  python somi_crypto_trader.py --daemon   # 데몬 (10분 발굴 주기 + 60초 보유관리)
  python somi_crypto_trader.py --scan     # 1회 스캔(체결 없이 후보 출력)
  python somi_crypto_trader.py            # 원장 출력
"""
from __future__ import annotations

import json
import os
import socket
import sys
import time
import urllib.request
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

CACHE = ROOT / "output" / "cache"
LEDGER = CACHE / "somi_crypto_paper.json"       # KRW 원장 {cash, positions{mkt:{qty,avg,ts,stop,target}}}
CLOSED = CACHE / "somi_crypto_closed.json"      # 청산 기록(크립토 전용 — 주식 학습과 분리)
TUNING_FILE = CACHE / "somi_tuning_crypto.json"  # 자동 튜닝 파일(주식·미장과 분리)

# 이중 데몬 방지(미장과 동일 패턴) — 다른 기기 담당 시 SOMI_CRYPTO_ENABLE=false
CRYPTO_ENABLED = os.getenv("SOMI_CRYPTO_ENABLE", "true").lower() in {"1", "true", "yes"}

START_CASH = float(os.getenv("SOMI_CRYPTO_PAPER_CASH", "10000000"))   # KRW 1천만(주식 모의와 동일)
# ⚠️ 크립토 자체 백테스트 미검증 — 기본창은 미장(somi_us_trader) 44~54 차용(2026-07-07).
# 근거: 미장과 입력이 동일(OHLCV만, 수급 데이터 없음 → calculate_score의 수급 가점 0)이라
# 점수 눈금이 같고, 미장은 이 창이 24개월 검증(PF 1.25·130건)됨. 국내주식 눈금(50~70)은
# 수급 가점 포함이라 크립토에 구조적으로 과높음(실측: 조용한 날 전 종목 0~14점).
_CRYPTO_TUNING_BOUNDS = {"gate_lo": (42, 50), "gate_hi": (50, 60)}


def _tuning_crypto(key: str, default: int) -> int:
    try:
        v = int(json.loads(TUNING_FILE.read_text(encoding="utf-8")).get("params", {}).get(key, default))
    except Exception:
        v = default
    lo, hi = _CRYPTO_TUNING_BOUNDS.get(key, (v, v))
    return max(lo, min(hi, v))


def _gate_lo() -> int:
    return _tuning_crypto("gate_lo", int(os.getenv("SOMI_CRYPTO_GATE_LO", "44")))


def _gate_hi() -> int:
    return _tuning_crypto("gate_hi", int(os.getenv("SOMI_CRYPTO_GATE_HI", "54")))


MAX_SLOTS = int(os.getenv("SOMI_CRYPTO_SLOTS", "3"))
STOP_PCT = float(os.getenv("SOMI_CRYPTO_STOP_PCT", "5"))        # 하드손절 -5% (크립토 변동성 반영)
TARGET_PCT = float(os.getenv("SOMI_CRYPTO_TARGET_PCT", "10"))   # 목표 +10%
MAX_HOLD_D = int(os.getenv("SOMI_CRYPTO_MAX_HOLD_DAYS", "7"))   # 시간청산
SLIP = float(os.getenv("SOMI_CRYPTO_SLIP", "0.001"))            # 편도 슬리피지 0.1%
FEE = float(os.getenv("SOMI_CRYPTO_FEE", "0.0005"))             # 업비트 편도 수수료 0.05%
SCAN_MIN = int(os.getenv("SOMI_CRYPTO_SCAN_MIN", "10"))
UNIVERSE_N = int(os.getenv("SOMI_CRYPTO_UNIVERSE", "20"))       # 24h 거래대금 상위 N
_EXCLUDE = {"KRW-USDT", "KRW-USDC", "KRW-DAI", "KRW-TUSD"}      # 스테이블 제외


def _get(url: str) -> object:
    req = urllib.request.Request(url, headers={"Accept": "application/json",
                                               "User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=15) as r:
        return json.loads(r.read().decode("utf-8"))


def universe() -> dict[str, str]:
    """KRW 마켓 24h 거래대금 상위 N — {market: 한글명}. 조회 실패 시 빈 dict(진입 보류)."""
    try:
        mkts = _get("https://api.upbit.com/v1/market/all?isDetails=false")
        krw = {m["market"]: m.get("korean_name", m["market"])
               for m in mkts if m["market"].startswith("KRW-") and m["market"] not in _EXCLUDE}
        # ticker는 마켓 인자 필수 — 100개씩 끊어 조회
        codes = list(krw)
        vols: dict[str, float] = {}
        for i in range(0, len(codes), 100):
            chunk = ",".join(codes[i:i + 100])
            for t in _get(f"https://api.upbit.com/v1/ticker?markets={chunk}"):
                vols[t["market"]] = float(t.get("acc_trade_price_24h", 0))
            time.sleep(0.15)
        top = sorted(vols, key=vols.get, reverse=True)[:UNIVERSE_N]
        return {m: krw[m] for m in top}
    except Exception as e:
        print(f"[{datetime.now()}] 유니버스 조회 실패: {e}")
        return {}


def _daily_bars(market: str, count: int = 200) -> list[dict]:
    """업비트 일봉(최신→과거 응답을 과거→최신으로 뒤집음). 진행중 당일 봉 포함."""
    rows = _get(f"https://api.upbit.com/v1/candles/days?market={market}&count={count}")
    bars = []
    for r in reversed(rows):
        c, o = float(r["trade_price"]), float(r["opening_price"])
        h, lo = float(r["high_price"]), float(r["low_price"])
        v = float(r.get("candle_acc_trade_volume") or 0)
        val = float(r.get("candle_acc_trade_price") or 0)
        bars.append({"date": r["candle_date_time_kst"][:10].replace("-", ""),
                     "o": o, "h": h, "l": lo, "c": c, "v": v, "val": val})
    return bars


def btc_regime() -> bool | None:
    """BTC 종가 > MA20 상승 국면 여부. 조회 실패 시 None(진입 보류 — 빈 국면 가드)."""
    try:
        bars = _daily_bars("KRW-BTC", 40)
        if len(bars) < 21:
            return None
        closes = [b["c"] for b in bars]
        ma20 = sum(closes[-20:]) / 20
        return closes[-1] > ma20
    except Exception as e:
        print(f"[{datetime.now()}] BTC 국면 조회 실패: {e}")
        return None


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


def _record_close(mkt: str, name: str, pos: dict, exit_px: float, reason: str) -> float:
    entry = pos["avg"]
    gross = (exit_px - entry) / entry * 100 if entry else 0.0
    cost = (FEE * 2 + SLIP * 2) * 100
    ret = gross - cost
    log = _load(CLOSED, [])
    log.append({"symbol": mkt, "name": name, "entry": entry, "exit": exit_px,
                "qty": pos["qty"], "ret_pct": round(ret, 2), "gross_ret_pct": round(gross, 2),
                "reason": reason, "score": pos.get("score"), "market": "CRYPTO",
                "ts_open": pos.get("ts", ""), "ts_close": datetime.now().strftime("%Y-%m-%d %H:%M")})
    _save(CLOSED, log)
    return ret


def scan_candidates(uni: dict[str, str]) -> list[dict]:
    """유니버스 스캔 — 게이트 창 통과 후보. BTC 하락 국면이면 신규 진입 없음."""
    up = btc_regime()
    if not up:            # None(조회실패)·False(하락) 모두 보류 — 빈 국면 가드
        return []
    gate_lo, gate_hi = _gate_lo(), _gate_hi()
    out = []
    for mkt, name in uni.items():
        try:
            bars = _daily_bars(mkt)
            if len(bars) < 25:
                continue
            score, entry, stop, target = bt._score_levels(bars, len(bars) - 1)
            if gate_lo <= score <= gate_hi:
                out.append({"symbol": mkt, "name": name, "score": score, "price": bars[-1]["c"]})
        except Exception:
            continue
        time.sleep(0.15)   # 업비트 공개 API rate limit(초당 10회) 여유
    return sorted(out, key=lambda x: x["score"], reverse=True)


def buy(cands: list[dict]) -> list[str]:
    led = _ledger()
    held = led["positions"]
    done = []
    for c in cands:
        if len(held) >= MAX_SLOTS or c["symbol"] in held:
            continue
        budget = led["cash"] / (MAX_SLOTS - len(held))
        fill = c["price"] * (1 + SLIP)
        qty = round(budget / fill, 8)          # 크립토는 소수 수량
        if qty <= 0 or fill * qty < 5000:      # 업비트 최소주문 5천원
            continue
        led["cash"] -= fill * qty
        held[c["symbol"]] = {"qty": qty, "avg": round(fill, 4), "score": c["score"],
                             "ts": datetime.now().strftime("%Y-%m-%d %H:%M"),
                             "stop": round(fill * (1 - STOP_PCT / 100), 4),
                             "target": round(fill * (1 + TARGET_PCT / 100), 4)}
        done.append(f"🪙 {c['name']}({c['symbol']}) {qty:g}개 @ ₩{fill:,.0f} (점수 {c['score']})")
    if done:
        _save(LEDGER, led)
    return done


def _cur_prices(markets: list[str]) -> dict[str, float]:
    if not markets:
        return {}
    try:
        rows = _get("https://api.upbit.com/v1/ticker?markets=" + ",".join(markets))
        return {r["market"]: float(r["trade_price"]) for r in rows}
    except Exception:
        return {}


# 시세 전체 실패 카운터(2026-07-08 감사) — _cur_prices()가 {}면 전 포지션 청산 감시가
# 조용히 정지된 상태. 다음 사이클 재시도는 정상이나 10회 연속이면 1회 경보.
_PX_FAIL = [0]


def manage(uni: dict[str, str]) -> list[str]:
    """보유 관리 — 하드손절/목표/시간청산."""
    led = _ledger()
    out = []
    prices = _cur_prices(list(led["positions"]))
    if led["positions"] and not prices:
        _PX_FAIL[0] += 1
        if _PX_FAIL[0] == 10:
            send("⚠️ [크립토소미] 시세 전체 조회 10회 연속 실패 — 보유 포지션 청산 감시 정지 상태(업비트 API 확인)")
    else:
        _PX_FAIL[0] = 0
    for mkt in list(led["positions"]):
        pos = led["positions"][mkt]
        name = uni.get(mkt, mkt)
        cur = prices.get(mkt)
        if cur is None:
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
            ret = _record_close(mkt, name, pos, fill, reason)
            del led["positions"][mkt]
            emoji = "🟢" if ret > 0 else "🔴"
            out.append(f"{emoji} {name}({mkt}) {reason} 청산 @ ₩{fill:,.0f} ({ret:+.2f}%)")
    if out:
        _save(LEDGER, led)
    return out


def daemon() -> None:
    with ProcessLock("somi_crypto_trader"):
        host = socket.gethostname()
        if not CRYPTO_ENABLED:
            print(f"[{datetime.now()}] 🪙 크립토 모의 비활성(SOMI_CRYPTO_ENABLE=false, host={host}) — 대기 전용")
            while True:
                time.sleep(600)
        print(f"[{datetime.now()}] 🪙 크립토 모의 데몬 시작 (host={host}, 24h, "
              f"게이트 {_gate_lo()}~{_gate_hi()} ⚠️미검증·수집단계, 슬롯 {MAX_SLOTS})")
        uni: dict[str, str] = {}
        uni_ts = 0.0
        last_scan = None
        while True:
            try:
                if time.time() - uni_ts > 3600:      # 유니버스는 1시간 캐시
                    u = universe()
                    if u:
                        uni, uni_ts = u, time.time()
                sold = manage(uni)
                bought = []
                now = datetime.now()
                if uni and (not last_scan or (now - last_scan).total_seconds() >= SCAN_MIN * 60):
                    cands = scan_candidates(uni)
                    bought = buy(cands)
                    last_scan = datetime.now()       # 종료 시각 기준(발굴 굶김 방지 — advisor 교훈)
                    print(f"[{now:%m-%d %H:%M}] 스캔 후보 {len(cands)} / 매수 {len(bought)} / "
                          f"보유 {len(_ledger()['positions'])}")
                if sold or bought:                   # 액션 메시지만 텔레그램(정보성 스팸 금지)
                    led = _ledger()
                    send("🪙 [소미 크립토 모의]\n" + "\n".join(sold + bought)
                         + f"\n💰 현금 ₩{led['cash']:,.0f} · 보유 {len(led['positions'])}종목")
            except Exception as e:
                print(f"[{datetime.now()}] 사이클 오류: {e}")
            time.sleep(60)


def main() -> None:
    if "--daemon" in sys.argv:
        daemon()
    elif "--scan" in sys.argv:
        uni = universe()
        up = btc_regime()
        cands = scan_candidates(uni)
        print(f"BTC 상승국면={up} / 게이트 {_gate_lo()}~{_gate_hi()}(미검증) / 유니버스 {len(uni)} / 후보 {len(cands)}")
        for c in cands:
            print(f"  {c['name']}({c['symbol']}) 점수 {c['score']} @ ₩{c['price']:,.0f}")
    else:
        print(json.dumps(_ledger(), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
