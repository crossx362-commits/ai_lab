# 원격 봇 제어 가이드

## 문제 상황
맥북과 윈도우에서 동시에 텔레그램 봇을 실행하면 **Telegram API 409 Conflict** 에러가 발생합니다.
한 번에 하나의 봇만 실행해야 합니다.

---

## 🚀 빠른 해결 방법 (추천)

### 방법 1: 맥북에서 직접 종료 (가장 확실함)
맥북 터미널에서 다음 명령 실행:
```bash
pkill -f telegram_receiver
```

### 방법 2: 집에 있는 사람에게 부탁
1. 맥북의 터미널(Terminal) 앱 열기
2. 위 명령어 복사해서 붙여넣기
3. Enter 누르기

### 방법 3: 맥북 슬립/종료
맥북을 슬립 모드나 종료 상태로 만들면 봇이 자동으로 종료됩니다.

---

## 🔧 SSH 원격 제어 설정 (한 번만 설정)

SSH를 설정하면 어디서든 원격으로 맥북 봇을 제어할 수 있습니다.

### 1단계: 맥북에서 SSH 활성화
1. **시스템 설정** → **일반** → **공유**
2. **원격 로그인** 켜기
3. 접근 권한 설정 (특정 사용자만 또는 모든 사용자)

### 2단계: SSH 키 생성 (비밀번호 없이 접속하려면)

**윈도우에서:**
```powershell
ssh-keygen -t ed25519 -f ~/.ssh/macbook_key
# Enter 3번 (비밀번호 없이)
```

**맥북으로 키 복사:**
```powershell
type ~/.ssh/macbook_key.pub | ssh user@macbook-ip "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys"
```

### 3단계: `.env` 파일 수정
`D:\ai_lab\.env` 파일에 추가:
```env
MACBOOK_SSH_HOST="사용자명@맥북IP주소"
# 예: MACBOOK_SSH_HOST="준@192.168.0.100"

MACBOOK_SSH_KEY_PATH="C:/Users/User/.ssh/macbook_key"
```

**맥북 IP 주소 확인 방법:**
- 맥북 터미널: `ifconfig | grep "inet " | grep -v 127.0.0.1`
- 또는 시스템 설정 → 네트워크 → Wi-Fi 상세정보

### 4단계: SSH 연결 테스트
```powershell
ssh -i ~/.ssh/macbook_key 사용자명@맥북IP
```
비밀번호 없이 연결되면 성공!

---

## 📱 텔레그램으로 봇 제어하기

SSH 설정 완료 후, 텔레그램에서 다음 명령어 사용:

### 기본 명령어
```
봇상태        - 모든 봇 상태 확인
봇종료        - 현재 머신의 봇 종료
봇시작        - 현재 머신의 봇 시작
봇재시작      - 현재 머신의 봇 재시작
맥북봇종료    - 원격으로 맥북 봇 종료 (SSH 필요)
맥북봇시작    - 원격으로 맥북 봇 시작 (SSH 필요)
```

### 영어 명령어도 지원
```
bot status
bot stop
bot start
bot restart
macbook stop
macbook start
```

---

## 🔍 문제 해결

### "SSH 연결 실패" 에러
1. 맥북이 켜져 있는지 확인
2. 맥북과 같은 Wi-Fi에 연결되어 있는지 확인
3. 맥북 방화벽 설정 확인 (SSH 허용)
4. `.env`의 IP 주소가 최신인지 확인

### 맥북 IP가 자주 바뀌는 경우
라우터에서 맥북에 **고정 IP 할당** 또는 **hostname 사용**:
```env
MACBOOK_SSH_HOST="준@준의-MacBook-Pro.local"
```

### 외부 네트워크에서 접속하려면
1. 공유기 포트포워딩 설정 (외부 포트 → 맥북 22번 포트)
2. 동적 DNS 서비스 사용 (No-IP, DuckDNS 등)
3. Tailscale/ZeroTier 같은 VPN 솔루션 사용 (추천)

---

## 🎯 권장 워크플로우

### 집에 있을 때
- 맥북 봇만 실행 (전력 효율적)

### 외출할 때
1. 텔레그램에서 `맥북봇종료` 전송
2. 윈도우 봇이 자동으로 연결 확보
3. 또는 수동으로 윈도우 봇 시작

### 집에 돌아올 때
1. 텔레그램에서 `봇종료` 전송 (윈도우 봇 종료)
2. 맥북 터미널에서 봇 시작:
   ```bash
   cd ~/ai_lab/projects/ai-team/skills/영숙_비서/tools
   nohup python3 telegram_receiver.py &
   ```

---

## 📝 스크립트 직접 실행

Python 스크립트로 직접 제어할 수도 있습니다:

```powershell
# Windows에서
cd D:\ai_lab\projects\ai-team\skills\영숙_비서\tools
python remote_bot_controller.py "봇상태"
python remote_bot_controller.py "맥북봇종료"
```

```bash
# MacBook에서
cd ~/ai_lab/projects/ai-team/skills/영숙_비서/tools
python3 remote_bot_controller.py "봇상태"
python3 remote_bot_controller.py "봇시작"
```

---

## ⚠️ 주의사항

1. **동시 실행 금지**: 맥북과 윈도우에서 동시에 봇을 실행하면 안 됩니다
2. **SSH 키 보안**: `~/.ssh/macbook_key` 파일은 외부에 공유하지 마세요
3. **방화벽 설정**: SSH 포트(22)를 외부에 열 때는 강력한 비밀번호 또는 키 인증 필수
4. **네트워크 변경**: IP가 바뀌면 `.env` 파일 업데이트 필요

---

## 📞 긴급 상황

### 봇이 응답하지 않을 때
1. 프로세스 강제 종료:
   - Windows: 작업 관리자에서 `pythonw.exe` 종료
   - MacBook: 활성 상태 보기에서 `Python` 종료
2. 로그 확인:
   ```
   D:\ai_lab\projects\ai-team\skills\영숙_비서\tools\telegram_receiver.log
   ```

### 모든 봇 인스턴스 로그아웃
Python으로 실행:
```python
import urllib.request, json, os
from _shared.env_loader import load_env
load_env()
token = os.getenv('TELEGRAM_BOT_TOKEN')
url = f'https://api.telegram.org/bot{token}/logOut'
urllib.request.urlopen(url).read()
print("All bots logged out!")
```

---

이제 외부에서도 텔레그램 명령어로 봇을 자유롭게 제어할 수 있습니다! 🎉
