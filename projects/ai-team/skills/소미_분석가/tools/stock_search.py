#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Stock symbol search using KIS API"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env


load_env(str(PROJECT_ROOT))


# 주요 종목명 → 종목코드 (KIS 검색 API가 한글명 검색을 지원하지 않아 로컬 맵 사용)
MAJOR_STOCKS: dict[str, str] = {
    "삼성전자": "005930",
    "SK하이닉스": "000660",
    "SK스퀘어": "402340",
    "카카오": "035720",
    "네이버": "035420",
    "NAVER": "035420",
    "현대차": "005380",
    "LG전자": "066570",
    "LG에너지솔루션": "373220",
    "삼성바이오로직스": "207940",
    "현대모비스": "012330",
    "POSCO홀딩스": "005490",
    "포스코홀딩스": "005490",
    "삼성SDI": "006400",
    "기아": "000270",
    "셀트리온": "068270",
    "KB금융": "105560",
    "신한지주": "055550",
    "카카오뱅크": "323410",
    "크래프톤": "259960",
    "우리기술": "032820",
}

# 종목 별칭 → 정식 종목명
STOCK_NAME_ALIASES: dict[str, str] = {
    "삼전": "삼성전자",
    "삼성": "삼성전자",
    "SK하닉": "SK하이닉스",
    "하닉": "SK하이닉스",
    "하이닉스": "SK하이닉스",
    "스퀘어": "SK스퀘어",
}


def naver_search(query: str) -> tuple[str, str] | None:
    """네이버 금융 자동완성으로 종목명 → (종목코드, 정식 종목명). 실패 시 None."""
    q = (query or "").strip()
    if not q:
        return None
    try:
        url = "https://ac.stock.naver.com/ac?" + urllib.parse.urlencode({"q": q, "target": "stock"})
        req = urllib.request.Request(
            url, headers={"User-Agent": "Mozilla/5.0", "Referer": "https://finance.naver.com/"}
        )
        with urllib.request.urlopen(req, timeout=8) as resp:
            data = json.loads(resp.read().decode("utf-8", "replace"))
    except Exception:
        return None

    items = [
        it for it in data.get("items", [])
        if it.get("category") == "stock"
        and it.get("nationCode") == "KOR"
        and str(it.get("code", "")).isdigit()
    ]
    if not items:
        return None
    target = q.replace(" ", "").lower()
    for it in items:  # 정확 일치 우선
        if str(it.get("name", "")).replace(" ", "").lower() == target:
            return it["code"], it["name"]
    return items[0]["code"], items[0]["name"]


def resolve_symbol(query: str) -> tuple[str, str] | None:
    """종목명/별칭 → (종목코드, 정식 종목명). 로컬 맵 → 네이버 검색 순. 실패 시 None."""
    q = (query or "").strip()
    if not q:
        return None
    q = STOCK_NAME_ALIASES.get(q, q)
    # 1) 로컬 주요 종목 정확/대소문자 일치 (오프라인, 즉시)
    if q in MAJOR_STOCKS:
        return MAJOR_STOCKS[q], q
    for name, code in MAJOR_STOCKS.items():
        if q.lower() == name.lower():
            return code, name
    # 2) 네이버 자동완성 (임의 종목 대응)
    online = naver_search(q)
    if online:
        return online
    # 3) 로컬 부분 일치 (가장 짧은 이름 우선)
    matches = [(name, code) for name, code in MAJOR_STOCKS.items() if q.lower() in name.lower()]
    if matches:
        name, code = min(matches, key=lambda x: len(x[0]))
        return code, name
    return None


def search_stock(query: str) -> str:
    """종목명/별칭/코드 → 종목코드 검색. 로컬 맵 → 네이버 자동완성(한글명 검색)."""
    query = query.strip()
    if not query:
        return "❌ 검색어를 입력하세요."

    # KIS PDNO 검색은 한글명 조회가 안 되므로 resolve_symbol(로컬 맵 → 네이버) 사용
    hit = resolve_symbol(query)
    if hit:
        code, name = hit
        return f"🔍 '{query}' 검색 결과:\n• {name} ({code})"
    return _fallback_search(query)


def _fallback_search(query: str) -> str:
    """KIS API 실패 시 로컬 주요 종목 맵에서 검색"""
    q = STOCK_NAME_ALIASES.get(query.strip(), query.strip())
    # 부분 일치 검색
    matches = [(name, code) for name, code in MAJOR_STOCKS.items() if q.lower() in name.lower()]

    if not matches:
        return f"❌ '{query}' 검색 결과가 없습니다.\n💡 주요 종목: 삼성전자, SK하이닉스, 카카오, 네이버, 현대차 등"

    results = [f"• {name} ({code})" for name, code in matches]
    return f"🔍 '{query}' 검색 결과:\n" + "\n".join(results)


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="종목명으로 종목코드 검색")
    parser.add_argument("query", help="검색어 (예: 삼성전자, 삼전)")
    args = parser.parse_args()

    print(search_stock(args.query))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
