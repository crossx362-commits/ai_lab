# AI 모델 전략 가이드
**작성일**: 2026-06-02  
**목적**: AI Team 프로젝트의 AI 모델 선택 및 폴백 전략 설명

---

## 🎯 전략 개요

AI Team은 **2단계 폴백 전략**을 사용합니다:

```
1순위: Ollama (로컬, 무료) → 2순위: Gemini API (클라우드, 유료)
```

### 핵심 원칙
1. **비용 최소화**: 로컬 Ollama를 최대한 활용
2. **안정성 보장**: Ollama 실패 시 Gemini로 자동 폴백
3. **작업별 최적화**: 코딩/블로그 등 task별 전문 모델 자동 선택

---

## 🏗️ 아키텍처

### 핵심 모듈 구조

```
_shared/
├── ollama_client.py     # Ollama 로컬 AI (1순위)
└── gemini_client.py     # Gemini API + Ollama 통합 (2순위)
```

### 계층 구조

```
에이전트 스크립트
    ↓
gemini_client.text()  (lm_first=True)
    ↓
1순위: ollama_client.chat()
    ├─ 성공 → 결과 반환
    └─ 실패 → Gemini API로 폴백
    ↓
2순위: Gemini API 호출
```

---

## 📚 Ollama 모델 선택 로직

### 자동 감지 시스템 (`ollama_client.py`)

```python
def _detect_model(task: str = "") -> str | None:
    """
    1. OLLAMA_MODEL 환경변수 확인 → 있으면 강제 사용
    2. Ollama 서버에서 로드된 모델 목록 조회
    3. task에 따라 최적 모델 선택
    4. 60초 캐시 (성능 최적화)
    """
```

### Task별 모델 우선순위

| Task | 우선 키워드 | 예시 모델 | 용도 |
|------|------------|----------|------|
| `"coding"` | deepseek, code, coder, codestral | `deepseek-coder:latest` | 코드 생성·리뷰·디버깅 |
| `"blog"` | qwen (deepseek 제외) | `qwen2.5:latest` | 블로그 글·캡션·설명 작성 |
| `""` (빈 문자열) | — | 첫 번째 로드된 모델 | 일반 텍스트 생성 |

### 모델 선택 알고리즘

```python
def _pick_model(models: list, task: str) -> str | None:
    if task == "coding":
        # deepseek/code/coder/codestral 중 하나라도 포함된 모델 우선
        preferred = [m for m in models 
                     if any(k in m.lower() for k in keywords)]
        if preferred:
            return preferred[0]
    
    elif task == "blog":
        # qwen 포함 + deepseek 제외
        preferred = [m for m in models
                     if "qwen" in m.lower() and "deepseek" not in m.lower()]
        if preferred:
            return preferred[0]
    
    # 매칭 실패 시 첫 번째 모델
    return models[0]
```

---

## 🔄 Gemini API 폴백 전략

### 자동 폴백 조건 (`gemini_client.py`)

```python
def text(prompt: str, lm_first: bool = True, ...):
    # 1순위: Ollama
    if lm_first:
        try:
            if ollama_available():
                result = ollama_chat(...)
                if result:
                    return result  # 성공 시 즉시 반환
        except Exception:
            pass  # 조용히 실패 → 폴백
    
    # 2순위: Gemini API
    return gemini_api_call(...)
```

### Gemini 모델 종류

| 용도 | 모델명 | 설정 위치 |
|------|--------|----------|
| 텍스트 생성 | `gemini-2.5-flash` | `gemini_client.py:15` |
| 이미지 생성 | `gemini-3.1-flash-image-preview` | `gemini_client.py:16` |
| Vision (이미지→텍스트) | `gemini-2.5-flash` | `gemini_client.py:146` |

---

## 🤖 에이전트별 모델 사용 현황

### 명시적 task 지정 에이전트

| 에이전트 | task 값 | 이유 | 사용 위치 |
|---------|---------|------|----------|
| **코다리** (개발자) | `"coding"` | 코드 생성·린트·Mermaid 다이어그램 | SKILL.md 명시 |
| *(미래)* 블로그 에이전트 | `"blog"` | 블로그 글 작성 | 예약됨 |

**코다리 SKILL.md**:
```markdown
- **AI 모델**: Ollama DeepSeek 전용 (task="coding"). 
  DeepSeek 미로드 시 Gemini API 사용.
```

**실제 사용 예시**:
```python
# lint_test.py:151
_gc.text(prompt, max_tokens=600, task="coding")

# mermaid_generator.py:194
lm_chat(prompt, task="coding", max_tokens=1200, temperature=0.2)

# ollama_health_check.py:93
lm_chat(_TEST_PROMPT, task="coding", max_tokens=60)
```

### 일반 task 에이전트 (자동 선택)

대부분의 에이전트는 `task=""`로 호출 → 첫 번째 로드된 모델 자동 사용:
- 루나 (디렉터) — 트렌드 분석, 제목 생성
- 아린 (관리자) — 프롬프트 생성, 캡션 작성
- 현빈 (전략가) — 비즈니스 리서치
- 가희 (검수관) — 콘텐츠 검수
- 티모 (디자이너) — UI/UX 검토

---

## ⚙️ 환경변수 설정

### Ollama 설정

```bash
# 선택 1: 자동 감지 (권장)
# → Ollama에 로드된 모델 자동 선택

# 선택 2: 강제 모델 지정
OLLAMA_MODEL=deepseek-coder:latest

# 선택 3: 커스텀 엔드포인트
OLLAMA_URL=http://localhost:11434/v1/chat/completions
```

### Gemini 설정 (필수)

```bash
GEMINI_API_KEY=your_api_key_here
```

---

## 🔧 실전 사용 가이드

### 1. 텍스트 생성 (기본)

```python
from _shared import gemini_client as _gc

# 자동 폴백 (Ollama → Gemini)
result = _gc.text(
    prompt="시티팝 음악 제목 10개 생성",
    lm_first=True,  # Ollama 우선 시도 (기본값)
)
```

### 2. 코딩 작업 (DeepSeek 우선)

```python
from _shared import gemini_client as _gc

# task="coding" → DeepSeek 계열 자동 선택
code = _gc.text(
    prompt="Python 함수 작성: 파일 경로 검증",
    task="coding",
    temperature=0.2,  # 코드는 낮은 temperature 권장
)
```

### 3. 블로그 글 작성 (Qwen 우선)

```python
from _shared import gemini_client as _gc

# task="blog" → Qwen 계열 자동 선택
article = _gc.text(
    prompt="AI 음악 생성 기술 블로그 글 작성",
    task="blog",
    max_tokens=3000,
)
```

### 4. JSON 응답 강제

```python
from _shared import gemini_client as _gc

result = _gc.text(
    prompt="다음 데이터를 JSON으로 구조화: ...",
    json_mode=True,  # JSON만 반환하도록 시스템 프롬프트 추가
)
```

### 5. Gemini만 사용 (Ollama 건너뛰기)

```python
from _shared import gemini_client as _gc

# lm_first=False → Ollama 시도 안 함
result = _gc.text(
    prompt="프롬프트",
    lm_first=False,
)
```

### 6. 이미지 생성 (Gemini 전용)

```python
from _shared import gemini_client as _gc

# Ollama는 이미지 생성 미지원 → Gemini 직접 호출
img_bytes = _gc.image(
    prompt="80s Japanese city pop album cover, neon lights"
)
```

### 7. Vision (이미지 분석)

```python
from _shared import gemini_client as _gc

caption = _gc.vision(
    img_bytes=image_data,
    prompt="이 이미지를 한국어로 감성적으로 설명해줘",
    max_tokens=800,
)
```

---

## 🎯 Task 선택 가이드

| 작업 유형 | 추천 task | 이유 |
|----------|----------|------|
| Python/JavaScript 코드 작성 | `"coding"` | DeepSeek의 코드 이해도 우수 |
| 디버깅·코드 리뷰 | `"coding"` | 구문 오류 탐지 정확 |
| Mermaid 다이어그램 | `"coding"` | 구조화된 텍스트 생성 |
| 블로그 글·에세이 | `"blog"` | Qwen의 자연스러운 글쓰기 |
| 인스타그램 캡션 | `""` (자동) | 일반 창작 작업 |
| YouTube 설명·태그 | `""` (자동) | 일반 창작 작업 |
| 비즈니스 분석 | `""` (자동) | 일반 분석 작업 |
| JSON 데이터 생성 | `"coding"` | 구조화된 출력 정확도 |

---

## 🛠️ 캐싱 및 성능 최적화

### Ollama 모델 캐시

```python
_cache: dict = {}  # {task: (model_id, timestamp)}
_CACHE_TTL = 60    # 60초
```

**동작**:
- 모델 목록 조회는 비용이 높음 (HTTP 요청)
- 60초 동안 같은 task는 캐시된 모델 재사용
- 1분 후 자동 만료 → 새 모델 로드 시 즉시 반영

---

## 🚨 에러 핸들링

### Ollama 연결 실패

```python
# ollama_client.py
def is_available() -> bool:
    """Ollama 서버가 응답하고 모델이 로드돼 있는지 확인."""
    return bool(_list_models())
```

**폴백 시나리오**:
1. Ollama 서버 다운 → Gemini 자동 사용
2. 모델 미로드 → 콘솔 경고 + Gemini 사용
3. 응답 타임아웃 (300초) → Gemini 폴백

### Gemini API 실패

```python
# gemini_client.py
except Exception as e:
    print(f"  [Gemini 텍스트] 실패: {e}")
    return None
```

**에러 타입**:
- API 키 없음 → `None` 반환
- Rate Limit (429) → 이미지 생성 시 백오프 재시도 (30초 → 60초)
- 네트워크 오류 → `None` 반환

---

## 📊 모델 성능 모니터링

### 로그 출력 패턴

```
✅ Ollama 사용:
  [Ollama[coding]] 자동 감지: deepseek-coder:latest
  [로컬 AI → deepseek-coder:latest]

✅ Gemini 폴백:
  [Gemini API → gemini-2.5-flash]

❌ Ollama 미설치:
  [Ollama] 로드된 모델 없음 — `ollama pull <모델명>` 으로 설치하세요
```

### 코다리 헬스체크 (2시간 주기)

```python
# ollama_health_check.py
# 1. 프로세스 확인
# 2. API 응답 확인
# 3. 테스트 프롬프트 품질 검증
```

---

## 🔐 보안 고려사항

### API 키 관리

```python
# ✅ 올바른 방법
api_key = os.getenv("GEMINI_API_KEY", "")

# ❌ 절대 금지
api_key = "AIzaSy..."  # 하드코딩 금지
```

### 민감 정보 차단

- `.env` 파일은 `.gitignore`에 포함
- 로그에 API 키 출력 금지
- 텔레그램 알림에 키 포함 금지

---

## 📈 비용 관리 전략

### Ollama 우선 사용 → 비용 0

대부분의 작업은 Ollama로 처리:
- 트렌드 분석, 제목 생성, 캡션 작성
- 비즈니스 리서치, 콘텐츠 검수
- 일반 텍스트 생성

### Gemini 사용 최소화

Gemini API는 다음 상황에서만 사용:
1. Ollama 서버 다운/모델 미로드 (폴백)
2. 이미지 생성 (Ollama 미지원)
3. Vision 분석 (Ollama 미지원)
4. `lm_first=False` 명시 (의도적)

**예상 비용**:
- Ollama 작업: 무료
- Gemini 폴백 (월 10-20회): ~$1-2
- Gemini 이미지 생성 (일 5개): ~$5-10/월

---

## 🧪 테스트 및 검증

### Ollama 연결 테스트

```bash
cd d:\ai_lab\projects\ai-team
python -c "from _shared import ollama_client; print(ollama_client.is_available())"
```

### Gemini API 테스트

```bash
python -c "from _shared import gemini_client as gc; print(gc.text('안녕', lm_first=False))"
```

### Task 모델 확인

```bash
python -c "from _shared.ollama_client import _detect_model; print('Coding:', _detect_model('coding')); print('Blog:', _detect_model('blog'))"
```

---

## 🔮 향후 확장 계획

### 계획 중인 기능

1. **thinking 모델 지원** (이미 구현됨)
   - `ollama_client.py:124-143` — reasoning 토큰 추출

2. **Multi-modal Ollama** (LLaVA 등)
   - Vision 기능을 Ollama로 대체 → Gemini 비용 절감

3. **모델 성능 벤치마크**
   - task별 응답 품질 자동 평가
   - 최적 모델 자동 추천

4. **동적 폴백 설정**
   - 시간대별 전략 (심야: Ollama만, 낮: Gemini 허용)
   - 비용 한도 자동 제어

---

## 📚 참고 자료

### 공식 문서
- [Ollama 모델 라이브러리](https://ollama.com/library)
- [Gemini API 문서](https://ai.google.dev/docs)
- [DeepSeek Coder](https://github.com/deepseek-ai/DeepSeek-Coder)
- [Qwen 2.5](https://github.com/QwenLM/Qwen2.5)

### 프로젝트 내 관련 파일
- [_shared/ollama_client.py](../\_shared/ollama_client.py)
- [_shared/gemini_client.py](../\_shared/gemini_client.py)
- [코다리 SKILL.md](../skills/코다리_개발자/SKILL.md)
- [AGENT_AUDIT_REPORT.md](../AGENT_AUDIT_REPORT.md)

---

**마지막 업데이트**: 2026-06-02  
**작성자**: AI Team 검수 시스템
