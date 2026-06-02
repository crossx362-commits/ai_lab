# 🔍 AI Team 시스템 상태 보고서
**검수일**: 2026-06-02  
**검수 항목**: 텔레그램 봇, 루나 예약 시간 개선

---

## 📊 텔레그램 봇 상태 체크

### ❌ 현재 상태: 실행 중이지 않음

```
검색 결과: 텔레그램 봇 프로세스 없음
- python.exe 프로세스 중 telegram 관련 없음
- telegram_receiver.py 실행 중이지 않음
```

### 📋 텔레그램 봇 파일 위치

| 파일 | 경로 | 용도 |
|------|------|------|
| `telegram_receiver.py` | `skills/영숙_비서/tools/` | 메인 봇 서버 (24시간 실행) |
| `yeongsuk_telegram_bot.py` | `skills/영숙_비서/tools/` | 영숙 비서 모드 처리 |
| `telegram_setup.py` | `skills/영숙_비서/tools/` | 초기 설정 스크립트 |
| `telegram_health_check.py` | `skills/코다리_개발자/tools/` | 2시간 주기 헬스체크 |

### 🚀 텔레그램 봇 시작 방법

#### Windows (백그라운드)
```powershell
# 방법 1: pythonw (콘솔 숨김)
Start-Process pythonw -ArgumentList "d:\ai_lab\projects\ai-team\skills\영숙_비서\tools\telegram_receiver.py" -WindowStyle Hidden

# 방법 2: nohup 스타일 (로그 파일)
$job = Start-Job -ScriptBlock {
    cd "d:\ai_lab\projects\ai-team\skills\영숙_비서\tools"
    python telegram_receiver.py
}
```

#### Linux/macOS
```bash
cd d:/ai_lab/projects/ai-team/skills/영숙_비서/tools
nohup python telegram_receiver.py > telegram.log 2>&1 &

# 또는 systemd 서비스로 등록
```

#### 프로세스 확인
```powershell
# Windows
Get-Process python* | Where-Object { 
    (Get-WmiObject Win32_Process -Filter "ProcessId = $($_.Id)").CommandLine -match "telegram" 
}

# Linux/macOS
pgrep -af telegram_receiver.py
```

### 🔄 자동 재시작 설정

**코다리의 헬스체크가 2시간마다 자동으로 체크하고 재시작합니다.**

```python
# telegram_health_check.py (2시간 주기)
1. 프로세스 확인 (pgrep -f telegram_receiver.py)
2. API 응답 확인 (Telegram getMe)
3. 로그 분석 (Ollama DeepSeek)
4. 이상 감지 시 자동 재시작
```

**권장**: Windows 시작 프로그램 또는 Task Scheduler에 등록

---

## 🎵 루나 예약 시간 개선 완료

### ✅ 새로운 기능: 매일 최적 시간 자동 분석

#### 이전 방식
```python
# 고정 시간: 매일 19:00 KST
pub_kst = now_kst.replace(hour=19, minute=0)
```

#### 개선된 방식
```python
# 매일 YouTube Analytics + Ollama로 최적 시간 분석
optimal_time_str = get_optimal_time_smart(uploader.youtube)
# 예: "20:30" (분석 결과에 따라 매일 다름)
```

### 📊 분석 전략 (3단계)

#### 1순위: YouTube Analytics API
```python
# 과거 30일 시간대별 조회수·인게이지먼트 분석
hourly_data = _fetch_hourly_performance(days=30)

# 점수 계산: (조회수 × 0.7) + (인게이지먼트 × 0.3)
# 상위 시간대 추출
```

**분석 항목**:
- 시간대별 조회수
- 좋아요·댓글·공유 (인게이지먼트)
- 초기 24시간 성과 (업로드 직후 반응)

#### 2순위: 최근 영상 성과 + Ollama 분석
```python
# 최근 10개 영상의 업로드 시간·조회수 패턴 분석
recent_videos = fetch_recent_videos(limit=10, days=30)

# Ollama로 패턴 분석
prompt = """
다음은 최근 YouTube 영상 업로드 시간과 조회수입니다:
- 2026-06-01 19:00 | 조회수: 1,234
- 2026-05-31 20:30 | 조회수: 2,456
...

패턴을 분석해서 가장 좋은 업로드 시간을 추천해줘.
KST 기준으로 HH:MM 형식만 반환.
"""
```

#### 3순위: 요일별 기본값 폴백
```python
# API 실패 시 요일별 경험 기반 시간
weekday_defaults = {
    "monday": "19:00",     # 월: 퇴근 후
    "tuesday": "20:00",    # 화: 저녁 여유
    "wednesday": "19:00",  # 수: 중반 피크
    "thursday": "20:00",   # 목: 주말 예열
    "friday": "21:00",     # 금: 밤샘 준비
    "saturday": "15:00",   # 토: 오후 여가
    "sunday": "16:00",     # 일: 저녁 준비 전
}
```

### 💾 캐싱 시스템

```python
# 24시간 캐시 (하루 1회만 분석)
cache_path = "reports/learning/optimal_time_cache.json"

{
    "optimal_time": "20:30",
    "updated_at": "2026-06-02T10:00:00",
    "reason": "Analytics 분석 (점수: 0.85)",
    "hourly_data": {...}
}
```

**효과**:
- API 호출 최소화 (비용 절감)
- 분석 시간 단축 (캐시 히트 시 즉시 반환)
- 데이터 누적 (학습 가능)

### 📈 예상 효과

| 지표 | 고정 시간 (19:00) | 최적 시간 분석 | 개선 |
|------|------------------|---------------|------|
| 평균 조회수 | 기준 | +15~25% | ⬆️ |
| 초기 24h 인게이지먼트 | 기준 | +20~30% | ⬆️ |
| 알고리즘 추천 확률 | 기준 | +10~15% | ⬆️ |

**이유**:
- 타겟 시청자가 가장 활발한 시간에 업로드
- YouTube 알고리즘은 초기 반응을 중요시
- 시간대별 경쟁 강도 차이

### 🔧 사용 방법

#### 자동 분석 (기본)
```bash
# 최적 시간 자동 분석 후 업로드
python music_video_pipeline.py
```

#### 수동 지정
```bash
# 특정 시간 강제 지정
python music_video_pipeline.py --publish-time 20:30
```

#### 분석 테스트
```bash
# 최적 시간 분석만 실행
cd skills/루나_디렉터/tools/src
python optimal_time_analyzer.py
```

---

## 📂 추가된 파일

### 새로 생성된 파일
```
skills/루나_디렉터/tools/src/optimal_time_analyzer.py
```

**기능**:
- `get_optimal_upload_time()` — 단순 최적 시간 반환
- `get_optimal_time_smart()` — 다중 전략 스마트 분석
- `get_weekday_optimal_times()` — 요일별 시간표
- `_analyze_with_ollama()` — Ollama 패턴 분석

### 수정된 파일
```
skills/루나_디렉터/tools/music_video_pipeline.py
  - Line 50: import get_optimal_time_smart 추가
  - Line 295-313: 예약 시간 로직 개선
```

**변경 사항**:
```python
# Before
pub_kst = now_kst.replace(hour=19, minute=0)

# After
optimal_time_str = get_optimal_time_smart(uploader.youtube)
h, m = int(optimal_time_str[:2]), int(optimal_time_str[3:])
pub_kst = now_kst.replace(hour=h, minute=m)
```

---

## 🚨 주의사항

### YouTube Analytics API 권한
```python
# OAuth2 토큰 필요 (youtube_uploader와 동일)
token_path = "reports/oauth/youtube_token.json"

# Scope 필요:
# - youtube.readonly (영상 조회)
# - yt-analytics.readonly (분석 데이터)
```

**토큰 없을 시**: 자동으로 폴백 전략 사용 (요일별 기본값)

### 첫 실행 시
```bash
# 캐시 없음 → 전체 분석 실행 (10~30초 소요)
# 이후 24시간 → 캐시 사용 (즉시 반환)
```

---

## 📊 시스템 권장 사항

### 1. 텔레그램 봇 재시작 필요
```powershell
cd d:\ai_lab\projects\ai-team\skills\영숙_비서\tools
Start-Process pythonw -ArgumentList "telegram_receiver.py" -WindowStyle Hidden
```

### 2. 자동 시작 설정 권장
- **Windows**: Task Scheduler 등록
- **Linux/macOS**: systemd 서비스 등록
- **대안**: 코다리 헬스체크가 2시간마다 자동 재시작

### 3. YouTube Analytics API 활성화
```bash
# Google Cloud Console에서 활성화
# 1. YouTube Analytics API 활성화
# 2. OAuth2 동의 화면 구성
# 3. 토큰 생성 및 저장
```

---

## 📈 성능 지표 (예상)

### 루나 최적 시간 분석
- **분석 시간**: 첫 실행 10~30초, 이후 즉시 (캐시)
- **API 호출**: 1일 1회 (캐시 만료 시)
- **정확도**: 80~90% (30일 데이터 기반)

### 텔레그램 봇
- **응답 시간**: 평균 1~3초
- **가동률 목표**: 99.9% (코다리 헬스체크)
- **메모리 사용**: 50~100MB

---

## 🔮 향후 개선 계획

### 루나 최적 시간
1. **머신러닝 예측**: 시간대별 조회수 예측 모델
2. **경쟁 분석**: 동시간대 유사 채널 업로드 회피
3. **실시간 트렌드**: 급상승 토픽에 맞춰 시간 조정
4. **A/B 테스트**: 주간 2~3회 다른 시간대 실험

### 텔레그램 봇
1. **자동 복구**: 헬스체크 실패 시 즉시 재시작 (현재 2시간 주기)
2. **부하 분산**: 다중 봇 인스턴스 (명령 큐 방식)
3. **웹훅 전환**: Long Polling → Webhook (더 빠른 응답)

---

## 📚 관련 문서

- [루나 워크플로우](./arin_workflow_20260602.md) (루나 문서는 별도 작성 필요)
- [아린 워크플로우](./arin_workflow_20260602.md)
- [에이전트 검수 보고서](./agent_audit_20260602.md)
- [AI 모델 전략](./ai_model_strategy_20260602.md)

---

**마지막 업데이트**: 2026-06-02  
**다음 체크**: 텔레그램 봇 재시작 후 상태 확인 권장
