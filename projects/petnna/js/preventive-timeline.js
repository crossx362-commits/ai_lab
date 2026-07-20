// preventive-timeline.js — 생애주기 예방 타임라인 (백로그 나무 제안, P3)
// 반려동물 나이·종을 기반으로 생애 전체 권장 케어(접종·검진 등)를 '시간순 타임라인'으로 펼쳐
// 지난 시기(완료 추정)·현재 시기·다가올 시기를 한눈에 보여주고, 다음에 챙길 케어를 리마인더로 안내한다.
// 기존 preventive-checklist(현재 단계 '무엇을' 체크)와 상호보완: 이쪽은 생애 전체 '언제'의 흐름.
// 신규 인프라·라이브러리 없이 순수 JS. 저장 상태 없음(정적 안내).
(function () {
    'use strict';

    function _esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    // 나이(년) 파싱 — "2살 (청소년기)" → 2. 없으면 null.
    function _ageYears(pet) {
        if (!pet || !pet.age) return null;
        const m = String(pet.age).match(/\d+/);
        return m ? parseInt(m[0], 10) : null;
    }

    // 종·시기별 권장 케어 마일스톤(정적). from/to = 나이 하한/상한(년, to=null이면 이후 계속).
    // 견·묘는 세분화, 그 외는 공통 기본.
    const TIMELINE = {
        dog: [
            { from: 0, to: 1, label: '유년기', icon: '🍼', care: '기초 접종 완성기', items: ['종합백신(DHPPL) 3~4회', '광견병 1차', '기초 구충·첫 건강검진'] },
            { from: 1, to: 2, label: '청소년기', icon: '🐕', care: '예방 습관 정착', items: ['접종 1년차 추가', '심장사상충 매월 예방', '중성화 시기 상담'] },
            { from: 2, to: 7, label: '성년기', icon: '🦴', care: '연례 관리 유지', items: ['접종·광견병 연 1회', '치과 검진·스케일링', '연 1회 건강검진'] },
            { from: 7, to: null, label: '노령기', icon: '💗', care: '조기검진 강화', items: ['6개월마다 건강검진', '관절·심장·신장 모니터링', '치과·구강 정기 점검'] },
        ],
        cat: [
            { from: 0, to: 1, label: '유년기', icon: '🍼', care: '기초 접종 완성기', items: ['종합백신(FVRCP) 2~3회', '광견병 1차', 'FeLV/FIV 검사·첫 검진'] },
            { from: 1, to: 2, label: '청소년기', icon: '🐈', care: '예방 습관 정착', items: ['접종 1년차 추가', '외부 기생충 예방', '중성화 시기 상담'] },
            { from: 2, to: 7, label: '성년기', icon: '🐾', care: '연례 관리 유지', items: ['접종·광견병 연 1회', '치과 검진·구강 관리', '연 1회 건강검진'] },
            { from: 7, to: null, label: '노령기', icon: '💗', care: '조기검진 강화', items: ['6개월마다 건강검진', '신장·갑상선 모니터링', '음수량·체중 변화 관찰'] },
        ],
        _default: [
            { from: 0, to: 1, label: '유년기', icon: '🍼', care: '입양 초기 관리', items: ['첫 건강검진', '기생충 예방 상담', '적정 환경·먹이 점검'] },
            { from: 1, to: 2, label: '청소년기', icon: '🐾', care: '기본 관리 정착', items: ['연 1회 건강검진', '기생충 예방 지속', '신체 상태 점검'] },
            { from: 2, to: 7, label: '성년기', icon: '🐾', care: '연례 관리 유지', items: ['연 1회 정기 건강검진', '치아·구강 점검', '적정 체중·환경 관리'] },
            { from: 7, to: null, label: '노령기', icon: '💗', care: '조기검진 강화', items: ['6개월마다 건강검진', '노령 질환 조기 모니터링', '온도·활동량 세심 관리'] },
        ],
    };

    // 각 시기의 상태 판정: 나이 미상이면 전부 '예정'(current 없음).
    function _phaseState(phase, yrs) {
        if (yrs === null) return 'future';
        if (phase.to !== null && yrs >= phase.to) return 'past';
        if (yrs >= phase.from && (phase.to === null || yrs < phase.to)) return 'current';
        return 'future';
    }

    function renderPreventiveTimeline() {
        const host = document.getElementById('preventive-timeline');
        if (!host) return;
        const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        if (!pet) { host.innerHTML = ''; return; }

        const type = (pet && pet.type) || 'dog';
        const phases = TIMELINE[type] || TIMELINE._default;
        const yrs = _ageYears(pet);

        // 리마인더: 현재 시기가 있으면 그 시기, 아니면(미상) 첫 시기의 케어를 안내.
        const curIdx = phases.findIndex(p => _phaseState(p, yrs) === 'current');
        const remindPhase = curIdx >= 0 ? phases[curIdx] : null;

        const rows = phases.map(phase => {
            const st = _phaseState(phase, yrs);
            const range = phase.to === null ? `${phase.from}살~` : `${phase.from}~${phase.to}살`;
            const dotCls = st === 'current'
                ? 'bg-brand-500 ring-4 ring-brand-100'
                : st === 'past' ? 'bg-emerald-400' : 'bg-gray-300';
            const badge = st === 'current'
                ? '<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[9px] font-black bg-brand-500 text-white shrink-0">지금</span>'
                : st === 'past' ? '<span class="text-[9px] font-bold text-emerald-500 shrink-0">완료 추정</span>'
                    : '<span class="text-[9px] font-bold text-gray-400 shrink-0">예정</span>';
            const titleCls = st === 'future' ? 'text-gray-400' : 'text-gray-900';
            const items = phase.items.map(t =>
                `<li class="text-[10px] leading-snug keep-all ${st === 'future' ? 'text-gray-400' : 'text-gray-600'}">· ${_esc(t)}</li>`
            ).join('');
            return `
            <li class="relative pl-6 pb-3 last:pb-0">
                <span class="absolute left-0 top-1 w-3 h-3 rounded-full ${dotCls}"></span>
                <div class="flex items-center gap-1.5 mb-0.5">
                    <span class="shrink-0">${phase.icon}</span>
                    <span class="text-[12px] font-bold ${titleCls}">${_esc(phase.label)}</span>
                    <span class="text-[9px] font-semibold text-gray-400 tabular-nums shrink-0">${range}</span>
                    ${badge}
                </div>
                <p class="text-[10px] font-semibold text-brand-600 mb-0.5 keep-all">${_esc(phase.care)}</p>
                <ul class="space-y-0.5">${items}</ul>
            </li>`;
        }).join('');

        const reminder = remindPhase ? `
            <div class="mx-5 mb-3 px-3 py-2 rounded-xl bg-brand-50 border border-brand-100 flex items-start gap-2">
                <i class="fa-solid fa-bell text-brand-500 mt-0.5 shrink-0"></i>
                <p class="text-[10px] text-brand-800 leading-snug keep-all">
                    지금은 <b>${_esc(remindPhase.label)}</b>예요. 이번 시기엔 <b>${_esc(remindPhase.items[0])}</b> 등을 챙겨주세요.
                </p>
            </div>` : '';

        host.innerHTML = `
        <div class="card-modern overflow-hidden mb-3">
            <div class="px-5 pt-4 pb-3 border-b border-gray-100 flex items-center gap-2">
                <i class="fa-solid fa-timeline text-brand-500"></i>
                <h2 class="text-base font-bold text-gray-900 flex-1">생애주기 예방 타임라인</h2>
            </div>
            ${reminder}
            <div class="px-5 py-3">
                <ul class="relative before:absolute before:left-[5px] before:top-2 before:bottom-2 before:w-0.5 before:bg-gray-100">${rows}</ul>
                <p class="mt-2 text-[9px] text-gray-400 keep-all leading-snug">※ 일반 가이드입니다. 실제 접종·검진 일정은 반려동물 상태에 따라 담당 수의사와 상담하세요.</p>
            </div>
        </div>`;
    }

    window.renderPreventiveTimeline = renderPreventiveTimeline;
})();
