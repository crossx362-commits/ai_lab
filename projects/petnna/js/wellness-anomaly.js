// wellness-anomaly.js — 예측 웰니스 이상감지 (오너 승인 2026-07-10)
// healthLogs.history의 음수량(water)·식사량(food)을 z-score로 분석해
// '평소보다 급증/급감' 미세변화(micro-shift)를 부드럽게 감지한다.
// 신규 인프라 없이 순수 JS 통계. 서버 이력은 health_logs 테이블에서 동기화(있으면).
//
// 가드레일(회의 결정 2026-07-09):
//  - 기준표본 N<14면 알림 억제(표본 부족 오탐 방지)
//  - health-dashboard 카드 단일 채널 + 확정 이상만 토스트(하루 1회)
(function () {
    'use strict';

    const MIN_SAMPLES = 14;   // 기준 표본 하한(이하면 판정·알림 억제)
    const RECENT_DAYS = 3;    // 최근 관찰 창(일)
    const Z_THRESHOLD = 2.0;  // 이상 판정 z 임계

    const METRICS = [
        { key: 'water', label: '음수량', unit: 'ml', emoji: '💧' },
        { key: 'food',  label: '식사량', unit: 'g',  emoji: '🍚' },
    ];

    function _mean(a) { return a.reduce((s, x) => s + x, 0) / a.length; }
    function _std(a, m) { return Math.sqrt(a.reduce((s, x) => s + (x - m) * (x - m), 0) / a.length); }

    // history에서 유효한 metric 값 시퀀스(날짜순). 0/null/비숫자는 미기록으로 제외.
    function _series(history, key) {
        return (history || [])
            .filter(d => d && typeof d[key] === 'number' && d[key] > 0)
            .map(d => d[key]);
    }

    // 순수 함수: history 배열 → 이상 소견 배열. 테스트/재사용 용이.
    function analyzeWellness(history) {
        const findings = [];
        for (const m of METRICS) {
            const s = _series(history, m.key);
            if (s.length < MIN_SAMPLES + RECENT_DAYS) continue;   // 표본 부족 → 억제
            const recent = s.slice(-RECENT_DAYS);
            const baseline = s.slice(0, -RECENT_DAYS);
            if (baseline.length < MIN_SAMPLES) continue;
            const bMean = _mean(baseline);
            const bStd = _std(baseline, bMean);
            if (bStd < 1e-6) continue;                            // 변동 없음 → 판정 불가
            const rMean = _mean(recent);
            const z = (rMean - bMean) / bStd;
            if (Math.abs(z) >= Z_THRESHOLD) {
                findings.push({
                    metric: m.key, label: m.label, unit: m.unit, emoji: m.emoji,
                    direction: z > 0 ? 'up' : 'down',
                    z: Math.round(z * 10) / 10,
                    recentAvg: Math.round(rMean),
                    baselineAvg: Math.round(bMean),
                    pct: Math.round(((rMean - bMean) / bMean) * 100),
                    samples: baseline.length,
                });
            }
        }
        return findings;
    }

    function _findingText(f) {
        const dir = f.direction === 'up' ? '높아요' : '낮아요';
        const arrow = f.direction === 'up' ? '급증' : '급감';
        return `최근 ${RECENT_DAYS}일 ${f.label}이 평소보다 ${Math.abs(f.pct)}% ${dir} ` +
               `(평균 ${f.baselineAvg}${f.unit} → ${f.recentAvg}${f.unit}, ${arrow})`;
    }

    function _sampleCount() {
        const h = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        return METRICS.reduce((mx, m) => Math.max(mx, _series(h, m.key).length), 0);
    }

    // 카드 렌더 (health-dashboard 단일 채널)
    function renderWellnessCard() {
        const host = document.getElementById('wellness-anomaly-card');
        if (!host) return;
        const history = (typeof healthLogs !== 'undefined' && healthLogs) ? healthLogs.history : [];
        const findings = analyzeWellness(history);
        const samples = _sampleCount();

        if (samples < MIN_SAMPLES + RECENT_DAYS) {
            // 표본 부족 — 조용히 안내(알림 없음)
            host.innerHTML = `
            <div class="card-modern p-5 border border-brand-100/60">
                <div class="flex items-center gap-3">
                    <div class="text-2xl">🔮</div>
                    <div class="min-w-0">
                        <h3 class="text-sm font-bold text-gray-900">예측 웰니스</h3>
                        <p class="text-xs text-gray-500 mt-0.5">
                            건강 기록이 ${MIN_SAMPLES + RECENT_DAYS}일 이상 쌓이면 평소와 다른 미세 변화를 자동으로 감지해요.
                            <span class="text-brand-600 font-semibold">(현재 ${samples}일)</span>
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
                    <div class="text-2xl">🔮</div>
                    <div class="min-w-0 flex-1">
                        <h3 class="text-sm font-bold text-gray-900">예측 웰니스 · <span class="text-emerald-600">안정적</span></h3>
                        <p class="text-xs text-gray-500 mt-0.5">최근 ${RECENT_DAYS}일 음수·식사량이 평소 범위 안에 있어요. 계속 잘 돌보고 계세요! 🐾</p>
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
                <div class="text-2xl">⚠️</div>
                <div class="min-w-0 flex-1">
                    <h3 class="text-sm font-bold text-amber-900">평소와 다른 변화가 감지됐어요</h3>
                    <p class="text-xs text-amber-700/80 mt-0.5">이상 징후가 아닐 수 있지만, 최근 컨디션을 한 번 살펴봐 주세요.</p>
                </div>
            </div>
            <ul class="space-y-1.5 pl-1">${items}</ul>
        </div>`;

        _maybeNotify(findings);
    }

    // 확정 이상 → 토스트 1회(하루 1회 억제)
    function _maybeNotify(findings) {
        if (!findings.length) return;
        const today = (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today && healthLogs.today.date)
            || new Date().toISOString().slice(0, 10);
        const flagKey = 'petna_wellness_alerted_' + today;
        try { if (localStorage.getItem(flagKey)) return; localStorage.setItem(flagKey, '1'); } catch (e) { return; }
        if (typeof showToast === 'function') showToast('🔮 ' + _findingText(findings[0]));
    }

    window.analyzeWellness = analyzeWellness;
    window.renderWellnessCard = renderWellnessCard;
})();
