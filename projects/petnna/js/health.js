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
        // 활성 펫 기준이어야 한다 — pets[0]로 두면 펫을 전환해도 물 목표가
        // 첫 펫 체중에 고정된다(2026-07-26 2차 회의에서 테오가 발견).
        const pet = (typeof getActivePet === 'function') ? getActivePet()
            : ((typeof pets !== 'undefined' && pets.length > 0) ? pets[0] : null);
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
    // 펫 0마리면 위젯을 그리지 않고 등록 유도만 보인다 — 예전엔 '--g / 0회' 카드가
    // 그대로 떠서 신규 사용자가 무엇을 해야 할지 알 수 없었다(2026-07-26 2차 회의).
    const _pets = (typeof pets !== 'undefined' && Array.isArray(pets)) ? pets : [];
    const _empty = document.getElementById('health-empty-state');
    const _grid = document.getElementById('health-main-grid');
    if (_empty && _grid) {
        const none = _pets.length === 0;
        _empty.classList.toggle('hidden', !none);
        _grid.classList.toggle('hidden', none);
        if (none) return;   // 위젯 렌더 자체를 건너뛴다(빈 값 계산·경보 방지)
    }

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
    if (typeof renderMedicationLogTracker === 'function') renderMedicationLogTracker();
    if (typeof renderMedicalRecordsTimeline === 'function') renderMedicalRecordsTimeline();

    // 몸무게/QOL 주간 체크인 (백로그 나무_20260709_3, 오너 승인 2026-07-10)
    if (typeof QolCheckin !== 'undefined') QolCheckin.renderWidget('qol-checkin-widget');

    // BCS 체형 셀프체크 위저드 (백로그 나무_20260712143815, P3)
    if (typeof BcsWizard !== 'undefined') BcsWizard.renderWidget('bcs-wizard-widget');

    // 일일 급식·칼로리 트래커 (백로그 나무_20260712143815, P3)
    if (typeof CalorieTracker !== 'undefined') CalorieTracker.renderWidget('calorie-tracker-widget');

    // 맞춤 식단·급여량 추천 카드 (백로그 나무, P2 — 체형·체중·활동량 종합)
    if (typeof DietRecommend !== 'undefined') DietRecommend.renderWidget('diet-recommend-widget');

    // AI 맞춤 데일리 케어 팁 카드 (백로그 나무, P3 — 프로필 기반 오늘의 팁)
    if (typeof DailyCareTip !== 'undefined') DailyCareTip.renderWidget('daily-care-tip-widget');

    // 병원비 제보·비교 보드 (백로그 나무, P3)
    if (typeof VetCostBoard !== 'undefined') VetCostBoard.renderWidget('vet-cost-board-widget');

    // 반려동물 가계부 (백로그 나무, P3 — 카테고리별 지출·월별 집계)
    if (typeof ExpenseTracker !== 'undefined') ExpenseTracker.renderWidget('expense-tracker-widget');

    // 재고 소진 D-day 추적기 (백로그 나무, P2 — 사료·용품 잔량·재구매 넛지)
    if (typeof InventoryTracker !== 'undefined') InventoryTracker.renderWidget('inventory-tracker-widget');

    // 차트 및 캘린더 렌더링
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
    if (typeof updateHealthTutorialMainVisibility === 'function') updateHealthTutorialMainVisibility();

    // AI 사용 횟수 업데이트
    updateAiUsageCount();

    // 식사 일지 렌더링
    if (typeof renderMealLogsList === 'function') renderMealLogsList();

    // 카드 접기 적용 — 위젯 렌더가 다 끝난 뒤에 감싸야 한다(먼저 감싸면 각 렌더러가
    // innerHTML을 덮어쓰며 접기 UI를 날린다). 여러 번 불려도 이미 감싼 카드는 건너뛴다.
    setTimeout(function () {
        if (window.SectionCollapse) SectionCollapse.apply('health-main-grid');
    }, 0);

    // 데일리 컨디션 원탭 로그 위젯 (2026-07-26 '오늘의 기록' 카드 안으로 이동)
    if (typeof DailyCondition !== 'undefined') DailyCondition.renderWidget('daily-condition-widget');

    // 오늘 투약·케어 체크 — 2026-07-26 마이펫에서 이 탭으로 이동했다.
    // 호스트를 옮기면서 렌더 호출을 같이 안 옮기면 호스트만 있고 영영 비어 있게 된다.
    if (typeof renderCareCheckBanner === 'function') renderCareCheckBanner();

    // 예측 웰니스 이상감지 카드
    if (typeof renderWellnessCard === 'function') renderWellnessCard();

    // 주간 건강 변화 조기경보 리포트 카드
    if (typeof renderWeeklyReportCard === 'function') renderWeeklyReportCard();

    // 케어 스코어 & Proof-of-Care 공유 카드
    if (typeof renderCareScoreCard === 'function') renderCareScoreCard();

    // 상단 예측웰니스·주간리포트 접기: 실제 이상 소견(amber 경고 카드)이 있으면 모바일에서도
    // 자동으로 펼쳐 놓치지 않게 하고, 평상시엔 접힌 1줄 요약으로 둔다.
    // 데스크톱(lg=1024px+)에선 open 속성을 켜 본문을 실제로 펼친다 — CSS ::details-content
    // 오버라이드(Chrome131+ 전용)에만 의존하지 않고 브라우저 무관하게 노출(요약은 CSS로 숨김).
    var _topFold = document.getElementById('health-top-analysis');
    if (_topFold) _topFold.open = (window.innerWidth >= 1024) || !!_topFold.querySelector('.border-amber-200');

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

    // 빈 상태는 대시(--) 대신 흐린 회색 "미입력"으로 의도를 명확히 한다(미오 P3).
    if (foodEl) {
        foodEl.textContent = logs.food ? `${logs.food}g` : '미입력';
        foodEl.classList.toggle('text-gray-300', !logs.food);
    }
    if (waterEl) {
        waterEl.textContent = logs.water ? `${logs.water}ml` : '미입력';
        waterEl.classList.toggle('text-gray-300', !logs.water);
    }

}

// ── 식사·음수 인라인 스테퍼 (2026-07-28 회의 결정) ──────────────────────────
// 타일의 −/+ 로 오늘 섭취량을 바로 올리고 내린다. 저장 형식·경로는 모달과 완전히
// 같다(healthLogs.today → saveHealthHistoryToday) — 여기서 다른 형식으로 쓰면
// wellness-anomaly·주간리포트가 읽는 이력이 갈라진다(백호 지적).
//
// 상한은 모달 슬라이더와 동일(식사 500g / 음수 1000ml)하게 맞춘다.
const INTAKE_MAX = { food: 500, water: 1000 };

// 저장은 디바운스한다 — 연타할 때마다 saveState + Supabase 업로드가 나가면
// 원격 동기화와 경합한다(나무 지적, 2026-07-25 sync 되감기 교훈).
// 단 디바운스가 데이터를 삼키면 안 되므로, 화면을 떠날 때는 즉시 flush 한다.
let _intakeSaveTimer = null;

function _flushIntakeSave() {
    if (!_intakeSaveTimer) return;
    clearTimeout(_intakeSaveTimer);
    _intakeSaveTimer = null;
    if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
    else if (typeof saveState === 'function') saveState();
    if (typeof renderWellnessCard === 'function') renderWellnessCard();
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
}

function adjustTodayIntake(field, delta) {
    if (typeof healthLogs === 'undefined' || !healthLogs) return;
    if (!(field in INTAKE_MAX)) return;

    const dateStr = new Date().toISOString().split('T')[0];
    if (!healthLogs.today) healthLogs.today = { date: dateStr };

    const cur = parseInt(healthLogs.today[field], 10) || 0;
    const next = Math.max(0, Math.min(INTAKE_MAX[field], cur + delta));
    if (next === cur) return;                      // 0에서 빼기·상한에서 더하기는 무시

    healthLogs.today[field] = next;
    healthLogs.today.date = dateStr;

    updateTodayHealthDisplay();                    // 화면은 즉시
    if (_intakeSaveTimer) clearTimeout(_intakeSaveTimer);
    _intakeSaveTimer = setTimeout(() => {
        _intakeSaveTimer = null;
        if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
        else if (typeof saveState === 'function') saveState();
        if (typeof renderWellnessCard === 'function') renderWellnessCard();
        if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
        if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
        if (typeof showToast === 'function') showToast('오늘 기록 저장 완료! ✅');
    }, 700);
}

// 탭을 숨기거나 페이지를 떠날 때 미저장분을 흘리지 않는다.
if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') _flushIntakeSave();
    });
    window.addEventListener('pagehide', _flushIntakeSave);
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
        window.healthTrendChartMain = null;
    }

    // 데이터가 하나도 없으면 빈 그래프 대신 empty-state 안내를 보여준다.
    const container = canvas.parentElement;
    const hasData = data.some(d => d.food > 0 || d.water > 0);
    const emptyEl = container ? container.querySelector('.health-trend-empty') : null;

    if (!hasData) {
        canvas.style.display = 'none';
        if (container) {
            // empty 상태: 고정 높이의 절반 수준으로 압축 + 배경 톤 한 단계 연하게
            container.style.minHeight = '60px';
            container.style.maxHeight = 'none';
            container.classList.remove('from-sky-50', 'to-blue-50', 'border-sky-100');
            container.classList.add('from-gray-50', 'to-gray-50', 'border-gray-100');
        }
        if (container && !emptyEl) {
            const empty = document.createElement('div');
            empty.className = 'health-trend-empty flex flex-col items-center justify-center text-center h-full py-3';
            empty.innerHTML = `
                <span class="text-2xl mb-1">📊</span>
                <p class="text-xs font-bold text-gray-500">아직 표시할 데이터가 없어요</p>
                <p class="text-[10px] text-gray-400 mt-0.5">건강 기록을 남기면 7일 트렌드가 그려집니다</p>`;
            container.appendChild(empty);
        }
        return;
    }

    canvas.style.display = '';
    if (emptyEl) emptyEl.remove();
    if (container) {
        // 데이터 복귀 시 원래 톤·높이로 복원
        container.style.minHeight = '120px';
        container.style.maxHeight = '180px';
        container.classList.add('from-sky-50', 'to-blue-50', 'border-sky-100');
        container.classList.remove('from-gray-50', 'to-gray-50', 'border-gray-100');
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

    // 배변 캘린더는 **이 함수의 early return 위에서** 부른다. 아래 '기록 없음' 분기에서
    // return하는 자리에 이 호출이 있으면, 기록이 하나도 없을 때 배변 캘린더가 아예 안 돌아
    // 스스로 숨지 못하고 호스트 div의 border-t만 남는다 — 내용 없이 선 하나가 떠 있게 된다
    // (2026-08-02 병합 검토에서 실측: 빈 계정에서 18px 높이 + 1px 상단선).
    // 호출부를 renderHealthCalendarMain 바깥 5곳에 흩뿌리지 않는 이유는, 새 호출부가 생길 때
    // 한 곳만 빠지는 비대칭이 이 저장소에서 반복돼 왔기 때문이다.
    if (typeof renderPoopCalendarMain === 'function') renderPoopCalendarMain();

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

// 배변 건강 월간 캘린더 (이번 달, 배변 상태별 색상 코딩)
// healthLogs.history의 poop 값(normal/hard/soft/liquid)을 달력 칸에 색으로 표시한다.
// 색 어휘는 daily-condition.js·건강 리포트 PDF와 동일하게 맞춘다.
function renderPoopCalendarMain() {
    const el = document.getElementById('poop-calendar-main');
    if (!el) return;

    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const today = new Date();
    const year = today.getFullYear();
    const month = today.getMonth(); // 0-based
    const monthPrefix = `${year}-${String(month + 1).padStart(2, '0')}`;

    // 이번 달 배변 기록이 하나도 없으면 숨김 (공간 절약, 기존 캘린더와 동일 정책)
    const monthPoop = history.filter(h => h.date && h.date.startsWith(monthPrefix) && h.poop != null);
    if (monthPoop.length === 0) {
        el.innerHTML = '';
        el.style.display = 'none';
        return;
    }
    el.style.display = 'block';

    const POOP_STYLE = {
        normal: { bg: 'bg-emerald-400', label: '정상' },
        hard:   { bg: 'bg-amber-400',   label: '딱딱' },
        soft:   { bg: 'bg-orange-400',  label: '무름' },
        liquid: { bg: 'bg-rose-400',    label: '설사' },
    };

    const poopByDay = {};
    monthPoop.forEach(h => { poopByDay[h.date] = h.poop; });

    const firstWeekday = new Date(year, month, 1).getDay(); // 0=일
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const todayStr = today.toISOString().split('T')[0];

    const weekdayHeader = ['일', '월', '화', '수', '목', '금', '토']
        .map(d => `<div class="text-center text-[9px] font-bold text-gray-400">${d}</div>`).join('');

    let cells = '';
    for (let i = 0; i < firstWeekday; i++) cells += '<div></div>';
    for (let day = 1; day <= daysInMonth; day++) {
        const dateStr = `${monthPrefix}-${String(day).padStart(2, '0')}`;
        const poop = poopByDay[dateStr];
        const style = poop ? POOP_STYLE[poop] : null;
        const bg = style ? style.bg + ' text-white' : 'bg-gray-100 text-gray-400';
        const ring = dateStr === todayStr ? ' ring-2 ring-amber-400' : '';
        const title = style ? `${dateStr} · ${style.label}` : dateStr;
        cells += `<div class="aspect-square flex items-center justify-center rounded-md text-[10px] font-bold ${bg}${ring}" title="${title}">${day}</div>`;
    }

    const legend = Object.values(POOP_STYLE).map(s =>
        `<span class="w-3 h-3 rounded-sm ${s.bg} inline-block"></span><span class="text-[9px] text-gray-400 mr-2">${s.label}</span>`
    ).join('');

    el.innerHTML = `
        <div class="flex items-center gap-2 mb-2">
            <span class="text-base">💩</span>
            <span class="text-sm font-bold text-gray-900">배변 월간 캘린더</span>
            <span class="text-xs text-gray-400">${year}년 ${month + 1}월</span>
        </div>
        <div class="grid grid-cols-7 gap-1 mb-1">${weekdayHeader}</div>
        <div class="grid grid-cols-7 gap-1">${cells}</div>
        <div class="flex items-center gap-1 mt-2 flex-wrap">${legend}</div>`;
}
window.renderPoopCalendarMain = renderPoopCalendarMain;

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
