const assert = require('assert');
global.PetGameItems = require('../../js/petgame/game-items.js');
const C = require('../../js/petgame/game-core.js');

let saved = 0;
C.init({ saveFn: () => { saved++; } });

// 마이그레이션: 기존 필드 이관 + 스티커 이관
const pet = { id: 101, type: 'dog', hunger: 55, happy: 66, pawCoins: 30,
              _legacyStickers: [{ emoji: '🛋️', x: 10, y: 20, size: 48 }] };
const g = C.ensureGame(pet);
assert.strictEqual(g.hunger, 55);
assert.strictEqual(g.happy, 66);
assert.strictEqual(g.level, 1);
assert.strictEqual(g.roomItems.length, 1);
assert.strictEqual(g.roomItems[0].emoji, '🛋️');
// 재호출 시 재마이그레이션 없음
assert.strictEqual(C.ensureGame(pet), g);

// XP 곡선
assert.strictEqual(C.xpForLevel(1), 100);
assert.strictEqual(C.xpForLevel(4), 800);

// 레벨업 + 코인 보너스(레벨×20) + 진화(4레벨 도달)
pet.pawCoins = 0;
g.level = 3; g.xp = C.xpForLevel(3) - 1;
const r = C.addXp(pet, 2);
assert.strictEqual(r.leveled, true);
assert.strictEqual(r.level, 4);
assert.strictEqual(r.evolved, true);
assert.strictEqual(r.stage.stage, 2);
assert.strictEqual(pet.pawCoins, 80); // 4×20

// Lv10 상한: 초과 XP는 버림, 더 못 올라감
g.level = 10; g.xp = 0;
const r2 = C.addXp(pet, 99999);
assert.strictEqual(r2.level, 10);
assert.strictEqual(r2.leveled, false);

assert.ok(saved > 0, 'addXp가 save를 호출해야 함');
console.log('test_core OK');
