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

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

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

        if premium > 3:
            status = "🔴 과열(김프 3%↑ — 한국 FOMO 과잉, 고점 주의)"
        elif premium < -2:
            status = "🟢 역프리미엄(글로벌 대비 저평가 — 매집 기회 가능)"
        else:
            status = "🟡 정상 범위"

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

class TradeDecision(BaseModel):
    decision: str = Field(description="최종 매매 결정. 반드시 'BUY', 'SELL', 'HOLD' 중 하나여야 합니다.")
    percentage: int = Field(description="매수/매도할 자산 비중 (0~100 %)")
    reason: str = Field(description="판단에 대한 핵심 근거 (한두 문장)")
    report: str = Field(description="출력 양식 템플릿에 맞추어 작성된 마크다운 형식의 최종 마스터 리포트 전체 텍스트")

    def __init__(self, decision: str = "HOLD", percentage: int = 0, reason: str = "", report: str = ""):
        self.decision = decision
        self.percentage = percentage
        self.reason = reason
        self.report = report

def load_system_instruction():
    """Few-shot + 핵심 규칙 (토큰 최소화 + 품질 향상)"""
    return """AI 트레이더 데이브. 극존칭.

규칙:
1. FOMC/CPI 24h전후→HOLD
2. 김프15%+→SELL
3. 가격↓+OBV↑→BUY(매집)
4. HA양봉3개→상승확인
5. StochRSI>80→과열HOLD
6. EMA200위+거래량↑→BUY

예시1:
입력: BTC 95M|상승|RSI:45|OBV:상승|HA:양봉
→ {"decision":"BUY","percentage":40,"reason":"OBV 다이버전스 세력 매집"}

예시2:
입력: BTC 95M|하락|RSI:85|OBV:하락|김프:18%
→ {"decision":"SELL","percentage":50,"reason":"과열+김프과열 고점"}

분석 단계:
1. 추세? 2. 거래량? 3. 지표? 4. 결론"""

def build_compact_trade_prompt(
    ticker: str,
    query: str,
    today_str: str,
    is_simulated: bool,
    current_price: float,
    current_trend: str,
    indicators: dict,
    poc_status: str,
    atr: float,
    total_asset: float,
    krw_balance: float,
    crypto_balance: float,
    avg_buy_price: float,
    loss_now: float,
    kimchi: dict,
) -> str:
    """LLM에 실제 전송하는 압축형 판단 프롬프트."""
    coin = ticker.split("-")[-1]
    fields = [
        f"T:{ticker}",
        f"D:{today_str}",
        f"M:{'SIM' if is_simulated else 'LIVE'}",
        f"Q:{query[:80]}",
        f"P:{current_price:.0f}",
        f"ST:{current_trend}",
        f"EMA:{indicators.get('대추세_EMA200')}",
        f"RSI:{indicators.get('RSI14')}",
        f"Stoch:{indicators.get('StochRSI_K')}/{indicators.get('StochRSI_D')}:{indicators.get('StochRSI_상태')}",
        f"HA:{indicators.get('HeikinAshi_상태')}",
        f"Vol:{indicators.get('VolumeSpike')}:{indicators.get('Volume_배율')}x",
        f"OBV:{indicators.get('OBV_추세')}:{indicators.get('OBV_다이버전스')}",
        f"CVD:{indicators.get('CVD_추세')}:{indicators.get('CVD_다이버전스')}",
        f"MACD:{indicators.get('MACD_상태')}",
        f"BB:{indicators.get('BB_위치')}",
        f"POC:{poc_status[:60]}",
        f"ATR:{atr:.0f}",
        f"KP:{kimchi.get('김치프리미엄', kimchi.get('premium_pct', 'N/A'))}:{kimchi.get('상태', kimchi.get('signal', 'N/A'))}",
        f"Wash:{indicators.get('통정매매의심')}",
        f"Accum:{indicators.get('세력매집패턴')}",
        f"Acct:asset{total_asset:.0f}|krw{krw_balance:.0f}|{coin}{crypto_balance}|avg{avg_buy_price:.0f}|pnl{loss_now}%",
    ]
    return (
        "압축데이터|" + "|".join(str(x).replace("\n", " ") for x in fields) + "\n"
        "판단순서: 추세>거래량>세력수급>과열/김프>계좌위험.\n"
        "JSON만 출력: decision BUY/SELL/HOLD, percentage 0-100, reason 1문장, "
        "report 짧은 markdown 5줄 이하."
    )

def run_gemini_trade_decision(query: str = "", ticker: str = "KRW-BTC") -> TradeDecision:
    # 1. 시세 데이터 및 기술분석
    data = get_upbit_data(ticker)
    if "error" in data:
        raise Exception(data["error"])

    current_price = data["현재가"]
    current_trend = data["현재추세"]
    indicators = data["보조지표"]
    volume_profile = data["매물대 상위 3구간"]
    kimchi = data.get("김치프리미엄", {})

    records = data["최근 30일 슈퍼트렌드 분석"]
    atr = _calc_atr(records)

    # POC 분석
    poc_status = "N/A"
    if volume_profile:
        poc_item = volume_profile[0]
        poc_mid = (poc_item["lo"] + poc_item["hi"]) / 2
        if current_price > poc_mid:
            poc_status = f"현재가({current_price:,.0f}원) > POC({poc_mid:,.0f}원) — [매물대 지지 형성]"
        else:
            poc_status = f"현재가({current_price:,.0f}원) < POC({poc_mid:,.0f}원) — [매물대 저항 형성 (매수 신중)]"

    # 2. 잔고 조회 (API 키가 없으면 시뮬레이션 모드)
    upbit_client = get_upbit_client()
    is_simulated = (upbit_client is None)

    if not is_simulated:
        try:
            krw_balance = float(upbit_client.get_balance("KRW"))
            crypto_balance = float(upbit_client.get_balance(ticker))
            avg_buy_price = float(upbit_client.get_avg_buy_price(ticker))
        except Exception as e:
            print(f"[Dave] 잔고 조회 오류로 시뮬레이션 모드 전환: {e}")
            is_simulated = True

    if is_simulated:
        krw_balance = 2214137.0
        crypto_balance = 0.05  # 예시 비트코인 보유량
        avg_buy_price = 95000000.0  # 평단가 예시

    total_asset = krw_balance + (crypto_balance * current_price)
    loss_now = 0.0
    if crypto_balance > 0 and avg_buy_price > 0:
        loss_now = round((current_price - avg_buy_price) / avg_buy_price * 100, 1)

    today_str = datetime.datetime.now().strftime("%Y년 %m월 %d일 (%a)")

    # 매물대 문자열 변환
    vp_rows = "\n".join(
        f"  {v['가격대']} — 누적거래량 {v['누적거래량']:,}"
        for v in volume_profile
    ) if volume_profile else "  (데이터 부족)"

    # 슈퍼트렌드 문자열 변환
    recent_records = records[-5:]
    st_rows = "\n".join(
        f"  {r['Date'].strftime('%Y-%m-%d') if isinstance(r['Date'], datetime.datetime) else r['Date']} | 종가 {r['Close']:,}원 | ST {r['Supertrend']:,}원 | {r['Trend']}"
        for r in recent_records
    )

    prompt = build_compact_trade_prompt(
        ticker=ticker,
        query=query,
        today_str=today_str,
        is_simulated=is_simulated,
        current_price=current_price,
        current_trend=current_trend,
        indicators=indicators,
        poc_status=poc_status,
        atr=atr,
        total_asset=total_asset,
        krw_balance=krw_balance,
        crypto_balance=crypto_balance,
        avg_buy_price=avg_buy_price,
        loss_now=loss_now,
        kimchi=kimchi,
    )
    # 1) Ollama 우선
    try:
        from _shared.ollama_client import chat as lm_chat, is_available as lm_available
        if lm_available():
            print(f"[Dave] Calling Ollama for {ticker}...")
            system = load_system_instruction()

            ollama_result = lm_chat(
                prompt,
                system=system,
                max_tokens=600,
                temperature=0.1,
                json_mode=True,
                task="trading"
            )

            if ollama_result:
                try:
                    return TradeDecision.model_validate_json(ollama_result)
                except Exception as parse_err:
                    print(f"[Dave] Ollama JSON 파싱 실패: {parse_err} → GPT 폴백")
    except Exception as ollama_err:
        print(f"[Dave] Ollama 실패: {ollama_err} → GPT 폴백")

    # 2) GPT-4o mini 폴백
    try:
        from _shared.gemini_client import gpt_mini
        print(f"[Dave] Calling GPT-4o mini for {ticker}...")
        gpt_result = gpt_mini(
            prompt,
            system=load_system_instruction(),
            max_tokens=500,
            temperature=0.1,
            json_mode=True,
        )
        if gpt_result:
            return TradeDecision.model_validate_json(gpt_result)
    except Exception as gpt_err:
        print(f"[Dave] GPT 폴백 실패: {gpt_err}")

    return TradeDecision(
        decision="HOLD",
        percentage=0,
        reason="Ollama/GPT 응답 실패로 안전 관망",
        report="## 데이브 안전 모드\n\nOllama/GPT 응답 실패로 HOLD 처리했습니다.",
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
        send_telegram_message(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n❌ 원인: API 키 미설정")
        return err_msg
    try:
        res = upbit_client.buy_market_order(ticker, krw_amount)
        if res is None:
            err_msg = "❌ [Dave] 매수 주문 실패: 주문 결과가 없습니다 (잔고 부족 또는 API 오류 가능성)."
            send_telegram_message(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: 잔고 부족 또는 API 오류")
            return err_msg
        if isinstance(res, dict):
            if "error" in res:
                err_msg = f"❌ [Dave] 매수 주문 실패: {res['error'].get('message', '알 수 없는 오류')}"
                send_telegram_message(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: {res['error'].get('message')}")
                return err_msg
            if "uuid" in res:
                success_msg = f"✅ [Dave] 시장가 매수 주문 성공!\n주문 결과: {res}"
                send_telegram_message(f"🤖 [데이브] 매수 성공! 🎉\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n🆔 주문ID: {res.get('uuid')}")
                return success_msg
        if isinstance(res, str):
            err_msg = f"❌ [Dave] 매수 주문 실패: {res}"
            send_telegram_message(f"🤖 [데이브] 매수 실패\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원\n❌ 원인: {res}")
            return err_msg
        success_msg = f"✅ [Dave] 시장가 매수 주문 접수 완료\n주문 결과: {res}"
        send_telegram_message(f"🤖 [데이브] 매수 주문 접수\n📌 대상: {ticker}\n💰 금액: {krw_amount:,}원")
        return success_msg
    except Exception as e:
        err_msg = f"❌ [Dave] 매수 주문 중 오류 발생: {e}"
        send_telegram_message(f"🤖 [데이브] 매수 에러 ❌\n📌 대상: {ticker}\n❌ 에러 내용: {e}")
        return err_msg

def execute_sell(ticker: str, volume: float):
    """시장가 매도 주문 실행"""
    upbit_client = get_upbit_client()
    if upbit_client is None:
        err_msg = "❌ [Dave] 업비트 API 키가 올바르게 설정되지 않아 매도를 수행할 수 없습니다 (시뮬레이션 모드에서는 주문이 불가합니다)."
        send_telegram_message(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n❌ 원인: API 키 미설정")
        return err_msg
    try:
        res = upbit_client.sell_market_order(ticker, volume)
        if res is None:
            err_msg = "❌ [Dave] 매도 주문 실패: 주문 결과가 없습니다 (잔고 부족 또는 API 오류 가능성)."
            send_telegram_message(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: 잔고 부족 또는 API 오류")
            return err_msg
        if isinstance(res, dict):
            if "error" in res:
                err_msg = f"❌ [Dave] 매도 주문 실패: {res['error'].get('message', '알 수 없는 오류')}"
                send_telegram_message(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: {res['error'].get('message')}")
                return err_msg
            if "uuid" in res:
                success_msg = f"✅ [Dave] 시장가 매도 주문 성공!\n주문 결과: {res}"
                send_telegram_message(f"🤖 [데이브] 매도 성공! 📉🎉\n📌 대상: {ticker}\n📉 수량: {volume}\n🆔 주문ID: {res.get('uuid')}")
                return success_msg
        if isinstance(res, str):
            err_msg = f"❌ [Dave] 매도 주문 실패: {res}"
            send_telegram_message(f"🤖 [데이브] 매도 실패\n📌 대상: {ticker}\n📉 수량: {volume}\n❌ 원인: {res}")
            return err_msg
        success_msg = f"✅ [Dave] 시장가 매도 주문 접수 완료\n주문 결과: {res}"
        send_telegram_message(f"🤖 [데이브] 매도 주문 접수\n📌 대상: {ticker}\n📉 수량: {volume}")
        return success_msg
    except Exception as e:
        err_msg = f"❌ [Dave] 매도 주문 중 오류 발생: {e}"
        send_telegram_message(f"🤖 [데이브] 매도 에러 ❌\n📌 대상: {ticker}\n❌ 에러 내용: {e}")
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
