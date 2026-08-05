# PayPal License Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gumroad 라이선스 게이트를 PayPal 일회성 결제 + HMAC 서명 라이선스 키로 교체한다. 구매 기록 DB 없이 서명만으로 키를 검증한다.

**Architecture:** 서버리스 함수 3개(주문 생성 → 결제 캡처 → 키 검증)를 추가하고, Gumroad 호출부를 제거한다. 라이선스 키는 `<PayPal주문ID>-<HMAC앞10자리>` 형태라 서버가 같은 시크릿으로 재계산해 대조하면 되므로 저장소가 필요 없다. HMAC은 `node:crypto`를 쓰므로 서버 전용 모듈(`lib/`)에 격리하고, 브라우저와 공유하는 순수 로직만 `src/`에 남긴다.

**Tech Stack:** Vanilla JS (ES modules), Node 26 내장 `node --test`, Vercel serverless functions, PayPal Orders v2 REST API

**저장소:** `/Users/junholee/ai-model-picker` (독립 git 저장소). `/Users/junholee/ai_lab`은 어떤 이유로도 건드리지 않는다.

## Global Constraints

- 런타임 의존성 0개. `package.json`에 `dependencies`/`devDependencies`를 추가하지 않는다. PayPal 호출은 내장 `fetch`로 한다.
- 빌드 단계 없음. 브라우저가 `src/`의 ES 모듈을 그대로 로드한다.
- **`src/`의 모든 모듈은 브라우저와 Node 양쪽에서 동작해야 한다** — `node:crypto` 등 Node 전용 API 금지. 서버 전용 코드는 `lib/`에 둔다.
- `lib/`와 `api/`는 Node 전용이며 브라우저에서 임포트되지 않는다.
- 모든 사용자 노출 텍스트는 **영어**. 코드 주석도 **영어**(`src/`가 배포되어 공개적으로 읽힌다).
- 시크릿(`PAYPAL_CLIENT_SECRET`, `PICKER_LICENSE_SECRET`)은 응답·로그·에러 메시지에 절대 등장하지 않는다.
- 라이선스 키 전문을 로그에 남기지 않는다.
- `vercel.json`에 비표준 키(`_comment_*` 등)를 넣지 않는다 — 스키마 검증 실패로 전체 배포가 죽는다.
- `vercel.json`의 `"outputDirectory": "."`를 제거하지 않는다 — 제거하면 `src/`가 배포되지 않아 사이트가 빈 페이지가 된다.
- 기존 테스트 33개는 계속 통과해야 한다.

## 환경변수 (오너가 Vercel에 설정)

| 이름 | 용도 |
| --- | --- |
| `PAYPAL_CLIENT_ID` | PayPal 앱 클라이언트 ID |
| `PAYPAL_CLIENT_SECRET` | PayPal 앱 시크릿 |
| `PAYPAL_ENV` | `sandbox` 또는 `live` (기본 `sandbox`) |
| `PICKER_LICENSE_SECRET` | 라이선스 키 서명용 임의 문자열(32자 이상) |
| `PICKER_PRICE_USD` | 가격. 미설정 시 `9.00` |

---

### Task 1: 라이선스 키 형식과 서명

**Files:**
- Create: `src/license-key.js`
- Create: `lib/license-sign.js`
- Test: `test/license-key.test.js`
- Test: `test/license-sign.test.js`
- Modify: `src/license.js` (Gumroad 전용 `parseVerifyResponse` 삭제)
- Modify: `test/license.test.js` (삭제 — Gumroad 응답 파서 테스트였음)

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `parseLicenseKey(key)` (from `src/license-key.js`): 문자열을 받아 `{ orderId, signature }` 또는 `null`. 형식은 `<orderId>-<signature>`이며 `orderId`는 `[A-Z0-9]{5,32}`, `signature`는 소문자 16진수 10자. 마지막 하이픈을 기준으로 나눈다(주문 ID에 하이픈이 없더라도 방어적으로). 형식이 틀리면 `null`.
  - `signOrderId(orderId, secret)` (from `lib/license-sign.js`): HMAC-SHA256 16진수 앞 10자 반환.
  - `makeLicenseKey(orderId, secret)` (from `lib/license-sign.js`): `` `${orderId}-${signOrderId(orderId, secret)}` ``
  - `verifyLicenseKey(key, secret)` (from `lib/license-sign.js`): `{ valid: boolean, reason: string }`. 타이밍 공격을 피하기 위해 `crypto.timingSafeEqual`로 비교한다.

- [ ] **Step 1: 실패하는 테스트 작성 — 키 형식 파서**

`test/license-key.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseLicenseKey } from '../src/license-key.js';

test('parseLicenseKey splits a well-formed key', () => {
  const r = parseLicenseKey('5O190127TN364715T-a1b2c3d4e5');
  assert.deepEqual(r, { orderId: '5O190127TN364715T', signature: 'a1b2c3d4e5' });
});

test('parseLicenseKey trims surrounding whitespace', () => {
  const r = parseLicenseKey('  5O190127TN364715T-a1b2c3d4e5\n');
  assert.equal(r.orderId, '5O190127TN364715T');
});

test('parseLicenseKey rejects a missing signature', () => {
  assert.equal(parseLicenseKey('5O190127TN364715T'), null);
});

test('parseLicenseKey rejects a wrong-length signature', () => {
  assert.equal(parseLicenseKey('5O190127TN364715T-a1b2c3'), null);
  assert.equal(parseLicenseKey('5O190127TN364715T-a1b2c3d4e5f6'), null);
});

test('parseLicenseKey rejects a non-hex signature', () => {
  assert.equal(parseLicenseKey('5O190127TN364715T-zzzzzzzzzz'), null);
});

test('parseLicenseKey rejects an uppercase signature', () => {
  assert.equal(parseLicenseKey('5O190127TN364715T-A1B2C3D4E5'), null);
});

test('parseLicenseKey rejects a bad order id', () => {
  assert.equal(parseLicenseKey('abc-a1b2c3d4e5'), null);
  assert.equal(parseLicenseKey('-a1b2c3d4e5'), null);
});

test('parseLicenseKey rejects non-strings and empties', () => {
  assert.equal(parseLicenseKey(''), null);
  assert.equal(parseLicenseKey(null), null);
  assert.equal(parseLicenseKey(undefined), null);
  assert.equal(parseLicenseKey(12345), null);
  assert.equal(parseLicenseKey({}), null);
});
```

- [ ] **Step 2: 실패하는 테스트 작성 — 서명**

`test/license-sign.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signOrderId, makeLicenseKey, verifyLicenseKey } from '../lib/license-sign.js';

const SECRET = 'test-secret-value-at-least-32-chars-long';
const ORDER = '5O190127TN364715T';

test('signOrderId returns 10 lowercase hex characters', () => {
  const sig = signOrderId(ORDER, SECRET);
  assert.match(sig, /^[0-9a-f]{10}$/);
});

test('signOrderId is deterministic', () => {
  assert.equal(signOrderId(ORDER, SECRET), signOrderId(ORDER, SECRET));
});

test('signOrderId changes with the order id', () => {
  assert.notEqual(signOrderId(ORDER, SECRET), signOrderId('9XY190127TN364715', SECRET));
});

test('signOrderId changes with the secret', () => {
  assert.notEqual(signOrderId(ORDER, SECRET), signOrderId(ORDER, SECRET + 'x'));
});

test('makeLicenseKey joins the order id and signature', () => {
  const key = makeLicenseKey(ORDER, SECRET);
  assert.equal(key, `${ORDER}-${signOrderId(ORDER, SECRET)}`);
});

test('verifyLicenseKey accepts a key it just made', () => {
  const key = makeLicenseKey(ORDER, SECRET);
  assert.equal(verifyLicenseKey(key, SECRET).valid, true);
});

test('verifyLicenseKey rejects a tampered signature', () => {
  const key = makeLicenseKey(ORDER, SECRET);
  const tampered = key.slice(0, -1) + (key.at(-1) === 'a' ? 'b' : 'a');
  assert.equal(verifyLicenseKey(tampered, SECRET).valid, false);
});

test('verifyLicenseKey rejects a key signed with a different secret', () => {
  const key = makeLicenseKey(ORDER, 'a-completely-different-secret-value-32');
  assert.equal(verifyLicenseKey(key, SECRET).valid, false);
});

test('verifyLicenseKey rejects a malformed key without throwing', () => {
  for (const bad of ['', 'nonsense', null, undefined, 12345, 'abc-zzzzzzzzzz']) {
    const r = verifyLicenseKey(bad, SECRET);
    assert.equal(r.valid, false);
    assert.ok(r.reason.length > 0);
  }
});

test('verifyLicenseKey rejects when the secret is missing', () => {
  const key = makeLicenseKey(ORDER, SECRET);
  assert.equal(verifyLicenseKey(key, '').valid, false);
  assert.equal(verifyLicenseKey(key, undefined).valid, false);
});

test('verifyLicenseKey never puts the key or secret in the reason', () => {
  const key = makeLicenseKey(ORDER, SECRET);
  const r = verifyLicenseKey(key, 'wrong-secret-wrong-secret-wrong-x');
  assert.ok(!r.reason.includes(SECRET));
  assert.ok(!r.reason.includes(key));
});
```

- [ ] **Step 3: 테스트 실패 확인**

Run: `npm test`
Expected: FAIL — `Cannot find module '../src/license-key.js'`

- [ ] **Step 4: 키 형식 파서 구현**

`src/license-key.js`:

```js
// License key format: <PayPal order id>-<10 hex chars>
// Pure string handling only — this module is served to the browser,
// so it must never import node:crypto or any other Node-only API.

const KEY_PATTERN = /^([A-Z0-9]{5,32})-([0-9a-f]{10})$/;

/**
 * Split a license key into its order id and signature.
 * Returns null for anything that is not a well-formed key.
 */
export function parseLicenseKey(key) {
  if (typeof key !== 'string') return null;
  const match = KEY_PATTERN.exec(key.trim());
  if (!match) return null;
  return { orderId: match[1], signature: match[2] };
}
```

- [ ] **Step 5: 서명 모듈 구현**

`lib/license-sign.js`:

```js
// Server-only. Uses node:crypto and must never be imported by the browser.
import { createHmac, timingSafeEqual } from 'node:crypto';
import { parseLicenseKey } from '../src/license-key.js';

const SIGNATURE_LENGTH = 10;

/** HMAC-SHA256 of the order id, truncated to 10 hex characters. */
export function signOrderId(orderId, secret) {
  return createHmac('sha256', secret)
    .update(String(orderId))
    .digest('hex')
    .slice(0, SIGNATURE_LENGTH);
}

/** Build the license key handed to a buyer after a captured payment. */
export function makeLicenseKey(orderId, secret) {
  return `${orderId}-${signOrderId(orderId, secret)}`;
}

/**
 * Check a license key against the signing secret.
 * No purchase record is stored — the signature IS the record.
 */
export function verifyLicenseKey(key, secret) {
  if (typeof secret !== 'string' || secret.length === 0) {
    return { valid: false, reason: 'License checks are temporarily unavailable.' };
  }
  const parsed = parseLicenseKey(key);
  if (!parsed) {
    return { valid: false, reason: "That doesn't look like a license key." };
  }
  const expected = Buffer.from(signOrderId(parsed.orderId, secret), 'utf8');
  const actual = Buffer.from(parsed.signature, 'utf8');
  if (expected.length !== actual.length || !timingSafeEqual(expected, actual)) {
    return { valid: false, reason: "We couldn't verify that license key." };
  }
  return { valid: true, reason: '' };
}
```

- [ ] **Step 6: Gumroad 잔재 삭제**

- `src/license.js` 삭제 (Gumroad 응답 파서 전용이었다).
- `test/license.test.js` 삭제.
- 두 파일을 임포트하는 곳이 남아 있지 않은지 확인: `grep -rn "license\.js" --include=*.js .` 결과에 `license-key.js`/`license-sign.js` 외의 히트가 없어야 한다.

- [ ] **Step 7: 테스트 통과 확인**

Run: `npm test`
Expected: PASS — 5개(Gumroad 파서 테스트) 제거, 20개 추가 → **48 tests**

- [ ] **Step 8: 커밋**

```bash
git add -A && git commit -m "feat: HMAC-signed license keys, drop Gumroad verifier"
```

---

### Task 2: PayPal 주문 생성·캡처 서버리스 함수

**Files:**
- Create: `lib/paypal.js`
- Create: `api/create-order.js`
- Create: `api/capture-order.js`
- Test: `test/paypal.test.js`

**Interfaces:**
- Consumes: `makeLicenseKey(orderId, secret)` (`lib/license-sign.js`, Task 1)
- Produces:
  - `paypalBase(env)` (from `lib/paypal.js`): `'live'`이면 `https://api-m.paypal.com`, 그 외 전부 `https://api-m.sandbox.paypal.com`.
  - `readConfig(env)` (from `lib/paypal.js`): `env`는 `process.env` 형태의 평범한 객체. `{ ok: true, config: { clientId, clientSecret, base, licenseSecret, priceUsd } }` 또는 `{ ok: false, missing: string[] }`. `priceUsd`는 `PICKER_PRICE_USD` 또는 `'9.00'`. **반환값에 시크릿 원문이 들어가지만 이 함수의 결과는 절대 응답으로 나가면 안 된다** — 호출부가 `ok`와 `missing`만 사용한다.
  - `captureStatusOf(captureResponse)` (from `lib/paypal.js`): PayPal 캡처 응답에서 `{ completed: boolean, orderId: string|null }` 추출. `status === 'COMPLETED'`이고 `id`가 문자열일 때만 `completed: true`.

- [ ] **Step 1: 실패하는 테스트 작성**

`test/paypal.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { paypalBase, readConfig, captureStatusOf } from '../lib/paypal.js';

const FULL = {
  PAYPAL_CLIENT_ID: 'cid',
  PAYPAL_CLIENT_SECRET: 'csecret',
  PICKER_LICENSE_SECRET: 'lsecret-at-least-32-characters-long!!',
};

test('paypalBase selects live only for the exact string "live"', () => {
  assert.equal(paypalBase('live'), 'https://api-m.paypal.com');
  assert.equal(paypalBase('sandbox'), 'https://api-m.sandbox.paypal.com');
  assert.equal(paypalBase(undefined), 'https://api-m.sandbox.paypal.com');
  assert.equal(paypalBase('LIVE'), 'https://api-m.sandbox.paypal.com');
  assert.equal(paypalBase('production'), 'https://api-m.sandbox.paypal.com');
});

test('readConfig succeeds when every required variable is present', () => {
  const r = readConfig(FULL);
  assert.equal(r.ok, true);
  assert.equal(r.config.clientId, 'cid');
  assert.equal(r.config.base, 'https://api-m.sandbox.paypal.com');
  assert.equal(r.config.priceUsd, '9.00');
});

test('readConfig honours PICKER_PRICE_USD', () => {
  const r = readConfig({ ...FULL, PICKER_PRICE_USD: '12.00' });
  assert.equal(r.config.priceUsd, '12.00');
});

test('readConfig reports every missing variable by name', () => {
  const r = readConfig({});
  assert.equal(r.ok, false);
  assert.deepEqual(
    r.missing.sort(),
    ['PAYPAL_CLIENT_ID', 'PAYPAL_CLIENT_SECRET', 'PICKER_LICENSE_SECRET'].sort(),
  );
});

test('readConfig treats an empty string as missing', () => {
  const r = readConfig({ ...FULL, PAYPAL_CLIENT_ID: '' });
  assert.equal(r.ok, false);
  assert.deepEqual(r.missing, ['PAYPAL_CLIENT_ID']);
});

test('captureStatusOf accepts a completed capture', () => {
  const r = captureStatusOf({ id: '5O190127TN364715T', status: 'COMPLETED' });
  assert.deepEqual(r, { completed: true, orderId: '5O190127TN364715T' });
});

test('captureStatusOf rejects a non-completed status', () => {
  assert.equal(captureStatusOf({ id: 'X', status: 'PENDING' }).completed, false);
  assert.equal(captureStatusOf({ id: 'X', status: 'DECLINED' }).completed, false);
});

test('captureStatusOf rejects malformed responses without throwing', () => {
  for (const bad of [null, undefined, {}, [], 'COMPLETED', { status: 'COMPLETED' }]) {
    assert.equal(captureStatusOf(bad).completed, false);
  }
});
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `npm test`
Expected: FAIL — `Cannot find module '../lib/paypal.js'`

- [ ] **Step 3: `lib/paypal.js` 구현**

```js
// Server-only PayPal helpers. Pure functions here so they can be unit tested
// without network access; the actual HTTP calls live in the api/ handlers.

const REQUIRED = ['PAYPAL_CLIENT_ID', 'PAYPAL_CLIENT_SECRET', 'PICKER_LICENSE_SECRET'];

export function paypalBase(env) {
  return env === 'live' ? 'https://api-m.paypal.com' : 'https://api-m.sandbox.paypal.com';
}

/**
 * Read and validate configuration from an environment object.
 * The returned config carries secrets — callers must use only `ok`/`missing`
 * when deciding what to send back to a client.
 */
export function readConfig(env) {
  const missing = REQUIRED.filter((name) => !env[name]);
  if (missing.length > 0) return { ok: false, missing };
  return {
    ok: true,
    config: {
      clientId: env.PAYPAL_CLIENT_ID,
      clientSecret: env.PAYPAL_CLIENT_SECRET,
      base: paypalBase(env.PAYPAL_ENV),
      licenseSecret: env.PICKER_LICENSE_SECRET,
      priceUsd: env.PICKER_PRICE_USD || '9.00',
    },
  };
}

/** Extract the outcome of a PayPal capture response. */
export function captureStatusOf(response) {
  if (!response || typeof response !== 'object' || Array.isArray(response)) {
    return { completed: false, orderId: null };
  }
  const completed = response.status === 'COMPLETED' && typeof response.id === 'string';
  return { completed, orderId: completed ? response.id : null };
}

/** Fetch an OAuth2 access token. Throws on failure; callers must catch. */
export async function accessToken(config) {
  const credentials = Buffer.from(`${config.clientId}:${config.clientSecret}`).toString('base64');
  const response = await fetch(`${config.base}/v1/oauth2/token`, {
    method: 'POST',
    headers: {
      Authorization: `Basic ${credentials}`,
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body: 'grant_type=client_credentials',
  });
  if (!response.ok) throw new Error('paypal auth failed');
  const json = await response.json();
  if (typeof json.access_token !== 'string') throw new Error('paypal auth malformed');
  return json.access_token;
}
```

- [ ] **Step 4: `api/create-order.js` 구현**

```js
import { readConfig, accessToken } from '../lib/paypal.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }

  const { ok, config } = readConfig(process.env);
  if (!ok) {
    res.status(500).json({ error: 'Checkout is temporarily unavailable. Please contact support.' });
    return;
  }

  const origin = typeof req.headers?.origin === 'string' ? req.headers.origin : '';
  const returnUrl = origin ? `${origin}/?paid=1` : undefined;
  const cancelUrl = origin ? `${origin}/?cancelled=1` : undefined;

  try {
    const token = await accessToken(config);
    const response = await fetch(`${config.base}/v2/checkout/orders`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        intent: 'CAPTURE',
        purchase_units: [
          {
            amount: { currency_code: 'USD', value: config.priceUsd },
            description: 'LLM Cost Picker Pro licence',
          },
        ],
        application_context: {
          brand_name: 'LLM Cost Picker',
          user_action: 'PAY_NOW',
          shipping_preference: 'NO_SHIPPING',
          ...(returnUrl ? { return_url: returnUrl, cancel_url: cancelUrl } : {}),
        },
      }),
    });
    const json = await response.json().catch(() => null);
    const approve = json?.links?.find?.((l) => l.rel === 'approve')?.href;
    if (!response.ok || typeof json?.id !== 'string' || typeof approve !== 'string') {
      res.status(502).json({ error: "Couldn't start checkout. Please try again in a moment." });
      return;
    }
    res.status(200).json({ orderId: json.id, approveUrl: approve });
  } catch {
    res.status(502).json({ error: "Couldn't reach PayPal. Please try again in a moment." });
  }
}
```

- [ ] **Step 5: `api/capture-order.js` 구현**

```js
import { readConfig, accessToken, captureStatusOf } from '../lib/paypal.js';
import { makeLicenseKey } from '../lib/license-sign.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }

  const { ok, config } = readConfig(process.env);
  if (!ok) {
    res.status(500).json({ error: 'Checkout is temporarily unavailable. Please contact support.' });
    return;
  }

  const orderId = typeof req.body?.orderId === 'string' ? req.body.orderId.trim() : '';
  if (!/^[A-Z0-9]{5,32}$/.test(orderId)) {
    res.status(400).json({ error: 'Missing order reference.' });
    return;
  }

  try {
    const token = await accessToken(config);
    const response = await fetch(`${config.base}/v2/checkout/orders/${orderId}/capture`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    });
    const json = await response.json().catch(() => null);
    const status = captureStatusOf(json);
    if (!status.completed) {
      res.status(402).json({ error: 'That payment has not completed.' });
      return;
    }
    res.status(200).json({ licenseKey: makeLicenseKey(status.orderId, config.licenseSecret) });
  } catch {
    res.status(502).json({ error: "Couldn't reach PayPal. Please try again in a moment." });
  }
}
```

- [ ] **Step 6: 테스트 통과 확인**

Run: `npm test`
Expected: PASS — **56 tests** (48 + 8)

- [ ] **Step 7: 커밋**

```bash
git add -A && git commit -m "feat: PayPal order creation and capture endpoints"
```

---

### Task 3: 검증 엔드포인트 교체와 결제 UI

**Files:**
- Rewrite: `api/verify-license.js`
- Modify: `public/premium.js`
- Modify: `public/index.html`
- Modify: `public/styles.css`

**Interfaces:**
- Consumes: `verifyLicenseKey(key, secret)` (`lib/license-sign.js`), `readConfig` (`lib/paypal.js`)
- Produces: 구매 → 키 발급 → 잠금 해제 흐름 전체.

- [ ] **Step 1: `api/verify-license.js` 전면 재작성**

Gumroad 호출을 전부 제거하고 서명 검증으로 대체한다:

```js
import { verifyLicenseKey } from '../lib/license-sign.js';

export default function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ valid: false, reason: 'Method not allowed.' });
    return;
  }

  const secret = process.env.PICKER_LICENSE_SECRET;
  if (!secret) {
    res.status(500).json({
      valid: false,
      reason: 'License checks are temporarily unavailable. Please contact support.',
    });
    return;
  }

  const licenseKey = typeof req.body?.licenseKey === 'string' ? req.body.licenseKey.trim() : '';
  if (!licenseKey) {
    res.status(400).json({ valid: false, reason: 'Enter your license key.' });
    return;
  }
  if (licenseKey.length > 200) {
    res.status(400).json({ valid: false, reason: 'That license key is too long.' });
    return;
  }

  res.status(200).json(verifyLicenseKey(licenseKey, secret));
}
```

- [ ] **Step 2: 구매 UI를 HTML에 반영**

`public/index.html`에서 Gumroad 링크가 있던 문단(`buy-link`를 포함한 `.footnote`)을 다음으로 교체한다:

```html
        <p class="footnote">
          Don't have a license?
          <button type="button" id="buy-button">Buy Pro — $9 one-time</button>
        </p>
        <p id="buy-message" role="status"></p>
```

- [ ] **Step 3: `public/premium.js`의 Gumroad 잔재 제거와 결제 흐름 추가**

`BUY_URL` 상수와 `document.getElementById('buy-link').href = BUY_URL;` 줄을 삭제하고, 다음을 추가한다:

```js
const buyButton = document.getElementById('buy-button');
const buyMessage = document.getElementById('buy-message');
const PENDING_ORDER_KEY = 'picker_pending_order';

async function startCheckout() {
  buyButton.disabled = true;
  buyMessage.className = '';
  buyMessage.textContent = 'Opening checkout…';
  try {
    const response = await fetch('/api/create-order', { method: 'POST' });
    const json = await response.json().catch(() => null);
    if (!response.ok || !json?.approveUrl || !json?.orderId) {
      buyMessage.className = 'error';
      buyMessage.textContent = json?.error || "Couldn't start checkout. Please try again.";
      buyButton.disabled = false;
      return;
    }
    localStorage.setItem(PENDING_ORDER_KEY, json.orderId);
    window.location.href = json.approveUrl;
  } catch {
    buyMessage.className = 'error';
    buyMessage.textContent = "Couldn't reach the checkout server. Please try again in a moment.";
    buyButton.disabled = false;
  }
}

/** After PayPal redirects back with ?paid=1, capture the payment and show the key. */
async function finishPendingCheckout() {
  const params = new URLSearchParams(window.location.search);
  if (params.get('paid') !== '1') return;
  const orderId = localStorage.getItem(PENDING_ORDER_KEY);
  window.history.replaceState({}, '', window.location.pathname);
  if (!orderId) return;

  buyMessage.textContent = 'Confirming your payment…';
  try {
    const response = await fetch('/api/capture-order', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ orderId }),
    });
    const json = await response.json().catch(() => null);
    if (!response.ok || typeof json?.licenseKey !== 'string') {
      buyMessage.className = 'error';
      buyMessage.textContent =
        json?.error || "We couldn't confirm that payment. Please contact support.";
      return;
    }
    localStorage.removeItem(PENDING_ORDER_KEY);
    localStorage.setItem(STORAGE_KEY, json.licenseKey);
    document.getElementById('license-key').value = json.licenseKey;
    buyMessage.className = 'ok';
    buyMessage.textContent = `Payment confirmed. Your license key is ${json.licenseKey} — save it somewhere safe.`;
    unlock();
  } catch {
    buyMessage.className = 'error';
    buyMessage.textContent = "Couldn't reach the server to confirm your payment. Please try again.";
  }
}

buyButton.addEventListener('click', startCheckout);
finishPendingCheckout();
```

- [ ] **Step 4: 버튼 스타일 확인**

`public/styles.css`에 이미 `button` 규칙이 있다. `#buy-button`이 문단 안의 인라인 버튼이므로 다음을 추가한다:

```css
#buy-button { padding: .35rem .7rem; font-size: .9rem; }
#buy-message:empty { display: none; }
#buy-message { margin: .5rem 0 0; font-size: .9rem; }
#buy-message.error { color: #c8372d; }
#buy-message.ok { color: #1c7c4a; }
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `npm test`
Expected: PASS — 56 tests (이 태스크는 단위 테스트를 추가하지 않는다)

- [ ] **Step 6: 브라우저 검증**

정적 서버로 페이지를 띄운다(포트는 비어 있는 것을 골라 쓰고, `vercel dev`·`vercel build`·`vercel link`는 **실행하지 않는다** — 오너 계정에 원치 않는 프로젝트가 생긴 전례가 있다). `/api/*`는 정적 서버에 없으므로 실패하는 것이 정상이며, 그 실패 처리가 검증 대상이다.

확인 항목:
1. 콘솔에 모듈 로딩 에러가 없다.
2. 표에 9개 행이 렌더된다(회귀 없음).
3. Buy 버튼을 누르면 "Couldn't reach the checkout server…" 메시지가 뜨고 잠금이 풀리지 않는다.
4. 아무 문자열이나 라이선스 키로 넣고 Unlock을 눌러도 잠금이 풀리지 않는다.
5. `?paid=1`을 붙여 로드해도(보류 주문 ID 없이) 아무 일도 일어나지 않고 에러도 없다.
6. 1440x900과 375px 양쪽에서 Buy 버튼과 메시지가 레이아웃을 깨지 않는다.

- [ ] **Step 7: 커밋**

```bash
git add -A && git commit -m "feat: PayPal checkout UI, signature-based license verification"
```

---

### Task 4: 문서와 배포 설정 갱신

**Files:**
- Modify: `README.md`
- Modify: `.vercelignore`

- [ ] **Step 1: README의 Gumroad 내용을 전부 교체**

다음을 반영한다:
- 결제는 PayPal 일회성 결제. 라이선스 키는 `<주문ID>-<HMAC10>`이며 구매 기록 DB가 없다.
- 환경변수 표: `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, `PAYPAL_ENV`, `PICKER_LICENSE_SECRET`, `PICKER_PRICE_USD`.
- 출시 전 체크리스트: PayPal 앱 생성(sandbox → live), 환경변수 5개 설정, `PAYPAL_ENV=live` 전환, 샌드박스 결제 1회 왕복 테스트.
- **알려진 한계**를 정직하게 유지·추가한다:
  - 프리미엄 게이트는 클라이언트 사이드 전용이라 devtools로 우회 가능하다(기존 항목 유지).
  - **`PICKER_LICENSE_SECRET`을 잃거나 바꾸면 발급된 모든 키가 무효화된다.**
  - **환불·차지백 시 키가 자동으로 무효화되지 않는다.**
  - **PayPal은 merchant of record가 아니므로 디지털 상품 판매의 부가세(EU VAT 등) 신고 의무가 판매자에게 있다.** Gumroad를 쓰면 대행되지만 PayPal은 그렇지 않다.
- `"outputDirectory": "."`가 왜 필요한지 설명한 기존 문단을 유지한다(제거하면 사이트가 빈 페이지가 된다).

- [ ] **Step 2: `.vercelignore`에 `lib/` 제외 금지 확인**

`lib/`는 `api/` 함수가 임포트하므로 **절대 제외하면 안 된다.** `.vercelignore`에 `lib/`가 없는지 확인하고, `test/`만 제외된 상태를 유지한다.

- [ ] **Step 3: 커밋**

```bash
git add -A && git commit -m "docs: PayPal 결제·환경변수·알려진 한계 반영"
```

---

## 오너가 해야 할 일 (클로드가 대신할 수 없음)

1. PayPal Developer 대시보드에서 앱 생성 → Client ID / Secret 발급 (계정 인증 필요)
2. Vercel 프로젝트에 환경변수 5개 설정
3. 샌드박스에서 실제 결제 1회 왕복 확인 후 `PAYPAL_ENV=live` 전환
4. 부가세 처리 방침 결정 (PayPal은 대행하지 않음)
