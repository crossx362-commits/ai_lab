# 주식 외국인 순매수 전략

외국인 3거래일 연속 순매수와 보조지표를 함께 보는 한국 주식 후보 발굴
전략이다. 시그널은 후보를 만들고, 데이브 주식 봇은 보수적으로 최종
진입 여부를 판단한다.

## 적용 대상

- 시장: KOSPI, KOSDAQ
- 후보 탐색: 시그널 분석가
- 매매 판단: 데이브 주식
- 출력 경로: `reports/research/market_pulse.json`

## 필수 조건

모든 조건을 만족해야 후보로 본다.

1. 외국인 3거래일 연속 순매수
2. RSI 50 이상
3. MACD line이 signal line을 상향 돌파하거나 상향 우위
4. 거래량이 충분한 종목

## 점수 체계

최대 16점 기준으로 진입 강도를 판단한다.

| 항목 | 조건 | 점수 |
| --- | --- | --- |
| 등락률 | -3.0% ~ -0.5% 조정 구간 | +3 |
| 등락률 | +0.5% ~ +4.0% 상승 확인 | +2 |
| 등락률 | +7.0% 이상 과열 | -2 |
| 거래량 | 100,000주 이상 | +2 |
| 가격 위치 | 현재가가 시가보다 높음 | +1.5 |
| 가격 위치 | 현재가가 당일 고저 범위의 60% 이상 | +1.5 |
| 외국인 | 3거래일 연속 순매수 | +2 |
| RSI | 50 이상 | +2 |
| MACD | 골든크로스 또는 상향 우위 | +2 |

기본 시그널 점수는 다음처럼 100점 척도로 보정할 수 있다.

```text
signal_score = 50 + (RSI - 50) + (10 if MACD bullish else 0)
```

## 시그널 역할

시그널 분석가는 감시 종목을 순회하며 외국인 순매수, RSI, MACD를 계산한다.

```python
for stock in watch_list:
    if consecutive_foreign_buy_days >= 3 and rsi >= 50 and macd_bullish:
        candidates.append(stock)
```

출력 예시:

```json
{
  "stock": {
    "top_stocks": [
      {
        "code": "005930",
        "name": "삼성전자",
        "score": 75,
        "rsi": 62.5,
        "macd": 1.23,
        "foreign_days": 3
      }
    ]
  }
}
```

## 데이브 주식 역할

데이브 주식 봇은 `market_pulse.json`의 후보를 읽고, 점수와 리스크 기준을
통과한 종목만 LLM 최종 판단으로 넘긴다.

```python
intel = load_json("reports/research/market_pulse.json")
top_stocks = intel.get("stock", {}).get("top_stocks", [])

for stock in top_stocks:
    if stock["score"] >= 55:
        analyze_with_llm(stock)
```

## 리스크 관리

- 익절: +5%
- 손절: -3%
- 과열 RSI 70 이상 종목은 추격 매수 금지
- 외국인 1~2일 순매수만으로 진입 금지
- MACD 약세 전환 시 신규 진입 금지
- 보조지표만 보고 진입하지 말고 가격, 거래량, 시장 상황을 함께 본다.

## 구현 파일

- `projects/ai-team/skills/데이브_주식/tools/kis_client.py`
- `projects/ai-team/skills/데이브_주식/tools/stock_auto_trader.py`
- `projects/ai-team/skills/시그널_분석가/tools/market_signal.py`

## 변경 이력

- 2026-06-25: 외국인 순매수 + RSI/MACD 기반 공용 전략 문서 정리
