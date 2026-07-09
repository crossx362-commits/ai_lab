// monthly-report-modal.js - 월간 리포트 상세 모달

function openMonthlyReportModal() {
    const modal = document.getElementById('monthly-report-modal');
    if (!modal) return;

    // 데이터 업데이트
    updateMonthlyReportData();

    modal.classList.remove('hidden');
    modal.classList.add('flex');
}

function closeMonthlyReportModal() {
    const modal = document.getElementById('monthly-report-modal');
    if (modal) {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

function updateMonthlyReportData() {
    const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petName = pet?.name || '댕이';

    // 월 정보
    const now = new Date();
    const month = now.getMonth() + 1;
    const monthEl = document.getElementById('report-modal-month');
    const petNameEl = document.getElementById('report-modal-pet-name');
    if (monthEl) monthEl.textContent = `${month}월`;
    if (petNameEl) petNameEl.textContent = petName;

    // 건강 점수
    const score = (typeof calcHealthScore === 'function') ? calcHealthScore() : 0;
    const scoreEl = document.getElementById('report-modal-health-score');
    if (scoreEl) scoreEl.textContent = score || '--';

    // 연속 기록
    const streak = (typeof calcHealthStreak === 'function') ? calcHealthStreak() : 0;
    const streakEl = document.getElementById('report-modal-streak');
    if (streakEl) streakEl.textContent = streak ? `${streak}일` : '--일';

    const thisMonth = now.toISOString().slice(0, 7);
    const getRecordDate = (record) => {
        const rawDate = record?.savedAt || record?.date || record?.completedAt || record?.analyzedAt || '';
        return typeof rawDate === 'string' ? rawDate.slice(0, 10) : '';
    };

    // 산책 데이터
    const walkList = (typeof walks !== 'undefined' && Array.isArray(walks))
        ? walks
        : (Array.isArray(window.walks) ? window.walks : []);
    const monthlyWalks = walkList.filter(w => getRecordDate(w).startsWith(thisMonth));

    const walkCountEl = document.getElementById('report-modal-walk-count');
    const walkDistanceEl = document.getElementById('report-modal-walk-distance');
    if (walkCountEl) walkCountEl.textContent = `${monthlyWalks.length}회`;

    const totalDistance = monthlyWalks.reduce((sum, w) => sum + (parseFloat(w.distance) || 0), 0);
    if (walkDistanceEl) walkDistanceEl.textContent = `${totalDistance.toFixed(1)}km`;

    // 건강 기록
    const healthState = (typeof healthLogs !== 'undefined' && healthLogs)
        ? healthLogs
        : window.healthLogs;
    const healthHistory = (healthState && Array.isArray(healthState.history)) ? healthState.history : [];
    const monthlyLogs = healthHistory.filter(h => getRecordDate(h).startsWith(thisMonth));
    const logCountEl = document.getElementById('report-modal-log-count');
    if (logCountEl) logCountEl.textContent = `${monthlyLogs.length}회`;

    // AI 분석
    const analyses = (typeof getHealthAnalyses === 'function') ? getHealthAnalyses() : [];
    const monthlyAI = analyses.filter(a => getRecordDate(a).startsWith(thisMonth));
    const aiCountEl = document.getElementById('report-modal-ai-count');
    if (aiCountEl) aiCountEl.textContent = `${monthlyAI.length}회`;

    // 평균 식사량/음수량
    const last7Days = (typeof getLast7DaysHealthData === 'function') ? getLast7DaysHealthData() : [];
    const avgFood = last7Days.length ? Math.round(last7Days.reduce((s, d) => s + d.food, 0) / 7) : 0;
    const avgWater = last7Days.length ? Math.round(last7Days.reduce((s, d) => s + d.water, 0) / 7) : 0;

    const foodEl = document.getElementById('report-modal-food');
    const waterEl = document.getElementById('report-modal-water');
    if (foodEl) foodEl.textContent = avgFood ? `${avgFood}g` : '--g';
    if (waterEl) waterEl.textContent = avgWater ? `${avgWater}ml` : '--ml';

    // 일정 준수율 계산
    const careState = (typeof AppStore !== 'undefined' && AppStore.getState)
        ? (AppStore.getState('careSchedules') || {})
        : {};
    const careSchedules = Array.isArray(careState.schedules) ? careState.schedules : [];
    const completionHistory = Array.isArray(careState.completionHistory) ? careState.completionHistory : [];
    const monthlyCompletions = completionHistory.filter(item => getRecordDate(item).startsWith(thisMonth));
    const careRate = careSchedules.length > 0
        ? Math.min(100, Math.round((monthlyCompletions.length / careSchedules.length) * 100))
        : 0;
    const careRateEl = document.getElementById('report-modal-care-rate');
    if (careRateEl) careRateEl.textContent = `${careRate}%`;

    // ── 지난 달 대비 개인화 비교 ──────────────────────────────
    let ly = now.getFullYear(), lm = now.getMonth() - 1;
    if (lm < 0) { lm = 11; ly -= 1; }
    const lastMonth = `${ly}-${String(lm + 1).padStart(2, '0')}`;

    const lastWalks = walkList.filter(w => getRecordDate(w).startsWith(lastMonth));
    const lastDistance = lastWalks.reduce((s, w) => s + (parseFloat(w.distance) || 0), 0);
    const lastLogs = healthHistory.filter(h => getRecordDate(h).startsWith(lastMonth));

    const applyDelta = (id, cur, prev, unit, digits) => {
        const el = document.getElementById(id);
        if (!el) return;
        const d = cur - prev;
        const fmt = (n) => digits ? Math.abs(n).toFixed(digits) : Math.abs(n);
        if (d > 0.0001) { el.textContent = `▲ ${fmt(d)}${unit}`; el.style.color = '#059669'; }
        else if (d < -0.0001) { el.textContent = `▼ ${fmt(d)}${unit}`; el.style.color = '#dc2626'; }
        else { el.textContent = '– 동일'; el.style.color = '#9ca3af'; }
    };
    applyDelta('report-modal-walk-count-delta', monthlyWalks.length, lastWalks.length, '회', 0);
    applyDelta('report-modal-distance-delta', totalDistance, lastDistance, 'km', 1);
    applyDelta('report-modal-log-delta', monthlyLogs.length, lastLogs.length, '회', 0);
}

// 모달 템플릿
const MONTHLY_REPORT_MODAL = `
<div id="monthly-report-modal" class="fixed inset-0 bg-black/60 backdrop-blur-sm items-center justify-center z-[100] p-4 hidden">
    <div class="bg-white rounded-3xl max-w-2xl w-full max-h-[90vh] overflow-y-auto shadow-2xl">
        <!-- 헤더 -->
        <div class="sticky top-0 bg-gradient-to-r from-brand-500 to-brand-600 px-6 py-5 rounded-t-3xl">
            <button onclick="closeMonthlyReportModal()" class="absolute top-4 right-4 text-white/80 hover:text-white transition-colors">
                <i class="fa-solid fa-xmark text-xl"></i>
            </button>
            <div class="flex items-center gap-3">
                <div class="w-12 h-12 bg-white/20 rounded-xl flex items-center justify-center">
                    <i class="fa-solid fa-chart-line text-white text-xl"></i>
                </div>
                <div>
                    <h2 class="text-xl font-bold text-white">
                        <span id="report-modal-pet-name">댕이</span>의 <span id="report-modal-month">6월</span> 리포트
                    </h2>
                    <p class="text-sm text-white/90 mt-0.5">건강 · 활동 · 분석 종합</p>
                </div>
            </div>
        </div>

        <!-- 내용 -->
        <div class="p-6 space-y-6">

            <!-- 건강 점수 & 연속 기록 -->
            <div class="grid grid-cols-2 gap-4">
                <div class="card-modern bg-brand-50/50 p-5 text-center">
                    <div class="text-5xl font-bold text-brand-600 mb-3" id="report-modal-health-score">--</div>
                    <div class="text-sm font-semibold text-gray-700">건강 점수</div>
                    <div class="text-xs text-gray-500 mt-1">최근 7일 평균</div>
                    <div class="mt-3 pt-3 border-t border-brand-200">
                        <div class="text-xs text-gray-600">
                            <i class="fa-solid fa-chart-line text-brand-500 mr-1"></i>
                            식사·음수·배변 종합 평가
                        </div>
                    </div>
                </div>

                <div class="card-modern bg-emerald-50/50 p-5 text-center">
                    <div class="text-5xl font-bold text-emerald-600 mb-3" id="report-modal-streak">--일</div>
                    <div class="text-sm font-semibold text-gray-700">연속 기록</div>
                    <div class="text-xs text-gray-500 mt-1">꾸준히 기록 중!</div>
                    <div class="mt-3 pt-3 border-t border-emerald-200">
                        <div class="text-xs text-gray-600">
                            <i class="fa-solid fa-fire text-emerald-500 mr-1"></i>
                            매일 건강 기록 달성
                        </div>
                    </div>
                </div>
            </div>

            <!-- 지난 달 대비 (개인화 비교) -->
            <div class="card-modern bg-violet-50/40 p-4">
                <h3 class="text-sm font-bold text-gray-900 mb-3 flex items-center gap-2">
                    <i class="fa-solid fa-arrow-trend-up text-violet-500"></i>
                    지난 달 대비
                </h3>
                <div class="grid grid-cols-3 gap-2 text-center">
                    <div>
                        <div class="text-xs text-gray-500 mb-0.5">산책</div>
                        <div class="text-sm font-bold" id="report-modal-walk-count-delta">–</div>
                    </div>
                    <div>
                        <div class="text-xs text-gray-500 mb-0.5">거리</div>
                        <div class="text-sm font-bold" id="report-modal-distance-delta">–</div>
                    </div>
                    <div>
                        <div class="text-xs text-gray-500 mb-0.5">건강기록</div>
                        <div class="text-sm font-bold" id="report-modal-log-delta">–</div>
                    </div>
                </div>
            </div>

            <!-- 활동 통계 -->
            <div>
                <h3 class="text-base font-bold text-gray-900 mb-3 flex items-center gap-2">
                    <i class="fa-solid fa-running text-orange-500"></i>
                    활동 통계
                </h3>
                <div class="grid grid-cols-2 gap-3">
                    <div class="card-modern bg-orange-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">🚶</span>
                            <div class="text-2xl font-bold text-orange-600" id="report-modal-walk-count">0회</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">이번 달 산책</div>
                    </div>
                    <div class="card-modern bg-amber-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">📍</span>
                            <div class="text-2xl font-bold text-amber-600" id="report-modal-walk-distance">0.0km</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">총 산책 거리</div>
                    </div>
                </div>
            </div>

            <!-- 건강 관리 -->
            <div>
                <h3 class="text-base font-bold text-gray-900 mb-3 flex items-center gap-2">
                    <i class="fa-solid fa-heart-pulse text-sky-500"></i>
                    건강 관리
                </h3>
                <div class="grid grid-cols-2 gap-3">
                    <div class="card-modern bg-sky-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">📝</span>
                            <div class="text-2xl font-bold text-sky-600" id="report-modal-log-count">0회</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">건강 기록</div>
                    </div>
                    <div class="card-modern bg-rose-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">🤖</span>
                            <div class="text-2xl font-bold text-rose-600" id="report-modal-ai-count">0회</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">AI 분석</div>
                    </div>
                </div>
            </div>

            <!-- 영양 관리 -->
            <div>
                <h3 class="text-base font-bold text-gray-900 mb-3 flex items-center gap-2">
                    <i class="fa-solid fa-bowl-food text-amber-500"></i>
                    영양 관리
                </h3>
                <div class="grid grid-cols-2 gap-3">
                    <div class="card-modern bg-amber-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">🍖</span>
                            <div class="text-2xl font-bold text-amber-600" id="report-modal-food">--g</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">평균 식사량</div>
                        <div class="text-xs text-gray-500 mt-1">7일 평균</div>
                    </div>
                    <div class="card-modern bg-sky-50/50 p-4">
                        <div class="flex items-center justify-between mb-2">
                            <span class="text-3xl">💧</span>
                            <div class="text-2xl font-bold text-sky-600" id="report-modal-water">--ml</div>
                        </div>
                        <div class="text-sm font-semibold text-gray-700">평균 음수량</div>
                        <div class="text-xs text-gray-500 mt-1">7일 평균</div>
                    </div>
                </div>
            </div>

            <!-- 일정 준수율 -->
            <div class="card-modern bg-gradient-to-r from-emerald-50 to-teal-50 p-5">
                <div class="flex items-center justify-between">
                    <div>
                        <h3 class="text-base font-bold text-gray-900 mb-1">돌봄 일정 준수율</h3>
                        <p class="text-xs text-gray-600">예정된 일정 대비 완료율</p>
                    </div>
                    <div class="text-4xl font-bold text-emerald-600" id="report-modal-care-rate">--%</div>
                </div>
            </div>

            <!-- 닫기 버튼 -->
            <button onclick="closeMonthlyReportModal()" class="w-full btn-modern bg-brand-500 hover:bg-brand-600 text-white py-4 text-base font-bold">
                확인
            </button>
        </div>
    </div>
</div>
`;
