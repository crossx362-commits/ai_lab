// medication-log.js — 투약 이행 로그 (백로그 나무 제안, P2)
// care-check.js의 '먹였어요/건너뜀' 원탭이 여기로 들어와 medication_logs 테이블
// (스키마 라이브 적용 2026-08-04)에 upsert되고, 복약 연속일·이번 달 이행률을
// 그 기록에서 계산해 건강 탭(#medication-log-tracker)에 표시한다.
// 건너뜀(taken=false)도 남겨 이행률 분모를 추정에 맡기지 않는다.
// 로컬 우선: localStorage에 항상 남기고, 세션이 있으면 supabase로 겹쳐 올린다.
(function () {
    'use strict';

    const LS_KEY = 'petna_medication_logs';
    const MAX_LOGS = 400;

    function _load() {
        try {
            const raw = localStorage.getItem(LS_KEY);
            const arr = raw ? JSON.parse(raw) : [];
            return Array.isArray(arr) ? arr : [];
        } catch (e) { return []; }
    }

    function _save(arr) {
        try {
            localStorage.setItem(LS_KEY, JSON.stringify(arr.slice(0, MAX_LOGS)));
        } catch (e) { /* 저장 실패는 무해 — 화면 렌더는 계속 */ }
    }

    // 날짜 키는 반드시 **로컬** 기준. toISOString()은 UTC라 KST 오전 9시 이전에는
    // 전날을 돌려주고, 그러면 아침 투약이 어제 기록과 같은 키를 써서 덮어쓴다
    // (로컬 findIndex도, 서버 UNIQUE(user_id,pet_id,schedule_id,log_date)도 같이 뭉갠다).
    // _streak()가 쓰는 _dateStr과 같은 함수를 공유해 두 계산이 어긋나지 않게 한다.
    function _todayStr() { return _dateStr(new Date()); }
    function _monthStr(d) { return String(d || _todayStr()).slice(0, 7); }
    function _activePetId() {
        const p = (typeof getActivePet === 'function') ? getActivePet() : null;
        return p ? String(p.id) : null;
    }

    // (petId, scheduleId, date) 유일. 같은 날 먹였어요↔건너뜀 번복 시 덮어쓴다.
    function record(schedule, taken) {
        if (!schedule) return;
        const petId = schedule.petId != null ? String(schedule.petId) : _activePetId();
        const date = _todayStr();
        const logs = _load();
        const idx = logs.findIndex(l =>
            String(l.petId) === String(petId) &&
            String(l.scheduleId) === String(schedule.id) &&
            l.date === date);
        const now = new Date().toISOString();
        const entry = (idx >= 0) ? logs[idx] : {
            _remoteId: Date.now() * 1000 + Math.floor(Math.random() * 1000),
            petId, scheduleId: schedule.id, date,
        };
        entry.kind = schedule.type || 'medicine';
        entry.title = schedule.title || '투약';
        entry.taken = !!taken;
        entry.takenAt = taken ? now : null;
        if (idx >= 0) logs[idx] = entry; else logs.unshift(entry);
        _save(logs);
        if (typeof SupabaseService !== 'undefined' && SupabaseService.uploadMedicationLog) {
            SupabaseService.uploadMedicationLog(entry);
        }
        renderTracker();
    }

    // 복약 연속일: taken=true인 날짜를 오늘(또는 어제)부터 거슬러 끊길 때까지 센다.
    // 오늘 아직 안 먹였어도(오늘 기록 없음) 어제까지의 연속은 깨지지 않게 어제부터 허용.
    function _streak(petId) {
        const taken = new Set(_load()
            .filter(l => String(l.petId) === String(petId) && l.taken)
            .map(l => l.date));
        if (taken.size === 0) return 0;
        const d = new Date();
        if (!taken.has(_dateStr(d))) d.setDate(d.getDate() - 1); // 오늘 미기록이면 어제부터
        let n = 0;
        while (taken.has(_dateStr(d))) { n++; d.setDate(d.getDate() - 1); }
        return n;
    }

    function _dateStr(d) {
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    function stats(petId) {
        const month = _monthStr();
        const mine = _load().filter(l =>
            String(l.petId) === String(petId) && _monthStr(l.date) === month);
        const taken = mine.filter(l => l.taken).length;
        const total = mine.length;
        return {
            streak: _streak(petId),
            monthTaken: taken,
            monthTotal: total,
            monthRate: total > 0 ? Math.round((taken / total) * 100) : null,
        };
    }

    function renderTracker() {
        const el = document.getElementById('medication-log-tracker');
        if (!el) return;
        const petId = _activePetId();
        if (!petId) { el.innerHTML = ''; el.hidden = true; return; }
        const s = stats(petId);
        // 이번 달 기록이 하나도 없으면 빈 카드로 공간만 차지하지 않는다.
        if (s.monthTotal === 0) { el.innerHTML = ''; el.hidden = true; return; }
        el.hidden = false;
        const rate = s.monthRate;
        const rateCls = rate == null ? 'text-gray-400'
            : (rate >= 90 ? 'text-emerald-600' : (rate >= 70 ? 'text-amber-600' : 'text-rose-600'));
        el.innerHTML = `
        <div class="card-modern p-4">
            <div class="flex items-center gap-2 mb-3">
                <span class="text-xl shrink-0">💊</span>
                <span class="text-sm font-bold text-gray-900 flex-1">복약 이행 로그</span>
            </div>
            <div class="grid grid-cols-2 gap-2">
                <div class="card-modern bg-amber-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">🔥</div>
                    <div class="text-xl font-bold text-amber-600">${s.streak}일</div>
                    <div class="text-[10px] text-gray-600 font-semibold">복약 연속일</div>
                </div>
                <div class="card-modern bg-emerald-50/50 p-3 text-center">
                    <div class="text-2xl mb-1">📅</div>
                    <div class="text-xl font-bold ${rateCls}">${rate == null ? '--' : rate}%</div>
                    <div class="text-[10px] text-gray-600 font-semibold">이번 달 이행률</div>
                </div>
            </div>
            <p class="text-[10px] text-gray-400 mt-2 text-center">이번 달 먹임 ${s.monthTaken} / 기록 ${s.monthTotal}건</p>
        </div>`;
    }

    // supabase.syncMedicationLogs가 로컬에 병합할 때 쓰는 진입점.
    function mergeRemote(rows) {
        if (!Array.isArray(rows) || rows.length === 0) return;
        const logs = _load();
        const key = (petId, sid, date) => `${petId}|${sid}|${date}`;
        const byKey = {};
        logs.forEach(l => { byKey[key(l.petId, l.scheduleId, l.date)] = l; });
        rows.forEach(row => {
            const k = key(row.pet_id, row.schedule_id, row.log_date);
            byKey[k] = {
                _remoteId: row.id,
                petId: row.pet_id != null ? String(row.pet_id) : null,
                scheduleId: row.schedule_id,
                date: row.log_date,
                kind: row.kind || 'medicine',
                title: row.title || '투약',
                taken: !!row.taken,
                takenAt: row.taken_at || null,
            };
        });
        _save(Object.values(byKey)
            .sort((a, b) => String(b.date).localeCompare(String(a.date))));
        renderTracker();
    }

    window.MedicationLog = { record, stats, renderTracker, getLogs: _load, mergeRemote };
    window.recordMedicationLog = record;
    window.renderMedicationLogTracker = renderTracker;
})();
