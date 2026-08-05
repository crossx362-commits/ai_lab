import { paypalConfig, captureOrder } from '../src/paypal.js';
import { signLicense } from '../src/license.js';

// PayPal은 승인 후 return_url로 ?token=<주문ID>를 붙여 리다이렉트한다.
export default async function handler(req, res) {
  const site = process.env.PICKER_SITE_URL || '';
  const orderId = typeof req.query?.token === 'string' ? req.query.token : '';
  const back = (params) => {
    res.setHeader('Location', `${site}/?${new URLSearchParams(params)}`);
    res.status(302).end();
  };

  if (!orderId) return back({ checkout: 'failed' });

  const secret = process.env.PICKER_LICENSE_SECRET;
  const cfg = paypalConfig();
  if (!secret || !cfg.clientId) return back({ checkout: 'failed' });

  try {
    const result = await captureOrder(cfg, orderId);
    if (result.status !== 'COMPLETED') return back({ checkout: 'failed' });
    return back({ license: signLicense(result.id, secret) });
  } catch (error) {
    console.error('capture-order failed:', error.message);
    return back({ checkout: 'failed' });
  }
}
