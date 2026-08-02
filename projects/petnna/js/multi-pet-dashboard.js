// 다견/다묘 가정 통합 케어 대시보드 (나무 제안, P3)
// 여러 펫의 오늘 케어 완료율과 다음 일정을 한 화면에서 요약한다.
// 펫이 2마리 이상일 때만 노출(1마리면 '오늘의 케어 요약' 카드로 충분).
// 케어 데이터는 careSchedules(petId 보유)에서만 집계하므로 활성 펫과 무관하게
// 모든 펫을 동시에 볼 수 있다. 읽기 전용 — 클릭 시 해당 펫으로 전환만 한다.
(function () {
    'use strict';

    function _todayStr() {
        const d = new Date();
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    // 한 펫의 오늘 케어 요약: 완료수/전체수/완료율/다음 일정
    function _petSummary(pet) {
        const all = (typeof getTodaySchedules === 'function' ? getTodaySchedules() : [])
            .filter(s => s.petId === pet.id);
        const today = _todayStr();
        const isDone = (s) => !!(s.lastCompleted && s.lastCompleted.startsWith(today));
        const doneCount = all.filter(isDone).length;
        const pending = all.filter(s => !isDone(s));
        // getTodaySchedules는 이미 time 오름차순 정렬 → pending[0]이 다음 일정
        const next = pending[0] || null;
        const total = all.length;
        return { total, doneCount, rate: total === 0 ? null : Math.round(doneCount / total * 100), next };
    }

    function _rateColor(rate) {
        if (rate === null) return 'text-gray-300';
        if (rate >= 100) return 'text-emerald-600';
        if (rate >= 50) return 'text-amber-600';
        return 'text-rose-500';
    }

    function _emoji(pet) {
        const byType = { dog: '🐶', cat: '🐱', rabbit: '🐰', hamster: '🐹' };
        return byType[pet.type] || '🐾';
    }

    function _row(pet, idx) {
        const s = _petSummary(pet);
        const rate = s.rate === null ? '—' : `${s.rate}%`;
        const count = s.total === 0 ? '일정 없음' : `${s.doneCount}/${s.total} 완료`;
        let nextText = s.total === 0
            ? '<span class="text-gray-300">오늘 예정된 케어가 없어요</span>'
            : (s.next
                ? `${typeof getCareTypeIcon === 'function' ? getCareTypeIcon(s.next.type) : '📋'} ${s.next.time || ''} ${s.next.title || (typeof getCareTypeName === 'function' ? getCareTypeName(s.next.type) : '')}`
                : '<span class="text-emerald-600">오늘 케어 모두 완료 🎉</span>');
        return `
        <button type="button" onclick="setActivePet(${idx})"
            class="w-full flex items-center gap-3 py-2.5 px-1 rounded-xl hover:bg-gray-50 active:scale-[0.99] transition-all text-left">
            <span class="text-2xl leading-none shrink-0">${_emoji(pet)}</span>
            <span class="flex flex-col min-w-0 flex-1">
                <span class="text-sm font-bold text-gray-800 truncate">${pet.name || '반려동물'}</span>
                <span class="text-[11px] text-gray-500 truncate">${nextText}</span>
            </span>
            <span class="flex flex-col items-end shrink-0">
                <span class="text-base font-black ${_rateColor(s.rate)} leading-none">${rate}</span>
                <span class="text-[10px] font-semibold text-gray-400 mt-0.5">${count}</span>
            </span>
        </button>`;
    }

    function renderMultiPetDashboard() {
        const host = document.getElementById('multi-pet-care-strip');
        if (!host) return;
        const all = (typeof pets !== 'undefined' && Array.isArray(pets)) ? pets : [];
        // 추모 펫은 케어 독촉 대상이 아니므로 제외(오늘의 케어 요약 카드와 동일 규약)
        const live = all.filter(p => !(typeof isMemorialPet === 'function' && isMemorialPet(p)));
        if (live.length < 2) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        const rows = live.map(pet => _row(pet, all.indexOf(pet))).join('');
        host.innerHTML = `
        <div class="px-4 py-3">
            <div class="flex items-center gap-1.5 mb-1.5">
                <span class="text-sm">🏠</span>
                <span class="text-xs font-bold text-gray-700">우리집 통합 케어</span>
                <span class="text-[10px] font-bold text-gray-400">읽기 전용 · 탭하면 이동</span>
            </div>
            <div class="divide-y divide-gray-50">${rows}</div>
        </div>`;
    }

    window.renderMultiPetDashboard = renderMultiPetDashboard;
})();
