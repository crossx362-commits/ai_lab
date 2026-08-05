import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatUSD, toMarkdown, toCSV } from '../src/format.js';

const ranked = [
  {
    model: { id: 'a', name: 'Model A', provider: 'Anthropic', contextWindow: 200000 },
    cost: { inputCost: 10, outputCost: 20, total: 30 },
    score: 0.9,
  },
  {
    model: { id: 'b', name: 'Model B, Turbo', provider: 'OpenAI', contextWindow: 128000 },
    cost: { inputCost: 5, outputCost: 5, total: 10 },
    score: 0.4,
  },
];

const usage = {
  requestsPerMonth: 10000, inputTokens: 1000,
  outputTokens: 500, cacheHitRate: 0,
};

test('formatUSD renders two decimals with thousands separators', () => {
  assert.equal(formatUSD(1234.5), '$1,234.50');
  assert.equal(formatUSD(0), '$0.00');
  assert.equal(formatUSD(7), '$7.00');
});

test('formatUSD marks tiny non-zero amounts', () => {
  assert.equal(formatUSD(0.004), '<$0.01');
});

test('toMarkdown includes a header row and one row per model', () => {
  const md = toMarkdown(ranked, usage);
  assert.ok(md.includes('| Model |'));
  assert.ok(md.includes('Model A'));
  assert.ok(md.includes('Model B, Turbo'));
  assert.ok(md.includes('$30.00'));
});

test('toMarkdown states the usage assumptions', () => {
  const md = toMarkdown(ranked, usage);
  assert.ok(md.includes('10000'));
  assert.ok(md.includes('1000'));
});

test('toCSV emits a header row plus one row per model', () => {
  const lines = toCSV(ranked).trim().split('\n');
  assert.equal(lines.length, 3);
  assert.ok(lines[0].startsWith('Model,Provider'));
});

test('toCSV quotes values containing commas', () => {
  const csv = toCSV(ranked);
  assert.ok(csv.includes('"Model B, Turbo"'));
});
