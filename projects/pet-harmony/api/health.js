module.exports = function handler(req, res) {
  const paymentConfigured = !!(process.env.PAYPAL_CLIENT_ID && process.env.PAYPAL_CLIENT_SECRET && process.env.SUPABASE_URL && process.env.SUPABASE_SERVICE_ROLE_KEY);
  const emailConfigured = !!(process.env.RESEND_API_KEY && process.env.REPORT_FROM_EMAIL);
  res.status(200).json({ ok: true, service: 'pet-harmony', paymentConfigured, webhookConfigured: paymentConfigured && !!process.env.PAYPAL_WEBHOOK_ID, emailConfigured });
};
