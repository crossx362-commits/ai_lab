// preventive-checklist.js — 생애주기 예방케어 체크리스트 (백로그 나무 제안, P2)
// 종·나이·중성화 여부로 라이프스테이지를 판정해 단계별 권장 예방 항목(접종·구충·치과·검진)을
// 정적 데이터 체크리스트로 노출한다. 2026 예방형 케어 트렌드 대응.
// 신규 인프라·라이브러리 없이 순수 JS. 체크 상태만 localStorage에 펫별로 저장한다.
// 기존 renderPreventiveCareDashboard(일정 D-Day)와 상호보완: 이쪽은 '무엇을 챙겨야 하는지' 안내.
(function () {
    'use strict';

    function _esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    // 나이(년) 파싱 — "2살 (청소년기)" → 2
    function _ageYears(pet) {
        if (!pet || !pet.age) return null;
        const m = String(pet.age).match(/\d+/);
        return m ? parseInt(m[0], 10) : null;
    }

    // 중성화 완료 여부 — gender 문자열에 '중성화' 포함
    function _isNeutered(pet) {
        return !!(pet && pet.gender && String(pet.gender).includes('중성화'));
    }

    // 라이프스테이지 판정: puppy(1살 미만) / junior(1~2) / adult(2~7) / senior(7살~)
    function _lifeStage(pet) {
        const txt = pet && pet.age ? String(pet.age) : '';
        const yrs = _ageYears(pet);
        if (/자견|자묘|유년|아기|퍼피/.test(txt) || (yrs !== null && yrs < 1)) return 'puppy';
        if (/노령|시니어|노견|노묘/.test(txt) || (yrs !== null && yrs >= 7)) return 'senior';
        if (yrs !== null && yrs < 2) return 'junior';
        return 'adult';
    }

    const STAGE_LABEL = { puppy: '유년기', junior: '청소년기', adult: '성년기', senior: '노령기' };

    // 종·단계별 권장 예방 항목(정적 데이터). 각 항목은 {icon, text}.
    // 견·묘는 세분화, 그 외(토끼·햄스터 등)는 공통 기본 항목.
    const CHECKLIST = {
        dog: {
            puppy: [
                { icon: '💉', text: '종합백신(DHPPL) 6~16주 3~4회 접종' },
                { icon: '🦠', text: '광견병 예방접종(생후 3개월 이후)' },
                { icon: '🪱', text: '내부 구충(회충·십이지장충) 정기 구충' },
                { icon: '🩺', text: '첫 건강검진·기생충 분변 검사' },
            ],
            junior: [
                { icon: '💉', text: '종합백신·광견병 1년차 추가 접종' },
                { icon: '🦟', text: '심장사상충 예방(매월, 연중)' },
                { icon: '🐛', text: '외부 기생충(진드기·벼룩) 예방' },
                { icon: '✂️', text: '중성화 수술 권장 시기(수의사 상담)' },
            ],
            adult: [
                { icon: '💉', text: '종합백신·광견병 연 1회 추가 접종' },
                { icon: '🦟', text: '심장사상충 예방(매월, 연중)' },
                { icon: '🦷', text: '치과 검진·스케일링 연 1회 권장' },
                { icon: '🩺', text: '연 1회 정기 건강검진' },
            ],
            senior: [
                { icon: '🩺', text: '6개월마다 건강검진(혈액·소변 포함)' },
                { icon: '🦷', text: '치과·구강 상태 정기 점검' },
                { icon: '🦟', text: '심장사상충·기생충 예방 지속' },
                { icon: '💗', text: '관절·심장·신장 노령 질환 모니터링' },
            ],
        },
        cat: {
            puppy: [
                { icon: '💉', text: '종합백신(FVRCP) 8~16주 2~3회 접종' },
                { icon: '🦠', text: '광견병 예방접종(생후 3개월 이후)' },
                { icon: '🪱', text: '내부 구충 정기 구충' },
                { icon: '🩺', text: '첫 건강검진·전염병(FeLV/FIV) 검사' },
            ],
            junior: [
                { icon: '💉', text: '종합백신·광견병 1년차 추가 접종' },
                { icon: '🐛', text: '외부 기생충(진드기·벼룩) 예방' },
                { icon: '✂️', text: '중성화 수술 권장 시기(수의사 상담)' },
                { icon: '🩺', text: '연 1회 건강검진' },
            ],
            adult: [
                { icon: '💉', text: '종합백신·광견병 연 1회 추가 접종' },
                { icon: '🦷', text: '치과 검진·구강 관리 연 1회 권장' },
                { icon: '🐛', text: '기생충 예방 지속' },
                { icon: '🩺', text: '연 1회 정기 건강검진' },
            ],
            senior: [
                { icon: '🩺', text: '6개월마다 건강검진(신장·갑상선 포함)' },
                { icon: '🦷', text: '치과·구강 상태 정기 점검' },
                { icon: '💧', text: '음수량·체중 변화 모니터링' },
                { icon: '💗', text: '만성 신부전 등 노령 질환 조기 검진' },
            ],
        },
        _default: {
            puppy: [
                { icon: '🩺', text: '입양 후 첫 건강검진' },
                { icon: '🪱', text: '기생충 예방·구충 상담' },
                { icon: '🏠', text: '적정 환경·먹이 관리 점검' },
            ],
            junior: [
                { icon: '🩺', text: '연 1회 건강검진' },
                { icon: '🪱', text: '기생충 예방 지속' },
                { icon: '🦷', text: '치아·발톱 등 신체 상태 점검' },
            ],
            adult: [
                { icon: '🩺', text: '연 1회 정기 건강검진' },
                { icon: '🦷', text: '치아·구강 상태 점검' },
                { icon: '🏠', text: '적정 체중·환경 관리' },
            ],
            senior: [
                { icon: '🩺', text: '6개월마다 건강검진' },
                { icon: '💗', text: '노령 질환 조기 모니터링' },
                { icon: '🏠', text: '온도·활동량 등 환경 세심 관리' },
            ],
        },
    };

    function _items(pet) {
        const type = (pet && pet.type) || 'dog';
        const stage = _lifeStage(pet);
        const bySpecies = CHECKLIST[type] || CHECKLIST._default;
        const items = (bySpecies[stage] || []).slice();
        // 중성화 미완료면 성년기까지 항목 상단에 안내(노령기는 제외)
        if (!_isNeutered(pet) && stage !== 'senior') {
            const already = items.some(i => i.text.includes('중성화'));
            if (!already) items.unshift({ icon: '✂️', text: '중성화 수술 여부 수의사 상담' });
        }
        return { stage, items };
    }

    function _checkKey(pet) {
        return 'petnna_prevcare_check_' + (pet && pet.id != null ? pet.id : 'x');
    }
    function _getChecked(pet) {
        try { return JSON.parse(localStorage.getItem(_checkKey(pet)) || '{}') || {}; }
        catch (e) { return {}; }
    }

    function togglePreventiveCheck(idx) {
        const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        if (!pet) return;
        const map = _getChecked(pet);
        map[idx] = !map[idx];
        try { localStorage.setItem(_checkKey(pet), JSON.stringify(map)); } catch (e) {}
        renderPreventiveChecklist();
    }

    function renderPreventiveChecklist() {
        const host = document.getElementById('preventive-checklist');
        if (!host) return;
        const pet = (typeof getActivePet === 'function') ? getActivePet() : null;
        if (!pet) { host.innerHTML = ''; return; }

        const { stage, items } = _items(pet);
        if (!items.length) { host.innerHTML = ''; return; }

        const checked = _getChecked(pet);
        const doneCount = items.reduce((n, _, i) => n + (checked[i] ? 1 : 0), 0);
        const pct = Math.round((doneCount / items.length) * 100);

        const rows = items.map((it, i) => {
            const on = !!checked[i];
            return `
            <li>
                <button type="button" onclick="togglePreventiveCheck(${i})"
                    class="w-full flex items-start gap-2.5 text-left p-2 rounded-xl hover:bg-brand-50/60 transition-colors">
                    <span class="mt-0.5 shrink-0 w-5 h-5 rounded-md border-2 flex items-center justify-center text-[10px] ${on ? 'bg-emerald-500 border-emerald-500 text-white' : 'border-gray-300 text-transparent'}">✓</span>
                    <span class="shrink-0">${it.icon}</span>
                    <span class="min-w-0 flex-1 text-[11px] leading-snug keep-all ${on ? 'line-through text-gray-400' : 'text-gray-700'}">${_esc(it.text)}</span>
                </button>
            </li>`;
        }).join('');

        host.innerHTML = `
        <div class="card-modern overflow-hidden mb-3">
            <div class="px-5 pt-4 pb-3 border-b border-gray-100 flex items-center gap-2">
                <i class="fa-solid fa-clipboard-check text-brand-500"></i>
                <h2 class="text-base font-bold text-gray-900 flex-1">생애주기 예방케어</h2>
                <span class="inline-flex items-center px-2.5 py-1 rounded-full text-[10px] font-black bg-brand-50 text-brand-700 shrink-0">${_esc(STAGE_LABEL[stage] || '')}</span>
            </div>
            <div class="px-5 py-4">
                <div class="flex items-center justify-between mb-1">
                    <span class="text-[10px] font-black text-emerald-700 uppercase tracking-wide">권장 예방 항목</span>
                    <span class="text-[10px] font-bold text-gray-500 tabular-nums">${doneCount}/${items.length} 확인</span>
                </div>
                <div class="h-2 w-full bg-emerald-100 rounded-full overflow-hidden mb-2.5">
                    <div class="h-full bg-emerald-500 rounded-full transition-all" style="width:${pct}%"></div>
                </div>
                <ul class="space-y-0.5">${rows}</ul>
                <p class="mt-2.5 text-[9px] text-gray-400 keep-all leading-snug">※ 일반 가이드입니다. 실제 접종·구충 일정은 반려동물 상태에 따라 담당 수의사와 상담하세요.</p>
            </div>
        </div>`;
    }

    window.renderPreventiveChecklist = renderPreventiveChecklist;
    window.togglePreventiveCheck = togglePreventiveCheck;
})();
