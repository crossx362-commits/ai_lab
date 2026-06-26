#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""소미 - KIS 기반 국내주식 수급 자동 텔레그램 리포터."""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta
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
from _shared.process import ProcessLock  # noqa: E402
from short_covering_analyzer import calculate_score, generate_report, parse_input_text  # noqa: E402

load_env(str(PROJECT_ROOT))

DEFAULT_SYMBOL = "032820"
DEFAULT_NAME = "우리기술"
DEFAULT_TIMES = ["08:50", "12:30", "15:40"]
TOKEN_CACHE = PROJECT_ROOT / "output" / "cache" / "kis_access_token.json"
LATEST_REPORT = PROJECT_ROOT / "reports" / "research" / "somi_ourtech_latest.md"
RUN_LOG = PROJECT_ROOT / "output" / "bot_logs" / "somi_kis_reporter.log"


def log(message: str) -> None:
    RUN_LOG.parent.mkdir(parents=True, exist_ok=True)
    line = f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}"
    print(line, flush=True)
    with RUN_LOG.open("a", encoding="utf-8") as f:
        f.write(line + "\n")


def num(value: object) -> float:
    text = str(value or "").replace(",", "").strip()
    if not text:
        return 0.0
    try:
        return float(text)
    except ValueError:
        return 0.0


def fmt_int(value: object) -> str:
    n = num(value)
    return f"{int(n):,}" if n else ""


def fmt_pct(value: object) -> str:
    n = num(value)
    if not n:
        return ""
    return f"{n:+.2f}%"


class KISClient:
    def __init__(self) -> None:
        self.app_key = os.getenv("KIS_APP_KEY", "").strip()
        self.app_secret = os.getenv("KIS_APP_SECRET", "").strip()
        real_mode = os.getenv("KIS_REAL_MODE", "true").strip().lower() in {"1", "true", "yes", "y"}
        self.base_url = (
            "https://openapi.koreainvestment.com:9443"
            if real_mode
            else "https://openapivts.koreainvestment.com:29443"
        )
        if not self.app_key or not self.app_secret:
            raise RuntimeError("KIS_APP_KEY/KIS_APP_SECRET 환경변수가 없습니다.")

    def token(self) -> str:
        cached = self._load_cached_token()
        if cached:
            return cached

        payload = {
            "grant_type": "client_credentials",
            "appkey": self.app_key,
            "appsecret": self.app_secret,
        }
        req = urllib.request.Request(
            f"{self.base_url}/oauth2/tokenP",
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=15) as response:
            data = json.loads(response.read())
        access_token = data.get("access_token", "")
        if not access_token:
            raise RuntimeError(f"KIS token 발급 실패: {data}")

        expires_in = int(data.get("expires_in", 86400))
        TOKEN_CACHE.parent.mkdir(parents=True, exist_ok=True)
        TOKEN_CACHE.write_text(
            json.dumps(
                {
                    "access_token": access_token,
                    "expires_at": time.time() + max(60, expires_in - 300),
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        return access_token

    def _load_cached_token(self) -> str:
        try:
            if TOKEN_CACHE.exists():
                data = json.loads(TOKEN_CACHE.read_text(encoding="utf-8"))
                if data.get("access_token") and float(data.get("expires_at", 0)) > time.time():
                    return data["access_token"]
        except Exception:
            return ""
        return ""

    def get(self, path: str, tr_id: str, params: dict[str, str]) -> dict:
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
        try:
            with urllib.request.urlopen(req, timeout=15) as response:
                return json.loads(response.read())
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"KIS HTTP {e.code}: {body[:300]}") from e

    def quote(self, symbol: str = DEFAULT_SYMBOL) -> dict:
        data = self.get(
            "uapi/domestic-stock/v1/quotations/inquire-price",
            "FHKST01010100",
            {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
        )
        return data.get("output") or {}

    def daily_prices(self, symbol: str = DEFAULT_SYMBOL, days: int = 30) -> list[dict]:
        today = datetime.now()
        data = self.get(
            "uapi/domestic-stock/v1/quotations/inquire-daily-itemchartprice",
            "FHKST03010100",
            {
                "FID_COND_MRKT_DIV_CODE": "J",
                "FID_INPUT_ISCD": symbol,
                "FID_INPUT_DATE_1": (today - timedelta(days=80)).strftime("%Y%m%d"),
                "FID_INPUT_DATE_2": today.strftime("%Y%m%d"),
                "FID_PERIOD_DIV_CODE": "D",
                "FID_ORG_ADJ_PRC": "0",
            },
        )
        output = data.get("output2") or []
        return output[:days] if isinstance(output, list) else []

    def investor_today(self, symbol: str = DEFAULT_SYMBOL) -> dict:
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
            log(f"KIS investor_today unavailable: {e}")
            return {}

    def daily_short_sale(self, symbol: str = DEFAULT_SYMBOL) -> dict:
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/daily-short-sale",
                "FHPST04830000",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
            )
            output = data.get("output") or data.get("output1") or []
            if isinstance(output, list):
                return output[0] if output else {}
            return output if isinstance(output, dict) else {}
        except Exception as e:
            log(f"KIS daily_short_sale unavailable: {e}")
            return {}


def pick(data: dict, *keys: str) -> str:
    for key in keys:
        value = data.get(key)
        if value not in (None, ""):
            return str(value)
    return ""


def build_input_text(kis: KISClient, report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> str:
    quote = kis.quote(symbol)
    dailies = kis.daily_prices(symbol, 30)
    investor = kis.investor_today(symbol)
    short_sale = kis.daily_short_sale(symbol)

    latest = dailies[0] if dailies else {}
    rows_for_avg = dailies[1:21] if len(dailies) > 1 else dailies[:20]
    avg_volume = 0
    if rows_for_avg:
        avg_volume = sum(num(row.get("acml_vol")) for row in rows_for_avg) / len(rows_for_avg)

    open_price = pick(quote, "stck_oprc") or pick(latest, "stck_oprc")
    high_price = pick(quote, "stck_hgpr") or pick(latest, "stck_hgpr")
    low_price = pick(quote, "stck_lwpr") or pick(latest, "stck_lwpr")
    close_price = pick(quote, "stck_prpr") or pick(latest, "stck_clpr")
    change_pct = pick(quote, "prdy_ctrt")
    volume = pick(quote, "acml_vol") or pick(latest, "acml_vol")
    trading_value = pick(quote, "acml_tr_pbmn") or pick(latest, "acml_tr_pbmn")

    individual = pick(investor, "prsn_ntby_qty", "indv_ntby_qty", "prsn_ntby_vol")
    foreigner = pick(investor, "frgn_ntby_qty", "frgn_ntby_vol")
    institution = pick(investor, "orgn_ntby_qty", "inst_ntby_qty", "orgn_ntby_vol")

    short_volume = pick(short_sale, "short_sale_qty", "ssts_cntg_qty", "stnd_shrn_seln_qty")
    short_ratio = pick(short_sale, "short_sale_rate", "ssts_tr_pbmn_rate", "stnd_shrn_seln_rate")
    short_avg_price = pick(short_sale, "short_sale_avg_prc", "ssts_avrg_prc")

    # KIS 기본 시세 연결값 외 대차/CB/개인 평단은 별도 데이터가 없으면 확인 필요로 남긴다.
    return f"""{name} 오늘 데이터야. 수급, 세력상황, 큰 수익 가능성, 매수 판단까지 분석해줘.

종목명: {name}
종목코드: {symbol}
날짜: {datetime.now().strftime("%Y-%m-%d")} {report_name}
시가: {fmt_int(open_price)}
고가: {fmt_int(high_price)}
저가: {fmt_int(low_price)}
종가: {fmt_int(close_price)}
등락률: {fmt_pct(change_pct)}
거래량: {fmt_int(volume)}
20일 평균 거래량: {int(avg_volume):,}
거래대금: {fmt_int(trading_value)}
최근 5일 주가 흐름: 확인 필요
최근 5일 거래량 흐름: 확인 필요
최근 5일 외국인·기관 수급: 확인 필요
개인 순매수: {fmt_int(individual)}
외국인 순매수: {fmt_int(foreigner)}
기관 순매수: {fmt_int(institution)}
프로그램 매매:
대차잔고수량:
전일 대차잔고수량:
대차상환수량:
대차체결수량:
공매도 거래량: {fmt_int(short_volume)}
공매도 비중: {short_ratio}
공매도 평균가: {fmt_int(short_avg_price)}
코스피 등락률:
코스닥 등락률:
시장경보 여부: 확인 필요
테마: 확인 필요
관련 뉴스: 확인 필요
CB/BW/유상증자/보호예수 이슈: 확인 필요
전환가:
리픽싱 가능 여부:
주요 지지선:
주요 저항선:
내 평단:
내 보유 비중:
추가 예수금:
장중 특징: KIS 현재가/일봉 기반 자동 수집. 대차·CB·지지/저항은 별도 확인 필요.
"""


def make_report(report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> str:
    kis = KISClient()
    input_text = build_input_text(kis, report_name, symbol, name)
    parsed = parse_input_text(input_text)
    score, grade, pos, neg = calculate_score(parsed)
    report = generate_report(parsed, score, grade, pos, neg)
    header = f"[소미 자동보고 - {name}({symbol}) / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n\n"
    return header + report


def send_report(report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> bool:
    report = make_report(report_name, symbol, name)
    LATEST_REPORT.parent.mkdir(parents=True, exist_ok=True)
    LATEST_REPORT.write_text(report, encoding="utf-8")
    chunks = [report[i : i + 3900] for i in range(0, len(report), 3900)]
    ok = True
    for index, chunk in enumerate(chunks, start=1):
        prefix = f"(part {index}/{len(chunks)})\n" if len(chunks) > 1 else ""
        ok = send(prefix + chunk) and ok
        time.sleep(0.5)
    return ok


def seconds_until_next(times: list[str]) -> tuple[int, str]:
    now = datetime.now()
    candidates = []
    for item in times:
        hour, minute = [int(x) for x in item.split(":", 1)]
        candidate = now.replace(hour=hour, minute=minute, second=0, microsecond=0)
        if candidate <= now:
            candidate += timedelta(days=1)
        candidates.append(candidate)
    next_run = min(candidates)
    return max(1, int((next_run - now).total_seconds())), next_run.strftime("%Y-%m-%d %H:%M")


def daemon(times: list[str], symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> None:
    with ProcessLock("somi_kis_reporter"):
        log(f"소미 KIS 리포터 시작: {', '.join(times)}")
        while True:
            wait_seconds, next_run = seconds_until_next(times)
            log(f"다음 소미 보고: {next_run} (대기 {wait_seconds}s)")
            time.sleep(wait_seconds)
            try:
                send_report("자동", symbol, name)
                log("소미 자동보고 전송 완료")
            except Exception as e:
                log(f"소미 자동보고 실패: {e}")
                send(f"⚠️ [소미 자동보고 실패]\n{e}")
            time.sleep(65)


def main() -> None:
    parser = argparse.ArgumentParser(description="소미 KIS 기반 국내주식 수급 자동 텔레그램 리포터")
    parser.add_argument("--send", action="store_true", help="즉시 텔레그램 보고")
    parser.add_argument("--print", action="store_true", help="리포트 출력")
    parser.add_argument("--daemon", action="store_true", help="하루 3회 자동 보고 데몬")
    parser.add_argument("--times", default=",".join(DEFAULT_TIMES), help="HH:MM,HH:MM 형식 보고 시간")
    parser.add_argument("--symbol", default=DEFAULT_SYMBOL, help="종목코드, 기본값 032820")
    parser.add_argument("--name", default=DEFAULT_NAME, help="종목명, 기본값 우리기술")
    args = parser.parse_args()

    times = [x.strip() for x in args.times.split(",") if x.strip()]

    if args.daemon:
        daemon(times, args.symbol, args.name)
    elif args.send:
        ok = send_report("즉시", args.symbol, args.name)
        raise SystemExit(0 if ok else 1)
    elif getattr(args, "print"):
        print(make_report("테스트", args.symbol, args.name))
    else:
        print("사용법: --send | --print | --daemon [--symbol 032820 --name 우리기술] [--times 08:50,12:30,15:40]")


if __name__ == "__main__":
    main()
