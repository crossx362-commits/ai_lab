const { supabaseRequest } = require('./_lib/supabase');

module.exports = async function handler(req, res) {
  if (req.method !== 'GET') return res.status(405).json({ error: 'method_not_allowed' });
  const resultId = String(req.query?.resultId || '').replace(/[^a-zA-Z0-9_-]/g, '');
  if (!resultId) return res.status(400).json({ error: 'missing_result_id' });
  try {
    const rows = await supabaseRequest(`unlocks?result_id=eq.${encodeURIComponent(resultId)}&status=eq.active&select=product`, { method: 'GET' });
    return res.status(200).json({ tips: rows.some((row) => row.product === 'tips'), pdf: rows.some((row) => row.product === 'pdf') });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'payment_not_configured' });
    return res.status(500).json({ error: 'unlock_status_failed' });
  }
};
