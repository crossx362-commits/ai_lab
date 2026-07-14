// health-dashboard.js — 건강 이력 저장 및 차트 렌더링

function saveHealthHistoryToday() {
    if (typeof healthLogs === 'undefined' || !healthLogs.today) return;
    const today = new Date().toISOString().split('T')[0];
    if (!healthLogs.history) healthLogs.history = [];
    const existing = healthLogs.history.findIndex(h => h.date === today);
    const entry = { ...healthLogs.today, date: today };
    if (existing >= 0) {
        healthLogs.history[existing] = entry;
    } else {
        healthLogs.history.unshift(entry);
        if (healthLogs.history.length > 90) healthLogs.history.splice(90);
    }
    if (typeof saveState === 'function') saveState();
    if (typeof updateHealthTutorialVisibility === 'function') updateHealthTutorialVisibility();
}

// 일주일치 랜덤 건강 데이터 생성 (누를 때마다 새로운 주 추가)
function generateWeeklyHealthData() {
    if (typeof healthLogs === 'undefined') return;
    if (!healthLogs.history) healthLogs.history = [];

    const poopTypes = ['normal', 'soft', 'hard', 'liquid'];
    const today = new Date();

    // 기존 데이터가 있으면 가장 오래된 날짜 이전 주를 생성
    let startOffset = 0;
    if (healthLogs.history.length > 0) {
        const oldestDate = new Date(healthLogs.history[healthLogs.history.length - 1].date);
        const daysDiff = Math.floor((today - oldestDate) / (1000 * 60 * 60 * 24));
        startOffset = daysDiff + 7; // 가장 오래된 날짜보다 1주 더 이전
    }

    let addedCount = 0;
    for (let i = 6; i >= 0; i--) {
        const date = new Date(today);
        date.setDate(date.getDate() - (i + startOffset));
        const dateStr = date.toISOString().split('T')[0];

        // 중복 체크 제거 - 항상 추가
        const entry = {
            date: dateStr,
            food: Math.floor(Math.random() * 50) + 30,      // 30-80
            water: Math.floor(Math.random() * 150) + 100,   // 100-250
            poop: Math.random() > 0.2 ? poopTypes[Math.floor(Math.random() * poopTypes.length)] : null,
            condition: Math.random() > 0.7 ? (Math.random() > 0.5 ? 'good' : 'tired') : null
        };

        healthLogs.history.push(entry);
        addedCount++;
    }

    // 중복 제거 및 정렬
    const uniqueDates = {};
    healthLogs.history = healthLogs.history.filter(h => {
        if (uniqueDates[h.date]) return false;
        uniqueDates[h.date] = true;
        return true;
    });

    healthLogs.history.sort((a, b) => new Date(b.date) - new Date(a.date));

    if (healthLogs.history.length > 90) {
        healthLogs.history = healthLogs.history.slice(0, 90);
    }

    // "오늘의 기록"(식사량/음수량/배변) 카드는 healthLogs.today를 별도로 읽는데,
    // 위에서 채운 건 healthLogs.history뿐이라 데모 데이터를 눌러도 반영 안 보이던
    // 버그(2026-07-13 오너 발견) — 방금 생성한 이력에 오늘 날짜 항목이 있으면 동기화.
    const todayStr = today.toISOString().split('T')[0];
    const todayEntry = healthLogs.history.find(h => h.date === todayStr);
    if (todayEntry) {
        if (!healthLogs.today) healthLogs.today = {};
        healthLogs.today.food = todayEntry.food;
        healthLogs.today.water = todayEntry.water;
        healthLogs.today.poop = todayEntry.poop;
        healthLogs.today.date = todayStr;
    }

    if (typeof saveState === 'function') saveState();
    if (typeof renderHealthTab === 'function') renderHealthTab();
    if (typeof renderHealthTrendChartMain === 'function') renderHealthTrendChartMain();
    if (typeof renderHealthCalendarMain === 'function') renderHealthCalendarMain();
    if (typeof updateHealthTutorialMainVisibility === 'function') updateHealthTutorialMainVisibility();
    if (typeof updateReportDashboard === 'function') updateReportDashboard();
    if (typeof showToast === 'function') {
        const weekCount = Math.floor(healthLogs.history.length / 7);
        showToast(`일주일치 데모 데이터 추가 완료! 📊 (총 ${healthLogs.history.length}일, ${weekCount}주)`);
    }
}

function getLast7DaysHealthData() {
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const days = Array.from({ length: 7 }, (_, i) => {
        const d = new Date();
        d.setDate(d.getDate() - (6 - i));
        return d.toISOString().split('T')[0];
    });
    return days.map(date => {
        const entry = history.find(h => h.date === date) || {};
        return {
            date: date.slice(5),      // MM-DD
            food: entry.food || 0,
            water: entry.water || 0,
            poop: entry.poop ? 1 : 0,
            condition: entry.condition || null
        };
    });
}

function calcHealthScore() {
    const data = getLast7DaysHealthData();
    let score = 70;
    const avgFood = data.reduce((s, d) => s + d.food, 0) / 7;
    const avgWater = data.reduce((s, d) => s + d.water, 0) / 7;
    const poopDays = data.filter(d => d.poop).length;
    if (avgFood > 50)  score += 10;
    if (avgWater > 200) score += 10;
    if (poopDays >= 5) score += 10;
    return Math.min(100, Math.round(score));
}

// 90일 건강 캘린더 히트맵
function renderHealthCalendar() {
    const el = document.getElementById('health-calendar');
    if (!el) return;
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const today = new Date();

    const hasAnyRecord = history.length > 0;

    // 데이터 없으면 숨김 처리 (공간 절약)
    if (!hasAnyRecord) {
        el.innerHTML = '';
        el.style.display = 'none';
        return;
    }

    // 데이터 있으면 표시
    el.style.display = 'block';

    const cells = Array.from({ length: 90 }, (_, i) => {
        const d = new Date(today);
        d.setDate(today.getDate() - (89 - i));
        const date = d.toISOString().split('T')[0];
        const entry = history.find(h => h.date === date);
        const hasRecord = entry && (entry.food > 0 || entry.water > 0 || entry.poop !== null);
        const isToday = date === today.toISOString().split('T')[0];
        let bg = hasRecord ? 'bg-emerald-400' : 'bg-gray-100';
        if (isToday) bg += ' ring-1 ring-amber-400';
        return `<div class="w-3 h-3 rounded-sm ${bg}" title="${date}"></div>`;
    }).join('');

    el.innerHTML = `
        <div class="flex flex-wrap gap-0.5">${cells}</div>
        <div class="flex items-center gap-2 mt-1.5">
            <span class="w-3 h-3 rounded-sm bg-gray-100 inline-block"></span><span class="text-[9px] text-gray-400">기록 없음</span>
            <span class="w-3 h-3 rounded-sm bg-emerald-400 inline-block ml-2"></span><span class="text-[9px] text-gray-400">기록 완료</span>
        </div>`;
}

// 복약 순응도 30일 트래커 (care-scheduler 투약 기록 기반 히트맵 + 순응률%)
function renderMedAdherenceTracker() {
    const el = document.getElementById('med-adherence-tracker');
    if (!el) return;

    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const activePet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petId = activePet ? activePet.id : null;

    const medSchedules = careSchedules.schedules.filter(s =>
        s.type === 'medicine' && (petId == null || s.petId == null || s.petId === petId));

    // 투약 일정이 없으면 위젯 숨김 (공간 절약)
    if (medSchedules.length === 0) {
        el.innerHTML = '';
        el.style.display = 'none';
        return;
    }
    el.style.display = 'block';

    const medDone = careSchedules.completionHistory.filter(c =>
        c.type === 'medicine' && (petId == null || c.petId == null || c.petId === petId));

    // 주어진 날짜(dateStr)에 특정 투약 일정이 예정돼 있었는지 (care-scheduler getTodaySchedules 로직과 동일)
    function expectedOn(schedule, dateObj, dateStr) {
        if (schedule.createdAt && new Date(schedule.createdAt).toISOString().split('T')[0] > dateStr) return false;
        if (schedule.repeat === 'daily') return true;
        if (schedule.repeat === 'weekly') return (schedule.repeatDays || []).includes(dateObj.getDay());
        if (schedule.repeat === 'monthly') {
            const base = new Date(schedule.date || schedule.createdAt);
            return base.getDate() === dateObj.getDate();
        }
        if (schedule.repeat === 'once') return schedule.date === dateStr;
        return false;
    }

    const today = new Date();
    let totalExpected = 0, totalTaken = 0;

    const cells = Array.from({ length: 30 }, (_, i) => {
        const d = new Date(today);
        d.setDate(today.getDate() - (29 - i));
        const dateStr = d.toISOString().split('T')[0];
        const expected = medSchedules.filter(s => expectedOn(s, d, dateStr)).length;
        const taken = medDone.filter(c => (c.completedAt || '').startsWith(dateStr)).length;
        totalExpected += expected;
        totalTaken += Math.min(taken, expected);

        const isToday = dateStr === today.toISOString().split('T')[0];
        let bg, label;
        if (expected === 0 && taken === 0) { bg = 'bg-gray-100'; label = '예정 없음'; }
        else if (taken >= (expected || 1)) { bg = 'bg-emerald-400'; label = '복용 완료'; }
        else if (taken > 0) { bg = 'bg-amber-300'; label = '일부 복용'; }
        else { bg = 'bg-rose-300'; label = '미복용'; }
        if (isToday) bg += ' ring-1 ring-amber-400';
        return `<div class="w-3 h-3 rounded-sm ${bg}" title="${dateStr} · ${label} (${taken}/${expected})"></div>`;
    }).join('');

    const rate = totalExpected > 0 ? Math.round((totalTaken / totalExpected) * 100) : 100;
    const rateColor = rate >= 80 ? 'text-emerald-600' : rate >= 60 ? 'text-amber-600' : 'text-rose-600';

    el.innerHTML = `
        <div class="card-modern p-4">
            <div class="flex justify-between items-center mb-3">
                <div class="flex items-center gap-2">
                    <span class="text-2xl">💊</span>
                    <h3 class="text-sm font-bold text-gray-900">복약 순응도 (최근 30일)</h3>
                </div>
                <span class="text-xl font-black ${rateColor}">${rate}%</span>
            </div>
            <div class="flex flex-wrap gap-0.5">${cells}</div>
            <div class="flex items-center gap-2 mt-2 flex-wrap">
                <span class="w-3 h-3 rounded-sm bg-emerald-400 inline-block"></span><span class="text-[9px] text-gray-400">복용</span>
                <span class="w-3 h-3 rounded-sm bg-amber-300 inline-block ml-1"></span><span class="text-[9px] text-gray-400">일부</span>
                <span class="w-3 h-3 rounded-sm bg-rose-300 inline-block ml-1"></span><span class="text-[9px] text-gray-400">미복용</span>
                <span class="w-3 h-3 rounded-sm bg-gray-100 inline-block ml-1"></span><span class="text-[9px] text-gray-400">예정 없음</span>
            </div>
        </div>`;
}
window.renderMedAdherenceTracker = renderMedAdherenceTracker;

// 💊 복약 순응도 위클리 카드 — 최근 7일 복약률(%) + 놓친 약을 achievements 스타일 카드로 요약
function renderMedAdherenceWeeklyCard() {
    const el = document.getElementById('med-adherence-weekly-card');
    if (!el) return;

    const careSchedules = (typeof AppStore !== 'undefined' && AppStore.getState('careSchedules')) || { schedules: [], completionHistory: [] };
    const activePet = (typeof getActivePet === 'function') ? getActivePet() : null;
    const petId = activePet ? activePet.id : null;

    const medSchedules = careSchedules.schedules.filter(s =>
        s.type === 'medicine' && (petId == null || s.petId == null || s.petId === petId));

    // 투약 일정이 없으면 카드 숨김 (공간 절약)
    if (medSchedules.length === 0) {
        el.innerHTML = '';
        el.style.display = 'none';
        return;
    }
    el.style.display = 'block';

    const medDone = careSchedules.completionHistory.filter(c =>
        c.type === 'medicine' && (petId == null || c.petId == null || c.petId === petId));

    // 특정 날짜에 해당 투약 일정이 예정돼 있었는지 (30일 트래커와 동일 로직)
    function expectedOn(schedule, dateObj, dateStr) {
        if (schedule.createdAt && new Date(schedule.createdAt).toISOString().split('T')[0] > dateStr) return false;
        if (schedule.repeat === 'daily') return true;
        if (schedule.repeat === 'weekly') return (schedule.repeatDays || []).includes(dateObj.getDay());
        if (schedule.repeat === 'monthly') {
            const base = new Date(schedule.date || schedule.createdAt);
            return base.getDate() === dateObj.getDate();
        }
        if (schedule.repeat === 'once') return schedule.date === dateStr;
        return false;
    }

    const today = new Date();
    let weekExpected = 0, weekTaken = 0;
    const missedByTitle = {}; // 놓친 약: 제목별 미복용 일수

    for (let i = 0; i < 7; i++) {
        const d = new Date(today);
        d.setDate(today.getDate() - i);
        const dateStr = d.toISOString().split('T')[0];
        const takenTitles = medDone
            .filter(c => (c.completedAt || '').startsWith(dateStr))
            .map(c => c.title);
        medSchedules.forEach(s => {
            if (!expectedOn(s, d, dateStr)) return;
            weekExpected++;
            const idx = takenTitles.indexOf(s.title);
            if (idx >= 0) {
                weekTaken++;
                takenTitles.splice(idx, 1); // 같은 약 중복 매칭 방지
            } else {
                // 오늘 아직 예정 시간이 안 됐을 수 있으므로 오늘은 놓침 집계 제외
                if (i > 0) missedByTitle[s.title] = (missedByTitle[s.title] || 0) + 1;
            }
        });
    }

    const rate = weekExpected > 0 ? Math.round((weekTaken / weekExpected) * 100) : 100;
    const rateColor = rate >= 80 ? 'text-emerald-600' : rate >= 60 ? 'text-amber-600' : 'text-rose-600';
    const barColor = rate >= 80 ? 'bg-emerald-400' : rate >= 60 ? 'bg-amber-400' : 'bg-rose-400';
    const missedEntries = Object.entries(missedByTitle).sort((a, b) => b[1] - a[1]);

    const missedHtml = missedEntries.length > 0 ? `
        <div class="mt-3 pt-3 border-t border-gray-100">
            <p class="text-[10px] font-black text-gray-500 mb-1.5">😿 놓친 약</p>
            <div class="flex flex-wrap gap-1.5">
                ${missedEntries.map(([title, n]) =>
                    `<span class="inline-flex items-center gap-1 bg-rose-50 border border-rose-100 text-rose-600 text-[10px] font-bold px-2 py-1 rounded-lg">💊 ${_esc(title)} <span class="text-[9px] text-rose-400">${n}일</span></span>`
                ).join('')}
            </div>
        </div>` : `
        <div class="mt-3 pt-3 border-t border-gray-100">
            <p class="text-[10px] font-black text-emerald-600 text-center">🎉 이번 주 놓친 약 없이 완벽 복약!</p>
        </div>`;

    el.innerHTML = `
        <div class="card-modern p-4">
            <div class="flex items-center justify-between mb-2">
                <div class="flex items-center gap-2">
                    <span class="text-2xl">💊</span>
                    <div>
                        <h3 class="text-sm font-bold text-gray-900">주간 복약 순응도</h3>
                        <p class="text-[9px] text-gray-400 font-medium">최근 7일 · ${weekTaken}/${weekExpected}회 복용</p>
                    </div>
                </div>
                <span class="text-2xl font-black ${rateColor} tabular-nums">${rate}%</span>
            </div>
            <div class="w-full bg-gray-100 rounded-full h-2 overflow-hidden">
                <div class="${barColor} h-full rounded-full transition-all duration-700" style="width:${rate}%"></div>
            </div>
            ${missedHtml}
        </div>`;
}
window.renderMedAdherenceWeeklyCard = renderMedAdherenceWeeklyCard;

// 월별 건강 리포트 PDF (window.print 기반, 프리미엄 기능)
function generateHealthReportPDF() {
    if (typeof isPremium === 'function' && !isPremium()) {
        if (typeof showPremiumModal === 'function') showPremiumModal();
        return;
    }
    const pet = typeof getActivePet === 'function' ? getActivePet() : null;
    const petName = pet?.name || '반려동물';
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const now = new Date();
    const monthStr = `${now.getFullYear()}년 ${now.getMonth() + 1}월`;
    const score = typeof calcHealthScore === 'function' ? calcHealthScore() : '--';
    const streak = typeof calcHealthStreak === 'function' ? calcHealthStreak() : 0;
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const latestAI = analyses[0];

    // 이번 달 기록만 필터
    const thisMonth = now.toISOString().slice(0, 7);
    const monthHistory = history.filter(h => h.date && h.date.startsWith(thisMonth));
    const avgFood = monthHistory.length ? Math.round(monthHistory.reduce((s, h) => s + (h.food || 0), 0) / monthHistory.length) : 0;
    const avgWater = monthHistory.length ? Math.round(monthHistory.reduce((s, h) => s + (h.water || 0), 0) / monthHistory.length) : 0;
    const poopDays = monthHistory.filter(h => h.poop !== null && h.poop !== undefined).length;
    const recordDays = monthHistory.length;

    const html = `<!DOCTYPE html><html lang="ko"><head>
<meta charset="UTF-8">
<title>${petName} 월간 종합 케어 리포트 — ${monthStr}</title>
<style>
  body{font-family:'Apple SD Gothic Neo','Noto Sans KR',sans-serif;margin:0;padding:32px;color:#1f2937;background:#fff}
  h1{font-size:24px;font-weight:900;color:#a9583e;margin-bottom:4px}
  .sub{font-size:13px;color:#6b7280;margin-bottom:24px}
  .grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:24px}
  .card{background:#f9fafb;border-radius:12px;padding:16px;text-align:center;border:1px solid #e5e7eb}
  .card .val{font-size:28px;font-weight:900;color:#374151;margin-bottom:2px}
  .card .lbl{font-size:11px;color:#9ca3af}
  .section{margin-bottom:20px}
  .section h2{font-size:15px;font-weight:900;color:#374151;margin-bottom:8px;padding-bottom:4px;border-bottom:2px solid #f4e2d9}
  table{width:100%;border-collapse:collapse;font-size:12px}
  th{background:#f4e2d9;color:#a9583e;padding:6px 8px;text-align:left}
  td{padding:5px 8px;border-bottom:1px solid #f3f4f6}
  .badge{display:inline-block;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700}
  .ok{background:#d1fae5;color:#065f46}.warn{background:#fef3c7;color:#92400e}.bad{background:#fee2e2;color:#991b1b}
  .footer{margin-top:32px;text-align:center;font-size:10px;color:#d1d5db}
  @media print{body{padding:0 20px}button{display:none}}
</style>
</head><body>
<div style="display:flex;align-items:center;gap:12px;margin-bottom:8px">
  <span style="font-size:36px">🐾</span>
  <div><h1>${petName}의 월간 종합 케어 리포트</h1><p class="sub">${monthStr} · 건강 + 돌봄 통합 분석 · 생성일: ${now.toLocaleDateString('ko-KR')}</p></div>
</div>

<div class="grid" style="grid-template-columns:repeat(5,1fr)">
  <div class="card"><div class="val" style="color:#a9583e">${score}</div><div class="lbl">건강 점수</div></div>
  <div class="card"><div class="val" style="color:#f59e0b">${streak}일</div><div class="lbl">연속 기록</div></div>
  <div class="card"><div class="val" style="color:#10b981">${recordDays}일</div><div class="lbl">이달 기록일</div></div>
  <div class="card"><div class="val" style="color:#3b82f6">${analyses.length}회</div><div class="lbl">AI 분석 횟수</div></div>
  <div class="card"><div class="val" style="color:#0ea5e9">${(() => {
    if (typeof getWeeklyCareCompletionRate !== 'function') return '-';
    const rate = getWeeklyCareCompletionRate();
    return rate > 0 ? rate + '%' : '-';
  })()}</div><div class="lbl">일정 준수율</div></div>
</div>

<div class="section">
  <h2>📊 이번 달 평균</h2>
  <div class="grid" style="grid-template-columns:repeat(3,1fr)">
    <div class="card"><div class="val">${avgFood}g</div><div class="lbl">일평균 식사량</div></div>
    <div class="card"><div class="val">${avgWater}ml</div><div class="lbl">일평균 음수량</div></div>
    <div class="card"><div class="val">${poopDays}일</div><div class="lbl">배변 기록일</div></div>
  </div>
</div>

${latestAI ? `<div class="section">
  <h2>🏥 최근 AI 건강 분석</h2>
  <div class="card" style="text-align:left">
    <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:8px">
      ${[['눈',latestAI.eyes],['귀',latestAI.ears],['피부',latestAI.skin],['털',latestAI.coat],['치아',latestAI.teeth]].filter(([,v])=>v&&v!=='확인불가').map(([l,v])=>`<span class="badge ${v==='정상'||v==='윤기있음'?'ok':v==='주의'?'warn':'bad'}">${l}: ${v}</span>`).join('')}
    </div>
    <div style="font-size:12px;color:#4b5563">${latestAI.summary || ''}</div>
    <div style="font-size:11px;color:#a9583e;margin-top:4px;font-weight:700">${latestAI.advice || ''}</div>
    <div style="font-size:10px;color:#9ca3af;margin-top:4px">분석일: ${new Date(latestAI.analyzedAt || Date.now()).toLocaleDateString('ko-KR')}</div>
  </div>
</div>` : ''}

<div class="section">
  <h2>📊 주간 돌봄 활동 통계 (최근 7일)</h2>
  ${(() => {
    if (typeof getWeeklyCareStats !== 'function') {
      return '<p style="text-align:center;color:#9ca3af;font-size:12px;padding:20px;">돌봄 스케줄러 기능을 사용하여 활동을 기록해보세요.</p>';
    }
    const stats = getWeeklyCareStats();
    const entries = Object.entries(stats);
    if (entries.length === 0) {
      return '<p style="text-align:center;color:#9ca3af;font-size:12px;padding:20px;">📅 아직 기록된 돌봄 활동이 없습니다.<br>마이펫 탭에서 오늘의 돌봄 일정을 완료해보세요!</p>';
    }
    const icons = { feed: '🍖', water: '💧', walk: '🚶', medicine: '💊', vet: '🏥', groom: '✂️', play: '🎾' };
    const names = { feed: '식사', water: '음수', walk: '산책', medicine: '투약', vet: '병원', groom: '미용', play: '놀이' };
    return `<div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(120px,1fr))">${entries.map(([type, count]) =>
      `<div class="card"><div class="val">${icons[type] || '📋'} ${count}회</div><div class="lbl">${names[type] || type}</div></div>`
    ).join('')}</div>`;
  })()}
</div>

<div class="section">
  <h2>📅 이번 달 일별 기록</h2>
  <table>
    <tr><th>날짜</th><th>식사(g)</th><th>음수(ml)</th><th>배변</th><th>컨디션</th></tr>
    ${monthHistory.slice(0,31).map(h=>`<tr>
      <td>${h.date}</td>
      <td>${h.food||0}g</td>
      <td>${h.water||0}ml</td>
      <td>${h.poop===null||h.poop===undefined?'-':h.poop==='normal'?'💩 정상':h.poop==='hard'?'🪨 단단':h.poop==='liquid'?'💦 무름':'기록됨'}</td>
      <td>${h.condition||'-'}</td>
    </tr>`).join('')}
  </table>
</div>

<div class="footer">
  ※ 이 리포트는 참고용이며 수의사의 의학적 진단을 대체하지 않습니다.<br>
  🐾 펫과나 (Pet & Na) — AI 반려동물 케어 올인원
</div>

<div style="text-align:center;margin-top:20px">
  <button onclick="window.print()" style="background:#a9583e;color:#fff;border:none;padding:10px 24px;border-radius:8px;font-size:13px;font-weight:700;cursor:pointer">🖨️ PDF로 저장 / 인쇄</button>
</div>
</body></html>`;
    const blob = new Blob([html], { type: 'text/html; charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const win = window.open(url, '_blank');
    if (!win) {
        URL.revokeObjectURL(url);
        if (typeof showToast === 'function') showToast('팝업 차단을 해제해주세요');
        return;
    }
    setTimeout(() => URL.revokeObjectURL(url), 60000);
}

let healthTrendChart = null;

function renderHealthTrendChart(period = 7) {
    const ctx = document.getElementById('health-trend-chart');
    if (!ctx || typeof Chart === 'undefined') return;
    const data = getLast7DaysHealthData().slice(-period);
    if (healthTrendChart) { healthTrendChart.destroy(); healthTrendChart = null; }

    const isDark = document.body.classList.contains('theme-dark');
    const textColor = isDark ? '#d1d1e0' : '#6b7280';
    const gridColor = isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)';

    healthTrendChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => d.date),
            datasets: [
                {
                    label: '음식(g)',
                    data: data.map(d => d.food),
                    backgroundColor: 'rgba(245,158,11,0.6)',
                    borderRadius: 4
                },
                {
                    label: '물(ml)',
                    data: data.map(d => d.water),
                    backgroundColor: 'rgba(56,189,248,0.6)',
                    borderRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { labels: { color: textColor, font: { size: 10 } } }
            },
            scales: {
                x: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor } },
                y: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor } }
            }
        }
    });

    // 건강 점수 업데이트
    const scoreEl = document.getElementById('health-score-value');
    if (scoreEl) {
        const score = calcHealthScore();
        scoreEl.textContent = score;
        scoreEl.className = score >= 80 ? 'text-2xl font-black text-emerald-500'
                          : score >= 60 ? 'text-2xl font-black text-amber-500'
                          : 'text-2xl font-black text-red-500';
    }
}
