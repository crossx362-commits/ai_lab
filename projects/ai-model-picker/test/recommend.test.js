import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rankModels, fitsContext } from '../src/recommend.js';

const cheap = {
  id: 'cheap', name: 'Cheap', provider: 'T',
  inputPer1M: 1, outputPer1M: 2, cachedInputPer1M: null,
  contextWindow: 8000, tier: 'budget', speed: 3,
};
const smart = {
  id: 'smart', name: 'Smart', provider: 'T',
  inputPer1M: 15, outputPer1M: 75, cachedInputPer1M: null,
  contextWindow: 200000, tier: 'premium', speed: 1,
};
const middle = {
  id: 'middle', name: 'Middle', provider: 'T',
  inputPer1M: 3, outputPer1M: 15, cachedInputPer1M: null,
  contextWindow: 128000, tier: 'balanced', speed: 2,
};
const all = [smart, cheap, middle];

const usage = {
  requestsPerMonth: 10000, inputTokens: 1000,
  outputTokens: 500, cacheHitRate: 0,
};

test('cost priority ranks the cheapest model first', () => {
  const ranked = rankModels(all, usage, 'cost');
  assert.equal(ranked[0].model.id, 'cheap');
  assert.equal(ranked.at(-1).model.id, 'smart');
});

test('quality priority ranks the premium tier first', () => {
  const ranked = rankModels(all, usage, 'quality');
  assert.equal(ranked[0].model.id, 'smart');
});

test('speed priority ranks the fastest model first', () => {
  const ranked = rankModels(all, usage, 'speed');
  assert.equal(ranked[0].model.id, 'cheap');
});

test('context priority ranks the largest context window first', () => {
  const ranked = rankModels(all, usage, 'context');
  assert.equal(ranked[0].model.id, 'smart');
});

test('rankModels attaches the computed cost to every entry', () => {
  const ranked = rankModels(all, usage, 'cost');
  assert.equal(ranked.length, 3);
  for (const entry of ranked) {
    assert.equal(typeof entry.cost.total, 'number');
    assert.ok(entry.cost.total > 0);
    assert.equal(typeof entry.score, 'number');
  }
});

test('rankModels returns scores in descending order', () => {
  const ranked = rankModels(all, usage, 'quality');
  for (let i = 1; i < ranked.length; i += 1) {
    assert.ok(ranked[i - 1].score >= ranked[i].score);
  }
});

test('rankModels does not mutate the input array', () => {
  const input = [smart, cheap, middle];
  rankModels(input, usage, 'cost');
  assert.equal(input[0].id, 'smart');
});

test('fitsContext compares combined tokens against the context window', () => {
  assert.equal(fitsContext(cheap, { inputTokens: 1000, outputTokens: 500 }), true);
  assert.equal(fitsContext(cheap, { inputTokens: 9000, outputTokens: 500 }), false);
  assert.equal(fitsContext(smart, { inputTokens: 9000, outputTokens: 500 }), true);
});
