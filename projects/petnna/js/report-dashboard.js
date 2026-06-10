// report-dashboard.js — 월간 종합 케어 리포트 대시보드

function updateReportDashboard() {
    // 건강 점수
    const healthScore = typeof calcHealthScore === 'function' ? calcHealthScore() : 0;
    const healthScoreEl = document.getElementById('report-health-score');
    if (healthScoreEl) {
        healthScoreEl.textContent = healthScore || '--';
        healthScoreEl.className = healthScore >= 80 ? 'text-2xl font-black text-emerald-600'
            : healthScore >= 60 ? 'text-2xl font-black text-amber-600'
            : 'text-2xl font-black text-rose-600';
    }

    // 일정 준수율
    const careRate = typeof getWeeklyCareCompletionRate === 'function' ? getWeeklyCareCompletionRate() : 0;
    const careRateEl = document.getElementById('report-care-rate');
    if (careRateEl) {
        careRateEl.textContent = careRate > 0 ? careRate + '%' : '--';
        careRateEl.className = careRate >= 80 ? 'text-2xl font-black text-emerald-600'
            : careRate >= 60 ? 'text-2xl font-black text-amber-600'
            : careRate > 0 ? 'text-2xl font-black text-rose-600'
            : 'text-2xl font-black text-gray-400';
    }

    // 연속 기록
    const streak = typeof calcHealthStreak === 'function' ? calcHealthStreak() : 0;
    const streakEl = document.getElementById('report-streak');
    if (streakEl) {
        streakEl.textContent = streak > 0 ? streak + '일' : '--';
    }

    // AI 분석 횟수
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const aiCountEl = document.getElementById('report-ai-count');
    if (aiCountEl) {
        aiCountEl.textContent = analyses.length > 0 ? analyses.length + '회' : '--';
    }
}
