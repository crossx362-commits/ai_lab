#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
investment_house.py — 1인 투자하우스 핵심 엔진

Action 1: 종목탐색  (sector → screen → earnings-preview → investment-memo)
Action 2: 추적관찰  (thesis → catalysts → earnings-review)
Action 3: 모닝노트  (morning_note.py로 위임)

Usage:
    python investment_house.py action1 005930 삼성전자
    python investment_house.py action2 000660 SK하이닉스
    python investment_house.py sector  AI반도체
    python investment_house.py morning
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
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

from _shared.env import load_env
from _shared.notify import send
from _shared.llm import gpt, gemini

load_env(str(PROJECT_ROOT))

TOKEN_CACHE  = PROJECT_ROOT / "output" / "cache" / "kis_access_token.json"
THESIS_FILE  = PROJECT_ROOT / "output" / "cache" / "investment_thesis.json"
LOG_FILE     = PROJECT_ROOT / "output" / "bot_logs" / "investment_house.log"


# ─── 유틸 ────────────────────────────────────────────────────────────────────

def log(msg: str) -> None:
    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
    line = f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {msg}"
    print(line, file=sys.stderr, flush=True)
    with LOG_FILE.open("a", encoding="utf-8") as f:
        f.write(line + "\n")


def num(v: object) -> float:
    try:
        return float(str(v or "").replace(",", "").strip())
    except ValueError:
        return 0.0


def llm(prompt: str, max_tokens: int = 800) -> str:
    """GPT → Gemini 폴백"""
    for fn in [gpt, gemini]:
        try:
            result = fn(prompt, max_tokens=max_tokens)
            if result:
                return result.strip()
        except Exception as e:
            log(f"llm 폴백: {e}")
    return "(분석 실패)"


# ─── KIS 클라이언트 ──────────────────────────────────────────────────────────

class KISClient:
    def __init__(self) -> None:
        self.app_key    = os.getenv("KIS_APP_KEY", "").strip()
        self.app_secret = os.getenv("KIS_APP_SECRET", "").strip()
        real_mode = os.getenv("KIS_REAL_MODE", "false").strip().lower() in {"1", "true", "yes"}
        self.base_url = (
            "https://openapi.koreainvestment.com:9443"
            if real_mode else
            "https://openapivts.koreainvestment.com:29443"
        )

    def token(self) -> str:
        try:
            if TOKEN_CACHE.exists():
                d = json.loads(TOKEN_CACHE.read_text(encoding="utf-8"))
                if d.get("access_token") and float(d.get("expires_at", 0)) > time.time():
                    return d["access_token"]
        except Exception:
            pass
        payload = {"grant_type": "client_credentials", "appkey": self.app_key, "appsecret": self.app_secret}
        req = urllib.request.Request(
            f"{self.base_url}/oauth2/tokenP",
            data=json.dumps(payload).encode(),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=15) as r:
            data = json.loads(r.read())
        token = data.get("access_token", "")
        TOKEN_CACHE.parent.mkdir(parents=True, exist_ok=True)
        TOKEN_CACHE.write_text(
            json.dumps({"access_token": token,
                        "expires_at": time.time() + int(data.get("expires_in", 86400)) - 300}),
            encoding="utf-8",
        )
        return token

    def get(self, path: str, tr_id: str, params: dict) -> dict:
        query = urllib.parse.urlencode(params)
        req = urllib.request.Request(
            f"{self.base_url}/{path}?{query}",
            headers={
                "Content-Type": "application/json; charset=utf-8",
                "authorization": f"Bearer {self.token()}",
                "appkey": self.app_key,
                "appsecret": self.app_secret,
                "tr_id": tr_id,
                "custtype": "P",
            },
        )
        with urllib.request.urlopen(req, timeout=15) as r:
            return json.loads(r.read())

    def quote(self, symbol: str) -> dict:
        return self.get(
            "uapi/domestic-stock/v1/quotations/inquire-price",
            "FHKST01010100",
            {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
        ).get("output") or {}

    def investor(self, symbol: str) -> dict:
        try:
            out = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-investor",
                "FHKST01010900",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
            ).get("output") or []
            return out[0] if isinstance(out, list) and out else (out if isinstance(out, dict) else {})
        except Exception:
            return {}

    def daily(self, symbol: str, days: int = 20) -> list[dict]:
        from datetime import timedelta
        today = datetime.now()
        try:
            out = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice",
                "FHKST03010100",
                {
                    "FID_COND_MRKT_DIV_CODE": "J",
                    "FID_INPUT_ISCD": symbol,
                    "FID_INPUT_DATE_1": (today - timedelta(days=60)).strftime("%Y%m%d"),
                    "FID_INPUT_DATE_2": today.strftime("%Y%m%d"),
                    "FID_PERIOD_DIV_CODE": "D",
                    "FID_ORG_ADJ_PRC": "0",
                },
            ).get("output2") or []
            return out[:days] if isinstance(out, list) else []
        except Exception:
            return []


# ─── 데이터 수집 ─────────────────────────────────────────────────────────────

def _collect_stock_data(symbol: str, name: str) -> dict:
    """KIS API로 종목 핵심 데이터 수집"""
    kis = KISClient()
    q   = kis.quote(symbol)
    inv = kis.investor(symbol)
    daily = kis.daily(symbol, 20)

    price      = num(q.get("stck_prpr", 0))
    chg_pct    = num(q.get("prdy_ctrt", 0))
    volume     = num(q.get("acml_vol", 0))
    high52     = num(q.get("stck_hgpr", 0))
    low52      = num(q.get("stck_lwpr", 0))
    per        = num(q.get("per", 0))
    pbr        = num(q.get("pbr", 0))
    eps        = num(q.get("eps", 0))
    market_cap = num(q.get("hts_avls", 0))  # 억원

    # 수급
    inst_net   = num(inv.get("instl_ntby_qty", 0))   # 기관 순매수
    foreign_net= num(inv.get("frgn_ntby_qty", 0))    # 외국인 순매수
    indiv_net  = num(inv.get("indvdl_ntby_qty", 0))  # 개인 순매수

    # 20일 모멘텀
    ma20 = 0.0
    if daily:
        closes = [num(d.get("stck_clpr", 0)) for d in daily if num(d.get("stck_clpr", 0))]
        ma20 = sum(closes) / len(closes) if closes else 0.0

    return {
        "symbol": symbol, "name": name,
        "price": price, "chg_pct": chg_pct,
        "volume": int(volume), "market_cap": market_cap,
        "high52": high52, "low52": low52,
        "per": per, "pbr": pbr, "eps": eps,
        "ma20": round(ma20, 0),
        "inst_net": int(inst_net), "foreign_net": int(foreign_net), "indiv_net": int(indiv_net),
    }


# ─── ACTION 1: 종목탐색 ──────────────────────────────────────────────────────

def action1_scout(symbol: str, name: str) -> str:
    """Action 1: 섹터 초감 → 스크리닝 → 어닝스 프리뷰 → 투자 메모 생성"""
    log(f"Action1 start: {name}({symbol})")

    try:
        d = _collect_stock_data(symbol, name)
    except Exception as e:
        log(f"KIS 데이터 수집 실패: {e}")
        d = {"symbol": symbol, "name": name, "price": 0, "chg_pct": 0,
             "volume": 0, "market_cap": 0, "high52": 0, "low52": 0,
             "per": 0, "pbr": 0, "eps": 0, "ma20": 0,
             "inst_net": 0, "foreign_net": 0, "indiv_net": 0}

    prompt = f"""당신은 기관급 주식 리서치 애널리스트입니다. 다음 데이터를 바탕으로 {name}({symbol})의 투자 메모를 작성하세요.

## 시장 데이터
- 현재가: {d['price']:,.0f}원 ({d['chg_pct']:+.2f}%)
- 거래량: {d['volume']:,}주
- 시가총액: {d['market_cap']:,.0f}억원
- 52주 최고/최저: {d['high52']:,.0f} / {d['low52']:,.0f}원
- PER: {d['per']:.1f}x / PBR: {d['pbr']:.2f}x / EPS: {d['eps']:,.0f}원
- 20일 이동평균: {d['ma20']:,.0f}원

## 수급 (당일)
- 기관 순매수: {d['inst_net']:+,}주
- 외국인 순매수: {d['foreign_net']:+,}주
- 개인 순매수: {d['indiv_net']:+,}주

다음 형식으로 간결하게 작성하세요 (텔레그램 전송용, 총 600자 이내):

📊 **{name} 투자 메모** (Action 1)

**[섹터 포지션]**
(AI/반도체 섹터 내 이 종목의 위치와 경쟁력 1~2줄)

**[스크리닝 결과]**
• 밸류에이션: (PER/PBR 해석)
• 수급: (기관/외국인/개인 해석)
• 모멘텀: (현재가 vs 20일선 해석)

**[어닝스 프리뷰]**
(다음 분기 예상 이슈 2~3줄)

**[투자 메모]**
• 매수 논리:
• 리스크:
• 목표 주가 범위: (밸류에이션 기반 추정)
• 포지션 제안: 관심/진입/보류 중 하나"""

    memo = llm(prompt, max_tokens=900)

    result = f"""🏠 *1인 투자하우스 — Action 1: 종목탐색*
━━━━━━━━━━━━━━━━━━━━
{memo}
━━━━━━━━━━━━━━━━━━━━
📅 {datetime.now().strftime('%Y-%m-%d %H:%M')} | 소미·영숙 AI Lab"""

    return result


# ─── ACTION 2: 추적관찰 ──────────────────────────────────────────────────────

def _load_thesis(symbol: str) -> dict:
    try:
        if THESIS_FILE.exists():
            data = json.loads(THESIS_FILE.read_text(encoding="utf-8"))
            return data.get(symbol, {})
    except Exception:
        pass
    return {}


def _save_thesis(symbol: str, thesis: dict) -> None:
    THESIS_FILE.parent.mkdir(parents=True, exist_ok=True)
    try:
        data = json.loads(THESIS_FILE.read_text(encoding="utf-8")) if THESIS_FILE.exists() else {}
    except Exception:
        data = {}
    data[symbol] = {**thesis, "updated_at": datetime.now().isoformat()}
    THESIS_FILE.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def action2_track(symbol: str, name: str) -> str:
    """Action 2: 보유 thesis 업데이트 → 카탈리스트 체크 → 어닝스 리뷰"""
    log(f"Action2 start: {name}({symbol})")

    try:
        d = _collect_stock_data(symbol, name)
    except Exception as e:
        log(f"KIS 데이터 수집 실패: {e}")
        d = {"symbol": symbol, "name": name, "price": 0, "chg_pct": 0,
             "volume": 0, "per": 0, "inst_net": 0, "foreign_net": 0, "indiv_net": 0}

    prev_thesis = _load_thesis(symbol)
    prev_note   = prev_thesis.get("note", "첫 추적 시작")
    entry_price = num(prev_thesis.get("entry_price", d["price"]))
    pnl_pct = ((d["price"] - entry_price) / entry_price * 100) if entry_price else 0.0

    prompt = f"""당신은 포트폴리오 매니저입니다. {name}({symbol}) 보유 중인 포지션을 추적 관찰 중입니다.

## 현재 데이터
- 현재가: {d['price']:,.0f}원 ({d['chg_pct']:+.2f}%)
- 진입가 기준 손익: {pnl_pct:+.2f}%
- 수급: 기관 {d['inst_net']:+,}주 / 외국인 {d['foreign_net']:+,}주

## 이전 투자 thesis
{prev_note}

다음 형식으로 추적 노트를 작성하세요 (텔레그램용, 500자 이내):

🔍 **{name} 추적관찰** (Action 2)

**[Thesis 유지/변경]**
(이전 thesis와 비교해 변화 있으면 업데이트)

**[카탈리스트 체크]**
• 다음 주목 이벤트: (예상 실적 발표, 정책, 섹터 이슈)
• 수급 신호: (기관/외국인 동향 해석)

**[어닝스 리뷰 포인트]**
(최근 실적 트렌드 및 다음 분기 체크포인트)

**[액션]**
• 현재 판단: 보유유지 / 비중확대 / 비중축소 / 손절 중 하나
• 다음 모니터링 포인트:"""

    note = llm(prompt, max_tokens=800)

    # thesis 저장
    _save_thesis(symbol, {
        "name": name,
        "entry_price": entry_price or d["price"],
        "note": note,
    })

    result = f"""🏠 *1인 투자하우스 — Action 2: 추적관찰*
━━━━━━━━━━━━━━━━━━━━
{note}
━━━━━━━━━━━━━━━━━━━━
📅 {datetime.now().strftime('%Y-%m-%d %H:%M')} | 소미·영숙 AI Lab"""

    return result


# ─── 섹터 분석 ───────────────────────────────────────────────────────────────

def sector_scan(query: str) -> str:
    """섹터 초감 — LLM 기반 섹터 이슈 요약"""
    prompt = f"""{query} 섹터의 현재 투자 관점 요약을 작성해주세요.

다음 형식으로 작성하세요 (텔레그램용, 400자 이내):

🔬 **{query} 섹터 초감**

**[지금 뭐가 중요해?]**
(핵심 드라이버 2~3개)

**[테일윈드]**
(주요 상승 요인)

**[헤드윈드]**
(주요 위험 요인)

**[주목 종목]**
(이 섹터에서 지금 봐야 할 종목 2~3개와 이유)"""

    summary = llm(prompt, max_tokens=600)
    return f"""🏠 *1인 투자하우스 — 섹터 초감*
━━━━━━━━━━━━━━━━━━━━
{summary}
━━━━━━━━━━━━━━━━━━━━
📅 {datetime.now().strftime('%Y-%m-%d %H:%M')} | 소미·영숙 AI Lab"""


# ─── 메인 ────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="1인 투자하우스")
    parser.add_argument("action", choices=["action1", "action2", "sector", "morning"],
                        help="실행할 액션")
    parser.add_argument("arg1", nargs="?", default="", help="종목코드 또는 섹터명")
    parser.add_argument("arg2", nargs="?", default="", help="종목명")
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    args = parser.parse_args()

    if args.action == "morning":
        morning_script = SCRIPT_DIR / "morning_note.py"
        import subprocess
        cmd = [sys.executable, str(morning_script)]
        if args.send:
            cmd.append("--send")
        subprocess.run(cmd)
        return

    if args.action == "sector":
        result = sector_scan(args.arg1 or "AI반도체")
    elif args.action == "action1":
        if not args.arg1:
            print("사용법: investment_house.py action1 <종목코드> <종목명>")
            sys.exit(1)
        result = action1_scout(args.arg1, args.arg2 or args.arg1)
    elif args.action == "action2":
        if not args.arg1:
            print("사용법: investment_house.py action2 <종목코드> <종목명>")
            sys.exit(1)
        result = action2_track(args.arg1, args.arg2 or args.arg1)
    else:
        result = "알 수 없는 액션"

    print(result)
    if args.send:
        send(result)
        log("텔레그램 전송 완료")


if __name__ == "__main__":
    main()
