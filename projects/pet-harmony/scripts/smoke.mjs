const base = process.argv[2] || 'https://pet-harmony-ebon.vercel.app';
const checks = [
  ['home', '/'],
  ['css', '/css/app.css'],
  ['app', '/js/app.js'],
  ['manifest', '/manifest.webmanifest'],
  ['hero-webp', '/assets/webp/pet-harmony-hero.webp'],
  ['health', '/api/health'],
];

for (const [name, path] of checks) {
  const response = await fetch(base + path);
  if (!response.ok) throw new Error(`${name}: HTTP ${response.status}`);
  console.log(`PASS ${name} ${response.status}`);
}

const health = await fetch(base + '/api/health').then((response) => response.json());
if (health.ok !== true) throw new Error('health: ok=false');
console.log(`INFO paymentConfigured=${health.paymentConfigured}`);
console.log(`INFO webhookConfigured=${health.webhookConfigured}`);

const orderResponse = await fetch(base + '/api/orders', {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ resultId: 'qa-smoke', product: 'tips' }),
});
if (health.paymentConfigured === false && orderResponse.status !== 503) {
  throw new Error(`orders should be 503 before configuration, got ${orderResponse.status}`);
}
console.log(`PASS payment-guard ${orderResponse.status}`);

const resultResponse = await fetch(base + '/api/results?resultId=qa-smoke');
if (health.paymentConfigured === false && resultResponse.status !== 503) {
  throw new Error(`results should be 503 before configuration, got ${resultResponse.status}`);
}
console.log(`PASS result-recovery-guard ${resultResponse.status}`);
