# AI 토큰 최적화 스킬

## 📋 스킬 개요
모든 에이전트가 AI API(GPT, Gemini, Claude) 호출 시 토큰을 최소화하면서도 품질을 유지/향상시키는 실전 스킬.

---

## ✅ 적용 대상
- **필수**: 데이브, 영숙, 예원 (API 사용 에이전트)
- **권장**: 모든 에이전트 (향후 API 사용 시)

---

## 🎯 핵심 기법

### 1. System Instruction 압축 (94% 토큰 절감)

#### ❌ 잘못된 예
```python
system = """
# 데이브 트레이더 완전 가이드

## 소개
데이브는 세계 최고의 AI 트레이더로서 다음과 같은 역할을 수행합니다:
- 글로벌 매크로 경제 분석
- 미 연준 정책 이벤트 모니터링
[30KB의 장황한 설명...]
"""
```

#### ✅ 올바른 예
```python
system = """AI 트레이더. 규칙:
1. FOMC/CPI 24h전후→HOLD
2. 김프15%+→SELL
3. 가격↓+OBV↑→BUY
[핵심만 7개]"""
```

**원칙:**
- 핵심만 (1줄 1규칙)
- 압축 표기 (→, ↑, ↓)
- 2KB 이하 목표

---

### 2. Few-Shot Learning (품질 +20%)

#### 구조
```python
system = """규칙 7개

예시1:
입력: [압축된 데이터]
출력: {"decision":"BUY","reason":"이유"}

예시2:
입력: [다른 케이스]
출력: {"decision":"HOLD","reason":"이유"}

이제 분석:"""
```

**원칙:**
- 예시 2-3개 필수
- 입력/출력 형식 정확히
- 다양한 케이스 커버

---

### 3. Prompt 압축 (60% 토큰 절감)

#### ❌ 잘못된 예
```python
prompt = f"""
현재 비트코인 시세는 {price}원입니다.
현재 추세는 {trend}입니다.
RSI 지표 값은 {rsi}입니다.
거래량은 {volume}입니다.
"""
```

#### ✅ 올바른 예
```python
# 구분자 압축
prompt = f"BTC:{price}|{trend}|RSI:{rsi}|Vol:{volume}"

# 또는 JSON 압축
prompt = json.dumps({"c":price,"t":trend,"r":rsi,"v":volume})
```

---

### 4. Output 제한 (75% 토큰 절감)

```python
# API 호출 시 항상 설정
max_output_tokens = 500  # 기본 2000 대신
temperature = 0.1  # 판단 작업은 0.1
```

**용도별 권장값:**
- 판단/분석: 500 토큰, temp=0.1
- 일반 대화: 800 토큰, temp=0.7
- 창작: 1500 토큰, temp=0.9

---

### 5. Structured Output (파싱 에러 0%)

```python
from pydantic import BaseModel

class Response(BaseModel):
    decision: str
    percentage: int
    reason: str

# API 호출
config = types.GenerateContentConfig(
    response_schema=Response,
    response_mime_type="application/json"
)
```

**필수 적용:**
- 모든 JSON 응답
- 데이터 추출 작업
- 자동화 파이프라인

---

### 6. Chain-of-Thought (품질 +15%)

```python
system = """분석 단계:
1. 추세는? (상승/하락/횡보)
2. 거래량은? (증가/감소)
3. 지표는? (과열/정상/과매도)
4. 결론: BUY/SELL/HOLD + 이유"""
```

**적용 대상:**
- 중요한 판단 (투자, 의료, 법률)
- 복잡한 분석
- 다단계 추론 필요 시

---

## 📊 통합 적용 템플릿 (2026-06-17 최신)

### 데이브 트레이더 예시 (점수 기반)

```python
def analyze_crypto(ticker, current_price, current_trend, indicators):
    # 1. 공통 + 특화 프롬프트 조합
    common = get_common_trader_prompt()  # 공통 원칙 ~500 토큰
    dave_specific = """
--- 데이브 특화 ---
성향: 보수적 (극존칭)
점수 → 판단:
85~100: BUY 20%
70~84: BUY 10%
55~69: BUY 5%
40~54: HOLD
0~39: HOLD"""
    
    system = common + dave_specific  # 총 ~700 토큰
    
    # 2. 점수 계산 (코드가 수행)
    scores = calculate_trade_score(indicators, current_trend)
    
    # 3. 간소화된 입력 (LLM은 판단만)
    prompt = f"""코인: {ticker}
가격: {current_price}원
추세: {current_trend}
총점: {scores['total']}/100
RSI: {indicators['RSI']}
OBV: {indicators['OBV']}
김프: {kimchi_pct}%
리스크상태: 정상
최근HOLD: {consecutive_holds}회"""
    
    # 4. API 호출: 구조화 + 토큰 제한
    response = lm_chat(
        prompt=prompt,
        system=system,
        max_tokens=300,  # 기존 500 → 300
        temperature=0.1,
        json_mode=True
    )
    
    return response

def calculate_trade_score(indicators: dict, current_trend: str) -> dict:
    """점수 계산은 코드가 수행 (일관성 보장)"""
    score = 0
    if "상승" in current_trend: score += 20
    if indicators.get('VolumeSpike') == '급증': score += 20
    if indicators.get('StochRSI_상태') == '골든크로스': score += 20
    # ...
    return {'total': score, 'trend': 20, 'volume': 20, ...}
```

---

## 🎯 체크리스트

### 토큰 절약 (필수)
- [ ] System instruction 2KB 이하
- [ ] Prompt 구분자 압축 (|, :, →)
- [ ] max_output_tokens=500
- [ ] temperature=0.1 (판단 작업)

### 품질 향상 (권장)
- [ ] Few-shot 예시 2-3개
- [ ] Chain-of-Thought 단계
- [ ] Structured Output (JSON schema)

### 비용 최적화 (권장)
- [ ] 간단한 작업 → GPT-4o mini
- [ ] 로컬 가능 → Ollama 우선
- [ ] 패턴 매칭 가능 → AI 생략

---

## 📈 예상 효과

| 지표 | Before | After | 개선 |
|------|--------|-------|------|
| 토큰/호출 | 12,000 | 1,500 | 87% ↓ |
| 일일 호출 | 83회 | 555회 | 6.7배 |
| 비용 | $0.012 | $0.0015 | 87% ↓ |
| 품질 | 기준 | +20% | 향상 |
| 파싱 에러 | 5% | 0% | 완벽 |

---

## 🔧 구현 예시

### 영숙 (텔레그램 봇)
```python
# 패턴 매칭으로 API 우회 (80% 절감)
if "현황" in msg or "뭐해" in msg:
    return get_agent_status("전체")  # AI 호출 없음

if "일정" in msg:
    return list_calendar()  # AI 호출 없음

# AI 호출은 복잡한 요청만
system = """비서 영숙. 규칙:
1. 현황→get_agent_status()
2. 일정→list_calendar()
3. 구동→dispatch()"""
```

### 데이브/레오 (공통 구조)
```python
# 공통 프롬프트 함수
def get_common_trader_prompt():
    """공통 원칙 ~500 토큰"""
    return """너는 암호화폐 매매 최종 판단 AI다.
목표는 제한된 토큰으로 기대값이 양수인 거래를 반복하는 것이다.
...
출력 JSON:
{"decision":"BUY|SELL|HOLD","percentage":0|5|10|20|40|50,"confidence":0-100,"reason":"40자이내"}"""

# 데이브 특화
def load_dave_prompt():
    common = get_common_trader_prompt()
    return common + """
--- 데이브 특화 ---
성향: 보수적 (극존칭)
점수 → 판단:
85~100: BUY 20%
70~84: BUY 10%
..."""

# 레오 특화
def load_leo_prompt():
    common = get_common_trader_prompt()
    return common + """
--- 레오 특화 ---
대상: DOGE, PEPE, NEAR...
성향: 공격적 단타
애매하면 5% 소액 진입 우선
..."""
```

---

## ⚠️ 주의사항

1. **압축 과도 금지**
   - ❌ "BTC:95M|↑|R:45|O:↑" (너무 압축)
   - ✅ "BTC:95M|상승|RSI:45|OBV:상승" (적절)

2. **예시 품질**
   - 다양한 케이스 커버
   - 입력/출력 형식 정확
   - 실제 사용 케이스 반영

3. **CoT 남용 금지**
   - 간단한 작업은 불필요
   - 중요 판단에만 적용

---

## 📚 참고 문서
- `docs/AI_QUALITY_OPTIMIZATION.md` - 전체 전략
- `docs/GEMINI_TOKEN_OPTIMIZATION.md` - Gemini 특화
- `_shared/gemini_client.py` - 구현 예시
