import requests
from bs4 import BeautifulSoup
import pandas as pd
import numpy as np

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
                    # diff = int(cols[2].get_text(strip=True).replace(',', '')) # 사용하지 않으므로 주석 처리
                    open_price = int(cols[3].get_text(strip=True).replace(',', ''))
                    high = int(cols[4].get_text(strip=True).replace(',', ''))
                    low = int(cols[5].get_text(strip=True).replace(',', ''))
                    # volume = int(cols[6].get_text(strip=True).replace(',', '')) # 사용하지 않으므로 주석 처리
                    df_list.append([date, open_price, high, low, close])
                except ValueError:
                    continue # 숫자 변환 오류 시 스킵

    df = pd.DataFrame(df_list, columns=['Date', 'Open', 'High', 'Low', 'Close'])
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
    return df[['Date', 'High', 'Low', 'Close', 'Supertrend', 'Trend']].dropna() # 초기 nan 값 제거

def analyze_stock_realtime(stock_code):
    try:
        df = get_naver_finance_data(stock_code)
        if df.empty:
            return {"error": "네이버 금융에서 데이터를 가져올 수 없습니다. 종목 코드를 확인하거나, 서비스에 문제가 있을 수 있습니다."}

        # 최근 30일 데이터만 사용 (가져온 데이터가 30일보다 적을 수도 있으므로 min 사용)
        df_recent = df.tail(min(len(df), 30)).copy()
        
        if len(df_recent) < 10: # 슈퍼트렌드 계산을 위한 최소 데이터 (period=10 가정)
            return {"error": f"슈퍼트렌드 분석을 위한 최소 10일치 데이터가 필요합니다. 현재 {len(df_recent)}일치 데이터만 있습니다."}

        df_supertrend = calculate_supertrend(df_recent)

        if df_supertrend.empty:
            return {"error": "슈퍼트렌드 계산에 필요한 충분한 데이터가 없거나 계산 중 오류가 발생했습니다."}

        current_price = df_supertrend['Close'].iloc[-1]
        current_supertrend = df_supertrend['Supertrend'].iloc[-1]
        current_trend = df_supertrend['Trend'].iloc[-1]

        report = {
            "종목 코드": stock_code,
            "현재 주가": f"{current_price}원",
            "최근 30일 슈퍼트렌드 분석": df_supertrend.to_dict(orient='records'),
            "현재 추세": current_trend,
            "데이브의 물타기 및 매도 타점": {
                "물타기(추가 매수) 타점": "13,500원 ~ 14,000원 선 돌파 시점",
                "탈출 매도(목표가) 타점": "16,500원 ~ 17,000원 구간"
            },
            "종합 의견": f"네이버 금융 데이터를 기반으로 분석한 결과, '우리기술'은 현재 {current_trend} 추세에 있습니다. 슈퍼트렌드 지표는 {current_supertrend}원입니다. 데이브의 물타기 기준을 고려하여 신중한 투자 판단이 필요합니다."
        }
        return report

    except Exception as e:
        return {"error": f"주식 데이터 분석 중 오류가 발생했습니다: {e}"}
