// today-care-card.js — 오늘의 케어 통합 카드 (백로그 나무 제안, P2)
// 홈(마이펫) 요약 카드 안에 산책·급여·케어예정·기분·이상신호를 한 줄로 요약해
// 각 탭을 일일이 열어보지 않아도 오늘 상태를 한눈에 파악하게 한다(앱 피로도 해소).
// 신규 저장소·라이브러리 없이 기존 모듈 값을 읽어 조립한다.
(function () {
    'use strict';

    function _todayStart() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d.getTime();
    }

    // 실제 기록의 id는 Date.now() 타임스탬프. 시드 데이터(id 401 등 작은 값)는
    // 오늘 시작 타임스탬프보다 작아 자연스럽게 제외된다.
    function _isTodayId(id) {
        const t = Number(id);
        return !isNaN(t) && t >= _todayStart();
    }

    function _walkStat() {
        const list = (typeof walks !== 'undefined' && Array.isArray(walks)) ? walks : [];
        let count = 0, dist = 0;
        list.forEach(w => {
            if (_isTodayId(w.id)) { count++; dist += parseFloat(w.distance) || 0; }
        });
        return count === 0
            ? { emoji: '🚶', label: '산책', value: '아직', done: false, tab: 'walk' }
            : { emoji: '🚶', label: '산책', value: `${count}회 · ${dist.toFixed(1)}km`, done: true, tab: 'walk' };
    }

    function _mealStat() {
        const list = (typeof meals !== 'undefined' && Array.isArray(meals)) ? meals : [];
        const count = list.filter(m => _isTodayId(m.id)).length;
        // 급여 기록 폼(meal-form)은 건강 탭에 있다 — 건강 탭으로 이동한 뒤 폼을 연다.
        // (탭 전환이 템플릿 렌더를 트리거하므로 폼 열기는 다음 틱으로 미룬다)
        const goLog = "switchTab('health'); setTimeout(() => { "
            + "if (typeof toggleMealForm === 'function') toggleMealForm(true); "
            + "const f = document.getElementById('meal-form'); "
            + "if (f) f.scrollIntoView({behavior:'smooth', block:'center'}); }, 250)";
        return count === 0
            ? { emoji: '🍽️', label: '급여', value: '아직', done: false, tab: 'health', action: goLog }
            : { emoji: '🍽️', label: '급여', value: `${count}회`, done: true, tab: 'health', action: goLog };
    }

    function _careStat() {
        if (typeof getTodaySchedules !== 'function') return null;
        const today = new Date().toISOString().split('T')[0];
        const pending = getTodaySchedules()
            .filter(s => !(s.lastCompleted && s.lastCompleted.startsWith(today)));
        return pending.length === 0
            ? { emoji: '💊', label: '케어', value: '완료', done: true, tab: 'health' }
            : { emoji: '💊', label: '케어', value: `${pending.length}건 남음`, done: false, tab: 'health' };
    }

    // 기분: 데일리 컨디션의 활력(activity) → 없으면 컨디션(condition) 순으로 읽는다.
    function _moodStat() {
        const today = (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today) ? healthLogs.today : {};
        const ACT = { high: '⚡ 활발', normal: '🙂 보통', low: '😴 처짐' };
        const COND = { happy: '😊 좋음', tired: '😪 지침', sick: '🤒 아픔' };
        if (today.activity && ACT[today.activity]) {
            return { emoji: '💛', label: '기분', value: ACT[today.activity], done: true, tab: 'health' };
        }
        if (today.condition && COND[today.condition]) {
            return { emoji: '💛', label: '기분', value: COND[today.condition], done: true, tab: 'health' };
        }
        return { emoji: '💛', label: '기분', value: '기록 전', done: false, tab: 'health' };
    }

    // 관찰 요약: wellness-anomaly의 분석 함수를 재사용해 소견 유무만 요약한다.
    // 라벨·값 문구는 판정("이상신호"/"이상 없음")이 아니라 관찰("건강 관찰"/
    // "특이사항 없음")로 통일한다(2026-08-12 오너 지시 "건강점수 이상신호도 통일") —
    // AI 사진분석에서 정리한 원칙(기록·관찰 우선, 진단·판정 지양)을 이 위젯에도 맞춘다.
    // wellness-anomaly.js·test_wellness_anomaly_flow.py의 카드 문구도 함께 맞췄다.
    function _anomalyStat() {
        const history = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        let flagged = false;
        try {
            if (typeof analyzeStool === 'function' && analyzeStool(history)) flagged = true;
            if (typeof analyzeUrine === 'function' && analyzeUrine(history)) flagged = true;
            if (typeof analyzeCondition === 'function' && (analyzeCondition(history) || []).length) flagged = true;
            if (typeof analyzeWellness === 'function' && (analyzeWellness(history) || []).length) flagged = true;
            if (typeof analyzeWeight === 'function' && typeof getWeightHistory === 'function'
                && analyzeWeight(getWeightHistory())) flagged = true;
        } catch (e) { /* 분석 실패 시 특이사항 없음으로 간주 */ }
        return flagged
            ? { emoji: '⚠️', label: '건강 관찰', value: '확인 필요', done: false, warn: true, tab: 'health' }
            : { emoji: '🩺', label: '건강 관찰', value: '특이사항 없음', done: true, tab: 'health' };
    }

    function _cell(s) {
        if (!s) return '';
        const valCls = s.warn ? 'text-amber-700' : (s.done ? 'text-gray-900' : 'text-gray-400');
        // 이 카드는 마이펫 탭에 있으므로 tab이 'mypet'이면 switchTab은 제자리 이동 =
        // 클릭해도 아무 반응이 없다(2026-07-25 오너 지적: "급여 클릭해도 아무 반응 없다").
        // 그런 셀은 탭 이동 대신 실제 기록 동작(action)을 부르게 한다.
        const handler = s.action || `switchTab('${s.tab}')`;
        return `
        <button type="button" onclick="${handler}"
            class="flex flex-col sm:flex-row sm:items-center sm:justify-center sm:gap-2 items-center gap-1 py-2 px-1 rounded-xl hover:bg-gray-50 active:scale-95 transition-all outline-none min-w-0">
            <span class="text-xl leading-none shrink-0">${s.emoji}</span>
            <span class="flex flex-col items-center sm:items-start min-w-0">
                <span class="text-[10px] font-semibold text-gray-400 leading-tight">${s.label}</span>
                <span class="text-xs font-black ${valCls} truncate max-w-full leading-tight">${s.value}</span>
            </span>
        </button>`;
    }

    function renderTodayCareCard() {
        const host = document.getElementById('today-care-strip');
        if (!host) return;
        // 추모 모드에서는 케어 유도를 띄우지 않는다 — 무지개다리를 건넌 아이에게
        // 산책·급여·투약을 독촉하는 건 위로가 아니라 상처다(2026-07-26 오너 지시).
        if (typeof isMemorialPet === 'function' && isMemorialPet()) {
            host.innerHTML = ''; host.hidden = true; return;
        }
        if (typeof getActivePet === 'function' && !getActivePet()) {
            host.innerHTML = ''; host.hidden = true; return;
        }
        // 이 요소는 원래 hidden을 쓰지 않았다 — 추모 가드가 hidden=true를 세우므로
        // 정상 경로에서 반드시 되돌려야 한다(안 되돌리면 추모 모드를 꺼도 영구히 숨는다).
        host.hidden = false;

        const stats = [_walkStat(), _mealStat(), _careStat(), _moodStat(), _anomalyStat()]
            .filter(Boolean);
        const cells = stats.map(_cell).join('');

        // 신규 사용자 유도: 핵심 활동(산책·급여·기분)이 모두 미기록이면, 회색 대시(-)
        // 상태로만 두지 않고 '데모 데이터 눌러보기' 마이크로카피를 옅게 노출해
        // 첫 화면 이탈을 줄인다(P3, 미오 제안). 기록이 하나라도 있으면 숨긴다.
        const core = stats.filter(s => s.label === '산책' || s.label === '급여' || s.label === '기분');
        const showDemoHint = core.length > 0 && core.every(s => !s.done)
            && typeof resetToRichDemoData === 'function';
        const demoHint = showDemoHint ? `
            <button type="button" onclick="resetToRichDemoData()"
                class="mt-2 w-full text-center text-[10px] text-gray-300 hover:text-brand-400 font-medium transition-colors">
                아직 기록이 없어요 — <span class="underline">데모 데이터 눌러보기</span>
            </button>` : '';

        // 예전엔 max-w-xl로 폭을 제한했는데, 데스크톱(1400px+)에서 오른쪽 2/3가 통째로
        // 비어 보였다(2026-07-26 오너 지적). 이제 전체 폭을 쓰되 칸 안을 가로 배치로
        // 바꿔 — 아이콘 옆에 라벨·값이 붙어 — 넓은 칸이 실제로 채워지게 한다.
        host.innerHTML = `
        <div class="px-4 py-3">
            <div class="flex items-center gap-1.5 mb-2">
                <span class="text-sm">📊</span>
                <span class="text-xs font-bold text-gray-700">오늘의 케어 요약</span>
                <span class="text-[10px] font-bold text-gray-400">읽기 전용</span>
            </div>
            <div class="grid grid-cols-5 gap-1">${cells}</div>${demoHint}
        </div>`;
    }

    window.renderTodayCareCard = renderTodayCareCard;
})();
