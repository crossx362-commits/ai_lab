// care-nudge.js — 선제 케어 넛지 카드 (백로그 나무 제안, P3)
// 홈(마이펫) 상단에 '오늘 챙길 것'을 선제 노출한다. 판정은 기존 로직만 재사용:
//   - wellness-anomaly.js  analyzeWellness / analyzeStool  (건강 미세변화·배변 이상)
//   - care-scheduler.js    getTodaySchedules              (오늘 예정된 돌봄 일정)
// 신규 인프라·라이브러리 없이 순수 JS. 챙길 것이 있을 때만 노출(과잉 노출 방지),
// 탭하면 건강 탭(웰니스 카드·돌봄 스케줄러가 있는 곳)으로 이동한다.
(function () {
    'use strict';

    const MAX_ITEMS = 4;   // 카드에 노출할 최대 항목 수

    function _history() {
        return (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.history) ? healthLogs.history : [];
    }

    // 순수 함수: 건강 로그 → 넛지 항목 배열. 챙길 게 없으면 빈 배열.
    // 돌봄 일정(pendingCare)은 바로 위 '오늘의 투약·케어 체크' 카드에서 이미 보여주므로 중복 노출하지 않는다.
    function buildCareNudge(history) {
        const items = [];

        // 건강 이상: 배변 우선, 다음 z-score 급변
        const stool = (typeof analyzeStool === 'function') ? analyzeStool(history) : null;
        if (stool) {
            items.push({ emoji: stool.emoji, text: `${stool.label} ${stool.days}일 연속 — 컨디션 살펴보기`, urgent: true });
        }
        const daily = (typeof analyzeWellness === 'function') ? analyzeWellness(history) : [];
        for (const f of daily) {
            const dir = f.direction === 'up' ? '급증' : '급감';
            items.push({ emoji: f.emoji, text: `${f.label} 평소보다 ${Math.abs(f.pct)}% ${dir} — 확인하기`, urgent: f.direction === 'down' });
        }

        return items.slice(0, MAX_ITEMS);
    }

    function _goHealth() {
        if (typeof switchTab === 'function') switchTab('health');
    }

    function renderCareNudgeBanner() {
        const host = document.getElementById('care-nudge-banner');
        if (!host) return;
        const items = buildCareNudge(_history());
        if (items.length === 0) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        const urgent = items.some(i => i.urgent);
        const wrap = urgent ? 'bg-amber-50/50 hover:bg-amber-50' : 'hover:bg-gray-50';
        const badgeCls = urgent ? 'bg-amber-100 text-amber-800' : 'bg-brand-50 text-brand-700';
        const rows = items.map(i => `
            <li class="flex items-start gap-2 text-xs text-gray-700">
                <span class="mt-0.5 shrink-0">${i.emoji}</span>
                <span class="min-w-0 flex-1 truncate">${i.text}</span>
            </li>`).join('');
        host.innerHTML = `
        <button type="button" onclick="renderCareNudgeGo()" class="w-full text-left p-3.5 ${wrap} transition-colors">
            <div class="flex items-center gap-2 mb-2">
                <span class="text-xl shrink-0">📌</span>
                <span class="text-sm font-bold text-gray-900 flex-1">오늘 챙길 것</span>
                <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold ${badgeCls} shrink-0">${items.length}건</span>
            </div>
            <ul class="space-y-1.5 pl-1">${rows}</ul>
        </button>`;
    }

    window.buildCareNudge = buildCareNudge;
    window.renderCareNudgeBanner = renderCareNudgeBanner;
    window.renderCareNudgeGo = _goHealth;
})();
