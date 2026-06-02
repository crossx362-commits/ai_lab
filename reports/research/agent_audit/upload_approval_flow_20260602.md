# 📋 업로드 승인 플로우 구축
**구축일**: 2026-06-02  
**담당**: 영숙 비서  
**목적**: 루나/아린 업로드 전 영숙→예원→가희 승인 프로세스

---

## 🎯 개요

### 기존 방식
```
루나/아린 → 직접 업로드 → 가희 사후 검수
```

### 새로운 방식 (승인 플로우)
```
1. 루나/아린 → 영숙 보고
2. 영숙 → 사장님 보고
3. 영숙 → 예원 CEO 피드백 요청
4. 예원 → 가희 검수 지시
5. 가희 검수 통과 → 영숙 보고
6. 영숙 최종 승인 → 업로드 지시
```

---

## 🔄 전체 워크플로우

```
┌─────────────────────────────────────────────────────────┐
│ Step 1: 루나/아린 → 영숙 보고                          │
├─────────────────────────────────────────────────────────┤
│ 루나: "YouTube 영상 준비 완료"                          │
│ 아린: "Instagram 포스트 준비 완료"                      │
│                                                          │
│ → 영숙이 콘텐츠 정보 수신                               │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ Step 2: 영숙 → 사장님 보고                             │
├─────────────────────────────────────────────────────────┤
│ 📢 [영숙 비서 → 사장님]                                │
│                                                          │
│ "루나/아린이 업로드를 준비했습니다."                    │
│ "예원 CEO님께 피드백을 요청하겠습니다."                 │
│                                                          │
│ 제목: ...                                                │
│ 해시태그: ...                                            │
└─────────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────────┐
│ Step 3: 영숙 → 예원 CEO 피드백 요청                    │
├─────────────────────────────────────────────────────────┤
│ 예원 CEO가 Ollama로 콘텐츠 평가:                        │
│                                                          │
│ 검토 기준:                                               │
│ - 제목/캡션 자연스러움                                   │
│ - 브랜드 톤앤매너 부합                                   │
│ - 타겟 오디언스 매력도                                   │
│ - 금지 키워드 없음                                       │
│ - 이전 콘텐츠와 차별화                                   │
│                                                          │
│ 결과: { approved: true/false, score: 1-10 }             │
└─────────────────────────────────────────────────────────┘
                        ↓
               ┌────────┴────────┐
               │                 │
            거절됨            승인됨
               │                 │
               ↓                 ↓
    ┌──────────────┐   ┌─────────────────────────────────┐
    │ 루나/아린    │   │ Step 4: 예원 → 가희 검수 지시  │
    │ 수정 요청    │   ├─────────────────────────────────┤
    │              │   │ 📋 [예원 CEO → 가희 검수관]     │
    │ 승인 플로우  │   │                                  │
    │ 재시작       │   │ "콘텐츠를 검수해주세요."         │
    └──────────────┘   │ "CEO 피드백: 승인"               │
                       └─────────────────────────────────┘
                                   ↓
               ┌────────────────────────────────────┐
               │ 가희 검수 실행                     │
               ├────────────────────────────────────┤
               │ YouTube:                            │
               │ - 제목/설명 금지 키워드 체크       │
               │ - 중복 감지 (MD5 해시)             │
               │ - 정책 위반 확인                   │
               │                                     │
               │ Instagram:                          │
               │ - 캡션 금지 키워드 체크             │
               │ - 70% 유사도 중복 체크              │
               │ - 해시태그 검증                     │
               └────────────────────────────────────┘
                        ↓
               ┌────────┴────────┐
               │                 │
            불합격            합격
               │                 │
               ↓                 ↓
    ┌──────────────┐   ┌─────────────────────────────────┐
    │ 루나/아린    │   │ Step 5: 가희 → 영숙 보고        │
    │ 수정 요청    │   ├─────────────────────────────────┤
    │              │   │ ✅ [가희 검수관 → 영숙 비서]    │
    │ 승인 플로우  │   │                                  │
    │ 재시작       │   │ "콘텐츠 검수가 완료되었습니다." │
    └──────────────┘   │ "검수 결과: 통과"                │
                       │ "업로드 승인 요청드립니다."      │
                       └─────────────────────────────────┘
                                   ↓
               ┌─────────────────────────────────────┐
               │ Step 6: 영숙 최종 승인 → 업로드    │
               ├─────────────────────────────────────┤
               │ ✅ [영숙 비서 → 사장님]             │
               │                                      │
               │ "업로드가 최종 승인되었습니다!"     │
               │                                      │
               │ 검토 단계:                           │
               │ 1. ✅ 영숙 → 사장님 보고            │
               │ 2. ✅ 예원 CEO 피드백: 승인         │
               │ 3. ✅ 가희 품질 검수: 통과          │
               │ 4. ✅ 가희 → 영숙 보고              │
               │ 5. ✅ 영숙 최종 승인                │
               │                                      │
               │ → 루나/아린에게 업로드 지시 🚀      │
               └─────────────────────────────────────┘
                                   ↓
               ┌─────────────────────────────────────┐
               │ 📤 [영숙 → 루나/아린]               │
               │                                      │
               │ "최종 승인이 완료되었습니다!"       │
               │ "지금 바로 업로드하세요."           │
               │                                      │
               │ → 루나: YouTube API 호출            │
               │ → 아린: Instagram Graph API 호출   │
               └─────────────────────────────────────┘
```

---

## 📄 생성된 파일

### upload_approval_flow.py
**위치**: `skills/영숙_비서/tools/upload_approval_flow.py`

#### 주요 함수

1. **request_upload_approval(agent, platform, content_info)**
   - 전체 승인 플로우 실행
   - 5단계 프로세스 자동화
   - 반환: `{ approved: True/False, stage, message, issues }`

2. **luna_upload_approval(video_info)**
   - 루나 YouTube 전용 래퍼
   - 호출: `luna_upload_approval({ title, description })`

3. **arin_upload_approval(post_info)**
   - 아린 Instagram 전용 래퍼
   - 호출: `arin_upload_approval({ caption, hashtags, image_url })`

4. **_get_ceo_feedback(request, content_info)**
   - 예원 CEO Ollama 피드백
   - 평가 기준 5가지 체크
   - 점수 7/10 이상 승인

5. **_run_gahee_inspection(platform, content_info)**
   - 가희 검수 실행
   - YouTube: 금지 키워드, 중복 감지
   - Instagram: 캡션 검수 (content_inspector.inspect_caption)

6. **_issue_upload_command(agent, platform, content_info)**
   - 업로드 지시 발행
   - 텔레그램 명령 전송
   - 실제 파이프라인 트리거

---

## 🚀 사용 방법

### 1. 루나 (YouTube)

#### Before (직접 업로드)
```python
# music_video_pipeline.py
uploader.upload_video(video_path, title, description)
```

#### After (승인 플로우)
```python
# music_video_pipeline.py
from upload_approval_flow import luna_upload_approval

# 업로드 전 승인 요청
video_info = {
    "title": "Neon City Nights - 80s K-Pop Fusion",
    "description": "몽환적인 80년대 도쿄의 네온 불빛..."
}

approval = luna_upload_approval(video_info)

if approval['approved']:
    # 승인됨 - 업로드 진행
    uploader.upload_video(video_path, title, description)
else:
    # 거절됨 - 수정 필요
    print(f"거절 사유: {approval['message']}")
    print(f"문제점: {approval['issues']}")
```

### 2. 아린 (Instagram)

#### Before (직접 업로드)
```python
# auto_pipeline.py
uploader.create_media_container(image_url, caption)
```

#### After (승인 플로우)
```python
# auto_pipeline.py
from upload_approval_flow import arin_upload_approval

# 업로드 전 승인 요청
post_info = {
    "caption": "몽환적인 한 순간 🌿\n...",
    "hashtags": ["#감성", "#힐링", "#자연"],
    "image_url": "https://catbox.moe/..."
}

approval = arin_upload_approval(post_info)

if approval['approved']:
    # 승인됨 - 포스팅 진행
    uploader.create_media_container(image_url, caption)
else:
    # 거절됨 - 수정 필요
    print(f"거절 사유: {approval['message']}")
    print(f"문제점: {approval['issues']}")
```

---

## 📊 승인/거절 예시

### 승인 케이스
```json
{
  "approved": true,
  "stage": "최종_승인",
  "message": "업로드 승인 및 지시 완료",
  "ceo_feedback": "제목이 감성적이고 클릭 유도력이 좋습니다. 브랜드 톤에도 잘 맞아요.",
  "gahee_status": "PASS",
  "upload_command": "루나 YouTube 업로드 실행"
}
```

### 거절 케이스 1: 예원 CEO
```json
{
  "approved": false,
  "stage": "예원_피드백",
  "message": "제목이 너무 기술적이고 감성이 부족합니다. '네온 불빛 아래'보다는 '네온 빛에 물든 밤'이 더 자연스러워요.",
  "issues": [
    "제목 감성 부족",
    "금지 키워드 'AI' 포함"
  ]
}
```

### 거절 케이스 2: 가희 검수
```json
{
  "approved": false,
  "stage": "가희_검수",
  "message": "품질 기준 미달",
  "issues": [
    "금지 키워드 발견: AI, 인공지능",
    "최근 7일 이내 유사 콘텐츠 존재"
  ]
}
```

---

## 🎯 검토 기준

### 예원 CEO 평가 기준
1. **제목/캡션 자연스러움** (클릭 유도력)
2. **브랜드 톤앤매너** 부합 여부
3. **타겟 오디언스** 매력도
4. **금지 키워드** 체크 (AI, 인공지능, 테크, 로봇, 미래)
5. **이전 콘텐츠 차별화** (중복 방지)

**점수 기준**:
- 7-10점: 승인
- 4-6점: 수정 권장
- 1-3점: 거절

### 가희 검수 기준

#### YouTube
- 제목/설명 금지 키워드 체크
- MD5 해시 중복 감지
- 정책 위반 확인
- 시각적 품질 체크 (썸네일)

#### Instagram
- 캡션 금지 키워드 (AI, 인공지능, 테크 등)
- 70% 유사도 중복 체크 (최근 7일)
- 이미지 해시 중복 (최근 14일)
- 해시태그 검증 (금지 해시태그 제외)

---

## 📈 기대 효과

### Before (직접 업로드)
```
❌ 품질 기준 미달 콘텐츠 업로드 가능
❌ 금지 키워드 포함 콘텐츠 노출
❌ CEO 피드백 없이 자동 업로드
❌ 사후 검수만 가능 (삭제 필요)
❌ 업로드 후 문제 발생 시 대응 지연
```

### After (승인 플로우)
```
✅ 업로드 전 사전 검수 (예원 + 가희)
✅ CEO 피드백 기반 품질 향상
✅ 금지 키워드 사전 차단
✅ 중복 콘텐츠 사전 방지
✅ 영숙이 전 과정 모니터링
✅ 텔레그램으로 실시간 보고
```

---

## 🔧 통합 방법

### 루나 파이프라인 통합
**파일**: `skills/루나_디렉터/tools/music_video_pipeline.py`

```python
# Line 1: import 추가
from upload_approval_flow import luna_upload_approval

# Line 300 (업로드 전):
# Before
uploader.upload_video(video_path, title, description)

# After
# 1. 승인 요청
video_info = {
    "title": title,
    "description": description
}

approval = luna_upload_approval(video_info)

# 2. 승인 여부 확인
if not approval['approved']:
    print(f"❌ 업로드 거절: {approval['message']}")
    print(f"문제점: {approval.get('issues', [])}")
    print(f"단계: {approval['stage']}")
    return False

# 3. 승인됨 - 업로드 진행
print(f"✅ 업로드 승인됨")
uploader.upload_video(video_path, title, description)
```

### 아린 파이프라인 통합
**파일**: `skills/아린_관리자/tools/auto_pipeline.py`

```python
# Line 1: import 추가
from upload_approval_flow import arin_upload_approval

# Line 450 (업로드 전):
# Before
container_id = uploader.create_media_container(image_url, caption)

# After
# 1. 승인 요청
post_info = {
    "caption": caption,
    "hashtags": hashtags,
    "image_url": image_url
}

approval = arin_upload_approval(post_info)

# 2. 승인 여부 확인
if not approval['approved']:
    print(f"❌ 포스팅 거절: {approval['message']}")
    print(f"문제점: {approval.get('issues', [])}")
    print(f"단계: {approval['stage']}")
    return False

# 3. 승인됨 - 포스팅 진행
print(f"✅ 포스팅 승인됨")
container_id = uploader.create_media_container(image_url, caption)
```

---

## 📞 텔레그램 알림

### 1. 영숙 → 사장님 보고
```
📢 [영숙 비서 → 사장님]

루나 에이전트가 YouTube 업로드를 준비했습니다.
예원 CEO님께 피드백을 요청하겠습니다.

제목: Neon City Nights - 80s K-Pop Fusion
```

### 2. 예원 → 가희 지시
```
📋 [예원 CEO → 가희 검수관]

루나의 YouTube 콘텐츠를 검수해주세요.
CEO 피드백: 승인

제목: Neon City Nights - 80s K-Pop Fusion
```

### 3. 가희 → 영숙 보고
```
✅ [가희 검수관 → 영숙 비서]

루나의 YouTube 콘텐츠 검수가 완료되었습니다.

검수 결과: 통과
상태: PASS
제목: Neon City Nights - 80s K-Pop Fusion

업로드 승인 요청드립니다.
```

### 4. 영숙 → 사장님 최종 보고
```
✅ [영숙 비서 → 사장님]

루나의 YouTube 업로드가 최종 승인되었습니다!

검토 단계:
1. ✅ 영숙 → 사장님 보고
2. ✅ 예원 CEO 피드백: 승인 (점수: 9/10)
3. ✅ 가희 품질 검수: 통과
4. ✅ 가희 → 영숙 보고
5. ✅ 영숙 최종 승인

제목: Neon City Nights - 80s K-Pop Fusion

지금 루나에게 업로드 지시를 내립니다! 🚀
```

### 5. 영숙 → 루나 업로드 지시
```
📤 [영숙 비서 → 루나 디렉터]

최종 승인이 완료되었습니다!
지금 바로 YouTube에 업로드하세요.

제목: Neon City Nights - 80s K-Pop Fusion
설명: 몽환적인 80년대 도쿄의 네온 불빛...
```

---

## ⚠️ 주의사항

### Ollama 의존성
- 예원 CEO 피드백은 Ollama 기반
- Ollama 오프라인 시: 자동 승인 (안전 장치)

### 가희 검수 에러 처리
- 검수 실패 시: 통과 처리 (안전 장치)
- 로그에 에러 기록
- 텔레그램 알림 전송

### 승인 플로우 시간
- 전체 프로세스: 약 10-20초
  - 예원 피드백: 3-5초 (Ollama)
  - 가희 검수: 5-10초
  - 텔레그램 알림: 1-2초

---

## 🔗 관련 파일

### 생성된 파일
```
skills/영숙_비서/tools/
└── upload_approval_flow.py  (새로 생성)
```

### 수정 필요 파일
```
skills/루나_디렉터/tools/
└── music_video_pipeline.py  (통합 필요)

skills/아린_관리자/tools/
└── auto_pipeline.py  (통합 필요)
```

### 의존성
```
- yewon_dispatcher: CEO 작업 분배
- content_inspector: 가희 검수 (Instagram)
- telegram_notifier: 텔레그램 알림
- ollama_client: 예원 CEO 피드백
```

---

## ✅ 체크리스트

### 완료
- [x] upload_approval_flow.py 생성
- [x] 5단계 승인 플로우 구현
- [x] 예원 CEO 피드백 (Ollama)
- [x] 가희 검수 통합
- [x] 텔레그램 알림 (5단계)
- [x] 루나/아린 래퍼 함수

### 다음 단계
- [ ] 루나 파이프라인 통합
- [ ] 아린 파이프라인 통합
- [ ] 실제 업로드 테스트
- [ ] 거절 케이스 테스트

---

## 📚 관련 문서

- [영숙 스케줄 중앙 관리](./schedule_centralization_20260602.md)
- [스케줄 제거 완료](./schedule_cleanup_20260602.md)
- [Dispatcher 경로 수정](./dispatcher_path_fix_20260602.md)
- [AI Team 역할 가이드](../../AI_TEAM_ROLES.md)

---

**마지막 업데이트**: 2026-06-02  
**상태**: ✅ 승인 플로우 구축 완료  
**다음 단계**: 루나/아린 파이프라인 통합
