# -*- coding: utf-8 -*-
"""
pyupbit 공개 시세 최소 호환 모듈.

pyupbit 설치가 없는 환경에서 시뮬레이션 스캔만 가능하게 합니다.
주문/잔고 기능은 제공하지 않습니다.
"""

from __future__ import annotations

import json
import time
import urllib.parse
import urllib.request
from datetime import datetime

import pandas as pd


BASE_URL = "https://api.upbit.com/v1"
_LAST_REQUEST_AT = 0.0
_MIN_REQUEST_INTERVAL = 0.14
_PRICE_CACHE = {}
_PRICE_CACHE_TTL = 3.0


def _get_json(path: str, params: dict) -> object:
    global _LAST_REQUEST_AT
    elapsed = time.time() - _LAST_REQUEST_AT
    if elapsed < _MIN_REQUEST_INTERVAL:
        time.sleep(_MIN_REQUEST_INTERVAL - elapsed)

    query = urllib.parse.urlencode(params)
    url = f"{BASE_URL}{path}?{query}"
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(req, timeout=10) as response:
                _LAST_REQUEST_AT = time.time()
                return json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            _LAST_REQUEST_AT = time.time()
            if e.code == 429 and attempt < 2:
                time.sleep(1.0 + attempt)
                continue
            raise


def get_current_price(ticker: str):
    cached = _PRICE_CACHE.get(ticker)
    if cached and time.time() - cached[0] < _PRICE_CACHE_TTL:
        return cached[1]

    data = _get_json("/ticker", {"markets": ticker})
    if isinstance(data, list) and data:
        price = data[0].get("trade_price")
        _PRICE_CACHE[ticker] = (time.time(), price)
        return price
    return None


def get_ohlcv(ticker: str, interval: str = "day", count: int = 200):
    if interval == "day":
        path = "/candles/days"
    elif interval.startswith("minute"):
        unit = interval.replace("minute", "") or "1"
        path = f"/candles/minutes/{unit}"
    else:
        raise ValueError(f"Unsupported interval for fallback: {interval}")

    rows = _get_json(path, {"market": ticker, "count": count})
    if not isinstance(rows, list) or not rows:
        return None

    records = []
    for row in reversed(rows):
        candle_time = row.get("candle_date_time_kst") or row.get("candle_date_time_utc")
        records.append(
            {
                "datetime": datetime.fromisoformat(candle_time),
                "open": row.get("opening_price"),
                "high": row.get("high_price"),
                "low": row.get("low_price"),
                "close": row.get("trade_price"),
                "volume": row.get("candle_acc_trade_volume"),
                "value": row.get("candle_acc_trade_price"),
            }
        )

    df = pd.DataFrame.from_records(records).set_index("datetime")
    df.index.name = "index"
    return df


class Upbit:
    """시뮬레이션용 더미 클라이언트. 실제 주문은 지원하지 않습니다."""

    def __init__(self, access_key: str, secret_key: str):
        self.access_key = access_key
        self.secret_key = secret_key

    def get_balances(self):
        return []

    def get_balance(self, ticker: str):
        return 0.0

    def get_avg_buy_price(self, ticker: str):
        return 0.0

    def buy_market_order(self, ticker: str, krw_amount: float):
        raise RuntimeError("upbit_public fallback does not support live orders")

    def sell_market_order(self, ticker: str, volume: float):
        raise RuntimeError("upbit_public fallback does not support live orders")
