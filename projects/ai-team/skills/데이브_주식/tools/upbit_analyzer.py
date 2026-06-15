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
from google import genai
from google.genai import types
from pydantic import BaseModel, Field

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

load_env()

import pyupbit

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

_dave_cache_name = None

def load_system_instruction():
    skill_md_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "SKILL.md")
    indicators_md_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "indicators_knowledge.md")
    
    system_instruction = "너는 글로벌 매크로, 온체인, 호가창 세력, 퀀트 지표를 통합 분석하는 지구 최강의 AI 수석 매크로 퀀트 트레이더 데이브(Dave)이다.\n\n"
    if os.path.exists(skill_md_path):
        with open(skill_md_path, "r", encoding="utf-8") as f:
            system_instruction += f.read() + "\n"
    if os.path.exists(indicators_md_path):
        with open(indicators_md_path, "r", encoding="utf-8") as f:
            system_instruction += f.read() + "\n"
    return system_instruction

def get_dave_context_cache(client):
    global _dave_cache_name
    if _dave_cache_name:
        return _dave_cache_name
        
    try:
        for c in client.caches.list():
            if c.display_name == "dave_coin_rules":
                _dave_cache_name = c.name
                return _dave_cache_name
    except Exception as e:
        print(f"[Dave Cache] Error checking cache list: {e}")
        
    system_instruction = load_system_instruction()
            
    try:
        count_resp = client.models.count_tokens(
            model="gemini-2.5-flash",
            contents=system_instruction
        )
        total_tokens = count_resp.total_tokens
        print(f"[Dave Cache] Base system instruction tokens: {total_tokens}")
        
        # Padding to meet the 32768 token limit for context caching
        while total_tokens < 32768:
            system_instruction += "\n# Cache Padding Reference Data\n" + "This is reference educational padding text for Context Caching. " * 1000
            count_resp = client.models.count_tokens(
                model="gemini-2.5-flash",
                contents=system_instruction
            )
            total_tokens = count_resp.total_tokens
        
        print(f"[Dave Cache] Padded system instruction tokens: {total_tokens}")
        
        cache = client.caches.create(
            model="gemini-2.5-flash",
            config=types.CreateCachedContentConfig(
                display_name="dave_coin_rules",
                contents=[types.Content(role="user", parts=[types.Part.from_text(text=system_instruction)])],
                ttl="3600s"
            )
        )
        _dave_cache_name = cache.name
        print(f"[Dave Cache] Created context cache successfully: {_dave_cache_name}")
        return _dave_cache_name
    except Exception as e:
        print(f"[Dave Cache] Failed to configure or create context cache: {e}")
        return None

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

    prompt = f"""당신은 글로벌 자산 시장의 거시 경제(Macro) 흐름, 고래의 온체인(On-chain) 데이터, 그리고 국내외 최정상 퀀트 매매법(나씨TV, 홍익희 교수, 닥터페퍼3, 실전 단타 기법)을 단 하나의 알고리즘으로 완벽하게 통합한 '지구 최강의 AI 수석 매크로 퀀트 트레이더' 데이브(Dave)입니다.
오늘 날짜는 **{today_str}**입니다. 모든 분석은 이 날짜를 기준으로 동기화합니다.
현재 모드: {'[시뮬레이션 모드] (API 키 미설정)' if is_simulated else '[실거래 모드] (API 키 연동 완료)'}

[사용자 요청/질문]: {query}

[업비트 실시간 데이터 및 기술분석 연산 결과]:
- 현재가: {current_price:,}원
- 대추세 (EMA 200 기준): {indicators['대추세_EMA200']} (현재가: {current_price:,}원 vs EMA200: {indicators['EMA200']:,}원)
- 보조지표 및 캔들 마감 상태:
  - 현재추세 (Supertrend): {current_trend} (최근 5일간 변동성 필터 추종)
  - 매물대 POC (Point of Control) 상태: {poc_status}
  - 스토캐스틱 RSI: K {indicators['StochRSI_K']} / D {indicators['StochRSI_D']} → {indicators['StochRSI_상태']}
  - 하이킨 애쉬 캔들: {indicators['HeikinAshi_상태']} (현재봉: {indicators['HeikinAshi_양봉여부']}, 아래꼬리: {indicators['HeikinAshi_아래꼬리여부']}, 몸통길이: {indicators['HeikinAshi_몸통길어짐여부']})
  - 실시간 거래량 급증 (Volume Spike): {indicators['VolumeSpike']} (오늘 거래량 대비 5일 평균 배율: {indicators['Volume_배율']}배, 5일평균: {indicators['Volume_Avg5']:,})
  - 24시간 거래대금: {indicators['거래대금_24h']:,}원 (유동성 상태: {indicators['유동성_상태']})
  - RSI(14): {indicators['RSI14']}
  - MACD: {indicators['MACD']:,} / Signal: {indicators['MACD_Signal']:,} → {indicators['MACD_상태']}
  - 볼린저밴드: 상단 {indicators['BB_상단']:,}원 / 중단 {indicators['BB_중단']:,}원 / 하단 {indicators['BB_하단']:,}원 → {indicators['BB_위치']}
  - OBV 추세: {indicators['OBV_추세']} (5일 변화: {indicators['OBV_5일변화']:,})
  - ATR (10일 변동성 폭): {atr:,.0f}원 (이탈 범위 방어 기준)
- 매물대 상위 3구간 (최근 30일):
{vp_rows}
- 최근 5일 슈퍼트렌드:
{st_rows}

[실시간 계좌 정보]:
- 총 평가자산: {total_asset:,.0f}원
- 보유 예수금(KRW): {krw_balance:,.0f}원
- 보유 수량: {crypto_balance} {ticker.split('-')[1]}
- 평균 매수 단가: {avg_buy_price:,.0f}원
- 현재 손실률: {loss_now}%

[세력 탐지 지표 (신규 주입)]:
- 김치 프리미엄: {kimchi.get('김치프리미엄', 'N/A')}% → {kimchi.get('상태', 'N/A')}
- OBV 다이버전스: {indicators.get('OBV_다이버전스', 'N/A')}
- CVD 추세: {indicators.get('CVD_추세', 'N/A')} | CVD 다이버전스: {indicators.get('CVD_다이버전스', 'N/A')}
- 세력 매집 패턴: {indicators.get('세력매집패턴', 'N/A')}
- 워시트레이딩 의심 여부: {indicators.get('통정매매의심', 'N/A')}

[매크로 & 온체인 추가 분석 재료 (자가 탐색 시 반영)]:
1. 미국 친크립토 규제 완화 및 정책 환경 우호성 여부 분석
2. 현물 ETF 자금 유입 강도 추이
3. 고래 대형 지갑 이동 및 청산 맵(Liquidation Map)의 주요 매물대 벽
4. 김프 폭등 여부, 인간 지표(무지성 수익 인증) 과열 여부 분석

[⚠️ 필수 거래 규칙 및 6단계 Confluence 조건]:
1. Step 1 (대추세 생명선): 현재가 > EMA 200 (상승 국면)이어야만 롱(Long) 가능.
2. Step 2 (중기 추세 방향): Supertrend = '상승' 추세일 때만 롱 가능.
3. Step 3 (단기 모멘텀): 스토캐스틱 RSI 과매도 구간 골든크로스(K, D < 20) 혹은 MACD 골든크로스 발생 확인.
4. Step 4 (가격 스무딩): 하이킨 애쉬 캔들이 아래꼬리 없고 몸통이 이전보다 길어지는 양봉 마감.
5. Step 5 (세력 수급/거래량): Volume Spike >= 2.0배 이상 및 OBV 추세 '상승'.
6. Step 6 (유동성/리스크/POC): 24시간 거래대금 >= 10억 원, ATR 대비 적정 손익비 및 매물대 POC 돌파/지지 여부 검증 (가격이 POC선 위에 있을 때 롱 신뢰도 극대화).

* 위 6단계 조건 중 단 하나라도 만족하지 못하면 최종 결정은 무조건 관망(HOLD)으로 출력하시오.

[출력 양식 — 무조건 이 포맷으로만 답변하세요. 줄글을 절대 배제하고 대시보드 형태로 출력해야 합니다]:

## 🌐 AI 매크로 퀀트 에이전트 최종 마스터 리포트

### 1. 🏛️ 매크로 & 온체인 세력 동향 (Macro & On-chain)
- **정책 환경 및 ETF 수급:** (미국 규제 흐름 및 기관 ETF 자금 유입/유출 상태 요약)
- **고래 물량 및 청산 매물대:** [세력 매집 중 / 덤핑 위험 / 매물대 저항 중 택 1] (온체인 및 청산 벽 분석 근거 기술)

### 2. 📊 기술적 대추세 및 광기 지수 (Trend & Sentiment)
- **대추세 (EMA 200 기준):** [상승 국면(LONG 전용) / 하락 국면(SHORT 전용) 중 택 1] (현재가: {current_price:,}원 vs EMA 200: {indicators['EMA200']:,}원)
- **중기 추세 (Supertrend 기준):** [상승 추세 유지 / 하락 추세 지속 중 택 1] (현재 추세: {current_trend})
- **매물대 POC 지지/저항:** [매물대 지지 형성 / 매물대 저항 형성 중 택 1] ({poc_status})
- **광기 지수 (인간 지표/코프):** [안정 / 과열 경고 / 폭락 징후 중 택 1]

### 3. ⚡ 퀀트 타점 매트릭스 (Trading Matrix)
- **보조지표 & 캔들 마감:** (스토캐스틱 RSI K {indicators['StochRSI_K']}/D {indicators['StochRSI_D']} ({indicators['StochRSI_상태']}), MACD {indicators['MACD']:,} ({indicators['MACD_상태']}), 하이킨 애쉬 {indicators['HeikinAshi_상태']}, Volume Spike {indicators['VolumeSpike']} (배율: {indicators['Volume_배율']}배), OBV {indicators['OBV_추세']}, 볼린저밴드 위치: {indicators['BB_위치']}, 매물대 POC 상태를 종합하여 6단계 진입 합치 여부 구체적으로 판정)
- **기계적 손익비 점수:** [최상(2:1 만족) / 불량(진입 금지) 중 택 1]

### 4. 🚨 최종 트레이딩 오더 (Final Order)
- **최종 결정 (Decision):** [전략적 매수(Long) / 헷징·매도(Short) / 무조건 관망(HOLD) 중 택 1]
- **확정 익절가 (Take-Profit):** {current_price * 1.025:,.0f} ~ {current_price * 1.03:,.0f}원 (진입가 대비 +2.5~3% 계산값 또는 볼린저밴드 상단 감안 목표값 제시)
- **확정 손절가 (Stop-Loss):** {current_price - 2 * atr:,.0f}원 또는 {current_price * 0.985:,.0f}원 (ATR 기반 손절가 {current_price - 2 * atr:,.0f}원 혹은 진입가 대비 -1.5% 수준 계산값 중 택 1, 칼손절 라인)
- **운용 가이드:** (안전기금 분리 비중 및 5배 이하 레버리지 가이드)

### 💡 수석 퀀트의 원칙 한 줄
- (거시 흐름, 세력 무빙, 기술적 원칙을 관통하는 냉정한 명언 한 줄 제공)
"""
    api_key = os.getenv("GEMINI_API_KEY", "").strip('"').strip("'")
    if not api_key:
        raise Exception("GEMINI_API_KEY 환경변수가 설정되지 않았습니다.")

    client = genai.Client(api_key=api_key)
    
    config = types.GenerateContentConfig(
        system_instruction=load_system_instruction(),
        response_mime_type="application/json",
        response_schema=TradeDecision,
        temperature=0.2
    )
        
    print(f"[Dave] Calling Gemini 2.5 Flash for {ticker}...")
    response = client.models.generate_content(
        model="gemini-2.5-flash",
        contents=prompt,
        config=config
    )
    
    return TradeDecision.model_validate_json(response.text)

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

        # Notion에 즉시 자동 동기화 업로드
        try:
            from _shared.notion_report_manager import NotionReportManager
            manager = NotionReportManager()
            today_str = datetime.datetime.now().strftime("%Y-%m-%d")
            manager.create_report_entry(
                agent_name="데이브",
                task_title=f"업비트 {ticker} {today_str} 분석 브리핑",
                result=report
            )
            print("[Dave] Upbit Notion report sync completed successfully.")
        except Exception as notion_err:
            print(f"[Dave] Notion sync warning: {notion_err}")
            
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
    # 4. 전량 매도: python upbit_analyzer.py --sell-all [KRW-BTC]
    args = sys.argv[1:]
    if args:
        if args[0] == "--buy" and len(args) >= 3:
            ticker = args[1]
            amount = float(args[2])
            result = execute_buy(ticker, amount)
            print(result)
        elif args[0] == "--sell" and len(args) >= 3:
            ticker = args[1]
            volume = float(args[2])
            result = execute_sell(ticker, volume)
            print(result)
        elif args[0] == "--sell-all" and len(args) >= 2:
            ticker = args[1]
            result = execute_sell_all(ticker)
            print(result)
        elif args[0].startswith("KRW-"):
            ticker = args[0]
            query_text = " ".join(args[1:]) or "금일 가상자산 시장 분석 및 리스크 진단"
            result = run_analysis(query_text, ticker=ticker)
            print(result)
        else:
            ticker = "KRW-BTC"
            query_text = " ".join(args) or "비트코인 금일 가상자산 분석 및 리스크 진단"
            result = run_analysis(query_text, ticker=ticker)
            print(result)
    else:
        ticker = "KRW-BTC"
        query_text = "비트코인 금일 가상자산 분석 및 리스크 진단"
        result = run_analysis(query_text, ticker=ticker)
        print(result)

