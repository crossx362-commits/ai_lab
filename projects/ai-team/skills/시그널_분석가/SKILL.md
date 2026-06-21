---
name: signal-market-analyst
description: 데이브·레오에게 공급할 시장 인텔 데이터를 10분 주기로 수집·가공·저장. 공포탐욕지수, 김치 프리미엄, 업비트 상위 코인 퀀트 점수, 국내 주식 지수를 단일 JSON으로 통합 출력.
---

# 시그널 (Signal) — 시장 인텔리전스 에이전트

## 1. 역할

데이브·레오가 AI 판단을 내리기 전에 참고할 **객관적 시장 데이터**를 제공한다.
직접 매매하지 않는다. 데이터 수집·가공·저장·알림이 전부다.

---

## 2. 수집 데이터

### 암호화폐 시그널

| 항목 | 출처 | 갱신 주기 | 용도 |
|------|------|----------|------|
| 공포탐욕지수 | alternative.me/fng | 10분 | 심리 과열/공포 판단 |
| 김치 프리미엄 | 업비트 vs 바이낸스 가격 비율 | 10분 | 참고값 (진입 차단 기준 아님) |
| 코인 퀀트 점수 | 업비트 일봉 OHLCV | 10분 | 데이브·레오 종목 선정 |

**퀀트 점수 산출 (각 0~100점)**

```python
score = 0
if close > MA5:   score += 25   # 단기 추세
if close > MA20:  score += 25   # 중기 추세
if change > 1.0:  score += 20   # 당일 모멘텀
if volume > avg_volume * 1.5: score += 20  # 거래량 급증
if change < -3.0: score -= 20   # 급락 패널티
```

**감시 종목**: KRW-BTC, ETH, SOL, XRP, DOGE, ADA, AVAX, LINK (점수 상위 8개 추출)

### 주식 시그널 (선택)

| 항목 | 출처 | 비고 |
|------|------|------|
| KOSPI 지수 | KIS API | 실패해도 전체 중단 없음 |
| KOSDAQ 지수 | KIS API | 실패해도 전체 중단 없음 |

---

## 3. 출력 파일

**3개 경로에 동일 JSON 동시 저장** (호환성 유지)

```
reports/research/market_signal.json        ← 정식 파일
reports/research/market_pulse.json         ← 구버전 호환
reports/research/crypto_market_intel.json  ← 데이브·레오 직접 읽는 파일
```

### JSON 구조

```json
{
  "agent": "시그널",
  "agent_slug": "signal",
  "timestamp": "2026-06-21T10:00:00+09:00",
  "crypto": {
    "fear_greed": {
      "value": 45,
      "label": "Neutral",
      "signal": "NEUTRAL"   // BUY(≤20) | NEUTRAL | SELL(≥75)
    },
    "kimchi_premium": {
      "value": 1.2,
      "signal": "NEUTRAL"   // 항상 NEUTRAL (거래 제한 기준 아님)
    },
    "top_coins": [
      {"ticker": "KRW-BTC", "score": 90, "change": 2.1},
      {"ticker": "KRW-SOL", "score": 70, "change": 1.5}
    ]
  },
  "stock": {
    "indexes": {
      "kospi":  {"value": 2750.0, "change": 0.8},
      "kosdaq": {"value": 920.0,  "change": 1.2}
    },
    "sentiment": "BULLISH"   // BULLISH | NEUTRAL | BEARISH
  },
  "ai_analysis": "공포탐욕 45 중립; 김치프리미엄 1.2% 중립; 상위 코인 BTC:90, SOL:70, ETH:65; 주의: 큰 시장 차단 신호 없음."
}
```

---

## 4. 알림 규칙

공포탐욕 시그널이 변경됐을 때만 텔레그램 발송 (NEUTRAL→BUY, BUY→SELL 등)

```python
# 변경 감지 → 알림 (불필요한 반복 알림 차단)
if current_signal != previous_signal and current_signal != "NEUTRAL":
    send("📡 [시그널] 시장 신호가 바뀌었어요\n" + ai_analysis)
```

**상태 파일**: `market_signal_state.json` (직전 시그널 저장, 변경 감지용)

---

## 5. 데이브·레오 연동 방법

```python
# 데이브·레오 코드에서 읽는 방법
import json
with open("reports/research/crypto_market_intel.json") as f:
    intel = json.load(f)

fear_greed = intel["crypto"]["fear_greed"]["value"]   # 0~100
top_coins  = intel["crypto"]["top_coins"]             # 점수 순 정렬
ai_summary = intel["ai_analysis"]                     # 한 줄 요약

# 데이브: score >= 50인 종목만 동적 추가
dave_extra = [c for c in top_coins if c["score"] >= 50][:8]

# 레오: score >= 40이고 change 높은 종목만 동적 추가
leo_extra = sorted(
    [c for c in top_coins if c["score"] >= 40],
    key=lambda x: x["change"], reverse=True
)[:10]
```

---

## 6. 실행

```bash
# 1회 수집
python tools/market_signal.py

# 알림 포함 1회 수집
python tools/market_signal.py --notify

# 데몬 (10분 주기, 자동 재시작)
python tools/market_signal.py --daemon
```

**환경변수**
- `SIGNAL_INTERVAL_SECONDS`: 수집 주기 (기본 600초)
- `SIGNAL_STALE_LOCK_SECONDS`: 락 파일 만료 시간 (기본 180초)
- `USD_KRW_RATE`: 달러 환율 (기본 1300, 김치 프리미엄 계산용)

**프로세스 락**: `ProcessLock("signal")` — 중복 실행 자동 차단

**의존성**: `requests`, `pyupbit` (코인 점수), `_shared.env`, `_shared.notify`, `_shared.process`

---

## 7. 장애 처리

- 개별 항목 수집 실패 → 해당 항목만 `{"error": "...", "signal": "NEUTRAL"}` 처리, 전체 중단 없음
- KIS 주식 API 실패 → stock 섹션 `{"error": "...", "sentiment": "UNKNOWN"}`, 암호화폐 수집 계속
- 락 파일 180초 이상 방치 → 강제 삭제 후 재실행
- 연속 실패 시 지수 백오프 (최대 10분 대기)
