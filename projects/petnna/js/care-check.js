// care-check.js — 오늘의 투약·케어 체크 배너 (백로그 나무 제안, P2)
// 홈(마이펫) 상단에 care-scheduler의 오늘 due 항목(투약 우선)을 노출하고,
// 배너에서 바로 원탭 완료 → completeCareSchedule()이 completionHistory에 적재한다.
// 신규 인프라·라이브러리 없이 순수 JS. 챙길 게 있을 때만 노출(과잉 노출 방지).
(function () {
    'use strict';

    const MAX_ITEMS = 5;

    // 오늘 due & 아직 오늘 완료하지 않은 항목. 투약(medicine)을 맨 앞으로.
    function _pendingToday() {
        if (typeof getTodaySchedules !== 'function') return [];
        const today = new Date().toISOString().split('T')[0];
        return getTodaySchedules()
            .filter(s => !(s.lastCompleted && s.lastCompleted.startsWith(today)))
            .sort((a, b) => {
                const am = a.type === 'medicine' ? 0 : 1;
                const bm = b.type === 'medicine' ? 0 : 1;
                if (am !== bm) return am - bm;
                return String(a.time).localeCompare(String(b.time));
            })
            .slice(0, MAX_ITEMS);
    }

    function renderCareCheckBanner() {
        const host = document.getElementById('care-check-banner');
        if (!host) return;
        const items = _pendingToday();
        if (items.length === 0) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        const hasMed = items.some(i => i.type === 'medicine');
        const wrap = hasMed ? 'bg-rose-50/50' : '';
        const badgeCls = hasMed ? 'bg-rose-100 text-rose-800' : 'bg-brand-50 text-brand-700';
        const titleEmoji = hasMed ? '💊' : '📋';

        const rows = items.map(i => {
            const icon = (typeof getCareTypeIcon === 'function') ? getCareTypeIcon(i.type) : '📋';
            return `
            <li class="flex items-center gap-2 text-xs text-gray-700">
                <span class="shrink-0">${icon}</span>
                <span class="font-black shrink-0">${i.time}</span>
                <span class="min-w-0 flex-1 truncate">${i.title}</span>
                <button type="button" onclick="careCheckComplete(${i.id})"
                    class="px-2 py-1 bg-emerald-500 hover:bg-emerald-600 text-white text-[9px] font-black rounded-lg transition-all shrink-0">
                    완료
                </button>
            </li>`;
        }).join('');

        host.innerHTML = `
        <div class="p-3.5 ${wrap}">
            <div class="flex items-center gap-2 mb-2">
                <span class="text-xl shrink-0">${titleEmoji}</span>
                <span class="text-sm font-bold text-gray-900 flex-1">오늘의 투약·케어 체크</span>
                <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold ${badgeCls} shrink-0">${items.length}건</span>
            </div>
            <ul class="space-y-1.5">${rows}</ul>
        </div>`;
    }

    // 원탭 완료: 기존 로직 재사용(completionHistory 적재 포함) 후 배너 갱신.
    function careCheckComplete(id) {
        if (typeof completeCareSchedule === 'function') completeCareSchedule(id);
        renderCareCheckBanner();
    }

    window.renderCareCheckBanner = renderCareCheckBanner;
    window.careCheckComplete = careCheckComplete;
})();
