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
            <h4 class="text-xs font-black text-gray-700 mb-2">💞 영역별 조화도</h4>
            <div class="grid grid-cols-2 gap-2">${(hd.areas || []).map(areaCard).join('')}</div>
          </div>
          ${luck !== null ? `<div class="bg-gradient-to-r from-rose-50 to-amber-50 rounded-2xl border border-rose-100 p-3 text-center">
            <span class="text-[11px] font-black text-gray-600">오늘의 조화도 지수</span>
            <span class="text-[18px] font-black text-rose-600 ml-2">${luck}</span>
            <p class="text-[10px] text-gray-500 mt-1">${luck >= 70 ? '💞 오늘은 함께하기 좋은 날! 펫게임 XP 1.2배 버프 발동 중' : '평온한 하루 — 꾸준한 케어가 내일의 운을 만들어요'}</p>
          </div>` : ''}
        </div>`;
    }

    let _cal = null; // {y, m}
    function calMove(delta) {
        if (!_cal) return;
        _cal.m += delta;
        if (_cal.m < 0) { _cal.m = 11; _cal.y--; }
        if (_cal.m > 11) { _cal.m = 0; _cal.y++; }
        renderCalendar('fortune-calendar');
    }

    function renderCalendar(containerId) {
        const el = document.getElementById(containerId);
        if (!el) return;
        const pet = (typeof getSajuPet === 'function') ? getSajuPet() : null;
        const hd = pet && pet.harmonyData;
        if (!hd || !hd.elements || typeof PetHarmony === 'undefined') {
            el.innerHTML = `<div class="bg-white rounded-3xl p-5 border border-amber-100 text-center">
              <p class="text-xs font-bold text-gray-500">일진 캘린더는 조화도 측정 후 열려요</p>
              <button onclick="switchSajuSubTab('harmony')" class="mt-2 text-[11px] font-black text-white bg-rose-400 px-4 py-2 rounded-xl">💞 조화도 측정하러 가기</button></div>`;
            return;
        }
        const now = new Date();
        if (!_cal) _cal = { y: now.getFullYear(), m: now.getMonth() };
        const dom = pet.harmonyData.elements.pet.dominant;
        const first = new Date(Date.UTC(_cal.y, _cal.m, 1));
        const days = new Date(Date.UTC(_cal.y, _cal.m + 1, 0)).getUTCDate();
        const pad = first.getUTCDay();
        const today = { y: now.getFullYear(), m: now.getMonth(), d: now.getDate() };
        const idx = PetHarmony.todayIndex(hd.score, dom);
        let cells = '';
        for (let i = 0; i < pad; i++) cells += '<div></div>';
        for (let d = 1; d <= days; d++) {
            const luck = PetHarmony.dayLuck(new Date(Date.UTC(_cal.y, _cal.m, d)), dom);
            const marks = [luck.walk ? '🐾' : '', luck.groom ? '✂️' : '', luck.vet ? '🏥' : ''].join('');
            const isToday = _cal.y === today.y && _cal.m === today.m && d === today.d;
            cells += `<div class="rounded-lg py-1 ${isToday ? 'bg-rose-100 border border-rose-300' : ''}">
              <div class="text-[10px] font-bold ${isToday ? 'text-rose-600' : 'text-gray-600'}">${d}</div>
              <div class="text-[9px] leading-none h-3">${marks}</div></div>`;
        }
        el.innerHTML = `
        <div class="bg-white rounded-3xl p-4 border border-amber-100 shadow-sm mb-4">
          <div class="flex items-center justify-between mb-1">
            <button onclick="PetHarmonyView.calMove(-1)" class="text-gray-400 font-black px-2">‹</button>
            <span class="text-xs font-black">🗓️ ${_cal.y}년 ${_cal.m + 1}월 일진 캘린더</span>
            <button onclick="PetHarmonyView.calMove(1)" class="text-gray-400 font-black px-2">›</button>
          </div>
          <div class="text-center text-[10px] text-gray-400 font-bold mb-2">🐾 산책 길일 · ✂️ 미용 길일 · 🏥 병원 길일</div>
          <div class="grid grid-cols-7 gap-0.5 text-center">
            ${['일','월','화','수','목','금','토'].map(d => `<div class="text-[9px] font-black text-gray-400">${d}</div>`).join('')}
            ${cells}
          </div>
          <div class="mt-2 text-center bg-amber-50 rounded-xl py-1.5">
            <span class="text-[10px] font-bold text-gray-500">오늘의 조화도 지수</span>
            <span class="text-[14px] font-black text-rose-600 ml-1.5">${idx}</span>
          </div>
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

    root.PetHarmonyView = { renderPlus, enrichOnMeasure, renderCalendar, calMove };
})(typeof window !== 'undefined' ? window : globalThis);
