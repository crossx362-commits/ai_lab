# 모두비교

상품 가격, 기능, 판매량을 비교하는 Vite/React 프론트엔드입니다.

## 실행

```bash
npm install
npm run dev
```

## 환경변수

`.env.example`을 기준으로 `.env`를 만듭니다.

```bash
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your-public-anon-key

# 실시간 실제 상품 검색 (선택 — 없으면 쇼핑몰 검색 링크로 폴백)
NAVER_CLIENT_ID=your-naver-client-id
NAVER_CLIENT_SECRET=your-naver-client-secret
```

## 실시간 상품 검색 (네이버 쇼핑 API)

키가 있으면 `/api/search`가 네이버 쇼핑에서 실제 상품·가격을 조회해 인라인으로 보여주고,
키가 없으면 각 쇼핑몰의 실제 검색 결과 페이지로 연결합니다. 앱은 가격을 임의로 만들지 않습니다.

발급 (무료, 일 25,000회):

1. https://developers.naver.com/apps 접속 → 네이버 로그인
2. **애플리케이션 등록** → 사용 API에서 **검색** 선택
3. 환경(웹) 서비스 URL은 로컬은 `http://localhost:5173`, 배포는 Vercel 도메인
4. 발급된 **Client ID / Client Secret**을 `.env`(로컬) 또는 Vercel 환경변수에 입력

`NAVER_CLIENT_ID`/`NAVER_CLIENT_SECRET`은 **서버 전용**이라 `VITE_` 접두사를 붙이지 않습니다
(클라이언트 번들에 노출되지 않고 서버리스 함수·dev 미들웨어에서만 사용).

## Supabase DB 적용

Supabase 콘솔의 SQL Editor에서 아래 파일을 실행합니다.

```text
supabase/migrations/202607060001_bboggl_core.sql
```

생성되는 리소스:

- `profiles`
- `posts`
- `comments`
- `likes`
- RLS 정책
- `admin_stats()` RPC
- 신규 사용자 프로필 자동 생성 트리거

## 배포

Vercel 기준:

- Root Directory: `projects/bboggl`
- Build Command: `npm run build`
- Output Directory: `dist`
- Environment Variables:
  - `VITE_SUPABASE_URL`
  - `VITE_SUPABASE_ANON_KEY`
  - `NAVER_CLIENT_ID` (실시간 상품 검색, 선택)
  - `NAVER_CLIENT_SECRET` (실시간 상품 검색, 선택)

## 현재 연동 상태

- 인증: 이메일, Google, Kakao OAuth 호출 준비
- 마이페이지: 로그인 사용자 프로필 조회/수정
- 게시판: 목록/작성
- 댓글: 목록/작성
- 좋아요: 토글 API
- 관리자: 통계 RPC, 게시글 상태 변경
