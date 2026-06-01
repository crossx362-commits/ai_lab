# 환경 변수 관리 가이드

## 🔐 암호화된 환경 변수 시스템

모든 환경 변수는 암호화되어 Git에 저장됩니다. 실제 `.env` 파일은 Git에 업로드되지 않습니다.

## 📁 파일 구조

```
.env                  # 실제 환경 변수 (Git 제외, 로컬 전용)
.env.encrypted        # 암호화된 환경 변수 (Git에 포함)
.env.key              # 암호화 키 (Git 제외, 안전하게 보관!)
encrypt_env.py        # 암호화/복호화 스크립트
ENV_README.md         # 이 파일
ENV_STATUS.md         # API 상태 보고서
```

## 🚀 최초 설정 (새로운 환경)

1. **암호화 키 받기**
   - 팀원에게 `.env.key` 파일 요청
   - 안전한 방법으로 전달받기 (Slack DM, 암호화된 이메일 등)
   - 프로젝트 루트에 저장

2. **환경 변수 복호화**
   ```bash
   python encrypt_env.py decrypt
   ```

3. **검증**
   ```bash
   python test_env_vars.py
   ```

## 🔄 환경 변수 업데이트 후

환경 변수를 수정한 후에는 **반드시** 다시 암호화하여 Git에 올립니다:

```bash
# 1. .env 파일 수정
# 2. 암호화
python encrypt_env.py encrypt

# 3. Git에 커밋
git add .env.encrypted
git commit -m "chore: update environment variables"
git push
```

## 🔑 암호화 키 관리

### 키 위치
- **로컬**: `d:\ai_lab\.env.key`
- **백업**: 안전한 비밀번호 관리자 또는 암호화된 저장소
- **절대 금지**: Git, Slack, 공개 채널

### 키 재생성 (긴급 시)
```bash
# 기존 키 삭제
rm .env.key

# 새 키 생성 및 재암호화
python encrypt_env.py encrypt

# 새 키를 팀원에게 안전하게 공유
```

## 📋 환경 변수 목록

현재 설정된 환경 변수 (15개):

### Vercel (Kevin DevOps Agent)
- `VERCEL_OIDC_TOKEN` - Vercel CLI 인증 (자동 생성)
- `VERCEL_TOKEN` - Vercel API
- `VERCEL_TEAM_ID` - 팀 ID
- `BLOB_READ_WRITE_TOKEN` - Blob Storage
- `CRON_SECRET` - Cron Job 인증

### Supabase (펫과나 DB)
- `SUPABASE_URL` - 프로젝트 URL
- `SUPABASE_ANON_KEY` - 공개 키

### Google APIs (AI 팀)
- `GEMINI_API_KEY` - Google AI (Gemini)
- `YOUTUBE_API_KEY` - YouTube Data API

### Instagram/Facebook (AI 팀)
- `INSTAGRAM_APP_ID` - App ID
- `INSTAGRAM_APP_SECRET` - App Secret
- `INSTAGRAM_ACCESS_TOKEN` - Access Token
- `INSTAGRAM_ACCOUNT_ID` - 계정 ID

### Telegram (알림)
- `TELEGRAM_BOT_TOKEN` - Bot Token
- `TELEGRAM_CHAT_ID` - Chat ID

## ✅ 검증 방법

```bash
python test_env_vars.py
```

**기대 결과**: 6/6 APIs working

## 🛡️ 보안 체크리스트

- [ ] `.env` 파일이 Git에 커밋되지 않았는지 확인
- [ ] `.env.key` 파일이 안전하게 보관되었는지 확인
- [ ] 환경 변수 변경 후 재암호화 했는지 확인
- [ ] API 키가 코드에 하드코딩되지 않았는지 확인
- [ ] 정기적으로 API 키 순환(rotate)

## 🔧 문제 해결

### "Invalid key" 오류
- `.env.key` 파일이 올바른지 확인
- 팀원에게 최신 키 요청

### "File not found" 오류
- `.env.encrypted` 파일이 있는지 확인
- Git pull로 최신 버전 받기

### API 테스트 실패
```bash
python test_env_vars.py
```
- 실패한 API 확인
- `ENV_STATUS.md` 참고
- 필요시 API 키 재발급

## 📞 지원

- API 상태: `ENV_STATUS.md` 참고
- Instagram 설정: `SETUP_INSTAGRAM.md` 참고
- 기타 문의: 팀 채널

---

**마지막 업데이트**: 2026-06-01
**암호화 방식**: Fernet (symmetric encryption)
