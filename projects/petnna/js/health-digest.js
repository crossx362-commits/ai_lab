// health-digest.js — 홈 건강 조기감지 다이제스트 (백로그 나무 제안, P2)
// 홈(마이펫) 대시보드 상단에 최근 건강 급변을 한 줄로 요약한 위험 배지·배너를 띄운다.
// 신규 인프라·라이브러리 없이 순수 JS. 판정 로직은 재사용:
//   - wellness-anomaly.js  analyzeWellness(history)         (일간 z-score 급변)
//   - weekly-report.js     analyzeWeekly(history, weight)   (지난주 대비 ±20% 변화)
//   - 연속 결식: 최근 며칠 연속 식사 미기록/0 (여기서 직접 계산)
// 배너는 위험이 있을 때만 눈에 띄게 노출하고(과잉 경보 방지), 탭하면 건강 탭으로 이동한다.
(function () {
    'use strict';

    const FAST_STREAK_MIN = 2;   // 이 일수 이상 연속 결식이면 고위험 경보

    function _history() {
        return (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.history) ? healthLogs.history : [];
    }
    function _weight() {
        return (typeof getWeightHistory === 'function') ? getWeightHistory() : [];
    }

    // 오늘부터 거슬러 연속으로 식사가 미기록(또는 0)인 일수. history는 최신순으로 저장됨.
    function _fastStreak(history) {
        let n = 0;
        for (const d of (history || [])) {
            const v = d && d.food;
            if (typeof v === 'number' && v > 0) break;
            n++;
        }
        return n;
    }

    // 순수 함수: 로그 → 다이제스트 { level, summary, count } 또는 null(표본 부족/데이터 없음).
    function buildHealthDigest(history, weightHistory) {
        history = history || [];
        weightHistory = weightHistory || [];
        const daily = (typeof analyzeWellness === 'function') ? analyzeWellness(history) : [];
        const weekly = (typeof analyzeWeekly === 'function') ? analyzeWeekly(history, weightHistory) : [];
        const streak = _fastStreak(history);

        const risks = [];
        if (streak >= FAST_STREAK_MIN) {
            risks.push({ high: true, text: `${streak}일 연속 식사 기록이 없어요` });
        }
        for (const f of weekly) {
            const dir = f.direction === 'up' ? '증가' : '감소';
            risks.push({ high: f.metric === 'weight' || f.direction === 'down', text: `${f.label} 지난주 대비 ${Math.abs(f.pct)}% ${dir}` });
        }
        for (const f of daily) {
            const dir = f.direction === 'up' ? '급증' : '급감';
            risks.push({ high: f.direction === 'down', text: `${f.label} 최근 평소보다 ${Math.abs(f.pct)}% ${dir}` });
        }

        // 데이터가 사실상 없으면(기록 0) 배너 자체를 숨긴다.
        const hasData = history.some(d => d && ((typeof d.food === 'number' && d.food > 0) || (typeof d.water === 'number' && d.water > 0)))
            || weightHistory.length > 0;
        if (!hasData) return null;

        if (risks.length === 0) return { level: 'ok', summary: '최근 식사·음수·체중이 평소 범위 안에 있어요', count: 0 };

        const high = risks.some(r => r.high);
        return { level: high ? 'high' : 'watch', summary: risks[0].text, count: risks.length };
    }

    function _goHealth() {
        if (typeof switchTab === 'function') switchTab('health');
    }

    function renderHealthDigestBanner() {
        const host = document.getElementById('health-digest-banner');
        if (!host) return;
        const d = buildHealthDigest(_history(), _weight());
        if (!d) { host.innerHTML = ''; return; }

        if (d.level === 'ok') {
            host.innerHTML = `
            <button type="button" onclick="renderHealthDigestGo()" class="w-full text-left card-modern p-3.5 border border-emerald-100 flex items-center gap-3 hover:border-emerald-200 transition-colors">
                <span class="text-xl">🩺</span>
                <span class="min-w-0 flex-1 text-xs font-medium text-gray-600 truncate">건강 조기감지 · ${d.summary}</span>
                <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 shrink-0">이상 없음</span>
            </button>`;
            return;
        }

        const high = d.level === 'high';
        const wrap = high ? 'border-red-200 bg-red-50/50 hover:border-red-300' : 'border-amber-200 bg-amber-50/50 hover:border-amber-300';
        const badgeCls = high ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-800';
        const badgeTxt = high ? '주의 필요' : '관찰 권장';
        const emoji = high ? '🚨' : '⚠️';
        const titleCls = high ? 'text-red-900' : 'text-amber-900';
        const more = d.count > 1 ? ` 외 ${d.count - 1}건` : '';
        host.innerHTML = `
        <button type="button" onclick="renderHealthDigestGo()" class="w-full text-left card-modern p-3.5 border ${wrap} flex items-center gap-3 transition-colors">
            <span class="text-xl shrink-0">${emoji}</span>
            <span class="min-w-0 flex-1">
                <span class="block text-xs font-bold ${titleCls} truncate">${d.summary}${more}</span>
                <span class="block text-[11px] text-gray-500 mt-0.5">건강 탭에서 자세히 확인하기 →</span>
            </span>
            <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold ${badgeCls} shrink-0">${badgeTxt}</span>
        </button>`;
    }

    window.buildHealthDigest = buildHealthDigest;
    window.renderHealthDigestBanner = renderHealthDigestBanner;
    window.renderHealthDigestGo = _goHealth;
})();
