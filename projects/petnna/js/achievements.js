// achievements.js — 업적 시스템 + 일일 챌린지

// ─── 업적 정의 ─────────────────────────────────────────────────────
const ACHIEVEMENTS = [
    { id: 'first_pet',      emoji: '🐾', name: '첫 만남',        desc: '반려동물을 처음 등록했어요',   check: () => typeof pets !== 'undefined' && pets.length > 0 },
    { id: 'first_walk',     emoji: '🗺️', name: '첫 산책',        desc: '첫 번째 산책을 기록했어요',    check: () => typeof walks !== 'undefined' && walks.length > 0 },
    { id: 'first_ai',       emoji: '🏥', name: 'AI 닥터',        desc: 'AI 건강 분석을 처음 받았어요', check: () => { const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : []; return a.length > 0; } },
    { id: 'first_saju',     emoji: '🔯', name: '운명의 실',       desc: '사주 궁합 분석을 완료했어요',  check: () => typeof pets !== 'undefined' && pets.length > 0 && !!pets[0]?.sajuData },
    { id: 'first_post',     emoji: '📢', name: '이웃 자랑',       desc: '소셜 피드에 첫 글을 올렸어요', check: () => { const p = typeof posts !== 'undefined' ? posts : []; return p.some(post => post.petName !== '초코'); } },
    { id: 'streak_7',       emoji: '🔥', name: '7일 집사',        desc: '7일 연속 건강 기록 달성!',     check: () => typeof calcHealthStreak === 'function' && calcHealthStreak() >= 7 },
    { id: 'streak_30',      emoji: '🔥🔥', name: '30일 전설',    desc: '30일 연속 건강 기록 달성!',    check: () => typeof calcHealthStreak === 'function' && calcHealthStreak() >= 30 },
    { id: 'ai_10',          emoji: '🔬', name: 'AI 연구원',       desc: 'AI 건강 분석 10회 완료',       check: () => { const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : []; return a.length >= 10; } },
    { id: 'walk_10',        emoji: '🏃', name: '산책왕',          desc: '산책 10회 기록 달성',           check: () => typeof walks !== 'undefined' && walks.length >= 10 },
    { id: 'premium',        emoji: '👑', name: '프리미엄 집사',   desc: '펫과나 프리미엄 구독 중',       check: () => typeof isPremium === 'function' && isPremium() },
    { id: 'voice_first',    emoji: '🎙️', name: '음성 문진',      desc: '음성으로 증상을 처음 분석했어요', check: () => !!localStorage.getItem('petna_voice_used') },
    { id: 'share_saju',     emoji: '✨', name: '사주 공유',       desc: '사주 결과 카드를 공유했어요',  check: () => !!localStorage.getItem('petna_saju_shared') },
];

function getUnlockedAchievements() {
    return ACHIEVEMENTS.filter(a => {
        try { return a.check(); } catch { return false; }
    });
}

function checkNewAchievements() {
    const shownKey = 'petna_shown_achievements';
    const shown = JSON.parse(localStorage.getItem(shownKey) || '[]');
    const unlocked = getUnlockedAchievements();
    const newOnes = unlocked.filter(a => !shown.includes(a.id));
    if (newOnes.length > 0) {
        newOnes.forEach((a, i) => {
            setTimeout(() => {
                if (typeof showToast === 'function') showToast(`🏅 업적 달성: ${a.emoji} ${a.name}!`);
            }, i * 1500);
        });
        localStorage.setItem(shownKey, JSON.stringify([...shown, ...newOnes.map(a => a.id)]));
    }
    return unlocked.length;
}

function showAchievementDetail(achievementId) {
    const achievement = ACHIEVEMENTS.find(a => a.id === achievementId);
    if (!achievement) return;

    const unlocked = getUnlockedAchievements();
    const isUnlocked = unlocked.some(u => u.id === achievementId);

    if (typeof showCustomDialog === 'function') {
        showCustomDialog({
            title: `${achievement.emoji} ${achievement.name}`,
            message: `
                <div class="text-center space-y-3">
                    <div class="text-5xl mb-3">${achievement.emoji}</div>
                    <div class="text-sm font-bold text-gray-700">${achievement.name}</div>
                    <div class="text-xs text-gray-500 leading-relaxed">${achievement.desc}</div>
                    <div class="mt-4 pt-3 border-t border-gray-100">
                        <span class="inline-block px-3 py-1 rounded-full text-xs font-bold ${
                            isUnlocked
                                ? 'bg-amber-100 text-amber-600'
                                : 'bg-gray-100 text-gray-400'
                        }">
                            ${isUnlocked ? '✅ 달성 완료!' : '🔒 미달성'}
                        </span>
                    </div>
                </div>
            `,
            type: 'info',
            allowHtml: true
        });
    }
}

function renderAchievementBadges() {
    const el = document.getElementById('achievement-badges');
    if (!el) return;
    const unlocked = getUnlockedAchievements();
    const total = ACHIEVEMENTS.length;
    if (unlocked.length === 0) {
        el.innerHTML = `<p class="text-[10px] text-gray-400 text-center py-2">아직 달성한 업적이 없어요. 첫 산책·건강기록을 시작해보세요!</p>`;
        return;
    }
    const badges = ACHIEVEMENTS.map(a => {
        const done = unlocked.some(u => u.id === a.id);
        return `<div class="flex flex-col items-center gap-1 cursor-pointer hover:scale-110 transition-transform ${done ? '' : 'opacity-30'}"
                     onclick="showAchievementDetail('${a.id}')"
                     title="${a.name}: ${a.desc}">
            <span class="text-xl">${a.emoji}</span>
            <span class="text-[8px] font-black text-center leading-tight ${done ? 'text-gray-700' : 'text-gray-400'}">${a.name}</span>
        </div>`;
    }).join('');
    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">🏅 업적 (${unlocked.length}/${total})</span>
            <div class="flex-1 mx-2 bg-gray-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-amber-400 h-full rounded-full transition-all" style="width:${Math.round(unlocked.length/total*100)}%"></div>
            </div>
            <span class="text-[10px] font-bold text-amber-500">${Math.round(unlocked.length/total*100)}%</span>
        </div>
        <div class="grid grid-cols-6 gap-2">${badges}</div>`;
}

// ─── 일일 챌린지 ────────────────────────────────────────────────────
function getDailyChallenges() {
    const today = new Date().toISOString().split('T')[0];
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const todayEntry = history.find(h => h.date === today) || {};
    const hasAiToday = (() => {
        const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
        return a.length > 0 && a[0].analyzedAt && a[0].analyzedAt.startsWith(today);
    })();
    const hasWalkToday = typeof walks !== 'undefined' && walks.some(w => w.date === today || (w.date && w.date.includes('오늘')));
    const hasPostToday = (() => {
        const done = localStorage.getItem('petna_daily_post_' + today);
        return !!done;
    })();

    return [
        {
            id: 'health_log',
            emoji: '📝',
            label: '건강 기록 완료',
            desc: '식사·음수·배변 중 하나라도 기록',
            done: todayEntry.food > 0 || todayEntry.water > 0 || todayEntry.poop !== null,
            action: () => { if (typeof switchTab === 'function') switchTab('mypet'); },
            actionLabel: '기록하기'
        },
        {
            id: 'ai_analysis',
            emoji: '🏥',
            label: 'AI 건강 분석',
            desc: '오늘 사진으로 AI 건강 분석',
            done: hasAiToday,
            action: () => { if (typeof triggerAiHealthAnalysis === 'function') triggerAiHealthAnalysis(); },
            actionLabel: '분석하기'
        },
        {
            id: 'walk',
            emoji: '🗺️',
            label: '산책 기록',
            desc: '오늘 산책을 기록해요',
            done: hasWalkToday,
            action: () => { if (typeof switchTab === 'function') switchTab('walk'); },
            actionLabel: '산책하기'
        },
    ];
}

function renderDailyChallenges() {
    const el = document.getElementById('daily-challenges');
    if (!el) return;
    const challenges = getDailyChallenges();
    const doneCount = challenges.filter(c => c.done).length;
    const progressPct = Math.round(doneCount / challenges.length * 100);

    const items = challenges.map(c => `
        <div class="flex items-center gap-2.5 p-2.5 rounded-xl ${c.done ? 'bg-emerald-50 border border-emerald-100' : 'bg-gray-50 border border-gray-100'}">
            <span class="text-lg">${c.done ? '✅' : c.emoji}</span>
            <div class="flex-1 min-w-0">
                <p class="text-[11px] font-black ${c.done ? 'text-emerald-700 line-through' : 'text-gray-700'}">${c.label}</p>
                <p class="text-[9px] text-gray-400 font-medium truncate">${c.desc}</p>
            </div>
            ${!c.done ? `<button onclick="(${c.action.toString()})()" class="text-[9px] font-black px-2 py-1 bg-brand-500 text-white rounded-lg whitespace-nowrap flex-shrink-0">${c.actionLabel}</button>` : ''}
        </div>`).join('');

    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">⚡ 오늘의 챌린지</span>
            <span class="text-[10px] font-bold ${doneCount === challenges.length ? 'text-emerald-500' : 'text-gray-400'}">${doneCount}/${challenges.length} 완료</span>
        </div>
        <div class="w-full bg-gray-100 rounded-full h-1.5 mb-2.5 overflow-hidden">
            <div class="bg-gradient-to-r from-brand-400 to-amber-400 h-full rounded-full transition-all duration-500" style="width:${progressPct}%"></div>
        </div>
        <div class="space-y-1.5">${items}</div>
        ${doneCount === challenges.length ? `<p class="text-[10px] text-emerald-600 font-black text-center mt-2">🎉 오늘 챌린지 모두 완료! 내일도 화이팅!</p>` : ''}`;
}
