// 🍚 맞춤 식단·급여량 추천 카드 — 백로그 나무(P2, 기획)
// ─────────────────────────────────────────────────────────────
// BCS 체형(BcsWizardData) + 체중(pet.weight) + 최근 산책 활동량(walks)을
// 룰 기반으로 종합해 하루 권장 kcal·건사료 급여량(g)을 추천한다.
// 표준 수의학 공식: RER = 70 × (체중kg)^0.75, DER = RER × 활동계수 × 체형보정.
// 프론트 전용 — 읽기만 함(신규 저장/DB 없음, 사료 밀도는 CalorieTracker 재사용).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var DEFAULT_DENSITY = 350; // kcal/100g (건사료 근사)

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
    function densityFor(petId) {
        if (window.CalorieTracker && typeof window.CalorieTracker._densityFor === "function") {
            return window.CalorieTracker._densityFor(petId);
        }
        return DEFAULT_DENSITY;
    }

    // 최근 7일 산책 활동량 판정 (walk.id = Date.now() 기준)
    function walkActivity() {
        var list = (typeof walks !== "undefined" && Array.isArray(walks)) ? walks : [];
        var weekAgo = Date.now() - 7 * 86400000;
        var recent = list.filter(function (w) { return w && typeof w.id === "number" && w.id >= weekAgo; });
        var count = recent.length;
        var km = recent.reduce(function (s, w) { return s + (parseFloat(w.distance) || 0); }, 0);
        if (count === 0) return { level: "unknown", factor: 1.5, label: "산책 기록 부족", count: 0, km: 0 };
        if (count >= 6 || km >= 15) return { level: "high", factor: 2.0, label: "활발함", count: count, km: km };
        if (count >= 3 || km >= 5) return { level: "moderate", factor: 1.6, label: "보통 활동", count: count, km: km };
        return { level: "low", factor: 1.4, label: "저활동", count: count, km: km };
    }

    // BCS 최근 체형 판정 (없으면 null)
    function bcsCategory(petId) {
        if (!window.BcsWizardData) return null;
        var h = window.BcsWizardData.history(petId);
        var last = h && h[h.length - 1];
        if (!last) return null;
        return window.BcsWizardData.classify(last.score);
    }

    // 체형별 보정계수 (과체중=감량, 저체중=증량)
    function bcsAdjust(cat) {
        if (!cat) return { mult: 1.0, note: "" };
        if (cat.key === "over") return { mult: 0.85, note: "과체중 판정 → 감량 목표로 15% 낮춤" };
        if (cat.key === "under") return { mult: 1.15, note: "저체중 판정 → 증량 목표로 15% 높임" };
        return { mult: 1.0, note: "정상 체형 유지" };
    }

    function compute(pet) {
        var w = parseFloat(pet && pet.weight);
        if (!w || w <= 0) return null;
        var rer = 70 * Math.pow(w, 0.75);
        var act = walkActivity();
        var cat = bcsCategory(pet.id);
        var adj = bcsAdjust(cat);
        var kcal = Math.round(rer * act.factor * adj.mult);
        var density = densityFor(pet.id);
        var grams = Math.round((kcal / density) * 100);
        return { weight: w, kcal: kcal, grams: grams, density: density, act: act, cat: cat, adj: adj };
    }

    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }

        var r = compute(pet);
        if (!r) {
            el.innerHTML =
                '<div class="card-modern p-5">' +
                '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2 mb-2"><span class="text-xl">🍚</span>맞춤 식단·급여량 추천</h3>' +
                '<p class="text-xs text-amber-600 bg-amber-50 rounded-xl px-3 py-2">추천을 계산하려면 마이펫에서 체중을 입력해 주세요 ⚖️</p>' +
                "</div>";
            return;
        }

        var catBadge = r.cat
            ? '<span class="inline-flex items-center gap-1 font-bold text-' + r.cat.color + '-600">' + r.cat.emoji + " " + esc(r.cat.label) + "</span>"
            : '<span class="text-gray-400">체형 미측정</span>';

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2 mb-3"><span class="text-xl">🍚</span>맞춤 식단·급여량 추천</h3>' +
            '<div class="grid grid-cols-2 gap-2 mb-3">' +
            '<div class="rounded-xl bg-brand-50 px-3 py-2.5 text-center">' +
            '<p class="text-[11px] text-brand-500 mb-0.5">하루 권장 칼로리</p>' +
            '<p class="text-xl font-extrabold text-brand-600">' + r.kcal + ' <span class="text-xs font-bold">kcal</span></p>' +
            "</div>" +
            '<div class="rounded-xl bg-amber-50 px-3 py-2.5 text-center">' +
            '<p class="text-[11px] text-amber-500 mb-0.5">건사료 급여량</p>' +
            '<p class="text-xl font-extrabold text-amber-600">' + r.grams + ' <span class="text-xs font-bold">g</span></p>' +
            "</div>" +
            "</div>" +
            '<div class="space-y-1 text-[11px] text-gray-500">' +
            '<div class="flex items-center justify-between"><span>체중</span><span class="font-bold text-gray-700">' + r.weight + ' kg</span></div>' +
            '<div class="flex items-center justify-between"><span>체형(BCS)</span><span>' + catBadge + "</span></div>" +
            '<div class="flex items-center justify-between"><span>최근 7일 활동량</span><span class="font-bold text-gray-700">' + esc(r.act.label) +
            (r.act.count ? ' · ' + r.act.count + '회 / ' + r.act.km.toFixed(1) + 'km' : '') + "</span></div>" +
            '<div class="flex items-center justify-between"><span>사료 열량밀도</span><span class="font-bold text-gray-700">' + r.density + ' kcal/100g</span></div>' +
            "</div>" +
            (r.adj.note
                ? '<p class="mt-2 text-[11px] text-' + (r.cat ? r.cat.color : "gray") + '-600 bg-' + (r.cat ? r.cat.color : "gray") + '-50 rounded-lg px-2.5 py-1.5">' + esc(r.adj.note) + "</p>"
                : "") +
            '<p class="mt-2 text-[10px] text-gray-400 leading-relaxed">※ 룰 기반 참고용 추천입니다. 실제 급여량은 사료 포장 지침·수의사 상담을 우선하세요.</p>' +
            "</div>";
    }

    window.DietRecommend = { renderWidget: renderWidget, _compute: compute, _walkActivity: walkActivity };
})();
