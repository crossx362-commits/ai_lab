const { paypalRequest } = require('./_lib/paypal');
const { supabaseRequest } = require('./_lib/supabase');

const PRODUCTS = { tips: '0.99', pdf: '3.99' };

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return res.status(405).json({ error: 'method_not_allowed' });
  try {
    const { resultId, product } = req.body || {};
    if (!resultId || !PRODUCTS[product]) return res.status(400).json({ error: 'invalid_product' });
    const order = await paypalRequest('/v2/checkout/orders', {
      method: 'POST',
      headers: { 'PayPal-Request-Id': `${resultId}-${product}-${Date.now()}` },
      body: JSON.stringify({
        intent: 'CAPTURE',
        purchase_units: [{ reference_id: resultId, custom_id: `${resultId}:${product}`, amount: { currency_code: 'USD', value: PRODUCTS[product] } }],
      }),
    });
    await supabaseRequest('orders', { method: 'POST', body: JSON.stringify({ result_id: resultId, product, paypal_order_id: order.id, status: 'created' }) });
    return res.status(200).json({ orderId: order.id });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'payment_not_configured' });
    return res.status(500).json({ error: 'order_create_failed' });
  }
};
