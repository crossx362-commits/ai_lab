// memorial.js — 무지개다리 추모 공간 (Memorial Mode, 백로그 나무 제안 P2)
// 펫 프로필의 memorial 상태를 읽어 홈(마이펫)에 추모 배너를 노출하고,
// 기존 앨범(일기)을 재사용한 추모 갤러리 모달 + 추모 카드 공유를 제공한다.
// 함께한 날 카운트 · 기일/생일 D-day 안내 포함.
// 신규 인프라·라이브러리 없이 순수 JS. memory-flashback.js와 동일한 홈 배너 패턴.
(function () {
    'use strict';

    function _esc(s) {
        s = (s == null) ? '' : String(s);
        if (typeof escapeHtml === 'function') return escapeHtml(s);
        return s.replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function _activePet() {
        return (typeof getActivePet === 'function') ? getActivePet() : null;
    }
    function _albums() {
        return (typeof albums !== 'undefined' && Array.isArray(albums)) ? albums : [];
    }
    function _parseDate(s) {
        if (!s) return null;
        const d = new Date(String(s) + 'T00:00:00');
        return isNaN(d) ? null : d;
    }
    function _daysBetween(a, b) {
        return Math.floor((b - a) / 86400000);
    }
    // 다가오는 기념일(매년 월-일)까지 남은 일수. 오늘이면 0.
    function _daysUntilAnniversary(dateStr, now) {
        const d = _parseDate(dateStr);
        if (!d) return null;
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        let next = new Date(now.getFullYear(), d.getMonth(), d.getDate());
        if (next < today) next = new Date(now.getFullYear() + 1, d.getMonth(), d.getDate());
        return _daysBetween(today, next);
    }

    // 추모 모드 여부 판정의 단일 소스. 케어 유도 모듈들이 이걸 묻는다 —
    // 각자 pet.memorial을 따로 읽으면 판정 기준이 갈라져 한쪽만 갱신되는 사고가 난다.
    function isMemorialPet(pet) {
        const p = (pet === undefined) ? _activePet() : pet;
        return !!(p && p.memorial);
    }

    // 순수 함수: pet → 추모 정보 또는 null(추모 상태 아님)
    function buildMemorialInfo(pet, now) {
        now = now || new Date();
        if (!pet || !pet.memorial) return null;
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const info = {
            name: pet.name || '반려동물',
            imageUrl: pet.imageUrl || '',
            type: pet.type || '',
            birthDate: pet.birthDate || '',
            memorialDate: pet.memorialDate || '',
            daysTogether: null,
            birthDday: null,
            memorialDday: null
        };
        const birth = _parseDate(pet.birthDate);
        const passed = _parseDate(pet.memorialDate);
        if (birth && passed && passed >= birth) {
            info.daysTogether = _daysBetween(birth, passed) + 1;
        }
        info.birthDday = pet.birthDate ? _daysUntilAnniversary(pet.birthDate, now) : null;
        info.memorialDday = pet.memorialDate ? _daysUntilAnniversary(pet.memorialDate, now) : null;
        return info;
    }

    // 다가오는 기념일 안내 문구(오늘이면 특별 문구). 없으면 ''.
    function _anniversaryLabel(info) {
        if (info.memorialDday === 0) return '오늘은 기일이에요 🕯️';
        if (info.birthDday === 0) return '오늘은 생일이에요 🎂';
        const parts = [];
        if (info.birthDday != null) parts.push({ d: info.birthDday, t: `생일 D-${info.birthDday}` });
        if (info.memorialDday != null) parts.push({ d: info.memorialDday, t: `기일 D-${info.memorialDday}` });
        if (!parts.length) return '';
        parts.sort((a, b) => a.d - b.d);
        return parts[0].t;
    }

    function renderMemorialBanner() {
        const host = document.getElementById('memorial-banner');
        if (!host) return;
        const info = buildMemorialInfo(_activePet(), new Date());
        if (!info) { host.innerHTML = ''; host.hidden = true; return; }
        host.hidden = false;

        const sub = [];
        if (info.daysTogether != null) sub.push(`함께한 ${info.daysTogether.toLocaleString()}일`);
        const anni = _anniversaryLabel(info);
        if (anni) sub.push(anni);
        const subText = sub.length ? sub.join(' · ') : '소중한 추억을 다시 만나보세요';

        host.innerHTML = `
        <button type="button" onclick="openMemorialSpace()" class="w-full text-left p-3.5 bg-gradient-to-r from-slate-50 to-brand-50 flex items-center gap-3 hover:from-slate-100 transition-colors">
            <span class="text-xl shrink-0">🌈</span>
            <span class="min-w-0 flex-1">
                <span class="block text-xs font-black text-brand-900">${_esc(info.name)}의 추모 공간</span>
                <span class="block text-[11px] text-brand-500 font-medium truncate mt-0.5">${_esc(subText)}</span>
            </span>
            <i class="fa-solid fa-chevron-right text-brand-300 text-xs shrink-0"></i>
        </button>`;
    }

    function _ensureModal() {
        let modal = document.getElementById('memorial-modal');
        if (modal) return modal;
        modal = document.createElement('div');
        modal.id = 'memorial-modal';
        modal.className = 'hidden fixed inset-0 z-[70] bg-black/50 items-center justify-center p-4';
        modal.innerHTML = `<div class="bg-white rounded-3xl w-full max-w-md max-h-[90vh] overflow-y-auto shadow-2xl" id="memorial-modal-panel"></div>`;
        modal.addEventListener('click', function (e) { if (e.target === modal) closeMemorialSpace(); });
        document.body.appendChild(modal);
        return modal;
    }

    function openMemorialSpace() {
        const info = buildMemorialInfo(_activePet(), new Date());
        if (!info) { if (typeof showToast === 'function') showToast('추모 모드가 켜진 반려동물이 없어요.'); return; }
        const modal = _ensureModal();
        const panel = document.getElementById('memorial-modal-panel');

        const photos = _albums().filter(function (a) { return a && a.url && !a.isVideo; }).slice(0, 12);
        const gallery = photos.length
            ? `<div class="grid grid-cols-3 gap-1.5">${photos.map(function (a) {
                return `<div class="aspect-square rounded-lg overflow-hidden bg-gray-100"><img loading="lazy" src="${_esc(a.url)}" class="w-full h-full object-cover"></div>`;
            }).join('')}</div>`
            : `<p class="text-xs text-gray-400 text-center py-6">아직 담긴 사진이 없어요.<br>일기(앨범)에 사진을 남기면 이곳에 함께 모여요.</p>`;

        const chips = [];
        if (info.daysTogether != null) chips.push(`함께한 ${info.daysTogether.toLocaleString()}일`);
        if (info.birthDate) chips.push(`생일 ${_esc(info.birthDate)}`);
        if (info.memorialDate) chips.push(`무지개다리 ${_esc(info.memorialDate)}`);
        const anni = _anniversaryLabel(info);

        const avatar = info.imageUrl
            ? `<img src="${_esc(info.imageUrl)}" class="w-20 h-20 rounded-full object-cover mx-auto shadow-md border-2 border-white">`
            : `<div class="w-20 h-20 rounded-full mx-auto bg-brand-50 flex items-center justify-center text-3xl shadow-md">🐾</div>`;

        panel.innerHTML = `
        <div class="relative bg-gradient-to-b from-brand-50 to-white px-6 pt-6 pb-5 text-center">
            <button type="button" onclick="closeMemorialSpace()" class="absolute top-4 right-4 text-gray-300 hover:text-gray-500" aria-label="닫기"><i class="fa-solid fa-xmark text-lg"></i></button>
            <div class="text-2xl mb-2">🌈</div>
            ${avatar}
            <h3 class="text-lg font-black text-gray-900 mt-3">${_esc(info.name)}</h3>
            <p class="text-[11px] text-brand-500 font-bold mt-0.5">무지개다리 건너 별이 된 우리 아이</p>
            ${anni ? `<p class="text-xs font-black text-rose-500 mt-1.5">${_esc(anni)}</p>` : ''}
            ${chips.length ? `<div class="flex flex-wrap justify-center gap-1.5 mt-3">${chips.map(function (c) { return `<span class="text-[10px] font-bold text-brand-600 bg-brand-50 rounded-full px-2.5 py-1">${c}</span>`; }).join('')}</div>` : ''}
        </div>
        <div class="px-5 pb-5 space-y-4">
            <div>
                <p class="text-[11px] font-black text-gray-500 mb-2">📸 추모 갤러리</p>
                ${gallery}
            </div>
            <button type="button" onclick="shareMemorialCard()" class="w-full bg-brand-500 hover:bg-brand-600 text-white font-bold text-xs py-3 rounded-xl transition-colors shadow-soft">
                <i class="fa-solid fa-share-nodes mr-1.5"></i>추모 카드 공유하기
            </button>
        </div>`;

        modal.classList.remove('hidden');
        modal.classList.add('flex');
    }

    function closeMemorialSpace() {
        const modal = document.getElementById('memorial-modal');
        if (modal) { modal.classList.add('hidden'); modal.classList.remove('flex'); }
    }

    function _memorialMessage(info) {
        const lines = [`🌈 무지개다리를 건넌 ${info.name}를 기억합니다.`];
        if (info.daysTogether != null) lines.push(`함께한 ${info.daysTogether.toLocaleString()}일, 영원히 잊지 않을게요.`);
        else lines.push('영원히 잊지 않을게요.');
        lines.push('#펫나 #무지개다리 #추모');
        return lines.join('\n');
    }

    function shareMemorialCard() {
        const info = buildMemorialInfo(_activePet(), new Date());
        if (!info) return;
        const text = _memorialMessage(info);
        if (navigator.share) {
            navigator.share({ title: `${info.name} 추모 카드`, text: text }).catch(function () {});
            return;
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () {
                if (typeof showToast === 'function') showToast('추모 카드 문구를 복사했어요. 💌');
            }).catch(function () {
                if (typeof showToast === 'function') showToast('공유를 사용할 수 없어요.');
            });
            return;
        }
        if (typeof showToast === 'function') showToast('공유를 사용할 수 없는 환경이에요.');
    }

    // 프로필 편집 패널의 추모 체크박스 → 날짜 입력 표시 토글
    function toggleMemorialFields() {
        const cb = document.getElementById('settings-pet-memorial');
        const fields = document.getElementById('settings-memorial-fields');
        if (fields) fields.classList.toggle('hidden', !(cb && cb.checked));
    }

    window.isMemorialPet = isMemorialPet;
    window.buildMemorialInfo = buildMemorialInfo;
    window.renderMemorialBanner = renderMemorialBanner;
    window.openMemorialSpace = openMemorialSpace;
    window.closeMemorialSpace = closeMemorialSpace;
    window.shareMemorialCard = shareMemorialCard;
    window.toggleMemorialFields = toggleMemorialFields;
})();
