const assert = require('assert');
const H = require('../../js/harmony/harmony-core.js');

// 갑자 앵커
assert.strictEqual(H.yearPillar(1984).stem + H.yearPillar(1984).branch, '갑자');
assert.strictEqual(H.yearPillar(2026).stem + H.yearPillar(2026).branch, '병오');
const d1900 = H.dayPillar(new Date(Date.UTC(1900, 0, 1)));
assert.strictEqual(d1900.stem + d1900.branch, '갑술');
const d2000 = H.dayPillar(new Date(Date.UTC(2000, 0, 1)));
assert.strictEqual(d2000.stem + d2000.branch, '무오');

// 오행 벡터: 5글자 집계, 합 5
const e = H.elementsOf('2024-03-15');
assert.strictEqual(Object.values(e.vec).reduce((a, b) => a + b, 0), 5);
assert.ok(['목','화','토','금','수'].includes(e.dominant));

// 궁합: 결정성·범위·대칭
const h1 = H.harmony('2024-03-15', '1990-07-07');
const h2 = H.harmony('2024-03-15', '1990-07-07');
assert.strictEqual(h1.score, h2.score);
assert.ok(h1.score >= 5 && h1.score <= 99);
const hSwap = H.harmony('1990-07-07', '2024-03-15');
assert.strictEqual(h1.score, hSwap.score); // 상생/상극 판정이 양방향이라 대칭
assert.ok(['상생','상극','동일','중립'].includes(h1.relation.type));
assert.ok(h1.relation.text.length > 5);

// 영역 4종: 키·범위·등급·팁
const ar = H.areas('2024-03-15', '1990-07-07');
assert.strictEqual(ar.length, 4);
assert.deepStrictEqual(ar.map(a => a.key), ['play','meal','walk','rest']);
for (const a of ar) {
  assert.ok(a.score >= 5 && a.score <= 99, a.key);
  assert.ok(['최고','좋음','보통','노력','주의'].includes(a.grade), a.key);
  assert.ok(a.tip.length > 5, a.key);
}

// 일진: boolean 3종 + 오늘지수 범위·보정 방향
const luck = H.dayLuck(new Date(Date.UTC(2026, 6, 7)), '수');
assert.ok(['walk','groom','vet'].every(k => typeof luck[k] === 'boolean'));
const idx = H.todayIndex(70, '수', new Date(Date.UTC(2026, 6, 7)));
assert.ok(idx >= 5 && idx <= 99);
assert.ok(Math.abs(idx - 70) <= 15);

// analyze: harmonyData 병합용 형태
const full = H.analyze('2024-03-15', '1990-07-07');
assert.ok(full.score && full.elements.pet.vec && full.areas.length === 4 && full.measuredAt);
console.log('harmony test_core OK');
