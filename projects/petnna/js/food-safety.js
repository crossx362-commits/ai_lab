// 🍽️ 급여 가능 음식 검색기 "이거 먹어도 돼?" — 인간 음식 안전/주의/금지 정적 테이블 (나무 백로그 P2)
// ─────────────────────────────────────────────────────────────
// 반려인 최다 검색 질의("○○ 먹여도 되나요?")에 즉답하는 결정론적 정적 표.
// 개·고양이를 구분해 안전(safe)·주의(caution)·금지(danger) 등급과 한 줄 설명을 준다.
// 금지 항목은 기존 응급 카드(EmergencyCard)로 바로 연결한다.
//
// 가드레일:
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. 모든 데이터는 정적 상수(LLM 비결정성 배제).
//  · 수의학 일반 권고(ASPCA 등) 기반 참고용 — 개체 차·양·조리법에 따라 다르므로 진료 대체 아님.
//  · 응급 연결은 기존 EmergencyCard.open() 재사용(신규 모듈 없음).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    // 등급: "safe" 안전 · "caution" 주의(소량·조건부) · "danger" 금지(독성·위험)
    // dog/cat 각각 등급을 가진다. note는 공통 한 줄 설명.
    var FOODS = [
        { name: "초콜릿", aka: "카카오 코코아", emoji: "🍫", dog: "danger", cat: "danger", note: "테오브로민·카페인 중독 — 소량도 위험, 다크·베이킹일수록 독성 강함." },
        { name: "포도", aka: "건포도 레이즌", emoji: "🍇", dog: "danger", cat: "danger", note: "급성 신부전 유발 — 소량으로도 위험, 절대 금지." },
        { name: "양파", aka: "대파 쪽파", emoji: "🧅", dog: "danger", cat: "danger", note: "적혈구 파괴(빈혈) — 익혀도 위험, 고양이가 더 민감." },
        { name: "마늘", aka: "", emoji: "🧄", dog: "danger", cat: "danger", note: "양파와 같은 계열 독성 — 소량 반복도 누적 위험." },
        { name: "자일리톨", aka: "무설탕 껌 사탕", emoji: "🦷", dog: "danger", cat: "caution", note: "개는 저혈당·간부전으로 치명적 — 무설탕 제품 라벨 확인 필수." },
        { name: "마카다미아", aka: "", emoji: "🌰", dog: "danger", cat: "caution", note: "개에게 무기력·구토·발열·후지 마비 유발." },
        { name: "알코올", aka: "술 맥주", emoji: "🍺", dog: "danger", cat: "danger", note: "소량도 중독 — 구토·저체온·호흡 억제." },
        { name: "카페인", aka: "커피 에너지음료", emoji: "☕", dog: "danger", cat: "danger", note: "심장·신경 자극 — 커피·차·에너지음료 모두 금지." },
        { name: "아보카도", aka: "", emoji: "🥑", dog: "caution", cat: "caution", note: "페르신 성분·큰 씨 질식·고지방 — 껍질/씨는 금지, 과육도 소량만." },
        { name: "익힌 뼈", aka: "닭뼈 갈비뼈", emoji: "🦴", dog: "danger", cat: "danger", note: "가열 뼈는 날카롭게 부서져 소화관 천공·질식 위험." },
        { name: "생지방·비계", aka: "삼겹살 기름", emoji: "🥓", dog: "caution", cat: "caution", note: "췌장염 유발 가능 — 기름진 부위는 피한다." },
        { name: "우유", aka: "", emoji: "🥛", dog: "caution", cat: "caution", note: "유당불내성으로 설사 흔함 — 반려동물용 락토프리만." },
        { name: "치즈", aka: "", emoji: "🧀", dog: "caution", cat: "caution", note: "짜고 고지방 — 아주 소량 간식 정도만, 유당 주의." },
        { name: "빵·밀가루", aka: "생반죽", emoji: "🍞", dog: "caution", cat: "caution", note: "익힌 소량은 무해하나 생이스트 반죽은 팽창·알코올로 위험." },
        { name: "견과류", aka: "아몬드 호두", emoji: "🥜", dog: "caution", cat: "caution", note: "고지방·질식 위험 — 마카다미아·호두는 특히 피한다." },
        { name: "소금·짠 음식", aka: "", emoji: "🧂", dog: "danger", cat: "danger", note: "과다 시 나트륨 중독 — 가공육·과자 등 짠 음식 금지." },
        { name: "생계란 흰자", aka: "", emoji: "🥚", dog: "caution", cat: "caution", note: "아비딘이 비오틴 흡수 방해·살모넬라 — 완전히 익혀서." },
        { name: "감자(생·싹)", aka: "", emoji: "🥔", dog: "caution", cat: "caution", note: "생감자·싹·녹색 부위 솔라닌 독성 — 익힌 흰 살은 소량 가능." },
        { name: "토마토(덜 익은)", aka: "줄기 잎", emoji: "🍅", dog: "caution", cat: "caution", note: "덜 익은 것·줄기·잎은 독성 — 잘 익은 과육 소량만." },
        { name: "버섯(야생)", aka: "", emoji: "🍄", dog: "danger", cat: "danger", note: "야생 버섯은 종 구분 어려워 위험 — 식용 조리버섯 외 금지." },

        { name: "닭가슴살", aka: "삶은 닭", emoji: "🍗", dog: "safe", cat: "safe", note: "기름·양념 없이 익혀서 — 훌륭한 저지방 단백질." },
        { name: "소고기(살코기)", aka: "", emoji: "🥩", dog: "safe", cat: "safe", note: "익힌 살코기 무염 — 좋은 단백질원." },
        { name: "연어", aka: "", emoji: "🐟", dog: "safe", cat: "safe", note: "완전히 익혀서 — 생연어는 기생충·세균 위험." },
        { name: "달걀(익힌)", aka: "삶은 계란", emoji: "🍳", dog: "safe", cat: "safe", note: "완숙으로 급여 — 소화 잘 되는 단백질." },
        { name: "고구마", aka: "", emoji: "🍠", dog: "safe", cat: "caution", note: "삶거나 찐 것 소량 — 식이섬유 풍부, 개에게 인기." },
        { name: "당근", aka: "", emoji: "🥕", dog: "safe", cat: "safe", note: "생/익힌 것 모두 OK — 저칼로리 치아 간식." },
        { name: "호박", aka: "단호박", emoji: "🎃", dog: "safe", cat: "safe", note: "익힌 무염 호박 — 소화·변 조절에 좋음." },
        { name: "사과", aka: "", emoji: "🍎", dog: "safe", cat: "caution", note: "씨·심 제거 후 과육만 — 씨앗엔 미량 시안화물." },
        { name: "바나나", aka: "", emoji: "🍌", dog: "safe", cat: "caution", note: "소량 간식 — 당분 많아 조금만." },
        { name: "블루베리", aka: "", emoji: "🫐", dog: "safe", cat: "safe", note: "항산화 간식 — 소량으로." },
        { name: "수박", aka: "", emoji: "🍉", dog: "safe", cat: "caution", note: "씨·껍질 제거 후 과육만 — 수분 보충 간식." },
        { name: "오이", aka: "", emoji: "🥒", dog: "safe", cat: "safe", note: "저칼로리 아삭 간식 — 얇게 썰어서." },
        { name: "브로콜리", aka: "", emoji: "🥦", dog: "safe", cat: "caution", note: "익힌 소량 OK — 많이 주면 가스·소화불량." },
        { name: "완두콩", aka: "", emoji: "🫛", dog: "safe", cat: "safe", note: "익힌 무염 소량 — 단백질·식이섬유." },
        { name: "쌀밥", aka: "흰쌀", emoji: "🍚", dog: "safe", cat: "safe", note: "무염 흰밥 — 배탈 시 소화에 좋음." },
        { name: "플레인 요거트", aka: "무가당", emoji: "🥣", dog: "safe", cat: "caution", note: "무가당·무자일리톨만 소량 — 유당 민감하면 주의." },
        { name: "땅콩버터", aka: "", emoji: "🥜", dog: "safe", cat: "caution", note: "무가당·무자일리톨 라벨 확인 필수 — 소량만." },
        { name: "귀리", aka: "오트밀", emoji: "🌾", dog: "safe", cat: "caution", note: "익힌 무설탕 오트밀 소량 — 식이섬유." },
        { name: "딸기", aka: "", emoji: "🍓", dog: "safe", cat: "caution", note: "소량 간식 — 당분 있어 조금만." },
        { name: "배", aka: "", emoji: "🍐", dog: "safe", cat: "caution", note: "씨 제거 후 과육 소량 — 씨앗 주의." },

        { name: "무염 육수", aka: "", emoji: "🍲", dog: "safe", cat: "safe", note: "양파·마늘·소금 없는 육수만 — 수분·식욕 촉진." },
        { name: "코티지치즈", aka: "", emoji: "🧀", dog: "safe", cat: "caution", note: "저지방·저염 소량 — 단백질 간식." },
        { name: "칠면조(살코기)", aka: "", emoji: "🦃", dog: "safe", cat: "safe", note: "익힌 무양념 살코기 — 껍질·뼈 제거." },
        { name: "돼지고기(살코기)", aka: "", emoji: "🐖", dog: "safe", cat: "caution", note: "완전히 익힌 무염 살코기 — 생/가공(햄·베이컨)은 금지." },
        { name: "새우", aka: "", emoji: "🦐", dog: "safe", cat: "safe", note: "완전히 익혀 껍질·꼬리 제거 — 소량 간식." },
        { name: "참치(사람용)", aka: "", emoji: "🐟", dog: "caution", cat: "caution", note: "가끔 소량만 — 수은·염분, 주식 대용 금지." },
        { name: "셀러리", aka: "", emoji: "🥬", dog: "safe", cat: "caution", note: "잘게 썬 소량 — 저칼로리." },
        { name: "그린빈", aka: "깍지콩", emoji: "🫛", dog: "safe", cat: "safe", note: "익힌 무염 — 저칼로리 다이어트 간식." },
        { name: "파인애플", aka: "", emoji: "🍍", dog: "safe", cat: "caution", note: "신선한 과육 소량 — 통조림(시럽) 금지." },
        { name: "망고", aka: "", emoji: "🥭", dog: "safe", cat: "caution", note: "씨 제거 후 과육 소량 — 당분 주의." },
        { name: "멜론", aka: "캔털루프", emoji: "🍈", dog: "safe", cat: "caution", note: "씨·껍질 제거 후 소량 — 수분 간식." },
        { name: "라즈베리", aka: "", emoji: "🍇", dog: "caution", cat: "caution", note: "미량 자일리톨 함유 — 아주 소량만." },
        { name: "코코넛", aka: "", emoji: "🥥", dog: "caution", cat: "caution", note: "과육 소량은 무해 — 고지방, 물/오일 과다 주의." },
        { name: "옥수수", aka: "", emoji: "🌽", dog: "safe", cat: "caution", note: "알갱이만 소량 — 통옥수수 심은 질식·폐색 위험." },
        { name: "시금치", aka: "", emoji: "🥬", dog: "caution", cat: "caution", note: "익힌 소량만 — 옥살산으로 신장 부담, 많이 금지." },
        { name: "감귤·오렌지", aka: "레몬 라임", emoji: "🍊", dog: "caution", cat: "danger", note: "신맛·정유 성분 위장 자극 — 고양이는 특히 피한다." },
        { name: "체리", aka: "", emoji: "🍒", dog: "caution", cat: "caution", note: "과육 소량 외 씨·줄기·잎은 시안화물로 금지." },
        { name: "복숭아·자두", aka: "살구", emoji: "🍑", dog: "caution", cat: "caution", note: "과육 소량만 — 씨앗은 시안화물·질식으로 금지." },
        { name: "아스파라거스", aka: "", emoji: "🌿", dog: "safe", cat: "caution", note: "익힌 소량 — 생것은 질겨 소화 어려움." },
        { name: "두부", aka: "콩", emoji: "🍢", dog: "caution", cat: "caution", note: "무양념 소량은 대체로 무해 — 가스·알레르기 개체차." },
        { name: "꿀", aka: "", emoji: "🍯", dog: "caution", cat: "caution", note: "아주 소량만 — 당분 많아 비만·충치 유발." },
        { name: "김·미역", aka: "해조류", emoji: "🌊", dog: "caution", cat: "caution", note: "무염 소량만 — 조미김(소금·기름)은 금지." },
        { name: "베이컨·햄", aka: "소시지", emoji: "🥓", dog: "danger", cat: "danger", note: "고염·고지방 가공육 — 췌장염·나트륨 중독 위험." },
        { name: "아이스크림", aka: "", emoji: "🍦", dog: "caution", cat: "caution", note: "유당·설탕·자일리톨 위험 — 반려동물용 간식으로 대체." },
        { name: "생선뼈", aka: "", emoji: "🐠", dog: "danger", cat: "danger", note: "가는 가시가 목·소화관을 찔러 위험 — 발라내고 급여." },
        { name: "생고기·생선", aka: "회", emoji: "🍣", dog: "caution", cat: "caution", note: "세균·기생충 위험 — 익혀서 급여 권장." },
        { name: "라면·짠 국물", aka: "", emoji: "🍜", dog: "danger", cat: "danger", note: "고나트륨·양념(양파·마늘) — 절대 금지." },
        { name: "껌·사탕", aka: "", emoji: "🍬", dog: "danger", cat: "danger", note: "자일리톨·질식 위험 — 무설탕도 위험." }
    ];

    var LEVELS = {
        safe: { label: "안전", cls: "text-emerald-700 bg-emerald-50 border-emerald-100", dot: "🟢" },
        caution: { label: "주의", cls: "text-amber-700 bg-amber-50 border-amber-100", dot: "🟡" },
        danger: { label: "금지", cls: "text-rose-700 bg-rose-50 border-rose-100", dot: "🔴" }
    };

    var state = { species: "dog", query: "" };

    function badge(level) {
        var L = LEVELS[level] || LEVELS.caution;
        return '<span class="text-[11px] font-bold px-2 py-0.5 rounded-full border ' + L.cls + '">' + L.dot + " " + L.label + "</span>";
    }

    function matches(f, q) {
        if (!q) return true;
        var hay = (f.name + " " + (f.aka || "")).toLowerCase();
        return hay.indexOf(q) !== -1;
    }

    function rowHtml(f) {
        var level = state.species === "cat" ? f.cat : f.dog;
        var danger = level === "danger";
        var link = danger
            ? '<button onclick="FoodSafety.emergency()" class="mt-1.5 text-[11px] font-bold text-rose-600 underline">🚨 먹었어요 — 응급 카드 열기</button>'
            : "";
        return '<div class="rounded-xl border border-gray-100 px-3 py-2.5">' +
            '<div class="flex items-center justify-between gap-2">' +
            '<div class="flex items-center gap-2 min-w-0"><span class="text-lg shrink-0">' + f.emoji + "</span>" +
            '<span class="text-sm font-bold text-gray-900 truncate">' + esc(f.name) + "</span></div>" +
            badge(level) + "</div>" +
            '<p class="text-[11px] text-gray-600 leading-relaxed mt-1">' + esc(f.note) + "</p>" +
            link + "</div>";
    }

    function listHtml() {
        var q = state.query.trim().toLowerCase();
        var rows = FOODS.filter(function (f) { return matches(f, q); });
        if (!rows.length) {
            return '<p class="text-center text-xs text-gray-400 py-8">"' + esc(state.query) + '" 검색 결과가 없어요.<br>다른 이름으로 찾아보거나, 확실치 않으면 수의사에게 문의하세요.</p>';
        }
        return rows.map(rowHtml).join("");
    }

    function renderBody() {
        var el = document.getElementById("fs-list");
        if (el) el.innerHTML = listHtml();
    }

    function setSpecies(sp) {
        state.species = sp === "cat" ? "cat" : "dog";
        var el = document.getElementById("fs-overlay");
        if (el) {
            ["dog", "cat"].forEach(function (s) {
                var btn = document.getElementById("fs-sp-" + s);
                if (!btn) return;
                if (s === state.species) btn.className = spBtnCls(true);
                else btn.className = spBtnCls(false);
            });
        }
        renderBody();
    }

    function spBtnCls(active) {
        return "flex-1 py-2 text-sm font-bold rounded-lg transition-colors " +
            (active ? "bg-brand-500 text-white" : "bg-gray-100 text-gray-500 hover:bg-gray-200");
    }

    function onSearch(v) { state.query = String(v || ""); renderBody(); }

    function open() {
        close();
        state.query = "";
        var overlay = document.createElement("div");
        overlay.id = "fs-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🍽️ 이거 먹어도 돼?</h3>' +
            '<button onclick="FoodSafety.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 pt-4 space-y-3">' +
            '<div class="flex gap-2">' +
            '<button id="fs-sp-dog" onclick="FoodSafety.setSpecies(\'dog\')" class="' + spBtnCls(true) + '">🐶 강아지</button>' +
            '<button id="fs-sp-cat" onclick="FoodSafety.setSpecies(\'cat\')" class="' + spBtnCls(false) + '">🐱 고양이</button></div>' +
            '<input id="fs-search" type="search" inputmode="search" placeholder="음식 이름 검색 (예: 초콜릿, 사과)" ' +
            'oninput="FoodSafety.onSearch(this.value)" ' +
            'class="w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm focus:outline-none focus:border-brand-400" />' +
            '<div class="flex items-center gap-3 text-[11px] text-gray-500">' +
            "<span>🟢 안전</span><span>🟡 주의(소량·조건부)</span><span>🔴 금지</span></div>" +
            "</div>" +
            '<div id="fs-list" class="px-5 py-3 space-y-1.5 overflow-y-auto flex-1">' + listHtml() + "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100">' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 참고용 일반 정보이며 수의사 진료를 대체하지 않아요. 같은 음식도 양·조리법·개체 상태에 따라 위험할 수 있고, 목록에 없거나 확실치 않으면 급여 전 병원에 문의하세요.</p>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function close() { var el = document.getElementById("fs-overlay"); if (el) el.remove(); }

    function emergency() {
        close();
        if (window.EmergencyCard && typeof window.EmergencyCard.open === "function") window.EmergencyCard.open();
        else if (typeof showToast === "function") showToast("응급 카드를 열 수 없어요");
    }

    window.FoodSafety = { open: open, close: close, setSpecies: setSpecies, onSearch: onSearch, emergency: emergency };
})();
