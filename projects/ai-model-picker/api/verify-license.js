import { verifyLicense } from '../src/license.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ valid: false, reason: 'Method not allowed.' });
    return;
  }
  const secret = process.env.PICKER_LICENSE_SECRET;
  if (!secret) {
    res.status(500).json({ valid: false, reason: 'Server is not configured yet.' });
    return;
  }
  const key = typeof req.body?.licenseKey === 'string' ? req.body.licenseKey : '';
  if (!key || key.length > 200) {
    res.status(400).json({ valid: false, reason: 'Enter your license key.' });
    return;
  }
  const { valid, reason } = verifyLicense(key, secret);
  res.status(200).json({ valid, reason });
}
