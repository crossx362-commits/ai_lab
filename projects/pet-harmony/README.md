# pet-harmony

펫과나(`projects/petnna`)에서 핵심 계산 로직을 분리해 만든 **펫 오디션** 랜딩 MVP.

## 출처

- 명리 엔진: `petnna/js/harmony/harmony-core.js` → `js/harmony-core.js` (복사)
- 사주 풀이: `petnna/js/saju.js`의 `startSajuAnalysis` 로직 → `js/saju-core.js` (추출)
- Petnna 본체는 수정하지 않음

## 모드

1. 전체 검사 후 우리 집과의 어울림 점수
2. 보호자 스타일 힌트
3. 입양 전 생활 준비
4. 강아지 vs 고양이
5. 커플 + 펫 세 식구 검사

무료: 점수 + 한 줄 + 공유 문구  
유료: $0.99 상세 / $3.99 사진 포함 최종 리포트 (PayPal Checkout)
환불: 디지털 결과 상품 특성상 결제 후 환불 없음.

## 로컬 실행

```bash
cd projects/pet-harmony
python3 -m http.server 8910
```

브라우저에서 `http://localhost:8910`

## 배포 주소

https://pet-harmony-ebon.vercel.app/

## 결제 API 환경변수

Vercel 배포 시 `.env.example`의 값을 Vercel Project Environment Variables에 등록하고 `supabase/schema.sql`을 Supabase SQL Editor에서 실행합니다. `SUPABASE_SERVICE_ROLE_KEY`와 PayPal Secret은 브라우저 코드에 넣지 않습니다.

자세한 연결 순서는 [DEPLOY_CHECKLIST.md](./DEPLOY_CHECKLIST.md)를 참고합니다.

## 운영 설정 확인

- Vercel에 PayPal·Supabase 환경변수를 등록하면 서버 승인 API·웹훅이 활성화됩니다.
- 공개 HTTPS 도메인에서 OG 이미지 절대 URL 적용 완료
- 생일을 모르면 모드별 기본 성향으로 추정합니다.

배포 상태 확인: `https://pet-harmony-ebon.vercel.app/api/health`

배포 스모크 QA: `node scripts/smoke.mjs https://pet-harmony-ebon.vercel.app`
