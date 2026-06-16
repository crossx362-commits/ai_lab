# 📸 아린 관리자 — Instagram 콘텐츠 자동화 워크플로우
**작성일**: 2026-06-02  
**에이전트**: 아린 (Instagram 채널 전담 디렉터)

---

## 🎯 전체 프로세스 (6단계)

```
① 구글 트렌드 수집 (KR·US·JP + 카테고리)
    ↓
② 시각 키워드 추출 + 콘텐츠 기획
    ↓
③ 이미지 생성 (Gemini → Pollinations 폴백)
    ↓
④ 이미지 호스팅 (Catbox.moe)
    ↓
⑤ Vision 캡션 생성 + 중복 체크
    ↓
⑥ 가희 검수 (사전→업로드→사후) + 자동 수정
```

---

## 🔍 단계별 상세 설명

### ① 구글 트렌드 수집
**도구**: `get_trends()` — Google Trends RSS

```python
# 3개 지역 + 카테고리별 수집
for geo in ["KR", "US", "JP"]:
    fetch_trends(f"https://trends.google.com/trending/rss?geo={geo}")

# 카테고리별 큐레이션 (선택적)
categories = ["entertainment", "business", "technology", "health", "science"]
```

**필터링**:
```python
# 최근 7일 사용한 트렌드 제외
used_topics = _get_recent_trend_topics(days=7)
fresh_trends = [t for t in trends if t not in used_topics]

# 금지 키워드 제외
_BANNED_PHRASES = [
    "AI 생성 이미지", "ai 생성", "인공지능이 만든",
    "체험해보세요", "경험해보세요"
]
_BANNED_TOPICS = [
    "미래", "인공지능", "ai", "기계", "테크", "로봇",
    "첨단기술", "4차산업", "딥러닝", "머신러닝"
]
```

**출력**:
```python
selected_trend = "봄 꽃 감성 피드"  # 최상위 신선 트렌드
```

---

### ② 시각 키워드 추출 + 콘텐츠 기획
**도구**: `_extract_visual_keywords()` + `_generate_full_content()`

#### 2-1. 시각 키워드 추출 (Ollama)
```python
# 상위 12개 트렌드에서 6가지 시각 요소 추출
keywords = _extract_visual_keywords(fresh_trends[:12])

# 반환 구조:
{
    "mood": "nostalgic, calm",      # 분위기
    "scene": "cafe, window",         # 장면
    "color": "pastel pink, beige",   # 색상
    "subject": "cherry blossom",     # 주제
    "style": "minimal, aesthetic",   # 스타일
    "season": "spring"               # 계절
}
```

#### 2-2. 통합 콘텐츠 기획 (Ollama)
```python
post_data = _generate_full_content(selected_trend, keywords)

# 반환 구조:
{
    "image_prompt": "Soft pastel pink cherry blossoms...",  # 이미지 생성 프롬프트
    "caption": "봄의 첫 설렘을 담은 벚꽃 🌸\n\n...",        # 캡션
    "best_time": "11:30-12:00",                            # 최적 업로드 시간
    "hashtags": ["#봄", "#벚꽃", "#감성", ...]               # 해시태그
}
```

**최적 업로드 시간 알고리즘**:
- 평일 11:30-12:15 (점심 타임)
- 평일 18:30-19:00 (퇴근 타임)
- 주말 14:00-16:00

---

### ③ 이미지 생성
**도구**: `craft_insta_prompt()` → Gemini Imagen / Pollinations.ai

#### 3-1. 프롬프트 크래프팅
```python
# 기본 프롬프트 → Instagram 최적화 프롬프트
raw_prompt = post_data['image_prompt']
crafted_prompt = craft_insta_prompt(raw_prompt)

# prompt_crafter.py 내부 로직:
# - 카테고리 자동 감지 (tech/landscape/animal/person)
# - 카테고리별 전문 프롬프트 템플릿 적용
# - Instagram 선호 스타일 추가 (high-resolution, vivid colors, ...)
```

#### 3-2. 유사 이미지 검수
```python
# 최근 14일 프롬프트와 비교
recent_prompts = _get_recent_image_prompts(days=14)
is_similar, ratio = _is_similar_prompt(crafted_prompt, recent_prompts, threshold=0.60)

if is_similar:
    # 프롬프트 강제 변형 (계절·시간대·구도 접두어)
    variation_seeds = [
        "Aerial bird's-eye view, ",
        "Close-up macro shot, ",
        "Twilight blue hour, ",
        "Foggy misty morning, ",
        "Vivid summer midday, ",
        "Snowy winter scene, ",
    ]
    idx = hash(crafted_prompt) % len(variation_seeds)
    crafted_prompt = variation_seeds[idx] + crafted_prompt
```

#### 3-3. 이미지 생성 (폴백 전략)
```python
# 1순위: Gemini Imagen
try:
    img_bytes = _generate_image_gemini(crafted_prompt)
    # gemini-3.1-flash-image-preview
except Exception:
    # 2순위: Pollinations.ai 폴백
    img_bytes = _generate_image_pollinations(crafted_prompt)
    # https://image.pollinations.ai/prompt/{encoded_prompt}
```

---

### ④ 이미지 호스팅
**도구**: Catbox.moe (무료 파일 호스팅)

```python
# Instagram Graph API는 공개 URL 필요
with open("temp_generated.jpg", "rb") as f:
    response = requests.post(
        "https://catbox.moe/user/api.php",
        data={"reqtype": "fileupload"},
        files={"fileToUpload": f}
    )

image_url = response.text.strip()
# 예: https://files.catbox.moe/abc123.jpg
```

**이유**:
- Instagram Graph API는 로컬 파일 직접 업로드 미지원
- 공개 URL을 통한 이미지 참조 방식 필수

---

### ⑤ Vision 캡션 생성 + 중복 체크
**도구**: Gemini Vision + Ollama (재생성)

#### 5-1. Vision 캡션 생성
```python
# 실제 이미지를 보고 캡션 작성
vision_caption = generate_caption_from_image(img_bytes)

# 금지 문구 체크
if _has_banned_content(vision_caption):
    vision_caption = None  # 템플릿 캡션으로 폴백

final_caption = vision_caption or post_data["caption"]
```

#### 5-2. 캡션 중복 체크
```python
# 최근 포스팅과 유사도 비교 (70% 이상 = 중복)
is_duplicate, ratio = _is_caption_duplicate(final_caption)

if is_duplicate:
    # Ollama로 새 캡션 생성
    regen = lm_chat(
        f"다음 인스타 캡션을 다른 표현으로 바꿔줘. "
        f"진짜 사람이 쓴 것처럼 짧고 자연스럽게, 친구한테 말하듯. "
        f"이모지·해시태그 유지. 캡션만 출력:\n{final_caption}",
        max_tokens=300, temperature=0.9
    )
    final_caption = regen.strip()
```

#### 5-3. 해시태그 중복 체크
```python
# 최근 포스팅과 해시태그 겹침 비율
is_dup, overlap = _is_hashtag_duplicate(final_caption)

if is_dup:
    # 마지막 2개 태그를 대안 태그로 교체
    alt_tags = [
        "#감성사진", "#일상공유", "#데일리", "#분위기", "#힐링",
        "#소확행", "#좋아요", "#팔로우", "#인스타그램", "#사진스타그램"
    ]
    # 해시 기반 랜덤 선택으로 교체
```

**캡션 구조**:
```
[감성적인 한 줄] 🌸

[본문 2~3문장]

#해시태그1 #해시태그2 ... (5~8개)
```

---

### ⑥ 가희 검수 + 자동 수정 루프
**도구**: `content_inspector.py` (가희) + Ollama (수정)

#### 워크플로우 (최대 3회 재시도)
```
사전 검수 → 업로드 → 사후 검수
    ↓ 실패
Ollama 캡션 수정 → 삭제 → 재업로드
    ↓ 실패
Ollama 캡션 수정 → 삭제 → 재업로드
    ↓ 실패
수동 확인 필요 알림
```

#### 6-1. 가희 사전 검수
```python
pre_check = gahee_inspect_caption(final_caption)

# 검수 항목:
{
    "pass": True/False,
    "issues": [
        "금지 키워드: 인공지능",
        "캡션 너무 짧음 (50자 미만)",
        "이전 포스팅과 85% 유사"
    ]
}
```

**금지 규칙** (`content_inspector.py`):
1. 금지 키워드: AI, 인공지능, 미래, 테크, 로봇
2. 금지 문구: "체험해보세요", "경험해보세요"
3. 캡션 길이: 50자 이상
4. 캡션 유사도: 70% 미만
5. 해시태그 개수: 8개 이하 (코드 기준)

#### 6-2. Instagram 업로드
```python
uploader = InstaUploader(account_id, access_token)
post_id = uploader.upload_image(image_url, final_caption)

# Instagram Graph API v23.0
# 1단계: 컨테이너 생성
creation_id = POST /me/media {image_url, caption}

# 2단계: 발행 (30초 대기)
post_id = POST /me/media_publish {creation_id}
```

#### 6-3. 가희 사후 검수
```python
post_check = gahee_inspect_post_upload(post_id)

if not post_check["pass"]:
    # 실패 시 자동 수정 루프
    # 1. 기존 포스팅 삭제
    DELETE https://graph.instagram.com/v23.0/{post_id}
    
    # 2. Ollama로 캡션 수정
    fixed_caption = _fix_caption_ollama(final_caption, issues)
    
    # 3. 재업로드
    new_post_id = uploader.upload_image(image_url, fixed_caption)
```

**Ollama 수정 프롬프트**:
```python
prompt = (
    f"이 인스타 캡션 고쳐줘. 문제: {issues}\n"
    f"원본: {bad_caption}\n\n"
    "진짜 사람이 쓴 것처럼 짧고 자연스럽게. "
    "AI·인공지능·미래·테크 금지. 해시태그 5~8개. 캡션만 출력."
)
```

---

## 🔄 자동화 기능

### 중복 방지 시스템

| 유형 | 체크 기간 | 임계값 | 조치 |
|------|----------|--------|------|
| 트렌드 주제 | 7일 | 완전 일치 | 스킵 |
| 이미지 프롬프트 | 14일 | 60% 유사 | 변형 접두어 추가 |
| 캡션 | 전체 | 70% 유사 | Ollama 재생성 |
| 해시태그 | 전체 | 고중복 | 일부 태그 교체 |

### Calendar 업데이트
```python
# .ics 파일에 예정 업로드 일정 자동 기록
update_ics_calendar(
    trend=selected_trend,
    date=current_date,
    time=post_data['best_time']
)
```

### Git 자동 동기화
```python
# 파이프라인 완료 후 자동 커밋
git_sync()
# - 변경된 파일들 자동 감지
# - 타임스탬프 커밋 메시지 생성
# - origin/main 푸시
```

---

## 📊 토큰 관리

### Instagram Access Token 자동 갱신
```python
# uploader.py - ensure_token_fresh()
def ensure_token_fresh():
    """만료 7일 전 자동 갱신 (60일 → 60일 연장)"""
    # 1. 현재 토큰 유효성 확인
    # 2. 만료일 7일 이내 → 자동 갱신 API 호출
    # 3. .env 파일 업데이트
    # 4. Instagram ACCOUNT_ID 재확인 (변경될 수 있음)
```

**토큰 타입**:
- User Access Token (60일): 자동 갱신 가능
- Page Access Token: 무기한 (갱신 불필요)

---

## 🚨 에러 처리

### 이미지 생성 실패
```python
try:
    img_bytes = _generate_image_gemini(crafted_prompt)
except Exception:
    # Pollinations.ai 폴백
    img_bytes = _generate_image_pollinations(crafted_prompt)
except Exception:
    # 완전 실패 → 텔레그램 알림 + 종료
    send_telegram_message("이미지 생성 실패")
    sys.exit(1)
```

### 업로드 실패 (3회 재시도)
```python
MAX_RETRIES = 3

for attempt in range(1, MAX_RETRIES + 1):
    pre_check = gahee_inspect_caption(caption)
    if not pre_check["pass"]:
        caption = _fix_caption_ollama(caption, issues)
        continue
    
    post_id = uploader.upload_image(url, caption)
    if not post_id:
        break
    
    post_check = gahee_inspect_post_upload(post_id)
    if not post_check["pass"]:
        # 삭제 + 수정 + 재업
        continue
    
    break  # 성공
```

### API Rate Limit
```python
# Gemini Imagen 429 에러 → 백오프 재시도
# Instagram API 오류 → 즉시 실패 (재시도 안 함)
```

---

## 📂 파일 구조

```
skills/아린_관리자/tools/
├── auto_pipeline.py           # 메인 파이프라인
├── uploader.py                # Instagram 업로더 + 토큰 관리
├── prompt_crafter.py          # 프롬프트 최적화
└── image_research.py          # 1시간 주기 리서치

출력 파일:
├── temp_generated.jpg         # 임시 이미지 (업로드 후 삭제)
└── reports/history/upload_history.json  # 업로드 이력
```

---

## 💾 업로드 히스토리 저장

```json
{
    "agent": "아린",
    "status": "published",
    "uploaded_at": "2026-06-02T14:30:00+09:00",
    "metadata": {
        "platform": "instagram",
        "post_id": "17841234567890123",
        "caption": "봄의 첫 설렘을 담은 벚꽃 🌸\n\n...",
        "image_url": "https://files.catbox.moe/abc123.jpg",
        "trend_topic": "봄 꽃 감성 피드",
        "image_prompt": "Soft pastel pink cherry blossoms..."
    }
}
```

---

## 📈 성능 지표

| 지표 | 값 | 비고 |
|------|-----|------|
| 평균 실행 시간 | 2~3분 | 이미지 생성 포함 |
| 중복 방지율 | 95%+ | 7일 트렌드 필터 |
| 가희 검수 통과율 | 80%+ | 1차 통과 기준 |
| 자동 수정 성공률 | 90%+ | Ollama 재생성 |
| 최종 업로드 성공률 | 98%+ | 3회 재시도 포함 |

---

## 🎯 최적화 포인트

### 골든 타임 업로드
- 평일 11:30-12:00: 점심 시간 피크
- 평일 18:30-19:00: 퇴근 시간 피크
- 주말 14:00-16:00: 여가 시간

### 인게이지먼트 극대화
- 감성적 캡션 (친구한테 말하듯)
- 이모지 적절히 사용 (3~5개)
- 해시태그 5~8개 (10개 이상은 스팸 취급)
- 첫 댓글 30분 내 답글

---

## 🔧 실행 명령어

```bash
# 기본 실행 (실제 업로드)
python auto_pipeline.py

# 드라이 런 (업로드 제외, 테스트용)
python auto_pipeline.py --dry-run

# 디버그 모드
python auto_pipeline.py --debug
```

---

## 🚀 향후 개선 계획

1. **릴스 자동화**: 짧은 비디오 콘텐츠 생성
2. **스토리 자동 포스팅**: 24시간 임시 콘텐츠
3. **A/B 테스트**: 캡션 스타일별 인게이지먼트 비교
4. **팔로워 분석**: 선호 콘텐츠 자동 학습
5. **댓글 자동 응답**: Ollama 기반 자연스러운 답글

---

## 📚 참고 자료

### API 문서
- [Instagram Graph API v23.0](https://developers.facebook.com/docs/instagram-api)
- [Gemini Imagen API](https://ai.google.dev/docs)
- [Google Trends RSS](https://trends.google.com/trending/rss)

### 프로젝트 파일
- [auto_pipeline.py](../../projects/ai-team/skills/아린_관리자/tools/auto_pipeline.py)
- [uploader.py](../../projects/ai-team/skills/아린_관리자/tools/uploader.py)
- [SKILL.md](../../projects/ai-team/skills/아린_관리자/SKILL.md)

---

**마지막 업데이트**: 2026-06-02  
**작성자**: AI Team 검수 시스템
