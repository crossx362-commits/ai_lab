const assert = require('assert');
const I = require('../../js/petgame/game-items.js');

// 먹이: 기본 6종 가격 고정(부분집합 검증 — 카탈로그 확장에 안 깨짐), 전 품목 가격>0
const priceMap = Object.fromEntries(I.FOODS.map(f => [f.id, f.price]));
for (const [id, price] of Object.entries({ kibble: 10, bone: 20, milk: 15, meat: 40, cake: 60, bento: 100 })) {
  assert.strictEqual(priceMap[id], price, `food price ${id}`);
}
for (const f of I.FOODS) assert.ok(f.price > 0, `food price>0 ${f.id}`);

// 아이템 id 유니크, 공간·해금레벨 유효
assert.strictEqual(new Set(I.ITEMS.map(i => i.id)).size, I.ITEMS.length);
for (const it of I.ITEMS) {
  assert.ok(['room', 'yard'].includes(it.space), it.id);
  assert.ok(it.unlockLv >= 1 && it.unlockLv <= 10, it.id);
  assert.ok(['L', 'M', 'S'].includes(it.sizeClass), it.id);
  assert.ok(it.price > 0 && it.basePx > 0, it.id);
}
// Lv1 기본템 8종 / Lv10 황금개집
assert.strictEqual(I.ITEMS.filter(i => i.unlockLv === 1).length, 8);
assert.ok(I.ITEMS.some(i => i.id === 'golden_doghouse' && i.unlockLv === 10));

// 해금 필터: Lv1은 8종, Lv10은 전체(상대 단언 — 카탈로그 확장 자동 추종)
assert.strictEqual(I.itemsForSpace('yard', 1).length + I.itemsForSpace('room', 1).length, 8);
assert.strictEqual(I.itemsForSpace('yard', 10).length + I.itemsForSpace('room', 10).length, I.ITEMS.length);

// 성장 단계
assert.strictEqual(I.stageForLevel(1).stage, 1);
assert.strictEqual(I.stageForLevel(3).stage, 1);
assert.strictEqual(I.stageForLevel(4).stage, 2);
assert.strictEqual(I.stageForLevel(7).stage, 3);
assert.strictEqual(I.stageForLevel(10).stage, 4);
assert.strictEqual(I.stageForLevel(10).scale, 1.0);

console.log('test_items OK');
