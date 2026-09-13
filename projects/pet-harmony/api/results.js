const { supabaseRequest } = require('./_lib/supabase');

module.exports = async function handler(req, res) {
  const resultId = String(req.query?.resultId || req.body?.resultId || '').replace(/[^a-zA-Z0-9_-]/g, '');
  if (!resultId) return res.status(400).json({ error: 'missing_result_id' });
  try {
    if (req.method === 'GET') {
      const rows = await supabaseRequest(`results?result_id=eq.${encodeURIComponent(resultId)}&select=result_id,payload`, { method: 'GET' });
      if (!rows[0]) return res.status(404).json({ error: 'result_not_found' });
      const payload = { ...(rows[0].payload || {}) };
      const unlocks = await supabaseRequest(`unlocks?result_id=eq.${encodeURIComponent(resultId)}&product=eq.tips&status=eq.active&select=result_id`, { method: 'GET' });
      if (!unlocks.length) delete payload.paid;
      delete payload.unlocked;
      delete payload.pdfPaid;
      return res.status(200).json({ result_id: rows[0].result_id, payload });
    }
    if (req.method === 'POST') {
      const payload = { ...(req.body?.payload || {}) };
      delete payload.photoData;
      delete payload.unlocked;
      delete payload.pdfPaid;
      await supabaseRequest('results?on_conflict=result_id', {
        method: 'POST',
        headers: { Prefer: 'resolution=merge-duplicates,return=representation' },
        body: JSON.stringify({ result_id: resultId, payload }),
      });
      return res.status(200).json({ saved: true, resultId });
    }
    return res.status(405).json({ error: 'method_not_allowed' });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'results_not_configured' });
    return res.status(500).json({ error: 'result_store_failed' });
  }
};
