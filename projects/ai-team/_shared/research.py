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
import xml.etree.ElementTree as ET
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


# ── 지수 (Yahoo Finance chart API, 실패 시 None) ────────────────────────────
def index_quote(symbol: str) -> dict | None:
    """Yahoo Finance chart API로 지수 종가·등락률 조회. (stooq는 2026년 차단됨)"""
    try:
        url = (f"https://query1.finance.yahoo.com/v8/finance/chart/"
               f"{urllib.parse.quote(symbol)}?interval=1d&range=5d")
        d = get_json(url)
        m = d["chart"]["result"][0]["meta"]
        px = m.get("regularMarketPrice")
        if px is None:
            return None
        prev = m.get("chartPreviousClose") or m.get("previousClose")
        chg = round((px - prev) / prev * 100, 2) if prev else None
        ts = m.get("regularMarketTime")
        dt = datetime.fromtimestamp(ts) if ts else datetime.now()
        # 신선도 가드: 10일 넘게 묵은 데이터는 폐기(예: 제재로 동결된 러시아 IMOEX 2022년치)
        if (datetime.now() - dt).days > 10:
            return None
        return {"symbol": symbol, "date": dt.strftime("%Y-%m-%d"), "close": px, "chg_pct": chg}
    except Exception:
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


def load_market_brief() -> dict:
    """마켓데스크 종합 브리프(fx·indices·disclosures·comment·web). 없으면 {}."""
    f = RESEARCH_DIR / "market_brief.json"
    try:
        return json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
    except Exception:
        return {}


def market_brief_age_hours() -> float | None:
    """브리프가 몇 시간 전 것인지(신선도 판단). 없으면 None."""
    mb = load_market_brief()
    ts = mb.get("updated")
    if not ts:
        return None
    try:
        return (datetime.now() - datetime.fromisoformat(ts)).total_seconds() / 3600
    except Exception:
        return None


# ── 종목별 이슈 영향도 (공시 기반 LLM 평가, 소미가 소비) ────────────────────
def save_issue_impact(impact: dict) -> Path:
    """{종목코드: {"score": -2~+2, "reason": str}} 저장."""
    RESEARCH_DIR.mkdir(parents=True, exist_ok=True)
    payload = {"updated": datetime.now().isoformat(timespec="seconds"), "impact": impact}
    path = RESEARCH_DIR / "issue_impact.json"
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def load_issue_impact() -> dict:
    f = RESEARCH_DIR / "issue_impact.json"
    try:
        d = json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
        return d.get("impact", {}) if isinstance(d, dict) else {}
    except Exception:
        return {}


# ── 뉴스 (RSS, 키 불필요) ───────────────────────────────────────────────────
def news_rss(url: str, limit: int = 8) -> list[str]:
    """RSS 피드의 기사 제목 목록. 실패 시 빈 리스트."""
    try:
        root = ET.fromstring(_get(url))
        titles = []
        for item in root.iter("item"):
            t = (item.findtext("title") or "").strip()
            if t:
                titles.append(t)
            if len(titles) >= limit:
                break
        return titles
    except Exception:
        return []


# ── 시장 심리 (CNN 공포탐욕, 키 불필요) ─────────────────────────────────────
def fear_greed() -> dict:
    """CNN Fear & Greed Index. {'score': float, 'rating': str} 또는 {}."""
    try:
        d = get_json("https://production.dataviz.cnn.io/index/fearandgreed/graphdata")
        fg = d.get("fear_and_greed", {}) or {}
        score = fg.get("score")
        return {"score": round(float(score), 1) if score is not None else None,
                "rating": fg.get("rating", "")}
    except Exception:
        return {}


# ── 미국 거시 (FRED, 키 조건부) ─────────────────────────────────────────────
def fred_latest(series_id: str) -> str | None:
    """FRED 시계열 최신값. FRED_API_KEY 미보유 시 None."""
    key = os.getenv("FRED_API_KEY", "").strip()
    if not key:
        return None
    try:
        url = (
            "https://api.stlouisfed.org/fred/series/observations"
            f"?series_id={series_id}&api_key={key}&file_type=json&sort_order=desc&limit=1"
        )
        obs = get_json(url).get("observations", [])
        return obs[0].get("value") if obs else None
    except Exception:
        return None


# ── 웹검색 (LLM grounding: Gemini → Claude 폴백) ────────────────────────────
def _gemini_search(query: str, max_tokens: int) -> str:
    key = os.getenv("GEMINI_API_KEY", "").strip()
    if not key:
        return ""
    try:
        url = (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"gemini-2.5-flash:generateContent?key={key}"
        )
        body = {
            "contents": [{"parts": [{"text": query}]}],
            "tools": [{"google_search": {}}],
            # 2.5-flash는 thinking 모델 → thinking이 출력 토큰을 먹어 답변이 잘림.
            # thinkingBudget=0으로 thinking을 끄고 출력 토큰을 답변에 온전히 쓴다.
            "generationConfig": {
                "maxOutputTokens": max_tokens,
                "temperature": 0.3,
                "thinkingConfig": {"thinkingBudget": 0},
            },
        }
        req = urllib.request.Request(
            url, data=json.dumps(body).encode("utf-8"),
            headers={"Content-Type": "application/json"}, method="POST",
        )
        with urllib.request.urlopen(req, timeout=30) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))
        cands = d.get("candidates", [])
        if cands:
            parts = cands[0].get("content", {}).get("parts", [])
            txt = "".join(p.get("text", "") for p in parts).strip()
            # grounding 출처(진위 확인용) 첨부
            gm = cands[0].get("groundingMetadata") or {}
            srcs = []
            for c in (gm.get("groundingChunks") or [])[:5]:
                t = (c.get("web") or {}).get("title", "")
                if t and t not in srcs:
                    srcs.append(t)
            if txt and srcs:
                txt += "\n📎 출처: " + ", ".join(srcs)
            return txt
    except Exception:
        return ""
    return ""


def _claude_search(query: str, max_tokens: int) -> str:
    key = os.getenv("ANTHROPIC_API_KEY", "").strip()
    if not key:
        return ""
    try:
        body = {
            "model": "claude-haiku-4-5-20251001",
            "max_tokens": max_tokens,
            "tools": [{"type": "web_search_20250305", "name": "web_search", "max_uses": 3}],
            "messages": [{"role": "user", "content": query}],
        }
        req = urllib.request.Request(
            "https://api.anthropic.com/v1/messages",
            data=json.dumps(body).encode("utf-8"),
            headers={
                "content-type": "application/json",
                "x-api-key": key,
                "anthropic-version": "2023-06-01",
            },
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=40) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))
        texts = [b.get("text", "") for b in d.get("content", []) if b.get("type") == "text"]
        return "\n".join(t for t in texts if t).strip()
    except Exception:
        return ""


def _gpt_search(query: str, max_tokens: int) -> str:
    key = os.getenv("OPENAI_API_KEY", "").strip()
    if not key:
        return ""
    try:
        body = {
            "model": "gpt-4o-search-preview",
            "messages": [{"role": "user", "content": query}],
            "max_tokens": max_tokens,
        }
        req = urllib.request.Request(
            "https://api.openai.com/v1/chat/completions",
            data=json.dumps(body).encode("utf-8"),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {key}"},
            method="POST",
        )
        with urllib.request.urlopen(req, timeout=40) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))
        return (d["choices"][0]["message"]["content"] or "").strip()
    except Exception:
        return ""


# 호출마다 다른 제공자로 시작 → Gemini 한 곳에 쏠리지 않게 부하 분산.
_WEB_PROVIDERS = [_gemini_search, _claude_search, _gpt_search]
_web_rr = [0]


def web_brief(query: str, max_tokens: int = 800) -> str:
    """실시간 웹 정보를 LLM 검색으로 요약. Gemini(google_search)·Claude(web_search)
    ·GPT(search-preview)를 라운드로빈으로 돌리고, 실패 시 다음 제공자로 폴백한다."""
    n = len(_WEB_PROVIDERS)
    start = _web_rr[0]
    _web_rr[0] = (start + 1) % n
    for i in range(n):
        result = _WEB_PROVIDERS[(start + i) % n](query, max_tokens)
        if result:
            return result
    return ""


# ── 노션 기록 ───────────────────────────────────────────────────────────────
def notion_page(title: str, bullets: list[str]) -> bool:
    """NOTION_DATABASE_ID DB에 간결한 페이지(제목 + 불릿)를 만든다.
    너무 길지 않게 — 불릿은 최대 12개, 각 줄 1800자 컷."""
    key = os.getenv("NOTION_API_KEY", "").strip()
    db = os.getenv("NOTION_DATABASE_ID", "").strip()
    if not key or not db:
        return False
    headers = {
        "Authorization": f"Bearer {key}",
        "Notion-Version": "2022-06-28",
        "Content-Type": "application/json",
    }

    def _api(method: str, url: str, payload: dict | None = None):
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.loads(r.read().decode("utf-8", "replace"))

    # title 속성 이름 동적 조회 (DB마다 다름)
    title_prop = "Name"
    try:
        meta = _api("GET", f"https://api.notion.com/v1/databases/{db}")
        for k, v in (meta.get("properties") or {}).items():
            if v.get("type") == "title":
                title_prop = k
                break
    except Exception:
        pass

    children = [
        {"object": "block", "type": "bulleted_list_item",
         "bulleted_list_item": {"rich_text": [{"type": "text", "text": {"content": (b or "")[:1800]}}]}}
        for b in bullets[:12] if (b or "").strip()
    ]
    payload = {
        "parent": {"database_id": db},
        "properties": {title_prop: {"title": [{"text": {"content": title[:200]}}]}},
        "children": children,
    }
    try:
        _api("POST", "https://api.notion.com/v1/pages", payload)
        return True
    except Exception as exc:
        print(f"  노션 기록 실패: {exc}")
        return False
