// training-passport.js — 트레이닝 패스포트 (백로그 나무 제안, P3)
// 반려동물이 익힌 훈련 항목(앉아·기다려 등)을 영구 체크리스트로 관리하고, 훈련한 날을
// 연속 기록(streak)으로 이어붙이며, 습득 개수에 따라 배지 등급을 부여해 참여율·성취도를 높인다.
// 신규 인프라·라이브러리 없이 순수 JS. 상태는 펫별로 localStorage에만 저장한다.
// 기존 주간 '훈련 미션'(achievements.js, 매주 초기화)과 상호보완: 이쪽은 '평생 습득 목록'.
(function () {
    'use strict';

    function _esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    // 종별 핵심 훈련 항목(정적 데이터). id는 저장 키라 바꾸지 말 것.
    const SKILLS = {
        dog: [
            { id: 'sit',      icon: '🐕', text: '앉아 (Sit)' },
            { id: 'wait',     icon: '✋', text: '기다려 (Wait)' },
            { id: 'come',     icon: '🏃', text: '이리와 (Come)' },
            { id: 'down',     icon: '🛌', text: '엎드려 (Down)' },
            { id: 'paw',      icon: '🐾', text: '손 (Shake)' },
            { id: 'no',       icon: '🚫', text: '안돼 (No)' },
            { id: 'potty',    icon: '🚽', text: '배변 훈련' },
            { id: 'leash',    icon: '🦮', text: '리드줄 산책 예절' },
        ],
        cat: [
            { id: 'name',     icon: '🐈', text: '이름 부르면 반응' },
            { id: 'scratch',  icon: '🪵', text: '스크래처 사용' },
            { id: 'carrier',  icon: '🧺', text: '이동장 적응' },
            { id: 'brush',    icon: '🪮', text: '빗질 받아들이기' },
            { id: 'toilet',   icon: '🚽', text: '화장실 사용' },
            { id: 'highfive', icon: '🙌', text: '하이파이브' },
        ],
        _default: [
            { id: 'bond',     icon: '🤲', text: '손 위 간식 받기(교감)' },
            { id: 'name',     icon: '📣', text: '이름 부르면 반응' },
            { id: 'target',   icon: '🎯', text: '타겟 따라오기' },
            { id: 'handle',   icon: '🩺', text: '몸 만지기 적응' },
        ],
    };

    // 습득 개수 기반 배지 등급(누적).
    const TIERS = [
        { min: 0, emoji: '🐣', label: '훈련 새싹' },
        { min: 2, emoji: '🎒', label: '훈련 견습생' },
        { min: 4, emoji: '🎓', label: '훈련 우등생' },
        { min: 6, emoji: '🏅', label: '훈련 마스터' },
    ];
    function _tier(n) {
        let t = TIERS[0];
        for (const x of TIERS) if (n >= x.min) t = x;
        return t;
    }

    function _skills(pet) {
        const type = (pet && pet.type) || 'dog';
        return SKILLS[type] || SKILLS._default;
    }

    function _petId(pet) { return pet && pet.id != null ? pet.id : 'x'; }
    function _masterKey(pet) { return 'petnna_training_passport_' + _petId(pet); }
    function _logKey(pet) { return 'petnna_training_log_' + _petId(pet); }

    function _getMastered(pet) {
        try { return JSON.parse(localStorage.getItem(_masterKey(pet)) || '{}') || {}; }
        catch (e) { return {}; }
    }
    function _getLog(pet) {
        try {
            const v = JSON.parse(localStorage.getItem(_logKey(pet)) || '[]');
            return Array.isArray(v) ? v : [];
        } catch (e) { return []; }
    }

    function _today() { return new Date().toISOString().split('T')[0]; }

    // 오늘 훈련한 것으로 기록(연속일 계산용). 중복 날짜는 추가 안 함.
    function _markToday(pet) {
        const log = _getLog(pet);
        const today = _today();
        if (!log.includes(today)) {
            log.push(today);
            try { localStorage.setItem(_logKey(pet), JSON.stringify(log)); } catch (e) {}
        }
    }

    // 연속 훈련일 계산: 오늘 또는 어제부터 하루도 안 끊긴 일수.
    function _streak(pet) {
        const dates = _getLog(pet).slice().sort().reverse();
        if (!dates.length) return 0;
        const today = _today();
        const dayDiff = (new Date(today) - new Date(dates[0])) / (1000 * 60 * 60 * 24);
        if (dayDiff > 1) return 0;
        let streak = 1;
        let check = new Date(dates[0]);
        check.setDate(check.getDate() - 1);
        for (let i = 0; i < 365; i++) {
            const ds = check.toISOString().split('T')[0];
            if (dates.includes(ds)) { streak++; check.setDate(check.getDate() - 1); }
            else break;
        }
        return streak;
    }

    const COIN_PER_SKILL = 10;

    function toggleTrainingSkill(id) {
        const pet = (typeof getActivePet === 'function') ? getActivePet()
            : (typeof pets !== 'undefined' ? pets[0] : null);
        if (!pet) return;
        const map = _getMastered(pet);
        const wasOn = !!map[id];
        map[id] = !wasOn;
        try { localStorage.setItem(_masterKey(pet), JSON.stringify(map)); } catch (e) {}
        // 새로 습득(off→on)일 때만: 오늘 훈련 기록 + 최초 1회 코인 지급(재체크 파밍 방지)
        if (!wasOn) {
            _markToday(pet);
            if (!map['_rewarded_' + id]) {
                map['_rewarded_' + id] = true;
                try { localStorage.setItem(_masterKey(pet), JSON.stringify(map)); } catch (e) {}
                if (typeof _earnCoins === 'function') _earnCoins(pet, COIN_PER_SKILL, '트레이닝 패스포트');
                if (typeof saveState === 'function') saveState();
            }
        }
        renderTrainingPassport();
        if (typeof renderAchievementBadges === 'function') renderAchievementBadges();
    }

    function renderTrainingPassport() {
        const host = document.getElementById('training-passport-card');
        if (!host) return;
        const pet = (typeof getActivePet === 'function') ? getActivePet()
            : (typeof pets !== 'undefined' ? pets[0] : null);
        if (!pet) {
            host.innerHTML = `
                <div class="flex items-center gap-2 text-[10px] text-gray-400 font-bold">
                    <i class="fa-solid fa-passport text-gray-300"></i>
                    <span>반려동물을 등록하면 트레이닝 패스포트가 열려요!</span>
                </div>`;
            return;
        }

        const skills = _skills(pet);
        const mastered = _getMastered(pet);
        const doneCount = skills.reduce((n, s) => n + (mastered[s.id] ? 1 : 0), 0);
        const pct = Math.round((doneCount / skills.length) * 100);
        const tier = _tier(doneCount);
        const streak = _streak(pet);

        const rows = skills.map(s => {
            const on = !!mastered[s.id];
            return `
            <li>
                <button type="button" onclick="toggleTrainingSkill('${_esc(s.id)}')"
                    class="w-full flex items-center gap-2.5 text-left p-2 rounded-xl hover:bg-brand-50/60 transition-colors">
                    <span class="shrink-0 w-5 h-5 rounded-md border-2 flex items-center justify-center text-[10px] ${on ? 'bg-brand-500 border-brand-500 text-white' : 'border-gray-300 text-transparent'}">✓</span>
                    <span class="shrink-0">${s.icon}</span>
                    <span class="min-w-0 flex-1 text-[11px] leading-snug keep-all ${on ? 'text-brand-700 font-bold' : 'text-gray-700'}">${_esc(s.text)}</span>
                    ${on ? '<span class="shrink-0 text-[9px] font-black text-brand-500">습득</span>' : ''}
                </button>
            </li>`;
        }).join('');

        host.innerHTML = `
        <div class="flex items-center gap-2 mb-2">
            <span class="text-lg">${tier.emoji}</span>
            <div class="min-w-0 flex-1">
                <p class="text-[11px] font-black text-gray-800 leading-tight">🎫 트레이닝 패스포트</p>
                <p class="text-[9px] text-gray-500 font-medium">${_esc(tier.label)} · 습득 ${doneCount}/${skills.length}</p>
            </div>
            <span class="shrink-0 inline-flex items-center gap-1 px-2 py-1 rounded-full text-[10px] font-black bg-orange-50 text-orange-600">🔥 ${streak}일</span>
        </div>
        <div class="h-2 w-full bg-brand-100 rounded-full overflow-hidden mb-2.5">
            <div class="h-full bg-brand-500 rounded-full transition-all" style="width:${pct}%"></div>
        </div>
        <ul class="space-y-0.5">${rows}</ul>
        <p class="mt-2 text-[9px] text-gray-400 keep-all leading-snug">※ 익힌 훈련을 체크하면 패스포트에 기록되고, 훈련한 날이 연속으로 이어지면 🔥 스트릭이 쌓여요.</p>`;
    }

    window.renderTrainingPassport = renderTrainingPassport;
    window.toggleTrainingSkill = toggleTrainingSkill;
})();
