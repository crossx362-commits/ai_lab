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
    if (typeof renderPreventiveChecklist === 'function') renderPreventiveChecklist();
    if (typeof renderPreventiveTimeline === 'function') renderPreventiveTimeline();
    if (typeof renderMedAdherenceTracker === 'function') renderMedAdherenceTracker();
    if (typeof renderMedicalRecordsTimeline === 'function') renderMedicalRecordsTimeline();

    // 몸무게/QOL 주간 체크인 (백로그 나무_20260709_3, 오너 승인 2026-07-10)
    if (typeof QolCheckin !== 'undefined') QolCheckin.renderWidget('qol-checkin-widget');

    // BCS 체형 셀프체크 위저드 (백로그 나무_20260712143815, P3)
    if (typeof BcsWizard !== 'undefined') BcsWizard.renderWidget('bcs-wizard-widget');

    // 일일 급식·칼로리 트래커 (백로그 나무_20260712143815, P3)
    if (typeof CalorieTracker !== 'undefined') CalorieTracker.renderWidget('calorie-tracker-widget');

    // 맞춤 식단·급여량 추천 카드 (백로그 나무, P2 — 체형·체중·활동량 종합)
    if (typeof DietRecommend !== 'undefined') DietRecommend.renderWidget('diet-recommend-widget');

    // 병원비 제보·비교 보드 (백로그 나무, P3)
    if (typeof VetCostBoard !== 'undefined') VetCostBoard.renderWidget('vet-cost-board-widget');

    // 차트 및 캘린더 렌더링
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
    if (typeof updateHealthTutorialMainVisibility === 'function') updateHealthTutorialMainVisibility();

    // AI 사용 횟수 업데이트
    updateAiUsageCount();

    // 식사 일지 렌더링
    if (typeof renderMealLogsList === 'function') renderMealLogsList();

    // 데일리 컨디션 원탭 로그 위젯
    if (typeof DailyCondition !== 'undefined') DailyCondition.renderWidget('daily-condition-widget');

    // 예측 웰니스 이상감지 카드
    if (typeof renderWellnessCard === 'function') renderWellnessCard();

    // 주간 건강 변화 조기경보 리포트 카드
    if (typeof renderWeeklyReportCard === 'function') renderWeeklyReportCard();

    // 돌봄 스케줄러 카드(오늘의 일정·준수율 배지·달력·다가오는 돌봄) — 마이펫→건강 탭
    // 이전(99757d36) 때 템플릿 id에 -health 접미사가 붙으며 이 호출들이 빠져 카드가
    // 영구 빈 채로 방치됐던 버그(2026-07-19 수리). renderCalendar가 upcoming까지 그린다.
    if (typeof renderCareScheduler === 'function') renderCareScheduler();
    if (typeof updateCareCompletionBadge === 'function') updateCareCompletionBadge();
    if (typeof renderCalendar === 'function') renderCalendar();

    // 위젯이 전부 빈 기능군은 소제목째 숨김(고아 소제목 방지)
    updateCareWidgetGroupVisibility();

    // 케어위젯 노출/클릭 최소 계측(회의_202607162027_3) — 위젯 DOM이 막 채워진 뒤 관측 시작
    if (typeof observeCareWidgetsForInstrumentation === 'function') observeCareWidgetsForInstrumentation();
}

// 케어 위젯 기능군 소제목 정리 — 그룹 내 위젯 mount가 전부 비어 있으면(일정 없음 등으로
// 위젯이 스스로 숨은 상태) 소제목·구분선까지 통째로 숨긴다. 위젯이 다시 차면 재노출.
function updateCareWidgetGroupVisibility() {
    const wrap = document.getElementById('care-widgets-group');
    if (!wrap) return;
    let visibleGroups = 0;
    wrap.querySelectorAll('[data-care-group]').forEach(group => {
        const mounts = group.querySelectorAll(':scope > div[id]');
        const hasContent = [...mounts].some(m =>
            m.innerHTML.trim() !== '' && m.style.display !== 'none');
        group.classList.toggle('hidden', !hasContent);
        if (hasContent) visibleGroups++;
    });
    // 전 그룹이 비면 '케어 위젯' 라벨도 숨김
    const label = wrap.firstElementChild;
    if (label && !label.hasAttribute('data-care-group')) {
        label.classList.toggle('hidden', visibleGroups === 0);
    }
}

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
