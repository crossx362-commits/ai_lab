---
name: coin-stock-integrated-trading-suite-v3-min
description: 토큰 최소화 코인·주식 통합 매매 분석 스킬. Signal 데이터, Dave 보수 스윙, Leo 공격 단타, Stock 참고 분석을 하나의 짧은 라우터로 처리하고 BUY/SELL/HOLD JSON만 출력한다.
---

# Coin/Stock Trading Suite v3-min

## 0. 목표
업비트 현물 중심 매매 판단. 주식은 별도 주문 API 없으면 분석만. LLM은 주문자가 아니라 판단 보조자. 사고 과정 출력 금지. 기본 출력은 JSON만.

## 1. 공통 원칙
- 항상 `BUY|SELL|HOLD` 중 하나.
- 현물 `SELL` = 보유 물량 청산. 보유 없으면 `HOLD`.
- 계좌 보호 > 데이터 신선도 > 전략 신호 > 포지션 크기.
- `reason` 25자 이내. 리포트는 사용자가 요청할 때만.
- `percentage`는 전략 이용금 기준. `total_exposure_pct`도 출력.

## 2. 자금
```text
Dave 40% / Leo 40% / Cash 20%
Dave 1회 5|10|20%, Leo 1회 5|10|20|30%
전체 일일손실 -3% → 신규진입 금지
연속손실 3회 → 신규진입 금지
```

## 3. 하드 HOLD
하나라도 해당하면 신규 `BUY` 금지.
```text
api_error | balance_error | stale_signal | cooldown | daily_loss_limit |
loss_streak_limit | min_order_fail | low_liquidity | wide_spread |
abnormal_spike | open_order_unresolved
```

## 4. Signal 입력
Signal은 매매하지 않고 데이터만 공급. 입력을 짧게 압축한다.
```text
ts, stale, event, fg, kp, btc_state, ticker, price, pos, pnl,
trend, vol_x, mom_1h, mom_5m, rsi, stoch, macd, ema200,
obv, cvd, atr, poc, spread, liquidity, score
```
만료 기준: Dave 20분, Leo 7분.
김프는 참고값. 단독 BUY/SELL/HOLD 근거 금지.

## 5. Router
```text
major coin or swing → Dave
DOGE/PEPE/NEAR/SUI/SEI/HBAR/STX or scalp → Leo
stock/index → Stock
unclear → score 높은 전략 1개만 선택
```

## 6. Dave: 보수 스윙
대상: BTC, ETH, SOL, XRP, ADA, AVAX, LINK 등 메이저.
이벤트: FOMC/CPI/PPI/NFP 전 2시간 신규진입 금지.
점수 100점:
```text
trend 25 + volume 20 + momentum 20 + support 15 + sentiment 10 + btc_sync 10
```
결정:
```text
85+   BUY 20%
70-84 BUY 10%
55-69 BUY 5%
40-54 HOLD
0-39  HOLD 또는 보유+추세붕괴 시 SELL
```
가산: price>ema200, supertrend_up, macd_up, obv_up, price>poc.
감산: stoch>80, fg>=75, btc_state=BEARISH, price<ema200.
손절: entry − max(1.5%, 2×ATR). 익절: +2~5% 또는 RR≥1.5.

## 7. Leo: 공격 단타
대상: 고변동 알트. 이벤트: 발표 전 30분 금지, 이후 거래량 방향 확인 후 가능.
진입 조건 4개 중 3개 이상:
```text
vol_x≥1.5 | mom_5m>0 | mom_1h≥0.5 | spread_normal | liquidity_ok
```
금지: fg≥80, wide_spread, low_liquidity, spike_exhausted, daily_loss≤-5%.
결정:
```text
80+   BUY 30%
65-79 BUY 20%
50-64 BUY 5|10%
else  HOLD
```
익절: +3% 50%, +5% 나머지. 손절: -2% 또는 1.5×ATR. 손실 후 30분 쿨다운. 시간당 5회 초과 금지.

## 8. Stock
공시·거래시간·VIX·주문·뉴스 확인 불가 시 `analysis_only:true`. 주문 API + 실시간 리스크 확인 가능할 때만 BUY/SELL 허용.

## 9. 출력 JSON
기본 출력은 아래 한 개만.
```json
{
  "agent": "Dave|Leo|Stock|Signal",
  "market": "crypto|stock",
  "ticker": "KRW-BTC",
  "decision": "BUY|SELL|HOLD",
  "action_type": "NEW_ENTRY|ADD|TAKE_PROFIT|STOP_LOSS|EXIT|NONE",
  "percentage": 0,
  "agent_allocation_pct": 40,
  "total_exposure_pct": 0,
  "confidence": 0,
  "entry": null,
  "tp1": null,
  "tp2": null,
  "sl": null,
  "rr": null,
  "risk_flags": [],
  "analysis_only": false,
  "reason": "25자이내"
}
```
