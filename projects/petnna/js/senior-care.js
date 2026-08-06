// senior-care.js — '시니어 케어' 단일 허브 (백로그 회의 제안, P2)
// 홈(마이펫)에 '우리 아이 나이 → 노령케어 대시보드' 진입점을 신설하고,
// 기존 5개 모듈을 비용 → 간병 → 임종 준비 3단계 여정으로 라우팅한다.
// 신규 저장소·라이브러리 없음: 각 단계 버튼은 기존 모듈의 공개 함수를 호출만 한다.
(function () {
    'use strict';

    var SENIOR_AGE = 7; // 통상 노령 진입 기준(개·고양이 약 7세)
    var _enteredAge = null; // 나이 미기록 펫이 진입점에서 직접 입력한 값

    function esc(s) {
        if (typeof window.escapeHtml === 'function') return window.escapeHtml(s);
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function activePet() { return typeof getActivePet === 'function' ? getActivePet() : null; }

    // 기존 preventive-timeline.js와 동일한 방식으로 나이(세)를 파싱한다.
    function ageYears(pet) {
        if (!pet || !pet.age) return null;
        var m = String(pet.age).match(/\d+/);
        return m ? parseInt(m[0], 10) : null;
    }
    function effectiveAge(pet) {
        var a = ageYears(pet);
        return a != null ? a : _enteredAge;
    }

    // 건강 탭 위젯으로 이동(탭 전환이 렌더를 트리거하므로 스크롤은 다음 틱)
    function goHealth(anchorId) {
        close();
        if (typeof switchTab === 'function') switchTab('health');
        setTimeout(function () {
            var el = document.getElementById(anchorId);
            if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }, 300);
    }

    // 3단계 여정 정의 — 각 모듈은 기존 공개 함수로만 연결한다.
    var STAGES = [
        {
            key: 'cost', emoji: '💰', title: '1단계 · 비용 준비',
            desc: '노령기엔 병원비가 늘어요. 시세를 알고 미리 대비해요.',
            items: [
                { emoji: '🏥', label: '병원비 시세 비교', desc: '진료 항목별 평균가 확인·제보', act: 'vetcost' },
                { emoji: '🧾', label: '병원비 기록·연간 예측', desc: '진료 기록으로 연간 지출 가늠', act: 'medical' }
            ]
        },
        {
            key: 'care', emoji: '🤲', title: '2단계 · 간병',
            desc: '삶의 질을 살피고 작은 이상신호를 놓치지 않아요.',
            items: [
                { emoji: '📋', label: '삶의 질(QOL) 주간 체크인', desc: '식욕·통증·활력 주간 기록', act: 'qol' },
                { emoji: '🩺', label: '건강 이상신호 점검', desc: '대소변·체중·컨디션 변화 감지', act: 'wellness' }
            ]
        },
        {
            key: 'endoflife', emoji: '🕊️', title: '3단계 · 임종 준비',
            desc: '말년의 정기 검진과 예방 일정을 함께 챙겨요.',
            items: [
                { emoji: '📅', label: '정기·예방 케어 타임라인', desc: '나이에 맞는 검진·예방 일정', act: 'preventive' }
            ]
        }
    ];

    // 진입점 버튼 → 해당 모듈로 라우팅
    function route(act) {
        if (act === 'vetcost') { close(); if (window.VetCostBoard) VetCostBoard.open(); return; }
        if (act === 'qol') { close(); if (window.QolCheckin) QolCheckin.open(); return; }
        if (act === 'medical') { goHealth('medical-records-timeline'); return; }
        if (act === 'wellness') { goHealth('wellness-anomaly-card'); return; }
        if (act === 'preventive') { goHealth('preventive-timeline'); return; }
    }

    function stageHtml(st) {
        var items = st.items.map(function (it) {
            return '<button type="button" onclick="SeniorCare.route(\'' + it.act + '\')" ' +
                'class="w-full flex items-center gap-3 text-left px-3 py-2.5 rounded-xl border border-gray-100 hover:border-brand-300 hover:bg-brand-50/40 active:scale-[0.98] transition-all">' +
                '<span class="text-xl shrink-0">' + it.emoji + '</span>' +
                '<span class="min-w-0"><span class="block text-sm font-bold text-gray-800">' + esc(it.label) + '</span>' +
                '<span class="block text-[11px] text-gray-400 truncate">' + esc(it.desc) + '</span></span>' +
                '<span class="ml-auto text-gray-300">›</span></button>';
        }).join('');
        return '<div class="rounded-2xl bg-gray-50/70 p-4">' +
            '<div class="flex items-center gap-2 mb-1"><span class="text-lg">' + st.emoji + '</span>' +
            '<h4 class="text-sm font-extrabold text-gray-900">' + esc(st.title) + '</h4></div>' +
            '<p class="text-[11px] text-gray-400 mb-3">' + esc(st.desc) + '</p>' +
            '<div class="space-y-2">' + items + '</div></div>';
    }

    function open() {
        close();
        var pet = activePet();
        var age = effectiveAge(pet);
        var name = pet && pet.name ? esc(pet.name) : '우리 아이';
        var ageLine = age != null
            ? name + ' · ' + age + '세' + (age >= SENIOR_AGE ? ' (노령기)' : ' (노령기 전 — 미리 준비)')
            : name + ' · 나이 미입력';

        var overlay = document.createElement('div');
        overlay.id = 'seniorcare-overlay';
        overlay.className = 'fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4';
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[85vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🌗 노령케어 여정</h3>' +
            '<button onclick="SeniorCare.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' +
            '<p class="text-xs text-gray-500 leading-relaxed"><span class="font-bold text-gray-700">' + ageLine + '</span><br>' +
            '비용 → 간병 → 임종 준비 3단계로, 지금 필요한 케어부터 안내해요.</p>' +
            STAGES.map(stageHtml).join('') +
            '</div></div>';
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function close() {
        var el = document.getElementById('seniorcare-overlay');
        if (el) el.remove();
    }

    // 나이 미입력 펫: 진입점에서 바로 입력받아 대시보드를 연다(값은 세션 내 사용).
    function startFromInput() {
        var input = document.getElementById('seniorcare-age-input');
        if (input) {
            var v = parseInt(input.value, 10);
            if (!isNaN(v) && v >= 0) _enteredAge = v;
        }
        open();
    }

    // 홈(마이펫) 진입점 카드
    function renderEntry() {
        var host = document.getElementById('senior-care-entry');
        if (!host) return;
        // 추모 모드에서는 노령케어 유도를 띄우지 않는다(오늘의 케어 카드와 동일 원칙).
        if (typeof isMemorialPet === 'function' && isMemorialPet()) { host.innerHTML = ''; return; }
        var pet = activePet();
        if (!pet) { host.innerHTML = ''; return; }

        var age = effectiveAge(pet);
        var body;
        if (age == null) {
            // 나이 미입력 → 입력 유도
            body =
                '<div class="flex items-center gap-2 mt-2">' +
                '<input id="seniorcare-age-input" type="number" min="0" max="40" placeholder="나이(세)" ' +
                'class="w-24 rounded-lg border border-gray-200 px-2 py-1.5 text-sm focus:border-brand-400 focus:outline-none">' +
                '<button onclick="SeniorCare.startFromInput()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full shadow-soft">노령케어 시작</button>' +
                '</div>';
        } else {
            var sub = age >= SENIOR_AGE
                ? '노령기예요 · ' + age + '세 — 비용·간병·임종 준비를 단계별로 챙겨요.'
                : '아직 노령기 전(' + age + '세) — 미리 여정을 준비해요.';
            body =
                '<p class="text-[11px] text-gray-400 mt-1">' + esc(sub) + '</p>' +
                '<button onclick="SeniorCare.open()" class="mt-2 text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full shadow-soft">여정 대시보드 열기</button>';
        }

        // 호스트 #senior-care-entry의 부모가 이미 카드(#home-today-card .card-modern
        // .divide-y)라 여기서 card-modern을 또 두면 '카드 속 카드'가 된다(봄이 QA 559a215bb044).
        // 형제 섹션(오늘의 운세 등)과 같은 px-4 py-3 블록으로 맞춘다 — divide-y가 구분선을 준다.
        host.innerHTML =
            '<div class="px-4 py-3">' +
            '<div class="flex items-center gap-2">' +
            '<span class="text-xl">🌗</span>' +
            '<h3 class="text-base font-bold text-gray-900">노령케어 여정</h3>' +
            '</div>' + body + '</div>';
    }

    window.SeniorCare = {
        open: open, close: close, route: route, renderEntry: renderEntry,
        startFromInput: startFromInput, SENIOR_AGE: SENIOR_AGE
    };
})();
