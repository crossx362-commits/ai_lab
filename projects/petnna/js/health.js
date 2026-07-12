// health.js — 건강 탭 컨트롤러

// 식사 탭 전환 함수
let activeMealTab = 'food';

function switchMealTab(tab) {
    activeMealTab = tab;

    // 탭 버튼 스타일 업데이트
    ['food', 'time', 'water'].forEach(t => {
        const btn = document.getElementById(`meal-tab-${t}`);
        const content = document.getElementById(`meal-content-${t}`);

        if (t === tab) {
            btn?.classList.add('bg-white', 'text-amber-600', 'shadow-sm');
            btn?.classList.remove('text-gray-500', 'hover:text-gray-700');
            content?.classList.remove('hidden');
        } else {
            btn?.classList.remove('bg-white', 'text-amber-600', 'shadow-sm');
            btn?.classList.add('text-gray-500', 'hover:text-gray-700');
            content?.classList.add('hidden');
        }
    });

    // 음수 탭: 오늘 음수량 + 프로그레스 바 업데이트
    if (tab === 'water') {
        const waterEl = document.getElementById('health-today-water-tab');
        const waterMain = document.getElementById('health-today-water');
        if (waterEl && waterMain) waterEl.textContent = waterMain.textContent;

        // 체중 기반 목표 (50ml/kg), 펫 데이터에서 추출
        const pet = (typeof pets !== 'undefined' && pets.length > 0) ? pets[0] : null;
        const weight = pet ? parseFloat(pet.weight) : 0;
        const goalMl = weight > 0 ? Math.round(weight * 50) : 300;

        const currentText = (waterEl?.textContent || '0').replace(/[^0-9]/g, '');
        const currentMl = parseInt(currentText) || 0;
        const pct = Math.min(100, Math.round((currentMl / goalMl) * 100));

        const bar = document.getElementById('water-progress-bar');
        const goalLabel = document.getElementById('water-goal-label');
        const goalMax = document.getElementById('water-goal-max');
        if (bar) bar.style.width = `${pct}%`;
        if (goalLabel) goalLabel.textContent = `${pct}% 달성`;
        if (goalMax) goalMax.textContent = `${goalMl}ml`;
    }

    // 시간 탭: 타임라인 렌더
    if (tab === 'time' && typeof renderMealTimeline === 'function') {
        renderMealTimeline();
    }
}

function renderHealthTab() {
    // 펫 선택 드롭다운 초기화
    updateHealthPetSelector();

    // 건강 요약 카드 업데이트
    updateHealthSummaryCards();

    // 오늘의 건강 기록 업데이트
    updateTodayHealthDisplay();

    // 투약·정기예방 대시보드 + 건강수첩 (마이펫 탭에서 이동)
    if (typeof renderPreventiveCareDashboard === 'function') renderPreventiveCareDashboard();
    if (typeof renderMedAdherenceTracker === 'function') renderMedAdherenceTracker();
    if (typeof renderMedicalRecordsTimeline === 'function') renderMedicalRecordsTimeline();

    // 몸무게/QOL 주간 체크인 (백로그 나무_20260709_3, 오너 승인 2026-07-10)
    if (typeof QolCheckin !== 'undefined') QolCheckin.renderWidget('qol-checkin-widget');

    // BCS 체형 셀프체크 위저드 (백로그 나무_20260712143815, P3)
    if (typeof BcsWizard !== 'undefined') BcsWizard.renderWidget('bcs-wizard-widget');

    // 일일 급식·칼로리 트래커 (백로그 나무_20260712143815, P3)
    if (typeof CalorieTracker !== 'undefined') CalorieTracker.renderWidget('calorie-tracker-widget');

    // 차트 및 캘린더 렌더링
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
    if (typeof updateHealthTutorialMainVisibility === 'function') updateHealthTutorialMainVisibility();

    // AI 사용 횟수 업데이트
    updateAiUsageCount();

    // 식사 일지 렌더링
    if (typeof renderMealLogsList === 'function') renderMealLogsList();

    // 예측 웰니스 이상감지 카드
    if (typeof renderWellnessCard === 'function') renderWellnessCard();

    // 주간 건강 변화 조기경보 리포트 카드
    if (typeof renderWeeklyReportCard === 'function') renderWeeklyReportCard();

    // 일일 권장 사료량 계산기 (RER/MER)
    renderFoodCalculator();
}

// 일일 권장 사료량 계산기 (RER/MER)
// RER = 70 × 체중(kg)^0.75, MER = RER × 활동계수 (종·중성화·활동량 기반)
function renderFoodCalculator() {
    const body = document.getElementById('food-calc-body');
    const empty = document.getElementById('food-calc-empty');
    if (!body || !empty) return;

    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const weight = pet ? parseFloat(pet.weight) : NaN;

    if (!pet || !(weight > 0)) {
        body.classList.add('hidden');
        empty.classList.remove('hidden');
        return;
    }
    body.classList.remove('hidden');
    empty.classList.add('hidden');

    const isCat = (pet.type === 'cat');
    const neutered = /중성화/.test(pet.gender || '');

    const weightEl = document.getElementById('food-calc-weight');
    const neuterEl = document.getElementById('food-calc-neuter');
    if (weightEl) weightEl.textContent = `${weight} kg`;
    if (neuterEl) neuterEl.textContent = neutered ? '중성화 완료' : '미중성화';

    // 활동계수: 중성화 성체 기준값에서 활동량으로 가감 (수의영양 표준 범위)
    const base = isCat ? (neutered ? 1.2 : 1.4) : (neutered ? 1.6 : 1.8);
    const activity = document.getElementById('food-calc-activity')?.value || 'normal';
    const delta = activity === 'low' ? -0.4 : (activity === 'high' ? 0.4 : 0);
    const factor = Math.max(0.8, base + delta);

    const rer = 70 * Math.pow(weight, 0.75);
    const mer = rer * factor;

    const density = parseFloat(document.getElementById('food-calc-density')?.value) || 350;
    const grams = density > 0 ? (mer / density) * 100 : 0;

    const rerEl = document.getElementById('food-calc-rer');
    const merEl = document.getElementById('food-calc-mer');
    const gramsEl = document.getElementById('food-calc-grams');
    if (rerEl) rerEl.textContent = `${Math.round(rer)} kcal`;
    if (merEl) merEl.textContent = `${Math.round(mer)} kcal`;
    if (gramsEl) gramsEl.textContent = `${Math.round(grams)} g`;
}

window.renderFoodCalculator = renderFoodCalculator;

// 펫 선택 드롭다운 업데이트
function updateHealthPetSelector() {
    const selector = document.getElementById('health-pet-selector');
    if (!selector) return;

    const pets = (typeof AppStore !== 'undefined') ? AppStore.getState('pets') : [];
    const activePet = (typeof getActivePet === 'function') ? getActivePet() : null;

    selector.innerHTML = '<option value="">펫 선택</option>';

    pets.forEach(pet => {
        const option = document.createElement('option');
        option.value = pet.id;
        option.textContent = `${pet.name} (${pet.type || '반려동물'})`;
        if (activePet && pet.id === activePet.id) {
            option.selected = true;
        }
        selector.appendChild(option);
    });
}

// 펫 변경 핸들러
function onHealthPetChange() {
    const selector = document.getElementById('health-pet-selector');
    if (!selector) return;

    const petId = parseInt(selector.value);
    if (!petId) return;

    // 활성 펫 변경
    if (typeof setActivePet === 'function') {
        setActivePet(petId);
    }

    // 건강 탭 새로고침
    renderHealthTab();

    if (typeof showToast === 'function') {
        const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        showToast(`${pet?.name || '펫'}의 건강 정보로 전환되었습니다`);
    }
}

// 전역 함수 등록
window.onHealthPetChange = onHealthPetChange;

// 건강 요약 카드 업데이트
function updateHealthSummaryCards() {
    const score = (typeof calcHealthScore === 'function') ? calcHealthScore() : 0;
    const streak = (typeof calcHealthStreak === 'function') ? calcHealthStreak() : 0;

    const scoreEl = document.getElementById('report-health-score');
    const streakEl = document.getElementById('report-streak');

    if (scoreEl) scoreEl.textContent = score || '--';
    if (streakEl) streakEl.textContent = streak ? `${streak}일` : '--일';

    // 준수율
    const rateEl = document.getElementById('report-care-rate');
    if (rateEl) {
        const rate = (typeof getWeeklyCareCompletionRate === 'function') ? getWeeklyCareCompletionRate() : 0;
        rateEl.textContent = rate ? `${rate}%` : '--%';
    }

    // AI 분석 횟수
    const aiCountEl = document.getElementById('report-ai-count');
    if (aiCountEl) {
        const analyses = (typeof getHealthAnalyses === 'function') ? getHealthAnalyses() : [];
        const thisMonth = new Date().toISOString().slice(0, 7);
        const monthlyCount = analyses.filter(a => a.date && a.date.startsWith(thisMonth)).length;
        aiCountEl.textContent = `${monthlyCount}회`;
    }
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
    if (!canvas || typeof Chart === 'undefined') return;

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
