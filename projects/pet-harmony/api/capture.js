const { paypalRequest } = require('./_lib/paypal');
const { supabaseRequest } = require('./_lib/supabase');
const PRODUCT_AMOUNTS = { tips: '0.99', pdf: '3.99' };

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return res.status(405).json({ error: 'method_not_allowed' });
  try {
    const { resultId, product, orderId } = req.body || {};
    if (!resultId || !product || !orderId) return res.status(400).json({ error: 'missing_payment_fields' });
    const orders = await supabaseRequest(`orders?paypal_order_id=eq.${encodeURIComponent(orderId)}&select=product,result_id,status`, { method: 'GET' });
    const savedOrder = orders[0];
    if (!savedOrder || savedOrder.result_id !== resultId || savedOrder.product !== product) {
      return res.status(403).json({ error: 'order_mismatch' });
    }
    if (savedOrder.status === 'completed') return res.status(200).json({ unlocked: true, product, idempotent: true });
    const capture = await paypalRequest(`/v2/checkout/orders/${encodeURIComponent(orderId)}/capture`, { method: 'POST' });
    if (capture.status !== 'COMPLETED') return res.status(402).json({ error: 'payment_not_completed' });
    const paidAmount = capture.purchase_units?.[0]?.payments?.captures?.[0]?.amount?.value;
    if (paidAmount !== PRODUCT_AMOUNTS[product]) return res.status(402).json({ error: 'amount_mismatch' });
    await supabaseRequest(`orders?paypal_order_id=eq.${encodeURIComponent(orderId)}`, { method: 'PATCH', body: JSON.stringify({ status: 'completed' }) });
    await supabaseRequest('unlocks?on_conflict=result_id,product', { method: 'POST', headers: { Prefer: 'resolution=merge-duplicates,return=representation' }, body: JSON.stringify({ result_id: resultId, product, paypal_order_id: orderId, status: 'active' }) });
    return res.status(200).json({ unlocked: true, product });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'payment_not_configured' });
    return res.status(500).json({ error: 'capture_failed' });
  }
};
