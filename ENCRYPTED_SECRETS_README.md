# 🔐 암호화된 환경 변수 관리 가이드

**작성일**: 2026-06-04  
**보안**: 중요 - 비밀번호는 안전하게 보관하세요

---

## 📋 개요

이 프로젝트는 **모든 환경 변수와 자격 증명을 암호화**하여 Git에 안전하게 저장합니다.

### 암호화된 파일

| 원본 파일 | 암호화된 파일 | 용도 |
|-----------|--------------|------|
| `.env` | `.env.encrypted` | 환경 변수 (API 키, 토큰 등) |
| `client_secret.json` | `client_secret.json.encrypted` | YouTube OAuth 자격 증명 |

### Git 관리

```
✅ Git에 포함 (안전):
   - .env.encrypted
   - client_secret.json.encrypted
   - encrypt_all_secrets.py
   - decrypt_all_secrets.py
   - .gitignore

❌ Git에서 제외 (.gitignore):
   - .env
   - client_secret.json
   - .env.key
   - *.pickle
   - youtube_token.pickle
```

---

## 🚀 빠른 시작

### 1. 저장소 클론 후 복호화

```bash
# 저장소 클론
git clone <repository-url>
cd ai_lab

# 암호화된 파일 복호화
python decrypt_all_secrets.py
```

**출력**:
```
✅ .env.encrypted → .env (5,595 bytes)
✅ client_secret.json.encrypted → client_secret.json (414 bytes)
```

### 2. 복호화 확인

```bash
# 파일 생성 확인
ls -la .env client_secret.json

# 환경 변수 테스트
python -c "from _shared.env_loader import load_env; load_env(); import os; print('API Key:', 'SET' if os.getenv('GEMINI_API_KEY') else 'NOT SET')"
```

---

## 🔧 사용 방법

### 암호화 (환경 변수 변경 후)

```bash
# 1. .env 또는 client_secret.json 수정
nano .env

# 2. 암호화 실행
python encrypt_all_secrets.py

# 3. Git에 커밋
git add .env.encrypted client_secret.json.encrypted
git commit -m "chore: update encrypted environment variables"
git push
```

### 복호화 (새 환경 설정 시)

```bash
# 최신 암호화 파일 가져오기
git pull

# 복호화
python decrypt_all_secrets.py

# 확인
ls -la .env client_secret.json
```

---

## 🔐 보안

### 암호화 방식

- **알고리즘**: Fernet (symmetric encryption)
- **키 유도**: PBKDF2-HMAC-SHA256
- **반복 횟수**: 100,000 iterations
- **솔트**: 파일마다 랜덤 생성 (16 bytes)

### 비밀번호

**기본 비밀번호**: `ai_lab_secure_env_2026`

**⚠️ 주의**:
- 이 비밀번호는 코드에 하드코딩되어 있습니다
- 프로덕션 환경에서는 환경 변수로 관리하세요
- 팀원에게만 공유하고 공개하지 마세요

### 비밀번호 변경 방법

1. `encrypt_all_secrets.py`와 `decrypt_all_secrets.py`의 `PASSWORD` 변수 수정
2. 모든 파일 재암호화:
   ```bash
   python encrypt_all_secrets.py
   git add .env.encrypted client_secret.json.encrypted
   git commit -m "chore: re-encrypt with new password"
   ```

---

## 📁 파일 구조

```
d:\ai_lab\
├── .env                              # ❌ Git 제외 (원본)
├── .env.encrypted                    # ✅ Git 포함
├── client_secret.json                # ❌ Git 제외 (원본)
├── client_secret.json.encrypted      # ✅ Git 포함
├── encrypt_all_secrets.py            # ✅ 암호화 스크립트
├── decrypt_all_secrets.py            # ✅ 복호화 스크립트
├── .gitignore                        # ✅ 원본 파일 제외 설정
└── ENCRYPTED_SECRETS_README.md       # ✅ 이 파일
```

---

## 🛠️ 스크립트 상세

### encrypt_all_secrets.py

**기능**:
- `.env` → `.env.encrypted`
- `client_secret.json` → `client_secret.json.encrypted`

**사용법**:
```bash
python encrypt_all_secrets.py
```

**출력**:
```
✅ .env → .env.encrypted (7,560 bytes)
✅ client_secret.json → client_secret.json.encrypted (648 bytes)
```

### decrypt_all_secrets.py

**기능**:
- `.env.encrypted` → `.env`
- `client_secret.json.encrypted` → `client_secret.json`

**사용법**:
```bash
python decrypt_all_secrets.py
```

**출력**:
```
✅ .env.encrypted → .env (5,595 bytes)
✅ client_secret.json.encrypted → client_secret.json (414 bytes)
```

---

## 🔍 트러블슈팅

### 1. 복호화 실패

**증상**:
```
❌ .env.encrypted 복호화 실패: Fernet.decrypt() failed
```

**원인**:
- 비밀번호 불일치
- 파일 손상

**해결**:
1. `PASSWORD` 변수 확인
2. 최신 암호화 파일 다시 받기: `git pull`

### 2. 원본 파일이 Git에 포함됨

**증상**:
```
warning: adding embedded git repository: .env
```

**해결**:
```bash
# Git 캐시에서 제거
git rm --cached .env
git rm --cached client_secret.json

# .gitignore 확인
cat .gitignore | grep -E "\.env|client_secret"

# 커밋
git commit -m "fix: remove unencrypted secrets from git"
```

### 3. 파일이 없음

**증상**:
```
⏭️  .env.encrypted - 파일 없음 (건너뜀)
```

**해결**:
```bash
# 최신 파일 가져오기
git pull

# 파일 확인
ls -la *.encrypted
```

---

## 🚨 긴급 상황

### 비밀번호 분실

1. **복구 불가능**: 비밀번호 없이는 복호화 불가
2. **해결책**:
   - 환경 변수를 수동으로 재생성
   - Google Cloud Console에서 OAuth 재발급
   - 모든 API 키 재설정

### Git에 원본 파일 실수로 커밋

1. **즉시 삭제**:
   ```bash
   git rm --cached .env client_secret.json
   git commit -m "SECURITY: remove accidentally committed secrets"
   git push --force
   ```

2. **키 갱신**:
   - 모든 API 키 즉시 재발급
   - OAuth 클라이언트 재생성
   - Telegram Bot 토큰 재생성

3. **Git 히스토리 정리** (필요 시):
   ```bash
   # BFG Repo-Cleaner 사용
   java -jar bfg.jar --delete-files .env
   git reflog expire --expire=now --all
   git gc --prune=now --aggressive
   ```

---

## 📖 추가 정보

### 환경 변수 목록

`.env` 파일에 포함된 주요 변수:

```bash
# Google AI
GEMINI_API_KEY
GEMINI_MUSIC_KEY

# YouTube
YOUTUBE_API_KEY

# Instagram
INSTAGRAM_APP_ID
INSTAGRAM_APP_SECRET
INSTAGRAM_ACCESS_TOKEN
INSTAGRAM_ACCOUNT_ID

# Telegram
TELEGRAM_BOT_TOKEN
TELEGRAM_CHAT_ID

# Notion
NOTION_API_KEY
NOTION_DATABASE_ID

# Supabase
SUPABASE_URL
SUPABASE_ANON_KEY
SUPABASE_ACCESS_TOKEN

# Vercel
VERCEL_TOKEN
VERCEL_TEAM_ID
BLOB_READ_WRITE_TOKEN
CRON_SECRET
```

### client_secret.json 구조

```json
{
  "installed": {
    "client_id": "389378472882-xxxxx.apps.googleusercontent.com",
    "project_id": "central-bulwark-xxxxx",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "client_secret": "GOCSPX-xxxxx",
    "redirect_uris": ["http://localhost"]
  }
}
```

---

## ✅ 체크리스트

### 새 팀원 온보딩

- [ ] 저장소 클론
- [ ] 비밀번호 전달 (안전한 채널)
- [ ] `python decrypt_all_secrets.py` 실행
- [ ] 환경 변수 로드 확인
- [ ] YouTube OAuth 인증 (필요 시)

### 환경 변수 업데이트

- [ ] 원본 파일 수정 (`.env`, `client_secret.json`)
- [ ] `python encrypt_all_secrets.py` 실행
- [ ] Git 커밋: `git add *.encrypted`
- [ ] 푸시: `git push`
- [ ] 팀원에게 `git pull` 알림

### 정기 보안 점검

- [ ] `.gitignore`에 원본 파일 제외 확인
- [ ] Git 히스토리에 원본 파일 없는지 확인
- [ ] 비밀번호 주기적 변경 (3-6개월)
- [ ] API 키 유효성 확인

---

## 🔗 관련 문서

- [FINAL_SETUP_COMPLETE.md](FINAL_SETUP_COMPLETE.md) - 전체 설정 가이드
- [YOUTUBE_OAUTH_SETUP.md](YOUTUBE_OAUTH_SETUP.md) - OAuth 설정
- [ENV_MANIFEST.json](ENV_MANIFEST.json) - 환경 변수 목록

---

## 📞 지원

### 문제 발생 시

1. **로그 확인**: 스크립트 출력 메시지
2. **파일 확인**: `ls -la *.encrypted`
3. **비밀번호 확인**: `PASSWORD` 변수

### 보안 문제 발견 시

1. **즉시 보고**: 팀 리더에게 연락
2. **키 갱신**: 영향받은 API 키 재발급
3. **Git 정리**: 필요 시 히스토리 정리

---

**최종 업데이트**: 2026-06-04  
**작성자**: AI Lab Security Team  
**버전**: 1.0
