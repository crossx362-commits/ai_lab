// 서버 전용 모듈 — PayPal 시크릿을 쓴다. 브라우저에서 import 하지 않는다.
const HOSTS = {
  live: 'https://api-m.paypal.com',
  sandbox: 'https://api-m.sandbox.paypal.com',
};

/**
 * 환경변수에서 PayPal 설정을 읽는다.
 * PAYPAL_ENV가 정확히 'live'일 때만 라이브다 — 오타·미설정은 전부 샌드박스로 떨어진다.
 * 잘못된 설정이 실제 결제를 받는 것보다 결제가 안 되는 쪽이 안전하다.
 */
export function paypalConfig(env = process.env) {
  const mode = env.PAYPAL_ENV === 'live' ? 'live' : 'sandbox';
  return {
    mode,
    host: HOSTS[mode],
    clientId: mode === 'live' ? env.PAYPAL_CLIENT_ID : env.PAYPAL_SANDBOX_CLIENT_ID,
    secret: mode === 'live' ? env.PAYPAL_CLIENT_SECRET : env.PAYPAL_SANDBOX_CLIENT_SECRET,
  };
}

function basicAuth(cfg) {
  return `Basic ${Buffer.from(`${cfg.clientId}:${cfg.secret}`).toString('base64')}`;
}

async function call(cfg, path, { method = 'GET', token, body } = {}) {
  const response = await fetch(`${cfg.host}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: token ? `Bearer ${token}` : basicAuth(cfg),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const json = await response.json().catch(() => null);
  if (!response.ok) {
    const detail = json?.message || json?.error_description || `HTTP ${response.status}`;
    throw new Error(`PayPal ${path} failed: ${detail}`);
  }
  return json;
}

export async function accessToken(cfg) {
  const response = await fetch(`${cfg.host}/v1/oauth2/token`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
      Authorization: basicAuth(cfg),
    },
    body: 'grant_type=client_credentials',
  });
  const json = await response.json().catch(() => null);
  if (!response.ok || !json?.access_token) {
    throw new Error(`PayPal auth failed: ${json?.error_description || response.status}`);
  }
  return json.access_token;
}

export async function createOrder(cfg, { amount, currency = 'USD', returnUrl, cancelUrl }) {
  const token = await accessToken(cfg);
  const order = await call(cfg, '/v2/checkout/orders', {
    method: 'POST',
    token,
    body: {
      intent: 'CAPTURE',
      purchase_units: [
        {
          amount: { currency_code: currency, value: amount },
          description: 'LLM Cost Picker — Pro license',
        },
      ],
      application_context: {
        brand_name: 'LLM Cost Picker',
        user_action: 'PAY_NOW',
        shipping_preference: 'NO_SHIPPING',
        return_url: returnUrl,
        cancel_url: cancelUrl,
      },
    },
  });
  const approve = (order.links || []).find((l) => l.rel === 'approve' || l.rel === 'payer-action');
  if (!approve) throw new Error('PayPal did not return an approval link');
  return { id: order.id, approveUrl: approve.href };
}

export async function captureOrder(cfg, orderId) {
  const token = await accessToken(cfg);
  const result = await call(cfg, `/v2/checkout/orders/${encodeURIComponent(orderId)}/capture`, {
    method: 'POST',
    token,
    body: {},
  });
  return { id: result.id, status: result.status };
}
