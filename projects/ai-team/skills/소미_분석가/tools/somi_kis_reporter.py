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
from _shared.notify import publish_report, send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared import growth  # noqa: E402
from short_covering_analyzer import (  # noqa: E402
    calculate_score,
    flow_short_analysis,
    generate_report,
    grade_of,
    parse_input_text,
)
from watchlist_manager import load_watchlist  # noqa: E402
from _shared import research  # noqa: E402


load_env(str(PROJECT_ROOT))

# 보고 대상은 watchlist에서 결정한다(아래는 함수 시그니처용 폴백, 정기 보고 대상 아님).
DEFAULT_SYMBOL = ""
DEFAULT_NAME = ""
DEFAULT_TIMES = ["08:00", "16:00"]
TOKEN_CACHE = PROJECT_ROOT / "output" / "cache" / "kis_access_token.json"
LATEST_REPORT = PROJECT_ROOT / "reports" / "research" / "somi_ourtech_latest.md"
RUN_LOG = PROJECT_ROOT / "output" / "bot_logs" / "somi_kis_reporter.log"
FLOW_HISTORY = PROJECT_ROOT / "output" / "cache" / "somi_flow_history.json"


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
        real_mode = os.getenv("KIS_REAL_MODE", "false").strip().lower() in {"1", "true", "yes", "y"}
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

    def short_sale_series(self, symbol: str = DEFAULT_SYMBOL, days: int = 20) -> list[dict]:
        """일자별 공매도 상세(output2) 전체 리스트 — 누적공매도·비중 추세용."""
        try:
            today = datetime.now()
            data = self.get(
                "uapi/domestic-stock/v1/quotations/daily-short-sale",
                "FHPST04830000",
                {
                    "FID_COND_MRKT_DIV_CODE": "J",
                    "FID_INPUT_ISCD": symbol,
                    "FID_INPUT_DATE_1": (today - timedelta(days=days)).strftime("%Y%m%d"),
                    "FID_INPUT_DATE_2": today.strftime("%Y%m%d"),
                },
            )
            out = data.get("output2") or []
            return out if isinstance(out, list) else []
        except Exception as exc:
            log(f"KIS short_sale_series unavailable: {exc}")
            return []

    def minute_chart(self, symbol: str = DEFAULT_SYMBOL, time_hhmmss: str = "") -> list[dict]:
        """당일 분봉(inquire-time-itemchartprice output2) — VWAP·장중 구조 산출용.
        bar: stck_cntg_hour/stck_prpr/stck_oprc/stck_hgpr/stck_lwpr/cntg_vol."""
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-time-itemchartprice",
                "FHKST03010200",
                {
                    "FID_ETC_CLS_CODE": "",
                    "FID_COND_MRKT_DIV_CODE": "J",
                    "FID_INPUT_ISCD": symbol,
                    "FID_INPUT_HOUR_1": time_hhmmss or datetime.now().strftime("%H%M%S"),
                    "FID_PW_DATA_INCU_YN": "Y",
                },
            )
            out = data.get("output2") or []
            return out if isinstance(out, list) else []
        except Exception as exc:
            log(f"KIS minute_chart unavailable: {exc}")
            return []

    def orderbook(self, symbol: str = DEFAULT_SYMBOL) -> dict:
        """실시간 호가/잔량(inquire-asking-price-exp-ccn output1) — 매수세=총매수잔량/총매도잔량.
        total_bidp_rsqn(총매수호가잔량)/total_askp_rsqn(총매도호가잔량) 포함."""
        try:
            data = self.get(
                "uapi/domestic-stock/v1/quotations/inquire-asking-price-exp-ccn",
                "FHKST01010200",
                {"FID_COND_MRKT_DIV_CODE": "J", "FID_INPUT_ISCD": symbol},
            )
            return data.get("output1") or {}
        except Exception as exc:
            log(f"KIS orderbook unavailable: {exc}")
            return {}


def intraday_vwap(bars: list[dict]) -> float:
    """분봉 리스트에서 거래량가중평균가(VWAP) 산출. 데이터 없으면 0.0 (미확인)."""
    tot_v = tot_pv = 0.0
    for b in bars:
        high = num(b.get("stck_hgpr"))
        low = num(b.get("stck_lwpr"))
        close = num(b.get("stck_prpr"))
        vol = num(b.get("cntg_vol"))
        typical = (high + low + close) / 3 if (high and low and close) else close
        if typical and vol:
            tot_pv += typical * vol
            tot_v += vol
    return (tot_pv / tot_v) if tot_v else 0.0


def buy_pressure_ratio(ob: dict) -> float:
    """호가 매수세 = 총매수호가잔량/총매도호가잔량. 1.0 초과면 매수 우위. 데이터 없으면 0.0."""
    bid = num(ob.get("total_bidp_rsqn"))
    ask = num(ob.get("total_askp_rsqn"))
    return (bid / ask) if (bid and ask) else 0.0


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


def build_input_text(kis: KISClient, report_name: str = "정기", symbol: str = DEFAULT_SYMBOL,
                     name: str = DEFAULT_NAME, investor_hist=None, short_sale=None) -> str:
    quote = kis.quote(symbol)
    dailies = kis.daily_prices(symbol, 30)
    investor = kis.investor_today(symbol)
    # make_report가 10일/시리즈를 미리 받아오면 재사용(중복 호출 방지), 단독 호출 시만 직접 조회
    # ([1:6] 슬라이스 대비 6일 확보) — 오늘 빈 행 제외 시 과거 5거래일 사용
    raw_hist = investor_hist if investor_hist is not None else kis.investor_history(symbol, 6)
    short_sale = short_sale if short_sale is not None else kis.daily_short_sale(symbol)

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

    # 당일 외국인/기관 수급 가용 여부 — 오전엔 KIS가 당일치를 빈 값으로 내려줌.
    # 미확정이면 5일 누적 윈도에서 오늘 빈 행을 제외([1:6]), 정상이면 오늘 포함([:5]).
    today_investor_available = bool((foreigner or "").strip() or (institution or "").strip())
    if today_investor_available:
        investor_hist = (raw_hist or [])[:5]
        investor_history_window = "today_included"
    else:
        investor_hist = (raw_hist or [])[1:6]
        investor_history_window = "today_excluded"

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
오늘수급확정: {"예" if today_investor_available else "아니오"}
수급윈도: {investor_history_window}
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


def _load_prev_loan_rate(symbol: str) -> str | None:
    """오늘 이전에 기록된 대차잔고율(전일 대비 추이용)."""
    try:
        rec = json.loads(FLOW_HISTORY.read_text(encoding="utf-8")).get(symbol)
        if rec and rec.get("date") != datetime.now().strftime("%Y-%m-%d"):
            return rec.get("loan_rate")
    except Exception:
        pass
    return None


def _save_loan_rate(symbol: str, loan_rate) -> None:
    if loan_rate in (None, "", "0", "0.0"):
        return  # 빈/0 값은 전일대비 추이를 오염시키므로 저장하지 않음
    try:
        FLOW_HISTORY.parent.mkdir(parents=True, exist_ok=True)
        data = {}
        if FLOW_HISTORY.exists():
            data = json.loads(FLOW_HISTORY.read_text(encoding="utf-8"))
        data[symbol] = {"date": datetime.now().strftime("%Y-%m-%d"), "loan_rate": loan_rate}
        FLOW_HISTORY.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    except Exception:
        pass


def make_report(report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> str:
    kis = KISClient()
    # 일자별 데이터는 한 번만 조회해 리포트·정밀분석에 함께 사용(중복 호출 방지)
    inv10 = kis.investor_history(symbol, 10)
    short_series = kis.short_sale_series(symbol, 20)
    input_text = build_input_text(
        kis, report_name, symbol, name,
        investor_hist=inv10, short_sale=(short_series[0] if short_series else None),
    )
    parsed = parse_input_text(input_text)
    score, grade, pos, neg = calculate_score(parsed)
    # 일자별 수급·공매도·대차 정밀 분석 (고점분산·공매도추세·숏커버 판별로 점수 보정)
    flow_block = ""
    try:
        prev_lr = _load_prev_loan_rate(symbol)
        fa = flow_short_analysis(
            inv10, short_series,
            parsed.get("support_line"), parsed.get("close"),
            parsed.get("loan_balance_rate"), prev_lr,
        )
        if fa["delta"]:
            score = max(0, min(100, score + fa["delta"]))
            grade = grade_of(score)
        pos = pos + fa["pos"]
        neg = neg + fa["neg"]
        flow_block = "\n\n" + fa["text"]
        _save_loan_rate(symbol, parsed.get("loan_balance_rate"))
    except Exception as exc:
        log(f"flow_short_analysis 실패: {exc}")
    report = generate_report(parsed, score, grade, pos, neg)
    # 공매도·대차 정밀 분석(1-2)을 세력 동향(1-1) 뒤, 매수 판단(2) 앞에 삽입
    if flow_block and "## 2. 매수 판단" in report:
        report = report.replace("## 2. 매수 판단", flow_block.strip() + "\n\n## 2. 매수 판단", 1)
    elif flow_block:
        report += flow_block
    # 마켓데스크 이슈 영향도 병기 (수급 점수는 보존, 공시 영향만 참고로 덧붙임)
    try:
        imp = research.load_issue_impact().get(symbol)
        if isinstance(imp, dict) and imp.get("score") is not None:
            report += f"\n\n📰 이슈 영향도 {int(imp['score']):+d} — {imp.get('reason', '')}"
    except Exception:
        pass
    # 마켓데스크 시장 배경 한 줄 (환율) — 종목 보고 맨 위
    market_bg = ""
    try:
        fx = research.load_market_brief().get("fx", {}) or {}
        if fx.get("KRW"):
            market_bg = f"🌐 시장 USD/KRW {fx['KRW']:.0f}\n\n"
    except Exception:
        pass
    header = f"[소미 자동보고 - {name}({symbol}) / {datetime.now().strftime('%Y-%m-%d %H:%M')}]\n\n"
    return header + market_bg + report


def send_report(report_name: str = "정기", symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME) -> bool:
    report = make_report(report_name, symbol, name)
    LATEST_REPORT.parent.mkdir(parents=True, exist_ok=True)
    LATEST_REPORT.write_text(report, encoding="utf-8")
    # 보고서는 노션에 작성, 텔레그램엔 링크만
    return publish_report(f"소미 {name}({symbol}) {report_name} 보고", report)


def daily_summary() -> str:
    """헌장 [정기 보고] 7항목 요약 — 짧게(시장·이슈·관심·보유·매수·청산·성장)."""
    import json
    cache = growth._root() / "output" / "cache"

    def _load(n):
        try:
            return json.loads((cache / n).read_text(encoding="utf-8"))
        except Exception:
            return {}

    watchlist = load_watchlist()
    positions = _load("somi_positions.json") or {}
    buys = [p for p in (_load("somi_proposals.json") or {}).get("items", []) if p.get("verdict") == "buy"]
    try:
        from market_regime import market_regime, regime_label
        reg = regime_label(market_regime().get("regime", "unknown"))
    except Exception:
        reg = "확인불가"
    gs = growth.summary()
    low = [a for a, d in gs.items() if d.get("avg_total", 100) < 60]
    top = f" (최고 {max(buys, key=lambda x: x.get('score', 0)).get('name')})" if buys else ""
    return "\n".join([
        "[정기 보고]",
        f"1. 시장 상태: {reg}",
        "2. 핵심 이슈: 수급 기반 자동 분석(상세는 종목별 보고)",
        f"3. 관심 종목: {len(watchlist)}종목",
        f"4. 보유 종목 리스크: {len(positions)}종목 보유 중",
        f"5. 매수 후보: {len(buys)}종목{top}",
        "6. 청산 후보: 포지션 점검 에이전트 참조",
        f"7. 성장/오류: 기록 {sum(d.get('count', 0) for d in gs.values())}건"
        + (f", 저점수 {','.join(low)}" if low else ", 이상 없음"),
    ])


def send_watchlist_reports(report_name: str = "정기") -> bool:
    """감시 목록(watchlist)에 등록된 종목을 순회하며 보고. 비어 있으면 안내만."""
    watchlist = load_watchlist()
    try:
        send(daily_summary())  # 헌장 [정기 보고] 요약 선두 발송
    except Exception:
        pass
    if not watchlist:
        send("📋 감시 중인 종목이 없습니다.\n텔레그램에서 '관심종목 추가 <종목코드> <종목명>'으로 등록하세요.")
        return True
    ok = True
    for symbol, name in watchlist.items():
        try:
            ok = send_report(report_name, symbol, name) and ok
        except Exception as exc:
            send(f"[소미 보고 실패 - {name}({symbol})]\n{exc}")
            ok = False
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


def daemon(times: list[str], symbol: str | None = None, name: str | None = None) -> None:
    with ProcessLock("somi_kis_reporter"):
        log(f"소미 KIS 리포터 시작: {', '.join(times)}")
        while True:
            wait_seconds, next_run = seconds_until_next(times)
            log(f"다음 소미 보고: {next_run} (대기 {wait_seconds}s)")
            time.sleep(wait_seconds)
            try:
                if symbol:
                    send_report("자동", symbol, name or symbol)
                else:
                    send_watchlist_reports("자동")
                log("소미 자동보고 전송 완료")
                growth.record(
                    "somi_reporter", role="정기 보고(08:00/16:00)",
                    data=f"watchlist {'단일' if symbol else '전체'}", judgment="정기 요약 전송",
                    result="전송 완료", good="2회/일로 축소", bad="",
                    scores={"fit": 20, "evidence": 18, "efficiency": 18, "risk": 16, "brevity": 8},
                )
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
    parser.add_argument("--symbol", default=None, help="종목코드 (미지정 시 watchlist 전체 보고)")
    parser.add_argument("--name", default=None, help="종목명")
    args = parser.parse_args()

    times = [x.strip() for x in args.times.split(",") if x.strip()]
    if args.daemon:
        daemon(times, args.symbol, args.name)
    elif args.send:
        if args.symbol:
            ok = send_report("즉시", args.symbol, args.name or args.symbol)
        else:
            ok = send_watchlist_reports("정기")
        raise SystemExit(0 if ok else 1)
    elif getattr(args, "print"):
        if args.symbol:
            print(make_report("테스트", args.symbol, args.name or args.symbol))
        else:
            wl = load_watchlist()
            if not wl:
                print("감시 종목 없음 — 텔레그램에서 '관심종목 추가 <코드> <종목명>'으로 등록")
            for symbol, name in wl.items():
                print(make_report("테스트", symbol, name))
    else:
        print("사용법: --send | --print | --daemon [--symbol <코드> --name <종목명>] [--times 08:50,12:30,15:40]")


if __name__ == "__main__":
    main()
