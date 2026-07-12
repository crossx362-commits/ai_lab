// weekly-report.js — 주간 건강 변화 조기경보 리포트 (백로그 나무 제안, P2)
// 기존 로그(식욕=food, 음수=water, 체중=weight)를 '이번 주(0~6일)' vs '지난 주(7~13일)'로
// 비교해 지난주 대비 변화(예: 식욕 감소)를 감지하고 관찰 권장 카드를 만든다.
// 신규 인프라·라이브러리 없이 순수 JS. wellness-anomaly.js(일간 z-score)와 별개 채널.
//
// 설계 원칙(wellness-anomaly.js 가드레일과 동일 계열):
//  - 두 주 모두 기록이 있어야 비교(한쪽만 있으면 오탐이라 판정 억제)
//  - 확정 변화만 카드로 안내(과잉 경보 방지), 토스트 없음(일간 카드가 이미 담당)
(function () {
    'use strict';

    const DAY = 86400000;

    // 변화 임계: 이보다 커야 '변화 감지'. 체중은 생리적으로 작은 변동도 의미가 커 낮게.
    const METRICS = [
        { key: 'food',   label: '식욕',   unit: 'g',  emoji: '🍚', threshold: 20, source: 'history' },
        { key: 'water',  label: '음수량', unit: 'ml', emoji: '💧', threshold: 20, source: 'history' },
        { key: 'weight', label: '체중',   unit: 'kg', emoji: '⚖️', threshold: 5,  source: 'weight'  },
    ];

    function _dayIndex(dateStr, now) {
        // dateStr(YYYY-MM-DD) 기준 며칠 전인지(오늘=0). 파싱 실패 시 null.
        const t = Date.parse(dateStr);
        if (isNaN(t)) return null;
        const base = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
        const d = new Date(new Date(t).getFullYear(), new Date(t).getMonth(), new Date(t).getDate()).getTime();
        return Math.round((base - d) / DAY);
    }

    function _mean(a) { return a.reduce((s, x) => s + x, 0) / a.length; }

    // 특정 metric의 이번주/지난주 평균(기록 값 >0만). 값 없으면 null.
    function _windowAvg(rows, key, now, from, to) {
        const vals = (rows || [])
            .filter(r => r && typeof r[key] === 'number' && r[key] > 0)
            .filter(r => { const i = _dayIndex(r.date, now); return i !== null && i >= from && i <= to; })
            .map(r => r[key]);
        return vals.length ? _mean(vals) : null;
    }

    // 순수 함수: 로그 → 주간 변화 소견 배열. 테스트/재사용 용이.
    function analyzeWeekly(history, weightHistory, now) {
        now = now || new Date();
        const findings = [];
        for (const m of METRICS) {
            const rows = m.source === 'weight' ? weightHistory : history;
            const thisWeek = _windowAvg(rows, m.key, now, 0, 6);
            const lastWeek = _windowAvg(rows, m.key, now, 7, 13);
            if (thisWeek === null || lastWeek === null || lastWeek === 0) continue;
            const pct = ((thisWeek - lastWeek) / lastWeek) * 100;
            if (Math.abs(pct) < m.threshold) continue;
            const round = m.key === 'weight' ? (v => Math.round(v * 10) / 10) : (v => Math.round(v));
            findings.push({
                metric: m.key, label: m.label, unit: m.unit, emoji: m.emoji,
                direction: pct > 0 ? 'up' : 'down',
                pct: Math.round(pct),
                thisWeek: round(thisWeek),
                lastWeek: round(lastWeek),
            });
        }
        return findings;
    }

    // 관찰 권장 문구. 감소는 조기경보 관점(식욕/음수 감소·체중 변화)이 특히 중요.
    function _findingText(f) {
        const dir = f.direction === 'up' ? '늘었어요' : '줄었어요';
        return `${f.label}이 지난주보다 ${Math.abs(f.pct)}% ${dir} ` +
               `(주 평균 ${f.lastWeek}${f.unit} → ${f.thisWeek}${f.unit})`;
    }

    function renderWeeklyReportCard() {
        const host = document.getElementById('weekly-report-card');
        if (!host) return;
        const history = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        const weightHistory = (typeof getWeightHistory === 'function') ? getWeightHistory() : [];
        const findings = analyzeWeekly(history, weightHistory);

        // 비교 가능한 데이터가 두 주에 걸쳐 있는지(어떤 metric이든 지난주 기록 유무).
        const now = new Date();
        const hasLastWeek = METRICS.some(m => {
            const rows = m.source === 'weight' ? weightHistory : history;
            return _windowAvg(rows, m.key, now, 7, 13) !== null;
        });

        if (!hasLastWeek) {
            host.innerHTML = `
            <div class="card-modern p-5 border border-brand-100/60">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">📈</div>
                    <div class="min-w-0">
                        <h3 class="text-sm font-bold text-gray-900">주간 변화 리포트</h3>
                        <p class="text-xs text-gray-500 mt-0.5">
                            식욕·음수·체중을 2주 이상 기록하면 지난주 대비 변화를 자동으로 요약해 드려요.
                        </p>
                    </div>
                </div>
            </div>`;
            return;
        }

        if (findings.length === 0) {
            host.innerHTML = `
            <div class="card-modern p-5 border border-emerald-100">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">📈</div>
                    <div class="min-w-0 flex-1">
                        <h3 class="text-sm font-bold text-gray-900">주간 변화 리포트 · <span class="text-emerald-600">안정적</span></h3>
                        <p class="text-xs text-gray-500 mt-0.5">이번 주 식욕·음수·체중이 지난주와 비슷해요. 꾸준히 잘 돌보고 계세요! 🐾</p>
                    </div>
                    <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700">이상 없음</span>
                </div>
            </div>`;
            return;
        }

        const items = findings.map(f => `
            <li class="flex items-start gap-2 text-sm text-amber-900">
                <span class="mt-0.5">${f.emoji}</span>
                <span>${_findingText(f)}</span>
            </li>`).join('');
        host.innerHTML = `
        <div class="card-modern p-5 border border-amber-200 bg-amber-50/40">
            <div class="flex items-center gap-3 mb-2">
                <div class="text-2xl">📋</div>
                <div class="min-w-0 flex-1">
                    <h3 class="text-sm font-bold text-amber-900">지난주 대비 변화가 있어요</h3>
                    <p class="text-xs text-amber-700/80 mt-0.5">문제가 아닐 수 있지만, 이번 주 컨디션을 한 번 관찰해 주세요.</p>
                </div>
            </div>
            <ul class="space-y-1.5 pl-1">${items}</ul>
        </div>`;
    }

    window.analyzeWeekly = analyzeWeekly;
    window.renderWeeklyReportCard = renderWeeklyReportCard;
})();
