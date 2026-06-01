# 환경 변수 상태 보고서

생성일: 2026-06-01

## ✅ 정상 작동 (3개)

### 1. Vercel API
- **상태**: ✅ 정상
- **변수**: `VERCEL_TOKEN`, `VERCEL_TEAM_ID`
- **확인**: 프로젝트 2개 조회 성공
- **용도**: Kevin DevOps Agent (cleanup-projects API)

### 2. YouTube Data API v3
- **상태**: ✅ 정상
- **변수**: `YOUTUBE_API_KEY`
- **값**: `AIzaSyCinGnUTcnY83LJSuplssorkBDEepebeyU`
- **확인**: 검색 API 호출 성공
- **용도**: AI 팀 - 루나 디렉터 (YouTube 트렌드 분석)

### 3. Instagram/Facebook Access Token
- **상태**: ✅ 정상 (Facebook User Token)
- **변수**: `INSTAGRAM_ACCESS_TOKEN`, `INSTAGRAM_ACCOUNT_ID`
- **확인**: Facebook Graph API 인증 성공
- **주의**: Instagram Business 계정 연결 필요
- **용도**: AI 팀 - 아린 관리자 (Instagram 콘텐츠 관리)

## ❌ 재발급 필요 (3개)

### 4. Gemini API
- **상태**: ❌ API 키 노출됨 (차단됨)
- **변수**: `GEMINI_API_KEY`
- **오류**: `Your API key was reported as leaked`
- **해결**: https://aistudio.google.com/app/apikey 에서 새 키 발급
- **용도**: AI 팀 - 모든 에이전트 (AI 생성)

### 5. Supabase API
- **상태**: ❌ 잘못된 엔드포인트 또는 키
- **변수**: `SUPABASE_URL`, `SUPABASE_ANON_KEY`
- **오류**: `Invalid API key` (service_role 필요)
- **해결**: 
  - 올바른 REST API 엔드포인트 확인
  - 또는 anon key로 접근 가능한 테이블 생성
- **용도**: 펫과나 앱 데이터베이스

### 6. Telegram Bot API
- **상태**: ❌ 토큰 무효
- **변수**: `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`
- **오류**: `401 Unauthorized`
- **해결**: BotFather에서 새 봇 생성 또는 기존 토큰 확인
- **용도**: AI 팀 알림 및 보고서 전송

## 🔧 추가 설정 필요

### Vercel Blob Storage
- **변수**: `BLOB_READ_WRITE_TOKEN`
- **상태**: ⚠️ 미테스트 (Kevin에서 사용)

### Vercel Cron
- **변수**: `CRON_SECRET`
- **상태**: ⚠️ 미테스트 (cleanup-projects API에서 사용)

### Instagram Business 연결
- 현재: Facebook User Access Token만 있음
- 필요: Instagram Business 계정과 Facebook Page 연결
- 가이드: `SETUP_INSTAGRAM.md` 참고

## 📋 다음 작업

1. **긴급**: Gemini API 키 재발급 (가장 많이 사용됨)
2. **긴급**: Telegram Bot 토큰 재발급 또는 확인
3. **중요**: Supabase 연결 확인 및 수정
4. **선택**: Instagram Business 계정 연결 (필요시)

## 💾 환경 변수 파일 위치

- 루트 `.env`: `d:\ai_lab\.env` (Git 제외됨)
- 모든 API 키가 한 곳에 통합 관리됨
- 예시 파일: `petnna/.env.example`, `SETUP_INSTAGRAM.md`

## 🔒 보안 주의사항

- ⚠️ Gemini API 키가 노출되었습니다 (이미 차단됨)
- `.env` 파일은 절대 Git에 커밋하지 마세요
- API 키를 코드나 로그에 출력하지 마세요
- 정기적으로 키를 순환(rotate)하세요
