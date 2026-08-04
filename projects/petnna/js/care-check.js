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
        // 추모 모드에서는 케어 유도를 띄우지 않는다 — 무지개다리를 건넌 아이에게
        // 산책·급여·투약을 독촉하는 건 위로가 아니라 상처다(2026-07-26 오너 지시).
        if (typeof isMemorialPet === 'function' && isMemorialPet()) {
            host.innerHTML = ''; host.hidden = true; return;
        }
        const items = _pendingToday();
        if (items.length === 0) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        const hasMed = items.some(i => i.type === 'medicine');
        const wrap = hasMed ? 'bg-rose-50/50' : '';
        const badgeCls = hasMed ? 'bg-rose-100 text-rose-800' : 'bg-brand-50 text-brand-700';
        const titleEmoji = hasMed ? '💊' : '📋';

        const rows = items.map(i => {
            const icon = (typeof getCareTypeIcon === 'function') ? getCareTypeIcon(i.type) : '📋';
            // 투약(medicine)은 이행률 분모를 위해 '먹였어요'(taken)·'건너뜀'(skip)을 모두 기록한다.
            const actions = (i.type === 'medicine')
                ? `<button type="button" onclick="careCheckComplete(${i.id})"
                        class="px-2 py-1 bg-emerald-500 hover:bg-emerald-600 text-white text-[9px] font-black rounded-lg transition-all shrink-0">먹였어요</button>
                   <button type="button" onclick="careCheckSkip(${i.id})"
                        class="px-2 py-1 bg-gray-200 hover:bg-gray-300 text-gray-600 text-[9px] font-black rounded-lg transition-all shrink-0">건너뜀</button>`
                : `<button type="button" onclick="careCheckComplete(${i.id})"
                        class="px-2 py-1 bg-emerald-500 hover:bg-emerald-600 text-white text-[9px] font-black rounded-lg transition-all shrink-0">완료</button>`;
            return `
            <li class="flex items-center gap-2 text-xs text-gray-700">
                <span class="shrink-0">${icon}</span>
                <span class="font-black shrink-0">${i.time}</span>
                <span class="min-w-0 flex-1 truncate">${i.title}</span>
                ${actions}
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

    function _scheduleById(id) {
        if (typeof getTodaySchedules !== 'function') return null;
        return getTodaySchedules().find(s => s.id === id) || null;
    }

    // 원탭 완료: 기존 로직 재사용(completionHistory 적재 포함) 후 배너 갱신.
    // 투약이면 medication_logs에도 taken=true로 남긴다(이행 로그).
    function careCheckComplete(id) {
        const sch = _scheduleById(id);
        if (typeof completeCareSchedule === 'function') completeCareSchedule(id);
        if (sch && sch.type === 'medicine' && typeof recordMedicationLog === 'function') {
            recordMedicationLog(sch, true);
        }
        renderCareCheckBanner();
    }

    // 건너뜀: completionHistory엔 넣지 않고(준수한 게 아니므로) 이행 로그에만 taken=false로
    // 남긴다. lastCompleted를 찍어 오늘 배너에서만 내린다(약을 안 먹었다고 계속 독촉하지 않음).
    function careCheckSkip(id) {
        const sch = _scheduleById(id);
        if (sch && typeof recordMedicationLog === 'function') recordMedicationLog(sch, false);
        // lastCompleted만 조용히 찍어 오늘 배너에서 내린다(updateCareSchedule은
        // '수정 완료' 토스트를 띄워 건너뜀엔 부적절하므로 직접 상태만 갱신).
        if (typeof AppStore !== 'undefined') {
            const cs = AppStore.getState('careSchedules') || { schedules: [], completionHistory: [] };
            const t = (cs.schedules || []).find(s => s.id === id);
            if (t) { t.lastCompleted = new Date().toISOString(); AppStore.setState('careSchedules', cs); }
        }
        renderCareCheckBanner();
    }

    window.renderCareCheckBanner = renderCareCheckBanner;
    window.careCheckComplete = careCheckComplete;
    window.careCheckSkip = careCheckSkip;
})();
