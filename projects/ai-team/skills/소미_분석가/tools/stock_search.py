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
from somi_kis_reporter import KISClient


load_env(str(PROJECT_ROOT))


def search_stock(query: str) -> str:
    """종목명으로 종목코드 검색"""
    query = query.strip()
    if not query:
        return "❌ 검색어를 입력하세요."

    # 별칭 처리
    aliases = {
        "삼전": "삼성전자",
        "삼성": "삼성전자",
        "SK하닉": "SK하이닉스",
        "하닉": "SK하이닉스",
    }
    query = aliases.get(query, query)

    try:
        kis = KISClient()

        # KIS 종목 검색 API
        data = kis.get(
            "uapi/domestic-stock/v1/quotations/search-stock-info",
            "CTPF1604R",
            {
                "PRDT_TYPE_CD": "300",  # 주식
                "PDNO": query,
            },
        )

        output = data.get("output") or []
        if not isinstance(output, list):
            output = [output]

        if not output:
            return f"❌ '{query}' 검색 결과가 없습니다."

        # 최대 5개 결과 반환
        results = []
        for item in output[:5]:
            symbol = item.get("pdno", "").strip()
            name = item.get("prdt_name", "").strip()
            if symbol and name:
                results.append(f"• {name} ({symbol})")

        if not results:
            return f"❌ '{query}' 검색 결과가 없습니다."

        header = f"🔍 '{query}' 검색 결과:\n"
        return header + "\n".join(results)

    except Exception as exc:
        # KIS API가 검색을 지원하지 않을 수 있으므로 fallback
        return _fallback_search(query)


def _fallback_search(query: str) -> str:
    """KIS API 실패 시 하드코딩된 주요 종목 검색"""
    major_stocks = {
        "삼성전자": "005930",
        "SK하이닉스": "000660",
        "카카오": "035720",
        "네이버": "035420",
        "현대차": "005380",
        "LG전자": "066570",
        "삼성바이오로직스": "207940",
        "현대모비스": "012330",
        "POSCO홀딩스": "005490",
        "삼성SDI": "006400",
        "기아": "000270",
        "우리기술": "032820",
    }

    # 부분 일치 검색
    matches = [(name, code) for name, code in major_stocks.items() if query.lower() in name.lower()]

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
