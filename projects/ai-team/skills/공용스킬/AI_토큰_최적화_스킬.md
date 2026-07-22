# AI 토큰 최적화 스킬

## 📋 스킬 개요
모든 에이전트가 AI API(GPT, Gemini, Claude) 호출 시 토큰을 최소화하면서도 품질을 유지/향상시키는 실전 스킬.

---

## ✅ 적용 대상
- **필수**: 영숙, 예원 (API 사용 에이전트)
- **권장**: 봄이·수리·테오·백호·미오·나무 포함 모든 에이전트

---

## 🎯 핵심 기법

### 1. System Instruction 압축 (94% 토큰 절감)

#### ❌ 잘못된 예
```python
system = """
# 봄이 QA 순찰 완전 가이드

## 소개
봄이는 펫나 앱의 QA 담당으로서 다음과 같은 역할을 수행합니다:
- 콘솔·JS 오류, 404, 깨진 이미지 탐지
- 접근성·가로스크롤·SEO 점검
[30KB의 장황한 설명...]
"""
```

#### ✅ 올바른 예
```python
system = """QA 순찰 AI. 규칙:
1. 콘솔 오류 발견→P1 즉시 알림
2. 404/깨진 이미지→P2
3. 접근성 위반→P3
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
반려동물의 최근 체중은 {weight}kg입니다.
활동량 추세는 {activity_trend}입니다.
배변 이상 징후 점수는 {stool_score}입니다.
식욕 변화율은 {appetite_pct}%입니다.
"""
```

#### ✅ 올바른 예
```python
# 구분자 압축
prompt = f"체중:{weight}kg|활동:{activity_trend}|배변:{stool_score}|식욕:{appetite_pct}%"

# 또는 JSON 압축
prompt = json.dumps({"w":weight,"a":activity_trend,"s":stool_score,"f":appetite_pct})
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
1. 증상 패턴은? (급성/만성/정상범위)
2. 최근 변화 추세는? (악화/개선/유지)
3. 위험도는? (높음/보통/낮음)
4. 결론: 병원 상담 권고/경과 관찰/정상 + 이유"""
```

**적용 대상:**
- 중요한 판단 (건강 분석, 법률)
- 복잡한 분석
- 다단계 추론 필요 시

---

## 📊 통합 적용 템플릿 (2026-06-17 최신)

### 봄이 QA 심각도 판정 예시 (점수 기반)

```python
def analyze_qa_issue(page, issue_type, signals):
    # 1. 공통 + 특화 프롬프트 조합
    common = get_common_judge_prompt()  # 공통 원칙 ~500 토큰
    bomi_specific = """
--- 봄이 특화 ---
성향: 사용자 영향 기반 보수적 판단
점수 → 판단:
60~100: P0/P1 즉시 알림
40~59: P2 백로그 적재
0~39: P3 관찰"""
    
    system = common + bomi_specific  # 총 ~700 토큰
    
    # 2. 점수 계산 (코드가 수행)
    scores = calculate_severity_score(signals, issue_type)
    
    # 3. 간소화된 입력 (LLM은 판단만)
    prompt = f"""페이지: {page}
이슈유형: {issue_type}
총점: {scores['total']}/100
콘솔오류: {signals['console_errors']}
영향탭수: {signals['affected_tabs']}
회귀여부: {signals['is_regression']}
최근발견: {recent_occurrences}회"""
    
    # 4. API 호출: 구조화 + 토큰 제한
    response = lm_chat(
        prompt=prompt,
        system=system,
        max_tokens=300,  # 기존 500 → 300
        temperature=0.1,
        json_mode=True
    )
    
    return response

def calculate_severity_score(signals: dict, issue_type: str) -> dict:
    """점수 계산은 코드가 수행 (일관성 보장)"""
    score = 0
    if signals.get('console_errors'): score += 20
    if signals.get('is_regression'): score += 20
    if signals.get('affected_tabs', 0) >= 2: score += 20
    # ...
    return {'total': score, 'console': 20, 'regression': 20, ...}
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

### 봄이/백호 (공통 구조)
```python
# 공통 프롬프트 함수
def get_common_judge_prompt():
    """공통 원칙 ~500 토큰"""
    return """너는 펫나 QA 최종 판단 AI다.
목표는 제한된 토큰으로 정확한 심각도 판정을 반복하는 것이다.
...
출력 JSON:
{"verdict":"p0|p1|p2|p3","score":0-100,"confidence":0-100,"reason":"40자이내"}"""

# 봄이 특화 (프론트 QA)
def load_bomi_prompt():
    common = get_common_judge_prompt()
    return common + """
--- 봄이 특화 ---
성향: 사용자 영향 기반 보수적
60↑ 즉시 알림 / 40↑ 백로그 적재 / 미만 관찰
..."""

# 백호 특화 (백엔드 계약 감사)
def load_baekho_prompt():
    common = get_common_judge_prompt()
    return common + """
--- 백호 특화 ---
대상: Supabase 스키마·RLS vs 프론트 쿼리 계약
읽기 전용, json_mode 고정 (하네스 가드레일)
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
