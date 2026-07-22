# Notion 통합 설정

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

리포트를 쌓을 데이터베이스를 하나 만들고, 최소 `Name`(Title) 속성만 있으면 된다.
필요에 따라 `Status`·`Date` 등 속성을 자유롭게 추가해도 무방하다 —
`_shared/research.py`의 `notion_page()`/`notion_report()`는 title 속성만 자동 탐지해 사용한다.

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
NOTION_API_KEY=secret_xxxxxxxxxxxxxxxxxxxxxxxxxx
NOTION_DATABASE_ID=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

## 6. 환경변수 암호화 (선택)

```bash
python projects/ai-team/_shared/env.py encrypt .env .env.encrypted
```

## 7. 사용 방법

```python
from _shared.research import notion_page, notion_report

# 짧은 불릿 리포트
notion_page("오늘의 QA 순찰 요약", ["콘솔 오류 0건", "접근성 이슈 1건 발견"])

# 긴 텍스트 리포트 (페이지 URL 반환)
url = notion_report("주간 파이프라인 감사", "상세 내용...\n2번째 줄...")
```

---

현재 `report-writer` 스킬(영숙 비서 플러그인)과 `reports_manager.py`가 이 함수들을 사용해
텔레그램 요약 + 노션 풀 리포트를 작성한다.
