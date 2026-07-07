const assert = require('assert');
const I = require('../../js/petgame/game-items.js');

// 먹이 6종, 스펙 가격
assert.strictEqual(I.FOODS.length, 6);
const priceMap = Object.fromEntries(I.FOODS.map(f => [f.id, f.price]));
assert.deepStrictEqual(priceMap, { kibble: 10, bone: 20, milk: 15, meat: 40, cake: 60, bento: 100 });

// 아이템 26종, id 유니크, 공간·해금레벨 유효
assert.strictEqual(I.ITEMS.length, 26);
assert.strictEqual(new Set(I.ITEMS.map(i => i.id)).size, 26);
for (const it of I.ITEMS) {
  assert.ok(['room', 'yard'].includes(it.space), it.id);
  assert.ok(it.unlockLv >= 1 && it.unlockLv <= 10, it.id);
  assert.ok(['L', 'M', 'S'].includes(it.sizeClass), it.id);
  assert.ok(it.price > 0 && it.basePx > 0, it.id);
}
// Lv1 기본템 8종 / Lv10 황금개집
assert.strictEqual(I.ITEMS.filter(i => i.unlockLv === 1).length, 8);
assert.ok(I.ITEMS.some(i => i.id === 'golden_doghouse' && i.unlockLv === 10));

// 해금 필터: Lv1은 8종만, Lv10은 전체
assert.strictEqual(I.itemsForSpace('yard', 1).length + I.itemsForSpace('room', 1).length, 8);
assert.strictEqual(I.itemsForSpace('yard', 10).length + I.itemsForSpace('room', 10).length, 26);

// 성장 단계
assert.strictEqual(I.stageForLevel(1).stage, 1);
assert.strictEqual(I.stageForLevel(3).stage, 1);
assert.strictEqual(I.stageForLevel(4).stage, 2);
assert.strictEqual(I.stageForLevel(7).stage, 3);
assert.strictEqual(I.stageForLevel(10).stage, 4);
assert.strictEqual(I.stageForLevel(10).scale, 1.0);

console.log('test_items OK');
