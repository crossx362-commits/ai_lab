const assert = require('assert');
const fs = require('fs');
const vm = require('vm');

const code = fs.readFileSync(require.resolve('../js/mypet.js'), 'utf8');
const templateCode = fs.readFileSync(require.resolve('../js/templates/mypet.js'), 'utf8');

function loadMypetContext() {
    const ctx = {
        console,
        window: { innerWidth: 1024 },
        document: { getElementById: () => null, createElementNS: () => ({ setAttribute() {}, appendChild() {}, style: { setProperty() {} } }) },
        setInterval: () => 0,
        navigator: {},
        localStorage: { getItem: () => null, setItem() {} },
        pets: [
            { id: 1, name: '초코' },
            { id: 2, name: '나비' },
            { id: 3, name: '솜이' }
        ],
        saveStateCalled: 0,
        saveState() { ctx.saveStateCalled += 1; },
        showToast() {},
    };
    vm.createContext(ctx);
    vm.runInContext(code, ctx);
    return ctx;
}

const ctx = loadMypetContext();

assert.strictEqual(ctx.getActiveRoomLayout(), 'living', 'new rooms should default to living room layout');

const circleSlots = ctx.getPetStageSlots(3, false, 'circle');
const livingSlots = ctx.getPetStageSlots(3, false, 'living');
assert.notDeepStrictEqual(livingSlots, circleSlots, 'living layout should use a distinct room arrangement');
assert.deepStrictEqual(JSON.parse(JSON.stringify(livingSlots.slice(0, 3))), [
    { x: 30, y: 72 },
    { x: 70, y: 72 },
    { x: 50, y: 84 }
]);

function boxesOverlap(a, b) {
    return !(a.x + a.w <= b.x || b.x + b.w <= a.x || a.y + a.h <= b.y || b.y + b.h <= a.y);
}

function assertRoomHasNoOverlaps(layout, isMobile, count) {
    const points = ctx.resolvePetStageCollisions(ctx.getPetStageSlots(count, isMobile, layout), isMobile);
    const butlerBox = { x: 46, y: 42, w: 8, h: 16 };
    const petBoxes = points.map(point => ({ x: point.x - 4, y: point.y - 6, w: 8, h: 12 }));

    petBoxes.forEach((box, idx) => {
        assert.ok(!boxesOverlap(box, butlerBox), `${layout}/${isMobile}/${count} pet ${idx} should not overlap butler`);
        for (let next = idx + 1; next < petBoxes.length; next += 1) {
            assert.ok(!boxesOverlap(box, petBoxes[next]), `${layout}/${isMobile}/${count} pets ${idx}/${next} should not overlap`);
        }
    });
}

['living', 'circle'].forEach((layout) => {
    [false, true].forEach((isMobile) => {
        [1, 2, 3, 4, 6, 8, 10, 12].forEach((count) => assertRoomHasNoOverlaps(layout, isMobile, count));
    });
});

ctx.setRoomLayoutForActivePet('circle');
assert.strictEqual(ctx.pets[0].roomLayout, 'circle', 'selected layout should persist on the active pet');
assert.strictEqual(ctx.saveStateCalled, 1, 'layout changes should save app state once');

assert.match(templateCode, /id="room-layout-badge"/, 'room header should show the active layout badge');
assert.match(templateCode, /room-layout-preview-card/, 'layout picker should use preview cards');
assert.match(templateCode, /편안한 소파와 러그/, 'living preview should explain the living room mood');
assert.match(templateCode, /images\/room\/cozy-sofa\.png/, 'living room should mount generated sofa asset');
assert.match(templateCode, /images\/room\/soft-round-rug\.png/, 'living room should mount generated rug asset');
assert.match(templateCode, /images\/room\/sunny-window\.png/, 'living room should mount generated window asset');

console.log('mypet room layout tests passed');
