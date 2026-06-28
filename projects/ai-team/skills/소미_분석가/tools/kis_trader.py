#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""KIS 국내주식 수동 주문 모듈 — 사용자가 텔레그램으로 지시한 매수/매도만 실행.

자동매매 아님: 시그널 기반 진입/청산 없음. 명시적 주문만 체결한다.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request
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
from somi_kis_reporter import KISClient, num  # noqa: E402

load_env(str(PROJECT_ROOT))


def _account() -> tuple[str, str]:
    """계좌번호(앞 8자리)와 상품코드(뒤 2자리) 반환."""
    raw = os.getenv("KIS_ACCOUNT_NO", "").strip()
    code = os.getenv("KIS_ACCOUNT_CODE", "").strip()
    if "-" in raw:  # '12345678-01' 형식 지원
        cano, prod = raw.split("-", 1)
        return cano.strip(), (code or prod).strip()
    return raw, (code or "01")


PAPER_FILE = PROJECT_ROOT / "output" / "cache" / "somi_paper.json"
PAPER_START_CASH = int(os.getenv("SOMI_PAPER_CASH", "10000000"))  # 페이퍼 초기 예수금 1천만


def _paper_load() -> dict:
    try:
        if PAPER_FILE.exists():
            return json.loads(PAPER_FILE.read_text(encoding="utf-8"))
    except Exception:
        pass
    return {"cash": PAPER_START_CASH, "positions": {}}


def _paper_save(d: dict) -> None:
    PAPER_FILE.parent.mkdir(parents=True, exist_ok=True)
    PAPER_FILE.write_text(json.dumps(d, ensure_ascii=False, indent=2), encoding="utf-8")


class KISTrader:
    def __init__(self) -> None:
        self.kis = KISClient()
        self.cano, self.prod = _account()
        # 기본값 false(안전) — 환경변수 누락/오타 시 실거래가 아닌 모의로 떨어지게
        self.real = os.getenv("KIS_REAL_MODE", "false").strip().lower() in {"1", "true", "yes", "y"}
        # 페이퍼(모의) 모드: 시세는 실제, 주문은 가상 체결. 실거래 키만 있어도 즉시 사용 가능.
        self.paper = os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}
        if not self.paper and not self.cano:
            raise RuntimeError("KIS_ACCOUNT_NO 환경변수가 없습니다.")

    def _hashkey(self, body: dict) -> str:
        data = json.dumps(body).encode("utf-8")
        req = urllib.request.Request(
            f"{self.kis.base_url}/uapi/hashkey",
            data=data,
            headers={
                "Content-Type": "application/json; charset=utf-8",
                "appkey": self.kis.app_key,
                "appsecret": self.kis.app_secret,
            },
        )
        with urllib.request.urlopen(req, timeout=15) as resp:
            return json.loads(resp.read()).get("HASH", "")

    def _post(self, path: str, tr_id: str, body: dict) -> dict:
        data = json.dumps(body).encode("utf-8")
        req = urllib.request.Request(
            f"{self.kis.base_url}/{path}",
            data=data,
            headers={
                "Content-Type": "application/json; charset=utf-8",
                "authorization": f"Bearer {self.kis.token()}",
                "appkey": self.kis.app_key,
                "appsecret": self.kis.app_secret,
                "tr_id": tr_id,
                "custtype": "P",
                "hashkey": self._hashkey(body),
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                return json.loads(resp.read())
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"KIS HTTP {exc.code}: {detail[:300]}") from exc

    def order(self, symbol: str, qty: int, side: str, price: int = 0) -> dict:
        """매수/매도 주문. side='buy'|'sell'. price=0이면 시장가."""
        if side not in ("buy", "sell"):
            raise ValueError("side는 buy 또는 sell")
        if qty <= 0:
            raise ValueError("수량은 1 이상")
        if self.paper:
            return self._paper_order(symbol, qty, side, price)
        # tr_id: 실거래 매수 TTTC0802U / 매도 TTTC0801U, 모의 VTTC0802U / VTTC0801U
        tr_map = {
            (True, "buy"): "TTTC0802U", (True, "sell"): "TTTC0801U",
            (False, "buy"): "VTTC0802U", (False, "sell"): "VTTC0801U",
        }
        tr_id = tr_map[(self.real, side)]
        ord_dvsn = "00" if price > 0 else "01"  # 00=지정가, 01=시장가
        body = {
            "CANO": self.cano,
            "ACNT_PRDT_CD": self.prod,
            "PDNO": symbol,
            "ORD_DVSN": ord_dvsn,
            "ORD_QTY": str(int(qty)),
            "ORD_UNPR": str(int(price)) if price > 0 else "0",
        }
        result = self._post("uapi/domestic-stock/v1/trading/order-cash", tr_id, body)
        if result.get("rt_cd") != "0":
            raise RuntimeError(f"주문 실패: {result.get('msg1', result)}")
        out = result.get("output") or {}
        return {"order_no": out.get("ODNO", ""), "time": out.get("ORD_TMD", ""), "msg": result.get("msg1", "")}

    def _paper_order(self, symbol: str, qty: int, side: str, price: int) -> dict:
        """가상 체결: 시장가는 현재 시세로 체결. 페이퍼 원장(현금/보유) 갱신."""
        fill = price if price > 0 else int(num(self.kis.quote(symbol).get("stck_prpr")))
        if fill <= 0:
            raise RuntimeError("현재가 조회 실패로 체결 불가")
        led = _paper_load()
        pos = led["positions"]
        if side == "buy":
            cost = fill * qty
            if cost > led["cash"]:
                raise RuntimeError(f"페이퍼 예수금 부족 (필요 {cost:,} / 보유 {int(led['cash']):,})")
            led["cash"] -= cost
            cur = pos.get(symbol, {"qty": 0, "avg": 0})
            tot = cur["qty"] + qty
            pos[symbol] = {"qty": tot, "avg": round((cur["qty"] * cur["avg"] + cost) / tot)}
        else:  # sell
            held = pos.get(symbol, {}).get("qty", 0)
            if qty > held:
                raise RuntimeError(f"페이퍼 보유 수량 부족 (보유 {held}주)")
            led["cash"] += fill * qty
            if qty == held:
                pos.pop(symbol, None)
            else:
                pos[symbol]["qty"] = held - qty
        _paper_save(led)
        return {"order_no": f"PAPER-{symbol}-{qty}", "time": "", "msg": f"[페이퍼] {fill:,}원 체결"}

    def balance(self) -> dict:
        """보유 종목 + 예수금 조회."""
        if self.paper:
            led = _paper_load()
            holdings = []
            for sym, p in led["positions"].items():
                try:
                    cur = num(self.kis.quote(sym).get("stck_prpr"))
                except Exception:
                    cur = p["avg"]
                pnl = ((cur - p["avg"]) / p["avg"] * 100) if p["avg"] else 0
                holdings.append({"name": sym, "symbol": sym, "qty": p["qty"], "avg": p["avg"], "pnl": round(pnl, 2)})
            return {"cash": led["cash"], "holdings": holdings}
        tr_id = "TTTC8434R" if self.real else "VTTC8434R"
        data = self.kis.get(
            "uapi/domestic-stock/v1/trading/inquire-balance",
            tr_id,
            {
                "CANO": self.cano, "ACNT_PRDT_CD": self.prod,
                "AFHR_FLPR_YN": "N", "OFL_YN": "", "INQR_DVSN": "02",
                "UNPR_DVSN": "01", "FUND_STTL_ICLD_YN": "N",
                "FNCG_AMT_AUTO_RDPT_YN": "N", "PRCS_DVSN": "00",
                "CTX_AREA_FK100": "", "CTX_AREA_NK100": "",
            },
        )
        holdings = [
            {"name": r.get("prdt_name"), "symbol": r.get("pdno"),
             "qty": num(r.get("hldg_qty")), "avg": num(r.get("pchs_avg_pric")),
             "pnl": num(r.get("evlu_pfls_rt"))}
            for r in (data.get("output1") or []) if num(r.get("hldg_qty")) > 0
        ]
        summary = (data.get("output2") or [{}])[0]
        cash = num(summary.get("dnca_tot_amt"))  # 예수금 총액
        return {"cash": cash, "holdings": holdings}


def _fmt_balance(bal: dict) -> str:
    lines = [f"💰 예수금: {int(bal['cash']):,}원", "📊 보유 종목:"]
    if not bal["holdings"]:
        lines.append("  (없음)")
    for h in bal["holdings"]:
        lines.append(f"  · {h['name']}({h['symbol']}) {int(h['qty'])}주 / 평단 {int(h['avg']):,} / 수익률 {h['pnl']}%")
    return "\n".join(lines)


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="KIS 수동 주문 (테스트용 CLI)")
    parser.add_argument("--balance", action="store_true", help="잔고 조회 (읽기 전용)")
    args = parser.parse_args()
    trader = KISTrader()
    mode = "실거래" if trader.real else "모의투자"
    print(f"[KIS {mode}] 계좌 {trader.cano}-{trader.prod}")
    if args.balance:
        print(_fmt_balance(trader.balance()))
    else:
        print("사용법: --balance  (주문은 텔레그램 영숙 봇을 통해)")


if __name__ == "__main__":
    main()
