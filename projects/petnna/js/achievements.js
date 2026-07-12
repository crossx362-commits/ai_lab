// achievements.js — 업적 시스템 + 일일 챌린지
// ── 📊 펄스 전략가 패치: Woofz Training Passport + Duolingo Streak 벤치마크 ──

// ── 산책 연속일(streak) 계산 헬퍼 ─────────────────────────────────────────
function calcWalkStreak() {
    if (typeof walks === 'undefined' || walks.length === 0) return 0;
    const savedDates = walks
        .map(w => w.savedAt ? w.savedAt.split('T')[0] : null)
        .filter(Boolean)
        .sort()
        .reverse();
    if (savedDates.length === 0) return 0;

    const today = new Date().toISOString().split('T')[0];
    const mostRecent = savedDates[0];
    // 오늘 또는 어제 산책이 없으면 streak 0
    const dayDiff = (new Date(today) - new Date(mostRecent)) / (1000*60*60*24);
    if (dayDiff > 1) return 0;

    let streak = 1;
    let checkDate = new Date(mostRecent);
    checkDate.setDate(checkDate.getDate() - 1);
    for (let i = 0; i < 365; i++) {
        const ds = checkDate.toISOString().split('T')[0];
        if (savedDates.includes(ds)) {
            streak++;
            checkDate.setDate(checkDate.getDate() - 1);
        } else break;
    }
    return streak;
}

// ─── 업적 정의 ─────────────────────────────────────────────────────
const ACHIEVEMENTS = [
    { id: 'first_pet',        emoji: '🐾',    name: '첫 만남',        desc: '반려동물을 처음 등록했어요',         check: () => typeof pets !== 'undefined' && pets.length > 0 },
    { id: 'first_walk',       emoji: '🗺️',   name: '첫 산책',        desc: '첫 번째 산책을 기록했어요',           check: () => typeof walks !== 'undefined' && walks.length > 0 },
    { id: 'first_ai',         emoji: '🏥',    name: 'AI 닥터',        desc: 'AI 건강 분석을 처음 받았어요',        check: () => { const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : []; return a.length > 0; } },
    { id: 'first_saju',       emoji: '🔯',    name: '운명의 실',      desc: '사주 조화도 분석을 완료했어요',          check: () => typeof pets !== 'undefined' && pets.length > 0 && !!pets[0]?.sajuData },
    { id: 'first_post',       emoji: '📢',    name: '이웃 자랑',      desc: '소셜 피드에 첫 글을 올렸어요',         check: () => { const p = typeof posts !== 'undefined' ? posts : []; return p.some(post => post.petName !== '초코'); } },
    { id: 'streak_7',         emoji: '🔥',    name: '7일 집사',       desc: '7일 연속 건강 기록 달성!',            check: () => typeof calcHealthStreak === 'function' && calcHealthStreak() >= 7 },
    { id: 'streak_30',        emoji: '🔥🔥',  name: '30일 전설',      desc: '30일 연속 건강 기록 달성!',           check: () => typeof calcHealthStreak === 'function' && calcHealthStreak() >= 30 },
    { id: 'ai_10',            emoji: '🔬',    name: 'AI 연구원',      desc: 'AI 건강 분석 10회 완료',              check: () => { const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : []; return a.length >= 10; } },
    { id: 'walk_10',          emoji: '🏃',    name: '산책왕',         desc: '산책 10회 기록 달성',                 check: () => typeof walks !== 'undefined' && walks.length >= 10 },
    { id: 'premium',          emoji: '👑',    name: '프리미엄 집사',  desc: '펫과나 프리미엄 구독 중',              check: () => typeof isPremium === 'function' && isPremium() },
    { id: 'voice_first',      emoji: '🎙️',   name: '음성 문진',      desc: '음성으로 증상을 처음 분석했어요',      check: () => !!localStorage.getItem('petna_voice_used') },
    { id: 'share_saju',       emoji: '✨',    name: '사주 공유',      desc: '사주 결과 카드를 공유했어요',          check: () => !!localStorage.getItem('petna_saju_shared') },
    // ── 📊 펄스 추가: 산책 연속 streak 뱃지 (Duolingo/Woofz 스타일) ──
    { id: 'walk_streak_3',    emoji: '🏅',    name: '3일 산책러',     desc: '3일 연속 산책을 기록했어요!',          check: () => calcWalkStreak() >= 3 },
    { id: 'walk_streak_7',    emoji: '🥇',    name: '주간 산책왕',    desc: '7일 연속 산책 달성! 최고예요!',        check: () => calcWalkStreak() >= 7 },
    { id: 'walk_streak_14',   emoji: '🏆',    name: '2주 챔피언',     desc: '14일 연속 산책! 놀라운 의지력!',       check: () => calcWalkStreak() >= 14 },
    { id: 'walk_streak_30',   emoji: '💎',    name: '30일 산책 전설', desc: '30일 연속 산책! 진정한 집사의 길!',   check: () => calcWalkStreak() >= 30 },
    // ── 주간 산책 챌린지 달성 뱃지 (개인 목표 링) ──
    { id: 'weekly_goal',      emoji: '🎯',    name: '주간 목표 달성', desc: '이번 주 산책 목표를 달성했어요!',      check: () => hasMetWeeklyWalkGoal() },
];

function getUnlockedAchievements() {
    return ACHIEVEMENTS.filter(a => {
        try { return a.check(); } catch { return false; }
    });
}

function checkNewAchievements() {
    // 미인증 상태에서는 업적 토스트를 띄우지 않는다(로그인 전 맥락 붕괴 방지)
    if (localStorage.getItem('petna_is_logged_in') !== 'true') return 0;
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
        <div class="flex items-center justify-between mb-1.5">
            <span class="text-[10px] font-black text-gray-700">🏅 업적 (${unlocked.length}/${total})</span>
            <div class="flex-1 mx-2 bg-gray-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-amber-400 h-full rounded-full transition-all" style="width:${Math.round(unlocked.length/total*100)}%"></div>
            </div>
            <span class="text-[9px] font-bold text-amber-500">${Math.round(unlocked.length/total*100)}%</span>
        </div>
        <div class="grid grid-cols-6 gap-1.5">${badges}</div>`;
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
            action: 'openHealthLogModal',
            actionLabel: '기록하기'
        },
        {
            id: 'ai_analysis',
            emoji: '🏥',
            label: 'AI 건강 분석',
            desc: '오늘 사진으로 AI 건강 분석',
            done: hasAiToday,
            action: 'triggerAiHealthAnalysis',
            actionLabel: '분석하기'
        },
        {
            id: 'walk',
            emoji: '🗺️',
            label: '산책 기록',
            desc: '오늘 산책을 기록해요',
            done: hasWalkToday,
            action: "switchTab('walk')",
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

    const items = challenges.map(c => {
        // 준비 중인 기능 체크
        const notReady = c.id === 'health_log' || c.id === 'ai_analysis';
        const buttonAction = notReady
            ? `showToast('🚧 준비 중인 기능입니다')`
            : (c.action.includes('(') ? c.action : c.action + '()');

        return `
        <div class="flex items-center gap-2 p-2 rounded-xl ${c.done ? 'bg-emerald-50 border border-emerald-100' : 'bg-gray-50 border border-gray-100'}">
            <span class="text-base">${c.done ? '✅' : c.emoji}</span>
            <div class="flex-1 min-w-0">
                <p class="text-[10px] font-black ${c.done ? 'text-emerald-700 line-through' : 'text-gray-700'}">${c.label}</p>
                <p class="text-[8px] text-gray-400 font-medium truncate">${c.desc}</p>
            </div>
            ${!c.done ? `<button onclick="${buttonAction}" class="text-[9px] font-black px-2 py-1 ${notReady ? 'bg-gray-400' : 'bg-brand-500 hover:bg-brand-600'} text-white rounded-lg whitespace-nowrap flex-shrink-0 transition-colors">${c.actionLabel}</button>` : ''}
        </div>`;
    }).join('');

    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">⚡ 오늘의 챌린지</span>
            <span class="text-[10px] font-bold ${doneCount === challenges.length ? 'text-emerald-500' : 'text-gray-400'}">${doneCount}/${challenges.length} 완료</span>
        </div>
        <div class="w-full bg-gray-100 rounded-full h-1.5 mb-2.5 overflow-hidden">
            <div class="bg-gradient-to-r from-brand-400 to-amber-400 h-full rounded-full transition-all duration-500" style="width:${progressPct}%"></div>
        </div>
        <div class="space-y-1.5">${items}</div>
        ${doneCount === challenges.length ? '<p class="text-[10px] text-emerald-600 font-black text-center mt-2">🎉 오늘 챌린지 모두 완료! 내일도 화이팅!</p>' : ''}`;
}

// ── 📊 펄스: 산책 Streak 배너 렌더링 (Duolingo 스타일) ─────────────────────
function renderWalkStreakBanner() {
    const el = document.getElementById('walk-streak-banner');
    if (!el) return;
    const streak = calcWalkStreak();
    if (streak < 2) {
        el.innerHTML = `
            <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                <i class="fa-solid fa-fire text-gray-300"></i>
                <span>산책을 시작하면 streak이 쌓여요!</span>
            </div>`;
        return;
    }
    const milestones = [3, 7, 14, 30, 60, 100];
    const nextGoal = milestones.find(m => m > streak) || streak + 10;
    const pct = Math.min(100, Math.round((streak / nextGoal) * 100));

    el.innerHTML = `
        <div class="flex items-center justify-between gap-3">
            <div class="flex items-center gap-2">
                <span class="streak-badge"><i class="fa-solid fa-fire"></i> ${streak}일 연속</span>
                <div>
                    <p class="text-[11px] font-black text-gray-700">산책 연속 기록 중!</p>
                    <p class="text-[9px] text-gray-400 font-medium">목표 ${nextGoal}일까지 ${nextGoal - streak}일 남음</p>
                </div>
            </div>
            <div class="text-2xl">${streak >= 30 ? '💎' : streak >= 14 ? '🏆' : streak >= 7 ? '🥇' : '🏅'}</div>
        </div>
        <div class="mt-2 w-full bg-gray-100 rounded-full h-1.5 overflow-hidden">
            <div class="bg-gradient-to-r from-orange-400 to-brand-500 h-full rounded-full transition-all duration-700"
                 style="width:${pct}%"></div>
        </div>`;
}

// ── 🎯 주간 산책 챌린지 (개인 목표 링) ────────────────────────────────────
// walk streak 위에 이번 주 목표(거리·횟수)를 설정하고 진행 링으로 보여준다.
// 목표 달성 시 '주간 목표 달성' 뱃지를 부여. 주 시작은 월요일 기준.
const WEEKLY_WALK_GOAL_DEFAULT = { distance: 5, count: 5 };

function _weekStartStr(d = new Date()) {
    const dt = new Date(d);
    const day = (dt.getDay() + 6) % 7; // 월=0 ... 일=6
    dt.setDate(dt.getDate() - day);
    return dt.toISOString().split('T')[0];
}

function getWeeklyWalkGoal() {
    try {
        const raw = localStorage.getItem(AppConstants.StorageKeys.WEEKLY_WALK_GOAL);
        if (!raw) return { ...WEEKLY_WALK_GOAL_DEFAULT };
        const g = JSON.parse(raw);
        return {
            distance: Number(g.distance) > 0 ? Number(g.distance) : WEEKLY_WALK_GOAL_DEFAULT.distance,
            count: Number(g.count) > 0 ? Math.round(Number(g.count)) : WEEKLY_WALK_GOAL_DEFAULT.count,
        };
    } catch { return { ...WEEKLY_WALK_GOAL_DEFAULT }; }
}

function saveWeeklyWalkGoal(goal) {
    try { localStorage.setItem(AppConstants.StorageKeys.WEEKLY_WALK_GOAL, JSON.stringify(goal)); } catch {}
}

// 이번 주 산책 집계 (월요일 시작, streak 배너와 동일하게 savedAt 기준)
function getWeeklyWalkProgress() {
    const start = _weekStartStr();
    const weekWalks = (typeof walks !== 'undefined' ? walks : []).filter(w => {
        const d = w.savedAt ? w.savedAt.split('T')[0] : null;
        return d && d >= start;
    });
    const distance = weekWalks.reduce((s, w) => s + (parseFloat(w.distance) || 0), 0);
    return { count: weekWalks.length, distance };
}

// 목표 달성한 주(週)를 기록 — 뱃지는 한 번 달성하면 영구 유지
function _getMetWeeks() {
    try { return JSON.parse(localStorage.getItem(AppConstants.StorageKeys.WEEKLY_WALK_GOAL_MET) || '[]'); }
    catch { return []; }
}

function hasMetWeeklyWalkGoal() {
    return _getMetWeeks().length > 0;
}

// 이번 주 목표 달성 시 주 키를 기록(중복 방지)
function _recordWeeklyGoalIfMet() {
    const goal = getWeeklyWalkGoal();
    const prog = getWeeklyWalkProgress();
    if (prog.distance >= goal.distance && prog.count >= goal.count) {
        const wk = _weekStartStr();
        const met = _getMetWeeks();
        if (!met.includes(wk)) {
            met.push(wk);
            try { localStorage.setItem(AppConstants.StorageKeys.WEEKLY_WALK_GOAL_MET, JSON.stringify(met)); } catch {}
        }
    }
}

// 목표 설정: 거리 → 횟수 순으로 입력
function setWeeklyWalkGoal() {
    const cur = getWeeklyWalkGoal();
    if (typeof showCustomDialog !== 'function') return;
    showCustomDialog({
        title: '주간 목표 — 거리 🎯',
        message: '이번 주 산책 목표 거리를 입력하세요 (km)',
        icon: '📏',
        type: 'prompt',
        val: String(cur.distance),
        placeholder: '예: 5',
        onConfirm: (distRaw) => {
            const distance = Math.max(0.1, parseFloat(distRaw) || cur.distance);
            showCustomDialog({
                title: '주간 목표 — 횟수 🎯',
                message: '이번 주 산책 목표 횟수를 입력하세요 (회)',
                icon: '🔁',
                type: 'prompt',
                val: String(cur.count),
                placeholder: '예: 5',
                onConfirm: (cntRaw) => {
                    const count = Math.max(1, Math.round(parseInt(cntRaw) || cur.count));
                    saveWeeklyWalkGoal({ distance, count });
                    if (typeof showToast === 'function') showToast(`이번 주 목표: ${distance}km · ${count}회 🎯`);
                    renderWeeklyWalkChallenge();
                }
            });
        }
    });
}

function renderWeeklyWalkChallenge() {
    const el = document.getElementById('weekly-walk-challenge');
    if (!el) return;
    _recordWeeklyGoalIfMet();

    const goal = getWeeklyWalkGoal();
    const prog = getWeeklyWalkProgress();
    const distPct = Math.min(1, prog.distance / goal.distance);
    const countPct = Math.min(1, prog.count / goal.count);
    const overall = Math.round(((distPct + countPct) / 2) * 100);
    const done = prog.distance >= goal.distance && prog.count >= goal.count;

    // SVG 진행 링
    const r = 26, c = 2 * Math.PI * r;
    const offset = c * (1 - overall / 100);

    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">🎯 주간 산책 챌린지</span>
            <button onclick="setWeeklyWalkGoal()" class="text-[9px] font-bold text-brand-500 hover:text-brand-600">목표 설정</button>
        </div>
        <div class="flex items-center gap-3">
            <div class="relative shrink-0" style="width:64px;height:64px;">
                <svg width="64" height="64" viewBox="0 0 64 64" class="-rotate-90">
                    <circle cx="32" cy="32" r="${r}" fill="none" stroke="#f1e4d6" stroke-width="7"></circle>
                    <circle cx="32" cy="32" r="${r}" fill="none" stroke="${done ? '#f59e0b' : '#ea6d3a'}" stroke-width="7"
                        stroke-linecap="round" stroke-dasharray="${c.toFixed(1)}" stroke-dashoffset="${offset.toFixed(1)}"
                        style="transition:stroke-dashoffset 0.7s ease;"></circle>
                </svg>
                <div class="absolute inset-0 flex items-center justify-center">
                    <span class="text-[13px] font-black ${done ? 'text-amber-500' : 'text-brand-600'}">${done ? '🎉' : overall + '%'}</span>
                </div>
            </div>
            <div class="flex-1 min-w-0 space-y-1">
                <div>
                    <div class="flex items-center justify-between text-[10px] font-bold">
                        <span class="text-gray-500">📏 거리</span>
                        <span class="${distPct >= 1 ? 'text-amber-600' : 'text-gray-700'}">${prog.distance.toFixed(1)} / ${goal.distance}km</span>
                    </div>
                    <div class="w-full bg-gray-100 rounded-full h-1 overflow-hidden mt-0.5">
                        <div class="bg-brand-500 h-full rounded-full transition-all duration-700" style="width:${Math.round(distPct * 100)}%"></div>
                    </div>
                </div>
                <div>
                    <div class="flex items-center justify-between text-[10px] font-bold">
                        <span class="text-gray-500">🔁 횟수</span>
                        <span class="${countPct >= 1 ? 'text-amber-600' : 'text-gray-700'}">${prog.count} / ${goal.count}회</span>
                    </div>
                    <div class="w-full bg-gray-100 rounded-full h-1 overflow-hidden mt-0.5">
                        <div class="bg-amber-400 h-full rounded-full transition-all duration-700" style="width:${Math.round(countPct * 100)}%"></div>
                    </div>
                </div>
            </div>
        </div>
        ${done ? '<p class="text-[10px] text-amber-600 font-black text-center mt-2">🎉 이번 주 목표 달성! 🎯 뱃지를 획득했어요!</p>' : ''}`;
}

// ── 🏆 주간 케어 챌린지 (매주 회전하는 목표) ───────────────────────────────
// 주(週)마다 하나의 케어 목표가 자동 회전한다(산책·건강기록·AI케어). 진행바로
// 달성률을 보여주고, 달성 시 그 주 한 번만 축하 애니메이션을 재생한다.
// 데이터는 기존 walks / healthLogs / getHealthAnalyses()를 재사용(신규 저장 없음).
const WEEKLY_CARE_GOALS = [
    {
        id: 'walk',
        emoji: '🗺️',
        title: '산책 챌린지',
        unit: '회',
        target: 3,
        desc: '이번 주 산책 3회를 채워요',
        action: "switchTab('walk')",
        actionLabel: '산책하기',
        progress: () => (typeof getWeeklyWalkProgress === 'function' ? getWeeklyWalkProgress().count : 0),
    },
    {
        id: 'health_log',
        emoji: '📝',
        title: '건강 기록 챌린지',
        unit: '일',
        target: 4,
        desc: '이번 주 4일 건강을 기록해요',
        action: 'openHealthLogModal',
        actionLabel: '기록하기',
        progress: () => {
            const start = _weekStartStr();
            const hist = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
            return hist.filter(h => h.date && h.date >= start &&
                (h.food > 0 || h.water > 0 || (h.poop !== null && h.poop !== undefined))).length;
        },
    },
    {
        id: 'ai_care',
        emoji: '🏥',
        title: 'AI 건강 케어',
        unit: '회',
        target: 2,
        desc: '이번 주 AI 건강 분석 2회',
        action: "switchTab('health')",
        actionLabel: '분석하기',
        progress: () => {
            const start = _weekStartStr();
            const a = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
            return a.filter(x => x.analyzedAt && x.analyzedAt.split('T')[0] >= start).length;
        },
    },
];

// 이번 주에 해당하는 목표 하나를 결정적으로 선택(주 시작 날짜 기준 회전)
function getWeeklyCareGoal() {
    const start = _weekStartStr();
    const weekIdx = Math.floor(new Date(start).getTime() / (1000 * 60 * 60 * 24 * 7));
    return WEEKLY_CARE_GOALS[((weekIdx % WEEKLY_CARE_GOALS.length) + WEEKLY_CARE_GOALS.length) % WEEKLY_CARE_GOALS.length];
}

// 그 주 축하 애니메이션을 이미 재생했는지 (주 키 저장)
function _careCelebrated(wk) {
    try { return (JSON.parse(localStorage.getItem('petna_weekly_care_celebrated') || '[]')).includes(wk); }
    catch { return false; }
}
function _markCareCelebrated(wk) {
    try {
        const arr = JSON.parse(localStorage.getItem('petna_weekly_care_celebrated') || '[]');
        if (!arr.includes(wk)) { arr.push(wk); localStorage.setItem('petna_weekly_care_celebrated', JSON.stringify(arr)); }
    } catch {}
}

function renderWeeklyCareChallenge() {
    const el = document.getElementById('weekly-care-challenge');
    if (!el) return;
    const goal = getWeeklyCareGoal();
    const cur = Math.max(0, goal.progress());
    const pct = Math.min(100, Math.round((cur / goal.target) * 100));
    const done = cur >= goal.target;

    el.innerHTML = `
        <div class="flex items-center justify-between mb-2">
            <span class="text-[11px] font-black text-gray-700">🏆 이번 주 케어 챌린지</span>
            <span class="text-[9px] font-bold text-gray-400">매주 회전</span>
        </div>
        <div class="flex items-center gap-2">
            <span id="weekly-care-emoji" class="text-2xl ${done ? 'animate-bounce' : ''}">${goal.emoji}</span>
            <div class="flex-1 min-w-0">
                <p class="text-[11px] font-black ${done ? 'text-amber-600' : 'text-gray-700'}">${goal.title}</p>
                <p class="text-[9px] text-gray-400 font-medium truncate">${goal.desc}</p>
            </div>
            <span class="text-[11px] font-black ${done ? 'text-amber-500' : 'text-brand-600'} whitespace-nowrap">${done ? '🎉' : `${cur}/${goal.target}${goal.unit}`}</span>
        </div>
        <div class="w-full bg-gray-100 rounded-full h-1.5 mt-2 overflow-hidden">
            <div class="bg-gradient-to-r from-brand-400 to-amber-400 h-full rounded-full transition-all duration-700" style="width:${pct}%"></div>
        </div>
        ${done
            ? '<p class="text-[10px] text-amber-600 font-black text-center mt-2">🎉 이번 주 케어 챌린지 달성! 멋져요!</p>'
            : `<button onclick="${goal.action.includes('(') ? goal.action : goal.action + '()'}" class="w-full mt-2 text-[10px] font-black py-1.5 bg-brand-500 hover:bg-brand-600 text-white rounded-lg transition-colors">${goal.actionLabel}</button>`}`;

    // 달성 시 그 주 한 번만 축하 애니메이션
    if (done) {
        const wk = _weekStartStr();
        if (!_careCelebrated(wk)) {
            _markCareCelebrated(wk);
            _playCareCelebration(el);
            if (typeof showToast === 'function') showToast(`🏆 주간 케어 챌린지 달성: ${goal.title}!`);
        }
    }
}

// 위젯 위로 이모지가 튀어오르는 간단한 축하 연출(Tailwind animate 재사용, 신규 CSS 없음)
function _playCareCelebration(el) {
    const burst = document.createElement('div');
    burst.className = 'absolute inset-0 pointer-events-none flex items-center justify-center overflow-hidden';
    burst.innerHTML = ['🎉', '✨', '🏆', '🎊', '⭐'].map((e, i) =>
        `<span class="absolute text-xl animate-ping" style="left:${15 + i * 18}%;top:${20 + (i % 2) * 30}%;animation-duration:1s;">${e}</span>`
    ).join('');
    const host = el.parentElement || el;
    const prevPos = host.style.position;
    if (getComputedStyle(host).position === 'static') host.style.position = 'relative';
    host.appendChild(burst);
    setTimeout(() => { burst.remove(); if (!prevPos) host.style.position = prevPos; }, 1600);
}

// ── 🤝 버디 산책 스트릭 (함께 이어가기) ───────────────────────────────────
// 개인 산책 streak을 소셜화: 친구와 짝을 맺어 '함께 연속 산책일'을 쌓고,
// 오늘 아직 산책 전이면 서로에게 넛지(응원)를 보낼 수 있게 한다.
function getBuddyPair() {
    try {
        const raw = localStorage.getItem(AppConstants.StorageKeys.BUDDY_PAIR);
        return raw ? JSON.parse(raw) : null;
    } catch { return null; }
}

function saveBuddyPair(pair) {
    try {
        if (pair) localStorage.setItem(AppConstants.StorageKeys.BUDDY_PAIR, JSON.stringify(pair));
        else localStorage.removeItem(AppConstants.StorageKeys.BUDDY_PAIR);
    } catch {}
}

// 짝 맺기: 친구 목록에서 선택한 이웃과 버디 페어 생성
function setBuddy(friendId) {
    const list = typeof friends !== 'undefined' ? friends : [];
    const f = list.find(x => String(x.id) === String(friendId));
    if (!f) { if (typeof showToast === 'function') showToast('버디로 맺을 이웃을 선택해주세요.'); return; }
    saveBuddyPair({
        id: f.id, nickname: f.nickname, petName: f.petName, avatar: f.avatar,
        since: new Date().toISOString().split('T')[0],
    });
    if (typeof showToast === 'function') showToast(`${f.nickname}님과 버디 산책을 시작했어요! 🤝`);
    renderBuddyStreakCard();
}

function unpairBuddy() {
    saveBuddyPair(null);
    if (typeof showToast === 'function') showToast('버디 산책을 해제했어요.');
    renderBuddyStreakCard();
}

// 함께 연속 산책일 = 짝을 맺은 날 이후 내가 산책을 이어온 연속일(개인 streak을 페어 시점으로 상한)
function calcBuddyStreak() {
    const pair = getBuddyPair();
    if (!pair) return 0;
    const myStreak = calcWalkStreak();
    if (!pair.since) return myStreak;
    const today = new Date().toISOString().split('T')[0];
    const sinceDays = Math.floor((new Date(today) - new Date(pair.since)) / (1000*60*60*24)) + 1;
    return Math.max(0, Math.min(myStreak, sinceDays));
}

// 오늘 아직 산책 기록이 없으면 위기(스트릭이 이미 쌓여 있을 때만 위기로 간주)
function isBuddyStreakAtRisk() {
    if (!getBuddyPair()) return false;
    const today = new Date().toISOString().split('T')[0];
    const walkedToday = (typeof walks !== 'undefined' ? walks : [])
        .some(w => w.savedAt && w.savedAt.split('T')[0] === today);
    return !walkedToday && calcWalkStreak() >= 1;
}

// 버디에게 넛지(응원) 전송 — 기존 채팅 히스토리에 메시지 적재
function sendBuddyNudge() {
    const pair = getBuddyPair();
    if (!pair) return;
    if (typeof chatHistories === 'undefined') return;
    if (!chatHistories[pair.id]) chatHistories[pair.id] = [];
    const now = new Date();
    const timeStr = `${String(now.getHours()).padStart(2,'0')}:${String(now.getMinutes()).padStart(2,'0')}`;
    const days = calcBuddyStreak();
    chatHistories[pair.id].push({
        sender: 'me', time: timeStr,
        text: `🔥 우리 함께 산책 ${days}일째! 오늘도 같이 이어가요 🐾`,
    });
    if (typeof saveState === 'function') saveState();
    if (typeof showToast === 'function') showToast(`${pair.nickname}님에게 넛지를 보냈어요! 🐾`);
    renderBuddyStreakCard();
}

function renderBuddyStreakCard() {
    const el = document.getElementById('buddy-streak-card');
    if (!el) return;
    const pair = getBuddyPair();

    // 미페어 상태: 친구 중에서 버디를 고르는 선택 UI
    if (!pair) {
        const list = typeof friends !== 'undefined' ? friends : [];
        if (list.length === 0) {
            el.innerHTML = `
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-people-arrows text-gray-300"></i>
                    <span>이웃을 맺으면 함께 산책 스트릭을 쌓을 수 있어요!</span>
                </div>`;
            return;
        }
        const options = list.map(f =>
            `<option value="${f.id}">${escapeHtml(f.nickname)} (${escapeHtml(f.petName)})</option>`).join('');
        el.innerHTML = `
            <div class="flex items-center gap-1.5 mb-2">
                <span class="text-[11px] font-black text-gray-700">🤝 버디 산책</span>
                <span class="text-[9px] text-gray-400 font-medium">친구와 함께 연속일 쌓기</span>
            </div>
            <div class="flex items-center gap-1.5">
                <select id="buddy-pick-select" class="flex-1 min-w-0 text-[10px] font-bold text-gray-700 bg-white border border-amber-200 rounded-lg px-2 py-1.5">
                    ${options}
                </select>
                <button onclick="setBuddy(document.getElementById('buddy-pick-select').value)"
                    class="shrink-0 bg-brand-500 hover:bg-brand-600 text-white font-black text-[10px] px-2.5 py-1.5 rounded-lg transition-colors">짝 맺기</button>
            </div>`;
        return;
    }

    // 페어 상태: 함께 연속일 + 위기 시 넛지
    const days = calcBuddyStreak();
    const atRisk = isBuddyStreakAtRisk();
    el.innerHTML = `
        <div class="flex items-center justify-between gap-2 mb-2">
            <div class="flex items-center gap-2 min-w-0">
                <img loading="lazy" src="${pair.avatar}" class="w-7 h-7 object-cover rounded-full border border-amber-100 shrink-0" onerror="this.src='https://placehold.co/100/fbeee0/732f18?text=${escapeHtml(pair.nickname)}'">
                <div class="min-w-0">
                    <p class="text-[11px] font-black text-gray-700 truncate">🤝 ${escapeHtml(pair.nickname)}님과 함께</p>
                    <p class="text-[9px] text-gray-400 font-medium">함께 연속 산책</p>
                </div>
            </div>
            <span class="streak-badge shrink-0"><i class="fa-solid fa-fire"></i> ${days}일</span>
        </div>
        ${atRisk ? `
        <div class="bg-rose-50 border border-rose-100 rounded-xl p-2 flex items-center justify-between gap-2">
            <p class="text-[10px] font-bold text-rose-600 min-w-0">오늘 아직 산책 전! ${days}일 기록이 끊길 위기예요.</p>
            <button onclick="sendBuddyNudge()" class="shrink-0 bg-rose-500 hover:bg-rose-600 text-white font-black text-[9px] px-2 py-1 rounded-lg transition-colors">넛지 보내기</button>
        </div>` : `
        <div class="flex items-center justify-between gap-2">
            <p class="text-[9px] text-gray-400 font-medium">오늘도 함께 이어가는 중! 👏</p>
            <button onclick="sendBuddyNudge()" class="shrink-0 text-[9px] font-bold text-brand-500 hover:text-brand-600">응원 보내기</button>
        </div>`}
        <button onclick="unpairBuddy()" class="mt-1.5 text-[8px] text-gray-300 hover:text-gray-400 font-bold">버디 해제</button>`;
}

// ── 📊 펄스: 월간 리포트 카드 (PetDesk 벤치마크) ──────────────────────────
function renderMonthlyReport(targetElId) {
    const el = document.getElementById(targetElId || 'monthly-report-card');
    if (!el) return;

    const now = new Date();
    const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().split('T')[0];
    const monthEnd   = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().split('T')[0];

    // 이번 달 산책 집계
    const monthWalks = (typeof walks !== 'undefined' ? walks : []).filter(w => {
        const d = w.savedAt ? w.savedAt.split('T')[0] : null;
        return d && d >= monthStart && d <= monthEnd;
    });
    const totalDist = monthWalks.reduce((s, w) => s + (parseFloat(w.distance) || 0), 0);
    const totalKcal = monthWalks.reduce((s, w) => s + (parseInt(w.calories) || 0), 0);

    // 이번 달 건강 기록 횟수
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const monthHealth = history.filter(h => h.date >= monthStart && h.date <= monthEnd).length;

    // AI 분석 횟수
    const aiList = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const monthAI = aiList.filter(a => a.analyzedAt && a.analyzedAt.split('T')[0] >= monthStart).length;

    const walkStreak = calcWalkStreak();
    const monthName = `${now.getMonth() + 1}월`;

    el.setAttribute('role', 'button');
    el.setAttribute('tabindex', '0');
    el.classList.add('cursor-pointer');
    el.onclick = () => {
        if (typeof openMonthlyReportModal === 'function') {
            openMonthlyReportModal();
        } else if (typeof showToast === 'function') {
            showToast('월간 리포트를 불러오는 중입니다.');
        }
    };
    el.onkeydown = (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            el.click();
        }
    };

    el.innerHTML = `
        <div class="space-y-3">
            <div class="flex items-center justify-between">
                <span class="text-[12px] font-black text-gray-700">📊 ${monthName} 리포트</span>
                ${walkStreak >= 3 ? `<span class="streak-badge text-[9px]"><i class="fa-solid fa-fire"></i> ${walkStreak}일 연속</span>` : ''}
            </div>
            <div class="grid grid-cols-2 gap-2">
                <div class="bg-brand-50 border border-brand-100 rounded-xl p-3 text-center">
                    <div class="text-lg font-black text-brand-600">${monthWalks.length}회</div>
                    <div class="text-[9px] text-gray-500 font-bold mt-0.5">이번 달 산책</div>
                </div>
                <div class="bg-amber-50 border border-amber-100 rounded-xl p-3 text-center">
                    <div class="text-lg font-black text-amber-600">${totalDist.toFixed(1)}km</div>
                    <div class="text-[9px] text-gray-500 font-bold mt-0.5">총 산책 거리</div>
                </div>
                <div class="bg-emerald-50 border border-emerald-100 rounded-xl p-3 text-center">
                    <div class="text-lg font-black text-emerald-600">${monthHealth}회</div>
                    <div class="text-[9px] text-gray-500 font-bold mt-0.5">건강 기록</div>
                </div>
                <div class="bg-brand-50 border border-brand-100 rounded-xl p-3 text-center">
                    <div class="text-lg font-black text-brand-600">${monthAI}회</div>
                    <div class="text-[9px] text-gray-500 font-bold mt-0.5">AI 분석</div>
                </div>
            </div>
            ${totalKcal > 0 ? `
            <div class="bg-rose-50 border border-rose-100 rounded-xl p-2.5 flex items-center gap-2">
                <span class="text-lg">🔥</span>
                <div>
                    <p class="text-[11px] font-black text-rose-600">${totalKcal.toLocaleString()} kcal 소모</p>
                    <p class="text-[9px] text-gray-400 font-medium">${monthName} 산책으로 소모한 칼로리</p>
                </div>
            </div>` : ''}
        </div>`;
}
