# AI 퀄리티 최적화 전략: 토큰 최소화 + 높은 품질

## 🎯 목표
- **토큰 사용량 최소화** (비용 절감)
- **응답 품질 유지** (정확도, 유용성)

## 📋 핵심 전략

### 1. Few-Shot Examples (소수 예시)
**토큰 절약 + 품질 향상**

**적용 전:**
```python
system = """너는 QA 판정 AI다. P0/P1/P2/P3를 결정해라."""
```

**적용 후:**
```python
system = """너는 QA 판정 AI다.

예시:
입력: 콘솔오류있음|영향탭2개|회귀아님|접근성정상
출력: {"decision":"P1","score":70,"reason":"콘솔 오류 + 다중 탭 영향"}

입력: 콘솔오류없음|영향탭1개|회귀아님|접근성경미
출력: {"decision":"P3","score":20,"reason":"경미한 접근성 이슈만"}

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
"이 QA 이슈 심각도 판정해줘"
```

**CoT 적용:**
```python
"""QA 이슈 분석:
1. 콘솔 오류가 있는가? (있음/없음)
2. 영향 범위는? (전체/일부 탭)
3. 회귀인가? (신규/기존)
4. 결론: P0/P1/P2/P3 + 이유"""
```

**효과:**
- LLM이 단계별로 사고 → 더 정확한 결론
- 토큰 약간 증가하지만 품질 크게 향상
- 우선순위 판정 같은 중요 작업에 필수

---

### 3. Structured Output (구조화된 출력)
**토큰 절약 + 파싱 안정성**

**JSON Schema 사용:**
```python
from pydantic import BaseModel

class QaVerdict(BaseModel):
    decision: str  # P0/P1/P2/P3
    score: int  # 0-100
    reason: str  # 1문장

config = types.GenerateContentConfig(
    response_schema=QaVerdict,
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
페이지: {page}입니다.
이슈 유형은 {issue_type}입니다.
콘솔 오류는 {console_errors}건입니다.
영향받는 탭 수는 {affected_tabs}개입니다.
심각도를 판정해주세요.
"""
```

**적용 후:**
```python
# 구분자 기반 압축
prompt = f"{page}:{issue_type}|오류:{console_errors}|영향탭:{affected_tabs}→판정?"
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
# 봄이 QA 매뉴얼
## 1. 소개
봄이는 펫나 앱의 QA 담당으로서...
## 2. 점검 항목
콘솔 오류를 분석할 때는...
[30KB의 상세 설명]
```

**적용 후 (500 토큰):**
```markdown
QA 판정 AI 봄이. 규칙:
1. 콘솔 오류 → P1
2. 회귀 발생 → P1
3. 접근성 위반만 → P3
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
prompt = f"{all_console_logs}{all_screenshots}{all_history}..."
```

**적용 후:**
```python
# 상황에 따라 필요한 것만
if decision_type == "quick":
    prompt = f"{page}:{issue_type}|오류:{console_errors}"
elif decision_type == "detailed":
    prompt = f"{page}:{issue_type}|{all_console_logs}|{recent_history}"
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
# 정확한 분석/판단 (QA 심각도, 데이터 분석)
temperature = 0.1  # 가장 확률 높은 답만

# 일반 대화 (영숙 챗봇)
temperature = 0.7  # 자연스러운 대화

# 창작 (제목, 카피)
temperature = 0.9  # 다양한 아이디어
```

**효과:**
- 낮은 temperature = 짧고 결정론적
- 토큰 10-20% 감소
- 판정 작업은 반드시 0.1 사용

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

## 📊 종합 적용 예시 (봄이 QA)

### Before (12,000 토큰)
```python
system = load_full_manual()  # 8,000 토큰
prompt = f"""
현재 QA 점검 정보:
페이지: {page}
이슈유형: {issue_type}
[장황한 설명... 2,000 토큰]
"""
max_output_tokens = 2000
temperature = 0.7
```

### After (1,500 토큰)
```python
# 1. System: 핵심만 (500 토큰)
system = """QA 판정 AI. 규칙 7개 + 예시 2개"""

# 2. Prompt: 압축 (200 토큰)
prompt = f"{page}:{issue_type}|오류:{console_errors}|영향탭:{affected_tabs}"

# 3. Output: 제한 (500 토큰)
max_output_tokens = 500

# 4. Temperature: 낮춤
temperature = 0.1

# 5. Structured Output
response_schema = QaVerdict
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
