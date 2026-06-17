# AI 퀄리티 최적화 전략: 토큰 최소화 + 높은 품질

## 🎯 목표
- **토큰 사용량 최소화** (비용 절감)
- **응답 품질 유지** (정확도, 유용성)

## 📋 핵심 전략

### 1. Few-Shot Examples (소수 예시)
**토큰 절약 + 품질 향상**

**적용 전:**
```python
system = """너는 트레이더다. BUY/SELL/HOLD를 결정해라."""
```

**적용 후:**
```python
system = """너는 트레이더다.

예시:
입력: BTC 95M원|상승추세|RSI:75|OBV상승
출력: {"decision":"BUY","percentage":30,"reason":"OBV 다이버전스 매집"}

입력: BTC 95M원|하락추세|RSI:85|OBV하락
출력: {"decision":"HOLD","percentage":0,"reason":"과열 + OBV 분산"}

이제 분석:"""
```

**효과:**
- 예시 2-3개만으로도 정확도 크게 향상
- 긴 설명보다 예시가 토큰 효율적
- 일관된 출력 형식 보장

---

### 2. Chain-of-Thought (단계별 사고)
**퀄리티 향상 핵심**

**일반 프롬프트:**
```python
"BTC 매수/매도/관망 결정해줘"
```

**CoT 적용:**
```python
"""BTC 분석:
1. 현재 추세는? (상승/하락/횡보)
2. 거래량은 어떤가? (증가/감소)
3. 기술지표는? (과열/정상/과매도)
4. 결론: BUY/SELL/HOLD + 이유"""
```

**효과:**
- LLM이 단계별로 사고 → 더 정확한 결론
- 토큰 약간 증가하지만 품질 크게 향상
- 투자 판단 같은 중요 작업에 필수

---

### 3. Structured Output (구조화된 출력)
**토큰 절약 + 파싱 안정성**

**JSON Schema 사용:**
```python
from pydantic import BaseModel

class TradeDecision(BaseModel):
    decision: str  # BUY/SELL/HOLD
    percentage: int  # 0-100
    reason: str  # 1문장

config = types.GenerateContentConfig(
    response_schema=TradeDecision,
    response_mime_type="application/json"
)
```

**효과:**
- LLM이 불필요한 설명 없이 정확한 형식만 출력
- 파싱 에러 0%
- 출력 토큰 50-70% 감소

---

### 4. Prompt Compression (프롬프트 압축)
**토큰 절약 극대화**

**적용 전:**
```python
prompt = f"""
현재 비트코인 시세는 {price}원입니다.
현재 추세는 {trend}입니다.
RSI 지표는 {rsi}입니다.
OBV 지표는 {obv}입니다.
매수/매도를 결정해주세요.
"""
```

**적용 후:**
```python
# 구분자 기반 압축
prompt = f"BTC:{price}|{trend}|RSI:{rsi}|OBV:{obv}→결정?"
```

**효과:**
- 토큰 60-70% 감소
- GPT는 압축된 형식도 잘 이해
- 데이터가 많을수록 효과 큼

---

### 5. System Instruction 최적화
**입력 토큰 대폭 절감**

**원칙:**
1. **핵심만** - 장황한 설명 제거
2. **규칙 중심** - "하지 마라" 보다 "하라"
3. **예시 포함** - 1-2개 예시로 설명 대체

**적용 전 (8,000 토큰):**
```markdown
# 데이브 트레이더 매뉴얼
## 1. 소개
데이브는 세계 최고의 트레이더로...
## 2. 매크로 분석
연준의 정책을 분석할 때는...
[30KB의 상세 설명]
```

**적용 후 (500 토큰):**
```markdown
AI 트레이더 데이브. 규칙:
1. FOMC 전후 24h → HOLD
2. 김프 15%+ → SELL
3. OBV↑+가격↓ → BUY
[7개 핵심 규칙만]
```

**효과:**
- 토큰 94% 감소
- 품질 유지 (핵심만 전달)

---

### 6. Dynamic Context (동적 컨텍스트)
**필요한 것만 보내기**

**적용 전:**
```python
# 항상 전체 데이터 전송
prompt = f"{all_indicators}{all_history}{all_news}..."
```

**적용 후:**
```python
# 상황에 따라 필요한 것만
if decision_type == "quick":
    prompt = f"BTC:{price}|{trend}|RSI:{rsi}"
elif decision_type == "detailed":
    prompt = f"BTC:{price}|{all_indicators}|{recent_news}"
```

**효과:**
- 간단한 판단은 적은 토큰
- 복잡한 판단만 많은 토큰
- 평균 토큰 50% 감소

---

### 7. Temperature 조정
**품질과 다양성 균형**

**용도별 최적값:**
```python
# 정확한 분석/판단 (트레이딩, 데이터 분석)
temperature = 0.1  # 가장 확률 높은 답만

# 일반 대화 (영숙 챗봇)
temperature = 0.7  # 자연스러운 대화

# 창작 (제목, 카피)
temperature = 0.9  # 다양한 아이디어
```

**효과:**
- 낮은 temperature = 짧고 결정론적
- 토큰 10-20% 감소
- 투자 판단은 반드시 0.1 사용

---

### 8. Model Selection (모델 선택)
**작업별 최적 모델**

| 작업 | 모델 | 이유 |
|------|------|------|
| 간단한 판단 | GPT-4o mini | 빠르고 저렴 |
| 복잡한 분석 | GPT-4o | 높은 품질 필요 |
| 로컬 작업 | Ollama (무료) | 비용 0 |
| 구조화 출력 | GPT-4o mini + JSON schema | 완벽한 호환 |

**현재 적용:**
```python
# 기본: GPT-4o mini (저렴, 빠름)
# 폴백: Ollama (무료)
# Gemini: 비활성화 (할당량 문제)
```

---

## 📊 종합 적용 예시 (데이브 트레이더)

### Before (12,000 토큰)
```python
system = load_full_manual()  # 8,000 토큰
prompt = f"""
현재 비트코인 시세 정보:
가격: {price}
추세: {trend}
[장황한 설명... 2,000 토큰]
"""
max_output_tokens = 2000
temperature = 0.7
```

### After (1,500 토큰)
```python
# 1. System: 핵심만 (500 토큰)
system = """트레이더. 규칙 7개 + 예시 2개"""

# 2. Prompt: 압축 (200 토큰)
prompt = f"BTC:{price}|{trend}|RSI:{rsi}|OBV:{obv}"

# 3. Output: 제한 (500 토큰)
max_output_tokens = 500

# 4. Temperature: 낮춤
temperature = 0.1

# 5. Structured Output
response_schema = TradeDecision
```

**결과:**
- 토큰: 12,000 → 1,500 (87% 감소)
- 비용: 87% 절감
- 품질: **유지 또는 향상** (Few-shot + CoT + 구조화)

---

## ✅ 실전 체크리스트

### 토큰 절약
- [ ] System instruction 2KB 이하
- [ ] Prompt 압축 (구분자 사용)
- [ ] max_output_tokens 500 이하
- [ ] 불필요한 설명 제거
- [ ] Dynamic context (필요한 것만)

### 품질 유지/향상
- [ ] Few-shot 예시 2-3개
- [ ] Chain-of-Thought (중요 판단)
- [ ] Structured Output (JSON schema)
- [ ] Temperature 0.1 (판단 작업)
- [ ] 핵심 규칙 명확히

### 비용 최적화
- [ ] 간단한 작업 → GPT-4o mini
- [ ] 로컬 가능 → Ollama
- [ ] 패턴 매칭 가능 → AI 생략

---

## 🎯 기대 효과

| 지표 | Before | After | 개선 |
|------|--------|-------|------|
| 평균 토큰/호출 | 12,000 | 1,500 | 87% ↓ |
| 일일 가능 호출 | 83 | 555 | 6.7배 |
| 비용/호출 | $0.012 | $0.0015 | 87% ↓ |
| 응답 품질 | 기준 | **향상** | +15% |
| 파싱 에러율 | 5% | 0% | 100% ↓ |

**핵심:** 토큰을 줄이면서도 Few-shot + CoT + 구조화로 품질이 오히려 향상!
