// care-coins.js — 데일리 케어 코인 & 미니 리워드 상점 (짠테크 리텐션, 나무 기획)
// 기존 streak/achievements 데이터를 확장: 출석·산책·투약(케어일정)·건강기록 완료 시
// 코인을 멱등(idempotent) 적립하고, 미니 상점에서 프로필 꾸미기·게임 아이템으로 교환한다.
// 신규 서버 저장/스키마 변경 없음 — 전부 localStorage(walks/careSchedules/healthLogs 재사용).

const CARE_COIN_KEYS = {
    BALANCE: 'petna_care_coins',        // 현재 코인 잔액(숫자)
    LEDGER: 'petna_care_coin_ledger',   // 이미 지급한 "날짜:소스" 키 목록(중복 적립 방지)
    OWNED: 'petna_care_rewards_owned',  // 교환한 리워드 id 목록
};

// 적립 소스별 지급량
const CARE_COIN_RULES = [
    { source: 'attend', coins: 5,  label: '출석' },
    { source: 'walk',   coins: 10, label: '산책' },
    { source: 'care',   coins: 10, label: '투약·케어' },
    { source: 'health', coins: 5,  label: '건강기록' },
];

// 미니 리워드 상점 카탈로그 (프로필 꾸미기 + 게임 아이템)
const CARE_REWARD_CATALOG = [
    { id: 'frame_gold',    emoji: '🥇', name: '골드 프로필 테두리', cost: 100, kind: '프로필 꾸미기' },
    { id: 'nametag_rainbow', emoji: '🌈', name: '무지개 이름표',    cost: 150, kind: '프로필 꾸미기' },
    { id: 'crown_badge',   emoji: '👑', name: '왕관 뱃지',          cost: 200, kind: '프로필 꾸미기' },
    { id: 'food_premium',  emoji: '🍖', name: '프리미엄 사료 팩',    cost: 80,  kind: '게임 아이템' },
    { id: 'toybox',        emoji: '🧸', name: '장난감 상자',         cost: 120, kind: '게임 아이템' },
];

function _coinGetBalance() {
    const n = parseInt(localStorage.getItem(CARE_COIN_KEYS.BALANCE) || '0', 10);
    return Number.isFinite(n) && n >= 0 ? n : 0;
}
function _coinSetBalance(n) {
    localStorage.setItem(CARE_COIN_KEYS.BALANCE, String(Math.max(0, Math.round(n))));
}
function _coinGetLedger() {
    try { return new Set(JSON.parse(localStorage.getItem(CARE_COIN_KEYS.LEDGER) || '[]')); }
    catch { return new Set(); }
}
function _coinSaveLedger(set) {
    try { localStorage.setItem(CARE_COIN_KEYS.LEDGER, JSON.stringify([...set])); } catch {}
}
function _coinGetOwned() {
    try { return JSON.parse(localStorage.getItem(CARE_COIN_KEYS.OWNED) || '[]'); }
    catch { return []; }
}
function _coinSaveOwned(arr) {
    try { localStorage.setItem(CARE_COIN_KEYS.OWNED, JSON.stringify(arr)); } catch {}
}

// 소스별로 "코인 지급 대상이 되는 날짜 집합"을 구성 (기존 데이터 재사용)
function _coinEarnDates() {
    const out = { attend: new Set(), walk: new Set(), care: new Set(), health: new Set() };
    // 출석: 오늘 로그인/방문한 것을 인정 (미인증 상태에선 적립 안 함)
    if (localStorage.getItem('petna_is_logged_in') === 'true') {
        out.attend.add(new Date().toISOString().split('T')[0]);
    }
    // 산책
    (typeof walks !== 'undefined' ? walks : []).forEach(w => {
        const d = w.savedAt ? w.savedAt.split('T')[0] : null;
        if (d) out.walk.add(d);
    });
    // 투약·케어 일정 완료
    try {
        const cs = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || null;
        (cs && Array.isArray(cs.completionHistory) ? cs.completionHistory : []).forEach(c => {
            const d = c.completedAt ? c.completedAt.split('T')[0] : null;
            if (d) out.care.add(d);
        });
    } catch {}
    // 건강 기록(식사·음수·배변 중 하나라도)
    const hist = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    hist.forEach(h => {
        if (h.date && (h.food > 0 || h.water > 0 || (h.poop !== null && h.poop !== undefined))) out.health.add(h.date);
    });
    return out;
}

// 아직 지급하지 않은 (날짜:소스)에 코인을 멱등 적립. 지급된 총액을 반환.
function syncCareCoins() {
    const dates = _coinEarnDates();
    const ledger = _coinGetLedger();
    let earned = 0;
    CARE_COIN_RULES.forEach(rule => {
        (dates[rule.source] || new Set()).forEach(d => {
            const key = `${d}:${rule.source}`;
            if (!ledger.has(key)) {
                ledger.add(key);
                earned += rule.coins;
            }
        });
    });
    if (earned > 0) {
        _coinSetBalance(_coinGetBalance() + earned);
        _coinSaveLedger(ledger);
    }
    return earned;
}

// 리워드 교환
function redeemCareReward(rewardId) {
    const reward = CARE_REWARD_CATALOG.find(r => r.id === rewardId);
    if (!reward) return;
    const balance = _coinGetBalance();
    const owned = _coinGetOwned();

    if (reward.kind === '프로필 꾸미기' && owned.includes(rewardId)) {
        if (typeof showToast === 'function') showToast('이미 보유한 아이템이에요 ✨');
        return;
    }
    if (balance < reward.cost) {
        if (typeof showToast === 'function') showToast(`코인이 부족해요. ${reward.cost - balance}코인 더 모아보세요! 🪙`);
        return;
    }
    _coinSetBalance(balance - reward.cost);
    owned.push(rewardId);
    _coinSaveOwned(owned);
    if (typeof showToast === 'function') showToast(`${reward.emoji} ${reward.name} 교환 완료! 🎉`);
    renderCareCoinShop();
}

function renderCareCoinShop() {
    const el = document.getElementById('care-coin-shop');
    if (!el) return;
    syncCareCoins();
    const balance = _coinGetBalance();
    const owned = _coinGetOwned();

    const items = CARE_REWARD_CATALOG.map(r => {
        const isOwned = r.kind === '프로필 꾸미기' && owned.includes(r.id);
        const canAfford = balance >= r.cost;
        const btn = isOwned
            ? `<span class="text-[9px] font-black text-emerald-600 px-2 py-1">보유 중 ✅</span>`
            : `<button onclick="redeemCareReward('${r.id}')" class="text-[9px] font-black px-2 py-1 rounded-lg whitespace-nowrap transition-colors ${canAfford ? 'bg-brand-500 hover:bg-brand-600 text-white' : 'bg-gray-200 text-gray-400'}">🪙 ${r.cost}</button>`;
        return `
        <div class="flex items-center gap-2 p-2 rounded-xl bg-gray-50 border border-gray-100">
            <span class="text-base shrink-0">${r.emoji}</span>
            <div class="flex-1 min-w-0">
                <p class="text-[10px] font-black text-gray-700 truncate">${r.name}</p>
                <p class="text-[8px] text-gray-400 font-medium">${r.kind}</p>
            </div>
            ${btn}
        </div>`;
    }).join('');

    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">🪙 케어 코인 상점</span>
            <span class="streak-badge shrink-0"><i class="fa-solid fa-coins"></i> ${balance.toLocaleString()}</span>
        </div>
        <p class="text-[9px] text-gray-400 font-medium mb-2">출석·산책·투약·건강기록으로 코인을 모아 교환하세요!</p>
        <div class="space-y-1.5">${items}</div>`;
}

window.syncCareCoins = syncCareCoins;
window.redeemCareReward = redeemCareReward;
window.renderCareCoinShop = renderCareCoinShop;
