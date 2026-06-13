import pandas as pd
import numpy as np
from datetime import datetime
from _shared.naver_finance_scraper import get_stock_data_naver

def calculate_supertrend(df, period=10, multiplier=3):
    df['TR'] = 0.0
    df['ATR'] = 0.0
    df['UpperBand'] = 0.0
    df['LowerBand'] = 0.0
    df['Supertrend'] = 0.0
    df['Trend'] = 0 # 1 for uptrend, -1 for downtrend

    # Calculate True Range (TR)
    for i in range(1, len(df)):
        df.loc[i, 'TR'] = max(df.loc[i, 'High'] - df.loc[i, 'Low'],
                              abs(df.loc[i, 'High'] - df.loc[i-1, 'Close']),
                              abs(df.loc[i, 'Low'] - df.loc[i-1, 'Close']))

    # Calculate Average True Range (ATR)
    df['ATR'] = df['TR'].rolling(period).mean()

    # Calculate Basic Upper and Lower Bands
    df['BasicUpperBand'] = ((df['High'] + df['Low']) / 2) + (multiplier * df['ATR'])
    df['BasicLowerBand'] = ((df['High'] + df['Low']) / 2) - (multiplier * df['ATR'])

    # Calculate Final Upper and Lower Bands
    for i in range(1, len(df)):
        if df.loc[i, 'BasicUpperBand'] < df.loc[i-1, 'UpperBand'] or df.loc[i-1, 'Close'] > df.loc[i-1, 'UpperBand']:
            df.loc[i, 'UpperBand'] = df.loc[i, 'BasicUpperBand']
        else:
            df.loc[i, 'UpperBand'] = df.loc[i-1, 'UpperBand']

        if df.loc[i, 'BasicLowerBand'] > df.loc[i-1, 'LowerBand'] or df.loc[i-1, 'Close'] < df.loc[i-1, 'LowerBand']:
            df.loc[i, 'LowerBand'] = df.loc[i, 'BasicLowerBand']
        else:
            df.loc[i, 'LowerBand'] = df.loc[i-1, 'LowerBand']

    # Calculate Supertrend
    for i in range(1, len(df)):
        if df.loc[i-1, 'Trend'] == 1: # Previous was uptrend
            if df.loc[i, 'Close'] > df.loc[i, 'LowerBand']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'LowerBand']
                df.loc[i, 'Trend'] = 1
            else:
                df.loc[i, 'Supertrend'] = df.loc[i, 'UpperBand']
                df.loc[i, 'Trend'] = -1
        else: # Previous was downtrend
            if df.loc[i, 'Close'] < df.loc[i, 'UpperBand']:
                df.loc[i, 'Supertrend'] = df.loc[i, 'UpperBand']
                df.loc[i, 'Trend'] = -1
            else:
                df.loc[i, 'Supertrend'] = df.loc[i, 'LowerBand']
                df.loc[i, 'Trend'] = 1
                
    return df

def analyze_stock_realtime(stock_code):
    df = get_stock_data_naver(stock_code, days=30)
    
    if df.empty:
        return "데이터를 가져오지 못했습니다. 종목 코드 또는 네트워크 상태를 확인해주세요."

    df = calculate_supertrend(df.copy())

    latest_data = df.iloc[-1]
    current_price = latest_data['Close']
    supertrend_value = latest_data['Supertrend']
    trend = "상승" if latest_data['Trend'] == 1 else "하락"

    report = f"**[데이브 에이전트의 '{stock_code}' 슈퍼트렌드 분석 보고서 (네이버 금융 실데이터 기반)]**\n\n"
    report += f"*   **종목 코드:** {stock_code}\n"
    report += f"*   **현재 주가:** {current_price}원\n"
    report += f"*   **현재 추세:** {trend} 추세 ({supertrend_value:.2f}원)\n\n"
    report += "**슈퍼트렌드 지표 분석 (지난 30일):**\n"
    report += "| 날짜 | 고가 | 저가 | 종가 | 슈퍼트렌드 | 추세 |\n"
    report += "|---|---|---|---|---|---|\n"

    for index, row in df.tail(30).iterrows():
        report += (f"| {row['Date'].strftime('%Y-%m-%d')} | {int(row['High'])} | {int(row['Low'])} | {int(row['Close'])} | "
                   f"{row['Supertrend']:.0f} | {"상승" if row['Trend'] == 1 else "하락"} |\n")

    report += "\n**데이브의 분석:**\n"
    report += f"네이버 금융에서 가져온 지난 30일간의 데이터에 따르면, '{stock_code}'는 현재 **{trend} 추세**를 보이고 있습니다. 슈퍼트렌드 지표는 {supertrend_value:.0f}원에 위치하고 있으며, 이는 현재 주가 {current_price}원과 비교하여 추세 전환 여부를 판단하는 중요한 지표가 됩니다.\n\n"
    
    # 물타기 및 매도 타점 (기존 규칙 반영)
    buy_target_low = 13500
    buy_target_high = 14000
    sell_target_low = 16500
    sell_target_high = 17000

    report += "**데이브의 물타기 및 매도 타점 (기존 규칙 반영):**\n"
    report += f"*   **물타기(추가 매수) 타점:** {buy_target_low}원 ~ {buy_target_high}원 선 돌파 시점\n"
    report += f"*   **탈출 매도(목표가) 타점:** {sell_target_low}원 ~ {sell_target_high}원 구간\n\n"

    report += "**종합 의견:**\n"
    if latest_data['Trend'] == 1:
        report += f"현재 '{stock_code}'는 상승 추세에 있으며, 슈퍼트렌드 지표({supertrend_value:.0f}원)가 이를 지지하고 있습니다. 데이브의 물타기 타점 기준({buy_target_low}원 ~ {buy_target_high}원)과 비교하여 예원아빠의 투자 전략에 따라 추가 매수를 고려해 볼 수 있습니다. 다만, 실제 투자 시에는 항상 신중한 판단이 필요합니다.\n"
    else:
        report += f"현재 '{stock_code}'는 하락 추세에 있으며, 슈퍼트렌드 지표({supertrend_value:.0f}원)가 이를 나타내고 있습니다. 데이브의 물타기 타점 기준({buy_target_low}원 ~ {buy_target_high}원)을 고려할 때, 현재는 관망하거나 추가적인 하락 여부를 지켜보는 것이 좋습니다. 실제 투자 시에는 항상 신중한 판단이 필요합니다.\n"

    return report

if __name__ == '__main__':
    # 예시 사용
    stock_code = "032820" # 우리기술 종목 코드
    report = analyze_stock_realtime(stock_code)
    print(report)
