// 💡 AI 맞춤 데일리 케어 팁 카드 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// 반려동물 프로필(종·견종·나이)을 반영해 '오늘의 맞춤 케어 팁'을 노출한다.
// generic → tailored 트렌드 대응: 같은 종이라도 프로필에 맞는 팁만 후보에 담고,
// 날짜+petId 시드로 하루 한 개를 결정론적으로 고른다(하루 안엔 고정, 날마다 바뀜).
// 프론트 전용 룰 기반 — 신규 저장/DB/라이브러리 없음, 읽기만 함.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function activePet() {
        if (typeof window.getActivePet === "function") return window.getActivePet();
        return (typeof pets !== "undefined" && pets && pets[0]) || null;
    }

    // 나이 문자열("2살 (청소년기)", "3살 (성묘)" 등)에서 생애단계 추정
    function lifeStage(pet) {
        var s = String((pet && pet.age) || "");
        if (/노령|시니어|노견|노묘/.test(s)) return "senior";
        if (/아기|퍼피|자묘|유년|이유/.test(s)) return "baby";
        if (/청소년/.test(s)) return "young";
        if (/성견|성묘|성체|어덜트/.test(s)) return "adult";
        var m = s.match(/(\d+)\s*살/);
        var yr = m ? parseInt(m[1], 10) : NaN;
        if (!isNaN(yr)) {
            if (yr < 1) return "baby";
            if (yr < 2) return "young";
            if (yr >= 7) return "senior";
            return "adult";
        }
        return "adult";
    }

    // 종별 기본 팁
    var SPECIES = {
        dog: [
            { emoji: "🦷", text: "이빨은 사람 손가락 칫솔로 하루 한 번, 잇몸선 위주로 닦아주면 치석을 크게 줄일 수 있어요." },
            { emoji: "🐾", text: "산책 후 발바닥 젤리 사이를 물티슈로 닦아 이물질·진드기를 확인해 주세요." },
            { emoji: "🧠", text: "노즈워크나 간식 숨기기로 코를 쓰게 하면 짧은 시간에도 스트레스가 풀려요." }
        ],
        cat: [
            { emoji: "💧", text: "고양이는 갈증을 잘 못 느껴요. 물그릇을 여러 곳에 두거나 흐르는 물을 주면 음수량이 늘어요." },
            { emoji: "🚽", text: "화장실은 '마릿수+1'개가 이상적이에요. 모래는 하루 한 번 이상 치워 배뇨 이상도 체크하세요." },
            { emoji: "🪮", text: "빗질로 빠진 털을 미리 제거하면 그루밍 때 삼키는 헤어볼을 줄일 수 있어요." }
        ],
        rabbit: [
            { emoji: "🌾", text: "건초는 24시간 무제한으로. 치아·장 건강의 핵심이라 절대 떨어지지 않게 해주세요." },
            { emoji: "🦷", text: "토끼 이빨은 평생 자라요. 딱딱한 건초·나무 장난감으로 부정교합을 예방하세요." }
        ],
        hamster: [
            { emoji: "🎡", text: "쳇바퀴는 등이 굽지 않는 큰 지름(20cm+)으로. 매일 밤 충분히 달릴 수 있게 해주세요." },
            { emoji: "🌡️", text: "햄스터는 더위·추위에 약해요. 20~24℃를 유지하고 직사광선·에어컨 바람을 피하세요." }
        ]
    };

    // 생애단계별 팁(종 공통)
    var STAGE = {
        baby: [{ emoji: "🍼", text: "성장기엔 하루 급식을 3~4회로 나눠 소량씩. 저혈당·소화불량을 막아줘요." }],
        young: [{ emoji: "🎾", text: "에너지가 넘치는 시기예요. 매일 충분히 놀아주면 문제 행동을 크게 줄일 수 있어요." }],
        adult: [{ emoji: "⚖️", text: "성체는 체중 관리가 핵심. 간식은 하루 칼로리의 10% 이내로 유지해 주세요." }],
        senior: [{ emoji: "🩺", text: "노령기엔 6개월마다 건강검진을. 관절·신장·치아 변화를 조기에 잡을 수 있어요." }]
    };

    // 견종/묘종 특성 키워드 → 맞춤 팁(공백 제거 후 부분일치)
    var TRAITS = [
        { keys: ["시츄", "불독", "퍼그", "페키니즈", "페르시안", "히말라얀"], tip: { emoji: "😮‍💨", text: "코가 짧은 단두종이에요. 더운 날 격한 운동은 피하고 호흡이 거칠어지면 즉시 쉬게 하세요." } },
        { keys: ["골든리트리버", "리트리버", "래브라도", "코카", "저먼셰퍼드"], tip: { emoji: "🦴", text: "대형·활동견은 고관절 부담이 커요. 미끄러운 바닥엔 매트를 깔아 관절을 보호해 주세요." } },
        { keys: ["푸들", "말티즈", "비숑", "요크셔", "포메라니안", "페르시안", "샴", "앙고라"], tip: { emoji: "✂️", text: "털이 잘 엉키는 아이예요. 이틀에 한 번 빗질하면 피부 트러블과 뭉침을 예방할 수 있어요." } },
        { keys: ["닥스훈트", "코기", "웰시코기"], tip: { emoji: "🐕", text: "허리가 긴 체형은 디스크에 취약해요. 소파·계단 점프를 줄이고 안아 올릴 땐 허리를 받쳐주세요." } }
    ];

    // 프로필 → 오늘 후보 팁 배열
    function buildTips(pet) {
        if (!pet) return [];
        var out = [];
        var type = pet.type || "dog";
        (SPECIES[type] || SPECIES.dog).forEach(function (t) { out.push(t); });
        (STAGE[lifeStage(pet)] || []).forEach(function (t) { out.push(t); });
        var breed = String(pet.breed || "").replace(/\s/g, "");
        TRAITS.forEach(function (tr) {
            if (tr.keys.some(function (k) { return breed.indexOf(k) !== -1; })) out.push(tr.tip);
        });
        return out;
    }

    // 날짜+petId 결정론적 인덱스(하루 고정)
    function daySeed(petId) {
        var d = new Date();
        var base = d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate();
        return base + (parseInt(petId, 10) || 0);
    }

    // '다른 팁 보기' 오프셋(세션 내 순환, 저장 안 함)
    var _offset = {};

    function pickTip(pet) {
        var tips = buildTips(pet);
        if (tips.length === 0) return null;
        var idx = (daySeed(pet.id) + (_offset[pet.id] || 0)) % tips.length;
        return { tip: tips[idx], total: tips.length };
    }

    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }
        var picked = pickTip(pet);
        if (!picked) { el.innerHTML = ""; return; }

        var profile = [pet.breed, pet.age].filter(Boolean).map(esc).join(" · ");
        var more = picked.total > 1
            ? '<button type="button" onclick="DailyCareTip.next()" class="shrink-0 text-[11px] font-bold text-brand-600 bg-brand-50 hover:bg-brand-100 rounded-full px-2.5 py-1 transition-colors"><i class="fa-solid fa-rotate mr-1"></i>다른 팁</button>'
            : "";

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center gap-2 mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2 flex-1"><span class="text-xl">💡</span>' + esc(pet.name || "우리 아이") + ' 맞춤 케어 팁</h3>' +
            more +
            "</div>" +
            (profile ? '<p class="text-[11px] text-gray-400 mb-2">' + profile + "</p>" : "") +
            '<div class="flex items-start gap-3 rounded-xl bg-brand-50/60 px-3.5 py-3">' +
            '<span class="text-2xl shrink-0 leading-none">' + picked.tip.emoji + "</span>" +
            '<p class="text-sm text-gray-700 leading-relaxed">' + esc(picked.tip.text) + "</p>" +
            "</div>" +
            '<p class="mt-2 text-[10px] text-gray-400 leading-relaxed">※ 프로필 기반 참고용 팁입니다. 건강 이상이 있으면 수의사 상담을 우선하세요.</p>' +
            "</div>";
    }

    function next() {
        var pet = activePet();
        if (!pet) return;
        _offset[pet.id] = (_offset[pet.id] || 0) + 1;
        renderWidget("daily-care-tip-widget");
    }

    window.DailyCareTip = { renderWidget: renderWidget, next: next, _buildTips: buildTips, _lifeStage: lifeStage };
})();
