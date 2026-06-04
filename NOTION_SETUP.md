# Notion 통합 리서치 리포트 설정

## 1. Notion Integration 생성

1. **Notion Integrations 페이지** 접속: https://www.notion.so/my-integrations
2. **"+ New integration"** 클릭
3. 정보 입력:
   - Name: `AI Team Reporter`
   - Associated workspace: 본인의 워크스페이스 선택
   - Type: `Internal integration`
4. **"Submit"** 클릭
5. **Internal Integration Token** 복사 (secret_xxx 형식)

## 2. Notion 데이터베이스 생성

### 데이터베이스 구조:

| Property | Type | Options |
|----------|------|---------|
| Name | Title | - |
| Agent | Select | 루나, 아린, 가희, 경수, 예원, 영숙 |
| Status | Status | Not started, In progress, Done, Failed |
| Priority | Select | High, Medium, Low |
| Description | Text | - |
| Result | Text | - |
| Deadline | Date | - |
| Completed | Date | - |
| URL | URL | - |

### 생성 방법:

1. Notion에서 **새 페이지** 생성
2. **"Table - Full page"** 선택
3. 제목: `AI 팀 통합 리서치 리포트`
4. 위 표에 맞게 속성(Property) 추가

## 3. 데이터베이스 공유

1. 생성한 데이터베이스 페이지 우측 상단 **"Share"** 클릭
2. **"Invite"** 검색창에 `AI Team Reporter` 입력
3. Integration 선택 후 **"Invite"** 클릭

## 4. 데이터베이스 ID 확인

1. 데이터베이스 페이지 URL 복사
2. URL 형식: `https://www.notion.so/{workspace}/{database_id}?v=...`
3. `{database_id}` 부분 복사 (32자 길이)

## 5. 환경변수 설정

`.env` 파일에 추가:

```bash
# Notion Integration
NOTION_TOKEN=secret_xxxxxxxxxxxxxxxxxxxxxxxxxx
NOTION_REPORT_DB_ID=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

또는 별칭 사용:
```bash
NOTION_API_KEY=secret_xxxxxxxxxxxxxxxxxxxxxxxxxx
NOTION_DATABASE_ID=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

## 6. 환경변수 암호화

```bash
# 암호화
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted
```

## 7. 테스트

```bash
python projects/ai-team/_shared/notion_report_manager.py
```

성공 시:
```
=== Notion 연동 테스트 ===

루나 대기 작업: 0개
```

## 8. 샘플 작업 추가

데이터베이스에 샘플 작업 추가:

| Name | Agent | Status | Priority | Description |
|------|-------|--------|----------|-------------|
| 새로운 시티팝 뮤직비디오 생성 | 루나 | Not started | High | Imagen 4.0으로 고품질 영상 제작 |
| 인스타그램 트렌드 포스팅 | 아린 | Not started | Medium | 최신 트렌드 기반 이미지 생성 및 업로드 |
| 유튜브 영상 품질 검수 | 가희 | Not started | High | 최근 업로드된 영상 메타데이터 검증 |

## 사용 방법

### 에이전트에서 작업 조회

```python
from _shared.notion_report_manager import get_my_tasks

# 내 작업 가져오기
tasks = get_my_tasks("루나")

for task in tasks:
    print(f"작업: {task['title']}")
    print(f"설명: {task['description']}")
```

### 작업 완료 보고

```python
from _shared.notion_report_manager import report_task_done

# 작업 완료
result = "Imagen 4.0으로 5개 파트 이미지 생성, YouTube 업로드 완료. URL: https://youtu.be/xxx"
report_task_done(task_id, result)
```

### 자동 리포트 생성

```python
from _shared.notion_report_manager import NotionReportManager

manager = NotionReportManager()
manager.create_report_entry(
    agent_name="루나",
    task_title="뮤직비디오 생성 완료",
    result="Imagen 4.0, 170초, 1280x720",
    metadata={"url": "https://youtu.be/xxx", "priority": "High"}
)
```

---

**설정 완료 후** 각 에이전트 파이프라인이 자동으로 Notion에서 작업을 읽고 결과를 기록합니다.
