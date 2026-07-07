const assert = require('assert');
global.PetGameItems = require('../../js/petgame/game-items.js');
const C = require('../../js/petgame/game-core.js');
C.init({ saveFn: () => {} });

const pet = { id: 1, type: 'dog', pawCoins: 100 };
const g = C.ensureGame(pet);

// 구매: 코인 차감·인벤 증가, 잔액 부족 거부
assert.strictEqual(C.buyFood(pet, 'kibble').ok, true);
assert.strictEqual(pet.pawCoins, 90);
assert.strictEqual(g.foods.kibble, 3);        // 시작 2 + 구매 1
pet.pawCoins = 5;
assert.strictEqual(C.buyFood(pet, 'meat').ok, false);
assert.strictEqual(pet.pawCoins, 5);

// 아이템: 레벨 미달 거부 / 성공 시 ownedItems / 중복 거부
pet.pawCoins = 1000;
assert.strictEqual(C.buyItem(pet, 'doghouse').ok, false);   // unlockLv 4 > 현재 1
assert.strictEqual(C.buyItem(pet, 'bowl').ok, true);
assert.ok(g.ownedItems.includes('bowl'));
assert.strictEqual(C.buyItem(pet, 'bowl').ok, false);

// 먹이: 인벤 차감 + 회복 + XP
g.hunger = 50; g.happy = 50; g.foods.kibble = 1;
const r = C.feed(pet, 'kibble');
assert.strictEqual(r.ok, true);
assert.strictEqual(g.foods.kibble, 0);
assert.strictEqual(g.hunger, 80);
assert.strictEqual(g.happy, 55);
assert.strictEqual(C.feed(pet, 'kibble').ok, false);        // 인벤 없음

// 케어 보상: 일일 1회 + walk km 상한
pet.pawCoins = 0;
assert.strictEqual(C.earnCare(pet, 'diary').coins, 10);
assert.strictEqual(C.earnCare(pet, 'diary'), null);          // 같은 날 중복 거부
assert.strictEqual(C.earnCare(pet, 'walk', 2).coins, 40);    // 2km × 20
assert.strictEqual(C.earnCare(pet, 'walk', 4).coins, 60);    // 일 100 상한: 40+60까지만
assert.strictEqual(C.earnCare(pet, 'walk', 1), null);        // 상한 도달
assert.strictEqual(pet.pawCoins, 110);

// 감쇠: 하한 0
g.hunger = 5; g.happy = 2;
C.decay(pet, 3);
assert.strictEqual(g.hunger, 0);
assert.strictEqual(g.happy, 0);
console.log('test_economy OK');
