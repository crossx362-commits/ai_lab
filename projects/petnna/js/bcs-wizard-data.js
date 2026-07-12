// 🐾 BCS(체형점수) 데이터/판정 로직 — 백로그 나무_20260712143815(나무 제안, P3)
// ─────────────────────────────────────────────────────────────
// 5문항 응답 저장과 WSAVA 5점 척도 평균 기반 판정(저체중/정상/과체중)만 담당한다.
// 화면 렌더링은 js/bcs-wizard.js(BcsWizard, 이 모듈을 사용)에서 처리한다.
// 프론트 전용 — localStorage에만 저장(신규 DB 테이블 없음, QolCheckin과 동일 원칙).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_bcs_records"; // { [petId]: [{date, answers:{...}, score, category}] }

    // 각 문항: 1(마름)~5(과체중) 방향. 3이 이상적 체형.
    var QUESTIONS = [
        {
            key: "ribs", emoji: "🖐️", label: "갈비뼈 촉감",
            desc: "손으로 옆구리를 만졌을 때 갈비뼈가 어떻게 느껴지나요?",
            options: [
                { v: 1, label: "뼈가 툭 튀어나와 눈에 보임" },
                { v: 2, label: "지방 없이 쉽게 만져짐" },
                { v: 3, label: "얇은 지방 아래로 쉽게 만져짐" },
                { v: 4, label: "누르면 겨우 만져짐" },
                { v: 5, label: "두꺼운 지방으로 만지기 어려움" },
            ],
        },
        {
            key: "waist", emoji: "👀", label: "위에서 본 허리",
            desc: "위에서 내려다볼 때 갈비뼈 뒤 허리 라인은?",
            options: [
                { v: 1, label: "허리가 극단적으로 잘록함" },
                { v: 2, label: "허리가 뚜렷이 잘록함" },
                { v: 3, label: "모래시계처럼 자연스러운 허리" },
                { v: 4, label: "허리 구분이 거의 없음" },
                { v: 5, label: "허리가 오히려 밖으로 불룩함" },
            ],
        },
        {
            key: "tuck", emoji: "📐", label: "옆에서 본 복부",
            desc: "옆에서 볼 때 배(복부) 라인은?",
            options: [
                { v: 1, label: "배가 극단적으로 말려 올라감" },
                { v: 2, label: "배가 뚜렷이 올라감" },
                { v: 3, label: "배가 살짝 올라감(정상 턱업)" },
                { v: 4, label: "배가 거의 수평임" },
                { v: 5, label: "배가 아래로 처지고 나옴" },
            ],
        },
        {
            key: "bones", emoji: "🦴", label: "척추·골반뼈",
            desc: "등뼈와 골반뼈가 만져지거나 보이나요?",
            options: [
                { v: 1, label: "뼈가 도드라지게 보임" },
                { v: 2, label: "쉽게 만져지고 조금 보임" },
                { v: 3, label: "약간의 지방 아래 만져짐" },
                { v: 4, label: "눌러야 겨우 만져짐" },
                { v: 5, label: "지방에 덮여 만지기 어려움" },
            ],
        },
        {
            key: "fat", emoji: "🫧", label: "등·허리 지방",
            desc: "등과 허리 위를 쓸어보면 지방층이 느껴지나요?",
            options: [
                { v: 1, label: "지방이 전혀 없음" },
                { v: 2, label: "지방이 거의 없음" },
                { v: 3, label: "얇고 매끄러운 지방층" },
                { v: 4, label: "도톰한 지방층" },
                { v: 5, label: "두껍고 물렁한 지방층" },
            ],
        },
    ];

    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || {}; } catch (e) { return {}; } }
    function saveAll(map) { localStorage.setItem(LS_KEY, JSON.stringify(map)); }
    function history(petId) { return loadAll()[String(petId)] || []; }

    function record(petId, answers, score, category) {
        var map = loadAll();
        var key = String(petId);
        if (!map[key]) map[key] = [];
        map[key].push({ date: new Date().toISOString(), answers: answers, score: score, category: category });
        saveAll(map);
    }

    // 평균(1~5)을 저체중/정상/과체중으로 판정
    function classify(score) {
        if (score < 2.5) return { key: "under", label: "저체중", emoji: "🥺", color: "amber" };
        if (score > 3.5) return { key: "over", label: "과체중", emoji: "🍔", color: "red" };
        return { key: "ideal", label: "정상", emoji: "💚", color: "emerald" };
    }

    window.BcsWizardData = {
        QUESTIONS: QUESTIONS,
        history: history,
        record: record,
        classify: classify,
    };
})();
