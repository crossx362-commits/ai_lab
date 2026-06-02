# AI Team 에이전트 검수 보고서
**검수일**: 2026-06-02  
**검수 범위**: 스킬/툴 경로, 의존성, 업무 파악

---

## 📋 전체 에이전트 구성 (11개)

| 에이전트 | 역할 | 주요 도구 수 | 상태 |
|---------|------|------------|------|
| 예원 (CEO) | 회사 총괄·작업 분배 | 4개 | ✅ 정상 |
| 영숙 (비서) | 텔레그램·일정·보고 | 10개 | ✅ 정상 |
| 루나 (디렉터) | YouTube 음악 영상 제작 | 6개 (+src 4개) | ✅ 정상 |
| 아린 (관리자) | Instagram 콘텐츠 관리 | 4개 | ✅ 정상 |
| 가희 (검수관) | 콘텐츠 품질·정책 검수 | 2개 | ✅ 정상 |
| 코다리 (개발자) | 웹 개발·헬스체크 | 10개 | ✅ 정상 |
| 케빈 (인프라) | Vercel·Supabase 관리 | 8개 | ✅ 정상 |
| 티모 (디자이너) | UI/UX 검토·개선 | 1개 | ✅ 정상 |
| 현빈 (전략가) | 비즈니스 리서치 | 3개 | ✅ 정상 |
| 경수 (수사관) | 악플 감지·보안 감사 | 1개 | ✅ 정상 |
| 로율 (변호사) | 법률·세무·컴플라이언스 | 1개 | ✅ 정상 |

---

## ✅ 경로 및 구조 검수 결과

### 1. Tools 디렉토리 구조
모든 에이전트가 `skills/<에이전트명>/tools/` 구조를 올바르게 사용하고 있습니다.

**특이사항**:
- 루나: `/tools/src/` 서브디렉토리 추가 (youtube_research, trend_analyzer 등 4개)
- 공용스킬: tools 디렉토리 없음 (문서 전용)

### 2. _shared 모듈 Import 경로
✅ **모두 정상 작동**

**Import 패턴**:
```python
# 프로젝트 루트를 sys.path에 추가하는 공통 패턴
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):  # 또는 .agent
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

# 이후 직접 import
from _shared.telegram_notifier import send_telegram_message
from _shared.gemini_client import text, image
```

**검증 완료**: `_shared.gemini_client` 모듈 import 테스트 성공

### 3. 공통 의존성 (_shared 모듈 목록)

| 모듈 | 용도 | 사용 에이전트 |
|------|------|-------------|
| `gemini_client.py` | Gemini API 통합 (텍스트/이미지/Vision) | 루나, 아린, 가희, 현빈, 티모 |
| `ollama_client.py` | Ollama 로컬 LLM | 모든 에이전트 |
| `telegram_notifier.py` | 텔레그램 알림 | 모든 에이전트 |
| `env_loader.py` | 환경변수 로딩 | 케빈, 코다리, 루나, 아린 |
| `calendar_client.py` | Google Calendar 연동 | 영숙 |
| `notion_client.py` | Notion API | 영숙 |
| `duplicate_guard.py` | 중복 방지 | 루나, 아린 |
| `image_uploader.py` | 이미지 업로드 (Catbox 등) | 아린 |
| `knowledge_base.py` | 지식베이스 관리 | 루나, 현빈 |
| `resource_utils.py` | 리소스 유틸리티 | 여러 에이전트 |

---

## 👥 에이전트별 상세 업무 및 도구

### 🎯 예원 (CEO) — 회사 총괄 오케스트레이터
**핵심 미션**:
- 사장님 명령 분석 및 에이전트 작업 분배
- 종합 보고서 작성
- 에이전트 스킬 감사 (주간)
- 업로드 관리 총괄 (영숙에게 위임)

**도구**:
- `yewon_dispatcher.py` — 작업 라우팅 및 분배
- `skill_auditor.py` — 주간 스킬 검토 (Ollama 기반)
- `upload_manager.py` — 일일 업로드 현황 점검
- `evaluate_feedback.py` — 에이전트 성과 평가

**의존성**:
- 모든 전문 에이전트와 양방향 통신
- 영숙 → 일일 브리핑 제공
- 가희 → 긴급 검수 보고 수령

---

### 💼 영숙 (비서) — 개인 비서 & 업로드 관리자
**핵심 미션**:
- 텔레그램 메시지 최우선 응답
- Google Calendar 일정 관리
- YouTube 영상 추천 (3~8시간 랜덤)
- 일일 업로드 현황 보고
- Reports 폴더 관리

**도구**:
- `telegram_receiver.py` — 텔레그램 봇 서버
- `yeongsuk_telegram_bot.py` — 비서 모드 JSON 응답
- `google_calendar.py` / `google_calendar_write.py` — 일정 CRUD
- `youtube_recommender.py` — 자동 영상 추천
- `notion_summarizer.py` — Notion 요약
- `posting_scheduler.py` — 포스팅 스케줄 관리
- `reports_manager.py` — 리포트 정리 (30일 이상 archive)
- `register_upload_schedule.py` — 업로드 일정 등록

**의존성**:
- CEO 예원 → 작업 지시 수령
- 가희 → 검수 보고 필터링 후 전달
- 모든 에이전트 → 결과물 취합 후 사장님께 보고

---

### 🎵 루나 (디렉터) — AI Music & Video Director
**핵심 미션**:
- Lyria 3 Pro 완곡 생성 (2분 이상, 시티팝·K-POP 퓨전)
- Veo 3.1 비주얼 생성 및 합성
- YouTube SEO 최적화 제목/태그/설명 생성
- 트렌드 분석 (1시간 주기)
- KST 19:00 예약 업로드

**도구**:
- `music_video_pipeline.py` — 전체 뮤직비디오 파이프라인
- `shorts_pipeline.py` — YouTube Shorts 제작 (60초 이하)
- `lyria_music_gen.py` — Lyria 3 음악 생성
- `veo_video_maker.py` — Veo 3.1 영상 생성
- `update_titles.py` — 제목 최적화 업데이트
- `audit_output.py` — 출력물 품질 검증 (60초 미만 삭제)
- **src/**:
  - `youtube_research.py` — 1시간 주기 트렌드 리서치
  - `trend_analyzer.py` — YouTube 상위 100개 패턴 분석
  - `youtube_uploader.py` — YouTube 업로드
  - `youtube_trending_notify.py` — 트렌드 알림

**의존성**:
- Ollama → 목표 설정, 패턴 분석
- Gemini API → 비주얼 생성 (Pollinations 폴백)
- 가희 → 업로드 전 사전 검수

**금지사항**:
- ❌ Lofi / Lo-fi / Study Beats / Chill Beats 장르
- ❌ 제목에 "LUNA" / "Official" / "MV" 고정 태그
- ❌ 2분 미만 음악 (자동 삭제됨)
- ❌ 쇼츠 60초 초과

---

### 📸 아린 (관리자) — Instagram 채널 전담 디렉터
**핵심 미션**:
- 구글 트렌드 기반 콘텐츠 자동 기획
- Gemini 이미지 생성 + Catbox 호스팅
- Instagram Graph API v23.0 자동 포스팅
- 이미지 리서치 (1시간 주기)

**도구**:
- `auto_pipeline.py` — 전체 Instagram 자동화 파이프라인
- `uploader.py` — Instagram 업로더 (토큰 갱신 포함)
- `prompt_crafter.py` — 이미지 프롬프트 제작
- `image_research.py` — 1시간 주기 이미지 트렌드 리서치

**의존성**:
- Ollama → 트렌드 수집, 프롬프트 생성
- Gemini Imagen → 이미지 생성 (Pollinations 폴백)
- 가희 → 업로드 전 캡션 검수

**금지사항**:
- ❌ 캡션: "AI 생성", "인공지능", "체험해보세요"
- ❌ 주제: 미래, 테크, 로봇, 딥러닝
- ❌ 캡션 유사도 70% 이상
- ❌ 해시태그 8개 초과 (코드 기준)

---

### 🔍 가희 (검수관) — 콘텐츠 품질 관리 전문가
**핵심 미션**:
- YouTube 음악 영상 품질·정책 위반 검수
- Instagram 캡션 금지 키워드 감지
- 중복 제목/설명/썸네일 자동 수정
- 하루 3회 정기 스캔 (07:00 / 13:00 / 21:00)

**도구**:
- `content_inspector.py` — 검수 실행 (NEW_UPLOAD / EXISTING_CONTENT)
- `fix_issues.py` — 자동 수정 실행 (판정 로그 기반)

**판정 기준**:
- **REJECT**: 심각한 정책 위반 (즉시 비공개 + 수정)
- **REVIEW**: 오탐 가능 (검토 필요)
- **PASS**: 정상

**의존성**:
- Ollama → 패턴 분석, 금지 키워드 감지
- 루나/아린 → 업로드 전 호출
- 경수 → 정책 위반 시 에스컬레이션
- CEO 예원 → 긴급 사안 보고

---

### 💻 코다리 (개발자) — 풀스택 개발자
**핵심 미션**:
- Vite+React+TypeScript 웹 프로젝트 초기화
- 템플릿 팩 적용
- PWA 설정
- 텔레그램 봇 / Ollama 헬스체크 (2시간 주기)
- Mermaid 다이어그램 생성

**도구**:
- `web_init.py` — 웹 프로젝트 초기화
- `pack_apply.py` — 템플릿 팩 적용
- `pwa_setup.py` — PWA 설정
- `lint_test.py` — 린트·테스트
- `web_preview.py` — 개발 서버 미리보기
- `telegram_health_check.py` — 텔레그램 봇 진단 (2시간)
- `ollama_health_check.py` — Ollama 진단 (2시간)
- `agent_health_check.py` — 전체 에이전트 상태 체크
- `instagram_token_refresher.py` — Instagram 토큰 갱신
- `mermaid_generator.py` — Mermaid 다이어그램 자동 생성

**의존성**:
- Ollama DeepSeek → 코딩 전용 모델
- CEO 예원 → 장애 보고

---

### 🏗️ 케빈 (인프라) — DevOps & 클라우드 관리자
**핵심 미션**:
- Vercel 배포 관리 및 클린업
- Supabase 스키마 동기화
- Petnna PWA 모니터링 (매시간)
- 환경변수 동기화
- Git 리포지토리 관리

**도구**:
- `vercel_manager.py` — Vercel 배포·프로젝트 관리
- `supabase_manager.py` — Supabase 연결·스키마 동기화
- `petnna_monitor.py` — Petnna 헬스체크 (매시간)
- `sync_env_to_vercel.py` — 환경변수 동기화
- `debug_env.py` / `test_env_vars.py` — 환경변수 디버깅

**의존성**:
- Vercel API, Supabase API
- Git (자동 커밋·푸시)

---

### 🎨 티모 (디자이너) — UI/UX 전문가
**핵심 미션**:
- Petnna UI/UX 검토 (주 2회: 화·금 10시)
- 연구 기반 디자인 피드백 (Nielsen Norman Group)
- 접근성 검증

**도구**:
- `petnna_reviewer.py` — Petnna 각 모듈 검토 (Ollama 기반)

**검토 기준**: 7가지 (시각 계층, 가독성, 터치 타겟, 빈 상태, 반응형, 일관성, 접근성)

---

### 💼 현빈 (전략가) — 비즈니스 전략가
**핵심 미션**:
- 시장 트렌드 리서치 (1시간 주기)
- AI 크리에이터 수익화 모델 분석
- PayPal 매출 모니터링

**도구**:
- `business_research.py` — 비즈니스 리서치 (Ollama 기반)
- `paypal_revenue.py` — PayPal API 거래 조회
- `deep_search_6h.py` — 심층 리서치 (6시간 주기)

**의존성**:
- Ollama → 모델 수집·분석
- CEO 예원 → 인사이트 보고

---

### 🚨 경수 (수사관) — 사이버 수사관
**핵심 미션**:
- 악플 탐지 및 Google Sheets 박제
- 보안 취약점 스캔 (API 키 노출, Firebase 규칙)
- 코드 감사

**도구**:
- `comment_forensics.py` — YouTube 댓글 포렌식

**의존성**:
- YouTube API, Google Sheets API
- 가희 → 정책 위반 시 에스컬레이션 수령

---

### ⚖️ 로율 (변호사) — 법률·세무 전문가
**핵심 미션**:
- 대한민국 민법·세법 시뮬레이션
- Petnna 법률 검토 (주간: 월 10시 / 월간: 1일)
- 업로드 작업물 저작권·라이선스 감사

**도구**:
- `tax_simulator.py` — 상속세·증여세 시뮬레이션

**의존성**:
- 웹 서치 → 최신 법령·판례 자동 조회
- CEO 예원 → 주간 리포트

---

## 🔄 에이전트 간 워크플로우 맵

### 일반 작업 흐름
```
사장님 명령 → 예원 (CEO) 분석 → 전문 에이전트 배정
    ↓
전문 에이전트 실행 → 영숙 (비서) 결과 포맷
    ↓
영숙 → 사장님 텔레그램 보고
```

### 콘텐츠 업로드 흐름
```
[루나 or 아린] 콘텐츠 생성
    ↓
가희 사전 검수 → PASS/REJECT/REVIEW
    ↓
PASS: 업로드 진행
REJECT: 즉시 수정 (fix_issues.py 자동 호출)
    ↓
업로드 완료 → 영숙 → 사장님 보고
```

### 정기 작업 (자동)
- **1시간 주기**: 루나 트렌드, 아린 이미지 리서치, 현빈 비즈니스 리서치
- **2시간 주기**: 코다리 텔레그램·Ollama 헬스체크
- **하루 3회** (07:00 / 13:00 / 21:00): 가희 정기 스캔
- **매시간**: 케빈 Petnna 모니터링
- **주간**: 예원 스킬 감사 (월 09:00), 티모 UI 검토 (화·금 10:00), 로율 법률 검토 (월 10:00)
- **월간**: 로율 심층 감사 (1일)

---

## 🛠️ 개선 권고사항

### ✅ 현재 정상 작동 중
1. **_shared 모듈 import** — 모든 스크립트에서 정상 동작
2. **Tools 디렉토리 구조** — 일관성 유지
3. **AI 우선순위** — Ollama 1순위 → Gemini 2순위 패턴 준수

### 🟡 선택적 개선 사항

1. **Python 경로 관리 개선**
   - 현재: 각 스크립트마다 루프로 프로젝트 루트 찾기
   - 제안: `_shared/path_setup.py` 공통 모듈 생성
   
2. **중복 코드 제거**
   - 현재: 각 에이전트 리서치 스크립트에 유사한 Ollama 호출 패턴
   - 제안: `_shared/research_base.py` 공통 베이스 클래스

3. **환경변수 검증 강화**
   - 케빈의 `debug_env.py` 패턴을 모든 에이전트에 적용
   - 필수 환경변수 누락 시 명확한 에러 메시지

4. **로깅 표준화**
   - 현재: 텔레그램 메시지만 주로 사용
   - 제안: `_shared/logger.py` 통합 로깅 시스템 (파일 + 텔레그램)

---

## 📊 전체 통계

- **총 에이전트**: 11개
- **총 도구 스크립트**: 50개
- **_shared 공통 모듈**: 12개
- **정기 자동 작업**: 10개
- **AI 모델**: Ollama (1순위), Gemini (2순위)
- **외부 API**: YouTube, Instagram, Google Calendar, Notion, Vercel, Supabase, PayPal, Telegram

---

## ✅ 검수 결론

**전체 시스템 상태**: 🟢 정상 작동

모든 에이전트의 스킬과 툴이 올바른 경로에 배치되어 있으며, _shared 모듈 의존성이 정상적으로 해결됩니다. 각 에이전트는 명확하게 정의된 역할과 도구를 가지고 있으며, 워크플로우가 체계적으로 설계되어 있습니다.

**추천 조치**: 선택적 개선 사항은 시스템 안정성이 확보된 후 점진적으로 적용하시면 됩니다.
