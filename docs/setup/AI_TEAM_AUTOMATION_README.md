# AI 팀 자동화 시스템

## 개요

Notion 통합 리서치 리포트를 기반으로 AI 에이전트들이 자동으로 작업을 읽고 실행하며, 결과를 Notion에 기록하는 완전 자동화 시스템입니다.

## 아키텍처

```
┌─────────────────────────────────────────────────────┐
│          Notion 통합 리서치 리포트                    │
│  (작업 목록, 우선순위, 상태 관리)                      │
└─────────────────┬───────────────────────────────────┘
                  │
                  │ 작업 조회/상태 업데이트
                  │
┌─────────────────▼───────────────────────────────────┐
│           AI 팀 자동 작업 스케줄러                    │
│         (ai_team_scheduler.py)                       │
│                                                      │
│  • 30분마다 Notion에서 작업 확인                      │
│  • 우선순위 기반 작업 할당                             │
│  • 에이전트 파이프라인 실행                            │
│  • 결과를 Notion에 기록                               │
└─────────────────┬───────────────────────────────────┘
                  │
        ┌─────────┼─────────┬─────────┐
        │         │         │         │
┌───────▼───┐ ┌──▼──┐ ┌───▼───┐ ┌───▼───┐
│   루나    │ │ 아린 │ │ 가희  │ │ 경수  │
│(뮤직비디오)│ │(인스타)│ │(검수) │ │(감사) │
└───────────┘ └─────┘ └───────┘ └───────┘
     │           │        │         │
     └───────────┴────────┴─────────┘
                 │
                 ▼
      ┌──────────────────────┐
      │   Notion 리포트 작성  │
      │  • 작업 완료 상태     │
      │  • 결과 URL          │
      │  • 완료 시각          │
      └──────────────────────┘
```

## 설정 방법

### 1. Notion Integration 설정

상세 가이드: [`NOTION_SETUP.md`](./NOTION_SETUP.md)

**요약:**
1. https://www.notion.so/my-integrations 에서 Integration 생성
2. Notion 데이터베이스 생성 (템플릿 제공)
3. 데이터베이스를 Integration과 공유
4. 환경변수 설정:
   ```bash
   NOTION_TOKEN=secret_xxxxx
   NOTION_REPORT_DB_ID=xxxxx
   ```

### 2. 환경변수 암호화

```bash
# .env 파일 수정
notepad .env

# 재암호화
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted
```

### 3. 스케줄러 테스트

```bash
# 한 번만 실행 (테스트)
python projects/ai-team/skills/ai_team_scheduler.py --once

# 30분 주기로 계속 실행
python projects/ai-team/skills/ai_team_scheduler.py --interval 30
```

## 사용 방법

### Notion에서 작업 추가

1. Notion 데이터베이스에 새 행 추가
2. 필수 필드 입력:
   - **Name**: 작업 제목
   - **Agent**: 담당 에이전트 (루나/아린/가희/경수)
   - **Status**: `Not started`
   - **Priority**: High/Medium/Low
   - **Description**: 작업 상세 설명

3. 저장하면 스케줄러가 자동으로 감지하여 실행

### 작업 예시

| Name | Agent | Status | Priority | Description |
|------|-------|--------|----------|-------------|
| 시티팝 뮤직비디오 제작 | 루나 | Not started | High | Imagen 4.0으로 5개 파트 이미지 생성, Lyria 3 Pro 음악, 19:00 예약 업로드 |
| 여름 트렌드 인스타 포스팅 | 아린 | Not started | Medium | 구글 트렌드 기반 이미지 생성 및 인스타그램 업로드 |
| 최근 영상 품질 검수 | 가희 | Not started | High | YouTube 메타데이터 및 썸네일 품질 검증 |

## 에이전트별 자동 실행

### 루나 (뮤직비디오)

**실행 파이프라인:** `skills/루나_디렉터/tools/music_video_pipeline.py`

**작업 흐름:**
1. 테마 선택
2. 음악 프롬프트 생성
3. Lyria 3 Pro 완곡 생성 (2분 이상)
4. Imagen 4.0 비주얼 5개 생성
5. 비디오 합성 (1280x720)
6. 가희 사전 검수
7. YouTube 예약 업로드
8. **Notion 리포트 작성**

**Notion 리포트 내용:**
- 제목, 영상 길이
- 이미지 생성 방식 (Imagen 4.0)
- 음악 생성 방식 (Lyria 3 Pro)
- YouTube URL
- 예약 업로드 시각

### 아린 (인스타그램)

**실행 파이프라인:** `skills/아린_관리자/tools/auto_pipeline.py`

**작업 흐름:**
1. 구글 트렌드 수집
2. 키워드 선택 및 프롬프트 생성
3. Imagen 4.0 이미지 생성
4. 캡션 자동 생성
5. 가희 & 경수 검수
6. 인스타그램 업로드
7. **Notion 리포트 작성**

### 가희 (품질 검수)

**실행:** 수동 트리거 또는 검수 도구 실행

**검수 항목:**
- YouTube 메타데이터 검증
- 썸네일 품질 확인
- 제목/설명 최적화 여부
- 금지 키워드 체크

## 모니터링

### 텔레그램 알림

모든 작업 시작/완료/실패 시 텔레그램으로 실시간 알림:

```
🤖 [루나] 작업 시작
제목: 시티팝 뮤직비디오 제작
설명: Imagen 4.0으로 고품질 영상 생성

✅ [루나] 작업 완료
제목: 시티팝 뮤직비디오 제작
결과: 뮤직비디오 생성 완료
URL: https://youtu.be/xxx
```

### Notion 대시보드

Notion 데이터베이스에서 실시간 작업 상태 확인:

**보기 추천:**
- **대기 중**: Status = "Not started"
- **진행 중**: Status = "In progress"
- **완료**: Status = "Done", 완료일 기준 정렬
- **실패**: Status = "Failed"

## 고급 기능

### 수동으로 작업 생성

```python
from _shared.notion_report_manager import NotionReportManager

manager = NotionReportManager()

# 리포트 항목 생성
manager.create_report_entry(
    agent_name="루나",
    task_title="긴급 뮤직비디오 생성",
    result="Imagen 4.0, 170초, 1280x720",
    metadata={
        "url": "https://youtu.be/xxx",
        "priority": "High"
    }
)
```

### 프로그래밍 방식 작업 조회

```python
from _shared.notion_report_manager import get_my_tasks

# 루나의 대기 작업
tasks = get_my_tasks("루나")

for task in tasks:
    print(f"작업: {task['title']}")
    print(f"우선순위: {task['priority']}")
    print(f"설명: {task['description']}")
```

## Windows 서비스로 실행 (선택)

스케줄러를 백그라운드 서비스로 실행:

```powershell
# NSSM 다운로드 및 설치
# https://nssm.cc/download

# 서비스 생성
nssm install AITeamScheduler "C:\Python312\python.exe" "d:\ai_lab\projects\ai-team\skills\ai_team_scheduler.py --interval 30"

# 서비스 시작
nssm start AITeamScheduler

# 서비스 상태 확인
nssm status AITeamScheduler
```

## 트러블슈팅

### Notion 연동 실패

```bash
# 환경변수 확인
python -c "import os; from _shared.env_loader import load_env; load_env(); print('NOTION_TOKEN:', 'OK' if os.getenv('NOTION_TOKEN') else 'MISSING')"

# Notion 연동 테스트
python projects/ai-team/_shared/notion_report_manager.py
```

### 작업이 실행되지 않음

1. Notion 데이터베이스 공유 확인 (Integration 연결됨?)
2. Status가 "Not started"인지 확인
3. Agent 필드가 정확한지 확인 (루나/아린/가희/경수)
4. 스케줄러 로그 확인

### 작업 실행 중 오류

- 텔레그램 알림 확인
- Notion의 Result 필드에 오류 메시지 기록됨
- 수동 실행으로 디버깅:
  ```bash
  python projects/ai-team/skills/루나_디렉터/tools/music_video_pipeline.py
  ```

## 보안

- ✅ 모든 API 키는 암호화되어 저장 (`.env.encrypted`)
- ✅ Notion Integration은 특정 데이터베이스만 접근
- ✅ 각 에이전트는 독립적으로 실행
- ✅ 텔레그램 봇 토큰 암호화

## 확장

새로운 에이전트 추가:

1. `AGENT_EXECUTORS`에 매핑 추가 (`ai_team_scheduler.py`)
2. `execute_agent_task()` 함수에 실행 로직 추가
3. 에이전트 파이프라인 끝에 Notion 리포트 작성 코드 추가

---

**마지막 업데이트:** 2026-06-04
**문서 버전:** 1.0
