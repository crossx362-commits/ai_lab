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
        { id: 'churu',  name: '츄르',    emoji: '🐟', img: P + 'food/churu.png',  price: 35,  hunger: 25,  happy: 30, xp: 12 },
        { id: 'yogurt', name: '요거트',  emoji: '🍦', img: P + 'food/yogurt.png', price: 50,  hunger: 30,  happy: 35, xp: 16 },
        { id: 'jerky',  name: '수제 육포', emoji: '🥓', img: P + 'food/jerky.png', price: 90,  hunger: 70,  happy: 45, xp: 28 },
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
        // 코인 상점 확장 (2026-07-09) — 이모지 폴백으로 안전, 실결제 무관
        item('scratcher',   '스크래처',   '🪵', 'room', 'S', 45,  2),
        item('hammock',     '해먹',       '🪢', 'yard', 'M', 130, 3),
        item('aquarium',    '어항',       '🐠', 'room', 'M', 170, 4),
        item('teepee',      '펫 텐트',    '⛺', 'room', 'M', 160, 5),
        item('snowman',     '눈사람',     '⛄', 'yard', 'M', 220, 6),
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
