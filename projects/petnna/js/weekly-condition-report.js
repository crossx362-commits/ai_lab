// weekly-condition-report.js — 주간 컨디션·급여·활동 요약 인사이트 카드 (백로그 회의 제안, P3)
// 기존 healthLogs.history(원탭 컨디션: appetite/activity/poop, 급여: food, 음수: water)만으로
// '이번 주(0~6일)' 추이를 긍정적으로 요약한다. 신규 컬럼·외부 네트워크 의존 0.
// weekly-report.js(지난주 대비 '변화 조기경보')와 채널이 다르다 — 이건 '이번 주 요약'이다.
(function () {
    'use strict';

    const DAY = 86400000;

    function _dayIndex(dateStr, now) {
        const t = Date.parse(dateStr);
        if (isNaN(t)) return null;
        const base = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
        const d = new Date(new Date(t).getFullYear(), new Date(t).getMonth(), new Date(t).getDate()).getTime();
        return Math.round((base - d) / DAY);
    }

    // 이번 주(0~6일) 행만. date 파싱 실패는 제외.
    function _thisWeekRows(rows, now) {
        return (rows || []).filter(r => {
            if (!r) return false;
            const i = _dayIndex(r.date, now);
            return i !== null && i >= 0 && i <= 6;
        });
    }

    function _avg(rows, key) {
        const vals = rows.filter(r => typeof r[key] === 'number' && r[key] > 0).map(r => r[key]);
        return vals.length ? Math.round(vals.reduce((s, x) => s + x, 0) / vals.length) : null;
    }

    // 가장 많이 기록된 값(최빈값). 기록 없으면 null.
    function _mode(rows, key) {
        const counts = {};
        rows.forEach(r => { const v = r[key]; if (v) counts[v] = (counts[v] || 0) + 1; });
        let best = null, bestN = 0;
        Object.keys(counts).forEach(v => { if (counts[v] > bestN) { best = v; bestN = counts[v]; } });
        return best;
    }

    // 순수 함수: 이번 주 요약. 테스트/재사용 용이.
    function summarizeWeek(history, now) {
        now = now || new Date();
        const rows = _thisWeekRows(history, now);
        const loggedDays = new Set(rows.map(r => r.date)).size;
        return {
            loggedDays,
            avgFood: _avg(rows, 'food'),
            avgWater: _avg(rows, 'water'),
            appetite: _mode(rows, 'appetite'),
            activity: _mode(rows, 'activity'),
        };
    }

    const APPETITE_TXT = { good: '좋음 😋', normal: '보통 🙂', low: '없음 😔' };
    const ACTIVITY_TXT = { high: '활발 ⚡', normal: '보통 🙂', low: '처짐 😴' };

    function _stat(label, value) {
        return `
        <div class="rounded-xl bg-white/70 border border-brand-100/60 px-3 py-2">
            <div class="text-[11px] text-gray-500">${label}</div>
            <div class="text-sm font-bold text-gray-900 mt-0.5">${value}</div>
        </div>`;
    }

    function renderWeeklyConditionReport() {
        const host = document.getElementById('weekly-condition-report-card');
        if (!host) return;
        const history = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        const s = summarizeWeek(history);

        if (s.loggedDays === 0) {
            host.innerHTML = `
            <div class="card-modern p-5 border border-brand-100/60">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">🗓️</div>
                    <div class="min-w-0 flex-1">
                        <h3 class="text-sm font-bold text-gray-900">이번 주 건강 요약</h3>
                        <p class="text-xs text-gray-500 mt-0.5">
                            컨디션·급여를 기록하면 이번 주 추이를 한눈에 요약해 드려요.
                        </p>
                    </div>
                </div>
            </div>`;
            return;
        }

        const stats = [];
        if (s.avgFood !== null) stats.push(_stat('평균 급여', `${s.avgFood}g`));
        if (s.avgWater !== null) stats.push(_stat('평균 음수', `${s.avgWater}ml`));
        if (s.appetite) stats.push(_stat('주로 식욕', APPETITE_TXT[s.appetite] || s.appetite));
        if (s.activity) stats.push(_stat('주로 활력', ACTIVITY_TXT[s.activity] || s.activity));

        host.innerHTML = `
        <div class="card-modern p-5 border border-brand-100/60 bg-brand-50/30">
            <div class="flex items-center gap-3 mb-3">
                <div class="text-2xl">🗓️</div>
                <div class="min-w-0 flex-1">
                    <h3 class="text-sm font-bold text-gray-900">이번 주 건강 요약</h3>
                    <p class="text-xs text-gray-500 mt-0.5">최근 7일 동안 <b class="text-brand-600">${s.loggedDays}일</b> 기록했어요.</p>
                </div>
                <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-brand-50 text-brand-700">${s.loggedDays}/7일</span>
            </div>
            <div class="grid grid-cols-2 gap-2">${stats.join('')}</div>
        </div>`;
    }

    window.summarizeWeek = summarizeWeek;
    window.renderWeeklyConditionReport = renderWeeklyConditionReport;
})();
