#!/usr/bin/env python3
"""한국투자증권 OpenAPI 클라이언트"""
import os
import sys
import json
import requests
import hashlib
import time
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))
from _shared.env import load_env
load_env()

class KISClient:
    """한국투자증권 API 클라이언트"""

    def __init__(self):
        self.app_key = os.getenv("KIS_APP_KEY")
        self.app_secret = os.getenv("KIS_APP_SECRET")
        self.account_no = os.getenv("KIS_ACCOUNT_NO", "12345678-01")
        self.account_code = os.getenv("KIS_ACCOUNT_CODE", "01")

        # 계좌번호 파싱 (CANO: 앞 8자리, ACNT_PRDT_CD: 뒤 2자리)
        if "-" in self.account_no:
            parts = self.account_no.split("-")
            self.cano = parts[0]
            self.account_code = parts[1]
        else:
            self.cano = self.account_no[:8]
            if len(self.account_no) > 8:
                self.account_code = self.account_no[8:10]

        # 실전/모의 투자 구분
        self.is_real = os.getenv("KIS_REAL_MODE", "false").lower() == "true"

        if self.is_real:
            self.base_url = "https://openapi.koreainvestment.com:9443"
        else:
            self.base_url = "https://openapivts.koreainvestment.com:29443"

        self.token_cache_file = os.path.join(os.path.dirname(__file__), ".kis_token_cache.json")
        self.access_token = None
        self.token_expires = 0

        # 캐시된 토큰 로드
        self._load_token_cache()

        if not self.app_key or not self.app_secret:
            print("⚠️  KIS API 키가 설정되지 않았습니다")

    def _save_token_cache(self):
        """토큰 파일 캐시 저장"""
        try:
            with open(self.token_cache_file, "w", encoding="utf-8") as f:
                json.dump({"token": self.access_token, "expires": self.token_expires}, f)
        except Exception:
            pass

    def _load_token_cache(self):
        """캐시된 토큰 로드"""
        try:
            if os.path.exists(self.token_cache_file):
                with open(self.token_cache_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                if time.time() < data.get("expires", 0):
                    self.access_token = data["token"]
                    self.token_expires = data["expires"]
                    print("✅ KIS 토큰 캐시 로드 완료")
        except Exception:
            pass

    def _get_access_token(self) -> str:
        """액세스 토큰 발급 (캐싱)"""
        if self.access_token and time.time() < self.token_expires:
            return self.access_token

        url = f"{self.base_url}/oauth2/tokenP"
        headers = {"content-type": "application/json"}
        body = {
            "grant_type": "client_credentials",
            "appkey": self.app_key,
            "appsecret": self.app_secret
        }

        try:
            res = requests.post(url, headers=headers, json=body)
            data = res.json()

            if res.status_code == 200 and "access_token" in data:
                self.access_token = data["access_token"]
                self.token_expires = time.time() + (23 * 3600)
                self._save_token_cache()
                print(f"✅ KIS 액세스 토큰 발급 완료 (24시간 유효)")
                return self.access_token
            else:
                print(f"❌ KIS 토큰 발급 실패: {data}")
                return None
        except Exception as e:
            print(f"❌ KIS 토큰 발급 오류: {e}")
            return None

    def _make_request(self, method: str, path: str, tr_id: str, params: dict = None, body: dict = None) -> dict:
        """공통 API 요청"""
        token = self._get_access_token()
        if not token:
            return {"error": "토큰 발급 실패"}

        url = f"{self.base_url}{path}"
        headers = {
            "content-type": "application/json; charset=utf-8",
            "authorization": f"Bearer {token}",
            "appkey": self.app_key,
            "appsecret": self.app_secret,
            "tr_id": tr_id
        }

        try:
            if method == "GET":
                res = requests.get(url, headers=headers, params=params)
            else:
                res = requests.post(url, headers=headers, json=body)

            return res.json()
        except Exception as e:
            print(f"❌ KIS API 요청 오류: {e}")
            return {"error": str(e)}

    def get_balance(self) -> dict:
        """계좌 잔고 조회"""
        tr_id = "VTTC8434R" if not self.is_real else "TTTC8434R"

        params = {
            "CANO": self.cano,
            "ACNT_PRDT_CD": self.account_code,
            "AFHR_FLPR_YN": "N",  # 시간외단일가여부
            "OFL_YN": "N",  # 오프라인여부
            "INQR_DVSN": "01",  # 조회구분(01:대출일별, 02:종목별)
            "UNPR_DVSN": "01",  # 단가구분
            "FUND_STTL_ICLD_YN": "N",  # 펀드결제분포함여부
            "FNCG_AMT_AUTO_RDPT_YN": "N",  # 융자금액자동상환여부
            "PRCS_DVSN": "01",  # 처리구분
            "CTX_AREA_FK100": "",  # 연속조회검색조건100
            "CTX_AREA_NK100": ""  # 연속조회키100
        }

        result = self._make_request("GET", "/uapi/domestic-stock/v1/trading/inquire-balance", tr_id, params=params)
        return result

    def get_current_price(self, stock_code: str) -> dict:
        """현재가 조회"""
        tr_id = "FHKST01010100"

        params = {
            "FID_COND_MRKT_DIV_CODE": "J",  # 시장분류코드 (J:주식)
            "FID_INPUT_ISCD": stock_code  # 종목코드
        }

        result = self._make_request("GET", "/uapi/domestic-stock/v1/quotations/inquire-price", tr_id, params=params)
        return result

    def buy_stock(self, stock_code: str, quantity: int, price: int = 0) -> dict:
        """주식 매수 (시장가/지정가)

        Args:
            stock_code: 종목코드 (예: "005930")
            quantity: 수량
            price: 가격 (0이면 시장가, 아니면 지정가)
        """
        tr_id = "VTTC0802U" if not self.is_real else "TTTC0802U"

        body = {
            "CANO": self.cano,
            "ACNT_PRDT_CD": self.account_code,
            "PDNO": stock_code,  # 종목코드
            "ORD_DVSN": "01" if price > 0 else "01",  # 주문구분 (00:지정가, 01:시장가)
            "ORD_QTY": str(quantity),  # 주문수량
            "ORD_UNPR": str(price) if price > 0 else "0"  # 주문단가
        }

        result = self._make_request("POST", "/uapi/domestic-stock/v1/trading/order-cash", tr_id, body=body)
        return result

    def sell_stock(self, stock_code: str, quantity: int, price: int = 0) -> dict:
        """주식 매도 (시장가/지정가)"""
        tr_id = "VTTC0801U" if not self.is_real else "TTTC0801U"

        body = {
            "CANO": self.cano,
            "ACNT_PRDT_CD": self.account_code,
            "PDNO": stock_code,
            "ORD_DVSN": "01" if price > 0 else "01",
            "ORD_QTY": str(quantity),
            "ORD_UNPR": str(price) if price > 0 else "0"
        }

        result = self._make_request("POST", "/uapi/domestic-stock/v1/trading/order-cash", tr_id, body=body)
        return result

    def get_index_price(self, index_code: str) -> dict:
        """시장 지수 조회 (KOSPI: 0001, KOSDAQ: 1001)
        TR ID: FHPUP02100000 (업종/지수 현재가)
        """
        tr_id = "FHPUP02100000"
        params = {
            "FID_COND_MRKT_DIV_CODE": "U",   # U = 업종/지수
            "FID_INPUT_ISCD": index_code,      # 0001=KOSPI, 1001=KOSDAQ
        }
        result = self._make_request(
            "GET",
            "/uapi/domestic-stock/v1/quotations/inquire-index-price",
            tr_id,
            params=params
        )
        return result

    def get_daily_price(self, stock_code: str, days: int = 100) -> dict:
        """일봉 데이터 조회"""
        tr_id = "FHKST01010400"

        params = {
            "FID_COND_MRKT_DIV_CODE": "J",
            "FID_INPUT_ISCD": stock_code,
            "FID_PERIOD_DIV_CODE": "D",  # D:일봉, W:주봉, M:월봉
            "FID_ORG_ADJ_PRC": "0"  # 수정주가 (0:수정주가반영, 1:수정주가미반영)
        }

        result = self._make_request("GET", "/uapi/domestic-stock/v1/quotations/inquire-daily-price", tr_id, params=params)
        return result

    def get_minute_price(self, stock_code: str, interval: str = "1") -> dict:
        """분봉 데이터 조회 (당일)"""
        tr_id = "FHKST03010200"

        params = {
            "FID_COND_MRKT_DIV_CODE": "J",
            "FID_INPUT_ISCD": stock_code,
            "FID_INPUT_HOUR_1": "",  # 조회시작시간 (공백: 당일전체)
            "FID_PW_DATA_INCU_YN": "Y"  # 과거데이터포함여부
        }

        result = self._make_request("GET", "/uapi/domestic-stock/v1/quotations/inquire-time-itemchartprice", tr_id, params=params)
        return result


def test_kis_connection():
    """한국투자증권 API 연결 테스트"""
    print("=== 한국투자증권 API 연결 테스트 ===\n")

    client = KISClient()

    # 1. 토큰 발급 테스트
    print("1. 액세스 토큰 발급...")
    token = client._get_access_token()
    if token:
        print(f"   ✅ 토큰: {token[:20]}...\n")
    else:
        print("   ❌ 토큰 발급 실패\n")
        return

    # 2. 계좌 잔고 조회
    print("2. 계좌 잔고 조회...")
    balance = client.get_balance()
    if "output1" in balance:
        print(f"   ✅ 잔고 조회 성공")
        if balance["output1"]:
            for stock in balance["output1"][:3]:
                print(f"      {stock.get('prdt_name', 'N/A')}: {stock.get('hldg_qty', 0)}주")
    else:
        print(f"   ❌ 잔고 조회 실패: {balance}")

    # 3. 삼성전자 현재가 조회
    print("\n3. 삼성전자(005930) 현재가 조회...")
    price = client.get_current_price("005930")
    if "output" in price:
        current = price["output"].get("stck_prpr", "N/A")
        print(f"   ✅ 현재가: {current}원")
    else:
        print(f"   ❌ 현재가 조회 실패: {price}")

    print("\n=== 테스트 완료 ===")


if __name__ == "__main__":
    test_kis_connection()
