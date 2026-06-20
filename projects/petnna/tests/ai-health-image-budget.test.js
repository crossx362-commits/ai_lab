const assert = require('assert');
const fs = require('fs');
const vm = require('vm');

const code = fs.readFileSync(require.resolve('../js/ai-health.js'), 'utf8');

const ctx = {
    console,
    Date,
    fetch: async () => ({ ok: true, json: async () => ({ ok: true }) }),
    isAiHealthEnabled: () => true,
    getAiHealthProxyPath: () => '/api/ai-health',
};

vm.createContext(ctx);
vm.runInContext(code, ctx);

assert.strictEqual(typeof ctx.normalizeAiImagePayload, 'function', 'AI image payload should be normalized before upload');
assert.ok(ctx.AI_HEALTH_IMAGE_BUDGET.maxBase64Chars <= 900000, 'client budget should match or beat the server guard');

const oversized = ctx.normalizeAiImagePayload('a'.repeat(ctx.AI_HEALTH_IMAGE_BUDGET.maxBase64Chars + 1));
assert.strictEqual(oversized.error, true, 'oversized images should be rejected before calling the API');
assert.match(oversized.message, /이미지/, 'oversized rejection should explain the image limit');

const safe = ctx.normalizeAiImagePayload('a'.repeat(1024));
assert.strictEqual(safe.error, false, 'small images should pass the client budget check');
assert.strictEqual(safe.imageBase64.length, 1024);

console.log('ai health image budget tests passed');
