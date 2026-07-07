const assert = require('assert');
global.PetGameItems = require('../../js/petgame/game-items.js');
const C = require('../../js/petgame/game-core.js');
C.init({ saveFn: () => {} });

// 가짜 PetHarmony 주입 — todayIndex 임계값(70)으로 버프 on/off 제어
global.PetHarmony = { todayIndex: (score, dominant) => (global.__idx !== undefined ? global.__idx : 60) };

// 미측정 펫(harmonyData 없음): 버프 무영향 — 1배
const petUnmeasured = { id: 1, type: 'dog', pawCoins: 0 };
const gU = C.ensureGame(petUnmeasured);
gU.foods.kibble = 1; gU.hunger = 50; gU.happy = 50;
global.__idx = 90; // PetHarmony가 있어도 harmonyData 없으면 무영향
const rU = C.feed(petUnmeasured, 'kibble');
assert.strictEqual(rU.ok, true);
assert.strictEqual(rU.msg.includes('+' + require('../../js/petgame/game-items.js').getFood('kibble').xp + 'XP'), true);

// 측정 펫, 오늘 궁합 <70: 버프 없음 — 1배
const petLow = { id: 2, type: 'dog', pawCoins: 0, harmonyData: { score: 60, elements: { pet: { dominant: '목' }, owner: { dominant: '화' } } } };
const gL = C.ensureGame(petLow);
gL.foods.kibble = 1; gL.hunger = 50; gL.happy = 50;
global.__idx = 65;
const rL = C.feed(petLow, 'kibble');
const kibbleXp = require('../../js/petgame/game-items.js').getFood('kibble').xp;
assert.strictEqual(rL.msg.includes(`+${kibbleXp}XP`), true);
assert.strictEqual(rL.msg.includes(`+${Math.round(kibbleXp * 1.2)}XP`), false);

// 측정 펫, 오늘 궁합 >=70: XP 1.2배(반올림)
const petHigh = { id: 3, type: 'dog', pawCoins: 0, harmonyData: { score: 75, elements: { pet: { dominant: '목' }, owner: { dominant: '화' } } } };
const gH = C.ensureGame(petHigh);
gH.foods.kibble = 1; gH.hunger = 50; gH.happy = 50;
global.__idx = 70;
const xpBefore = gH.xp;
const rH = C.feed(petHigh, 'kibble');
assert.strictEqual(rH.ok, true);
const expectedGain = Math.round(kibbleXp * 1.2);
assert.strictEqual(rH.msg.includes(`+${expectedGain}XP`), true);
assert.strictEqual(gH.xp - xpBefore, expectedGain);

// avgScore(legacy) 필드로도 동작
const petLegacy = { id: 4, type: 'dog', pawCoins: 0, harmonyData: { avgScore: 80, elements: { pet: { dominant: '화' }, owner: { dominant: '목' } } } };
const gLe = C.ensureGame(petLegacy);
gLe.foods.kibble = 1; gLe.hunger = 50; gLe.happy = 50;
global.__idx = 70;
const rLe = C.feed(petLegacy, 'kibble');
assert.strictEqual(rLe.msg.includes(`+${expectedGain}XP`), true);

// PetHarmony 미정의(모듈 부재): 예외 없이 1배로 폴백
delete global.PetHarmony;
const petNoMod = { id: 5, type: 'dog', pawCoins: 0, harmonyData: { score: 90, elements: { pet: { dominant: '목' }, owner: { dominant: '화' } } } };
const gN = C.ensureGame(petNoMod);
gN.foods.kibble = 1; gN.hunger = 50; gN.happy = 50;
const rN = C.feed(petNoMod, 'kibble');
assert.strictEqual(rN.msg.includes(`+${kibbleXp}XP`), true);

// decay: harmonyData 있으면 0.8배 완화, 없으면 1배
global.PetHarmony = { todayIndex: () => 60 };
const petDecayMeasured = { id: 6, type: 'dog', harmonyData: { score: 60, elements: { pet: { dominant: '목' }, owner: { dominant: '화' } } } };
const gDM = C.ensureGame(petDecayMeasured);
gDM.hunger = 50; gDM.happy = 50;
C.decay(petDecayMeasured, 10);
assert.strictEqual(gDM.hunger, 50 - 3 * 10 * 0.8);
assert.strictEqual(gDM.happy, 50 - 1 * 10 * 0.8);

const petDecayUnmeasured = { id: 7, type: 'dog' };
const gDU = C.ensureGame(petDecayUnmeasured);
gDU.hunger = 50; gDU.happy = 50;
C.decay(petDecayUnmeasured, 10);
assert.strictEqual(gDU.hunger, 50 - 3 * 10);
assert.strictEqual(gDU.happy, 50 - 1 * 10);

console.log('test_harmony_buff OK');
