import requests
from bs4 import BeautifulSoup
import pandas as pd
from datetime import datetime, timedelta

def get_naver_finance_historical_data(stock_code, days=30):
    """
    네이버 금융에서 특정 종목의 지난 N일간의 일별 고가, 저가, 종가 데이터를 가져옵니다.
    :param stock_code: 종목 코드 (예: '032820')
    :param days: 가져올 일수
    :return: Pandas DataFrame (컬럼: Date, High, Low, Close)
    """
    url = f"https://finance.naver.com/item/sise_day.naver?code={stock_code}"
    
    # 데이터를 저장할 리스트
    data = []
    
    # 페이지를 역순으로 탐색 (최신 데이터부터 가져오기 위해)
    page = 1
    while len(data) < days:
        page_url = f"{url}&page={page}"
        response = requests.get(page_url, headers={'User-Agent': 'Mozilla/5.0'})
        html = response.text
        soup = BeautifulSoup(html, 'html.parser')
        
        table = soup.find("table", class_="type2")
        if not table:
            break
            
        rows = table.find_all("tr")
        
        for row in rows:
            cols = row.find_all("td")
            if len(cols) == 7: # 날짜, 종가, 전일비, 시가, 고가, 저가, 거래량
                try:
                    date_str = cols[0].get_text().strip()
                    if not date_str:
                        continue
                    
                    # 날짜 파싱
                    date = datetime.strptime(date_str, '%Y.%m.%d')
                    
                    # 현재 날짜로부터 days 이전까지만 데이터 추가
                    if date < datetime.now() - timedelta(days=days):
                        continue
                    
                    close_price = int(cols[1].get_text().replace(',', ''))
                    open_price = int(cols[3].get_text().replace(',', ''))
                    high_price = int(cols[4].get_text().replace(',', ''))
                    low_price = int(cols[5].get_text().replace(',', ''))
                    
                    data.append({'Date': date, 'High': high_price, 'Low': low_price, 'Close': close_price})
                except ValueError:
                    continue
        page += 1
        if page > 100: # 무한 루프 방지
            break

    df = pd.DataFrame(data)
    if not df.empty:
        df = df.sort_values(by='Date').reset_index(drop=True)
        df = df.tail(days) # 요청한 일수만큼만 유지
    return df

if __name__ == '__main__':
    # 예시 사용법
    stock_code = '032820' # 우리기술
    historical_data = get_naver_finance_historical_data(stock_code, days=30)
    print(historical_data)
