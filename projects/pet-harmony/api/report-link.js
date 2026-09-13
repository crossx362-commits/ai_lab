const { supabaseRequest } = require('./_lib/supabase');

module.exports = async function handler(req, res) {
  if (req.method !== 'POST') return res.status(405).json({ error: 'method_not_allowed' });
  const { resultId, email } = req.body || {};
  if (!/^[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}$/.test(String(email || '')) || !/^[\w-]{6,80}$/.test(String(resultId || ''))) {
    return res.status(400).json({ error: 'invalid_request' });
  }
  if (!process.env.RESEND_API_KEY || !process.env.REPORT_FROM_EMAIL) {
    return res.status(503).json({ error: 'email_not_configured' });
  }
  try {
    const unlocks = await supabaseRequest(`unlocks?result_id=eq.${encodeURIComponent(resultId)}&product=eq.pdf&status=eq.active&select=result_id`, { method: 'GET' });
    if (!unlocks.length) return res.status(403).json({ error: 'pdf_not_unlocked' });
    const link = `${process.env.PUBLIC_SITE_URL || 'https://petnna-app.vercel.app'}/#/result/${encodeURIComponent(resultId)}`;
    const response = await fetch('https://api.resend.com/emails', {
      method: 'POST',
      headers: { Authorization: `Bearer ${process.env.RESEND_API_KEY}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({
        from: process.env.REPORT_FROM_EMAIL,
        to: [email],
        subject: '펫과나 최종 리포트 링크',
        html: `<p>결제하신 펫과나 최종 리포트를 다시 열어볼 수 있어요.</p><p><a href="${link}">리포트 열기</a></p><p>브라우저에서 Print / Save as PDF를 눌러 파일로 저장할 수 있습니다.</p>`,
      }),
    });
    if (!response.ok) return res.status(502).json({ error: 'email_send_failed' });
    return res.status(200).json({ sent: true });
  } catch (error) {
    if (error.code === 'NOT_CONFIGURED') return res.status(503).json({ error: 'email_not_configured' });
    return res.status(500).json({ error: 'email_failed' });
  }
};
