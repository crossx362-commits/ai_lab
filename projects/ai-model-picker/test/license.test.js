import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signLicense, verifyLicense } from '../src/license.js';

const SECRET = 'test-secret-do-not-use-in-production';
const ORDER = '5O190127TN364715T';

test('signLicense returns the order id joined to a signature', () => {
  const key = signLicense(ORDER, SECRET);
  assert.ok(key.startsWith(`${ORDER}-`));
  assert.equal(key.slice(ORDER.length + 1).length, 10);
});

test('signLicense is deterministic', () => {
  assert.equal(signLicense(ORDER, SECRET), signLicense(ORDER, SECRET));
});

test('signLicense produces a different signature for a different secret', () => {
  assert.notEqual(signLicense(ORDER, SECRET), signLicense(ORDER, 'other-secret'));
});

test('verifyLicense accepts a key it just signed', () => {
  const result = verifyLicense(signLicense(ORDER, SECRET), SECRET);
  assert.equal(result.valid, true);
  assert.equal(result.orderId, ORDER);
});

test('verifyLicense is case and whitespace tolerant', () => {
  const key = signLicense(ORDER, SECRET);
  assert.equal(verifyLicense(`  ${key.toLowerCase()}  `, SECRET).valid, true);
});

test('verifyLicense rejects a tampered signature', () => {
  const key = signLicense(ORDER, SECRET);
  const tampered = `${key.slice(0, -1)}${key.at(-1) === 'A' ? 'B' : 'A'}`;
  assert.equal(verifyLicense(tampered, SECRET).valid, false);
});

test('verifyLicense rejects a tampered order id', () => {
  const key = signLicense(ORDER, SECRET);
  assert.equal(verifyLicense(`X${key.slice(1)}`, SECRET).valid, false);
});

test('verifyLicense rejects a key signed with a different secret', () => {
  assert.equal(verifyLicense(signLicense(ORDER, 'other-secret'), SECRET).valid, false);
});

test('verifyLicense rejects malformed input rather than throwing', () => {
  for (const bad of [null, undefined, '', '   ', 'nodash', '-', 'ABC-', 42, {}]) {
    const result = verifyLicense(bad, SECRET);
    assert.equal(result.valid, false, `expected ${JSON.stringify(bad)} to be invalid`);
    assert.ok(result.reason.length > 0);
  }
});

test('verifyLicense rejects a signature of the wrong length', () => {
  assert.equal(verifyLicense(`${ORDER}-ABC`, SECRET).valid, false);
});
