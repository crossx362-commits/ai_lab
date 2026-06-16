# AI Lab 에이전트 파이프라인 기술 문서

생성일: 2026-06-02 | 최종 수정: 2026-06-03  
> 에이전트 역할·팀 구성·스케줄은 **[AI_TEAM_ROLES.md](./reports/AI_TEAM_ROLES.md)** 참조.  
> 이 문서는 파이프라인 구현 세부사항, 환경변수, 알려진 이슈만 다룬다.

---

## 아린 파이프라인 (`auto_pipeline.py`)

```
1. 트렌드 수집: Google Trends RSS (KR/US/JP) + 큐레이션 → 랜덤 선택
2. 키워드 추출: Ollama → 규칙 기반 폴백
3. 이미지 생성: Gemini → Pollinations.ai 폴백 → Catbox.moe 호스팅
4. Vision 분석: Gemini Vision → (caption + alt_text 150자) → Ollama 폴백
5. 중복 검증: 캡션 70% / 해시태그 80% / 프롬프트 60% 유사도 체크
6. 업로드 (최대 3회 재시도)
7. 히스토리 기록 + Git 동기화
```

**환경변수**

| 변수 | 용도 | 필수 |
|------|------|------|
| `GEMINI_API_KEY` | 이미지 생성, Vision 캡션 | ✅ |
| `INSTAGRAM_ACCESS_TOKEN` | Graph API 인증 (자동 갱신) | ✅ |
| `INSTAGRAM_ACCOUNT_ID` | Business 계정 ID | ✅ |
| `INSTAGRAM_APP_ID` / `APP_SECRET` | Facebook App 인증 | ✅ |

**해결된 이슈**
- ✅ 트렌드 선택: `fresh_trends[0]` → `random.choice()` 변경
- ✅ 캡션 생성: Vision 모델 JSON 포맷 강제 + 구조화 포맷 방지 (`_clean_vision_caption`)
- ✅ Alt Text: Gemini Vision에서 caption과 동시 생성, `upload_image(alt_text=...)` 연동

---

## 루나 파이프라인 (`music_video_pipeline.py`)

```
1. 테마 선정 + 제목 생성: TrendAnalyzer → title_patterns.json 누적 학습
2. 음악 프롬프트: generate_music_prompt_from_title() — 6단 구조 (제목 콘셉트 연계)
3. Lyria 3 Pro 완곡: lyria-3-pro-preview (2분↑) → Pollinations 폴백
4. 5단 비주얼: 각 파트별 이미지 → Ken Burns 비디오 → 비주얼 병합
5. 오디오+비디오 합성: ffmpeg concat (1280×720 16:9)
6. 메타데이터 생성: Ollama — description + tags (_auto_generate_metadata)
7. YouTube 예약 업로드 (KST 19:00)
8. 히스토리 기록
```

**환경변수**

| 변수 | 용도 | 필수 |
|------|------|------|
| `GEMINI_API_KEY` / `GEMINI_MUSIC_KEY` | 이미지 생성, Lyria 음악 생성 | ✅ |
| `YOUTUBE_API_KEY` | 트렌드 분석 (선택) | ⬜ |

YouTube 업로드는 OAuth 2.0 pickle 파일 사용.

**해결된 이슈**
- ✅ ffmpeg 경로: `_shared/ffmpeg_utils.py`로 중앙화 (`get_ffmpeg_path()`)
- ✅ `_record_to_history`: `_shared/history_recorder.py`로 통합
- ✅ 제목 생성: `_generate_optimized_title`이 `youtube_title_optimization.md` + SKILL.md 지식 반영
- ✅ 제목 중복: `_auto_generate_metadata`에서 제목 덮어쓰기 제거 (description/tags만 담당)

**잔여 이슈**
- ⬜ 체크포인트 만료: `CHECKPOINT_MAX_AGE_HOURS = 36` 하드코딩 (설정 파일화 권장)

---

## 공통 환경변수

| 변수 | 사용처 |
|------|--------|
| `TELEGRAM_BOT_TOKEN` / `TELEGRAM_CHAT_ID` | 영숙 봇, 공용 알림 |
| `GEMINI_API_KEY` | 루나, 아린, 티모 공통 |
| `VERCEL_TOKEN` / `VERCEL_TEAM_ID` | 케빈 |
| `SUPABASE_URL` / `SUPABASE_ANON_KEY` | 케빈, petnna |

### `load_env()` 미적용 파일
- ✅ `_shared/telegram_notifier.py` — 이미 적용됨 (`_load_env` 내부 호출)
- ✅ `코다리_개발자/tools/pack_apply.py` — 적용 완료
- ✅ `현빈_전략가/tools/paypal_revenue.py` — 적용 완료

---

## 공유 유틸리티 (`_shared/`)

| 파일 | 기능 |
|------|------|
| `ffmpeg_utils.py` | FFmpeg/FFprobe 경로 감지, 썸네일 보정 |
| `history_recorder.py` | 에이전트별 업로드 히스토리 기록 |
| `duplicate_guard.py` | 트렌드/프롬프트/캡션 중복 검사 |
| `gemini_client.py` | Vision, 웹서치 공통 클라이언트 |
| `ollama_client.py` | 로컬 LLM 클라이언트 |
