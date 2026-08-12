// daily-condition.js — 데일리 컨디션 원탭 로그 (백로그 나무 제안, P3)
// 홈(건강 탭)에서 배변 상태·식욕·활력을 원탭 이모지로 기록한다.
// 기록은 healthLogs.today/history에 그대로 쌓여 wellness-anomaly 이상감지
// (배변 연속 이상 + 식욕·활력 연속 저하)의 입력이 된다. 신규 저장소 없음.
(function () {
    'use strict';

    // 필드별 원탭 옵션. poop 값(normal/hard/liquid)은 기존 건강 기록 모달·
    // wellness-anomaly의 analyzeStool과 동일 어휘를 그대로 쓴다.
    //
    // detail:true인 그룹(배변 색·소변 색)은 기본 접힘이다(2026-07-28 회의 만장일치).
    // 매일 누르는 건 배변·식욕·활력이고, 색은 이상이 있을 때만 본다 — 5행을 늘 펼쳐
    // 두면 카드 최대 블록(371px)이 된다. 접되 반드시 지킬 것:
    //  · 항상 보이는 펼침 버튼을 둔다 — 진입점 없는 기능은 렌더돼도 도달 불가다
    //    (2026-07-25 설정 탭이 모바일에서 진입점 0이던 사고와 같은 계열).
    //  · 이미 기록된 값이 있으면 자동으로 펼친다 — 사용자가 남긴 이상 소견을
    //    접어서 숨기면 '기록이 사라진 것'과 구분되지 않는다.
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
            field: 'poopColor', label: '배변 색', icon: '🎨', detail: true,
            options: [
                { value: 'normal', emoji: '🟤', text: '갈색' },
                { value: 'black', emoji: '⚫', text: '검은색' },
                { value: 'red', emoji: '🔴', text: '붉은색' },
                { value: 'white', emoji: '⚪', text: '회백색' },
            ],
        },
        {
            field: 'urine', label: '소변 색', icon: '🚽', detail: true,
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

    // 항목 수가 홀수면 2열 그리드에서 마지막 하나가 홀로 남아 오른쪽 절반이 빈다
    // (봄이 layout_waste_checks가 '오늘의 기록 49%'로 잡아낸 실제 소견, 2026-07-26).
    // 홀수일 때 마지막 항목만 두 칸을 쓰게 해 행을 채운다.
    function _rows(groups, today) {
        return groups.map((g, gi) => `
            <div class="${(gi === groups.length - 1 && groups.length % 2 === 1) ? 'sm:col-span-2' : ''}">
                <div class="flex items-center gap-1.5 mb-1.5 pb-1 border-b border-gray-100">
                    <span class="text-sm">${g.icon}</span>
                    <span class="text-xs font-bold text-gray-700">${g.label}</span>
                </div>
                <div class="flex gap-2">
                    ${g.options.map(o => _btn(g.field, o, today[g.field] === o.value)).join('')}
                </div>
            </div>`).join('');
    }

    // 접이식 상태. null이면 '자동' — 기록된 색 값이 있으면 펼치고 없으면 접는다.
    // 사용자가 직접 누르면 그 선택(true/false)이 이 세션 동안 우선한다.
    let _detailOpen = null;

    function _detailHasValue(today) {
        return GROUPS.filter(g => g.detail).some(g => !!today[g.field]);
    }

    function renderWidget(hostId) {
        if (hostId) _hostId = hostId;
        const host = document.getElementById(_hostId);
        if (!host) return;
        const today = _today();
        const mainGroups = GROUPS.filter(g => !g.detail);
        const detailGroups = GROUPS.filter(g => g.detail);
        const hasDetail = _detailHasValue(today);
        const open = (_detailOpen === null) ? hasDetail : _detailOpen;

        // 2026-07-26 '오늘의 기록' 카드 안으로 들어갔다 — 자체 카드 테두리·제목을 유지하면
        // 카드 속 카드가 되고 "오늘의 기록 / 오늘의 컨디션 원탭 기록" 제목이 겹친다.
        // 같은 카드의 '7일 건강 트렌드'와 동일한 소제목 행 패턴으로 맞춘다.
        host.innerHTML = `
        <div class="pt-4 border-t border-gray-100">
            <div class="flex items-center gap-2 mb-3">
                <span class="text-xl">📝</span>
                <span class="text-sm font-bold text-gray-900">원탭 컨디션</span>
                <span class="text-[10px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">입력</span>
                <span class="text-xs text-gray-400 truncate">배변·식욕·활력</span>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">${_rows(mainGroups, today)}</div>

            <button type="button" id="condition-detail-toggle" aria-expanded="${open ? 'true' : 'false'}"
                aria-controls="condition-detail-panel" onclick="DailyCondition.toggleDetail()"
                class="w-full mt-3 flex items-center justify-center gap-1.5 py-2 rounded-xl border border-dashed
                       border-gray-200 text-gray-500 hover:border-brand-300 hover:text-brand-600
                       transition-colors outline-none">
                <span class="text-xs font-bold">🎨 배변·소변 색 ${open ? '접기' : '기록하기'}</span>
                ${hasDetail && !open ? '<span class="text-[10px] font-bold text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">기록됨</span>' : ''}
                <i class="fa-solid fa-chevron-${open ? 'up' : 'down'} text-[10px]"></i>
            </button>
            <!-- 숨길 때 hidden 속성만 쓰면 안 된다 — 이 패널은 Tailwind .grid를 달고 있고,
                 클래스 선택자(.grid{display:grid})가 브라우저 기본 [hidden]{display:none}을
                 이겨서 그대로 보인다(2026-07-28 실측으로 잡음). 인라인 style이 확실하다.
                 hidden 속성은 접근성·의미 표시용으로 같이 남긴다. -->
            <div id="condition-detail-panel" ${open ? '' : 'hidden style="display:none"'}
                class="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-3">${_rows(detailGroups, today)}</div>
        </div>`;
    }

    // 펼침/접힘 토글. 접어도 값은 그대로 남는다(표시만 숨김).
    function toggleDetail() {
        const panel = document.getElementById('condition-detail-panel');
        // offsetParent로 '실제로 보이는가'를 본다 — hidden 속성만 보면 위 주석의
        // display 우선순위 함정에 다시 걸린다.
        _detailOpen = panel ? (panel.offsetParent === null) : true;
        renderWidget(_hostId);
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

    window.DailyCondition = { renderWidget, set, toggleDetail };
})();
