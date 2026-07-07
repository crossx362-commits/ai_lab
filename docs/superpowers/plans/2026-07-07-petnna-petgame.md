# 펫과나 마이펫 게임 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 마이펫 탭의 방/마당을 케어연동 경제·먹이·성장(4단계 진화)·자유배치 꾸미기를 갖춘 육성 게임으로 리뉴얼.

**Architecture:** 신규 3모듈(`js/petgame/`) — `game-items.js`(카탈로그 데이터), `game-core.js`(순수 로직, DOM 없음, node 테스트 가능), `game-stage.js`(DOM 렌더·연출). 기존 `mypet.js`의 방 코드는 `PetGame.mount()` 호출로 대체. 상태는 `pet.game` 객체에 저장, 기존 localStorage(`petna_pets`) 경로 재사용.

**Tech Stack:** 바닐라 JS(전역 스크립트, ES 모듈 아님), Tailwind 유틸 클래스, PNG 에셋(`images/petgame/`), node 내장 assert로 로직 테스트.

**Spec:** `docs/superpowers/specs/2026-07-07-petnna-petgame-design.md`

## Global Constraints

- **ES 모듈 금지** — petnna는 전역 스크립트 로딩. 새 파일은 `window.*`에 API를 붙이되, node 테스트를 위해 말미에 `if (typeof module !== 'undefined') module.exports = ...` 추가 (UMD-lite).
- **펫 종**: `pet.type`은 `"dog"|"cat"|"rabbit"|"hamster"`. 스프라이트 경로 `images/petgame/pet/{type}_{1..4}.png`, 파일 없으면 이모지 폴백(🐶🐱🐰🐹).
- **이미지 플레이스홀더 필수**: 모든 아이템/펫 이미지는 `onerror` 시 이모지 카드로 대체. 게임은 에셋 없이도 완전 동작해야 함.
- **실서비스 데이터 보존**: `pet.hunger/happy/pawCoins`·기존 스티커는 1회 마이그레이션으로 이관, 파괴 금지.
- **부정 경험 금지**: 배고픔 페널티는 표정/말풍선까지만.
- **경제 수치(스펙 고정)**: 산책 1km=20(일 100 상한)·건강(몸무게기록) 15/일·일기 10/일·출석 10/일. 먹이 6종 `사료10·뼈간식20·우유15·고기40·케이크60·도시락100`코인. XP곡선 `Math.round(100*Math.pow(lv,1.5))`. 레벨업 보너스 `레벨×20`코인. 최대 Lv10.
- **성장 단계**: 1–3 아기(scale 0.62) / 4–6 주니어(0.78) / 7–9 어른(0.9) / 10 킹(1.0).
- **캐시 버스팅**: index.html에서 새 스크립트는 `?v=1`, 수정한 기존 파일은 `?v=161`로 상향.
- 커밋 메시지 말미: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

---

### Task 1: 카탈로그 데이터 모듈 (game-items.js)

**Files:**
- Create: `projects/petnna/js/petgame/game-items.js`
- Test: `projects/petnna/tests/petgame/test_items.js`

**Interfaces:**
- Produces (전역 `PetGameItems` / node `module.exports`):
  - `FOODS: Array<{id,name,emoji,img,price,hunger,happy,xp}>` (6종)
  - `ITEMS: Array<{id,name,emoji,img,space:'room'|'yard',sizeClass:'L'|'M'|'S',basePx:number,price,unlockLv}>` (26종)
  - `THEMES: Array<{id,name,img,price,space:'room'|'yard'}>` (bg 테마 7종, 기본 room/yard는 price 0)
  - `STAGES: Array<{stage:1|2|3|4,name,minLv,scale}>`
  - `getFood(id)`, `getItem(id)`, `itemsForSpace(space, level)` (해금분만), `stageForLevel(lv)`

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/petnna/tests/petgame/test_items.js`:
```js
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
```

- [ ] **Step 2: 실패 확인**

Run: `cd projects/petnna && node tests/petgame/test_items.js`
Expected: FAIL — `Cannot find module '../../js/petgame/game-items.js'`

- [ ] **Step 3: 구현**

`projects/petnna/js/petgame/game-items.js`:
```js
// 펫게임 카탈로그 — 데이터만(로직·DOM 없음). 이미지 없으면 emoji 폴백은 stage가 처리.
(function (root) {
    const P = 'images/petgame/';

    const FOODS = [
        { id: 'kibble', name: '사료',    emoji: '🥣', img: P + 'food/kibble.png', price: 10,  hunger: 30,  happy: 5,  xp: 5 },
        { id: 'bone',   name: '뼈간식',  emoji: '🦴', img: P + 'food/bone.png',   price: 20,  hunger: 15,  happy: 12, xp: 8 },
        { id: 'milk',   name: '우유',    emoji: '🥛', img: P + 'food/milk.png',   price: 15,  hunger: 20,  happy: 8,  xp: 6 },
        { id: 'meat',   name: '고기',    emoji: '🍖', img: P + 'food/meat.png',   price: 40,  hunger: 50,  happy: 15, xp: 15 },
        { id: 'cake',   name: '케이크',  emoji: '🍰', img: P + 'food/cake.png',   price: 60,  hunger: 40,  happy: 40, xp: 20 },
        { id: 'bento',  name: '특식 도시락', emoji: '🍱', img: P + 'food/bento.png', price: 100, hunger: 100, happy: 100, xp: 30 },
    ];

    // sizeClass → 스테이지 기본 px (스펙: L 140–180, M 90–130, S 50–80 → 중간값)
    const BASE_PX = { L: 160, M: 110, S: 64 };
    function item(id, name, emoji, space, sizeClass, price, unlockLv, imgDir) {
        return { id, name, emoji, space, sizeClass, basePx: BASE_PX[sizeClass], price, unlockLv,
                 img: P + (imgDir || space) + '/' + id + '.png' };
    }

    const ITEMS = [
        // Lv1 기본 8종
        item('bowl',        '밥그릇',   '🍽️', 'yard', 'S', 20,  1),
        item('flowerpot',   '꽃화분',   '🌼', 'room', 'S', 25,  1),
        item('cushion',     '쿠션',     '🛋️', 'room', 'S', 30,  1),
        item('rug',         '러그',     '🧶', 'room', 'M', 40,  1),
        item('ball',        '공',       '⚽', 'yard', 'S', 25,  1),
        item('plant',       '화분',     '🪴', 'room', 'S', 30,  1),
        item('stones',      '디딤돌',   '🪨', 'yard', 'S', 35,  1),
        item('flowerbed',   '꽃화단',   '🌷', 'yard', 'M', 50,  1),
        // Lv2–3 6종
        item('tree',        '나무',     '🌳', 'yard', 'L', 120, 2),
        item('fence',       '울타리',   '🚧', 'yard', 'M', 60,  2),
        item('sofa',        '소파',     '🛋️', 'room', 'L', 150, 2),
        item('window',      '창문',     '🪟', 'room', 'M', 80,  3),
        item('bench',       '벤치',     '🪑', 'yard', 'M', 90,  3),
        item('bookshelf',   '책장',     '📚', 'room', 'M', 100, 3),
        // Lv4 진화 보상 5종
        item('doghouse',    '개집',     '🏠', 'yard', 'L', 200, 4),
        item('petbed',      '침대',     '🛏️', 'room', 'M', 150, 4),
        item('tv',          'TV',       '📺', 'room', 'M', 180, 4),
        item('lamp',        '램프',     '💡', 'room', 'S', 90,  4),
        item('toybox',      '장난감상자', '🧸', 'room', 'M', 120, 4),
        // Lv5–6 4종
        item('pool',        '수영장',   '🏊', 'yard', 'L', 300, 5),
        item('swing',       '그네',     '🎠', 'yard', 'L', 250, 5),
        item('garden',      '텃밭',     '🥕', 'yard', 'M', 150, 6),
        item('cattower',    '캣타워',   '🐈', 'room', 'M', 200, 6),
        // Lv7 진화 보상 2종
        item('fountain',    '분수대',   '⛲', 'yard', 'L', 500, 7),
        item('fireplace',   '벽난로',   '🔥', 'room', 'L', 450, 7),
        // Lv10 킹
        item('golden_doghouse', '황금 개집', '👑', 'yard', 'L', 999, 10),
    ];

    const THEMES = [
        { id: 'room',       name: '기본 방',   img: P + 'bg/room.webp',       price: 0,   space: 'room' },
        { id: 'yard',       name: '기본 마당', img: P + 'bg/yard.webp',       price: 0,   space: 'yard' },
        { id: 'room_cozy',  name: '코지룸',    img: P + 'bg/room_cozy.webp',  price: 200, space: 'room' },
        { id: 'room_play',  name: '플레이룸',  img: P + 'bg/room_play.webp',  price: 300, space: 'room' },
        { id: 'room_spa',   name: '스파룸',    img: P + 'bg/room_spa.webp',   price: 400, space: 'room' },
        { id: 'room_luxe',  name: '럭셔리룸',  img: P + 'bg/room_luxe.webp',  price: 600, space: 'room' },
        { id: 'room_royal', name: '로열룸',    img: P + 'bg/room_royal.webp', price: 999, space: 'room' },
    ];

    const STAGES = [
        { stage: 1, name: '아기',   minLv: 1,  scale: 0.62 },
        { stage: 2, name: '주니어', minLv: 4,  scale: 0.78 },
        { stage: 3, name: '어른',   minLv: 7,  scale: 0.9 },
        { stage: 4, name: '킹',     minLv: 10, scale: 1.0 },
    ];

    const api = {
        FOODS, ITEMS, THEMES, STAGES,
        getFood: (id) => FOODS.find(f => f.id === id) || null,
        getItem: (id) => ITEMS.find(i => i.id === id) || null,
        itemsForSpace: (space, level) => ITEMS.filter(i => i.space === space && i.unlockLv <= level),
        stageForLevel: (lv) => [...STAGES].reverse().find(s => lv >= s.minLv) || STAGES[0],
    };
    root.PetGameItems = api;
    if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/petnna && node tests/petgame/test_items.js`
Expected: `test_items OK`

- [ ] **Step 5: 커밋**

```bash
git add projects/petnna/js/petgame/game-items.js projects/petnna/tests/petgame/test_items.js
git commit -m "feat(petgame): 아이템·먹이·테마 카탈로그 모듈 (Task 1)"
```

---

### Task 2: 코어 로직 — 상태·레벨·진화·마이그레이션 (game-core.js 1/2)

**Files:**
- Create: `projects/petnna/js/petgame/game-core.js`
- Test: `projects/petnna/tests/petgame/test_core.js`

**Interfaces:**
- Consumes: `PetGameItems.stageForLevel` (node에선 require)
- Produces (전역 `PetGameCore`):
  - `init({saveFn})` — 저장 콜백 주입(브라우저: pets를 localStorage에 쓰는 함수, 테스트: 스파이)
  - `ensureGame(pet)` — `pet.game` 없으면 생성+마이그레이션(hunger/happy 이관, 스티커→roomItems, 반환: pet.game)
  - `xpForLevel(lv)` → number (`Math.round(100*Math.pow(lv,1.5))`)
  - `addXp(pet, n)` → `{level, leveled:boolean, evolved:boolean, stage}` (레벨업 시 코인보너스 지급 포함, Lv10 상한)
  - `save()` — 주입된 saveFn 호출

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/petnna/tests/petgame/test_core.js`:
```js
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
```

- [ ] **Step 2: 실패 확인**

Run: `cd projects/petnna && node tests/petgame/test_core.js`
Expected: FAIL — `Cannot find module '../../js/petgame/game-core.js'`

- [ ] **Step 3: 구현**

`projects/petnna/js/petgame/game-core.js`:
```js
// 펫게임 코어 — 순수 로직(DOM 금지). 브라우저: window.PetGameCore / node: module.exports.
(function (root) {
    const Items = root.PetGameItems;
    let _saveFn = null;

    function init(deps) { _saveFn = (deps && deps.saveFn) || null; }
    function save() { if (_saveFn) try { _saveFn(); } catch (e) { console.error('[petgame] save 실패', e); } }

    function xpForLevel(lv) { return Math.round(100 * Math.pow(lv, 1.5)); }

    function ensureGame(pet) {
        if (!pet) return null;
        if (pet.game) return pet.game;
        // 1회 마이그레이션 — 기존 배고픔/행복 이관, 레거시 스티커 → roomItems
        const legacy = pet._legacyStickers || [];
        pet.game = {
            level: 1, xp: 0,
            hunger: typeof pet.hunger === 'number' ? pet.hunger : 80,
            happy: typeof pet.happy === 'number' ? pet.happy : 80,
            foods: { kibble: 2 },                       // 신규 시작 먹이
            roomItems: legacy.map((s, i) => ({ uid: 'lg' + i, emoji: s.emoji, x: s.x, y: s.y, size: s.size || 48 })),
            yardItems: [],
            ownedItems: [],                              // 구매한 아이템 id 목록
            ownedThemes: ['room', 'yard'],
            roomTheme: 'room', yardTheme: 'yard',
            lastCareRewards: {},                         // {walk:'YYYY-MM-DD', ...}
            walkKmToday: { date: '', km: 0 },
        };
        save();
        return pet.game;
    }

    function addXp(pet, n) {
        const g = ensureGame(pet);
        const before = Items.stageForLevel(g.level).stage;
        let leveled = false;
        if (g.level < 10) {
            g.xp += n;
            while (g.level < 10 && g.xp >= xpForLevel(g.level)) {
                g.xp -= xpForLevel(g.level);
                g.level += 1;
                leveled = true;
                pet.pawCoins = (pet.pawCoins || 0) + g.level * 20;  // 레벨업 보너스
            }
            if (g.level >= 10) { g.level = 10; g.xp = 0; }
        }
        const stage = Items.stageForLevel(g.level);
        save();
        return { level: g.level, leveled, evolved: leveled && stage.stage !== before, stage };
    }

    const api = { init, save, ensureGame, xpForLevel, addXp };
    root.PetGameCore = api;
    if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/petnna && node tests/petgame/test_core.js`
Expected: `test_core OK` (test_items도 재실행해 회귀 없음 확인)

- [ ] **Step 5: 커밋**

```bash
git add projects/petnna/js/petgame/game-core.js projects/petnna/tests/petgame/test_core.js
git commit -m "feat(petgame): 코어 상태·레벨·진화·마이그레이션 (Task 2)"
```

---

### Task 3: 코어 로직 — 경제(구매·먹이·케어보상) (game-core.js 2/2)

**Files:**
- Modify: `projects/petnna/js/petgame/game-core.js` (api에 함수 추가)
- Test: `projects/petnna/tests/petgame/test_economy.js`

**Interfaces:**
- Consumes: Task 2의 `ensureGame/addXp/save`, `PetGameItems.getFood/getItem`
- Produces (PetGameCore에 추가):
  - `spend(pet, n)` → boolean (부족 시 false, 차감 없음)
  - `buyFood(pet, foodId)` → `{ok, msg}` (코인 차감→인벤 +1)
  - `buyItem(pet, itemId)` → `{ok, msg}` (레벨 미달/중복 구매 거부, ownedItems에 추가)
  - `buyTheme(pet, themeId)` → `{ok, msg}`
  - `feed(pet, foodId)` → `{ok, msg, food, levelUp}` (인벤 차감, hunger/happy 회복, addXp 결과 포함)
  - `earnCare(pet, type, amount)` → `{coins, msg}|null` — type: `'walk'|'health'|'diary'|'attend'`. 일일 중복 방지(`lastCareRewards`), walk는 km당 20·일 100 상한(`walkKmToday` 누적)
  - `decay(pet, hours)` — 시간당 hunger -3, happy -1 (하한 0)
  - `todayStr()` → 'YYYY-MM-DD'

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/petnna/tests/petgame/test_economy.js`:
```js
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
```

- [ ] **Step 2: 실패 확인**

Run: `cd projects/petnna && node tests/petgame/test_economy.js`
Expected: FAIL — `C.buyFood is not a function`

- [ ] **Step 3: 구현 — game-core.js의 `const api = {...}` 직전에 추가, api 객체에 신규 함수 등록**

```js
    function todayStr() {
        const d = new Date();
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    function spend(pet, n) {
        if ((pet.pawCoins || 0) < n) return false;
        pet.pawCoins -= n; save(); return true;
    }

    function buyFood(pet, foodId) {
        const g = ensureGame(pet);
        const f = Items.getFood(foodId);
        if (!f) return { ok: false, msg: '알 수 없는 먹이' };
        if (!spend(pet, f.price)) return { ok: false, msg: `코인이 부족해요 (필요 🐾${f.price})` };
        g.foods[foodId] = (g.foods[foodId] || 0) + 1; save();
        return { ok: true, msg: `${f.name} 구매!` };
    }

    function buyItem(pet, itemId) {
        const g = ensureGame(pet);
        const it = Items.getItem(itemId);
        if (!it) return { ok: false, msg: '알 수 없는 아이템' };
        if (g.level < it.unlockLv) return { ok: false, msg: `Lv.${it.unlockLv}에 해금돼요` };
        if (g.ownedItems.includes(itemId)) return { ok: false, msg: '이미 보유 중이에요' };
        if (!spend(pet, it.price)) return { ok: false, msg: `코인이 부족해요 (필요 🐾${it.price})` };
        g.ownedItems.push(itemId); save();
        return { ok: true, msg: `${it.name} 구매! 꾸미기에서 배치하세요` };
    }

    function buyTheme(pet, themeId) {
        const g = ensureGame(pet);
        const t = Items.THEMES.find(x => x.id === themeId);
        if (!t) return { ok: false, msg: '알 수 없는 테마' };
        if (g.ownedThemes.includes(themeId)) return { ok: false, msg: '이미 보유 중이에요' };
        if (!spend(pet, t.price)) return { ok: false, msg: `코인이 부족해요 (필요 🐾${t.price})` };
        g.ownedThemes.push(themeId); save();
        return { ok: true, msg: `${t.name} 테마 구매!` };
    }

    function feed(pet, foodId) {
        const g = ensureGame(pet);
        const f = Items.getFood(foodId);
        if (!f) return { ok: false, msg: '알 수 없는 먹이' };
        if (!(g.foods[foodId] > 0)) return { ok: false, msg: '보유한 먹이가 없어요 — 상점에서 구매하세요' };
        g.foods[foodId] -= 1;
        g.hunger = Math.min(100, g.hunger + f.hunger);
        g.happy = Math.min(100, g.happy + f.happy);
        const levelUp = addXp(pet, f.xp);   // 내부에서 save
        return { ok: true, msg: `냠냠! ${f.name} 맛있다! (+${f.xp}XP)`, food: f, levelUp };
    }

    const CARE = { walk: { per: 20, cap: 100 }, health: { flat: 15 }, diary: { flat: 10 }, attend: { flat: 10 } };
    function earnCare(pet, type, amount) {
        const g = ensureGame(pet);
        const conf = CARE[type];
        if (!conf) return null;
        const today = todayStr();
        if (type === 'walk') {
            if (g.walkKmToday.date !== today) g.walkKmToday = { date: today, km: 0 };
            const already = Math.min(conf.cap, Math.round(g.walkKmToday.km * conf.per));
            const room = conf.cap - already;
            if (room <= 0) return null;
            const coins = Math.min(room, Math.round((amount || 0) * conf.per));
            if (coins <= 0) return null;
            g.walkKmToday.km += (amount || 0);
            pet.pawCoins = (pet.pawCoins || 0) + coins; save();
            return { coins, msg: `산책 보상 🐾+${coins}` };
        }
        if (g.lastCareRewards[type] === today) return null;
        g.lastCareRewards[type] = today;
        pet.pawCoins = (pet.pawCoins || 0) + conf.flat; save();
        const label = { health: '건강기록', diary: '일기', attend: '출석' }[type];
        return { coins: conf.flat, msg: `${label} 보상 🐾+${conf.flat}` };
    }

    function decay(pet, hours) {
        const g = ensureGame(pet);
        g.hunger = Math.max(0, g.hunger - 3 * hours);
        g.happy = Math.max(0, g.happy - 1 * hours);
        save();
    }
```
api 등록: `const api = { init, save, ensureGame, xpForLevel, addXp, todayStr, spend, buyFood, buyItem, buyTheme, feed, earnCare, decay };`

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/petnna && node tests/petgame/test_economy.js && node tests/petgame/test_core.js && node tests/petgame/test_items.js`
Expected: 3개 모두 OK

- [ ] **Step 5: 커밋**

```bash
git add projects/petnna/js/petgame/game-core.js projects/petnna/tests/petgame/test_economy.js
git commit -m "feat(petgame): 경제 로직 — 구매·먹이·케어보상·감쇠 (Task 3)"
```

---

### Task 4: 스테이지 렌더 — HUD·탭·배경·아이템·펫 (game-stage.js 1/2)

**Files:**
- Create: `projects/petnna/js/petgame/game-stage.js`
- Test: 수동(preview) — Task 7에서 통합 후 검증. 이 태스크에선 문법·독립 로드만 확인.

**Interfaces:**
- Consumes: `PetGameCore.*`, `PetGameItems.*`, 전역 `getActivePet()`(mypet.js), `showToast(msg)`(기존 전역)
- Produces (전역 `PetGame`):
  - `PetGame.mount(rootId)` — 루트에 HUD+탭+스테이지+액션바 렌더, 60초 decay 타이머·펫 산책 루프 시작
  - `PetGame.refresh()` — 전체 리렌더 (탭 전환·구매 후 호출)
  - `PetGame.state` — `{space:'room'|'yard', mode:'play'|'decorate'}`
  - `PetGame.earnCare(type, amount)` — 활성 펫에 코어 earnCare + 토스트 (케어 훅용, 미마운트 상태도 동작)

- [ ] **Step 1: 구현**

`projects/petnna/js/petgame/game-stage.js` (전체 신규 — 핵심 함수 전부 포함):
```js
// 펫게임 스테이지 — DOM 렌더/연출. 로직은 PetGameCore에 위임.
(function (root) {
    const Core = root.PetGameCore, Items = root.PetGameItems;
    const S = { space: 'yard', mode: 'play', rootId: null, timers: [] };

    function pet() { return (typeof getActivePet === 'function') ? getActivePet() : null; }
    function toast(m) { if (typeof showToast === 'function') showToast(m); else console.log('[petgame]', m); }
    function esc(x) { return String(x ?? '').replace(/</g, '&lt;'); }

    // 이미지 로드 실패 → 이모지 폴백 (전역 constraint)
    function imgOrEmoji(src, emoji, cls, px) {
        return `<img src="${src}" class="${cls}" style="width:${px}px" draggable="false"
                 onerror="this.outerHTML='<span class=&quot;${cls}&quot; style=&quot;font-size:${Math.round(px * 0.8)}px;line-height:1&quot;>${emoji}</span>'">`;
    }

    function petSprite(p, px) {
        const g = Core.ensureGame(p);
        const st = Items.stageForLevel(g.level);
        const type = ['dog', 'cat', 'rabbit', 'hamster'].includes(p.type) ? p.type : 'dog';
        const fallback = { dog: '🐶', cat: '🐱', rabbit: '🐰', hamster: '🐹' }[type];
        const size = Math.round(px * st.scale);
        return imgOrEmoji(`images/petgame/pet/${type}_${st.stage}.png`, fallback, 'pg-pet-img', size);
    }

    function hudHTML(p, g) {
        const st = Items.stageForLevel(g.level);
        const need = Core.xpForLevel(g.level);
        const xpPct = g.level >= 10 ? 100 : Math.min(100, Math.round(g.xp / need * 100));
        return `
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="font-black text-[15px]">${esc(p.name)}</span>
            <span class="text-[10px] font-bold text-white bg-brand-500 px-2 py-0.5 rounded-full">Lv.${g.level} ${st.name}</span>
          </div>
          <span class="text-[12px] font-black text-amber-700 bg-amber-50 px-2.5 py-1 rounded-full">🐾 ${p.pawCoins || 0}</span>
        </div>
        <div class="mt-1.5 h-[6px] bg-brand-100 rounded-full overflow-hidden"><div class="h-full bg-brand-500 rounded-full" style="width:${xpPct}%"></div></div>
        <div class="flex gap-3 mt-1.5 text-[10px] font-bold text-gray-500">
          <span class="flex-1 flex items-center gap-1">🍖<span class="flex-1 h-[4px] bg-gray-100 rounded-full overflow-hidden"><span class="block h-full bg-amber-400 rounded-full" style="width:${g.hunger}%"></span></span></span>
          <span class="flex-1 flex items-center gap-1">💗<span class="flex-1 h-[4px] bg-gray-100 rounded-full overflow-hidden"><span class="block h-full bg-pink-400 rounded-full" style="width:${g.happy}%"></span></span></span>
        </div>`;
    }

    function itemsOf(g) { return S.space === 'room' ? g.roomItems : g.yardItems; }

    function stageHTML(p, g) {
        const themeId = S.space === 'room' ? g.roomTheme : g.yardTheme;
        const theme = Items.THEMES.find(t => t.id === themeId) || Items.THEMES[0];
        const placed = itemsOf(g).map((e, idx) => {
            const it = e.emoji ? null : Items.getItem(e.id);
            const inner = e.emoji
                ? `<span style="font-size:${e.size}px;line-height:1">${e.emoji}</span>`
                : imgOrEmoji(it.img, it.emoji, '', e.size);
            return `<div class="pg-item absolute select-none ${S.mode === 'decorate' ? 'pg-editable' : ''}"
                      data-idx="${idx}" style="left:${e.x}%;top:${e.y}%;transform:translate(-50%,-100%)">${inner}</div>`;
        }).join('');
        const mood = g.hunger < 30 ? `<div class="pg-bubble">배고파요…🥺</div>` : '';
        return `
        <div id="pg-stage" class="relative w-full rounded-2xl overflow-hidden" style="aspect-ratio:4/3;background:#cde9f2 url('${theme.img}') center/cover">
          ${placed}
          <div id="pg-pet" class="absolute" style="left:50%;top:78%;transform:translate(-50%,-100%);transition:left 2.5s ease-in-out">
            ${mood}${petSprite(p, 150)}
          </div>
          <div id="pg-fx" class="absolute inset-0 pointer-events-none"></div>
        </div>`;
    }

    function actionsHTML() {
        const on = (m) => S.mode === m ? 'bg-brand-500 text-white' : 'bg-white border border-brand-100 text-gray-700';
        return `
        <div class="flex gap-2 mt-2">
          <button onclick="PetGame.openFeed()" class="flex-[1.3] py-2.5 rounded-xl font-black text-[13px] bg-brand-500 text-white shadow-sm">🦴 먹이주기</button>
          <button onclick="PetGame.openShop()" class="flex-1 py-2.5 rounded-xl font-black text-[13px] bg-white border border-brand-100 text-gray-700">🛒 상점</button>
          <button onclick="PetGame.toggleDecorate()" class="flex-1 py-2.5 rounded-xl font-black text-[13px] ${on('decorate')}">🎨 꾸미기</button>
        </div>
        <div id="pg-sheet"></div>`;
    }

    function refresh() {
        const p = pet(); if (!p || !S.rootId) return;
        const rootEl = document.getElementById(S.rootId); if (!rootEl) return;
        const g = Core.ensureGame(p);
        const tab = (sp, label) => `<button onclick="PetGame.setSpace('${sp}')"
            class="flex-1 py-1.5 rounded-xl text-[12px] font-bold ${S.space === sp ? 'bg-brand-500 text-white' : 'bg-brand-50 text-gray-500'}">${label}</button>`;
        rootEl.innerHTML = `
          <div class="bg-white rounded-2xl border border-brand-100 p-3.5 shadow-soft">
            <div id="pg-hud">${hudHTML(p, g)}</div>
            <div class="flex gap-1.5 mt-2.5">${tab('room', '🏠 방')}${tab('yard', '🌳 마당')}</div>
            <div class="mt-2">${stageHTML(p, g)}</div>
            ${actionsHTML()}
          </div>`;
        if (S.mode === 'decorate') bindDecorate();
    }

    function setSpace(sp) { S.space = sp; S.mode = 'play'; refresh(); }

    // 펫 산책 루프 — 스테이지 안에서 좌우로 이동
    function wanderTick() {
        const el = document.getElementById('pg-pet');
        if (el && S.mode === 'play') el.style.left = (25 + Math.random() * 50).toFixed(0) + '%';
    }

    function mount(rootId) {
        S.rootId = rootId;
        Core.init({ saveFn: root.PetGameSave || (() => {
            try { localStorage.setItem('petna_pets', JSON.stringify(root.pets || [])); } catch (e) {}
        }) });
        const p = pet(); if (p) Core.ensureGame(p);
        S.timers.forEach(clearInterval); S.timers = [];
        S.timers.push(setInterval(wanderTick, 4000));
        S.timers.push(setInterval(() => { const q = pet(); if (q) { Core.decay(q, 1 / 60); updateHud(); } }, 60000));
        refresh();
    }

    function updateHud() {
        const p = pet(); if (!p) return;
        const el = document.getElementById('pg-hud');
        if (el) el.innerHTML = hudHTML(p, Core.ensureGame(p));
    }

    function earnCare(type, amount) {
        const p = pet(); if (!p) return null;
        const r = Core.earnCare(p, type, amount);
        if (r) { toast(r.msg); updateHud(); }
        return r;
    }

    root.PetGame = Object.assign(root.PetGame || {}, {
        mount, refresh, setSpace, earnCare, updateHud, state: S,
        // Task 5·6에서 구현 — 미구현 호출 시 안내
        openFeed() { toast('준비 중'); }, openShop() { toast('준비 중'); }, toggleDecorate() { toast('준비 중'); },
    });
    function bindDecorate() {} // Task 6에서 교체
    root.PetGameStageInternals = { bindDecorate: (fn) => { bindDecorate = fn; } };
})(typeof window !== 'undefined' ? window : globalThis);
```

CSS(스테이지 전용 소량)는 Task 7에서 `css/style.css` 말미에 추가한다(`.pg-bubble`, `.pg-editable`).

- [ ] **Step 2: 문법 확인**

Run: `cd projects/petnna && node --check js/petgame/game-stage.js`
Expected: 에러 없음

- [ ] **Step 3: 커밋**

```bash
git add projects/petnna/js/petgame/game-stage.js
git commit -m "feat(petgame): 스테이지 렌더 — HUD·탭·배경·아이템·펫 (Task 4)"
```

---

### Task 5: 먹이주기 연출 + 상점 시트 (game-stage.js 2/2)

**Files:**
- Modify: `projects/petnna/js/petgame/game-stage.js` (openFeed/openShop 교체 + 연출 함수 추가)

**Interfaces:**
- Consumes: `Core.feed/buyFood/buyItem/buyTheme`, `Items.FOODS/itemsForSpace/THEMES`
- Produces: `PetGame.openFeed()`, `PetGame.feedNow(foodId)`, `PetGame.openShop()`, `PetGame.buy(kind,id)`, `PetGame.closeSheet()`

- [ ] **Step 1: 구현 — 파일 말미 `root.PetGame = ...` 직전에 추가하고, PetGame 등록부의 openFeed/openShop 자리에 실제 함수 연결**

```js
    function sheet(html) {
        const el = document.getElementById('pg-sheet');
        if (el) el.innerHTML = html ? `<div class="mt-2 bg-brand-50 border border-brand-100 rounded-2xl p-3">${html}</div>` : '';
    }
    function closeSheet() { sheet(''); }

    function openFeed() {
        const p = pet(); if (!p) return;
        const g = Core.ensureGame(p);
        const rows = Items.FOODS.map(f => {
            const n = g.foods[f.id] || 0;
            return `<button ${n ? `onclick="PetGame.feedNow('${f.id}')"` : 'disabled'}
                class="flex flex-col items-center gap-0.5 p-2 rounded-xl ${n ? 'bg-white' : 'bg-gray-100 opacity-50'} border border-brand-100">
                <span class="text-[22px]">${f.emoji}</span>
                <span class="text-[10px] font-bold">${f.name}</span>
                <span class="text-[9px] text-gray-400">보유 ${n}</span></button>`;
        }).join('');
        sheet(`<div class="flex items-center justify-between mb-2"><b class="text-[12px]">먹이 선택</b>
               <button onclick="PetGame.closeSheet()" class="text-[11px] text-gray-400">닫기 ✕</button></div>
               <div class="grid grid-cols-3 gap-1.5">${rows}</div>
               <p class="text-[10px] text-gray-400 mt-2">먹이가 없으면 🛒 상점에서 구매!</p>`);
    }

    function feedNow(foodId) {
        const p = pet(); if (!p) return;
        const r = Core.feed(p, foodId);
        if (!r.ok) { toast(r.msg); return; }
        closeSheet();
        throwFoodFx(r.food, () => {
            toast(r.msg);
            updateHud();
            if (r.levelUp && r.levelUp.leveled) celebrate(r.levelUp);
        });
    }

    // 먹이 투척 → 펫 달려옴 → 냠냠 + 하트/XP 플로팅 (약 2.5초)
    function throwFoodFx(food, done) {
        const stage = document.getElementById('pg-stage'), petEl = document.getElementById('pg-pet'),
              fx = document.getElementById('pg-fx');
        if (!stage || !petEl || !fx) { done(); return; }
        const foodEl = document.createElement('div');
        foodEl.style.cssText = 'position:absolute;left:12%;top:20%;font-size:30px;transition:all .8s cubic-bezier(.3,-.3,.6,1);z-index:5';
        foodEl.textContent = food.emoji;
        fx.appendChild(foodEl);
        requestAnimationFrame(() => { foodEl.style.left = '58%'; foodEl.style.top = '74%'; });
        setTimeout(() => { petEl.style.transition = 'left .9s ease-in-out'; petEl.style.left = '60%'; }, 700);
        setTimeout(() => {
            foodEl.remove();
            petEl.classList.add('pg-eat');
            const heart = document.createElement('div');
            heart.className = 'pg-float';
            heart.style.cssText = 'position:absolute;left:60%;top:48%';
            heart.innerHTML = `💖 <b style="font-size:11px;color:#a9583e">+${food.xp}XP</b>`;
            fx.appendChild(heart);
            setTimeout(() => { heart.remove(); petEl.classList.remove('pg-eat'); petEl.style.transition = 'left 2.5s ease-in-out'; done(); }, 1200);
        }, 1700);
    }

    function celebrate(levelUp) {
        const fx = document.getElementById('pg-fx'); if (!fx) { refresh(); return; }
        const el = document.createElement('div');
        el.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(255,253,248,.75);z-index:9';
        el.innerHTML = `<div class="text-center">
            <div style="font-size:44px">${levelUp.evolved ? '✨🐾✨' : '🎉'}</div>
            <div class="font-black text-[16px] text-brand-700">${levelUp.evolved ? `${levelUp.stage.name}(으)로 진화!` : `레벨 ${levelUp.level} 달성!`}</div>
            <div class="text-[11px] text-gray-500 mt-1">보너스 🐾+${levelUp.level * 20}</div></div>`;
        fx.appendChild(el);
        setTimeout(() => { el.remove(); refresh(); }, 2200);
    }

    function openShop() {
        const p = pet(); if (!p) return;
        const g = Core.ensureGame(p);
        const foodRows = Items.FOODS.map(f =>
            `<button onclick="PetGame.buy('food','${f.id}')" class="flex flex-col items-center p-2 rounded-xl bg-white border border-brand-100">
             <span class="text-[20px]">${f.emoji}</span><span class="text-[10px] font-bold">${f.name}</span>
             <span class="text-[9px] font-black text-amber-700">🐾${f.price}</span></button>`).join('');
        const itemRows = Items.ITEMS.map(it => {
            const owned = g.ownedItems.includes(it.id), locked = g.level < it.unlockLv;
            return `<button ${owned || locked ? 'disabled' : `onclick="PetGame.buy('item','${it.id}')"`}
                class="flex flex-col items-center p-2 rounded-xl border border-brand-100 ${owned ? 'bg-brand-100' : locked ? 'bg-gray-100 opacity-60' : 'bg-white'}">
                <span class="text-[20px]">${it.emoji}</span><span class="text-[10px] font-bold">${it.name}</span>
                <span class="text-[9px] ${locked ? 'text-gray-400' : 'font-black text-amber-700'}">${owned ? '보유중' : locked ? `Lv.${it.unlockLv} 해금` : `🐾${it.price}`}</span></button>`;
        }).join('');
        const themeRows = Items.THEMES.filter(t => t.price > 0).map(t => {
            const owned = g.ownedThemes.includes(t.id);
            return `<button ${owned ? `onclick="PetGame.applyTheme('${t.id}')"` : `onclick="PetGame.buy('theme','${t.id}')"`}
                class="flex flex-col items-center p-2 rounded-xl border border-brand-100 ${owned ? 'bg-brand-100' : 'bg-white'}">
                <span class="text-[20px]">🖼️</span><span class="text-[10px] font-bold">${t.name}</span>
                <span class="text-[9px] font-black text-amber-700">${owned ? '적용하기' : `🐾${t.price}`}</span></button>`;
        }).join('');
        sheet(`<div class="flex items-center justify-between mb-2"><b class="text-[12px]">🛒 상점</b>
            <button onclick="PetGame.closeSheet()" class="text-[11px] text-gray-400">닫기 ✕</button></div>
            <p class="text-[10px] font-black text-gray-500 mb-1">먹이</p><div class="grid grid-cols-3 gap-1.5">${foodRows}</div>
            <p class="text-[10px] font-black text-gray-500 mt-2 mb-1">아이템 (방·마당)</p><div class="grid grid-cols-3 gap-1.5">${itemRows}</div>
            <p class="text-[10px] font-black text-gray-500 mt-2 mb-1">방 테마</p><div class="grid grid-cols-3 gap-1.5">${themeRows}</div>`);
    }

    function buy(kind, id) {
        const p = pet(); if (!p) return;
        const r = kind === 'food' ? Core.buyFood(p, id) : kind === 'item' ? Core.buyItem(p, id) : Core.buyTheme(p, id);
        toast(r.msg); updateHud();
        if (r.ok) openShop();   // 시트 갱신(보유중 반영)
    }

    function applyTheme(themeId) {
        const p = pet(); if (!p) return;
        const g = Core.ensureGame(p);
        const t = Items.THEMES.find(x => x.id === themeId);
        if (!t || !g.ownedThemes.includes(themeId)) return;
        if (t.space === 'room') g.roomTheme = themeId; else g.yardTheme = themeId;
        Core.save(); S.space = t.space; refresh(); toast(`${t.name} 적용!`);
    }
```
PetGame 등록부를 다음으로 교체:
```js
    root.PetGame = Object.assign(root.PetGame || {}, {
        mount, refresh, setSpace, earnCare, updateHud, state: S,
        openFeed, feedNow, closeSheet, openShop, buy, applyTheme,
        toggleDecorate() { toast('준비 중'); },   // Task 6에서 교체
    });
```

- [ ] **Step 2: 문법 확인**

Run: `cd projects/petnna && node --check js/petgame/game-stage.js`
Expected: 에러 없음

- [ ] **Step 3: 커밋**

```bash
git add projects/petnna/js/petgame/game-stage.js
git commit -m "feat(petgame): 먹이 연출·상점 시트 (Task 5)"
```

---

### Task 6: 꾸미기 모드 — 드래그 배치·크기·보관 (game-stage.js 3/3)

**Files:**
- Modify: `projects/petnna/js/petgame/game-stage.js`

**Interfaces:**
- Consumes: `S.mode`, `itemsOf(g)`, `Core.save`
- Produces: `PetGame.toggleDecorate()`, `PetGame.placeOwned(itemId)`, `PetGame.resizePlaced(idx, dir)`, `PetGame.storePlaced(idx)` + 스테이지 아이템 포인터 드래그

- [ ] **Step 1: 구현 — toggleDecorate·배치 팔레트·드래그 바인딩 추가**

```js
    function toggleDecorate() {
        S.mode = S.mode === 'decorate' ? 'play' : 'decorate';
        refresh();
        if (S.mode === 'decorate') openPalette();
        else closeSheet();
    }

    // 보유했지만 현재 공간에 미배치인 아이템 팔레트
    function openPalette() {
        const p = pet(); if (!p) return;
        const g = Core.ensureGame(p);
        const placedIds = itemsOf(g).filter(e => !e.emoji).map(e => e.id);
        const avail = g.ownedItems
            .map(id => Items.getItem(id))
            .filter(it => it && it.space === S.space && !placedIds.includes(it.id));
        const rows = avail.length ? avail.map(it =>
            `<button onclick="PetGame.placeOwned('${it.id}')" class="flex flex-col items-center p-2 rounded-xl bg-white border border-brand-100">
             <span class="text-[20px]">${it.emoji}</span><span class="text-[10px] font-bold">${it.name}</span></button>`).join('')
            : `<p class="text-[10px] text-gray-400 col-span-3">배치할 아이템이 없어요 — 상점에서 구매하세요</p>`;
        sheet(`<div class="flex items-center justify-between mb-2"><b class="text-[12px]">🎨 꾸미기 — 아이템을 끌어서 이동</b>
            <button onclick="PetGame.toggleDecorate()" class="text-[11px] font-bold text-brand-600">완료 ✓</button></div>
            <div class="grid grid-cols-3 gap-1.5">${rows}</div>
            <p class="text-[10px] text-gray-400 mt-2">배치된 아이템 탭: 🔍＋/－ 크기 · 📦 보관</p>`);
    }

    function placeOwned(itemId) {
        const p = pet(); const g = Core.ensureGame(p);
        const it = Items.getItem(itemId); if (!it) return;
        itemsOf(g).push({ uid: 'i' + Date.now(), id: itemId, x: 50, y: 70, size: it.basePx });
        Core.save(); refresh(); openPalette();
    }

    function resizePlaced(idx, dir) {
        const g = Core.ensureGame(pet());
        const e = itemsOf(g)[idx]; if (!e) return;
        e.size = Math.max(32, Math.min(280, Math.round(e.size * (dir > 0 ? 1.15 : 0.87))));
        Core.save(); refresh(); openPalette();
    }

    function storePlaced(idx) {
        const g = Core.ensureGame(pet());
        itemsOf(g).splice(idx, 1);
        Core.save(); refresh(); openPalette();
    }

    // 꾸미기 모드에서 스테이지 아이템 드래그 + 선택 툴바
    function bindDecorateReal() {
        const stage = document.getElementById('pg-stage'); if (!stage) return;
        stage.querySelectorAll('.pg-item').forEach(el => {
            const idx = parseInt(el.dataset.idx, 10);
            el.addEventListener('pointerdown', (ev) => {
                ev.preventDefault();
                el.setPointerCapture(ev.pointerId);
                let moved = false;
                const rect = stage.getBoundingClientRect();
                const onMove = (mv) => {
                    moved = true;
                    const x = Math.max(4, Math.min(96, (mv.clientX - rect.left) / rect.width * 100));
                    const y = Math.max(10, Math.min(96, (mv.clientY - rect.top) / rect.height * 100));
                    el.style.left = x + '%'; el.style.top = y + '%';
                    el._pos = { x, y };
                };
                const onUp = () => {
                    el.removeEventListener('pointermove', onMove);
                    el.removeEventListener('pointerup', onUp);
                    if (moved && el._pos) {
                        const g = Core.ensureGame(pet());
                        const e = itemsOf(g)[idx];
                        if (e) { e.x = Math.round(el._pos.x); e.y = Math.round(el._pos.y); Core.save(); }
                    } else {
                        // 탭 = 선택 툴바 표시
                        sheet(`<div class="flex items-center gap-2">
                          <b class="text-[12px] flex-1">아이템 편집</b>
                          <button onclick="PetGame.resizePlaced(${idx},1)" class="px-3 py-1.5 rounded-lg bg-white border text-[12px]">🔍＋</button>
                          <button onclick="PetGame.resizePlaced(${idx},-1)" class="px-3 py-1.5 rounded-lg bg-white border text-[12px]">🔍－</button>
                          <button onclick="PetGame.storePlaced(${idx})" class="px-3 py-1.5 rounded-lg bg-white border text-[12px]">📦 보관</button>
                          <button onclick="PetGame.toggleDecorate()" class="px-3 py-1.5 rounded-lg bg-brand-500 text-white text-[12px] font-bold">완료</button></div>`);
                    }
                };
                el.addEventListener('pointermove', onMove);
                el.addEventListener('pointerup', onUp);
            });
        });
    }
```
- 파일 상단의 `function bindDecorate() {}`를 삭제하고 `refresh()`가 `bindDecorateReal()`을 호출하도록 교체 (`if (S.mode === 'decorate') bindDecorateReal();`). `PetGameStageInternals` 노출부도 삭제.
- PetGame 등록부의 `toggleDecorate` 자리를 실제 함수로 교체하고 `placeOwned, resizePlaced, storePlaced` 추가.

- [ ] **Step 2: 문법 확인**

Run: `cd projects/petnna && node --check js/petgame/game-stage.js`
Expected: 에러 없음

- [ ] **Step 3: 커밋**

```bash
git add projects/petnna/js/petgame/game-stage.js
git commit -m "feat(petgame): 꾸미기 모드 — 드래그·크기·보관 (Task 6)"
```

---

### Task 7: 통합 — 템플릿 교체·스크립트 로드·구 방코드 제거·CSS

**Files:**
- Modify: `projects/petnna/index.html` (스크립트 태그)
- Modify: `projects/petnna/js/templates/mypet.js` (`.room-stage` 블록 → `#petgame-root`)
- Modify: `projects/petnna/js/mypet.js` (렌더 진입에서 `PetGame.mount` 호출, 구 방 렌더 호출 제거)
- Modify: `projects/petnna/css/style.css` (펫게임 소량 CSS 추가)

**Interfaces:**
- Consumes: `PetGame.mount('petgame-root')`
- Produces: 마이펫 탭 진입 시 게임이 뜬다. 기존 위젯(시계·운세·사주카드·미션)은 유지.

- [ ] **Step 1: index.html 스크립트 추가** — `js/state.js` 로드 직후에 삽입:

```html
    <script src="js/petgame/game-items.js?v=1"></script>
    <script src="js/petgame/game-core.js?v=1"></script>
    <script src="js/petgame/game-stage.js?v=1"></script>
```
그리고 `js/templates/mypet.js`·`js/mypet.js`·`css/style.css`의 `?v=`를 한 단계 올린다(예: 158→161, 156→161).

- [ ] **Step 2: 템플릿 교체** — `js/templates/mypet.js`에서 `class="room-stage` 를 포함한 div 블록(주변 room-layout 컨트롤·room-shop 마크업 포함, 대략 라인 280–420 사이 — `room-stage` 검색으로 시작·닫힘 태그 짝을 맞춰 정확한 범위 확인)을 다음으로 교체:

```html
                <div id="petgame-root" class="w-full"></div>
```
주의: 사주 카드(`room-saju-card`)·상단 위젯은 블록 밖이면 보존. 교체 후 `grep -c "room-stage" js/templates/mypet.js` → 0 확인.

- [ ] **Step 3: mypet.js 연결** — 마이펫 탭 렌더 함수(방 렌더를 호출하던 위치 — `renderRoomStickers`/`loadRoomTheme` 호출부를 `grep -n "renderRoomStickers\|loadRoomTheme" js/mypet.js`로 찾음)에서 구 방 렌더 호출들을 제거하고 아래로 대체:

```js
    if (typeof PetGame !== 'undefined' && document.getElementById('petgame-root')) {
        // 레거시 스티커 1회 이관 준비 — ensureGame이 pet._legacyStickers를 읽는다
        const cur = getActivePet();
        if (cur && !cur.game) {
            try { cur._legacyStickers = JSON.parse(localStorage.getItem(`petnna_room_stickers_${cur.id}`)) || []; } catch (e) {}
        }
        PetGame.mount('petgame-root');
    }
```
구 방 전용 함수들(`normalizeRoomLayout`~`saveRoomStickers`, `ROOM_SHOP_ITEMS`, `buyRoomItem`, `renderRoomShop`, `ROOM_THEMES`, `applyRoomTheme`, `loadRoomTheme`, `createRoomConnection`, butler/pet stage 렌더러)은 **삭제하지 말고 이번 태스크에서는 호출만 끊는다** (죽은 코드 정리는 별도 후속 — 회귀 위험 최소화). 단, 다른 탭에서 호출되는 것이 없는지 `grep -rn "<함수명>" js/ | grep -v mypet.js`로 각각 확인하고, 있으면 그 함수는 보존 목록에 기록.

- [ ] **Step 4: CSS 추가** — `css/style.css` 말미:

```css
/* ── 펫게임 ─────────────────────────────── */
.pg-bubble{position:absolute;top:-34px;left:50%;transform:translateX(-50%);background:#fff;border:1px solid #e6dfd8;border-radius:12px;padding:3px 10px;font-size:11px;font-weight:700;white-space:nowrap;box-shadow:0 2px 8px rgba(0,0,0,.08)}
.pg-editable{outline:2px dashed #cc785c;outline-offset:3px;border-radius:8px;cursor:grab;touch-action:none}
.pg-eat{animation:pgEat .5s ease-in-out 2}
@keyframes pgEat{0%,100%{transform:translate(-50%,-100%) scale(1)}50%{transform:translate(-50%,-100%) scale(1.08,0.92)}}
.pg-float{animation:pgFloat 1.1s ease-out forwards;font-size:20px}
@keyframes pgFloat{from{opacity:1;transform:translateY(0)}to{opacity:0;transform:translateY(-46px)}}
@media (prefers-reduced-motion: reduce){.pg-eat,.pg-float{animation:none}}
```

- [ ] **Step 5: preview로 검증**

- `preview_start`(petnna-dev, 8901) → 로그인 게이트 우회(`preview_eval`로 main 표시) → 마이펫 탭
- 확인: HUD(레벨·코인·게이지) 표시 / 방↔마당 탭 전환 / 배경 webp 로드 / 콘솔에 신규 에러 없음(Supabase 에러는 기존) / 기존 위젯(시계·운세) 생존
- `preview_screenshot`으로 캡처

- [ ] **Step 6: 커밋**

```bash
git add projects/petnna/index.html projects/petnna/js/templates/mypet.js projects/petnna/js/mypet.js projects/petnna/css/style.css
git commit -m "feat(petgame): 마이펫 탭 통합 — 게임 마운트·구 방코드 호출 차단 (Task 7)"
```

---

### Task 8: 케어 연동 훅 — 산책·일기·몸무게·출석

**Files:**
- Modify: `projects/petnna/js/walk.js` (`stopAndSaveWalk()` 내부, 거리 확정 직후 — `walkDistanceRun.toFixed(2)` 사용 지점 ~566라인)
- Modify: `projects/petnna/js/album.js` (일기 저장 성공 다이얼로그 지점 ~940라인 `title: "일기 저장 성공! 📕✨"` 직전 / `submitWeightLog()` 성공 지점 ~489라인)
- Modify: `projects/petnna/js/app.js` (부트스트랩 완료 지점에 출석 보상)

**Interfaces:**
- Consumes: `PetGame.earnCare(type, amount)` (Task 4)
- Produces: 실제 케어 활동 → 코인 토스트

- [ ] **Step 1: 훅 삽입** (공통 패턴 — 반드시 `typeof` 가드):

walk.js — `stopAndSaveWalk()`에서 산책 기록 객체가 만들어진 직후:
```js
        if (typeof PetGame !== 'undefined') PetGame.earnCare('walk', parseFloat(walkDistanceRun.toFixed(2)));
```
album.js — 일기 저장 성공 지점:
```js
        if (typeof PetGame !== 'undefined') PetGame.earnCare('diary');
```
album.js — `submitWeightLog()` 저장 성공 지점:
```js
        if (typeof PetGame !== 'undefined') PetGame.earnCare('health');
```
app.js — 초기화 마지막(메인 표시 후):
```js
        setTimeout(() => { if (typeof PetGame !== 'undefined') PetGame.earnCare('attend'); }, 1500);
```

- [ ] **Step 2: preview 검증**

콘솔에서 `PetGame.earnCare('diary')` 실행 → 토스트+코인 증가, 재실행 → null(일일 중복 방지). `node --check`로 수정 파일 4개 문법 확인.

- [ ] **Step 3: 커밋**

```bash
git add projects/petnna/js/walk.js projects/petnna/js/album.js projects/petnna/js/app.js
git commit -m "feat(petgame): 케어 연동 — 산책·일기·몸무게·출석 코인 (Task 8)"
```

---

### Task 9: 종합 검증·회귀·마무리

**Files:**
- Test: preview 수동 시나리오 + node 테스트 3종 재실행

- [ ] **Step 1: node 테스트 전체 재실행**

Run: `cd projects/petnna && for t in tests/petgame/*.js; do node $t; done`
Expected: 3개 모두 OK

- [ ] **Step 2: preview 종합 시나리오**

1. 마이펫 진입 → HUD·마당 렌더 (스크린샷)
2. 상점 → 사료 구매(코인 감소) → 먹이주기 → 투척·냠냠 연출 → XP 증가 확인
3. 레벨업 강제(`콘솔: PetGameCore.addXp(getActivePet(), 200); PetGame.refresh()`) → 축하 연출·해금 반영
4. 꾸미기 → 밥그릇 구매·배치·드래그·크기조절·보관
5. 방↔마당 전환 후 각 공간 배치 유지 확인, 새로고침 후 상태 복원(localStorage) 확인
6. 다른 탭(건강·산책·일기장·설정) 진입 — 콘솔 신규 에러 0 확인
7. `preview_resize`(mobile 375px)로 모바일 레이아웃 확인

- [ ] **Step 3: 발견 이슈 수정 후 최종 커밋·푸시**

```bash
git add -A && git commit -m "feat(petgame): 마이펫 육성 게임 v1 완성 — 검증 통과"
git push origin master
```

---

## Self-Review 결과

- **스펙 커버리지**: 루프/경제(T3·T8)·먹이연출(T5)·성장진화(T2·T5)·자유배치(T6)·방↔마당(T4)·테마상점(T5)·마이그레이션(T2·T7)·플레이스홀더(T4 imgOrEmoji)·해금로드맵(T1 unlockLv) — 전부 태스크에 매핑됨. 진화 연출은 celebrate()가 담당(전용 빛 이펙트는 v1에선 카드 연출로 충분).
- **플레이스홀더 스캔**: TBD/TODO 없음. 모든 코드 스텝에 실제 코드 포함.
- **타입 일관성**: `PetGame.earnCare(type, amount)`(stage) ↔ `Core.earnCare(pet, type, amount)`(core) 시그니처 구분 명시. `itemsOf(g)`·`ensureGame(pet)`·`stageForLevel(lv)` 명칭 태스크 간 일치 확인.
