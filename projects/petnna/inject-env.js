#!/usr/bin/env node
/**
 * inject-env.js - Inject environment variables into index.html at build time
 *
 * Vercel에서 빌드 시 실행되어 환경 변수를 index.html에 주입합니다.
 * Vercel 환경 변수로부터 값을 읽어서 window._env_ 객체에 설정합니다.
 */

const fs = require('fs');
const path = require('path');

const indexPath = path.join(__dirname, 'index.html');

// 로컬 개발 시 .env 파일에서 폴백 로드
const envFilePath = path.join(__dirname, '..', '..', '.env');
if (fs.existsSync(envFilePath)) {
  fs.readFileSync(envFilePath, 'utf8').split('\n').forEach(line => {
    line = line.trim();
    if (!line || line.startsWith('#') || !line.includes('=')) return;
    const [key, ...rest] = line.split('=');
    const value = rest.join('=').replace(/^["']|["']$/g, '').trim();
    if (!process.env[key.trim()]) process.env[key.trim()] = value;
  });
}

// Read index.html
let html = fs.readFileSync(indexPath, 'utf8');

// Environment variables
// GEMINI_API_KEY is server-only for /api/ai-health and must not be injected into HTML.
const env = {
  SUPABASE_URL: process.env.SUPABASE_URL || '',
  SUPABASE_ANON_KEY: process.env.SUPABASE_ANON_KEY || '',
  // 가입 허용 이메일. 이 키가 목록에서 빠져 있어서 빌드가 돌 때마다 index.html에서
  // 통째로 사라졌고, app.js는 값이 없으면 '제한 없음'으로 fail-open 한다
  // (app.js:503, supabase.js:177). 즉 배포본에서 프론트 가입 게이트가 꺼져 있었다
  // (2026-07-31 발견 — 배포본에 이 키가 0건).
  // 기본값을 오너 이메일로 둬 fail-closed 한다 — 환경변수 미설정이 곧 '전면 개방'이
  // 되면 안 된다. 진짜 방어선은 DB 트리거(migrations/restrict_signups_allowlist.sql)이고
  // 이건 UI 단 이중 방어다.
  ALLOWED_LOGIN_EMAILS: process.env.ALLOWED_LOGIN_EMAILS || 'crossx362@gmail.com',
  GEMINI_API_KEY: '',
  AI_HEALTH_ENABLED: process.env.AI_HEALTH_ENABLED || 'false',
  AI_HEALTH_PROXY_PATH: process.env.AI_HEALTH_PROXY_PATH || '/api/ai-health',
  PAYMENTS_ENABLED: process.env.PAYMENTS_ENABLED || 'false',
  PREMIUM_ACTIVATION_ENABLED: process.env.PREMIUM_ACTIVATION_ENABLED || 'false',
  STRIPE_PAYMENT_LINK: process.env.STRIPE_PAYMENT_LINK || '',
  STRIPE_PAYMENT_LINK_YEARLY: process.env.STRIPE_PAYMENT_LINK_YEARLY || '',
  STRIPE_SHOP_PAYMENT_LINK: process.env.STRIPE_SHOP_PAYMENT_LINK || ''
};

// Create the injection script
const envScript = `
    <script>
        window._env_ = ${JSON.stringify(env, null, 8)};
    </script>`;

// window._env_ 블록만 정확히 매치 (다른 <script> 블록 삼키지 않도록)
const regex = /<script>\s*window\._env_\s*=\s*\{[\s\S]*?\};\s*<\/script>/;

if (regex.test(html)) {
  html = html.replace(regex, envScript.trim());
  fs.writeFileSync(indexPath, html, 'utf8');
  console.log('✅ Environment variables injected into index.html');
  console.log(`   SUPABASE_URL: ${env.SUPABASE_URL ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   SUPABASE_ANON_KEY: ${env.SUPABASE_ANON_KEY ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   AI_HEALTH_ENABLED: ${env.AI_HEALTH_ENABLED}`);
  console.log(`   PAYMENTS_ENABLED: ${env.PAYMENTS_ENABLED}`);
  console.log(`   STRIPE_PAYMENT_LINK: ${env.STRIPE_PAYMENT_LINK ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   STRIPE_PAYMENT_LINK_YEARLY: ${env.STRIPE_PAYMENT_LINK_YEARLY ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   STRIPE_SHOP_PAYMENT_LINK: ${env.STRIPE_SHOP_PAYMENT_LINK ? 'Set ✓' : 'Empty ✗'}`);
} else {
  console.log('⚠️  Could not find environment variable placeholder in index.html');
  console.log('   Searching for pattern: <script>...// Environment variables...window._env_...</script>');
  console.log('   Skipping injection');
}
