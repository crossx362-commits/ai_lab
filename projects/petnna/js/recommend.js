// 반려동물 프로필(품종·나이·체중·알러지) 기반 맞춤 추천 카드
// 규칙 매칭으로 간식/영양제 TOP3를 홈(마이펫)·상점(펫라이프) 상단에 노출한다.

const RECO_CATALOG = [
    { id: 'r1', emoji: '🦴', name: '관절 튼튼 글루코사민 스틱', kind: '영양제',
      types: ['dog'], stages: ['senior', 'adult'], concerns: ['joint'], allergens: [],
      reason: '관절 부담이 큰 체형·연령에 도움' },
    { id: 'r2', emoji: '🐟', name: '오메가3 연어 스킨케어 츄',   kind: '영양제',
      types: ['dog', 'cat'], stages: ['adult', 'senior'], concerns: ['skin'], allergens: ['fish'],
      reason: '피부·모질 관리에 좋은 오메가3' },
    { id: 'r3', emoji: '🥛', name: '락토프리 소화 유산균 간식',   kind: '간식',
      types: ['dog', 'cat', 'rabbit'], stages: ['puppy', 'adult'], concerns: ['digestion'], allergens: ['dairy'],
      reason: '장 건강과 소화를 돕는 저자극 간식' },
    { id: 'r4', emoji: '🦷', name: '치석 케어 덴탈 츄',         kind: '간식',
      types: ['dog', 'cat'], stages: ['adult', 'senior'], concerns: ['dental'], allergens: [],
      reason: '씹으며 치석을 줄이는 덴탈 간식' },
    { id: 'r5', emoji: '🐣', name: '성장기 칼슘 영양 파우더',     kind: '영양제',
      types: ['dog', 'cat'], stages: ['puppy'], concerns: ['growth'], allergens: [],
      reason: '성장기 뼈·근육 발달 지원' },
    { id: 'r6', emoji: '🥕', name: '섬유질 가득 채소 스낵',       kind: '간식',
      types: ['rabbit', 'hamster'], stages: ['puppy', 'adult', 'senior'], concerns: ['digestion'], allergens: [],
      reason: '초식 반려동물의 소화·치아 마모에 도움' },
    { id: 'r7', emoji: '💊', name: '종합 멀티비타민 정',         kind: '영양제',
      types: ['dog', 'cat', 'rabbit', 'hamster'], stages: ['adult', 'senior'], concerns: ['general'], allergens: [],
      reason: '전연령 기본 영양 밸런스 보충' },
    { id: 'r8', emoji: '🍖', name: '저칼로리 다이어트 트릿',     kind: '간식',
      types: ['dog', 'cat'], stages: ['adult', 'senior'], concerns: ['weight'], allergens: ['chicken'],
      reason: '체중 관리가 필요한 아이의 부담 없는 간식' },
    { id: 'r9', emoji: '🐹', name: '소동물 미니 곡물 트릿',       kind: '간식',
      types: ['hamster'], stages: ['puppy', 'adult', 'senior'], concerns: ['general'], allergens: ['grain'],
      reason: '햄스터 크기에 맞춘 소량 영양 간식' },
];

// 나이 문자열("2살 (청소년기)" 등)에서 생애 단계 추정
function recoLifeStage(pet) {
    const raw = String(pet && pet.age || '');
    const m = raw.match(/(\d+(?:\.\d+)?)/);
    const years = m ? parseFloat(m[1]) : null;
    if (years === null) {
        if (/시니어|노령|노묘|노견/.test(raw)) return 'senior';
        if (/유아|아기|퍼피|자견|자묘/.test(raw)) return 'puppy';
        return 'adult';
    }
    if (years < 1) return 'puppy';
    if (years >= 8) return 'senior';
    return 'adult';
}

// 타입별 대형(관절/체중 부담) 여부 추정
function recoIsHeavy(pet) {
    const w = parseFloat(pet && pet.weight) || 0;
    switch (pet && pet.type) {
        case 'dog':     return w >= 20;
        case 'cat':     return w >= 6;
        case 'rabbit':  return w >= 2.5;
        default:        return false;
    }
}

// 알러지 정보(문자열/배열) → 알러젠 키워드 매칭
function recoAllergens(pet) {
    let src = pet && (pet.allergies || pet.allergy);
    if (!src) return [];
    if (Array.isArray(src)) src = src.join(' ');
    src = String(src).toLowerCase();
    const map = {
        chicken: ['닭', 'chicken'], fish: ['생선', '어류', 'fish', '연어', 'salmon'],
        dairy: ['유제품', '우유', 'dairy', 'milk'], grain: ['곡물', 'grain', '밀', 'wheat'],
    };
    const hit = [];
    Object.keys(map).forEach(key => {
        if (map[key].some(word => src.includes(word.toLowerCase()))) hit.push(key);
    });
    return hit;
}

function getPetRecommendations(pet) {
    if (!pet) return [];
    const stage = recoLifeStage(pet);
    const heavy = recoIsHeavy(pet);
    const allergens = recoAllergens(pet);

    const scored = RECO_CATALOG
        .filter(item => !item.allergens.some(a => allergens.includes(a)))
        .map(item => {
            let score = 0;
            if (item.types.includes(pet.type)) score += 5; else score -= 3;
            if (item.stages.includes(stage)) score += 3;
            if (heavy && (item.concerns.includes('joint') || item.concerns.includes('weight'))) score += 3;
            if (item.concerns.includes('general')) score += 1;
            return { item, score };
        })
        .filter(s => s.score > 0)
        .sort((a, b) => b.score - a.score);

    return scored.slice(0, 3).map(s => s.item);
}

function petNameJosa(name) {
    const n = String(name || '');
    if (!n) return '우리 아이에게';
    const last = n.charCodeAt(n.length - 1);
    const isKorean = last >= 0xac00 && last <= 0xd7a3;
    const hasBatchim = isKorean && ((last - 0xac00) % 28) !== 0;
    return hasBatchim ? `${n}이에게` : `${n}에게`;
}

function renderPetRecoCard(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const items = getPetRecommendations(pet);
    if (!pet || items.length === 0) {
        container.innerHTML = '';
        return;
    }

    const rows = items.map(item => `
        <div class="flex items-center gap-3 p-2.5 rounded-2xl bg-white/70 border border-amber-50">
            <span class="text-2xl shrink-0" aria-hidden="true">${item.emoji}</span>
            <div class="min-w-0">
                <div class="flex items-center gap-1.5">
                    <h4 class="font-bold text-gray-800 text-xs truncate">${item.name}</h4>
                    <span class="shrink-0 bg-brand-500 text-white font-mono text-[9px] font-black px-1.5 py-0.5 rounded-full">${item.kind}</span>
                </div>
                <p class="text-[10px] text-gray-500 leading-snug mt-0.5">${item.reason}</p>
            </div>
        </div>
    `).join('');

    container.innerHTML = `
        <div class="card-modern bg-amber-50/50 p-4 space-y-2.5">
            <div class="flex items-center gap-1.5">
                <span class="text-base" aria-hidden="true">🎯</span>
                <h3 class="text-sm font-bold text-gray-900">${petNameJosa(pet.name)} 맞는 맞춤 추천 TOP3</h3>
            </div>
            <p class="text-[10px] text-gray-400 -mt-1">품종·나이·체중·알러지 정보를 반영했어요</p>
            <div class="space-y-2">${rows}</div>
        </div>
    `;
}
