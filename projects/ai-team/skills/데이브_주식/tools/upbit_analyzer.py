# -*- coding: utf-8 -*-
"""
데이브 가상자산(업비트) 분석 및 보고서 생성 실행 스크립트
"""
import os
import sys
import datetime
import pandas as pd
import numpy as np

_here = os.path.dirname(os.path.abspath(__file__))
# projects/ai-team/skills/데이브_주식/tools -> projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
PROJECT_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
from _shared.notify import send

import json
import time

# 노션 업로드 주기 제어 (하루 2회)
last_notion_upload_time = 0
NOTION_UPLOAD_INTERVAL_SECONDS = 12 * 3600  # 12시간마다 (하루 2회)


try:
    from pydantic import BaseModel as PydanticBaseModel, Field as PydanticField, ConfigDict

    class BaseModel(PydanticBaseModel):
        model_config = ConfigDict(arbitrary_types_allowed=True)

    Field = PydanticField
except ImportError:
    def Field(description=""):
        return None

    class BaseModel:
        @classmethod
        def model_validate_json(cls, text):
            return cls(**json.loads(text))

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

load_env()

try:
    import pyupbit
except ModuleNotFoundError:
    import upbit_public as pyupbit

def get_upbit_client():
    """업비트 API 클라이언트를 로드하고 유효성을 검증합니다."""
    access_key = os.getenv("UPBIT_ACCESS_KEY", "").strip('"').strip("'")
    secret_key = os.getenv("UPBIT_SECRET_KEY", "").strip('"').strip("'")

    # 자리 표시자이거나 비어있으면 None 반환 (시뮬레이션 모드)
    if not access_key or not secret_key or "입력" in access_key or "입력" in secret_key:
        return None

    try:
        upbit = pyupbit.Upbit(access_key, secret_key)
        # 키 검증 시도 (단순 잔고 조회)
        res = upbit.get_balances()
        if res is None or (isinstance(res, dict) and "error" in res) or (isinstance(res, list) and len(res) > 0 and isinstance(res[0], dict) and "error" in res[0]):
            print(f"[Dave] Upbit API 키 검증 실패 (결과: {res})")
            return None
        return upbit
    except Exception as e:
        print(f"[Dave] Upbit API 키 검증 실패 (시뮬레이션 모드로 전환): {e}")
        return None

def calculate_supertrend(df, period=10, multiplier=3):
    """Supertrend 기술 지표 계산."""
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())
    tr = pd.concat([high_low, high_close, low_close], axis=1).max(axis=1)
    atr = tr.ewm(com=period-1, adjust=False, min_periods=period).mean()

    df['Basic_Upper_Band'] = ((df['High'] + df['Low']) / 2) + (multiplier * atr)
    df['Basic_Lower_Band'] = ((df['High'] + df['Low']) / 2) - (multiplier * atr)

    df['Final_Upper_Band'] = np.nan
    df['Final_Lower_Band'] = np.nan

    for i in range(len(df)):
        if i < period:
            df.loc[i, 'Final_Upper_Band'] = df.loc[i, 'Basic_Upper_Band']
            df.loc[i, 'Final_Lower_Band'] = df.loc[i, 'Basic_Lower_Band']
            continue

        if df.loc[i-1, 'Close'] <= df.loc[i-1, 'Final_Upper_Band']:
            df.loc[i, 'Final_Upper_Band'] = min(df.loc[i, 'Basic_Upper_Band'], df.loc[i-1, 'Final_Upper_Band'])
        else:
            df.loc[i, 'Final_Upper_Band'] = df.loc[i, 'Basic_Upper_Band']

        if df.loc[i-1, 'Close'] >= df.loc[i-1, 'Final_Lower_Band']:
            df.loc[i, 'Final_Lower_Band'] = max(df.loc[i, 'Basic_Lower_Band'], df.loc[i-1, 'Final_Lower_Band'])
        else:
            df.loc[i, 'Final_Lower_Band'] = df.loc[i, 'Basic_Lower_Band']

    df['Supertrend'] = np.nan
    df['Trend'] = np.nan

    for i in range(len(df)):
        if i < period:
            continue

        if df.loc[i-1, 'Trend'] == -1:
            if df.loc[i, 'Close'] <= df.loc[i, 'Final_Upper_Band']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Upper_Band']
                df.loc[i, 'Trend'] = -1
            else:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Lower_Band']
                df.loc[i, 'Trend'] = 1
        elif df.loc[i-1, 'Trend'] == 1:
            if df.loc[i, 'Close'] >= df.loc[i, 'Final_Lower_Band']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Lower_Band']
                df.loc[i, 'Trend'] = 1
            else:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Upper_Band']
                df.loc[i, 'Trend'] = -1
        else:
            if df.loc[i, 'Close'] > df.loc[i, 'Final_Upper_Band']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Lower_Band']
                df.loc[i, 'Trend'] = 1
            else:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Upper_Band']
                df.loc[i, 'Trend'] = -1

    df['Supertrend'] = df['Supertrend'].round(2)
    df['Trend'] = df['Trend'].apply(lambda x: '상승' if x == 1 else '하락' if x == -1 else np.nan)
    return df[['Date', 'High', 'Low', 'Close', 'Supertrend', 'Trend']].dropna()

def analyze_volume_profile(df: pd.DataFrame, bins: int = 10) -> list:
    """가격대별 매물대 분석 (POC 정보 포함)."""
    if 'Volume' not in df.columns or df['Volume'].sum() == 0:
        return []

    price_min = df['Low'].min()
    price_max = df['High'].max()
    step = (price_max - price_min) / bins

    buckets = []
    for i in range(bins):
        lo = price_min + step * i
        hi = lo + step
        mask = (df['Close'] >= lo) & (df['Close'] < hi)
        vol = int(df.loc[mask, 'Volume'].sum())
        buckets.append({"가격대": f"{lo:,.0f}~{hi:,.0f}원", "누적거래량": vol, "lo": lo, "hi": hi})

    buckets.sort(key=lambda x: x["누적거래량"], reverse=True)
    return [{"가격대": b["가격대"], "누적거래량": b["누적거래량"], "lo": b["lo"], "hi": b["hi"]} for b in buckets[:3]]

def calc_indicators(df: pd.DataFrame) -> dict:
    """RSI(14), MACD(12/26/9), Bollinger Bands(20,2), OBV, 거래량 회전율, EMA200, StochRSI, Heikin Ashi 계산."""
    close = df["Close"]
    volume = df["Volume"]
    result = {}

    # RSI
    delta = close.diff()
    gain = delta.clip(lower=0).ewm(com=13, adjust=False).mean()
    loss = (-delta.clip(upper=0)).ewm(com=13, adjust=False).mean()
    rs = gain / loss.replace(0, np.nan)
    rsi = rs / (1 + rs) * 100
    result["RSI14"] = round(float(rsi.iloc[-1]) if not np.isnan(rsi.iloc[-1]) else 50.0, 1)

    # MACD
    ema12 = close.ewm(span=12, adjust=False).mean()
    ema26 = close.ewm(span=26, adjust=False).mean()
    macd = ema12 - ema26
    signal = macd.ewm(span=9, adjust=False).mean()
    result["MACD"]        = round(float(macd.iloc[-1]), 0)
    result["MACD_Signal"] = round(float(signal.iloc[-1]), 0)
    result["MACD_상태"]   = "골든크로스" if macd.iloc[-1] > signal.iloc[-1] else "데드크로스"

    # 볼린저밴드
    ma20 = close.rolling(20).mean()
    std20 = close.rolling(20).std()
    result["BB_상단"] = round(float((ma20 + 2 * std20).iloc[-1]), 0)
    result["BB_중단"] = round(float(ma20.iloc[-1]), 0)
    result["BB_하단"] = round(float((ma20 - 2 * std20).iloc[-1]), 0)
    result["BB_위치"] = "밴드 하단 이탈" if close.iloc[-1] < result["BB_하단"] else (
        "밴드 상단 돌파" if close.iloc[-1] > result["BB_상단"] else "밴드 내부")

    # OBV
    obv = (np.sign(close.diff().fillna(0)) * volume).cumsum()
    result["OBV_추세"] = "상승" if obv.iloc[-1] > obv.iloc[-5] else "하락"
    result["OBV_5일변화"] = int(obv.iloc[-1] - obv.iloc[-5])

    # OBV 다이버전스 (세력 매집/배분 탐지)
    price_trend_5 = "상승" if close.iloc[-1] > close.iloc[-5] else "하락"
    obv_trend_5 = "상승" if obv.iloc[-1] > obv.iloc[-5] else "하락"
    if price_trend_5 == "하락" and obv_trend_5 == "상승":
        result["OBV_다이버전스"] = "✅ 상승 다이버전스(세력 매집 신호)"
    elif price_trend_5 == "상승" and obv_trend_5 == "하락":
        result["OBV_다이버전스"] = "⚠️ 하락 다이버전스(세력 배분 신호)"
    else:
        result["OBV_다이버전스"] = "없음"

    # CVD (Cumulative Volume Delta) — 매수/매도 압력 누적
    buy_vol = volume.where(close.diff() > 0, 0)
    sell_vol = volume.where(close.diff() <= 0, 0)
    cvd = (buy_vol - sell_vol).cumsum()
    cvd_trend = "상승(매수 우위)" if cvd.iloc[-1] > cvd.iloc[-5] else "하락(매도 우위)"
    if price_trend_5 == "상승" and "하락" in cvd_trend:
        result["CVD_다이버전스"] = "⚠️ 하락 다이버전스(고래 미참여 상승 — 세력 배분 의심)"
    elif price_trend_5 == "하락" and "상승" in cvd_trend:
        result["CVD_다이버전스"] = "✅ 상승 다이버전스(저가 매집 중)"
    else:
        result["CVD_다이버전스"] = "없음"
    result["CVD_추세"] = cvd_trend

    # 세력 매집 패턴 탐지
    price_change_5d = (close.iloc[-1] - close.iloc[-5]) / close.iloc[-5] * 100
    avg_vol_20 = volume.rolling(20).mean().iloc[-1]
    recent_vol_5_avg = volume.iloc[-5:].mean()
    recent_vol_ratio = recent_vol_5_avg / avg_vol_20 if avg_vol_20 > 0 else 1.0
    if recent_vol_ratio >= 2.0 and abs(price_change_5d) < 3.0:
        result["세력매집패턴"] = "✅ 바닥 매집(거래량↑ 가격 정체 — 매집 강력 의심)"
    elif price_change_5d < -5.0 and recent_vol_ratio >= 1.5:
        result["세력매집패턴"] = "⚠️ 세력 손털기(급락+거래량 — shakeout 의심)"
    elif price_change_5d > 5.0 and recent_vol_ratio < 0.8:
        result["세력매집패턴"] = "⚠️ 거래량 없는 상승(수급 취약 — 가짜 펌핑 의심)"
    else:
        result["세력매집패턴"] = "중립"


    # 거래량 회전율
    avg_vol = volume.rolling(20).mean().iloc[-1]
    today_vol = volume.iloc[-1]
    result["거래량회전율"] = f"{round(today_vol / avg_vol * 100, 0):.0f}%" if avg_vol > 0 else "N/A"
    result["거래량평균대비"] = "급증(매집가능)" if today_vol > avg_vol * 5 else (
        "증가" if today_vol > avg_vol * 1.5 else "보통")

    # 거래량 방향성
    up_vol   = volume[close.diff() > 0].mean()
    down_vol = volume[close.diff() < 0].mean()
    result["거래량방향성"] = "상승 시 거래량 우위(긍정)" if up_vol > down_vol else "하락 시 거래량 우위(경고)"

    # EMA 200
    ema200 = close.ewm(span=200, adjust=False).mean()
    result["EMA200"] = round(float(ema200.iloc[-1]), 0)
    result["대추세_EMA200"] = "상승 국면(LONG 전용)" if close.iloc[-1] > ema200.iloc[-1] else "하락 국면(SHORT 전용)"

    # Stochastic RSI
    rsi_min = rsi.rolling(14).min()
    rsi_max = rsi.rolling(14).max()
    stoch_rsi = (rsi - rsi_min) / (rsi_max - rsi_min).replace(0, np.nan)
    stoch_rsi_k = stoch_rsi.rolling(3).mean() * 100
    stoch_rsi_d = stoch_rsi_k.rolling(3).mean()
    result["StochRSI_K"] = round(float(stoch_rsi_k.iloc[-1]), 1) if not np.isnan(stoch_rsi_k.iloc[-1]) else 50.0
    result["StochRSI_D"] = round(float(stoch_rsi_d.iloc[-1]), 1) if not np.isnan(stoch_rsi_d.iloc[-1]) else 50.0
    result["StochRSI_상태"] = (
        "과매도 골든크로스" if stoch_rsi_k.iloc[-1] < 20 and stoch_rsi_k.iloc[-1] > stoch_rsi_d.iloc[-1] and stoch_rsi_k.iloc[-2] <= stoch_rsi_d.iloc[-2]
        else "과매도" if stoch_rsi_k.iloc[-1] < 20
        else "과매수" if stoch_rsi_k.iloc[-1] > 80
        else "중립"
    )

    # Heikin Ashi
    ha_close = (df['Open'] + df['High'] + df['Low'] + df['Close']) / 4
    ha_open = np.zeros(len(df))
    ha_open[0] = (df['Open'].iloc[0] + df['Close'].iloc[0]) / 2
    for i in range(1, len(df)):
        ha_open[i] = (ha_open[i-1] + ha_close.iloc[i-1]) / 2

    latest_ha_close = ha_close.iloc[-1]
    latest_ha_open = ha_open[-1]
    prev_ha_close = ha_close.iloc[-2]
    prev_ha_open = ha_open[-2]
    latest_ha_low = min(df['Low'].iloc[-1], latest_ha_open, latest_ha_close)

    body_size = abs(latest_ha_close - latest_ha_open)
    prev_body_size = abs(prev_ha_close - prev_ha_open)

    is_bullish = latest_ha_close > latest_ha_open
    lower_tail = min(latest_ha_open, latest_ha_close) - latest_ha_low

    has_no_lower_tail = lower_tail <= (latest_ha_close * 0.0005)
    is_body_longer = body_size > prev_body_size

    result["HeikinAshi_상태"] = (
        "아래꼬리 없는 장대양봉(매수 신호)" if is_bullish and has_no_lower_tail and is_body_longer
        else "양봉" if is_bullish
        else "음봉"
    )
    result["HeikinAshi_양봉여부"] = "양봉" if is_bullish else "음봉"
    result["HeikinAshi_아래꼬리여부"] = "없음" if has_no_lower_tail else "있음"
    result["HeikinAshi_몸통길어짐여부"] = "길어짐" if is_body_longer else "짧아짐"

    # Volume Spike (이전 5일 평균 거래량 대비 당일 거래량 배율)
    avg_vol_5d = volume.shift(1).rolling(5).mean().iloc[-1]
    ratio = today_vol / avg_vol_5d if avg_vol_5d > 0 else 0
    result["VolumeSpike"] = "✅ 급증" if ratio >= 2.0 else "❌ 보통"
    result["Volume_Avg5"] = round(float(avg_vol_5d), 1)
    result["Volume_배율"] = round(float(ratio), 1)

    # 24시간 거래대금 및 유동성 체크 (최소 10억 원 기준)
    today_value = df["Value"].iloc[-1] if "Value" in df.columns else 0.0
    result["거래대금_24h"] = round(float(today_value), 0)
    result["유동성_상태"] = "✅ 충분" if today_value >= 1000000000.0 else "❌ 부족 (10억 미만)"

    # 워시트레이딩(통정매매) 탐지 — 거래량 급증 + 가격 변화 미미
    last_price_change_pct = abs(close.diff().iloc[-1] / close.iloc[-2]) if close.iloc[-2] != 0 else 0
    if ratio >= 3.0 and last_price_change_pct < 0.01:
        result["통정매매의심"] = "⚠️ 워시트레이딩 의심(거래량↑↑ 가격변화 1% 미만)"
    else:
        result["통정매매의심"] = "정상"

    return result

def _calc_atr(records: list, period: int = 10) -> float:
    """ATR 계산."""
    if len(records) < period + 1:
        return 0.0
    trs = []
    for i in range(1, len(records)):
        high = records[i]["High"]
        low  = records[i]["Low"]
        prev_close = records[i - 1]["Close"]
        tr = max(high - low, abs(high - prev_close), abs(low - prev_close))
        trs.append(tr)
    return round(float(np.mean(trs[-period:])), 0)

def get_kimchi_premium(ticker="KRW-BTC"):
    """김치 프리미엄 계산 (업비트 vs 바이낸스 가격 괴리율)."""
    try:
        import requests
        binance_symbol = ticker.replace("KRW-", "") + "USDT"
        b_resp = requests.get(f"https://api.binance.com/api/v3/ticker/price?symbol={binance_symbol}", timeout=5)
        binance_usd = float(b_resp.json()["price"])

        fx_resp = requests.get("https://api.exchangerate-api.com/v4/latest/USD", timeout=5)
        usd_krw = float(fx_resp.json()["rates"]["KRW"])

        binance_krw = binance_usd * usd_krw
        upbit_krw = float(pyupbit.get_current_price(ticker))
        premium = (upbit_krw - binance_krw) / binance_krw * 100

        status = "참고값(거래 제한 없음)"

        return {"김치프리미엄": round(premium, 2), "상태": status,
                "업비트가": upbit_krw, "바이낸스환산가": round(binance_krw, 0)}
    except Exception as e:
        return {"김치프리미엄": None, "상태": "조회불가", "error": str(e)}


def get_upbit_data(ticker="KRW-BTC"):
    """업비트에서 일봉 시세 데이터를 추출하고 보조지표 및 Supertrend를 분석합니다."""
    try:
        # 최근 300일 데이터 가져오기 (EMA 200 계산용)
        df = pyupbit.get_ohlcv(ticker, interval="day", count=300)
        if df is None or df.empty:
            return {"error": "업비트에서 OHLCV 데이터를 가져올 수 없습니다."}

        df = df.reset_index().rename(columns={
            "index": "Date",
            "open": "Open",
            "high": "High",
            "low": "Low",
            "close": "Close",
            "volume": "Volume",
            "value": "Value"
        })

        df_supertrend = calculate_supertrend(df.copy())
        current_price = df_supertrend['Close'].iloc[-1]
        current_trend = df_supertrend['Trend'].iloc[-1]

        indicators = calc_indicators(df)
        kimchi = get_kimchi_premium(ticker)

        # 매물대는 최근 30일치 데이터로만 계산
        volume_profile = analyze_volume_profile(df.tail(30))

        return {
            "ticker": ticker,
            "현재가": current_price,
            "현재추세": current_trend,
            "최근 30일 슈퍼트렌드 분석": df_supertrend.tail(30).to_dict(orient='records'),
            "보조지표": indicators,
            "매물대 상위 3구간": volume_profile,
            "김치프리미엄": kimchi,
        }
    except Exception as e:
        return {"error": f"가상자산 시세 조회 중 오류 발생: {e}"}

class TradeDecision:
    decision: str = Field(description="최종 매매 결정. 반드시 'BUY', 'SELL', 'HOLD' 중 하나여야 합니다.")
    percentage: int = Field(description="매수/매도할 자산 비중 (0~100 %)")
    reason: str = Field(description="판단에 대한 핵심 근거 (한두 문장)")
    report: str = Field(description="출력 양식 템플릿에 맞추어 작성된 마크다운 형식의 최종 마스터 리포트 전체 텍스트")

    def __init__(self, decision: str = "HOLD", percentage: int = 0, reason: str = "", report: str = ""):
        self.decision = decision
        self.percentage = percentage
        self.reason = reason
        self.report = report


def parse_trade_decision(text: str) -> TradeDecision:
    """Parse an LLM JSON response without relying on Pydantic internals."""
    raw = (text or "").strip()
    if raw.startswith("```"):
        raw = raw.strip("`").strip()
        if raw.lower().startswith("json"):
            raw = raw[4:].strip()
    start = raw.find("{")
    end = raw.rfind("}")
    if start >= 0 and end >= start:
        raw = raw[start:end + 1]

    data = json.loads(raw)
    decision = str(data.get("decision", "HOLD")).upper()
    if decision not in {"BUY", "SELL", "HOLD"}:
        decision = "HOLD"

    try:
        percentage = int(float(data.get("percentage", 0)))
    except Exception:
        percentage = 0
    percentage = max(0, min(100, percentage))

    return TradeDecision(
        decision=decision,
        percentage=percentage,
        reason=str(data.get("reason", ""))[:500],
        report=str(data.get("report", "")),
    )

def _update_consecutive_holds(decision: str):
    """HOLD 연속 카운터 업데이트"""
    holds_file = os.path.join(os.path.dirname(__file__), ".consecutive_holds.txt")
    try:
        current = 0
        if os.path.exists(holds_file):
            with open(holds_file, "r") as f:
                current = int(f.read().strip())

        if decision == "HOLD":
            current += 1
        else:
            current = 0

        with open(holds_file, "w") as f:
            f.write(str(current))
    except Exception as e:
        print(f"[Dave] HOLD 카운터 업데이트 실패: {e}")

def get_common_trader_prompt():
    """공통 트레이더 시스템 프롬프트"""
    return """너는 암호화폐 매매 최종 판단 AI다.
목표는 제한된 토큰으로 기대값이 양수인 거래를 반복하는 것이다.

원칙:
- 완벽한 진입점보다 확률 우위가 중요하다.
- HOLD는 명확한 회피 사유가 있을 때만 선택한다.
- 단순 불확실성만으로 HOLD 금지.
- 예상 승률 55% 이상 또는 RR 1:1.5 이상이면 진입 검토.
- 항상 BUY, SELL, HOLD 중 하나만 선택한다.
- 설명은 40자 이내.
- 사고 과정 출력 금지.

강제 HOLD:
- FOMC/CPI 전후 24시간
- 연속손실 제한 초과
- 일일손실 제한 초과
- 거래 쿨다운 중

출력 JSON:
{
  "decision": "BUY|SELL|HOLD",
  "percentage": 0|5|10|20|40|50,
  "confidence": 0-100,
  "reason": "40자 이내"
}"""

def load_system_instruction():
    """데이브: 보수적 트레이더 (극존칭)"""
    common = get_common_trader_prompt()
    dave_specific = """

--- 데이브 특화 ---
성향: 보수적 트레이더 (극존칭 사용)
- 안정적 추세와 리스크 관리 우선
- 과매수 구간 진입 신중
- 강한 근거 없는 공격적 진입 회피
- 5회 이상 HOLD 반복 시 기회비용 재검토

점수 → 판단:
85~100: BUY 20%
70~84: BUY 10%
55~69: BUY 5%
40~54: HOLD
0~39: HOLD

예외 규칙:
- 가격↓ + OBV↑ → BUY 가능 (세력 매집)
- EMA200 위 + 거래량↑ → BUY 우선
- StochRSI > 80 → 신규 BUY 신중
- 김프는 참고값이며 단독 SELL/HOLD 또는 신규 진입 차단 근거로 쓰지 않는다."""

    return common + dave_specific

def calculate_trade_score(indicators: dict, current_trend: str) -> dict:
    """코드가 점수를 계산 (LLM은 판단만)"""
    trend_score = 0
    volume_score = 0
    momentum_score = 0
    support_score = 0
    sentiment_score = 0
    btc_sync_score = 0

    # 추세 강도 (0-25)
    if "상승" in current_trend:
        trend_score = 20
    elif "하락" in current_trend:
        trend_score = 5
    else:
        trend_score = 10

    if indicators.get('대추세_EMA200') == '상승 국면(LONG 전용)':
        trend_score += 5

    # 거래량 (0-20)
    if indicators.get('VolumeSpike') == '✅ 급증':
        volume_score = 20
    elif indicators.get('거래량평균대비') == '증가':
        volume_score = 12
    else:
        volume_score = 5

    # 모멘텀 (0-20)
    stoch_status = indicators.get('StochRSI_상태', '')
    if '골든크로스' in stoch_status:
        momentum_score = 20
    elif '과매도' in stoch_status:
        momentum_score = 15
    elif '과매수' in stoch_status:
        momentum_score = 5
    else:
        momentum_score = 10

    if indicators.get('MACD_상태') == '골든크로스':
        momentum_score += 5

    # 지지/저항 (0-15)
    bb_pos = indicators.get('BB_위치', '')
    if '하단' in bb_pos:
        support_score = 15
    elif '상단' in bb_pos:
        support_score = 5
    else:
        support_score = 10

    # 심리 (0-10)
    if indicators.get('OBV_다이버전스') == '매집신호(상승전환가능)':
        sentiment_score = 10
    elif indicators.get('세력매집패턴') == '✅ 세력 매집':
        sentiment_score += 5

    # BTC 동조성 (0-10) - 기본값
    btc_sync_score = 8

    total_score = trend_score + volume_score + momentum_score + support_score + sentiment_score + btc_sync_score

    return {
        'total': total_score,
        'trend': trend_score,
        'volume': volume_score,
        'momentum': momentum_score,
        'support': support_score,
        'sentiment': sentiment_score,
        'btc_sync': btc_sync_score
    }

def build_compact_trade_prompt(
    ticker: str,
    is_simulated: bool,
    current_price: float,
    current_trend: str,
    indicators: dict,
    loss_now: float,
    kimchi: dict,
    consecutive_holds: int = 0,
) -> str:
    """간소화된 입력 프롬프트 (점수는 코드가 계산)"""
    scores = calculate_trade_score(indicators, current_trend)

    risk_status = "정상"
    if is_simulated:
        risk_status = "시뮬레이션"

    prompt = f"""코인: {ticker}
가격: {current_price:.0f}원
추세: {current_trend}
추세점수: {scores['trend']}/25
거래량점수: {scores['volume']}/20
모멘텀점수: {scores['momentum']}/20
지지저항점수: {scores['support']}/15
심리점수: {scores['sentiment']}/10
BTC동조점수: {scores['btc_sync']}/10
총점: {scores['total']}/100
RSI: {indicators.get('RSI14')}
StochRSI: {indicators.get('StochRSI_K')}
OBV: {indicators.get('OBV_추세')}
OBV다이버: {indicators.get('OBV_다이버전스')}
HA: {indicators.get('HeikinAshi_양봉여부')}
리스크상태: {risk_status}
최근HOLD: {consecutive_holds}회
보유PNL: {loss_now}%"""

    return prompt

def run_gemini_trade_decision(query: str = "", ticker: str = "KRW-BTC") -> TradeDecision:
    # 1. 시세 데이터 및 기술분석
    data = get_upbit_data(ticker)
    if "error" in data:
        raise Exception(data["error"])

    current_price = data["현재가"]
    current_trend = data["현재추세"]
    indicators = data["보조지표"]
    kimchi = data.get("김치프리미엄", {})

    # 2. 잔고 조회 (API 키가 없으면 시뮬레이션 모드)
    upbit_client = get_upbit_client()
    is_simulated = (upbit_client is None)

    if not is_simulated:
        try:
            crypto_balance = float(upbit_client.get_balance(ticker))
            avg_buy_price = float(upbit_client.get_avg_buy_price(ticker))
        except Exception as e:
            print(f"[Dave] 잔고 조회 오류로 시뮬레이션 모드 전환: {e}")
            is_simulated = True

    if is_simulated:
        crypto_balance = 0.05  # 예시 비트코인 보유량
        avg_buy_price = 95000000.0  # 평단가 예시

    loss_now = 0.0
    if crypto_balance > 0 and avg_buy_price > 0:
        loss_now = round((current_price - avg_buy_price) / avg_buy_price * 100, 1)

    # consecutive_holds 추적 (전역 변수 또는 파일 기반)
    consecutive_holds = 0
    holds_file = os.path.join(os.path.dirname(__file__), ".consecutive_holds.txt")
    try:
        if os.path.exists(holds_file):
            with open(holds_file, "r") as f:
                consecutive_holds = int(f.read().strip())
    except:
        consecutive_holds = 0

    prompt = build_compact_trade_prompt(
        ticker=ticker,
        is_simulated=is_simulated,
        current_price=current_price,
        current_trend=current_trend,
        indicators=indicators,
        loss_now=loss_now,
        kimchi=kimchi,
        consecutive_holds=consecutive_holds,
    )
    system = load_system_instruction()

    # 1) Ollama 우선
    try:
        from _shared.llm import ollama as lm_ollama
        print(f"[Dave] Calling Ollama for {ticker}...")
        ollama_result = lm_ollama(prompt, system=system, max_tokens=600, temperature=0.1, task="trading")
        if ollama_result:
            try:
                return parse_trade_decision(ollama_result)
            except Exception as parse_err:
                print(f"[Dave] Ollama JSON 파싱 실패: {parse_err} → GPT 폴백")
    except Exception as ollama_err:
        print(f"[Dave] Ollama 실패: {ollama_err} → GPT 폴백")

    # 2) GPT 폴백
    try:
        from _shared.llm import gpt as lm_gpt
        print(f"[Dave] Calling GPT for {ticker}...")
        gpt_result = lm_gpt(prompt, system=system, max_tokens=300, temperature=0.1, json_mode=True)
        if gpt_result:
            decision = parse_trade_decision(gpt_result)
            _update_consecutive_holds(decision.decision)
            return decision
    except Exception as gpt_err:
        print(f"[Dave] GPT 폴백 실패: {gpt_err}")

    # 3) Gemini 폴백
    try:
        from _shared.llm import gemini as lm_gemini
        print(f"[Dave] Calling Gemini for {ticker}...")
        gemini_result = lm_gemini(prompt, system=system, max_tokens=300, temperature=0.1, json_mode=True)
        if gemini_result:
            decision = parse_trade_decision(gemini_result)
            _update_consecutive_holds(decision.decision)
            return decision
    except Exception as gemini_err:
        print(f"[Dave] Gemini 폴백 실패: {gemini_err}")

    # 4) 모든 AI 실패 시 안전 HOLD
    _update_consecutive_holds("HOLD")
    return TradeDecision(
        decision="HOLD",
        percentage=0,
        reason="AI 응답 실패로 안전 관망",
        report="## 데이브 안전 모드\n\nAI 응답 실패로 HOLD 처리했습니다.",
    )

def run_analysis(query: str = "", ticker: str = "KRW-BTC") -> str:
    print(f"[Dave] Starting Upbit crypto analysis: {query}")
    try:
        decision_data = run_gemini_trade_decision(query, ticker)
        report = decision_data.report

        # 영숙 보고 섹션 후처리 제거
        for marker in ["영숙 보고", "영숙이 보고", "영숙님", "📱"]:
            if marker in report:
                report = report[:report.index(marker)].rstrip("-— \n")

        # 분석 결과를 reports/research/dave_upbit_analysis.md 에 기록
        report_dir = os.path.join(PROJECT_ROOT, "reports", "research")
        os.makedirs(report_dir, exist_ok=True)
        report_path = os.path.join(report_dir, "dave_upbit_analysis.md")

        with open(report_path, "w", encoding="utf-8") as f:
            f.write(report)

        # Notion에 12시간마다만 자동 동기화 업로드 (하루 2회)
        global last_notion_upload_time
        current_time = time.time()
        if current_time - last_notion_upload_time >= NOTION_UPLOAD_INTERVAL_SECONDS:
            try:
                from _shared.notion_report_manager import NotionReportManager
                manager = NotionReportManager()
                today_str = datetime.datetime.now().strftime("%Y-%m-%d")
                manager.create_report_entry(
                    agent_name="데이브",
                    task_title=f"업비트 {ticker} {today_str} 분석 브리핑",
                    result=report
                )
                last_notion_upload_time = current_time
                print("[Dave] Upbit Notion report sync completed successfully.")
            except Exception as notion_err:
                print(f"[Dave] Notion sync warning: {notion_err}")
        else:
            remaining = NOTION_UPLOAD_INTERVAL_SECONDS - (current_time - last_notion_upload_time)
            print(f"[Dave] Notion upload skipped (next upload in {remaining/3600:.1f}h)")

        return report
    except Exception as e:
        return f"❌ [데이브] 분석 중 오류 발생: {e}"
def execute_buy(ticker: str, krw_amount: float):
    """시장가 매수 주문 실행"""
    upbit_client = get_upbit_client()
    if upbit_client is None:
        err_msg = "❌ [Dave] 업비트 API 키가 올바르게 설정되지 않아 매수를 수행할 수 없습니다 (시뮬레이션 모드에서는 주문이 불가합니다)."
        send(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n❌ 원인: API 키 미설정")
        return err_msg
    try:
        res = upbit_client.buy_market_order(ticker, krw_amount)
        if res is None:
            err_msg = "❌ [Dave] 매수 주문 실패: 주문 결과가 없습니다 (잔고 부족 또는 API 오류 가능성)."
            send(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: 잔고 부족 또는 API 오류")
            return err_msg
        if isinstance(res, dict):
            if "error" in res:
                err_msg = f"❌ [Dave] 매수 주문 실패: {res['error'].get('message', '알 수 없는 오류')}"
                send(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: {res['error'].get('message')}")
                return err_msg
            if "uuid" in res:
                success_msg = f"✅ [Dave] 시장가 매수 주문 성공!\n주문 결과: {res}"
                send(f"🤖 [데이브] 매수 성공! 🎉\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n🆔 주문ID: {res.get('uuid')}")
                return success_msg
        if isinstance(res, str):
            err_msg = f"❌ [Dave] 매수 주문 실패: {res}"
            send(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: {res}")
            return err_msg
        success_msg = f"✅ [Dave] 시장가 매수 주문 접수 완료\n주문 결과: {res}"
        send(f"🤖 [데이브] 매수 주문 접수\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원")
        return success_msg
    except Exception as e:
        err_msg = f"❌ [Dave] 매수 주문 중 오류 발생: {e}"
        send(f"🤖 [데이브] 매수 에러 ❌\n📌 대상: {ticker}\n❌ 에러 내용: {e}")
        return err_msg

def execute_sell(ticker: str, volume: float):
    """시장가 매도 주문 실행"""
    upbit_client = get_upbit_client()
    if upbit_client is None:
        err_msg = "❌ [Dave] 업비트 API 키가 올바르게 설정되지 않아 매도를 수행할 수 없습니다 (시뮬레이션 모드에서는 주문이 불가합니다)."
        send(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n❌ 원인: API 키 미설정")
        return err_msg
    try:
        res = upbit_client.sell_market_order(ticker, volume)
        if res is None:
            err_msg = "❌ [Dave] 매도 주문 실패: 주문 결과가 없습니다 (잔고 부족 또는 API 오류 가능성)."
            send(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: 잔고 부족 또는 API 오류")
            return err_msg
        if isinstance(res, dict):
            if "error" in res:
                err_msg = f"❌ [Dave] 매도 주문 실패: {res['error'].get('message', '알 수 없는 오류')}"
                send(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: {res['error'].get('message')}")
                return err_msg
            if "uuid" in res:
                success_msg = f"✅ [Dave] 시장가 매도 주문 성공!\n주문 결과: {res}"
                send(f"🤖 [데이브] 매도 성공! 📉🎉\n📌 대상: {ticker}\n📉 수량: {volume}\n🆔 주문ID: {res.get('uuid')}")
                return success_msg
        if isinstance(res, str):
            err_msg = f"❌ [Dave] 매도 주문 실패: {res}"
            send(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: {res}")
            return err_msg
        success_msg = f"✅ [Dave] 시장가 매도 주문 접수 완료\n주문 결과: {res}"
        send(f"🤖 [데이브] 매도 주문 접수\n📌 대상: {ticker}\n📉 수량: {volume}")
        return success_msg
    except Exception as e:
        err_msg = f"❌ [Dave] 매도 주문 중 오류 발생: {e}"
        send(f"🤖 [데이브] 매도 에러 ❌\n📌 대상: {ticker}\n❌ 에러 내용: {e}")
        return err_msg

def execute_sell_all(ticker: str):
    """전량 시장가 매도 주문 실행"""
    upbit_client = get_upbit_client()
    if upbit_client is None:
        return "❌ [Dave] 업비트 API 키가 올바르게 설정되지 않아 전량 매도를 수행할 수 없습니다."
    try:
        currency = ticker.split("-")[1]
        balance = float(upbit_client.get_balance(ticker))
        if balance <= 0:
            return f"❌ [Dave] 보유 중인 {currency} 잔고가 없습니다."
        return execute_sell(ticker, balance)
    except Exception as e:
        return f"❌ [Dave] 전량 매도 중 오류 발생: {e}"

if __name__ == "__main__":
    # 사용법:
    # 1. 분석: python upbit_analyzer.py [KRW-BTC/ticker] [질문]
    # 2. 매수: python upbit_analyzer.py --buy [KRW-BTC] [금액]
    # 3. 매도: python upbit_analyzer.py --sell [KRW-BTC] [수량]
    pass
