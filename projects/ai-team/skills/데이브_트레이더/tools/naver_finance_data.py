import pandas as pd
import numpy as np

def fetch_historical_data_from_naver_finance(stock_code, days=30):
    """
    네이버 금융에서 과거 주가 데이터를 가져오는 (개념적인) 함수.
    현재는 외부 라이브러리 사용 제한으로 인해 시뮬레이션 데이터를 반환합니다.
    향후 실제 데이터를 가져오도록 확장될 수 있습니다.

    Args:
        stock_code (str): 종목 코드 (예: "032820.KQ")
        days (int): 가져올 과거 일수

    Returns:
        pd.DataFrame: 'High', 'Low', 'Close' 컬럼을 포함하는 과거 주가 데이터.
                      인덱스는 날짜 (DatetimeIndex).
    """
    print(f"⚠️ 경고: 현재 외부 라이브러리 사용 제한으로 인해 네이버 금융에서 실제 데이터를 가져올 수 없습니다.")
    print(f"       '{stock_code}' 종목의 지난 {days}일간 시뮬레이션 데이터를 반환합니다.")

    # 시뮬레이션 데이터 생성
    np.random.seed(42) # 재현성을 위해 시드 고정
    start_date = pd.to_datetime('today') - pd.Timedelta(days=days)
    dates = pd.date_range(start=start_date, periods=days, freq='B') # 영업일 기준

    # 가상의 주가 흐름 생성 (예시: 10000원대 주가)
    base_price = 13000
    price_fluctuations = np.cumsum(np.random.normal(0, 100, days))
    close_prices = base_price + price_fluctuations
    
    # 고가, 저가 생성
    high_prices = close_prices + np.random.uniform(50, 200, days)
    low_prices = close_prices - np.random.uniform(50, 200, days)

    # DataFrame 생성
    historical_data = pd.DataFrame({
        'High': high_prices,
        'Low': low_prices,
        'Close': close_prices
    }, index=dates)
    
    # 음수 가격 방지
    historical_data = historical_data.apply(lambda x: x.clip(lower=100), axis=1)

    return historical_data

def calculate_supertrend(df, period=10, multiplier=3):
    """
    주어진 DataFrame에 슈퍼트렌드 지표를 계산하여 추가합니다.
    'High', 'Low', 'Close' 컬럼이 필요합니다.
    """
    # ATR (Average True Range) 계산
    high_low = df['High'] - df['Low']
    high_close = np.abs(df['High'] - df['Close'].shift())
    low_close = np.abs(df['Low'] - df['Close'].shift())
    tr = pd.DataFrame({'high_low': high_low, 'high_close': high_close, 'low_close': low_close}).max(axis=1)
    atr = tr.ewm(com=period - 1, adjust=False).mean()

    # Basic Upper/Lower Band 계산
    df['Basic Upper Band'] = ((df['High'] + df['Low']) / 2) + (multiplier * atr)
    df['Basic Lower Band'] = ((df['High'] + df['Low']) / 2) - (multiplier * atr)

    # Final Upper/Lower Band 계산
    df['Final Upper Band'] = 0.0
    df['Final Lower Band'] = 0.0

    for i in range(1, len(df)):
        if df['Basic Upper Band'].iloc[i] < df['Final Upper Band'].iloc[i-1] or df['Close'].iloc[i-1] > df['Final Upper Band'].iloc[i-1]:
            df['Final Upper Band'].iloc[i] = df['Basic Upper Band'].iloc[i]
        else:
            df['Final Upper Band'].iloc[i] = df['Final Upper Band'].iloc[i-1]

        if df['Basic Lower Band'].iloc[i] > df['Final Lower Band'].iloc[i-1] or df['Close'].iloc[i-1] < df['Final Lower Band'].iloc[i-1]:
            df['Final Lower Band'].iloc[i] = df['Basic Lower Band'].iloc[i]
        else:
            df['Final Lower Band'].iloc[i] = df['Final Lower Band'].iloc[i-1]

    # Supertrend 계산
    df['Supertrend'] = 0.0
    df['Trend'] = 0 # 1 for uptrend, -1 for downtrend

    for i in range(len(df)):
        if i == 0:
            df['Supertrend'].iloc[i] = df['Final Upper Band'].iloc[i] # 초기값은 상단 밴드로 설정
            df['Trend'].iloc[i] = 1 # 초기값은 상승 추세로 설정
            continue

        if df['Supertrend'].iloc[i-1] == df['Final Upper Band'].iloc[i-1]: # 이전이 상승 추세였다면
            if df['Close'].iloc[i] <= df['Final Upper Band'].iloc[i]: # 종가가 상단 밴드 아래면
                df['Supertrend'].iloc[i] = df['Final Upper Band'].iloc[i]
                df['Trend'].iloc[i] = 1
            else: # 종가가 상단 밴드 위면 추세 전환
                df['Supertrend'].iloc[i] = df['Final Lower Band'].iloc[i]
                df['Trend'].iloc[i] = -1
        elif df['Supertrend'].iloc[i-1] == df['Final Lower Band'].iloc[i-1]: # 이전이 하락 추세였다면
            if df['Close'].iloc[i] >= df['Final Lower Band'].iloc[i]: # 종가가 하단 밴드 위면
                df['Supertrend'].iloc[i] = df['Final Lower Band'].iloc[i]
                df['Trend'].iloc[i] = -1
            else: # 종가가 하단 밴드 아래면 추세 전환
                df['Supertrend'].iloc[i] = df['Final Upper Band'].iloc[i]
                df['Trend'].iloc[i] = 1
        else: # 초기값 처리 (이전 로직에서 이미 처리됨)
            df['Supertrend'].iloc[i] = df['Supertrend'].iloc[i-1]
            df['Trend'].iloc[i] = df['Trend'].iloc[i-1]

    # Supertrend 지표가 종가 위에 있으면 하락 추세, 아래에 있으면 상승 추세
    # 이 부분은 Supertrend 지표의 계산 로직에 따라 다르게 해석될 수 있음
    # 일반적으로 Supertrend 값이 종가보다 아래에 있으면 상승 추세, 위에 있으면 하락 추세
    # 여기서는 Trend 컬럼을 사용하여 명확히 구분
    df['Supertrend_Indicator'] = np.where(df['Trend'] == 1, df['Final Lower Band'], df['Final Upper Band'])
    df['Supertrend_Trend'] = np.where(df['Trend'] == 1, '상승', '하락')

    return df
