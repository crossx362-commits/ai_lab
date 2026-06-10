// health.js — 건강 탭 컨트롤러

function renderHealthTab() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petName = pet?.name || '댕이';

    // 펫 이름 업데이트
    const petNameEl = document.getElementById('health-pet-name');
    if (petNameEl) petNameEl.textContent = petName;

    // 건강 요약 카드 업데이트
    updateHealthSummaryCards();

    // 오늘의 건강 기록 업데이트
    updateTodayHealthDisplay();

    // 차트 및 캘린더 렌더링
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
    if (typeof updateHealthTutorialMainVisibility === 'function') updateHealthTutorialMainVisibility();

    // AI 사용 횟수 업데이트
    updateAiUsageCount();

    // 식사 일지 렌더링
    if (typeof renderMealLogsList === 'function') renderMealLogsList();
}

// 건강 요약 카드 업데이트
function updateHealthSummaryCards() {
    const score = (typeof calcHealthScore === 'function') ? calcHealthScore() : 0;
    const streak = (typeof calcHealthStreak === 'function') ? calcHealthStreak() : 0;

    const scoreEl = document.getElementById('health-summary-score');
    const streakEl = document.getElementById('health-summary-streak');

    if (scoreEl) scoreEl.textContent = score || '--';
    if (streakEl) streakEl.textContent = streak ? `${streak}일` : '--일';

    // 평균 식사량/음수량
    const last7Days = (typeof getLast7DaysHealthData === 'function') ? getLast7DaysHealthData() : [];
    const avgFood = last7Days.length ? Math.round(last7Days.reduce((s, d) => s + d.food, 0) / 7) : 0;
    const avgWater = last7Days.length ? Math.round(last7Days.reduce((s, d) => s + d.water, 0) / 7) : 0;

    const foodEl = document.getElementById('health-summary-food');
    const waterEl = document.getElementById('health-summary-water');

    if (foodEl) foodEl.textContent = avgFood ? `${avgFood}g` : '--g';
    if (waterEl) waterEl.textContent = avgWater ? `${avgWater}ml` : '--ml';
}

// 오늘의 건강 기록 표시
function updateTodayHealthDisplay() {
    const logs = (typeof healthLogs !== 'undefined' && healthLogs?.today) ? healthLogs.today : { food: 0, water: 0, poop: null };

    const foodEl = document.getElementById('health-today-food');
    const waterEl = document.getElementById('health-today-water');
    const poopEl = document.getElementById('health-today-poop');

    if (foodEl) foodEl.textContent = logs.food ? `${logs.food}g` : '--g';
    if (waterEl) waterEl.textContent = logs.water ? `${logs.water}ml` : '--ml';

    if (poopEl) {
        const poopTypes = { normal: '정상', soft: '무름', hard: '딱딱', liquid: '설사' };
        poopEl.textContent = logs.poop ? poopTypes[logs.poop] || '--' : '--';
    }
}

// AI 사용 횟수 업데이트
function updateAiUsageCount() {
    const usageEl = document.getElementById('ai-usage-count-health');
    if (!usageEl) return;

    const analyses = (typeof getHealthAnalyses === 'function') ? getHealthAnalyses() : [];
    const thisMonth = new Date().toISOString().slice(0, 7);
    const monthlyCount = analyses.filter(a => a.date && a.date.startsWith(thisMonth)).length;

    usageEl.textContent = monthlyCount;
}

// 건강 트렌드 차트 렌더링 (메인)
function renderHealthTrendChartMain() {
    const canvas = document.getElementById('health-trend-chart-main');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    const data = (typeof getLast7DaysHealthData === 'function') ? getLast7DaysHealthData() : [];

    if (window.healthTrendChartMain) {
        window.healthTrendChartMain.destroy();
    }

    window.healthTrendChartMain = new Chart(ctx, {
        type: 'line',
        data: {
            labels: data.map(d => {
                const dt = new Date(d.date);
                return `${dt.getMonth() + 1}/${dt.getDate()}`;
            }),
            datasets: [
                {
                    label: '식사량 (g)',
                    data: data.map(d => d.food),
                    borderColor: '#f59e0b',
                    backgroundColor: 'rgba(245, 158, 11, 0.1)',
                    tension: 0.3,
                    fill: true
                },
                {
                    label: '음수량 (ml)',
                    data: data.map(d => d.water),
                    borderColor: '#0ea5e9',
                    backgroundColor: 'rgba(14, 165, 233, 0.1)',
                    tension: 0.3,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: true, position: 'top', labels: { font: { size: 11 }, boxWidth: 12 } },
                tooltip: { enabled: true }
            },
            scales: {
                y: { beginAtZero: true, ticks: { font: { size: 10 } } },
                x: { ticks: { font: { size: 10 } } }
            }
        }
    });
}

// 건강 캘린더 렌더링 (메인)
function renderHealthCalendarMain() {
    const el = document.getElementById('health-calendar-main');
    if (!el) return;

    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const today = new Date();

    const hasAnyRecord = history.length > 0;

    if (!hasAnyRecord) {
        el.innerHTML = '';
        el.style.display = 'none';
        return;
    }

    el.style.display = 'block';

    const cells = Array.from({ length: 90 }, (_, i) => {
        const d = new Date(today);
        d.setDate(today.getDate() - (89 - i));
        const date = d.toISOString().split('T')[0];
        const entry = history.find(h => h.date === date);
        const hasRecord = entry && (entry.food > 0 || entry.water > 0 || entry.poop !== null);
        const isToday = date === today.toISOString().split('T')[0];
        let bg = hasRecord ? 'bg-emerald-400' : 'bg-gray-100';
        if (isToday) bg += ' ring-1 ring-amber-400';
        return `<div class="w-3 h-3 rounded-sm ${bg}" title="${date}"></div>`;
    }).join('');

    el.innerHTML = `
        <div class="flex flex-wrap gap-0.5">${cells}</div>
        <div class="flex items-center gap-2 mt-2">
            <span class="w-3 h-3 rounded-sm bg-gray-100 inline-block"></span><span class="text-[9px] text-gray-400">기록 없음</span>
            <span class="w-3 h-3 rounded-sm bg-emerald-400 inline-block ml-2"></span><span class="text-[9px] text-gray-400">기록 완료</span>
        </div>`;
}

// 사용법 안내 표시 여부
function updateHealthTutorialMainVisibility() {
    const tutorialEl = document.getElementById('health-tutorial-main');
    if (!tutorialEl) return;

    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const last7Days = (typeof getLast7DaysHealthData === 'function') ? getLast7DaysHealthData() : [];
    const hasData = last7Days.some(d => d.food > 0 || d.water > 0 || d.poop);

    if (hasData || history.length > 0) {
        tutorialEl.classList.add('hidden');
    } else {
        tutorialEl.classList.remove('hidden');
    }
}

// 건강 탭 컨트롤러 등록
const HealthTabController = {
    init: function() {
        renderHealthTab();
    },
    destroy: function() {
        if (window.healthTrendChartMain) {
            window.healthTrendChartMain.destroy();
            window.healthTrendChartMain = null;
        }
    }
};

// TabControllers에 등록
if (typeof TabControllers !== 'undefined') {
    TabControllers.health = HealthTabController;
}
