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
import urllib.error
import urllib.parse
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


# 지수 요약(헤더 칩) — 국장: KIS ETF 프록시, 미장: 야후
KR_INDICES = [("069500", "KOSPI200"), ("229200", "코스닥150")]
US_INDICES = [("SPY", "S&P500"), ("QQQ", "나스닥100"), ("DIA", "다우"), ("^VIX", "VIX")]

# 크립토(업비트 KRW, 공개 API — 인증 불필요). 메이저는 고정 섹터, 나머지는 24h 거래대금 상위를 '알트'로.
CRYPTO_MAJORS = {"KRW-BTC": "비트코인", "KRW-ETH": "이더리움", "KRW-XRP": "리플", "KRW-SOL": "솔라나",
                 "KRW-ADA": "에이다", "KRW-DOGE": "도지코인", "KRW-TRX": "트론", "KRW-AVAX": "아발란체"}
CRYPTO_EXCLUDE = {"KRW-USDT", "KRW-USDC", "KRW-DAI", "KRW-TUSD"}   # 스테이블 제외
CRYPTO_ALT_N = int(os.getenv("HEATMAP_CRYPTO_ALT_N", "16"))         # 알트 표시 수


def _collect_kr() -> tuple[list[dict], list[dict]]:
    """국장 — KIS 현재가/등락/거래대금/시가총액(주가×상장주식수) + 지수 프록시."""
    from somi_kis_reporter import KISClient, num
    kis = KISClient()
    out = []
    for sector, stocks in KR_SECTORS.items():
        for code, name in stocks:
            try:
                q = kis.quote(code)
                price = num(q.get("stck_prpr"))
                listed = num(q.get("lstn_stcn"))
                out.append({
                    "code": code, "name": name, "sector": sector,
                    "price": price,
                    "change": round(num(q.get("prdy_ctrt")), 2),
                    "value": num(q.get("acml_tr_pbmn")),        # 거래대금(원)
                    "mcap": round(price * listed) if listed else None,  # 시가총액(원)
                })
            except Exception:
                continue
            time.sleep(0.12)
    idx = []
    for code, label in KR_INDICES:
        try:
            q = kis.quote(code)
            idx.append({"name": label, "price": num(q.get("stck_prpr")),
                        "change": round(num(q.get("prdy_ctrt")), 2)})
        except Exception:
            continue
        time.sleep(0.12)
    return out, idx


_YCRUMB: dict = {}  # {"cookie": str, "crumb": str} — 프로세스 생존 동안 재사용


def _yahoo_crumb() -> tuple[str, str]:
    """야후 v7 API용 쿠키+크럼(2023~ 필수). 실패 시 ("","")."""
    if _YCRUMB.get("crumb"):
        return _YCRUMB["cookie"], _YCRUMB["crumb"]
    try:
        req = urllib.request.Request("https://fc.yahoo.com", headers={"User-Agent": "Mozilla/5.0"})
        try:
            urllib.request.urlopen(req, timeout=10)
            cookie = ""
        except urllib.error.HTTPError as e:   # 404여도 Set-Cookie는 온다
            cookie = e.headers.get("Set-Cookie", "").split(";")[0]
        req = urllib.request.Request("https://query1.finance.yahoo.com/v1/test/getcrumb",
                                     headers={"User-Agent": "Mozilla/5.0", "Cookie": cookie})
        with urllib.request.urlopen(req, timeout=10) as r:
            crumb = r.read().decode().strip()
        _YCRUMB.update({"cookie": cookie, "crumb": crumb})
        return cookie, crumb
    except Exception:
        return "", ""


def _yahoo_quotes(symbols: list[str]) -> dict[str, dict]:
    """야후 v7 배치 시세(시총 포함) → {심볼: quote}. 실패 시 빈 dict(개별 차트 폴백)."""
    try:
        cookie, crumb = _yahoo_crumb()
        url = ("https://query1.finance.yahoo.com/v7/finance/quote?symbols="
               + ",".join(urllib.parse.quote(s) for s in symbols)
               + (f"&crumb={urllib.parse.quote(crumb)}" if crumb else ""))
        headers = {"User-Agent": "Mozilla/5.0"}
        if cookie:
            headers["Cookie"] = cookie
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req, timeout=15) as r:
            res = json.loads(r.read())["quoteResponse"]["result"]
        return {q.get("symbol"): q for q in res}
    except Exception:
        return {}


def _yahoo_chart_fallback(sym: str) -> tuple[float, float, float] | None:
    """개별 차트(range=2d)로 (종가, 등락%, 거래량) 폴백."""
    try:
        url = f"https://query1.finance.yahoo.com/v8/finance/chart/{urllib.parse.quote(sym)}?range=2d&interval=1d"
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=12) as r:
            d = json.loads(r.read())["chart"]["result"][0]
        cl = [c for c in d["indicators"]["quote"][0]["close"] if c is not None]
        vol = [v for v in d["indicators"]["quote"][0]["volume"] if v is not None]
        if len(cl) >= 2:
            return round(cl[-1], 2), round((cl[-1] - cl[-2]) / cl[-2] * 100, 2), (vol[-1] if vol else 0)
    except Exception:
        pass
    return None


def _collect_us() -> tuple[list[dict], list[dict]]:
    """미장 — 야후 배치 시세(시총 포함) + 지수. 배치 실패 시 개별 차트 폴백."""
    syms = [s for stocks in US_SECTORS.values() for s, _ in stocks]
    sec_of = {s: sec for sec, stocks in US_SECTORS.items() for s, _ in stocks}
    name_of = {s: n for stocks in US_SECTORS.values() for s, n in stocks}
    quotes = _yahoo_quotes(syms + [s for s, _ in US_INDICES])
    out = []
    for s in syms:
        q = quotes.get(s)
        if q:
            price = q.get("regularMarketPrice") or 0
            vol = q.get("regularMarketVolume") or 0
            out.append({
                "code": s, "name": name_of.get(s, s), "sector": sec_of.get(s, "기타"),
                "price": price, "change": round(q.get("regularMarketChangePercent") or 0, 2),
                "value": price * vol,                      # 거래대금 근사(USD)
                "mcap": q.get("marketCap"),                # 시가총액(USD)
            })
        else:
            fb = _yahoo_chart_fallback(s)
            if fb:
                out.append({"code": s, "name": name_of.get(s, s), "sector": sec_of.get(s, "기타"),
                            "price": fb[0], "change": fb[1], "value": fb[0] * fb[2], "mcap": None})
            time.sleep(0.1)
    idx = []
    for s, label in US_INDICES:
        q = quotes.get(s)
        if q:
            idx.append({"name": label, "price": q.get("regularMarketPrice"),
                        "change": round(q.get("regularMarketChangePercent") or 0, 2)})
        else:
            fb = _yahoo_chart_fallback(s)
            if fb:
                idx.append({"name": label, "price": fb[0], "change": fb[1]})
    return out, idx


def _collect_crypto() -> tuple[list[dict], list[dict]]:
    """크립토(업비트 KRW) — 전 마켓 현재가/등락/24h 거래대금 배치 조회. 시총은 미제공(None,
    트리맵은 거래대금으로 폴백). 지수 칩은 BTC·ETH. 실패 시 빈 리스트(열지도에서 생략)."""
    try:
        req = urllib.request.Request("https://api.upbit.com/v1/market/all?isDetails=false",
                                     headers={"Accept": "application/json", "User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=15) as r:
            mkts = json.loads(r.read())
        krw = {m["market"]: m.get("korean_name", m["market"])
               for m in mkts if m["market"].startswith("KRW-") and m["market"] not in CRYPTO_EXCLUDE}
        rows: list[dict] = []
        codes = list(krw)
        for i in range(0, len(codes), 100):
            chunk = ",".join(codes[i:i + 100])
            req = urllib.request.Request(f"https://api.upbit.com/v1/ticker?markets={chunk}",
                                         headers={"Accept": "application/json", "User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=15) as r:
                rows += json.loads(r.read())
            time.sleep(0.15)
        by = {t["market"]: t for t in rows}
        out = []
        # 메이저(고정) + 알트(24h 거래대금 상위)
        alt_pool = sorted((m for m in by if m not in CRYPTO_MAJORS),
                          key=lambda m: float(by[m].get("acc_trade_price_24h", 0)), reverse=True)
        for mkt in list(CRYPTO_MAJORS) + alt_pool[:CRYPTO_ALT_N]:
            t = by.get(mkt)
            if not t:
                continue
            out.append({
                "code": mkt, "name": CRYPTO_MAJORS.get(mkt, krw.get(mkt, mkt)),
                "sector": "메이저" if mkt in CRYPTO_MAJORS else "알트",
                "price": float(t.get("trade_price") or 0),
                "change": round(float(t.get("signed_change_rate") or 0) * 100, 2),
                "value": float(t.get("acc_trade_price_24h") or 0),   # 24h 거래대금(원)
                "mcap": None,                                        # 업비트 미제공 — 거래대금 폴백
            })
        idx = [{"name": lbl, "price": float(by[m]["trade_price"]),
                "change": round(float(by[m].get("signed_change_rate") or 0) * 100, 2)}
               for m, lbl in (("KRW-BTC", "BTC"), ("KRW-ETH", "ETH")) if m in by]
        return out, idx
    except Exception as e:
        print(f"[열지도] 크립토 수집 실패: {e}")
        return [], []


def build() -> dict:
    kr, kr_idx = _collect_kr()
    us, us_idx = _collect_us()
    crypto, crypto_idx = _collect_crypto()
    data = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "kr": kr, "us": us, "crypto": crypto,
            "indices": {"kr": kr_idx, "us": us_idx, "crypto": crypto_idx}}
    try:
        tmp = CACHE.with_name(CACHE.name + ".tmp")
        tmp.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
        os.replace(tmp, CACHE)
    except Exception as e:
        print(f"캐시 기록 실패: {e}")
    print(f"[열지도] 국장 {len(data['kr'])} · 미장 {len(data['us'])} · 크립토 {len(data['crypto'])}종목 수집 ({data['ts']})")
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
