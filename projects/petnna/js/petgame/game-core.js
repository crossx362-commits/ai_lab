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
        let xpGain = f.xp;
        // 조화도 버프: 오늘 조화도 ≥70 → XP 1.2배 (하모니 모듈·측정 데이터 없으면 무영향)
        try {
            if (typeof PetHarmony !== 'undefined' && pet.harmonyData && pet.harmonyData.elements) {
                const idx = PetHarmony.todayIndex(pet.harmonyData.score || pet.harmonyData.avgScore, pet.harmonyData.elements.pet.dominant);
                if (idx >= 70) xpGain = Math.round(f.xp * 1.2);
            }
        } catch (e) {}
        const levelUp = addXp(pet, xpGain);   // 내부에서 save
        return { ok: true, msg: `냠냠! ${f.name} 맛있다! (+${xpGain}XP)`, food: f, levelUp };
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
        const soft = (pet.harmonyData && pet.harmonyData.elements) ? 0.8 : 1;
        g.hunger = Math.max(0, g.hunger - 3 * hours * soft);
        g.happy = Math.max(0, g.happy - 1 * hours * soft);
        save();
    }

    const api = { init, save, ensureGame, xpForLevel, addXp, todayStr, spend, buyFood, buyItem, buyTheme, feed, earnCare, decay };
    root.PetGameCore = api;
    if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
