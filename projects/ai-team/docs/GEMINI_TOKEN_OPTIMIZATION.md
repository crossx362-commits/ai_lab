# Gemini API 토큰 절약 가이드

## 🎯 현재 문제점
- **비대한 system instruction**: 31KB (약 8,000 토큰)
- **매 API 호출마다 전송**: 입력 토큰 낭비
- **Context Caching 시도**: 32,768 토큰 padding → 더 많은 토큰 소비
- **Free tier 제한**: gemini-2.5-flash 하루 20회만 가능

## ✅ 해결 방법

### 1. System Instruction 최소화
**적용 전:**
```python
# 31KB 전체 로드
system_instruction = load_system_instruction()  # SKILL.md + 전체 참고자료
```

**적용 후:**
```python
# 핵심만 요약 (2-3KB)
system_instruction = """
너는 QA 판정 AI 봄이. 
- 콘솔 오류 즉시 P1 처리
- 회귀 발생 여부 우선 확인
- 접근성 위반 패턴 간파
- 최종 결정: P0/P1/P2/P3 + 확신도 0-100%
"""
```

### 2. Prompt 압축
**적용 전:**
```python
prompt = f"""
현재 페이지: {page}
이슈 유형: {issue_type}
상세 신호: {json.dumps(signals, indent=2, ensure_ascii=False)}
영향받는 탭 상위 3개: {json.dumps(affected_tabs, indent=2)}
... (장황한 설명)
"""
```

**적용 후:**
```python
# JSON 압축, 불필요한 설명 제거
prompt = f"{page}|{issue_type}|오류:{signals['console_errors']}|탭:{signals['affected_tabs']}"
```

### 3. max_output_tokens 제한
**적용 전:**
```python
max_output_tokens=2000  # 기본값
```

**적용 후:**
```python
max_output_tokens=500  # JSON 응답은 500토큰이면 충분
```

### 4. Context Caching 제거
**문제점:**
- 32,768 토큰 padding = 엄청난 입력 토큰
- Free tier에서는 오히려 손해
- 캐시 TTL 1시간마다 재생성 필요

**해결:**
```python
# Context Caching 완전 비활성화
# get_agent_context_cache() 함수 사용 중단
```

### 5. Structured Output 활용
**JSON mode 대신 Pydantic schema 사용:**
```python
config = types.GenerateContentConfig(
    response_schema=QaVerdict,  # Pydantic 모델
    response_mime_type="application/json"
)
# → LLM이 정확한 형식만 출력하므로 토큰 절약
```

### 6. Temperature 낮추기
```python
temperature=0.1  # 0.7 → 0.1로 변경
# 더 결정론적이고 짧은 응답
```

## 📊 예상 절감 효과

| 항목 | 적용 전 | 적용 후 | 절감률 |
|------|---------|---------|--------|
| System Instruction | 8,000 토큰 | 500 토큰 | 94% ↓ |
| Prompt | 2,000 토큰 | 800 토큰 | 60% ↓ |
| Output | 2,000 토큰 | 500 토큰 | 75% ↓ |
| **총 토큰/호출** | **12,000** | **1,800** | **85% ↓** |
| **일일 가능 호출 수** | **83회** | **555회** | **6.7배 ↑** |

*(Free tier 1M 토큰/일 기준)*

## 🔧 우선 적용 순서
1. ✅ Context Caching 제거 (즉시)
2. ✅ System Instruction 요약 (즉시)
3. ✅ max_output_tokens=500 제한 (즉시)
4. ⬜ Prompt 압축 (점진적)
5. ⬜ Temperature 조정 (테스트 필요)
