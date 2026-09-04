import { test } from 'node:test';
import assert from 'node:assert/strict';
import { monthlyCost } from '../src/cost.js';
import { MODELS } from '../src/pricing.js';

const fakeModel = {
  id: 'fake',
  name: 'Fake',
  provider: 'Test',
  inputPer1M: 3,
  outputPer1M: 15,
  cachedInputPer1M: 0.3,
  contextWindow: 200000,
  tier: 'balanced',
  speed: 2,
};

test('monthlyCost multiplies tokens by per-1M rates', () => {
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0,
  };
  const result = monthlyCost(fakeModel, usage);
  assert.equal(result.inputCost, 3);
  assert.equal(result.outputCost, 15);
  assert.equal(result.total, 18);
});

test('monthlyCost applies the cache hit rate to input tokens only', () => {
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0.5,
  };
  const result = monthlyCost(fakeModel, usage);
  // 절반은 정가 3, 절반은 캐시가 0.3 → 1.65
  assert.equal(result.inputCost, 1.65);
  assert.equal(result.outputCost, 15);
});

test('monthlyCost ignores the cache hit rate when the model has no cache pricing', () => {
  const noCache = { ...fakeModel, cachedInputPer1M: null };
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0.9,
  };
  assert.equal(monthlyCost(noCache, usage).inputCost, 3);
});

test('monthlyCost returns zero for zero usage', () => {
  const usage = {
    requestsPerMonth: 0,
    inputTokens: 500,
    outputTokens: 500,
    cacheHitRate: 0,
  };
  assert.equal(monthlyCost(fakeModel, usage).total, 0);
});

test('MODELS entries all carry the required fields', () => {
  assert.ok(MODELS.length >= 6, 'expected at least 6 models');
  for (const m of MODELS) {
    assert.equal(typeof m.id, 'string');
    assert.equal(typeof m.name, 'string');
    assert.equal(typeof m.provider, 'string');
    assert.equal(typeof m.inputPer1M, 'number');
    assert.equal(typeof m.outputPer1M, 'number');
    assert.equal(typeof m.contextWindow, 'number');
    assert.ok(['budget', 'balanced', 'premium'].includes(m.tier));
    assert.ok([1, 2, 3].includes(m.speed));
    assert.ok(m.cachedInputPer1M === null || typeof m.cachedInputPer1M === 'number');
  }
});

test('MODELS ids are unique', () => {
  const ids = MODELS.map((m) => m.id);
  assert.equal(new Set(ids).size, ids.length);
});
