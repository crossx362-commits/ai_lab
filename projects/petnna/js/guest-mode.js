// guest-mode.js — 가입 없이 먼저 둘러보기 (회의 결정 2026-08-08, 오너 승인)
//
// 왜 만드나
// ---------
// 2026 기준 가입 UX 조사에서 반복 확인된 것: **계정 생성을 미루면 전환이 오른다.**
// 로그인 화면이 "이 앱이 뭘 하는지"를 보여주기 전에 자격증명부터 요구하면 이탈한다.
//
// 어떻게 안전하게 하나
// -------------------
// 새 자격증명·새 테이블·새 RLS를 만들지 않는다. 기존 데모 세션 방식
// (butler@petna.co.kr, app.js의 ?demo=1 경로와 동일)을 재사용하고,
// **원격 쓰기는 supabase.js의 isReadOnlySession() 가드가 데이터 계층에서 막는다** —
// CTA를 하나씩 감싸면 새 버튼이 생길 때마다 뚫리기 때문이다(2026-07-25 교훈).
//
// 게스트가 만드는 기록은 localStorage에만 남는다. 화면은 정상 동작하고 서버로만
// 안 나간다. 앱에 INITIAL_PETS 등 초기 샘플이 있어 빈 화면을 보지 않는다.
(function () {
    'use strict';

    var GUEST_KEY = 'petna_guest_mode';
    var DEMO_EMAIL = 'butler@petna.co.kr';

    function isGuest() {
        try { return localStorage.getItem(GUEST_KEY) === '1'; } catch (e) { return false; }
    }

    function enterGuestMode() {
        try {
            localStorage.setItem(GUEST_KEY, '1');
            // 원격 동기화(읽기·쓰기)를 함께 멈춘다. 이 플래그가 없으면 새로고침 직후
            // sync*가 로컬을 DB 내용으로 덮어써 게스트가 만든 것이 사라진다(2026-07-25).
            localStorage.setItem('petna_demo_mode', '1');
            localStorage.setItem('petna_is_logged_in', 'true');
            localStorage.setItem('petna_user_email', DEMO_EMAIL);
            localStorage.setItem('petna_active_tab', 'mypet');
        } catch (e) {
            if (typeof showToast === 'function') showToast('브라우저 저장소를 쓸 수 없어 둘러보기를 시작하지 못했습니다.');
            return;
        }
        seedSamplePets();
        window.location.reload();
    }

    // 둘러보기인데 펫이 하나도 없으면 "첫 반려동물 등록하기" 빈 화면만 보인다 —
    // 등록을 해야 볼 수 있으면 '가입 없이 둘러보기'라는 약속이 반쯤 깨진다.
    // (이 파일 상단 주석은 원래 "INITIAL_PETS 샘플이 있어 빈 화면을 보지 않는다"고
    //  적혀 있었지만 실제로 심는 코드가 없었다 — 실측으로 빈 화면 확인, 2026-08-12.)
    //
    // 이미 쓰던 기록이 있으면 절대 덮지 않는다. 비어 있을 때만 샘플을 넣는다.
    function seedSamplePets() {
        try {
            if (typeof AppStore === 'undefined' || typeof INITIAL_PETS === 'undefined') return;
            AppStore.load(DEMO_EMAIL);
            if ((AppStore.getState('pets') || []).length) return;
            AppStore.setState('pets', INITIAL_PETS.map(function (p) {
                return JSON.parse(JSON.stringify(p));
            }));
            AppStore.save();
        } catch (e) { /* 샘플이 없어도 앱은 열린다 — 빈 화면일 뿐 실패는 아니다 */ }
    }

    // 게스트 종료 = 데모 흔적을 걷고 로그인 화면으로.
    //
    // 데모 계정(butler@petna.co.kr) 키를 지우는 이유는 "게스트가 입력한 내용"을 없애기
    // 위해서다. 이 키들 자체는 게스트 모드와 무관하게 앱이 로그인 화면에서도 기본 상태로
    // 만들어 두므로(실측: 진입 전에도 16개 존재), 지운 뒤 새로고침하면 앱이 **기본 상태로**
    // 다시 만든다 — 즉 결과는 "게스트가 쓴 것만 사라진다"가 맞다.
    // 오너의 실제 데이터는 자기 이메일 키(petna_*_<이메일>)에 따로 있어 영향이 없다.
    function exitGuestMode() {
        try {
            [GUEST_KEY, 'petna_demo_mode', 'petna_is_logged_in', 'petna_user_email',
             'petna_active_tab'].forEach(function (k) { localStorage.removeItem(k); });
            Object.keys(localStorage)
                .filter(function (k) { return k.indexOf(DEMO_EMAIL) !== -1; })
                .forEach(function (k) { localStorage.removeItem(k); });
        } catch (e) { /* 저장소 접근 실패는 무해 — 새로고침이 어차피 로그인 화면을 띄운다 */ }
        window.location.reload();
    }

    // 상시 배너 — 게스트가 "내 기록이 저장되고 있다"고 오해하지 않게 한다.
    // 정직성이 전환보다 우선이다: 저장되지 않는다는 사실을 숨기면 나중에 데이터를
    // 잃었다고 느낀다.
    function renderGuestBanner() {
        var existing = document.getElementById('guest-mode-banner');
        if (!isGuest()) { if (existing) existing.remove(); return; }
        if (existing) return;

        // 문서 흐름의 **맨 위**에 둔다(fixed 아님). 하단에 fixed로 깔았더니 모바일
        // 하단 네비(position:fixed, h=64)를 15px 가려 탭 하단이 안 눌렸다 — 겹침을
        // margin으로 보정하려면 네비가 언제 생기는지에 의존하게 되고, 그건 깨지기 쉽다.
        // 흐름에 넣으면 sticky 헤더가 그 아래로 자연히 밀려 계산이 아예 필요 없다.
        var bar = document.createElement('div');
        bar.id = 'guest-mode-banner';
        // sticky: 스크롤해도 계속 보여야 한다. "기록이 저장되지 않는다"는 고지를
        // 사용자가 입력하는 순간에 못 보면 나중에 데이터를 잃었다고 느낀다.
        bar.className = 'sticky top-0 z-50 bg-brand-700 text-white px-4 py-2.5 ' +
                        'flex items-center justify-center gap-3 text-[11px] font-bold';
        bar.innerHTML =
            '<span class="[word-break:keep-all] text-center">👀 둘러보기 중이에요 — 기록은 이 기기에만 저장돼요.</span>' +
            '<button onclick="exitGuestMode()" class="shrink-0 min-h-[32px] px-3 rounded-full ' +
            'bg-white text-brand-700 font-extrabold hover:bg-brand-50 transition">가입하고 시작</button>';
        document.body.insertBefore(bar, document.body.firstChild);

        // 헤더도 sticky top:0이라 그대로 두면 배너와 같은 자리를 다퉈 서로를 덮는다.
        // 게스트일 때만 헤더 시작점을 배너 높이만큼 내린다(해제 시 원복은 새로고침).
        //
        // 높이는 **다음 프레임에** 읽는다 — 삽입 직후 offsetHeight를 읽었더니 49px짜리
        // 배너가 34px로 잡혀 헤더 상단 15px이 가렸다(폰트·버튼 min-height 반영 전 값).
        // 폭이 바뀌면 문구가 줄바꿈되며 높이도 변하므로 resize에도 다시 맞춘다.
        function syncHeaderOffset() {
            var h = bar.getBoundingClientRect().height;
            ['header', '#mobile-header'].forEach(function (sel) {
                var el = document.querySelector(sel);
                if (el && getComputedStyle(el).position === 'sticky') el.style.top = h + 'px';
            });
        }
        // rAF만 쓰면 **탭이 백그라운드일 때 영영 실행되지 않는다**(실측으로 확인 —
        // 브라우저 창이 가려진 상태에서 헤더 오프셋이 끝까지 적용되지 않았다).
        // setTimeout은 백그라운드에서도(스로틀될 뿐) 돈다. 즉시 1회 + 지연 1회로,
        // 레이아웃이 확정되기 전 값이라도 일단 맞춰두고 곧 교정한다.
        syncHeaderOffset();
        setTimeout(syncHeaderOffset, 0);
        window.addEventListener('load', syncHeaderOffset);
        window.addEventListener('resize', syncHeaderOffset);
    }

    // 게스트가 쓰기 CTA(기록·소셜 등)를 눌렀을 때의 능동적 로그인 유도.
    // 데이터 계층 가드(supabase.js isReadOnlySession)가 원격 쓰기를 이미 막아 RLS 403은
    // 절대 노출되지 않는다 — 여기서는 "왜 저장이 안 됐는지"를 알리고 가입으로 유도만 한다.
    // 세션당 1회만(디바운스): 쓰기 시도마다 토스트가 쏟아지면 도리어 방해가 된다.
    var _signupPromptShown = false;
    function promptGuestSignup() {
        if (!isGuest() || _signupPromptShown) return;
        _signupPromptShown = true;
        if (typeof showToast === 'function') {
            showToast('👀 둘러보기 중이라 이 기기에만 저장돼요 — 가입하면 기록이 남아요.');
        }
    }

    window.enterGuestMode = enterGuestMode;
    window.exitGuestMode = exitGuestMode;
    window.isGuestMode = isGuest;
    window.renderGuestBanner = renderGuestBanner;
    window.promptGuestSignup = promptGuestSignup;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', renderGuestBanner);
    } else {
        renderGuestBanner();
    }
})();
