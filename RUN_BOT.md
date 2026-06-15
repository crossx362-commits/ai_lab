# 텔레그램 봇 실행 가이드

## ✅ 봇 실행 방법

### 1. 터미널 열기
- Git Bash 또는 PowerShell 실행

### 2. 봇 실행
```bash
cd d:/ai_lab
python simple_bot.py
```

### 3. 실행 확인
다음과 같은 메시지가 보이면 성공:
```
============================================================
SIMPLE BOT STARTING
============================================================
Importing Gemini...
Creating client...
Deleting webhook...
Sent: Bot started - single instance!

Listening for messages...
```

### 4. 텔레그램에서 메시지 보내기
- "안녕"
- "테스트"
- "현황"

### 5. 봇 종료
- `Ctrl + C` 키를 누르면 봇이 종료됩니다

## 🔧 문제 해결

### 봇이 즉시 종료되는 경우
```bash
python simple_bot.py
```
실행 후 오류 메시지를 확인하세요.

### 여러 번 응답하는 경우
다른 봇 인스턴스가 실행 중입니다. 모두 종료:
```bash
# PowerShell
Get-Process python | Stop-Process -Force

# 또는 Bash
pkill python
```

## 📝 봇 파일 위치
- `d:/ai_lab/simple_bot.py` - 가장 간단한 버전 (권장)
- `d:/ai_lab/bot.py` - 최소 버전
- `d:/ai_lab/yeongsuk_bot.py` - UTF-8 버전

## ✅ 성공!
봇이 정상 실행되면 텔레그램에서 메시지를 보낼 때마다:
```
>>> Received: 안녕
Question: 안녕
Answer: 안녕하세요!
Sent: 안녕하세요!
```
이런 로그가 터미널에 출력됩니다.
