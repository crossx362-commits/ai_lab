
## 2026-06-05 18:30 KST — 아린 인스타 포스팅
- 상태: 실패
- 실패 원인: `.env.encrypted` 복호화 불가 (클라우드 환경 — 암호화 키가 원본 머신과 불일치)
- 필요 조치: Claude Code on the Web 환경변수 설정에서 다음 변수 직접 등록 필요
  - `INSTAGRAM_ACCESS_TOKEN`
  - `INSTAGRAM_ACCOUNT_ID`
  - `GEMINI_API_KEY`
  - `TELEGRAM_BOT_TOKEN`
  - `TELEGRAM_CHAT_ID`
- 트렌드 주제: 미실행
- 포스트 ID: N/A

## 2026-06-06 09:30 UTC — 아린 인스타 포스팅
- 상태: 실패
- 트렌드 주제: N/A (환경변수 미로드)
- 포스트 ID: N/A
- 실패 원인: .env.encrypted 파일이 원본 기기(머신 고유 키) 기준으로 암호화되어 클라우드 환경에서 복호화 불가. INSTAGRAM_ACCESS_TOKEN 미설정.
