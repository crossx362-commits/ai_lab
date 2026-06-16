import requests
from bs4 import BeautifulSoup
import pandas as pd
import numpy as np
import datetime

def get_naver_finance_data(stock_code, pages=3): # 30일치 데이터는 보통 2~3페이지에 걸쳐 있음
    url_base = f"https://finance.naver.com/item/sise_day.naver?code={stock_code}"
    df_list = []

    for page in range(1, pages + 1):
        url = f"{url_base}&page={page}"
        response = requests.get(url, headers={'User-Agent': 'Mozilla/5.0'})
        soup = BeautifulSoup(response.text, 'html.parser')
        table = soup.find('table', class_='type2')
        if not table:
            break
        
        rows = table.find_all('tr')
        for row in rows:
            cols = row.find_all('td')
            if len(cols) > 1: # 유효한 데이터 행만
                try:
                    date = cols[0].get_text(strip=True)
                    # '날짜' 열의 'YYYY.MM.DD' 형식만 처리
                    if not date or '.' not in date:
                        continue
                    
                    close = int(cols[1].get_text(strip=True).replace(',', ''))
                    open_price = int(cols[3].get_text(strip=True).replace(',', ''))
                    high = int(cols[4].get_text(strip=True).replace(',', ''))
                    low = int(cols[5].get_text(strip=True).replace(',', ''))
                    volume = int(cols[6].get_text(strip=True).replace(',', ''))
                    df_list.append([date, open_price, high, low, close, volume])
                except ValueError:
                    continue # 숫자 변환 오류 시 스킵

    df = pd.DataFrame(df_list, columns=['Date', 'Open', 'High', 'Low', 'Close', 'Volume'])
    df['Date'] = pd.to_datetime(df['Date'])
    df = df.sort_values(by='Date').reset_index(drop=True)
    return df

def calculate_supertrend(df, period=10, multiplier=3):
    # ATR 계산
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())
    tr = pd.concat([high_low, high_close, low_close], axis=1).max(axis=1)
    atr = tr.ewm(com=period-1, adjust=False, min_periods=period).mean()

    # Basic Upper/Lower Band 계산
    df['Basic_Upper_Band'] = ((df['High'] + df['Low']) / 2) + (multiplier * atr)
    df['Basic_Lower_Band'] = ((df['High'] + df['Low']) / 2) - (multiplier * atr)

    # Final Upper/Lower Band 계산
    df['Final_Upper_Band'] = np.nan
    df['Final_Lower_Band'] = np.nan

    for i in range(len(df)):
        if i < period: # ATR 계산을 위한 초기 기간
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

    # Supertrend 계산
    df['Supertrend'] = np.nan
    df['Trend'] = np.nan # 1 for uptrend, -1 for downtrend

    for i in range(len(df)):
        if i < period:
            continue

        if df.loc[i-1, 'Trend'] == -1: # 이전이 하락 추세
            if df.loc[i, 'Close'] <= df.loc[i, 'Final_Upper_Band']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Upper_Band']
                df.loc[i, 'Trend'] = -1
            else: # 하락 추세에서 상승 전환
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Lower_Band']
                df.loc[i, 'Trend'] = 1
        elif df.loc[i-1, 'Trend'] == 1: # 이전이 상승 추세
            if df.loc[i, 'Close'] >= df.loc[i, 'Final_Lower_Band']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Lower_Band']
                df.loc[i, 'Trend'] = 1
            else: # 상승 추세에서 하락 전환
                df.loc[i, 'Supertrend'] = df.loc[i, 'Final_Upper_Band']
                df.loc[i, 'Trend'] = -1
        else: # 초기값 설정 (첫 번째 유효한 데이터 포인트)
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
    """가격대별 거래량 집중 구간(매물대) 분석.

    Returns:
        list of dict — 거래량 상위 3개 가격 구간 (지지/저항 후보)
    """
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
    return [{"가격대": b["가격대"], "누적거래량": b["누적거래량"]} for b in buckets[:3]]


def get_investor_data(stock_code) -> dict:
    """네이버 금융 외국인/기관 순매매 및 외국인 보유율 스크래핑."""
    url = f"https://finance.naver.com/item/frgn.naver?code={stock_code}"
    headers = {"User-Agent": "Mozilla/5.0"}
    rows_data = []
    try:
        r = requests.get(url, headers=headers, timeout=10)
        soup = BeautifulSoup(r.text, "html.parser")
        tables = soup.find_all("table")
        if len(tables) < 3:
            return {}
        table = tables[2]
        for row in table.find_all("tr"):
            cols = [c.get_text(strip=True) for c in row.find_all("td")]
            if len(cols) >= 9 and "." in cols[0]:
                try:
                    rows_data.append({
                        "date":       cols[0],
                        "close":      int(cols[1].replace(",", "")),
                        "inst_net":   int(cols[5].replace(",", "").replace("+", "") or 0),
                        "frgn_net":   int(cols[6].replace(",", "").replace("+", "") or 0),
                        "frgn_ratio": float(cols[8].replace("%", "") or 0),
                    })
                except ValueError:
                    continue
    except Exception:
        return {}

    if not rows_data:
        return {}

    latest = rows_data[0]
    frgn_5d = [r["frgn_net"] for r in rows_data[:5]]
    inst_5d = [r["inst_net"] for r in rows_data[:5]]
    return {
        "외국인보유율":     f"{latest['frgn_ratio']}%",
        "외국인5일순매매": sum(frgn_5d),
        "기관5일순매매":   sum(inst_5d),
        "외국인추세":       "매수" if sum(frgn_5d) > 0 else "매도",
        "기관추세":         "매수" if sum(inst_5d) > 0 else "매도",
    }


def calc_indicators(df: pd.DataFrame) -> dict:
    """RSI(14), MACD(12/26/9), 볼린저밴드(20,2), OBV, 거래량 회전율 계산."""
    close = df["Close"]
    volume = df["Volume"] if "Volume" in df.columns else pd.Series([0]*len(df))
    result = {}

    # RSI
    delta = close.diff()
    gain = delta.clip(lower=0).ewm(com=13, adjust=False).mean()
    loss = (-delta.clip(upper=0)).ewm(com=13, adjust=False).mean()
    rs = gain / loss.replace(0, np.nan)
    result["RSI14"] = round(float(rs.iloc[-1] / (1 + rs.iloc[-1]) * 100) if not np.isnan(rs.iloc[-1]) else 50, 1)

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

    # 거래량 회전율 (오늘 거래량 / 20일 평균 거래량)
    avg_vol = volume.rolling(20).mean().iloc[-1]
    today_vol = volume.iloc[-1]
    result["거래량회전율"] = f"{round(today_vol / avg_vol * 100, 0):.0f}%" if avg_vol > 0 else "N/A"
    result["거래량평균대비"] = "급증(매집가능)" if today_vol > avg_vol * 5 else (
        "증가" if today_vol > avg_vol * 1.5 else "보통")

    # 거래량 방향성 (상승일 vs 하락일 평균 거래량)
    up_vol   = volume[close.diff() > 0].mean()
    down_vol = volume[close.diff() < 0].mean()
    result["거래량방향성"] = "상승 시 거래량 우위(긍정)" if up_vol > down_vol else "하락 시 거래량 우위(경고)"

    return result


def analyze_stock_realtime(stock_code):
    try:
        df = get_naver_finance_data(stock_code)
        if df.empty:
            return {"error": "네이버 금융에서 데이터를 가져올 수 없습니다."}

        df_recent = df.tail(min(len(df), 30)).copy()

        if len(df_recent) < 10:
            return {"error": f"최소 10일치 데이터 필요. 현재 {len(df_recent)}일치만 있습니다."}

        df_supertrend = calculate_supertrend(df_recent)

        if df_supertrend.empty:
            return {"error": "슈퍼트렌드 계산 실패."}

        current_price = df_supertrend['Close'].iloc[-1]
        current_supertrend = df_supertrend['Supertrend'].iloc[-1]
        current_trend = df_supertrend['Trend'].iloc[-1]

        # 보조지표 + 거래량 분석
        indicators = calc_indicators(df_recent)

        # 매물대 분석
        volume_profile = analyze_volume_profile(df_recent)

        # 외국인/기관 지분율
        investor = get_investor_data(stock_code)

        report = {
            "종목 코드": stock_code,
            "현재 주가": f"{current_price}원",
            "최근 30일 슈퍼트렌드 분석": df_supertrend.to_dict(orient='records'),
            "현재 추세": current_trend,
            "보조지표": indicators,
            "매물대 상위 3구간": volume_profile,
            "외국인기관동향": investor,
        }
        return report

    except Exception as e:
        return {"error": f"분석 중 오류: {e}"}
