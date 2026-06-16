# AI 팀 일일 자동화 설정 가이드

## 개요

Notion 리포트를 읽고 → Ollama로 분석 → 에이전트 자동 실행 → Notion에 결과 기록하는 완전 자동화 시스템입니다.

**컴퓨터를 바꿔도 동일하게 작동합니다!**

## 빠른 시작 (3단계)

### 1단계: 라이브러리 설치

```bash
pip install schedule
```

### 2단계: Ollama 설치 및 모델 다운로드

```bash
# Ollama 설치: https://ollama.com/download

# 모델 다운로드
ollama pull gemma2:9b
```

### 3단계: 자동화 시작

```bash
cd d:\ai_lab
python start_daily_automation.py
```

**완료!** 이제 매일 오전 9시에 자동으로 실행됩니다.

## 작동 방식

```
┌─────────────────────────────────────────┐
│  매일 09:00 자동 실행                    │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  1. Notion 리포트 읽기                   │
│     - 최근 10개 페이지 내용 추출         │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  2. Ollama로 분석                        │
│     - 리포트 내용 분석                   │
│     - 오늘의 작업 계획 수립              │
│     - 우선순위 결정                      │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  3. 에이전트 자동 실행                   │
│     - 루나: 뮤직비디오 생성              │
│     - 아린: 인스타그램 포스팅            │
│     - 텔레그램 알림 전송                 │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│  4. Notion에 결과 기록                   │
│     - 작업 완료 상태 업데이트            │
│     - 결과 URL 기록                      │
└─────────────────────────────────────────┘
```

## 백그라운드 실행

### Windows

```powershell
# 백그라운드 실행
Start-Process python -ArgumentList "start_daily_automation.py" -WindowStyle Hidden -WorkingDirectory "d:\ai_lab"

# 또는 최소화 상태로 실행
Start-Process python -ArgumentList "start_daily_automation.py" -WindowStyle Minimized -WorkingDirectory "d:\ai_lab"
```

### 시작프로그램 등록 (선택사항)

1. `Win + R` → `shell:startup` 입력
2. 바로가기 생성:
   - 대상: `C:\Python312\python.exe "d:\ai_lab\start_daily_automation.py"`
   - 시작 위치: `d:\ai_lab`
3. 컴퓨터 시작 시 자동 실행됨

## 설정 변경

### 실행 시각 변경

`start_daily_automation.py` 파일 수정:

```python
# 오전 9시 → 오후 6시로 변경
schedule.every().day.at("18:00").do(run_daily_task)

# 여러 시각 설정
schedule.every().day.at("09:00").do(run_daily_task)
schedule.every().day.at("18:00").do(run_daily_task)

# 매 시간마다
schedule.every().hour.do(run_daily_task)

# 30분마다
schedule.every(30).minutes.do(run_daily_task)
```

### 즉시 실행 (테스트)

`daily_ai_team_runner.py` 직접 실행:

```bash
python projects/ai-team/skills/daily_ai_team_runner.py
```

## Notion 설정

Notion API가 설정되어 있어야 합니다:

```bash
# .env.encrypted에 다음 변수 필요:
NOTION_API_KEY=ntn_xxxxx
NOTION_DATABASE_ID=xxxxx
```

설정 방법은 `NOTION_SETUP.md` 참고

## Ollama 설정

로컬 Ollama가 실행 중이어야 합니다:

```bash
# Ollama 상태 확인
ollama list

# 모델 다운로드 (필요시)
ollama pull gemma2:9b

# 또는
ollama pull llama3.1:8b
```

## 로그 확인

실행 중인 스케줄러 창에서 실시간 로그 확인:

```
============================================================
  AI 팀 일일 자동 실행
  2026-06-04 09:00:00
============================================================

[1/4] Notion 리포트 읽기...
  리포트 읽기 완료 (1,234 문자)

[2/4] Ollama로 작업 분석...
  요약: 루나 뮤직비디오 2개 생성, 아린 트렌드 포스팅
  작업 2개 식별

[3/4] 작업 실행...
============================================================
  [루나] 일일 뮤직비디오 생성 실행
============================================================
  ...

[4/4] Notion에 결과 기록...

============================================================
  완료: 2/2 성공
============================================================
```

## 텔레그램 알림

작업 시작/완료 시 자동으로 텔레그램 알림 전송:

```
🤖 AI 팀 일일 작업 시작

📋 루나 뮤직비디오 2개 생성, 아린 트렌드 포스팅
📌 작업: 2개

---

✅ [루나] 일일 뮤직비디오 생성
결과: 뮤직비디오 생성 완료
URL: https://youtu.be/xxx

---

📊 AI 팀 일일 작업 완료

✅ 성공: 2
❌ 실패: 0
⏰ 09:45
```

## 트러블슈팅

### 스케줄러가 실행되지 않음

1. Python 프로세스 확인:
   ```bash
   tasklist | findstr python
   ```

2. 수동 실행으로 오류 확인:
   ```bash
   python start_daily_automation.py
   ```

### Notion 읽기 실패

- `NOTION_API_KEY` 확인
- `NOTION_DATABASE_ID` 확인
- Integration 연결 상태 확인

### Ollama 연결 실패

```bash
# Ollama 실행 확인
curl http://localhost:11434

# 모델 확인
ollama list
```

### 에이전트 실행 실패

- 파이프라인 파일 경로 확인
- 환경변수 설정 확인 (GEMINI_API_KEY 등)
- 수동 실행으로 테스트:
  ```bash
  python projects/ai-team/skills/루나_디렉터/tools/music_video_pipeline.py
  ```

## 중단 방법

### 일시 중단

스케줄러 창에서 `Ctrl + C` 입력

### 완전 중단

1. 프로세스 찾기:
   ```bash
   tasklist | findstr python
   ```

2. 종료:
   ```bash
   taskkill /PID <프로세스_ID> /F
   ```

---

**설정 완료 후** 스케줄러를 실행하면 매일 자동으로 작업이 진행됩니다! 🚀

**컴퓨터 변경 시:**
1. `d:\ai_lab` 폴더 복사
2. `pip install schedule` 실행
3. Ollama 설치
4. `python start_daily_automation.py` 실행

끝!
