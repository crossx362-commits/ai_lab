#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""시장조사 공통 모듈 — HTTP·DART 공시·환율·지수·지역 브리프 저장.

지역 조사관(행크/유나/레온)과 시장 데스크가 공유한다. 키가 필요한 소스는
DART(보유)뿐이며, 환율(open.er-api.com)·지수(stooq)는 키 없이 호출한다.
실패는 조용히 넘기고(None/[]) 호출측이 폴백하도록 한다.
"""

from __future__ import annotations

import json
import os
import urllib.parse
import urllib.request
from datetime import datetime, timedelta
from pathlib import Path

_HERE = Path(__file__).resolve().parent
PROJECT_ROOT = _HERE.parents[2]              # _shared → ai-team → projects → ai_lab
RESEARCH_DIR = PROJECT_ROOT / "output" / "research"
CACHE_DIR = PROJECT_ROOT / "output" / "cache"


# ── HTTP ────────────────────────────────────────────────────────────────────
def _get(url: str, timeout: int = 12) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (ai-team research)"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def get_json(url: str, timeout: int = 12) -> dict:
    return json.loads(_get(url, timeout))


# ── watchlist (소미와 공유) ─────────────────────────────────────────────────
def load_watchlist() -> dict[str, str]:
    f = CACHE_DIR / "somi_watchlist.json"
    try:
        return json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
    except Exception:
        return {}


# ── DART 공시 ───────────────────────────────────────────────────────────────
def dart_recent(stock_codes: set[str], days: int = 2, max_pages: int = 10) -> list[dict]:
    """최근 `days`일 공시 중 watchlist 종목(stock_code)에 해당하는 항목만 반환."""
    key = os.getenv("DART_API_KEY", "").strip()
    if not key or not stock_codes:
        return []
    bgn = (datetime.now() - timedelta(days=days)).strftime("%Y%m%d")
    out: list[dict] = []
    for page in range(1, max_pages + 1):
        url = (
            "https://opendart.fss.or.kr/api/list.json"
            f"?crtfc_key={key}&bgn_de={bgn}&page_no={page}&page_count=100"
        )
        try:
            d = get_json(url)
        except Exception:
            break
        if d.get("status") != "000":
            break
        for it in d.get("list", []):
            if it.get("stock_code") in stock_codes:
                out.append({
                    "name": it.get("corp_name", ""),
                    "code": it.get("stock_code", ""),
                    "report": (it.get("report_nm") or "").strip(),
                    "date": it.get("rcept_dt", ""),
                    "rcept_no": it.get("rcept_no", ""),
                    "url": f"https://dart.fss.or.kr/dsaf001/main.do?rcpNo={it.get('rcept_no','')}",
                })
        if page >= int(d.get("total_page", 1) or 1):
            break
    return out


# ── 환율 ────────────────────────────────────────────────────────────────────
def fx(*codes: str) -> dict[str, float | None]:
    """USD 기준 환율. 예: fx('KRW','JPY','EUR') → {'KRW':1543.5, ...}."""
    try:
        d = get_json("https://open.er-api.com/v6/latest/USD")
        rates = d.get("rates", {})
        return {c: rates.get(c) for c in codes}
    except Exception:
        return {c: None for c in codes}


# ── 지수 (stooq, 실패 시 None) ──────────────────────────────────────────────
def index_quote(symbol: str) -> dict | None:
    """stooq CSV로 지수 종가 조회. 형식: Symbol,Date,Time,Open,High,Low,Close,Volume."""
    try:
        csv = _get(f"https://stooq.com/q/l/?s={urllib.parse.quote(symbol)}&f=sd2t2ohlcv&h&e=csv")
        lines = [ln for ln in csv.strip().splitlines() if ln.strip()]
        if len(lines) >= 2:
            v = lines[1].split(",")
            if len(v) >= 7 and v[6] not in ("", "N/D"):
                return {"symbol": symbol, "date": v[1], "close": v[6]}
    except Exception:
        pass
    return None


def indices(symbol_map: dict[str, str]) -> dict[str, dict | None]:
    """{표시명: stooq심볼} → {표시명: quote|None}."""
    return {name: index_quote(sym) for name, sym in symbol_map.items()}


# ── 지역 브리프 저장/로드 ───────────────────────────────────────────────────
def save_region(region: str, payload: dict) -> Path:
    RESEARCH_DIR.mkdir(parents=True, exist_ok=True)
    payload = dict(payload)
    payload["region"] = region
    payload["updated"] = datetime.now().isoformat(timespec="seconds")
    path = RESEARCH_DIR / f"region_{region}.json"
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def load_region(region: str) -> dict:
    f = RESEARCH_DIR / f"region_{region}.json"
    try:
        return json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
    except Exception:
        return {}


def save_market_brief(text_md: str, payload: dict) -> Path:
    RESEARCH_DIR.mkdir(parents=True, exist_ok=True)
    (RESEARCH_DIR / "market_brief.md").write_text(text_md, encoding="utf-8")
    payload = dict(payload)
    payload["updated"] = datetime.now().isoformat(timespec="seconds")
    path = RESEARCH_DIR / "market_brief.json"
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return path
