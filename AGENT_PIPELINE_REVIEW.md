# AI Lab 전체 에이전트 파이프라인 검토 보고서

생성일: 2026-06-02  
작성자: Claude Code  
버전: 1.0

---

## 📋 목차

1. [전체 에이전트 개요](#전체-에이전트-개요)
2. [아린 관리자 파이프라인](#아린-관리자-파이프라인)
3. [루나 디렉터 파이프라인](#루나-디렉터-파이프라인)
4. [환경변수 통합 현황](#환경변수-통합-현황)
5. [파이프라인 품질 평가](#파이프라인-품질-평가)
6. [개선 권장사항](#개선-권장사항)

---

## 전체 에이전트 개요

### 에이전트 구성 (12개)

| 에이전트 | 역할 | 주요 파이프라인 | 환경변수 의존성 |
|---------|------|----------------|----------------|
| **영숙 비서** | Telegram 인터페이스 | 텔레그램 봇, 일정 관리 | TELEGRAM_BOT_TOKEN, GEMINI_API_KEY, NOTION_API_KEY |
| **예원 CEO** | 에이전트 분배 | 업무 분배, 스킬 감시 | GEMINI_API_KEY, TELEGRAM_BOT_TOKEN |
| **루나 디렉터** | YouTube 뮤직비디오 | 음악 생성, 영상 합성, YouTube 업로드 | GEMINI_API_KEY, YOUTUBE_API_KEY |
| **아린 관리자** | Instagram 포스팅 | 트렌드 분석, 이미지 생성, Instagram 업로드 | GEMINI_API_KEY, INSTAGRAM_* (4개) |
| **코다리 개발자** | 웹 개발, 시스템 관리 | 웹 미리보기, 헬스체크 | TELEGRAM_BOT_TOKEN, INSTAGRAM_* (3개) |
| **케빈 인프라** | 클라우드 관리 | Vercel 배포, Supabase 관리, 환경변수 검증 | VERCEL_*, SUPABASE_*, 전체 검증 |
| **티모 디자이너** | UI/UX 검수 | Petnna 웹 서비스 검수 | GEMINI_API_KEY |
| **가희 검수관** | 콘텐츠 품질 검수 | 캡션 검수, 업로드 후 검수 | GEMINI_API_KEY |
| **현빈 전략가** | 비즈니스 분석 | 비즈니스 리서치, 매출 분석 | GEMINI_API_KEY |
| **로율 변호사** | 법률/세무 상담 | 세무 시뮬레이션 | GEMINI_API_KEY |
| **경수 수사관** | 악플 감시 | 댓글 분석 | GEMINI_API_KEY, INSTAGRAM_* (2개) |

---

## 아린 관리자 파이프라인

### 📁 파일 구조
```
projects/ai-team/skills/아린_관리자/tools/
├── auto_pipeline.py        # 메인 자동화 파이프라인
├── uploader.py              # Instagram 업로더
├── image_research.py        # 이미지 리서치
└── prompt_crafter.py        # 프롬프트 최적화
```

### 🔄 파이프라인 흐름 (auto_pipeline.py)

#### 1단계: 트렌드 수집 및 선정
- **트렌드 소스**: Google Trends (KR/US/JP) + 카테고리 큐레이션
- **필터링**: 최근 7일 사용 트렌드 제외, 금지 키워드 제외
- **결과**: 20+ 개 후보 중 1개 선정

#### 2단계: 키워드 추출
- **방법**: Ollama (우선) → 규칙 기반 (폴백)
- **추출 정보**: 무드, 장면, 색감, 피사체, 스타일, 계절, 토픽

#### 3단계: 콘텐츠 생성
- **이미지 프롬프트**: Ollama로 150자 이내 영어 프롬프트 생성
- **캡션**: 제목 + 설명 + 해시태그 (한국어, 자연스러운 말투)
- **최적 업로드 시간**: 요일별 분석 (주말 13:45~16:00, 평일 11:30~19:15)

#### 4단계: 이미지 생성
- **주 엔진**: Gemini 3.1 Flash Image Preview
- **폴백**: Pollinations.ai
- **업로드**: Catbox.moe (퍼블릭 URL)

#### 5단계: 프롬프트 크래프팅
- **도구**: prompt_crafter.py
- **유사 이미지 검출**: 최근 14일 프롬프트와 60% 이상 유사도 체크
- **변형**: 유사 시 계절/시간대/구도 접두어 자동 추가

#### 6단계: Gemini Vision 캡션 생성
- **방법**: 생성된 이미지를 Gemini Vision으로 분석
- **스타일**: 진짜 사람이 쓴 것처럼 짧고 감성적 (1~2문장 + 이모지 + 해시태그)
- **금지 문구 필터**: AI 생성, 인공지능, 미래 등 차단

#### 7단계: 중복 검증
- **캡션 중복 체크**: 70% 이상 유사도 감지 시 Ollama로 재생성
- **해시태그 중복 체크**: 80% 이상 겹침 시 일부 교체

#### 8단계: 가희 검수 + 업로드 루프
- **사전 검수**: 캡션 금지 문구 확인
- **업로드**: Instagram Graph API
- **사후 검수**: 업로드된 포스팅 메타데이터 확인
- **재시도**: 최대 3회, 실패 시 Ollama로 캡션 수정 후 재업

#### 9단계: 이력 기록 및 Git 동기화
- **히스토리**: reports/history/upload_history.json
- **Git 동기화**: 자동 커밋 및 푸시

### 🔑 환경변수 사용

| 변수 | 용도 | 필수 여부 |
|------|------|---------|
| `GEMINI_API_KEY` | 이미지 생성, Vision 캡션 | 필수 |
| `INSTAGRAM_APP_ID` | Facebook App 인증 | 필수 |
| `INSTAGRAM_APP_SECRET` | Facebook App 시크릿 | 필수 |
| `INSTAGRAM_ACCESS_TOKEN` | Instagram API 인증 (자동 갱신) | 필수 |
| `INSTAGRAM_ACCOUNT_ID` | Instagram Business 계정 ID | 필수 |

### 💡 핵심 기능

1. **자동 토큰 갱신**: `ensure_token_fresh()` - 60일 만료 전 자동 갱신
2. **중복 방지 시스템**: 
   - 트렌드 중복 (7일)
   - 이미지 프롬프트 유사도 (60%)
   - 캡션 유사도 (70%)
   - 해시태그 겹침 (80%)
3. **품질 관리**: 가희 검수관 통합 (사전 + 사후)
4. **폴백 체계**: Ollama → Gemini → Template 순차 폴백
5. **캘린더 통합**: .ics 파일 자동 업데이트

### ⚠️ 발견된 이슈

1. **환경변수 로드**: `load_env()` 이미 적용됨 ✅
2. **금지 키워드**: `_BANNED_PHRASES`, `_BANNED_TOPICS` 하드코딩 (개선 가능)
3. **중복 검증 로직**: `_shared/duplicate_guard.py`에 위임 ✅

---

## 루나 디렉터 파이프라인

### 📁 파일 구조
```
projects/ai-team/skills/루나_디렉터/tools/
├── music_video_pipeline.py         # 메인 뮤직비디오 파이프라인
├── lyria_music_gen.py               # Lyria 음악 생성
├── veo_video_maker.py               # Veo 영상 생성
├── shorts_pipeline.py               # 쇼츠 파이프라인
└── src/
    ├── trend_analyzer.py            # 트렌드 분석 + 제목 생성
    ├── lyria_music_generator.py     # Lyria API 래퍼
    ├── video_generator.py           # 영상 합성 (Ken Burns)
    ├── youtube_uploader.py          # YouTube OAuth 업로더
    ├── youtube_research.py          # YouTube 트렌드 학습
    ├── fallback_generators.py       # 폴백 생성기
    └── youtube_audit.py             # 업로드 후 감사
```

### 🔄 파이프라인 흐름 (music_video_pipeline.py)

#### 0단계: 체크포인트 복원
- **파일**: `output/music_video_checkpoint.json`
- **만료**: 36시간 (초과 시 삭제)
- **목적**: 중단된 작업 재개, 중복 실행 방지

#### 1단계: 테마 선정 + 제목 생성
- **방법**: `TrendAnalyzer.select_best_theme()`
- **금지 장르 필터**: lofi, study beats, chill beats 등 차단
- **재시도**: 최대 5회
- **제목 최적화**: 키워드 → 유튜브 트렌드 기반 제목 생성

#### 2단계: 음악 프롬프트 생성
- **방법**: `generate_music_prompt_from_title()` - 5단 템플릿
- **구성**: intro → verse → chorus → bridge → outro
- **길이**: 2분 이상 완곡 (clip 금지)

#### 3단계: Lyria 3 Pro 완곡 생성
- **엔진**: Lyria 3 Pro Preview (Gemini API)
- **폴백**: Pollinations.ai 음악 생성
- **길이 사전 검수**: 120초 미만 시 파이프라인 중단 (리소스 절약)

#### 4단계: 5단 비주얼 생성
- **방법**: 각 파트별 이미지 생성 (intro/verse/chorus/bridge/outro)
- **주 엔진**: Gemini 2.5 Flash Image
- **폴백**: Pollinations.ai
- **비디오 변환**: 
  - 광고(AD) 콘텐츠: Veo Video (우선)
  - 일반 콘텐츠: Ken Burns Effect (ffmpeg)
  - 최종 폴백: 단순 슬라이드쇼

#### 5단계: 비주얼 + 오디오 합성
- **해상도**: 1280x720 (16:9, 쇼츠 방지)
- **병합**: ffmpeg concat (모든 파트)
- **오디오 합성**: 비주얼 길이에 맞춰 음악 루핑
- **리소스 관리**: `wait_for_resources()` - 무거운 렌더링 전 시스템 자원 체크

#### 6단계: 메타데이터 자동 생성
- **방법**: Ollama로 음악 프롬프트 + YouTube 트렌드 분석
- **생성 항목**:
  - **제목**: 트렌드 기반, LUNA·Official·MV 등 고정 태그 금지
  - **설명**: 곡 분위기·감성·스토리 2~3문장 + 추천상황 + 해시태그 8개+
  - **태그**: 최소 20개, lofi 금지, 시티팝/citypop/LUNA/루나 필수
- **폴백**: 템플릿 메타데이터

#### 7단계: 가희 사전 검수
- **제목 검수**: 금지 키워드 (`_BANNED_TITLE_WORDS`) 체크
- **길이 검수**: 120초 미만 차단

#### 8단계: YouTube 예약 업로드
- **썸네일**: 영상 5초 지점 추출 + PIL 채도/대비 보정
- **중복 체크**: 채널 내 동일 길이(±2초) 영상 존재 시 차단
- **예약 시간**: 기본 KST 19:00 (커스텀 가능)
- **플레이리스트**: 자동 추가

#### 9단계: 가희 사후 검수
- **방법**: 업로드된 영상 메타데이터 확인
- **자동 수정**: 실패 시 Ollama로 메타데이터 재생성 후 YouTube API 업데이트
- **재검수**: 수정 후 재확인

#### 10단계: 이력 기록
- **위치**: `.agent/memory/upload_history.json`
- **내용**: 에이전트, 상태, 업로드 시간, 메타데이터 (플랫폼, 비디오 ID, 제목, 프롬프트, 파일명)

### 🔑 환경변수 사용

| 변수 | 용도 | 필수 여부 |
|------|------|---------|
| `GEMINI_API_KEY` | 이미지 생성, 음악 생성 (Lyria), Vision | 필수 |
| `YOUTUBE_API_KEY` | YouTube 트렌드 분석, 메타데이터 조회 | 선택 |

**참고**: YouTube 업로드는 OAuth 2.0 (pickle 파일 캐싱) 사용, API 키 불필요

### 💡 핵심 기능

1. **체크포인트 시스템**: 중단된 작업 재개 (36시간 유효)
2. **리소스 관리**: `wait_for_resources()` - CPU/메모리 과부하 방지
3. **다단 폴백**:
   - 음악: Lyria → Pollinations → 무음
   - 이미지: Gemini → Pollinations
   - 비디오: Veo → Ken Burns → 슬라이드쇼
4. **품질 관리**:
   - 사전 길이 검수 (120초 미만 조기 차단)
   - 금지 장르 필터 (5회 재시도)
   - 가희 사전/사후 검수
   - 중복 영상 차단 (길이 기반)
5. **자동화**:
   - 메타데이터 자동 생성 (Ollama)
   - 썸네일 자동 추출 및 보정
   - 플레이리스트 자동 추가

### ⚠️ 발견된 이슈

1. **환경변수 로드**: `load_env()` 이미 적용됨 ✅
2. **ffmpeg 경로**: Windows 하드코딩 (`C:\Users\cross\...\ffmpeg.exe`) - 개선 가능
3. **금지 장르**: `_BANNED_GENRES`, `_BANNED_TITLE_WORDS` 하드코딩 - 환경변수화 권장
4. **체크포인트 만료**: 36시간 하드코딩 - 설정 파일화 권장

---

## 환경변수 통합 현황

### ✅ 완료된 작업

1. **환경변수 스캔**: `scan_env_usage.py` 작성 완료
2. **ENV_MANIFEST.json**: 자동 생성 완료
   - 총 12개 환경변수 감지
   - 12개 파일에서 사용
   - 8개 파일이 `load_env()` 적용
   - 4개 파일이 미적용
3. **env_config.py**: 메타정보 정의 완료
   - 17개 환경변수 정의
   - 필수/선택 분류
   - 검증 규칙 추가
   - 에이전트별 매핑
4. **env_loader.py**: `validate_env()`, `get_env_with_fallback()` 함수 추가
5. **.env 파일**: Notion API 정보 추가, 주석 개선
6. **ENV_README.md**: Notion 섹션 추가

### 📊 환경변수 사용 현황

| 환경변수 | 사용 횟수 | 사용 에이전트 |
|---------|----------|--------------|
| `INSTAGRAM_ACCOUNT_ID` | 3회 | 경수_수사관, 아린_관리자 |
| `GEMINI_API_KEY` | 3회 | 루나_디렉터 |
| `INSTAGRAM_ACCESS_TOKEN` | 2회 | 경수_수사관, 아린_관리자 |
| `TELEGRAM_BOT_TOKEN` | 1회 | 기타 (scripts) |
| `TELEGRAM_CHAT_ID` | 1회 | 기타 (scripts) |
| `SUPPRESS_TELEGRAM` | 1회 | _shared (공용) |
| `GEMINI_API_KEYS` | 1회 | 루나_디렉터 |
| `VERCEL_TOKEN` | 1회 | 케빈_인프라 |
| `VERCEL_TEAM_ID` | 1회 | 케빈_인프라 |
| `BRAIN_ROOT` | 1회 | 코다리_개발자 |
| `OUTPUT` | 1회 | 현빈_전략가 |

### ⚠️ load_env() 미적용 파일 (4개)

1. `projects\ai-team\_shared\telegram_notifier.py` - `SUPPRESS_TELEGRAM`
2. `projects\ai-team\skills\루나_디렉터\tools\src\lyria_music_generator.py` - `GEMINI_API_KEY`
3. `projects\ai-team\skills\코다리_개발자\tools\pack_apply.py` - `BRAIN_ROOT`
4. `projects\ai-team\skills\현빈_전략가\tools\paypal_revenue.py` - `OUTPUT`

---

## 파이프라인 품질 평가

### ⭐ 우수한 점

#### 아린 관리자
1. **중복 방지 시스템**: 트렌드, 이미지, 캡션, 해시태그 4단계 중복 검증
2. **품질 관리**: 가희 검수관 통합 (사전 + 사후, 최대 3회 재시도)
3. **자동화**: 토큰 자동 갱신, Git 동기화, 캘린더 통합
4. **폴백 체계**: Ollama → Gemini → Template 순차 폴백

#### 루나 디렉터
1. **체크포인트 시스템**: 중단된 작업 재개 (리소스 절약)
2. **리소스 관리**: 무거운 렌더링 전 시스템 자원 체크
3. **다단 폴백**: 음악, 이미지, 비디오 각 3단계 폴백
4. **조기 검증**: 음원 길이 미달 시 비주얼 합성 전 중단 (리소스 절약)
5. **자동 메타데이터**: Ollama로 고유한 제목/설명 생성

### ⚠️ 개선 필요 사항

#### 공통
1. **환경변수 로드 누락**: 4개 파일 `load_env()` 미적용
2. **하드코딩된 설정**: 금지 키워드, 경로, 시간 등

#### 아린 관리자
1. **금지 키워드**: `_BANNED_PHRASES`, `_BANNED_TOPICS` 하드코딩
2. **트렌드 소스**: Google Trends RSS만 사용 (다양화 필요)

#### 루나 디렉터
1. **ffmpeg 경로**: Windows 절대 경로 하드코딩
2. **체크포인트 만료**: 36시간 하드코딩
3. **금지 장르**: `_BANNED_GENRES` 하드코딩

---

## 개선 권장사항

### 1. 환경변수 통합 완료 (우선순위: 높음)

#### 미적용 파일에 load_env() 추가
- `_shared/telegram_notifier.py`
- `루나_디렉터/tools/src/lyria_music_generator.py`
- `코다리_개발자/tools/pack_apply.py`
- `현빈_전략가/tools/paypal_revenue.py`

#### 권장 코드
```python
import sys
import os
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..")))
from _shared.env_loader import load_env, validate_env

load_env()
validate_env(["GEMINI_API_KEY"])  # 필수 환경변수 검증
```

### 2. 하드코딩 제거 (우선순위: 중간)

#### 금지 키워드 환경변수화
- `.env`에 `BANNED_KEYWORDS`, `BANNED_GENRES` 추가
- `env_config.py`에서 로드

#### ffmpeg 경로 자동 감지 개선
```python
# 현재
FFMPEG = r"C:\Users\cross\...\ffmpeg.exe"

# 개선
FFMPEG = shutil.which("ffmpeg") or os.getenv("FFMPEG_PATH", "ffmpeg")
```

### 3. 설정 파일 중앙화 (우선순위: 낮음)

#### config.json 도입
```json
{
  "checkpoint_max_age_hours": 36,
  "duplicate_check_days": 7,
  "similarity_thresholds": {
    "prompt": 0.60,
    "caption": 0.70,
    "hashtag": 0.80
  },
  "retry_limits": {
    "gahee_check": 3,
    "upload": 3
  }
}
```

### 4. 문서화 강화 (우선순위: 중간)

#### 각 에이전트별 README.md 작성
- 파이프라인 다이어그램
- 필수 환경변수 목록
- 실행 방법
- 트러블슈팅

### 5. 모니터링 및 로깅 (우선순위: 높음)

#### 통합 로깅 시스템
- 각 에이전트의 실행 로그 중앙 집중
- 성공/실패 메트릭 수집
- 대시보드 구축 (Streamlit/Grafana)

### 6. 테스트 자동화 (우선순위: 높음)

#### 단위 테스트
- 각 에이전트의 핵심 함수 테스트
- 환경변수 누락 시나리오 테스트
- 폴백 메커니즘 테스트

#### 통합 테스트
- 전체 파이프라인 E2E 테스트
- 모의 업로드 (dry-run 모드)

---

## 결론

### 📊 현황 요약

- **총 에이전트**: 12개
- **환경변수**: 17개 정의, 12개 실사용
- **파이프라인 품질**: 우수 (중복 방지, 품질 관리, 폴백 체계)
- **통합 수준**: 높음 (8/12 파일 load_env() 적용)

### ✅ 완료된 작업

1. 환경변수 사용 현황 스캔 및 분석
2. env_config.py 메타정보 정의
3. env_loader.py 검증 함수 추가
4. ENV_README.md 업데이트
5. 아린/루나 파이프라인 상세 검토

### 🔜 다음 단계

1. 미적용 파일 4개에 load_env() 추가
2. 환경변수 암호화 및 재배포
3. test_env_vars.py 개선 (Notion/Instagram 검증 추가)
4. ENV_REFERENCE.md, ENV_SETUP_GUIDE.md 생성
5. 하드코딩 제거 및 설정 파일 중앙화

---

**작성**: Claude Code  
**일시**: 2026-06-02  
**버전**: 1.0
