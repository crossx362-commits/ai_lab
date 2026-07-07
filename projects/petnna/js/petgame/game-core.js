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
