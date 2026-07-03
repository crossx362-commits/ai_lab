#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""열지도(트리맵) 데이터 수집 — 국장(KIS)·미장(야후) 주요종목 등락·거래대금.

트레이딩뷰 스타일 히트맵용: 섹터별 그룹 / 등락률 색 / 거래대금 크기.
대시보드가 KIS·야후를 직접 안 부르도록 캐시(somi_heatmap.json)에 기록(대시보드는 stdlib 유지).

실행:
  python heatmap_collector.py            # 1회 수집 + 캐시 기록
  python heatmap_collector.py --daemon   # N분 주기 갱신
"""
from __future__ import annotations

import json
import os
import sys
import time
import urllib.request
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
load_env(str(PROJECT_ROOT))

CACHE = PROJECT_ROOT / "output" / "cache" / "somi_heatmap.json"

# 국장 주요종목 — 섹터: [(코드, 이름), ...]
KR_SECTORS = {
    "반도체": [("005930", "삼성전자"), ("000660", "SK하이닉스"), ("042700", "한미반도체")],
    "2차전지": [("373220", "LG에너지솔루션"), ("006400", "삼성SDI"), ("051910", "LG화학"),
                ("086520", "에코프로"), ("247540", "에코프로비엠"), ("003670", "포스코퓨처엠"),
                ("005490", "POSCO홀딩스")],
    "자동차": [("005380", "현대차"), ("000270", "기아"), ("012330", "현대모비스"), ("086280", "현대글로비스")],
    "바이오": [("207940", "삼성바이오로직스"), ("068270", "셀트리온"), ("196170", "알테오젠")],
    "인터넷·게임": [("035420", "NAVER"), ("035720", "카카오"), ("259960", "크래프톤"),
                    ("036570", "엔씨소프트"), ("352820", "하이브"), ("323410", "카카오뱅크")],
    "금융": [("105560", "KB금융"), ("055550", "신한지주"), ("032830", "삼성생명"),
             ("138040", "메리츠금융지주"), ("024110", "기업은행")],
    "IT·전자": [("009150", "삼성전기"), ("011070", "LG이노텍")],
    "방산·조선·해운": [("012450", "한화에어로스페이스"), ("034020", "두산에너빌리티"), ("011200", "HMM")],
    "지주·소재": [("402340", "SK스퀘어"), ("028260", "삼성물산"), ("003550", "LG")],
    "통신·유틸": [("017670", "SK텔레콤"), ("030200", "KT"), ("015760", "한국전력")],
    "소비": [("090430", "아모레퍼시픽")],
}

# 미장 주요종목 — 섹터: [(티커, 이름), ...]
US_SECTORS = {
    "메가캡테크": [("AAPL", "Apple"), ("MSFT", "Microsoft"), ("NVDA", "Nvidia"), ("GOOGL", "Alphabet"),
                   ("AMZN", "Amazon"), ("META", "Meta"), ("TSLA", "Tesla"), ("AVGO", "Broadcom")],
    "반도체": [("AMD", "AMD"), ("QCOM", "Qualcomm"), ("MU", "Micron"), ("INTC", "Intel"),
               ("ARM", "Arm"), ("SMCI", "Supermicro"), ("TSM", "TSMC")],
    "소프트웨어·플랫폼": [("CRM", "Salesforce"), ("ORCL", "Oracle"), ("ADBE", "Adobe"), ("NFLX", "Netflix"),
                         ("UBER", "Uber"), ("PLTR", "Palantir"), ("SNOW", "Snowflake"), ("SHOP", "Shopify"),
                         ("COIN", "Coinbase")],
    "소비·헬스": [("COST", "Costco"), ("WMT", "Walmart"), ("LLY", "EliLilly"), ("UNH", "UnitedHealth"),
                  ("JNJ", "J&J"), ("MRK", "Merck")],
    "금융": [("JPM", "JPMorgan"), ("BAC", "BofA"), ("GS", "GoldmanSachs"), ("V", "Visa"), ("MA", "Mastercard")],
    "에너지·산업": [("XOM", "Exxon"), ("CVX", "Chevron"), ("BA", "Boeing"), ("CAT", "Caterpillar"), ("GE", "GE")],
}


def _collect_kr() -> list[dict]:
    """국장 — KIS 현재가/등락/거래대금."""
    from somi_kis_reporter import KISClient, num
    kis = KISClient()
    out = []
    for sector, stocks in KR_SECTORS.items():
        for code, name in stocks:
            try:
                q = kis.quote(code)
                out.append({
                    "code": code, "name": name, "sector": sector,
                    "price": num(q.get("stck_prpr")),
                    "change": round(num(q.get("prdy_ctrt")), 2),
                    "value": num(q.get("acml_tr_pbmn")),  # 거래대금(원) — 타일 크기
                })
            except Exception:
                continue
            time.sleep(0.12)
    return out


def _collect_us() -> list[dict]:
    """미장 — 야후 배치 시세(v7 quote)로 한 번에. 실패 시 개별 차트 폴백."""
    syms = [s for stocks in US_SECTORS.values() for s, _ in stocks]
    sec_of = {s: sec for sec, stocks in US_SECTORS.items() for s, _ in stocks}
    name_of = {s: n for stocks in US_SECTORS.values() for s, n in stocks}
    out = []
    try:
        url = "https://query1.finance.yahoo.com/v7/finance/quote?symbols=" + ",".join(syms)
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=15) as r:
            res = json.loads(r.read())["quoteResponse"]["result"]
        for q in res:
            s = q.get("symbol")
            price = q.get("regularMarketPrice") or 0
            vol = q.get("regularMarketVolume") or 0
            out.append({
                "code": s, "name": name_of.get(s, s), "sector": sec_of.get(s, "기타"),
                "price": price, "change": round(q.get("regularMarketChangePercent") or 0, 2),
                "value": price * vol,  # 거래대금 근사(USD)
            })
    except Exception:
        # 폴백: 개별 차트(range=2d)로 등락 계산
        for s in syms:
            try:
                url = f"https://query1.finance.yahoo.com/v8/finance/chart/{s}?range=2d&interval=1d"
                req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
                with urllib.request.urlopen(req, timeout=12) as r:
                    d = json.loads(r.read())["chart"]["result"][0]
                cl = [c for c in d["indicators"]["quote"][0]["close"] if c is not None]
                vol = [v for v in d["indicators"]["quote"][0]["volume"] if v is not None]
                if len(cl) >= 2:
                    chg = (cl[-1] - cl[-2]) / cl[-2] * 100
                    out.append({"code": s, "name": name_of.get(s, s), "sector": sec_of.get(s, "기타"),
                                "price": round(cl[-1], 2), "change": round(chg, 2),
                                "value": cl[-1] * (vol[-1] if vol else 0)})
            except Exception:
                continue
            time.sleep(0.1)
    return out


def build() -> dict:
    data = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "kr": _collect_kr(), "us": _collect_us()}
    try:
        tmp = CACHE.with_name(CACHE.name + ".tmp")
        tmp.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
        os.replace(tmp, CACHE)
    except Exception as e:
        print(f"캐시 기록 실패: {e}")
    print(f"[열지도] 국장 {len(data['kr'])}종목 · 미장 {len(data['us'])}종목 수집 ({data['ts']})")
    return data


def main() -> None:
    if "--daemon" in sys.argv:
        interval = int(os.getenv("HEATMAP_INTERVAL", "180"))  # 3분
        print(f"[열지도] 데몬 시작 (갱신 {interval}초)")
        while True:
            try:
                build()
            except Exception as e:
                print(f"[열지도] 수집 오류: {e}")
            time.sleep(interval)
    else:
        build()


if __name__ == "__main__":
    main()
