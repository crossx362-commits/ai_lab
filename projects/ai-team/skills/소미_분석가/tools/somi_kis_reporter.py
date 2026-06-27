#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Somi KIS-based Korean stock score reporter."""

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
    print(line, file=sys.stderr, flush=True)
    with RUN_LOG.open("a", encoding="utf-8") as file:
        file.write(line + "\n")


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
    text = str(value or "").strip()
    if not text:
        return ""
    n = num(text)
    return f"{n:+.2f}%"


def pick(data: dict, *keys: str) -> str:
    for key in keys:
        value = data.get(key)
        if value not in (None, ""):
            return str(value)
    return ""


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

        payload = {"grant_type": "client_credentials", "appkey": self.app_key, "appsecret": self.app_secret}
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
            json.dumps({"access_token": access_token, "expires_at": time.time() + max(60, expires_in - 300)}),
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
        except urllib.error.HTTPError as exc:
            body = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"KIS HTTP {exc.code}: {body[:300]}") from exc

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
                "FID_INPUT_DATE_1": (today - timedelta(days=90)).strftime("%Y%m%d"),
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
            log(f"KIS investor_today raw response: {json.dumps(data, ensure_ascii=False)[:500]}")
            output = data.get("output") or data.get("output1") or []
            if isinstance(output, list):
                return output[0] if output else {}
            return output if isinstance(output, dict) else {}
        except Exception as exc:
            log(f"KIS investor_today unavailable: {exc}")
            return {}

    def investor_history(self, symbol: str = DEFAULT_SYMBOL, days: int = 5) -> list[dict]:
        """최근 N일간 투자자별 수급 데이터 조회"""
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-investor",
                "FHKST01010900",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
            )
            log(f"KIS investor_history raw response: {json.dumps(data, ensure_ascii=False)[:500]}")
            output = data.get("output") or []
            if isinstance(output, list):
                return output[:days]
            return [output] if isinstance(output, dict) else []
        except Exception as exc:
            log(f"KIS investor_history unavailable: {exc}")
            return []

    def daily_short_sale(self, symbol: str = DEFAULT_SYMBOL) -> dict:
        try:
            today = datetime.now()
            data = self.get(
                "uapi/domestic-stock/v1/quotations/daily-short-sale",
                "FHPST04830000",
                {
                    "FID_COND_MRKT_DIV_CODE": "J",
                    "FID_INPUT_ISCD": symbol,
                    "FID_INPUT_DATE_1": (today - timedelta(days=7)).strftime("%Y%m%d"),
                    "FID_INPUT_DATE_2": today.strftime("%Y%m%d"),
                },
            )
            log(f"KIS daily_short_sale raw response: {json.dumps(data, ensure_ascii=False)[:500]}")
            # output2: 일자별 공매도 상세(체결수량/비중/평균가), output1: 가격 요약
            output = data.get("output2") or data.get("output") or data.get("output1") or []
            if isinstance(output, list):
                return output[0] if output else {}
            return output if isinstance(output, dict) else {}
        except Exception as exc:
            log(f"KIS daily_short_sale unavailable: {exc}")
            return {}


def _flow_text(dailies: list[dict]) -> str:
    rows = list(reversed(dailies[:5]))
    if len(rows) < 2:
        return "확인 필요"
    first = num(rows[0].get("stck_clpr"))
    last = num(rows[-1].get("stck_clpr"))
    pct = ((last - first) / first * 100) if first else 0
    closes = " -> ".join(fmt_int(row.get("stck_clpr")) for row in rows if row.get("stck_clpr"))
    return f"{closes} ({pct:+.2f}%)"


def _volume_flow_text(dailies: list[dict]) -> str:
    vols = [num(row.get("acml_vol")) for row in dailies[:5] if row.get("acml_vol")]
    if not vols:
        return "확인 필요"
    return f"최근 5일 평균 {int(sum(vols) / len(vols)):,}주, 오늘 {int(vols[0]):,}주"


def _market_warning_text(code: str) -> str:
    mapping = {
        "00": "없음",
        "01": "투자주의",
        "02": "투자경고",
        "03": "투자위험",
    }
    return mapping.get(str(code or "").strip(), str(code or "확인 필요"))


def _investor_flow_text(investor_hist: list[dict]) -> str:
    """최근 5일 투자자별 수급 흐름 분석"""
    if not investor_hist:
        return "확인 필요"

    foreigner_sum = sum(num(row.get("frgn_ntby_qty") or row.get("frgn_ntby_vol")) for row in investor_hist)
    institution_sum = sum(num(row.get("orgn_ntby_qty") or row.get("inst_ntby_qty") or row.get("orgn_ntby_vol")) for row in investor_hist)
    individual_sum = sum(num(row.get("prsn_ntby_qty") or row.get("indv_ntby_qty") or row.get("prsn_ntby_vol")) for row in investor_hist)

    parts = []
    if foreigner_sum > 0:
        parts.append(f"외국인 {int(foreigner_sum):,}주 순매수")
    elif foreigner_sum < 0:
        parts.append(f"외국인 {int(abs(foreigner_sum)):,}주 순매도")

    if institution_sum > 0:
        parts.append(f"기관 {int(institution_sum):,}주 순매수")
    elif institution_sum < 0:
        parts.append(f"기관 {int(abs(institution_sum)):,}주 순매도")

    if individual_sum > 0:
        parts.append(f"개인 {int(individual_sum):,}주 순매수")
    elif individual_sum < 0:
        parts.append(f"개인 {int(abs(individual_sum)):,}주 순매도")

    return ", ".join(parts) if parts else "확인 필요"


def build_input_text(kis: KISClient, report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> str:
    quote = kis.quote(symbol)
    dailies = kis.daily_prices(symbol, 30)
    investor = kis.investor_today(symbol)
    investor_hist = kis.investor_history(symbol, 5)
    short_sale = kis.daily_short_sale(symbol)

    latest = dailies[0] if dailies else {}
    rows_for_avg = dailies[1:21] if len(dailies) > 1 else dailies[:20]
    avg_volume = sum(num(row.get("acml_vol")) for row in rows_for_avg) / len(rows_for_avg) if rows_for_avg else 0

    open_price = pick(quote, "stck_oprc") or pick(latest, "stck_oprc")
    high_price = pick(quote, "stck_hgpr") or pick(latest, "stck_hgpr")
    low_price = pick(quote, "stck_lwpr") or pick(latest, "stck_lwpr")
    close_price = pick(quote, "stck_prpr") or pick(latest, "stck_clpr")
    volume = pick(quote, "acml_vol") or pick(latest, "acml_vol")

    individual = pick(investor, "prsn_ntby_qty", "indv_ntby_qty", "prsn_ntby_vol")
    foreigner = pick(investor, "frgn_ntby_qty", "frgn_ntby_vol")
    institution = pick(investor, "orgn_ntby_qty", "inst_ntby_qty", "orgn_ntby_vol")

    foreigner_5d = sum(num(row.get("frgn_ntby_qty") or row.get("frgn_ntby_vol")) for row in investor_hist)
    institution_5d = sum(num(row.get("orgn_ntby_qty") or row.get("inst_ntby_qty") or row.get("orgn_ntby_vol")) for row in investor_hist)
    individual_5d = sum(num(row.get("prsn_ntby_qty") or row.get("indv_ntby_qty") or row.get("prsn_ntby_vol")) for row in investor_hist)

    short_volume = (
        pick(short_sale, "ssts_cntg_qty", "short_sale_qty", "stnd_shrn_seln_qty")
        or pick(quote, "last_ssts_cntg_qty")
    )
    short_ratio = pick(short_sale, "ssts_vol_rlim", "short_sale_rate", "ssts_tr_pbmn_rate", "stnd_shrn_seln_rate")
    short_avg_price = pick(short_sale, "avrg_prc", "short_sale_avg_prc", "ssts_avrg_prc")
    loan_rate = pick(quote, "whol_loan_rmnd_rate")
    support_line = pick(quote, "pvt_frst_dmsp_prc", "dmsp_val")
    resistance_line = pick(quote, "pvt_frst_dmrs_prc", "dmrs_val")
    foreign_holding = pick(quote, "frgn_hldn_qty")
    foreign_rate = pick(quote, "hts_frgn_ehrt")
    market_warning = _market_warning_text(pick(quote, "mrkt_warn_cls_code"))

    return f"""종목명: {name}
종목코드: {symbol}
날짜: {datetime.now().strftime("%Y-%m-%d")} {report_name}
시가: {fmt_int(open_price)}
고가: {fmt_int(high_price)}
저가: {fmt_int(low_price)}
종가: {fmt_int(close_price)}
등락률: {fmt_pct(pick(quote, "prdy_ctrt"))}
거래량: {fmt_int(volume)}
20일 평균 거래량: {int(avg_volume):,}
거래대금: {fmt_int(pick(quote, "acml_tr_pbmn") or pick(latest, "acml_tr_pbmn"))}
최근 5일 주가 흐름: {_flow_text(dailies)}
최근 5일 거래량 흐름: {_volume_flow_text(dailies)}
최근 5일 외국인기관 수급: {_investor_flow_text(investor_hist)}
개인 순매수: {fmt_int(individual)}
외국인 순매수: {fmt_int(foreigner)}
기관 순매수: {fmt_int(institution)}
최근 5일 개인 누적: {fmt_int(individual_5d) if individual_5d else "확인 필요"}
최근 5일 외국인 누적: {fmt_int(foreigner_5d) if foreigner_5d else "확인 필요"}
최근 5일 기관 누적: {fmt_int(institution_5d) if institution_5d else "확인 필요"}
외국인 보유수량: {fmt_int(foreign_holding)}
외국인 보유율: {foreign_rate}
프로그램 매매: {fmt_int(pick(quote, "pgtr_ntby_qty"))}
대차잔고율: {loan_rate}
대차상환수량:
대차체결수량:
직전 공매도 체결수량: {fmt_int(short_volume)}
공매도 비중: {short_ratio}
공매도 평균가: {fmt_int(short_avg_price)}
시장경보 여부: {market_warning}
테마: {pick(quote, "bstp_kor_isnm") or "확인 필요"}
관련 뉴스: 확인 필요
CB/BW/유상증자/보호예수 이슈: 확인 필요
전환가:
리픽싱 가능 여부:
주요 지지선: {fmt_int(support_line)}
주요 저항선: {fmt_int(resistance_line)}
내 평단:
내 보유 비중:
추가 예수금:
장중 특징: KIS 현재가/일봉 기반 자동 수집. 대차잔고율, 직전 공매도 체결수량, 피벗 지지/저항 반영.
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
            except Exception as exc:
                log(f"소미 자동보고 실패: {exc}")
                send(f"[소미 자동보고 실패]\n{exc}")
            time.sleep(65)


def main() -> None:
    parser = argparse.ArgumentParser(description="소미 KIS 기반 국내주식 점수 자동 리포터")
    parser.add_argument("--send", action="store_true", help="즉시 텔레그램 보고")
    parser.add_argument("--print", action="store_true", help="리포트 출력")
    parser.add_argument("--daemon", action="store_true", help="지정 시간 자동 보고")
    parser.add_argument("--times", default=",".join(DEFAULT_TIMES), help="HH:MM,HH:MM 형식 보고 시간")
    parser.add_argument("--symbol", default=DEFAULT_SYMBOL, help="종목코드")
    parser.add_argument("--name", default=DEFAULT_NAME, help="종목명")
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
