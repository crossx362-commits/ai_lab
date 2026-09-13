# 펫과나 운영 연결 체크리스트

## 1. Supabase

1. Supabase 프로젝트를 만든다.
2. `supabase/schema.sql`을 SQL Editor에서 실행한다.
3. Project URL과 Service Role Key를 복사한다.

## 2. PayPal Live

1. Live 앱의 Client ID와 Secret을 준비한다.
2. Webhook을 만들고 `PAYMENT.CAPTURE.COMPLETED` 이벤트를 선택한다.
3. Webhook URL은 다음 주소다.

`https://pet-harmony-ebon.vercel.app/api/webhook`

4. Webhook ID를 복사한다.

## 3. Vercel Environment Variables

Production에 다음 값을 등록한다.

```text
PAYPAL_ENV=live
PAYPAL_CLIENT_ID=...
PAYPAL_CLIENT_SECRET=...
PAYPAL_WEBHOOK_ID=...
SUPABASE_URL=...
SUPABASE_SERVICE_ROLE_KEY=...
```

Secret 값은 소스 코드나 채팅에 넣지 않는다.

## 4. 확인

```bash
node scripts/smoke.mjs https://pet-harmony-ebon.vercel.app
```

헬스체크에서 `paymentConfigured=true`, `webhookConfigured=true`가 되어야 한다. 그 뒤 Sandbox에서 `$0.99` 결제, 상세 해금, `$3.99` PDF 결제 순서로 E2E 테스트한다.
