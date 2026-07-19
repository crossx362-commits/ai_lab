// 🚨 응급 카드 — 상황별 1차 처치 가이드 + 가까운 24h 동물병원 연결 (나무 백로그 P3)
// ─────────────────────────────────────────────────────────────
// 위급 상황(중독·이물질/질식·출혈·경련·열사병·화상)에서 병원 도착 전까지의
// 정적·결정론적 1차 처치 가이드를 제공하고, 기존 지도 데이터(PETLIFE_REAL_LOCATIONS)의
// 24h/야간 동물병원으로 즉시 전화·지도 연결한다.
//
// 가드레일:
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. 모든 가이드는 정적 상수(LLM 비결정성 배제).
//  · 의료 면책 필수 + "먼저 병원에 전화" 원칙 — 1차 처치는 진료를 대체하지 않는다.
//  · 지도 연결은 기존 switchTab('shop') 지도 탭 재사용(신규 지도 모듈 없음).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    // 상황별 1차 처치 가이드 (정적 — 수의학 일반 권고 기반, 진료 대체 아님)
    var GUIDES = [
        {
            id: "poison", emoji: "☠️", title: "중독 (초콜릿·포도·양파·자일리톨 등)",
            steps: [
                "무엇을 언제·얼마나 먹었는지 파악하고 포장지·남은 것을 챙긴다.",
                "임의로 토하게 하지 않는다 — 물질에 따라 더 위험할 수 있다.",
                "24h 병원에 먼저 전화해 섭취물·체중을 알리고 지시를 따른다.",
            ],
            warn: "부식성 물질(세제·표백제)·날카로운 것은 절대 구토 유도 금지.",
        },
        {
            id: "choke", emoji: "🦴", title: "이물질 삼킴 · 질식",
            steps: [
                "입안에 보이는 이물질만 조심히 제거(손가락을 물릴 수 있으니 주의).",
                "숨을 못 쉬면 소형견은 등 위쪽을, 대형견은 명치 아래를 밀어 올린다.",
                "호흡·의식이 없으면 지체 없이 즉시 병원으로 이동한다.",
            ],
            warn: "억지로 깊이 손을 넣으면 이물질을 더 밀어 넣을 수 있다.",
        },
        {
            id: "bleed", emoji: "🩸", title: "출혈 · 외상",
            steps: [
                "깨끗한 천·거즈로 상처를 직접 5분 이상 압박한다.",
                "피가 배어도 천을 떼지 말고 그 위에 덧대어 계속 누른다.",
                "지혈이 안 되거나 출혈이 심하면 압박한 채로 병원으로 이동한다.",
            ],
            warn: "지혈대는 잘못 쓰면 조직 손상 — 압박 지혈을 우선.",
        },
        {
            id: "seizure", emoji: "⚡", title: "경련 · 발작",
            steps: [
                "주변의 위험 물건을 치우고 아이를 만지거나 붙잡지 않는다.",
                "입에 손·물건을 넣지 않는다(혀를 삼키지 않는다).",
                "발작 시작 시각을 재고, 5분 이상 지속되면 즉시 병원으로.",
            ],
            warn: "발작 직후는 방향감각을 잃을 수 있으니 낙상 주의.",
        },
        {
            id: "heat", emoji: "🥵", title: "열사병 (헐떡임·침흘림·쓰러짐)",
            steps: [
                "그늘·시원한 곳으로 옮기고 미지근한 물로 몸을 적신다.",
                "선풍기·바람으로 식히되 얼음물·찬물은 급격해 위험하니 피한다.",
                "체온이 내려가도 반드시 병원 진료를 받는다(장기 손상 위험).",
            ],
            warn: "차 안·더운 날 방치가 흔한 원인 — 예방이 최선.",
        },
        {
            id: "burn", emoji: "🔥", title: "화상 · 화학물질 접촉",
            steps: [
                "화상 부위를 미지근한 흐르는 물에 10분 이상 식힌다.",
                "화학물질은 마르지 않게 충분히 물로 씻어내고 핥지 못하게 한다.",
                "연고·기름을 바르지 말고 병원 진료를 받는다.",
            ],
            warn: "얼음을 직접 대지 않는다 — 동상·조직 손상 위험.",
        },
    ];

    // 24h/야간 동물병원 후보 (기존 지도 데이터 재사용)
    function emergencyHospitals() {
        var list = (typeof PETLIFE_REAL_LOCATIONS !== "undefined" && Array.isArray(PETLIFE_REAL_LOCATIONS))
            ? PETLIFE_REAL_LOCATIONS : [];
        return list.filter(function (loc) {
            if (loc.category !== "hospital") return false;
            var hay = String(loc.hours || "") + " " + (Array.isArray(loc.tags) ? loc.tags.join(" ") : "");
            return /24|야간|응급/.test(hay);
        });
    }

    function guideHtml(g) {
        var steps = g.steps.map(function (s, i) {
            return '<li class="flex gap-2"><span class="text-rose-400 font-bold shrink-0">' + (i + 1) + ".</span><span>" + esc(s) + "</span></li>";
        }).join("");
        return '<details class="rounded-xl border border-gray-100 overflow-hidden">' +
            '<summary class="cursor-pointer select-none px-3 py-2.5 bg-gray-50 text-sm font-bold text-gray-800 flex items-center gap-2">' +
            '<span class="text-lg">' + g.emoji + "</span>" + esc(g.title) + "</summary>" +
            '<div class="px-3 py-3 space-y-2"><ul class="space-y-1.5 text-xs text-gray-700 leading-relaxed">' + steps + "</ul>" +
            '<p class="text-[11px] text-rose-500 bg-rose-50 rounded-lg px-2.5 py-1.5">⚠️ ' + esc(g.warn) + "</p></div></details>";
    }

    function hospitalHtml() {
        var hs = emergencyHospitals();
        if (!hs.length) {
            return '<button onclick="EmergencyCard.openMap()" class="w-full rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">🗺️ 지도에서 병원 찾기</button>';
        }
        var rows = hs.map(function (h) {
            var tel = String(h.phone || "").replace(/[^0-9+]/g, "");
            var callBtn = tel
                ? '<a href="tel:' + esc(tel) + '" class="rounded-lg bg-rose-500 hover:bg-rose-600 text-white px-3 py-1.5 text-xs font-bold whitespace-nowrap">📞 전화</a>'
                : "";
            return '<div class="rounded-xl border border-gray-100 px-3 py-2.5 flex items-center justify-between gap-2">' +
                '<div class="min-w-0"><p class="text-sm font-bold text-gray-900 truncate">' + esc(h.name) + "</p>" +
                '<p class="text-[11px] text-gray-500 truncate">' + esc(h.address || "") + " · " + esc(h.hours || "") + "</p></div>" +
                '<div class="flex gap-1.5 shrink-0">' + callBtn + "</div></div>";
        }).join("");
        return rows +
            '<button onclick="EmergencyCard.openMap()" class="w-full rounded-xl border border-brand-200 hover:bg-brand-50 text-brand-600 py-2.5 text-sm font-bold mt-1">🗺️ 지도에서 더 찾기</button>';
    }

    function open() {
        close();
        var overlay = document.createElement("div");
        overlay.id = "ec-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white z-10">' +
            '<h3 class="text-base font-extrabold text-gray-900">🚨 응급 카드</h3>' +
            '<button onclick="EmergencyCard.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' +
            '<div class="rounded-xl bg-rose-50 px-3 py-2.5 text-xs text-rose-700 leading-relaxed">' +
            '위급하면 <b>먼저 24h 병원에 전화</b>해 상황을 알리고 지시를 따르세요. 아래 처치는 병원 도착 전 응급 조치일 뿐 진료를 대체하지 않아요.</div>' +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">상황별 1차 처치</p>' +
            '<div class="space-y-1.5">' + GUIDES.map(guideHtml).join("") + "</div></div>" +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">가까운 24시간 · 야간 병원</p>' +
            '<div class="space-y-1.5">' + hospitalHtml() + "</div></div>" +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 응급 카드는 참고용이며 수의사의 진료·진단을 대체하지 않아요. 병원 정보는 실제 운영 시간과 다를 수 있으니 방문 전 전화로 확인하세요.</p>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function close() { var el = document.getElementById("ec-overlay"); if (el) el.remove(); }

    // 기존 지도 탭(shop)으로 이동 — 신규 지도 모듈 없이 재사용
    function openMap() {
        close();
        if (typeof switchTab === "function") switchTab("shop");
        else if (typeof showToast === "function") showToast("지도를 열 수 없어요");
    }

    window.EmergencyCard = { open: open, close: close, openMap: openMap };
})();
