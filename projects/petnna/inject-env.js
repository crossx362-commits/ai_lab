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

// Read index.html
let html = fs.readFileSync(indexPath, 'utf8');

// Environment variables from Vercel
const env = {
  SUPABASE_URL: process.env.SUPABASE_URL || '',
  SUPABASE_ANON_KEY: process.env.SUPABASE_ANON_KEY || '',
  GEMINI_API_KEY: process.env.GEMINI_API_KEY || '',
  STRIPE_PAYMENT_LINK: process.env.STRIPE_PAYMENT_LINK || '',
  STRIPE_SHOP_PAYMENT_LINK: process.env.STRIPE_SHOP_PAYMENT_LINK || ''
};

// Create the injection script
const envScript = `
    <script>
        window._env_ = ${JSON.stringify(env, null, 8)};
    </script>`;

// Replace the placeholder - 더 유연한 정규식 (주석 있거나 없어도 매치)
const regex = /<script>[\s\S]*?window\._env_\s*=\s*\{[\s\S]*?\};?[\s\S]*?<\/script>/;

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
