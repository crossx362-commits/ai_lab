#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Signal market intelligence agent.

Produces a stable market snapshot for Dave and Leo.
The output keeps the existing reports/research/market_pulse.json contract for
compatibility, while the runtime identity is now Signal.
"""

from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Any

try:
    import requests
except Exception:  # pragma: no cover
    requests = None

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(WORKSPACE_ROOT))

REPORTS_DIR = WORKSPACE_ROOT / "reports" / "research"
SIGNAL_FILE = REPORTS_DIR / "market_signal.json"
COMPAT_FILE = REPORTS_DIR / "market_pulse.json"
HARNESS_COMPAT_FILE = REPORTS_DIR / "crypto_market_intel.json"
STATE_FILE = REPORTS_DIR / "market_signal_state.json"
COMPAT_STATE_FILE = REPORTS_DIR / "pulse_state.json"
RUN_LOCK_FILE = REPORTS_DIR / ".market_signal_run.lock"

KST = timezone(timedelta(hours=9))
DEFAULT_INTERVAL_SECONDS = int(os.getenv("SIGNAL_INTERVAL_SECONDS", "600"))
MAX_STALE_LOCK_SECONDS = int(os.getenv("SIGNAL_STALE_LOCK_SECONDS", "180"))


def log(message: str) -> None:
    print(f"[Signal] {datetime.now(KST).strftime('%Y-%m-%d %H:%M:%S')} {message}", flush=True)


def safe_float(value: Any, default: float = 0.0) -> float:
    try:
        return float(value)
    except Exception:
        return default


def http_json(url: str, timeout: int = 8) -> Any:
    if requests is None:
        raise RuntimeError("requests module is unavailable")
    response = requests.get(url, timeout=timeout, headers={"User-Agent": "ai-lab-signal/1.0"})
    response.raise_for_status()
    return response.json()


def acquire_file_lock() -> int | None:
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    now = time.time()
    if RUN_LOCK_FILE.exists() and now - RUN_LOCK_FILE.stat().st_mtime > MAX_STALE_LOCK_SECONDS:
        try:
            RUN_LOCK_FILE.unlink()
        except OSError:
            pass
    try:
        fd = os.open(str(RUN_LOCK_FILE), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        os.write(fd, str(os.getpid()).encode("ascii", errors="ignore"))
        return fd
    except FileExistsError:
        return None


def release_file_lock(fd: int | None) -> None:
    if fd is not None:
        try:
            os.close(fd)
        except OSError:
            pass
        try:
            RUN_LOCK_FILE.unlink()
        except OSError:
            pass


def get_crypto_signals() -> dict[str, Any]:
    crypto: dict[str, Any] = {}

    try:
        data = http_json("https://api.alternative.me/fng/?limit=1")["data"][0]
        value = int(data["value"])
        crypto["fear_greed"] = {
            "value": value,
            "label": data.get("value_classification", "unknown"),
            "signal": "BUY" if value <= 20 else "SELL" if value >= 75 else "NEUTRAL",
        }
    except Exception as exc:
        crypto["fear_greed"] = {"error": str(exc), "signal": "NEUTRAL"}

    try:
        upbit = http_json("https://api.upbit.com/v1/ticker?markets=KRW-BTC")[0]
        upbit_krw = safe_float(upbit.get("trade_price"))
        binance = http_json("https://api.binance.com/api/v3/ticker/price?symbol=BTCUSDT")
        btc_usdt = safe_float(binance.get("price"))
        fx = safe_float(os.getenv("USD_KRW_RATE"), 1300.0)
        offshore_krw = btc_usdt * fx
        premium = ((upbit_krw - offshore_krw) / offshore_krw) * 100 if offshore_krw else 0.0
        crypto["kimchi_premium"] = {
            "value": round(premium, 2),
            "signal": "SELL" if premium >= 5 else "BUY" if premium <= -3 else "NEUTRAL",
        }
    except Exception as exc:
        crypto["kimchi_premium"] = {"error": str(exc), "signal": "NEUTRAL"}

    crypto["top_coins"] = score_upbit_tickers()
    return crypto


def score_upbit_tickers() -> list[dict[str, Any]]:
    tickers = ["KRW-BTC", "KRW-ETH", "KRW-SOL", "KRW-XRP", "KRW-DOGE", "KRW-ADA", "KRW-AVAX", "KRW-LINK"]
    try:
        import pyupbit
    except Exception as exc:
        log(f"pyupbit unavailable for ticker scores: {exc}")
        return []

    scored: list[dict[str, Any]] = []
    for ticker in tickers:
        try:
            df = pyupbit.get_ohlcv(ticker, interval="day", count=21)
            if df is None or len(df) < 6:
                continue
            close = float(df["close"].iloc[-1])
            prev = float(df["close"].iloc[-2])
            ma5 = float(df["close"].rolling(5).mean().iloc[-1])
            ma20 = float(df["close"].rolling(20).mean().iloc[-1])
            volume = float(df["volume"].iloc[-1])
            avg_volume = float(df["volume"].tail(20).mean())
            change = ((close - prev) / prev) * 100 if prev else 0.0

            score = 0
            if close > ma5:
                score += 25
            if close > ma20:
                score += 25
            if change > 1.0:
                score += 20
            if avg_volume and volume > avg_volume * 1.5:
                score += 20
            if change < -3.0:
                score -= 20

            scored.append({"ticker": ticker, "score": max(0, min(100, score)), "change": round(change, 2)})
        except Exception as exc:
            log(f"ticker score failed {ticker}: {exc}")
    scored.sort(key=lambda row: (row["score"], row["change"]), reverse=True)
    return scored[:8]


def get_stock_signals() -> dict[str, Any]:
    # Keep stock section structurally present. KIS is optional and noisy, so
    # failure here must not restart or kill Signal.
    try:
        dave_tools = AI_TEAM_ROOT / "skills" / "데이브_주식" / "tools"
        sys.path.insert(0, str(dave_tools))
        from kis_client import KISClient

        client = KISClient()
        kospi = client.get_index_price("0001").get("output", {})
        kosdaq = client.get_index_price("1001").get("output", {})
        indexes = {
            "kospi": {
                "value": safe_float(kospi.get("bstp_nmix_prpr")),
                "change": safe_float(kospi.get("bstp_nmix_prdy_ctrt")),
            },
            "kosdaq": {
                "value": safe_float(kosdaq.get("bstp_nmix_prpr")),
                "change": safe_float(kosdaq.get("bstp_nmix_prdy_ctrt")),
            },
        }
        avg_change = (indexes["kospi"]["change"] + indexes["kosdaq"]["change"]) / 2
        sentiment = "BULLISH" if avg_change > 1 else "BEARISH" if avg_change < -1 else "NEUTRAL"
        return {"indexes": indexes, "sentiment": sentiment}
    except Exception as exc:
        return {"error": str(exc), "sentiment": "UNKNOWN"}


def summarize(data: dict[str, Any]) -> str:
    crypto = data.get("crypto", {})
    fg = crypto.get("fear_greed", {})
    kp = crypto.get("kimchi_premium", {})
    coins = crypto.get("top_coins", [])
    top = ", ".join(f"{c['ticker'].replace('KRW-', '')}:{c['score']}" for c in coins[:3]) or "none"
    risks = []
    if safe_float(kp.get("value")) >= 5:
        risks.append("kimchi premium high")
    if fg.get("signal") == "SELL":
        risks.append("fear-greed overheated")
    if not risks:
        risks.append("no major market-wide block")
    return (
        f"Fear/Greed {fg.get('value', 'n/a')} {fg.get('signal', 'NEUTRAL')}; "
        f"kimchi {kp.get('value', 'n/a')}% {kp.get('signal', 'NEUTRAL')}; "
        f"top coins {top}; risk: {', '.join(risks)}."
    )


def write_outputs(data: dict[str, Any]) -> None:
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    text = json.dumps(data, ensure_ascii=False, indent=2)
    SIGNAL_FILE.write_text(text, encoding="utf-8")
    COMPAT_FILE.write_text(text, encoding="utf-8")
    HARNESS_COMPAT_FILE.write_text(text, encoding="utf-8")


def notify_on_change(data: dict[str, Any]) -> None:
    current = {
        "fear_greed": data.get("crypto", {}).get("fear_greed", {}).get("signal"),
        "kimchi_premium": data.get("crypto", {}).get("kimchi_premium", {}).get("signal"),
    }
    previous: dict[str, Any] = {}
    if STATE_FILE.exists():
        try:
            previous = json.loads(STATE_FILE.read_text(encoding="utf-8"))
        except Exception:
            previous = {}
    STATE_FILE.write_text(json.dumps(current, ensure_ascii=False), encoding="utf-8")
    COMPAT_STATE_FILE.write_text(json.dumps(current, ensure_ascii=False), encoding="utf-8")

    changes = [key for key, value in current.items() if value and value != previous.get(key) and value != "NEUTRAL"]
    if changes:
        send("[Signal] Market signal changed\n" + data.get("ai_analysis", summarize(data)), silent=True)


def run_once(notify: bool = False) -> dict[str, Any] | None:
    fd = acquire_file_lock()
    if fd is None:
        log("another collection is already running; skipping")
        return None
    try:
        data = {
            "agent": "시그널",
            "agent_slug": "signal",
            "timestamp": datetime.now(KST).isoformat(),
            "crypto": get_crypto_signals(),
            "stock": get_stock_signals(),
        }
        data["ai_analysis"] = summarize(data)
        write_outputs(data)
        if notify:
            notify_on_change(data)
        log(f"wrote {COMPAT_FILE}")
        return data
    finally:
        release_file_lock(fd)


def daemon() -> None:
    log(f"daemon started interval={DEFAULT_INTERVAL_SECONDS}s")
    with ProcessLock("signal"):
        failures = 0
        while True:
            try:
                run_once(notify=True)
                failures = 0
                time.sleep(DEFAULT_INTERVAL_SECONDS)
            except KeyboardInterrupt:
                log("stopped")
                return
            except Exception as exc:
                failures += 1
                log(f"cycle failed ({failures}): {exc}")
                time.sleep(min(60 * failures, 600))


def main() -> None:
    if "--daemon" in sys.argv:
        daemon()
    else:
        run_once(notify="--notify" in sys.argv)


if __name__ == "__main__":
    main()
