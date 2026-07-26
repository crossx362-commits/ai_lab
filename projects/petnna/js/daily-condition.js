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
            field: 'poopColor', label: '배변 색', icon: '🎨',
            options: [
                { value: 'normal', emoji: '🟤', text: '갈색' },
                { value: 'black', emoji: '⚫', text: '검은색' },
                { value: 'red', emoji: '🔴', text: '붉은색' },
                { value: 'white', emoji: '⚪', text: '회백색' },
            ],
        },
        {
            field: 'urine', label: '소변 색', icon: '🚽',
            options: [
                { value: 'normal', emoji: '🟡', text: '연노랑' },
                { value: 'dark', emoji: '🟠', text: '진한색' },
                { value: 'red', emoji: '🔴', text: '붉은색' },
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
        // 항목이 5개라 2열 그리드에선 마지막 하나가 홀로 남아 오른쪽 절반이 빈다
        // (봄이 layout_waste_checks가 '오늘의 기록 49%'로 잡아낸 실제 소견, 2026-07-26).
        // 홀수 번째 마지막 항목은 두 칸을 쓰게 해 행을 채운다.
        const rows = GROUPS.map((g, gi) => `
            <div class="${(gi === GROUPS.length - 1 && GROUPS.length % 2 === 1) ? 'sm:col-span-2' : ''}">
                <div class="flex items-center gap-1.5 mb-1.5">
                    <span class="text-sm">${g.icon}</span>
                    <span class="text-xs font-bold text-gray-700">${g.label}</span>
                </div>
                <div class="flex gap-2">
                    ${g.options.map(o => _btn(g.field, o, today[g.field] === o.value)).join('')}
                </div>
            </div>`).join('');
        // 2026-07-26 '오늘의 기록' 카드 안으로 들어갔다 — 자체 카드 테두리·제목을 유지하면
        // 카드 속 카드가 되고 "오늘의 기록 / 오늘의 컨디션 원탭 기록" 제목이 겹친다.
        // 같은 카드의 '7일 건강 트렌드'와 동일한 소제목 행 패턴으로 맞춘다.
        host.innerHTML = `
        <div class="pt-4 border-t border-gray-100">
            <div class="flex items-center gap-2 mb-3">
                <span class="text-xl">📝</span>
                <span class="text-sm font-bold text-gray-900">원탭 컨디션</span>
                <span class="text-xs text-gray-400 truncate">배변·소변 색·식욕·활력</span>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">${rows}</div>
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
