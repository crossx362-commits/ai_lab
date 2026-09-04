# LLM Cost Picker

Estimate your monthly LLM API bill across Anthropic, OpenAI and Google, and
pick the model that fits your budget, latency and context needs.

## Local development

```bash
cd projects/ai-model-picker
npm test                    # 순수 로직 단위 테스트 (Node 내장 러너, 의존성 0개)
python3 -m http.server 4321 # http://localhost:4321/
```

로컬 정적 서버에는 `/api/*`가 없다. 결제·라이선스까지 확인하려면 `.env.local`에
샌드박스 값을 채우고 `npx vercel dev`를 쓴다.

`node --test test/`(디렉터리 인자)는 Node 26에서 `MODULE_NOT_FOUND`로 죽는다.
`npm test`(= 인자 없는 `node --test`, 자동 탐색)를 쓸 것.

## 결제 흐름

PayPal JS SDK를 쓰지 않는다 — 외부 스크립트 의존을 0으로 유지하기 위한 리다이렉트 방식이다.

1. Buy 클릭 → `POST /api/create-order`
2. 서버가 PayPal 주문(`intent: CAPTURE`) 생성 → 승인 URL 반환
3. 브라우저가 PayPal로 이동해 결제
4. PayPal이 `/api/capture-order?token=<주문ID>`로 리다이렉트
5. 서버가 캡처 → 성공 시 라이선스 키 발급 → `/?license=<KEY>`로 리다이렉트
6. `premium.js`가 키를 읽어 자동 unlock하고 화면에 표시

**라이선스 키에는 구매 기록 DB가 없다.** 키 = `<PayPal 주문ID>-<HMAC 앞 10자리>`이고,
서버가 `PICKER_LICENSE_SECRET`으로 서명을 재계산해 대조한다.

## Deploying (오너 작업)

1. Vercel에 이 저장소를 연결하고 **Root Directory**를 `projects/ai-model-picker`로 지정한다.
2. 환경변수를 설정한다:

   | 이름 | 값 |
   | --- | --- |
   | `PAYPAL_ENV` | 검증 중엔 `sandbox`, 판매 개시 시 `live` |
   | `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET` | PayPal 라이브 앱 자격증명 |
   | `PAYPAL_SANDBOX_CLIENT_ID` / `PAYPAL_SANDBOX_CLIENT_SECRET` | PayPal 샌드박스 앱 자격증명 |
   | `PICKER_LICENSE_SECRET` | `openssl rand -hex 32` 결과 |
   | `PICKER_PRICE_USD` | 예: `9.00` |
   | `PICKER_SITE_URL` | 배포 도메인 origin (끝에 `/` 없이) |

   값은 저장소 루트 `.env`에 이미 들어 있다(`PAYPAL_*`). `.env`는 git에 올라가지 않는다.

3. **`PICKER_LICENSE_SECRET`은 절대 바꾸지 않는다** — 바꾸면 이미 판매한 모든 라이선스 키가 무효화된다.
4. 샌드박스 구매자 계정으로 결제 왕복을 확인한 뒤에만 `PAYPAL_ENV=live`로 바꾼다.

`vercel.json`에 `_comment_*` 같은 비표준 키를 넣지 마라 — 스키마 검증 실패로 전체 배포가 죽는다
(2026-07-28 사고). 설명은 이 README에 쓴다.

## 환불 처리 (수동)

라이선스 키에 구매 기록이 없으므로 환불해도 키는 계속 동작한다. $9 상품에서 DB를 두는
비용보다 이 손실이 작다는 판단이다. 문제가 될 만큼 환불이 늘면 `api/verify-license.js`에
PayPal 주문 상태 조회를 덧붙인다 — `verifyLicense`가 이미 `orderId`를 돌려주므로 코드
변경만으로 가능하다.

## 세무 (오너 확인 필요)

PayPal은 merchant of record가 아니라 결제 대행일 뿐이라, 디지털 상품 판매에 대한
부가세(EU VAT 등) 의무가 판매자에게 있다. Gumroad·Paddle 같은 MoR을 쓰면 이 부담이
없어지므로 해외 매출이 늘면 재검토할 사안이다.

## 가격 데이터 갱신

`src/pricing.js`의 `MODELS`와 `PRICING_LAST_VERIFIED`를 분기마다 갱신한다.
출처 URL과 값을 채울 때 내린 판단(캐시 읽기 단가만 모델링, Sonnet 5는 도입가 대신
표준가 사용 등)은 파일 상단 주석에 있다.

## 구조

| 경로 | 역할 |
| --- | --- |
| `index.html` | 페이지 — **루트에 둔다**(`public/`에 두면 배포 시 상대경로 CSS/JS가 404) |
| `src/pricing.js` | 모델 가격표 (수동 갱신) |
| `src/cost.js` | 월 비용 계산 |
| `src/recommend.js` | 우선순위 기반 순위 산정 |
| `src/format.js` | 통화 포맷, Markdown/CSV 내보내기 |
| `src/license.js` | 라이선스 키 서명·검증 (**서버 전용**) |
| `src/paypal.js` | PayPal Orders API 클라이언트 (**서버 전용**) |
| `api/create-order.js` | 주문 생성 → PayPal 승인 URL 반환 |
| `api/capture-order.js` | 결제 캡처 → 라이선스 키 발급 후 리다이렉트 |
| `api/verify-license.js` | 라이선스 키 검증 |
| `public/` | CSS·클라이언트 JS |
| `test/` | 단위 테스트 |
