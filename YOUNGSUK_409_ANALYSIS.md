# 영숙 Telegram API 409 Conflict 원인 분석

**분석 시각**: 2026-06-17 15:05

---

## 🔍 문제 증상

```
❌ Telegram API 실패 (getUpdates): HTTP Error 409: Conflict
```

지속적으로 반복 발생

---

## ✅ 원인 파악 완료

### 1. 중복 실행 발견

**두 개의 영숙 프로세스가 동시에 실행 중:**

```
PID 30280: pythonw (2026-06-17 오후 12:00:49 시작) ❌ 종료됨
PID 39340: pythonw (2026-06-17 오후 3:00:44 시작) ✅ 실행 중
```

### 2. Telegram API 409 Conflict 원인

**Telegram Bot API의 특성:**
- `getUpdates` long polling은 **한 번에 하나의 연결만** 허용
- 동일한 봇 토큰으로 여러 곳에서 동시에 `getUpdates` 호출 시 **409 Conflict** 발생

**현재 상황:**
```
PID 39340 (pythonw) → getUpdates 호출 중 ✅
다른 프로세스 → getUpdates 시도 → 409 Conflict ❌
```

### 3. Lock 파일 상태

**Lock 파일 위치:**
- `C:\Users\User\.ai-team-brain\.telegram_poll.lock`
- `C:\Users\User\.connect-ai-brain\.telegram_poll.lock`

**Lock 파일 내용:**
```json
{
  "pid": 39340,
  "owner": "youngsuk-python",
  "heartbeat": 1718608995000  (9초 전, ACTIVE)
}
```

**의미:**
- ✅ PID 39340이 정상적으로 Telegram API 소유 중
- ✅ Heartbeat가 15초마다 갱신 중
- ❌ 그런데도 409 오류 발생 → 또 다른 프로세스가 폴링 중

---

## 🐛 근본 원인

### 가설 1: IDE Extension이 동시 폴링 중

**증거:**
- Lock 파일이 `.ai-team-brain`, `.connect-ai-brain` 두 곳에 존재
- 이는 **IDE 확장 프로그램**이 Telegram을 폴링하는 것을 방지하기 위한 메커니즘
- 그런데도 409 발생 → IDE Extension이 Lock을 무시하고 폴링 중일 가능성

**확인 방법:**
```bash
# Telegram API 연결 확인
netstat -ano | grep "91.108"  # Telegram 서버 IP
```

**결과:**
```
TCP    172.31.10.59:57367     91.108.56.183:443      ESTABLISHED     45528
```

PID 45528이 Telegram 서버에 연결 중!

### 가설 2: 이전 프로세스의 Zombie Connection

**증거:**
- PID 30280이 종료되었지만, TCP 연결이 남아있을 수 있음
- Telegram API가 이전 연결을 아직 정리하지 않음

---

## 🔧 해결 방법

### 즉시 조치 (우선순위 높음)

#### 1. PID 45528 확인 및 종료
```powershell
# PID 45528이 무엇인지 확인
Get-Process -Id 45528 -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,Path

# Telegram 관련이면 종료
Stop-Process -Id 45528 -Force
```

#### 2. 모든 Python 프로세스 재시작
```bash
# 트레이딩 팀 전체 종료
python projects/ai-team/scripts/cleanup_duplicate_processes.py

# 영숙만 재시작
powershell -ExecutionPolicy Bypass .\projects\ai-team\skills\영숙_비서\tools\start_telegram_bot.ps1
```

#### 3. Lock 파일 확인
```bash
# Lock이 올바르게 설정되었는지 확인
cat ~/.ai-team-brain/.telegram_poll.lock
```

### 근본적 해결 (장기)

#### 1. IDE Extension Lock 체크 강화

현재 코드:
```python
def _hold_extension_telegram_lock():
    # 15초마다 lock 파일 갱신
    # 하지만 IDE가 이를 무시할 수 있음
```

개선안:
```python
def _check_telegram_conflicts():
    """다른 프로세스가 Telegram을 폴링 중인지 확인"""
    # 실제 409 오류 발생 시
    # 1. 모든 Python 프로세스 스캔
    # 2. Lock 파일 PID와 비교
    # 3. 불일치 시 경고 + 자동 종료
```

#### 2. Process Lock 적용

`process_lock.py` 사용:
```python
from _shared.process_lock import ProcessLock

def main():
    lock = ProcessLock("youngsuk_telegram_bot")
    if not lock.acquire():
        print("이미 실행 중입니다")
        return
    
    try:
        # 봇 실행
        run_bot()
    finally:
        lock.release()
```

#### 3. 409 에러 발생 시 자동 복구

```python
def get_updates(offset):
    try:
        return tg_api("getUpdates", {...})
    except HTTPError as e:
        if "409" in str(e):
            print("⚠️ 409 Conflict - 다른 연결 감지")
            print("5초 후 재시도...")
            time.sleep(5)
            return get_updates(offset)  # 재시도
```

---

## 📊 현재 상태

### 실행 중인 Python 프로세스
```
PID 11296: python3.12 (데이브 트레이더)
PID 26476: python3.12 (레오 트레이더)
PID 34240: python3.12 (트레이딩 팀 런처)
PID 44616: python3.12 (현빈 전략가)
PID 39340: pythonw   (영숙 텔레그램 봇) ✅
```

### Telegram 연결
```
PID 45528 → 91.108.56.183:443 (Telegram API)
```

**문제:** PID 45528이 누구인지 불명확!

---

## ✅ 다음 조치

1. ✅ **PID 45528 확인**
   ```powershell
   Get-Process -Id 45528
   ```

2. ✅ **충돌하는 프로세스 종료**

3. ✅ **영숙 재시작**

4. ✅ **409 오류 사라지는지 확인**

5. 📝 **Process Lock 적용 (장기)**

---

**분석 완료**
