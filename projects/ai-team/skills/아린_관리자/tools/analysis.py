import os
import json
import requests
from typing import Dict

def run_analysis_and_deepsearch(report_path: str) -> Dict:
    """Analyze the given report and perform a web deep‑search.
    This is a lightweight stub: it extracts simple keywords from the
    report (if the file exists) and, when GOOGLE_SEARCH_API_KEY and
    GOOGLE_SEARCH_CX are set, queries the Google Custom Search JSON API.
    Returns a dictionary with keys: `summary`, `sources`, `insights`.
    """
    # 1️⃣ Extract keywords – very naive implementation
    keywords = []
    if report_path and os.path.isfile(report_path):
        try:
            txt = open(report_path, "r", encoding="utf-8").read()
            # split by whitespace and pick unique words longer than 4 chars
            words = {w.strip().lower() for w in txt.split() if len(w) > 4}
            keywords = list(words)[:10]
        except Exception as e:
            print(f"⚠️ 보고서 읽기 실패: {e}")
    else:
        print("⚠️ 보고서 경로가 유효하지 않음 – 기본 키워드 사용.")
        keywords = ["trend", "instagram", "visual", "style"]

    # 2️⃣ Web deep‑search (Google Custom Search) – optional
    api_key = os.getenv("GOOGLE_SEARCH_API_KEY")
    cx = os.getenv("GOOGLE_SEARCH_CX")
    sources = []
    insights = []
    summary = ""
    if api_key and cx:
        try:
            query = " ".join(keywords)
            url = "https://www.googleapis.com/customsearch/v1"
            params = {"key": api_key, "cx": cx, "q": query, "num": 3}
            resp = requests.get(url, params=params, timeout=10)
            data = resp.json()
            for item in data.get("items", []):
                title = item.get("title", "")
                link = item.get("link", "")
                snippet = item.get("snippet", "")
                sources.append({"title": title, "url": link})
                insights.append(snippet)
            summary = f"Found {len(sources)} relevant sources for query '{query}'."
        except Exception as e:
            print(f"⚠️ 웹 딥서치 실패: {e}")
    else:
        print("⚠️ Google Search API credentials 미설정 – 딥서치 건너뜀.")
        summary = "No deep‑search performed (missing credentials)."

    return {"summary": summary, "sources": sources, "insights": insights, "keywords": keywords}
