// daily-condition.js — 데일리 컨디션 원탭 로그 (백로그 나무 제안, P3)
// 홈(건강 탭)에서 배변 상태·식욕·활력을 원탭 이모지로 기록한다.
// 기록은 healthLogs.today/history에 그대로 쌓여 wellness-anomaly 이상감지
// (배변 연속 이상 + 식욕·활력 연속 저하)의 입력이 된다. 신규 저장소 없음.
(function () {
    'use strict';

    // 필드별 원탭 옵션. poop 값(normal/hard/liquid)은 기존 건강 기록 모달·
    // wellness-anomaly의 analyzeStool과 동일 어휘를 그대로 쓴다.
    const GROUPS = [
        {
            field: 'poop', label: '배변', icon: '💩',
            options: [
                { value: 'normal', emoji: '💩', text: '정상' },
                { value: 'hard', emoji: '🪨', text: '딱딱' },
                { value: 'liquid', emoji: '💦', text: '무름' },
            ],
        },
        {
            field: 'appetite', label: '식욕', icon: '🍽️',
            options: [
                { value: 'good', emoji: '😋', text: '좋음' },
                { value: 'normal', emoji: '🙂', text: '보통' },
                { value: 'low', emoji: '😔', text: '없음' },
            ],
        },
        {
            field: 'activity', label: '활력', icon: '⚡',
            options: [
                { value: 'high', emoji: '⚡', text: '활발' },
                { value: 'normal', emoji: '🙂', text: '보통' },
                { value: 'low', emoji: '😴', text: '처짐' },
            ],
        },
    ];

    let _hostId = 'daily-condition-widget';

    function _today() {
        const logs = (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today) ? healthLogs.today : {};
        return logs || {};
    }

    function _btn(field, opt, selected) {
        const on = selected
            ? 'border-brand-500 bg-brand-50 text-brand-700 ring-2 ring-brand-200 shadow-sm'
            : 'border-gray-200 bg-white text-gray-600 hover:border-brand-300';
        return `<button type="button" aria-pressed="${selected ? 'true' : 'false'}"
            onclick="DailyCondition.set('${field}','${opt.value}')"
            class="flex-1 flex flex-col items-center gap-0.5 py-2 rounded-xl border transition-all outline-none ${on}">
            <span class="text-xl">${opt.emoji}</span>
            <span class="text-[11px] font-bold">${opt.text}</span>
        </button>`;
    }

    function renderWidget(hostId) {
        if (hostId) _hostId = hostId;
        const host = document.getElementById(_hostId);
        if (!host) return;
        const today = _today();
        const rows = GROUPS.map(g => `
            <div>
                <div class="flex items-center gap-1.5 mb-1.5">
                    <span class="text-sm">${g.icon}</span>
                    <span class="text-xs font-bold text-gray-700">${g.label}</span>
                </div>
                <div class="flex gap-2">
                    ${g.options.map(o => _btn(g.field, o, today[g.field] === o.value)).join('')}
                </div>
            </div>`).join('');
        host.innerHTML = `
        <div class="card-modern p-5 border border-brand-100/60">
            <div class="flex items-center gap-3 mb-3">
                <div class="text-2xl">📝</div>
                <div class="min-w-0">
                    <h3 class="text-sm font-bold text-gray-900">오늘의 컨디션 원탭 기록</h3>
                    <p class="text-xs text-gray-500 mt-0.5">배변·식욕·활력을 한 번의 탭으로 기록해요. 예측 웰니스에 반영됩니다.</p>
                </div>
            </div>
            <div class="space-y-3">${rows}</div>
        </div>`;
    }

    // 원탭 저장: 같은 값을 다시 누르면 해제(토글). healthLogs.today/history에
    // 저장해 wellness-anomaly가 바로 읽을 수 있게 한다.
    function set(field, value) {
        if (typeof healthLogs === 'undefined' || !healthLogs) return;
        const dateStr = new Date().toISOString().split('T')[0];
        if (!healthLogs.today) healthLogs.today = { date: dateStr };
        healthLogs.today[field] = (healthLogs.today[field] === value) ? null : value;
        healthLogs.today.date = dateStr;
        if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
        else if (typeof saveState === 'function') saveState();

        renderWidget(_hostId);
        if (typeof renderWellnessCard === 'function') renderWellnessCard();
        if (typeof updateHealthQuickSummary === 'function') updateHealthQuickSummary();
        if (typeof showToast === 'function') showToast('오늘 컨디션 기록 완료! ✅');
    }

    window.DailyCondition = { renderWidget, set };
})();
