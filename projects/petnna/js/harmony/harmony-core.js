// 조화도 명리 코어 — 갑자/오행/궁합/일진 순수 계산(DOM 금지). UMD-lite.
(function (root) {
    const STEMS = ['갑','을','병','정','무','기','경','신','임','계'];
    const BRANCHES = ['자','축','인','묘','진','사','오','미','신','유','술','해'];
    const STEM_EL = ['목','목','화','화','토','토','금','금','수','수'];
    const BRANCH_EL = { 자:'수', 축:'토', 인:'목', 묘:'목', 진:'토', 사:'화', 오:'화', 미:'토', 신:'금', 유:'금', 술:'토', 해:'수' };
    const MONTH_BRANCH = ['축','인','묘','진','사','오','미','신','유','술','해','자']; // 1~12월(간이)
    const SHENG = { 목:'화', 화:'토', 토:'금', 금:'수', 수:'목' };  // 상생
    const KE = { 목:'토', 토:'수', 수:'화', 화:'금', 금:'목' };      // 상극
    const ELS = ['목','화','토','금','수'];

    function yearPillar(y) {
        const s = ((y - 4) % 10 + 10) % 10, b = ((y - 4) % 12 + 12) % 12;
        return { stem: STEMS[s], branch: BRANCHES[b], stemEl: STEM_EL[s], branchEl: BRANCH_EL[BRANCHES[b]] };
    }

    function dayPillar(date) {
        const jdn = Math.floor(date.getTime() / 86400000) + 2440588;
        const idx = ((jdn + 49) % 60 + 60) % 60;
        const s = idx % 10, b = idx % 12;
        return { stem: STEMS[s], branch: BRANCHES[b], stemEl: STEM_EL[s], branchEl: BRANCH_EL[BRANCHES[b]] };
    }

    function _parseISO(iso) {
        const [y, m, d] = String(iso).slice(0, 10).split('-').map(Number);
        return { y, m: m || 1, d: d || 1, date: new Date(Date.UTC(y, (m || 1) - 1, d || 1)) };
    }

    function elementsOf(birthISO) {
        const { y, m, date } = _parseISO(birthISO);
        const yp = yearPillar(y);
        const mb = MONTH_BRANCH[m - 1];
        const dp = dayPillar(date);
        const list = [yp.stemEl, yp.branchEl, BRANCH_EL[mb], dp.stemEl, dp.branchEl];
        const vec = { 목: 0, 화: 0, 토: 0, 금: 0, 수: 0 };
        list.forEach(e => { vec[e] += 1; });
        const dominant = ELS.reduce((best, e) => vec[e] > vec[best] ? e : best, '목');
        return { vec, list, dominant };
    }

    function _rel(a, b) {
        if (a === b) return 'same';
        if (SHENG[a] === b || SHENG[b] === a) return 'sheng';
        if (KE[a] === b || KE[b] === a) return 'ke';
        return 'neutral';
    }

    const REL_TEXT = {
        상생: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 서로를 살려주는 상생 궁합이에요. 함께할수록 기운이 자라나요!`,
        상극: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 부딪히기 쉬운 상극이지만, 그만큼 서로를 단련시키는 관계예요. 아래 가이드를 참고하세요.`,
        동일: (p) => `둘 다 ${p} 기운 — 닮은꼴 영혼이라 말하지 않아도 통해요. 다만 같은 데서 지치니 환기가 필요해요.`,
        중립: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 서로의 영역을 존중하는 담백한 궁합이에요. 꾸준함이 힘이 됩니다.`,
    };

    function harmony(petISO, ownerISO) {
        const pet = elementsOf(petISO), owner = elementsOf(ownerISO);
        let sheng = 0, ke = 0, same = 0;
        for (const a of pet.list) for (const b of owner.list) {
            const r = _rel(a, b);
            if (r === 'sheng') sheng++; else if (r === 'ke') ke++; else if (r === 'same') same++;
        }
        const n = pet.list.length * owner.list.length;
        const score = Math.round(Math.max(5, Math.min(99, 50 + (sheng / n) * 40 - (ke / n) * 25 + (same / n) * 10)));
        const rd = _rel(pet.dominant, owner.dominant);
        const type = rd === 'sheng' ? '상생' : rd === 'ke' ? '상극' : rd === 'same' ? '동일' : '중립';
        const text = type === '동일' ? REL_TEXT.동일(pet.dominant) : REL_TEXT[type](pet.dominant, owner.dominant);
        return { score, pet, owner, relation: { type, text } };
    }

    const AREAS = [
        { key: 'play', name: '놀이', emoji: '🎾', element: '화' },
        { key: 'meal', name: '식사', emoji: '🍚', element: '토' },
        { key: 'walk', name: '산책', emoji: '🐾', element: '목' },
        { key: 'rest', name: '휴식', emoji: '💤', element: '수' },
    ];
    const TIPS = {
        play: { 최고: '놀이 궁합 만점! 새 장난감이 최고의 선물이에요', 좋음: '짧고 굵은 놀이가 잘 맞아요. 하루 2번 10분씩!', 보통: '펫이 좋아하는 놀이 하나를 정해 루틴으로 만들어보세요', 노력: '놀이 전 간식으로 텐션을 올려주면 훨씬 잘 놀아요', 주의: '과격한 놀이보다 노즈워크 같은 차분한 놀이가 길해요' },
        meal: { 최고: '먹복이 타고났어요. 규칙적인 식사가 건강운을 지켜줘요', 좋음: '식사 시간을 일정하게 — 토(土) 기운은 규칙에서 자라요', 보통: '간식보다 주식에 정성을. 식기를 깨끗이 하면 운이 트여요', 노력: '급하게 먹는 습관 주의 — 슬로우 식기를 써보세요', 주의: '소화가 약할 수 있어요. 소량씩 나눠 급여하는 게 좋아요' },
        walk: { 최고: '산책이 곧 보약! 새로운 길을 함께 개척해보세요', 좋음: '아침 산책이 특히 길해요 — 목(木) 기운이 아침에 자라요', 보통: '매일 같은 시간 산책이 둘의 리듬을 맞춰줘요', 노력: '산책 거리보다 냄새 맡을 시간을 충분히 주세요', 주의: '무리한 장거리보다 짧은 산책 여러 번이 좋아요' },
        rest: { 최고: '함께 쉬기만 해도 충전되는 사이! 낮잠 메이트네요', 좋음: '조용한 휴식 공간을 만들어주면 애착이 깊어져요', 보통: '휴식 중엔 건드리지 않기 — 수(水) 기운은 고요에서 회복돼요', 노력: '잠자리를 어둡고 아늑하게 바꿔보세요', 주의: '수면이 예민할 수 있어요. 소음을 줄여주는 게 우선이에요' },
    };

    function _grade(s) { return s >= 85 ? '최고' : s >= 70 ? '좋음' : s >= 55 ? '보통' : s >= 40 ? '노력' : '주의'; }

    function areas(petISO, ownerISO) {
        const h = harmony(petISO, ownerISO);
        return AREAS.map(a => {
            const strength = (h.pet.vec[a.element] + h.owner.vec[a.element]) / 10; // 0~1
            const bonus = (SHENG[h.pet.dominant] === a.element || SHENG[h.owner.dominant] === a.element) ? 10 : 0;
            const score = Math.round(Math.max(5, Math.min(99, h.score * 0.5 + strength * 40 + bonus)));
            const grade = _grade(score);
            return { ...a, score, grade, tip: TIPS[a.key][grade] };
        });
    }

    function dayLuck(date, petDominant) {
        const dp = dayPillar(date);
        const els = [dp.stemEl, dp.branchEl];
        const shengWithPet = els.some(e => SHENG[e] === petDominant || SHENG[petDominant] === e);
        return {
            walk: (els.includes('목') || els.includes('화')) && shengWithPet,
            groom: dp.stemEl === '금',
            vet: (els.includes('수') || els.includes('토')) && shengWithPet,
            dayEl: dp.stemEl,
        };
    }

    function todayIndex(harmonyScore, petDominant, date) {
        const d = date || new Date();
        const el = dayPillar(d).stemEl;
        const r = _rel(el, petDominant);
        const adj = r === 'sheng' ? 15 : r === 'ke' ? -15 : 0;
        return Math.round(Math.max(5, Math.min(99, (harmonyScore || 60) + adj)));
    }

    function analyze(petISO, ownerISO) {
        const h = harmony(petISO, ownerISO);
        return {
            score: h.score,
            elements: { pet: h.pet, owner: h.owner },
            relation: h.relation,
            areas: areas(petISO, ownerISO),
            measuredAt: new Date().toISOString().slice(0, 10),
        };
    }

    const api = { yearPillar, dayPillar, elementsOf, harmony, areas, dayLuck, todayIndex, analyze };
    root.PetHarmony = api;
    if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
