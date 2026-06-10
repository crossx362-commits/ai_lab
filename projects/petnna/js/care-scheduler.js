// care-scheduler.js — 돌봄 스케줄러 관리

// 돌봄 일정 추가
function addCareSchedule(schedule) {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const newSchedule = {
        id: Date.now(),
        petId: schedule.petId || (typeof getActivePet === 'function' ? getActivePet()?.id : null),
        type: schedule.type, // 'feed', 'water', 'walk', 'medicine', 'vet', 'groom', 'play'
        title: schedule.title,
        time: schedule.time, // 'HH:MM'
        repeat: schedule.repeat || 'daily', // 'daily', 'weekly', 'monthly', 'once'
        repeatDays: schedule.repeatDays || [0, 1, 2, 3, 4, 5, 6], // 0=일요일, 6=토요일
        date: schedule.date || null, // 'YYYY-MM-DD' for 'once' type
        completed: false,
        lastCompleted: null,
        notes: schedule.notes || '',
        createdAt: new Date().toISOString()
    };
    careSchedules.schedules.push(newSchedule);
    if (typeof AppStore !== 'undefined') AppStore.setState('careSchedules', careSchedules);
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof showToast === 'function') showToast(`일정 "${newSchedule.title}" 추가 완료! 🗓️`);
    return newSchedule;
}

// 돌봄 일정 수정
function updateCareSchedule(scheduleId, updates) {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const idx = careSchedules.schedules.findIndex(s => s.id === scheduleId);
    if (idx === -1) return false;
    careSchedules.schedules[idx] = { ...careSchedules.schedules[idx], ...updates };
    if (typeof AppStore !== 'undefined') AppStore.setState('careSchedules', careSchedules);
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof showToast === 'function') showToast(`일정 수정 완료! ✏️`);
    return true;
}

// 돌봄 일정 삭제
function deleteCareSchedule(scheduleId) {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    careSchedules.schedules = careSchedules.schedules.filter(s => s.id !== scheduleId);
    if (typeof AppStore !== 'undefined') AppStore.setState('careSchedules', careSchedules);
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof showToast === 'function') showToast(`일정 삭제 완료! 🗑️`);
}

// 돌봄 일정 완료 처리
function completeCareSchedule(scheduleId, notes = '') {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const schedule = careSchedules.schedules.find(s => s.id === scheduleId);
    if (!schedule) return false;

    const completionRecord = {
        scheduleId: scheduleId,
        completedAt: new Date().toISOString(),
        petId: schedule.petId,
        type: schedule.type,
        title: schedule.title,
        notes: notes
    };
    careSchedules.completionHistory.unshift(completionRecord);
    if (careSchedules.completionHistory.length > 500) careSchedules.completionHistory.splice(500);

    // 반복 일정이 아니면 완료 표시
    if (schedule.repeat === 'once') {
        schedule.completed = true;
    }
    schedule.lastCompleted = new Date().toISOString();

    if (typeof AppStore !== 'undefined') AppStore.setState('careSchedules', careSchedules);
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof updateCareCompletionBadge === 'function') updateCareCompletionBadge();
    if (typeof showToast === 'function') showToast(`"${schedule.title}" 완료! 🎉`);

    // 건강 데이터와 연동 (식사/물)
    if (schedule.type === 'feed' && typeof healthLogs !== 'undefined' && healthLogs.today) {
        healthLogs.today.food = (healthLogs.today.food || 0) + 50; // 기본 50g
        if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
    }
    if (schedule.type === 'water' && typeof healthLogs !== 'undefined' && healthLogs.today) {
        healthLogs.today.water = (healthLogs.today.water || 0) + 100; // 기본 100ml
        if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
    }

    return true;
}

// 오늘 일정 가져오기
function getTodaySchedules() {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const today = new Date();
    const dayOfWeek = today.getDay();
    const todayStr = today.toISOString().split('T')[0];

    return careSchedules.schedules.filter(schedule => {
        if (schedule.repeat === 'once') {
            return schedule.date === todayStr && !schedule.completed;
        }
        if (schedule.repeat === 'daily') {
            return true;
        }
        if (schedule.repeat === 'weekly') {
            return schedule.repeatDays.includes(dayOfWeek);
        }
        if (schedule.repeat === 'monthly') {
            // 매월 같은 날짜
            const scheduleDate = new Date(schedule.date || schedule.createdAt);
            return scheduleDate.getDate() === today.getDate();
        }
        return false;
    }).sort((a, b) => a.time.localeCompare(b.time));
}

// 주간 일정 준수율 계산
function getWeeklyCareCompletionRate() {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const today = new Date();
    const weekAgo = new Date(today);
    weekAgo.setDate(weekAgo.getDate() - 7);

    const completions = careSchedules.completionHistory.filter(c => {
        const completedDate = new Date(c.completedAt);
        return completedDate >= weekAgo && completedDate <= today;
    });

    // 7일간 예상 일정 수 계산
    let expectedCount = 0;
    careSchedules.schedules.forEach(schedule => {
        if (schedule.repeat === 'daily') {
            expectedCount += 7;
        } else if (schedule.repeat === 'weekly') {
            expectedCount += schedule.repeatDays.length;
        }
    });

    if (expectedCount === 0) return 100;
    return Math.round((completions.length / expectedCount) * 100);
}

// 돌봄 타입별 아이콘
function getCareTypeIcon(type) {
    const icons = {
        feed: '🍖',
        water: '💧',
        walk: '🚶',
        medicine: '💊',
        vet: '🏥',
        groom: '✂️',
        play: '🎾'
    };
    return icons[type] || '📋';
}

// 돌봄 타입별 이름
function getCareTypeName(type) {
    const names = {
        feed: '식사',
        water: '음수',
        walk: '산책',
        medicine: '투약',
        vet: '병원',
        groom: '미용',
        play: '놀이'
    };
    return names[type] || '기타';
}

// 주간 돌봄 통계
function getWeeklyCareStats() {
    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const today = new Date();
    const weekAgo = new Date(today);
    weekAgo.setDate(weekAgo.getDate() - 7);

    const completions = careSchedules.completionHistory.filter(c => {
        const completedDate = new Date(c.completedAt);
        return completedDate >= weekAgo && completedDate <= today;
    });

    const stats = {};
    completions.forEach(c => {
        if (!stats[c.type]) stats[c.type] = 0;
        stats[c.type]++;
    });

    return stats;
}

// 돌봄 스케줄러 렌더링
function renderCareScheduler() {
    const container = document.getElementById('care-scheduler-container');
    if (!container) return;

    const todaySchedules = getTodaySchedules();
    if (todaySchedules.length === 0) {
        container.innerHTML = `
            <div class="text-center py-6 text-gray-400 text-sm">
                <div class="text-2xl mb-2">📅</div>
                <p>오늘 일정이 없습니다</p>
            </div>`;
        return;
    }

    const now = new Date();
    const currentTime = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

    const html = todaySchedules.map(schedule => {
        const isPast = schedule.time < currentTime;
        const isCompleted = schedule.lastCompleted && schedule.lastCompleted.startsWith(now.toISOString().split('T')[0]);
        const status = isCompleted ? 'completed' : isPast ? 'overdue' : 'pending';
        const bgColor = isCompleted ? 'bg-emerald-50 border-emerald-200' : isPast ? 'bg-rose-50 border-rose-200' : 'bg-white border-gray-200';
        const textColor = isCompleted ? 'text-emerald-700' : isPast ? 'text-rose-700' : 'text-gray-700';

        return `
            <div class="flex items-center gap-2 p-2.5 rounded-xl border ${bgColor} ${textColor}">
                <div class="text-xl">${getCareTypeIcon(schedule.type)}</div>
                <div class="flex-1">
                    <div class="flex items-center gap-1.5">
                        <span class="text-xs font-black">${schedule.time}</span>
                        <span class="text-[11px] font-medium">${schedule.title}</span>
                    </div>
                    ${schedule.notes ? `<p class="text-[9px] text-gray-500 mt-0.5">${schedule.notes}</p>` : ''}
                </div>
                ${!isCompleted ? `
                    <button onclick="completeCareSchedule(${schedule.id})"
                        class="px-2 py-1 bg-emerald-500 hover:bg-emerald-600 text-white text-[9px] font-black rounded-lg transition-all">
                        완료
                    </button>
                ` : `
                    <span class="text-emerald-500 text-sm">✓</span>
                `}
            </div>
        `;
    }).join('');

    container.innerHTML = html;
}

// 돌봄 완료 배지 업데이트
function updateCareCompletionBadge() {
    const badge = document.getElementById('care-completion-badge');
    if (!badge) return;

    const rate = getWeeklyCareCompletionRate();
    const color = rate >= 80 ? 'bg-emerald-100 text-emerald-700' : rate >= 60 ? 'bg-amber-100 text-amber-700' : 'bg-rose-100 text-rose-700';
    badge.className = `text-[9px] font-black px-2 py-0.5 rounded-full ${color}`;
    badge.textContent = `${rate}% 준수`;
}
