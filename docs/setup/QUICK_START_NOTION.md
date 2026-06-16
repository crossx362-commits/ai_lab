# Notion 자동화 빠른 시작 가이드

## 5분 안에 설정하기

### 1단계: Notion Integration 생성 (2분)

1. **https://www.notion.so/my-integrations** 접속
2. **"+ New integration"** 클릭
3. 이름: `AI Team Reporter` 입력
4. **Submit** → **Copy** 버튼으로 토큰 복사 (`secret_xxx...`)

### 2단계: 데이터베이스 템플릿 복사 (1분)

아래 Notion 템플릿을 복사하거나, 수동으로 데이터베이스를 만드세요:

**템플릿 URL:** (사용자가 만든 Notion 페이지 공유 URL)

**또는 수동 생성:**
1. Notion에서 새 페이지 생성
2. `/table` 입력 → "Table - Full page" 선택
3. 제목: `AI 팀 통합 리서치 리포트`

### 3단계: 속성(Property) 추가 (1분)

데이터베이스에 다음 속성 추가:

| Property | Type | Options |
|----------|------|---------|
| Name | Title | (기본) |
| Agent | Select | 루나, 아린 |
| Status | Status | Not started, In progress, Done, Failed |
| Priority | Select | High, Medium, Low |
| Description | Text | - |
| Result | Text | - |
| URL | URL | - |

### 4단계: Integration 연결 (30초)

1. 데이터베이스 페이지에서 우측 상단 **"⋯"** 클릭
2. **"Connect to"** → `AI Team Reporter` 선택

### 5단계: 데이터베이스 ID 복사 (30초)

1. 데이터베이스 페이지 URL 복사
2. URL에서 32자 ID 추출:
   ```
   https://www.notion.so/{workspace}/{이_부분_복사}?v=...
                                    ^^^^^^^^^^^^^^^^^^^^
   ```

### 6단계: 환경변수 설정 (1분)

```bash
# 1. .env.encrypted를 복호화
cd d:/ai_lab
python projects/ai-team/_shared/env_crypto.py decrypt .env.encrypted .env

# 2. .env 파일 열기
notepad .env

# 3. 아래 두 줄 추가
NOTION_TOKEN=secret_여기에_복사한_토큰
NOTION_REPORT_DB_ID=여기에_복사한_데이터베이스_ID

# 4. 저장 후 재암호화
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted

# 5. 평문 .env 파일 삭제 (선택)
del .env
```

### 7단계: 테스트 (30초)

```bash
# Notion 연동 테스트
python projects/ai-team/_shared/notion_report_manager.py
```

**성공 시 출력:**
```
=== Notion 연동 테스트 ===

루나 대기 작업: 0개
```

## 첫 작업 추가하기

Notion 데이터베이스에 다음 작업을 추가하세요:

| Name | Agent | Status | Priority | Description |
|------|-------|--------|----------|-------------|
| 테스트 뮤직비디오 | 루나 | Not started | Medium | 시스템 테스트용 뮤직비디오 생성 |

## 스케줄러 실행

```bash
# 한 번만 실행 (테스트)
python projects/ai-team/skills/ai_team_scheduler.py --once
```

**예상 출력:**
```
============================================================
  AI 팀 자동 작업 스케줄러 시작
============================================================
  확인 주기: 30분
  Notion 연동: 활성화
============================================================

[2026-06-04 15:30:00] 반복 1
  [루나] 대기 작업 1개 발견

============================================================
  [루나] 작업 실행: 테스트 뮤직비디오
============================================================
  파이프라인 실행: ...
  ...
  ✅ 작업 완료: 테스트 뮤직비디오
```

Notion에서 **Status**가 `Done`으로 변경되고, **Result** 필드에 결과가 기록됩니다!

## 자동 실행 (백그라운드)

### Windows PowerShell

```powershell
# 백그라운드에서 30분마다 실행
Start-Process python -ArgumentList "projects/ai-team/skills/ai_team_scheduler.py --interval 30" -WindowStyle Hidden
```

### 또는 Windows Task Scheduler

1. **작업 스케줄러** 열기
2. **기본 작업 만들기**
3. 이름: `AI Team Scheduler`
4. 트리거: **컴퓨터가 시작할 때**
5. 동작: **프로그램 시작**
   - 프로그램: `C:\Python312\python.exe`
   - 인수: `d:\ai_lab\projects\ai-team\skills\ai_team_scheduler.py --interval 30`
   - 시작 위치: `d:\ai_lab`

## 트러블슈팅

### "NOTION_TOKEN 없음" 오류

```bash
# 환경변수 로드 확인
python -c "import os; import sys; sys.path.insert(0, 'projects/ai-team'); from _shared.env_loader import load_env; load_env(); print('Token:', 'OK' if os.getenv('NOTION_TOKEN') else 'MISSING')"
```

### 작업이 감지되지 않음

1. Notion에서 **Status**가 `Not started`인지 확인
2. **Agent** 필드가 정확한지 확인 (루나/아린)
3. Integration이 데이터베이스에 연결되었는지 확인

### Integration 연결 확인

Notion 데이터베이스 페이지에서:
- 우측 상단 **"⋯"** 클릭
- **"Connections"** 확인
- `AI Team Reporter`가 있어야 함

---

**설정 완료!** 🎉

이제 Notion에 작업을 추가하면 에이전트들이 자동으로 실행합니다!
