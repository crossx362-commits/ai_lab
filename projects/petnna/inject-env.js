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
// GEMINI_API_KEY는 로컬 .env 폴백 제외 — Vercel 빌드 시에만 주입 (git 노출 방지)
const isVercelBuild = !!process.env.VERCEL;
const env = {
  SUPABASE_URL: process.env.SUPABASE_URL || '',
  SUPABASE_ANON_KEY: process.env.SUPABASE_ANON_KEY || '',
  GEMINI_API_KEY: isVercelBuild ? (process.env.GEMINI_API_KEY || '') : '',
  STRIPE_PAYMENT_LINK: process.env.STRIPE_PAYMENT_LINK || '',
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
  console.log(`   GEMINI_API_KEY: ${env.GEMINI_API_KEY ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   STRIPE_PAYMENT_LINK: ${env.STRIPE_PAYMENT_LINK ? 'Set ✓' : 'Empty ✗'}`);
  console.log(`   STRIPE_SHOP_PAYMENT_LINK: ${env.STRIPE_SHOP_PAYMENT_LINK ? 'Set ✓' : 'Empty ✗'}`);
} else {
  console.log('⚠️  Could not find environment variable placeholder in index.html');
  console.log('   Searching for pattern: <script>...// Environment variables...window._env_...</script>');
  console.log('   Skipping injection');
}
