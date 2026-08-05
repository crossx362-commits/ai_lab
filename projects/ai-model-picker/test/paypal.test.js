import { test } from 'node:test';
import assert from 'node:assert/strict';
import { paypalConfig } from '../src/paypal.js';

test('paypalConfig defaults to sandbox when PAYPAL_ENV is unset', () => {
  const cfg = paypalConfig({
    PAYPAL_SANDBOX_CLIENT_ID: 'sb-id',
    PAYPAL_SANDBOX_CLIENT_SECRET: 'sb-secret',
  });
  assert.equal(cfg.mode, 'sandbox');
  assert.equal(cfg.host, 'https://api-m.sandbox.paypal.com');
  assert.equal(cfg.clientId, 'sb-id');
});

test('paypalConfig only selects live on an exact match', () => {
  for (const value of ['LIVE', 'production', 'true', '']) {
    assert.equal(paypalConfig({ PAYPAL_ENV: value }).mode, 'sandbox', `${value} must not go live`);
  }
  assert.equal(paypalConfig({ PAYPAL_ENV: 'live' }).mode, 'live');
});

test('paypalConfig picks the credentials matching the mode', () => {
  const env = {
    PAYPAL_ENV: 'live',
    PAYPAL_CLIENT_ID: 'live-id',
    PAYPAL_CLIENT_SECRET: 'live-secret',
    PAYPAL_SANDBOX_CLIENT_ID: 'sb-id',
    PAYPAL_SANDBOX_CLIENT_SECRET: 'sb-secret',
  };
  const cfg = paypalConfig(env);
  assert.equal(cfg.clientId, 'live-id');
  assert.equal(cfg.secret, 'live-secret');
  assert.equal(cfg.host, 'https://api-m.paypal.com');
});
