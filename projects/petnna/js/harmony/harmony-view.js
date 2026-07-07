// 조화도 리포트 뷰 — PetHarmony 계산 결과를 카드로 렌더. DOM 전용.
(function (root) {
    const EL_COLOR = { 목: '#7BA05B', 화: '#E05C5C', 토: '#E8A55A', 금: '#B9A88C', 수: '#5C8FC9' };
    const EL_HANJA = { 목: '木', 화: '火', 토: '土', 금: '金', 수: '水' };
    const ELS = ['목', '화', '토', '금', '수'];

    function bar(label, petN, ownerN) {
        const c = EL_COLOR[label];
        const w = (n) => Math.max(4, n * 20);
        return `
        <div class="flex items-center gap-2 text-[11px] font-bold">
          <span class="w-10 shrink-0" style="color:${c}">${label}(${EL_HANJA[label]})</span>
          <div class="flex-1 space-y-0.5">
            <div class="h-[7px] rounded-full" style="width:${w(petN)}%;background:${c}"></div>
            <div class="h-[7px] rounded-full opacity-45" style="width:${w(ownerN)}%;background:${c}"></div>
          </div>
          <span class="w-8 text-right text-gray-400">${petN}·${ownerN}</span>
        </div>`;
    }

    function renderPlus(containerId, hd) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (!hd || !hd.elements) { el.innerHTML = ''; return; }
        const pet = hd.elements.pet, owner = hd.elements.owner;
        const areaCard = (a) => `
          <div class="bg-white rounded-2xl border border-rose-100 p-3 text-center">
            <div class="text-[20px]">${a.emoji}</div>
            <div class="text-[11px] font-black mt-0.5">${a.name}</div>
            <div class="text-[16px] font-black" style="color:${EL_COLOR[a.element]}">${a.score}<span class="text-[10px] text-gray-400">점</span></div>
            <div class="text-[10px] font-bold text-rose-500">${a.grade}</div>
            <p class="text-[10px] text-gray-500 mt-1 leading-snug">${a.tip}</p>
          </div>`;
        const luck = (typeof PetHarmony !== 'undefined')
            ? PetHarmony.todayIndex(hd.score, pet.dominant) : null;
        el.innerHTML = `
        <div class="mt-4 space-y-4">
          <div class="bg-white rounded-3xl p-4 border border-rose-100 shadow-sm">
            <h4 class="text-xs font-black text-gray-700 mb-1">🌿 오행 분포 <span class="text-[10px] font-bold text-gray-400">진한색=펫 · 연한색=집사</span></h4>
            <div class="flex gap-2 mb-2">
              <span class="text-[10px] font-black px-2 py-0.5 rounded-full text-white" style="background:${EL_COLOR[pet.dominant]}">펫: ${pet.dominant}(${EL_HANJA[pet.dominant]}) 기운</span>
              <span class="text-[10px] font-black px-2 py-0.5 rounded-full text-white" style="background:${EL_COLOR[owner.dominant]}">집사: ${owner.dominant}(${EL_HANJA[owner.dominant]}) 기운</span>
            </div>
            <div class="space-y-1.5">${ELS.map(e => bar(e, pet.vec[e], owner.vec[e])).join('')}</div>
            <p class="text-[11px] text-gray-600 mt-3 leading-snug bg-rose-50/60 rounded-xl p-2.5">${hd.relation ? hd.relation.text : ''}</p>
          </div>
          <div>
            <h4 class="text-xs font-black text-gray-700 mb-2">💞 영역별 궁합</h4>
            <div class="grid grid-cols-2 gap-2">${(hd.areas || []).map(areaCard).join('')}</div>
          </div>
          ${luck !== null ? `<div class="bg-gradient-to-r from-rose-50 to-amber-50 rounded-2xl border border-rose-100 p-3 text-center">
            <span class="text-[11px] font-black text-gray-600">오늘의 궁합 지수</span>
            <span class="text-[18px] font-black text-rose-600 ml-2">${luck}</span>
            <p class="text-[10px] text-gray-500 mt-1">${luck >= 70 ? '💞 오늘은 함께하기 좋은 날! 펫게임 XP 1.2배 버프 발동 중' : '평온한 하루 — 꾸준한 케어가 내일의 운을 만들어요'}</p>
          </div>` : ''}
        </div>`;
    }

    function enrichOnMeasure() {
        try {
            const petB = document.getElementById('saju-pet-birth');
            const ownB = document.getElementById('saju-owner-birth');
            const pet = (typeof getSajuPet === 'function') ? getSajuPet() : null;
            const petISO = (petB && petB.value) || (pet && pet.harmonyData && pet.harmonyData.petBirth);
            const ownISO = (ownB && ownB.value) || (pet && pet.harmonyData && pet.harmonyData.ownerBirth);
            if (!pet || !petISO || !ownISO || typeof PetHarmony === 'undefined') return;
            const full = PetHarmony.analyze(petISO, ownISO);
            pet.harmonyData = Object.assign({}, pet.harmonyData, full,
                { petBirth: petISO, ownerBirth: ownISO, avgScore: (pet.harmonyData && pet.harmonyData.avgScore) || full.score });
            if (typeof saveState === 'function') saveState();
            renderPlus('harmony-plus', pet.harmonyData);
        } catch (e) { console.error('[harmony] enrich 실패', e); }
    }

    root.PetHarmonyView = { renderPlus, enrichOnMeasure };
})(typeof window !== 'undefined' ? window : globalThis);
