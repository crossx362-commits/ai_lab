
import yfinance as yf
import pandas as pd

def analyze_stock(ticker_symbol):
    """
    주식 종목 코드를 입력받아 현재 주가, 거래량, 이동평균선을 분석합니다.
    """
    try:
        ticker = yf.Ticker(ticker_symbol)
        hist = ticker.history(period="1mo") # 지난 1개월 데이터 가져오기

        if hist.empty:
            return {"error": "해당 종목의 데이터를 찾을 수 없습니다."}

        latest_data = hist.iloc[-1]
        current_price = latest_data['Close']
        current_volume = latest_data['Volume']

        # 이동평균선 계산
        hist['MA5'] = hist['Close'].rolling(window=5).mean()
        hist['MA20'] = hist['Close'].rolling(window=20).mean()

        ma5 = hist['MA5'].iloc[-1]
        ma20 = hist['MA20'].iloc[-1]

        analysis_result = {
            "ticker": ticker_symbol,
            "current_price": round(current_price, 2),
            "current_volume": int(current_volume),
            "MA5": round(ma5, 2),
            "MA20": round(ma20, 2),
            "analysis": ""
        }

        # 데이브의 분석 규칙을 반영한 간단한 분석
        if current_price > ma5 and current_price > ma20:
            analysis_result["analysis"] = "현재 주가가 단기 및 중기 이동평균선 위에 있어 긍정적인 추세로 보입니다."
        elif current_price < ma5 and current_price < ma20:
            analysis_result["analysis"] = "현재 주가가 단기 및 중기 이동평균선 아래에 있어 하락 추세로 보입니다. 추가적인 분석이 필요합니다."
        else:
            analysis_result["analysis"] = "주가가 이동평균선 사이에 위치하여 혼조세를 보입니다."

        # 거래량 분석 (데이브의 규칙 일부 반영)
        avg_volume_20d = hist['Volume'].rolling(window=20).mean().iloc[-1]
        if current_volume > avg_volume_20d * 1.5: # 평균 거래량의 1.5배 이상이면 관심
            analysis_result["analysis"] += " 최근 거래량이 평소보다 크게 증가하여 시장의 관심이 높아지고 있을 수 있습니다."
        elif current_volume < avg_volume_20d * 0.5: # 평균 거래량의 절반 이하이면 관심 저조
            analysis_result["analysis"] += " 최근 거래량이 평소보다 감소하여 시장의 관심이 저조할 수 있습니다."

        return analysis_result

    except Exception as e:
        return {"error": f"주식 데이터를 분석하는 중 오류가 발생했습니다: {e}"}

if __name__ == "__main__":
    # 테스트 코드 (예: 우리기술)
    # 한국 주식은 종목 코드 뒤에 ".KS" 또는 ".KQ"를 붙여야 합니다.
    # 우리기술은 코스닥 상장 종목이므로 032820.KQ
    result = analyze_stock("032820.KQ")
    print(result)
