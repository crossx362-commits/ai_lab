import requests
from bs4 import BeautifulSoup
import pandas as pd
from datetime import datetime, timedelta

def get_stock_data_naver(stock_code, days=30):
    all_data = []
    page = 1
    today = datetime.now().date()

    while len(all_data) < days:
        url = f"https://finance.naver.com/item/sise_day.naver?code={stock_code}&page={page}"
        res = requests.get(url, headers={'User-Agent': 'Mozilla/5.0'})
        soup = BeautifulSoup(res.text, 'html.parser')
        
        table = soup.find('table', class_='type2')
        if not table:
            break
        
        rows = table.find_all('tr')
        
        page_data_count = 0
        for row in rows:
            cols = row.find_all('td')
            if len(cols) > 1:
                try:
                    date_str = cols[0].text.strip()
                    if not date_str.replace('.', '').isdigit():
                        continue
                    
                    current_date = datetime.strptime(date_str, '%Y.%m.%d').date()
                    if current_date > today:
                        continue
                        
                    close_price = int(cols[1].text.strip().replace(',', ''))
                    open_price = int(cols[3].text.strip().replace(',', ''))
                    high_price = int(cols[4].text.strip().replace(',', ''))
                    low_price = int(cols[5].text.strip().replace(',', ''))
                    
                    all_data.append({
                        'Date': current_date,
                        'Open': open_price,
                        'High': high_price,
                        'Low': low_price,
                        'Close': close_price
                    })
                    page_data_count += 1
                    if len(all_data) >= days:
                        break
                except ValueError:
                    continue
        page += 1
        if page_data_count == 0: # 현재 페이지에서 더 이상 유효한 데이터가 없으면 종료
            break
            
    df = pd.DataFrame(all_data)
    if df.empty:
        return pd.DataFrame(columns=['Date', 'Open', 'High', 'Low', 'Close'])
        
    df = df.sort_values(by='Date').reset_index(drop=True)
    return df.head(days)