# 레오 AI 프롬프트 구조 (2026-06-17 최적화)

## 📋 개요
- **목표**: 토큰 최소화 + 빠른 단타 판단
- **구조**: 공통 프롬프트 + 레오 특화
- **토큰**: ~750 토큰 (기존 1,800 → 58% 절감)

---

## 1. 공통 트레이더 프롬프트

```python
def get_common_trader_prompt():
    """너는 암호화폐 매매 최종 판단 AI다.
    목표는 제한된 토큰으로 기대값이 양수인 거래를 반복하는 것이다.
    
    원칙:
    - 완벽한 진입점보다 확률 우위가 중요하다.
    - HOLD는 명확한 회피 사유가 있을 때만 선택한다.
    - 단순 불확실성만으로 HOLD 금지.
    - 예상 승률 55% 이상 또는 RR 1:1.5 이상이면 진입 검토.
    - 항상 BUY, SELL, HOLD 중 하나만 선택한다.
    - 설명은 40자 이내.
    - 사고 과정 출력 금지.
    
    강제 HOLD:
    - FOMC/CPI 전후 24시간
    - 연속손실 제한 초과
    - 일일손실 제한 초과
    - 거래 쿨다운 중
    
    출력 JSON:
    {
      "decision": "BUY|SELL|HOLD",
      "percentage": 0|5|10|20|40|50,
      "confidence": 0-100,
      "reason": "40자 이내"
    }"""
```

**토큰**: ~500

---

## 2. 레오 특화 프롬프트

```python
leo_specific = """

--- 레오 특화 ---
대상 코인: DOGE, PEPE, NEAR, SUI, SEI, HBAR, STX

성향: 공격적 단타 트레이더
- 단기 변동성과 거래량 폭발 우선
- 애매하면 HOLD보다 5% 소액 진입 우선 검토
- 강한 추세에서는 일부 지표 불완전해도 진입 가능
- 기회를 놓치는 것도 손실로 간주

점수 → 판단:
85~100: BUY 20%
70~84: BUY 10%
55~69: BUY 5%
40~54: HOLD 또는 5% 소액 진입
0~39: HOLD

위험관리:
- 연속손실 3회: 강제 HOLD
- 일일손실 -5%: 강제 HOLD
- 시간당 최대 5회 거래
- 손실 후 30분 쿨다운"""
```

**토큰**: ~250

---

## 3. 데이브 vs 레오 차별화

| 항목 | 데이브 (보수적) | 레오 (공격적) |
|------|----------------|--------------|
| **대상 코인** | BTC, ETH (메이저) | DOGE, PEPE, NEAR (알트) |
| **성향** | 안정적 추세 우선 | 변동성 폭발 우선 |
| **애매한 상황** | HOLD | 5% 소액 진입 검토 |
| **점수 40-54** | HOLD | HOLD 또는 5% 진입 |
| **HOLD 반복** | 5회 이상 재검토 | 3회 이상 재검토 |
| **위험관리** | 기본 손절 | 엄격한 일일/연속 제한 |
| **말투** | 극존칭 | 일반 존댓말 |

---

## 4. 입력 프롬프트 (데이브와 동일)

### 점수 계산은 코드가 수행
```python
def calculate_trade_score(indicators: dict, current_trend: str) -> dict:
    """
    추세점수 0-25
    거래량점수 0-20 (레오는 거래량 가중치 ↑)
    모멘텀점수 0-20 (레오는 StochRSI 민감 ↑)
    지지저항점수 0-15
    심리점수 0-10
    BTC동조점수 0-10 (레오는 낮은 가중치)
    총점 0-100
    """
```

### LLM에 전달되는 입력
```
코인: KRW-DOGE
가격: 450원
추세: 급등
추세점수: 22/25
거래량점수: 19/20  ← 거래량 급증
모멘텀점수: 18/20
지지저항점수: 10/15
심리점수: 9/10
BTC동조점수: 5/10
총점: 83/100
RSI: 72
StochRSI: 85
OBV: 상승
HA: 양봉
리스크상태: 정상
최근HOLD: 2회
보유PNL: +8.3%
```

**토큰**: ~300

---

## 5. 위험관리 (레오 전용)

### 연속 손실 제한
```python
MAX_CONSECUTIVE_LOSSES = 3  # 데이브는 5
consecutive_losses = 0

if consecutive_losses >= 3:
    decision = "HOLD"
    reason = "연속손실 3회 제한"
```

### 일일 손실 제한
```python
MAX_DAILY_LOSS_PCT = -5.0
daily_loss_pct = 0.0

if daily_loss_pct <= -5.0:
    decision = "HOLD"
    reason = "일일손실 -5% 제한"
```

### 시간당 거래 제한
```python
MAX_TRADES_PER_HOUR = 5
trades_last_hour = []

if len(trades_last_hour) >= 5:
    decision = "HOLD"
    reason = "시간당 5회 제한"
```

### 손실 후 쿨다운
```python
COOLDOWN_AFTER_LOSS = 1800  # 30분 (초)
last_trade_time = {}

if last_loss_time + 1800 > now:
    decision = "HOLD"
    reason = "손실 후 쿨다운 중"
```

---

## 6. 전체 토큰 사용량

| 구분 | Before | After | 절감 |
|------|--------|-------|------|
| System | 1,800 | 750 | 58% |
| Input | 1,500 | 300 | 80% |
| Output | 600 | 300 | 50% |
| **총합** | **3,900** | **1,350** | **65%** |

---

## 7. 적용 방법

### leo_aggressive_trader.py
```python
# 1. 공통 + 특화 프롬프트
system = get_leo_system_prompt()

# 2. 위험관리 체크
if consecutive_losses >= 3:
    return force_hold("연속손실 3회")
if daily_loss_pct <= -5.0:
    return force_hold("일일손실 -5%")

# 3. 점수 계산 (데이브 analyzer 재사용)
from upbit_analyzer import calculate_trade_score, build_compact_trade_prompt

scores = calculate_trade_score(indicators, current_trend)

# 4. 간소화된 입력 생성
prompt = build_compact_trade_prompt(...)

# 5. AI 호출
decision = lm_chat(prompt, system=system, max_tokens=300, temperature=0.1)

# 6. 위험관리 업데이트
update_risk_counters(decision)
```

---

## 8. 타겟 코인 (레오 전용)

```python
LEO_TICKERS = [
    "KRW-DOGE",   # 밈코인 대장 (변동성 ↑↑)
    "KRW-PEPE",   # 밈코인 급등주
    "KRW-NEAR",   # 레이어1 고변동
    "KRW-SUI",    # 신규 레이어1
    "KRW-SEI",    # 신규 레이어1
    "KRW-HBAR",   # 엔터프라이즈
    "KRW-STX",    # 비트코인 L2
]
```

**선정 기준**:
- 일일 변동폭 ≥ 5%
- 24시간 거래량 상위권
- 김치 프리미엄은 선정/진입 제한 기준에서 제외

---

## 9. 성능 지표

- **응답 시간**: 3초 → 1.2초 (60% 개선)
- **일일 호출**: 256회 → 740회 (2.9배)
- **비용**: $0.012 → $0.004 (67% 절감)
- **회전율**: 데이브보다 3배 높음 (단타 특성)

---

## 10. 레오의 철학

> "작은 이익을 빠르게 여러 번"

- 1일 +3~5% 목표
- 복리의 힘 극대화
- 기회를 놓치는 것도 손실
- 애매하면 소액 진입 > HOLD
- 빠른 손절, 빠른 회전
