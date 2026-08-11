// photo-contest.js — 월간 포토 콘테스트 + 투표 (나무 기획, P2)
// 기존 posts 데이터와 좋아요(likes)를 그대로 재사용한다. 신규 서버 테이블/스키마 변경 없음.
// - 매월 테마를 정하고, 그 달에 올라온 "사진 게시글" 중 테마 태그가 붙은 글을 수집한다.
// - 좋아요 수를 투표로 집계해 실시간 순위를 산정한다(renderFeed가 좋아요 토글 시 재호출).
// - 지난달이 끝나면 1위 게시글의 반려동물이 내 활성 펫이면 우승 보상(care_coins)을 멱등 지급한다.

const PHOTO_CONTEST = {
    PRIZE_COINS: 100,
    ENTRY_TAG: '#포토콘테스트',
    AWARDED_KEY: 'petna_contest_awarded', // 이미 보상 지급한 'YYYY-MM' 목록(중복 지급 방지)
    // 월(1~12)별 테마. 매년 반복 사용한다.
    THEMES: [
        { emoji: '❄️', title: '겨울 산책', keywords: ['겨울', '눈', '산책'] },
        { emoji: '😴', title: '단잠 자는 모습', keywords: ['낮잠', '단잠', '자는'] },
        { emoji: '🌸', title: '봄나들이', keywords: ['봄', '나들이', '꽃'] },
        { emoji: '🌷', title: '꽃과 함께', keywords: ['꽃', '봄'] },
        { emoji: '📸', title: '우리 가족 사진', keywords: ['가족', '함께'] },
        { emoji: '💦', title: '시원한 물놀이', keywords: ['물놀이', '수영', '계곡', '바다'] },
        { emoji: '🍧', title: '여름 간식 타임', keywords: ['간식', '아이스', '여름'] },
        { emoji: '🌳', title: '시원한 그늘', keywords: ['그늘', '나무', '여름'] },
        { emoji: '🍂', title: '가을 산책', keywords: ['가을', '단풍', '낙엽', '산책'] },
        { emoji: '🎃', title: '할로윈 코스튬', keywords: ['할로윈', '코스튬', '분장'] },
        { emoji: '🧦', title: '포근한 실내', keywords: ['실내', '포근', '이불'] },
        { emoji: '🎄', title: '크리스마스', keywords: ['크리스마스', '산타', '트리'] },
    ],
};

function _contestMonthKey(date) {
    const d = date instanceof Date ? date : new Date(date);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

// post.id는 발행 시각(Date.now(), ms)이다. 유효한 타임스탬프일 때만 월 키를 돌려준다.
function _contestPostMonthKey(post) {
    const ms = Number(post && post.id);
    if (!Number.isFinite(ms) || ms < 1577836800000 || ms > Date.now() + 86400000) return null;
    return _contestMonthKey(new Date(ms));
}

function _contestThemeFor(date) {
    const d = date instanceof Date ? date : new Date(date);
    return PHOTO_CONTEST.THEMES[d.getMonth()];
}

function _contestMatchesTheme(post, theme) {
    if (!post || !post.image) return false;
    const text = (post.content || '').toLowerCase();
    if (text.includes(PHOTO_CONTEST.ENTRY_TAG)) return true;
    return theme.keywords.some(kw => text.includes(kw.toLowerCase()));
}

// 특정 월(YYYY-MM)의 콘테스트 참가작을 좋아요(투표) 내림차순으로 반환.
function getContestEntries(monthKey) {
    if (typeof posts === 'undefined' || !Array.isArray(posts)) return [];
    const [y, m] = monthKey.split('-').map(Number);
    const theme = PHOTO_CONTEST.THEMES[m - 1];
    if (!theme) return [];
    return posts
        .filter(p => _contestPostMonthKey(p) === monthKey && _contestMatchesTheme(p, theme))
        .sort((a, b) => (b.likes || 0) - (a.likes || 0));
}

function _contestGetAwarded() {
    try { return new Set(JSON.parse(localStorage.getItem(PHOTO_CONTEST.AWARDED_KEY) || '[]')); }
    catch { return new Set(); }
}
function _contestSaveAwarded(set) {
    try { localStorage.setItem(PHOTO_CONTEST.AWARDED_KEY, JSON.stringify([...set])); } catch {}
}

// 지난달 우승자가 내 활성 펫이면 보상을 1회만 지급한다(멱등).
function settleLastMonthContest() {
    const now = new Date();
    const lastMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const monthKey = _contestMonthKey(lastMonth);

    const awarded = _contestGetAwarded();
    if (awarded.has(monthKey)) return;

    const entries = getContestEntries(monthKey);
    if (entries.length === 0) return;
    const winner = entries[0];

    // 표(좋아요)가 0이면 우승 미확정으로 보고 지급하지 않는다.
    if ((winner.likes || 0) <= 0) return;

    let myPetName = null;
    try { const p = typeof getActivePet === 'function' ? getActivePet() : null; myPetName = p ? p.name : null; } catch {}

    // 우승자를 정산 완료로 기록(내 펫이 아니어도 재확인 반복을 막는다).
    awarded.add(monthKey);
    _contestSaveAwarded(awarded);

    if (myPetName && winner.petName === myPetName && typeof window.awardCareCoins === 'function') {
        window.awardCareCoins(PHOTO_CONTEST.PRIZE_COINS);
        if (typeof showToast === 'function') {
            showToast(`🏆 지난달 포토 콘테스트 우승! 🪙 ${PHOTO_CONTEST.PRIZE_COINS}코인 지급`);
        }
        if (typeof renderCareCoinShop === 'function') renderCareCoinShop();
    }
}

function renderPhotoContestCard() {
    const el = document.getElementById('photo-contest-card');
    if (!el) return;

    settleLastMonthContest();

    const now = new Date();
    const theme = _contestThemeFor(now);
    const monthKey = _contestMonthKey(now);
    const entries = getContestEntries(monthKey);
    const rankEmoji = ['🥇', '🥈', '🥉'];

    const list = entries.length === 0
        ? `<p class="text-[11px] text-gray-400 font-medium text-center py-3">
               아직 참가작이 없어요. 사진에 <span class="font-black text-brand-600">${PHOTO_CONTEST.ENTRY_TAG}</span> 태그를 달아 참가해보세요!
           </p>`
        : `<div class="space-y-1.5">` + entries.slice(0, 3).map((p, i) => `
            <div class="flex items-center gap-2 p-1.5 rounded-xl bg-white/70 border border-amber-100">
                <span class="text-base shrink-0 w-6 text-center">${rankEmoji[i] || (i + 1)}</span>
                <img loading="lazy" src="${p.image}" class="w-9 h-9 rounded-lg object-cover shrink-0">
                <span class="flex-1 min-w-0 text-[11px] font-black text-gray-700 truncate">${escapeHtml(p.petName || '')}</span>
                <span class="shrink-0 text-[11px] font-black text-rose-500"><i class="fa-solid fa-heart"></i> ${p.likes || 0}</span>
            </div>`).join('') + `</div>`;

    el.innerHTML = `
        <div class="bg-gradient-to-r from-brand-50 to-amber-50 border border-brand-200/50 rounded-2xl p-3">
            <div class="flex items-center justify-between mb-1">
                <span class="text-[12px] font-black text-brand-700">📷 이달의 포토 콘테스트</span>
                <span class="text-[9px] font-black text-white bg-brand-500 px-2 py-0.5 rounded-full shrink-0">🪙 우승 ${PHOTO_CONTEST.PRIZE_COINS}</span>
            </div>
            <p class="text-[10px] text-gray-500 font-medium mb-2">이달의 테마 · ${theme.emoji} <span class="font-bold text-gray-700">${escapeHtml(theme.title)}</span> — 좋아요가 곧 투표예요!</p>
            ${list}
        </div>`;
}

window.getContestEntries = getContestEntries;
window.settleLastMonthContest = settleLastMonthContest;
window.renderPhotoContestCard = renderPhotoContestCard;
