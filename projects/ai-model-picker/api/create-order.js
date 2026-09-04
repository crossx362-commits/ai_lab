import { paypalConfig, createOrder } from '../src/paypal.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }
  const cfg = paypalConfig();
  const site = process.env.PICKER_SITE_URL;
  if (!cfg.clientId || !cfg.secret || !site) {
    res.status(500).json({ error: 'Checkout is not configured yet.' });
    return;
  }
  try {
    const { approveUrl } = await createOrder(cfg, {
      amount: process.env.PICKER_PRICE_USD || '9.00',
      returnUrl: `${site}/api/capture-order`,
      cancelUrl: `${site}/?checkout=cancelled`,
    });
    res.status(200).json({ approveUrl });
  } catch (error) {
    console.error('create-order failed:', error.message);
    res.status(502).json({ error: 'Could not start checkout. Try again.' });
  }
}
