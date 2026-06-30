#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
morning_note.py — 매일 아침 브리핑
  1) 전체 시장 동향 (코스피/코스닥/나스닥)
  2) AI/반도체 섹터 이슈
  3) watchlist 종목별 전일 등락 + 소미 점수 + 한줄 코멘트

Usage:
    python morning_note.py            # 콘솔 출력
    python morning_note.py --send     # 텔레그램 전송
    python morning_note.py --daemon   # 오전 8시 자동 실행 루프
"""

from __future__ import annotations

import argparse
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
from _shared.notify import send  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from watchlist_manager import load_watchlist  # noqa: E402
from short_covering_analyzer import calculate_score, flow_short_analysis, grade_of  # noqa: E402

load_env(str(PROJECT_ROOT))

TOKEN_CACHE = PROJECT_ROOT / "output" / "cache" / "kis_access_token.json"
LOG_FILE = PROJECT_ROOT / "output" / "bot_logs" / "morning_note.log"

SECTOR_KEYWORDS = ["AI 반도체", "엔비디아", "SK하이닉스", "삼성전자", "HBM", "파운드리"]


# ─── 유틸 ───────────────────────────────────────────────────────────────────

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


# ─── KIS 클라이언트 (토큰 캐시 재사용) ─────────────────────────────────────

class KISClient:
    def __init__(self) -> None:
        self.app_key = os.getenv("KIS_APP_KEY", "").strip()
        self.app_secret = os.getenv("KIS_APP_SECRET", "").strip()
        real_mode = os.getenv("KIS_REAL_MODE", "false").strip().lower() in {"1", "true", "yes"}
        self.base_url = (
            "https://openapi.koreainvestment.com:9443"
            if real_mode
            else "https://openapivts.koreainvestment.com:29443"
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
            json.dumps({"access_token": token, "expires_at": time.time() + int(data.get("expires_in", 86400)) - 300}),
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
        data = self.get(
            "uapi/domestic-stock/v1/quotations/inquire-price",
            "FHKST01010100",
            {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
        )
        return data.get("output") or {}

    def index_quote(self, index_code: str) -> dict:
        """코스피(0001) / 코스닥(1001) 지수 시세"""
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-index-price",
                "FHPUP02100000",
                {"FID_COND_MRKT_DIV_CODE": "U", "FID_INPUT_ISCD": index_code},
            )
            return data.get("output") or {}
        except Exception as e:
            log(f"index_quote {index_code} 실패: {e}")
            return {}

    def investor_today(self, symbol: str) -> dict:
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-investor",
                "FHKST01010900",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
            )
            output = data.get("output") or data.get("output1") or []
            if isinstance(output, list):
                return output[0] if output else {}
            return output if isinstance(output, dict) else {}
        except Exception as e:
            log(f"investor_today {symbol} 실패: {e}")
            return {}


# ─── 섹션 1: 시장 동향 ───────────────────────────────────────────────────────

def build_market_section(kis: KISClient) -> str:
    lines = ["📊 *전체 시장 동향*"]
    indices = [("코스피", "0001"), ("코스닥", "1001")]
    for name, code in indices:
        q = kis.index_quote(code)
        if q:
            close = num(q.get("bstp_nmix_prpr") or q.get("stck_prpr", 0))
            chg_pct = num(q.get("bstp_nmix_prdy_ctrt") or q.get("prdy_ctrt", 0))
            arrow = "▲" if chg_pct >= 0 else "▼"
            lines.append(f"  {name}: {close:,.2f} {arrow}{abs(chg_pct):.2f}%")
        else:
            lines.append(f"  {name}: 데이터 없음")
    return "\n".join(lines)


# ─── 섹션 2: 섹터 이슈 (LLM 요약) ───────────────────────────────────────────

def build_sector_section() -> str:
    prompt = (
        "오늘 AI·반도체 섹터의 주요 뉴스와 시장 이슈를 3줄 이내로 요약해줘. "
        "핵심 키워드: HBM, 엔비디아, SK하이닉스, TSMC, AI 수요. "
        "한국어로, 불릿 포인트 없이 간결하게."
    )
    try:
        summary = llm_text(prompt, lm_first=False)
        return f"🔬 *AI/반도체 섹터 이슈*\n{summary.strip()}"
    except Exception as e:
        log(f"섹터 이슈 LLM 실패: {e}")
        return "🔬 *AI/반도체 섹터 이슈*\n데이터 조회 실패"


# ─── 섹션 3: watchlist 종목 ──────────────────────────────────────────────────

def build_watchlist_section(kis: KISClient) -> str:
    watchlist = load_watchlist()
    if not watchlist:
        return "📋 *watchlist*\n등록된 종목 없음"

    lines = ["📋 *watchlist 종목 동향*"]
    for symbol, name in watchlist.items():
        try:
            q = kis.quote(symbol)
            inv = kis.investor_today(symbol)

            price = num(q.get("stck_prpr", 0))
            chg_pct = num(q.get("prdy_ctrt", 0))
            volume = num(q.get("acml_vol", 0))

            # 소미 점수 계산
            flow_data = flow_short_analysis(inv, {}, symbol, name)
            score = calculate_score(flow_data)
            grade = grade_of(score)

            arrow = "▲" if chg_pct >= 0 else "▼"
            vol_str = f"{int(volume / 1000)}천주" if volume >= 1000 else f"{int(volume)}주"

            lines.append(
                f"  [{grade}] {name}({symbol}): {price:,.0f}원 {arrow}{abs(chg_pct):.2f}% | "
                f"거래량 {vol_str} | 점수 {score:.1f}"
            )
        except Exception as e:
            log(f"watchlist {symbol} 처리 실패: {e}")
            lines.append(f"  {name}({symbol}): 데이터 오류")

    return "\n".join(lines)


# ─── 메인 ────────────────────────────────────────────────────────────────────

def build_morning_note() -> str:
    kis = KISClient()
    today = datetime.now().strftime("%Y-%m-%d %A")

    sections = [
        f"☀️ *모닝노트* — {today}",
        "",
        build_market_section(kis),
        "",
        build_sector_section(),
        "",
        build_watchlist_section(kis),
        "",
        "─────────────────",
        "🤖 소미·영숙 AI Lab",
    ]
    return "\n".join(sections)


def run(send_telegram: bool = False) -> None:
    log("morning_note 시작")
    try:
        note = build_morning_note()
        print(note)
        if send_telegram:
            send(note)
            log("텔레그램 전송 완료")
    except Exception as e:
        log(f"morning_note 오류: {e}")
        if send_telegram:
            send(f"⚠️ 모닝노트 오류: {e}")


def daemon_loop(send_telegram: bool, fire_hour: int = 8) -> None:
    log(f"daemon 시작 — 매일 {fire_hour:02d}:00 실행")
    fired_today: str = ""
    while True:
        now = datetime.now()
        today_str = now.strftime("%Y-%m-%d")
        if now.hour == fire_hour and fired_today != today_str:
            fired_today = today_str
            run(send_telegram)
        time.sleep(30)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Morning Note")
    parser.add_argument("--send", action="store_true", help="텔레그램 전송")
    parser.add_argument("--daemon", action="store_true", help="데몬 모드 (매일 08:00)")
    parser.add_argument("--hour", type=int, default=8, help="데몬 실행 시각 (기본 8)")
    args = parser.parse_args()

    if args.daemon:
        daemon_loop(args.send, args.hour)
    else:
        run(args.send)
