const { paypalRequest } = require('./_lib/paypal');
const { supabaseRequest } = require('./_lib/supabase');

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return res.status(405).json({ error: 'method_not_allowed' });
  if (!process.env.PAYPAL_WEBHOOK_ID) return res.status(503).json({ error: 'webhook_not_configured' });
  try {
    const event = req.body || {};
    const verification = await paypalRequest('/v1/notifications/verify-webhook-signature', {
      method: 'POST',
      body: JSON.stringify({
        auth_algo: req.headers['paypal-auth-algo'],
        cert_url: req.headers['paypal-cert-url'],
        transmission_id: req.headers['paypal-transmission-id'],
        transmission_sig: req.headers['paypal-transmission-sig'],
        transmission_time: req.headers['paypal-transmission-time'],
        webhook_id: process.env.PAYPAL_WEBHOOK_ID,
        webhook_event: event,
      }),
    });
    if (verification.verification_status !== 'SUCCESS') return res.status(400).json({ error: 'invalid_webhook' });
    if (event.event_type !== 'PAYMENT.CAPTURE.COMPLETED') return res.status(200).json({ received: true, ignored: true });
    const orderId = event.resource?.supplementary_data?.related_ids?.order_id;
    if (!orderId) return res.status(400).json({ error: 'invalid_event_data' });
    let customId = event.resource?.custom_id || '';
    if (!customId) {
      const order = await paypalRequest(`/v2/checkout/orders/${encodeURIComponent(orderId)}`);
      customId = order.purchase_units?.[0]?.custom_id || '';
    }
    const [resultId, product] = String(customId).split(':');
    if (!resultId || !['tips', 'pdf'].includes(product)) return res.status(200).json({ received: true, ignored: true, reason: 'missing_custom_id' });
    await supabaseRequest(`orders?paypal_order_id=eq.${encodeURIComponent(orderId)}`, { method: 'PATCH', body: JSON.stringify({ status: 'completed' }) });
    await supabaseRequest('unlocks?on_conflict=result_id,product', { method: 'POST', headers: { Prefer: 'resolution=merge-duplicates,return=representation' }, body: JSON.stringify({ result_id: resultId, product, paypal_order_id: orderId, status: 'active' }) });
    return res.status(200).json({ received: true });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'payment_not_configured' });
    return res.status(500).json({ error: 'webhook_failed' });
  }
};
