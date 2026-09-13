module.exports = function handler(req, res) {
  if (req.method !== 'GET') return res.status(405).json({ error: 'method_not_allowed' });
  res.setHeader('Cache-Control', 'no-store');
  res.status(200).json({ clientId: process.env.PAYPAL_CLIENT_ID || null, environment: process.env.PAYPAL_ENV || 'sandbox' });
};
