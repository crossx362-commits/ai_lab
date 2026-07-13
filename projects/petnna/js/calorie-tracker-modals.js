// 🍽️ 일일 급식·칼로리 트래커 — 급여 기록/활동량 설정 모달 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// js/calorie-tracker-core.js(CalorieTracker)가 만든 위젯의 "급여 기록"/"활동량"
// 버튼에서 호출되는 인터랙션(모달 열기/저장/삭제)만 담당한다. 같은 CalorieTracker
// 전역 객체를 확장한다 — 스크립트 로드 순서는 core.js가 먼저여야 한다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function CT() { return window.CalorieTracker || {}; }
    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }

    function openAdd() {
        var pet = CT()._activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        closeModal();

        var overlay = document.createElement("div");
        overlay.id = "cal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🍽️ 급여 기록</h3>' +
            '<button onclick="CalorieTracker.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3">' +
            '<div class="flex gap-2" id="cal-kind">' +
            '<button type="button" data-kind="food" class="cal-kind-btn flex-1 py-2 rounded-xl border border-brand-500 bg-brand-500 text-white text-xs font-bold">🥣 사료</button>' +
            '<button type="button" data-kind="treat" class="cal-kind-btn flex-1 py-2 rounded-xl border border-gray-200 text-gray-600 text-xs font-bold">🍖 간식</button>' +
            "</div>" +
            '<div><label class="text-xs font-bold text-gray-500">내용(선택)</label>' +
            '<input id="cal-label" type="text" placeholder="예: 연어 습식" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div class="grid grid-cols-2 gap-2">' +
            '<div><label class="text-xs font-bold text-gray-500">급여량 (g)</label>' +
            '<input id="cal-grams" type="number" step="1" min="0" placeholder="80" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">100g당 kcal</label>' +
            '<input id="cal-density" type="number" step="1" min="0" placeholder="350" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            "</div>" +
            '<p class="text-[11px] text-gray-400 leading-relaxed">사료 포장지의 "100g당 kcal"(대사에너지)를 입력하면 정확해요. 모르면 급여 kcal을 아래에 직접 넣어도 돼요.</p>' +
            '<div><label class="text-xs font-bold text-gray-500">또는 총 kcal 직접 입력</label>' +
            '<input id="cal-kcal" type="number" step="1" min="0" placeholder="직접 입력 시 위 값 무시" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100">' +
            '<button onclick="CalorieTracker.submit()" class="w-full rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) closeModal(); });
        document.body.appendChild(overlay);

        overlay.querySelectorAll(".cal-kind-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                overlay.querySelectorAll(".cal-kind-btn").forEach(function (b) {
                    b.classList.remove("bg-brand-500", "border-brand-500", "text-white");
                    b.classList.add("border-gray-200", "text-gray-600");
                });
                btn.classList.remove("border-gray-200", "text-gray-600");
                btn.classList.add("bg-brand-500", "border-brand-500", "text-white");
            });
        });
    }

    function submit() {
        var pet = CT()._activePet();
        if (!pet) return;
        var overlay = document.getElementById("cal-overlay");
        if (!overlay) return;

        var picked = overlay.querySelector(".cal-kind-btn.bg-brand-500");
        var kind = picked ? picked.dataset.kind : "food";
        var label = (document.getElementById("cal-label") || {}).value || "";

        var directKcal = parseFloat((document.getElementById("cal-kcal") || {}).value);
        var kcal;
        if (directKcal > 0) {
            kcal = directKcal;
        } else {
            var grams = parseFloat((document.getElementById("cal-grams") || {}).value);
            var density = parseFloat((document.getElementById("cal-density") || {}).value);
            if (!(grams > 0) || !(density > 0)) { toast("급여량·100g당 kcal 또는 총 kcal을 입력해 주세요"); return; }
            kcal = (grams * density) / 100;
        }

        var map = CT()._loadAll();
        var key = String(pet.id);
        var dk = CT()._todayKey();
        if (!map[key]) map[key] = {};
        if (!map[key][dk]) map[key][dk] = [];
        map[key][dk].push({ id: Date.now(), kind: kind, label: label.trim(), kcal: Math.round(kcal) });
        CT()._saveAll(map);

        closeModal();
        toast("급여 기록 완료! +" + Math.round(kcal) + " kcal 🍽️");
        CT().renderWidget("calorie-tracker-widget");
    }

    function remove(id) {
        var pet = CT()._activePet();
        if (!pet) return;
        var map = CT()._loadAll();
        var key = String(pet.id);
        var dk = CT()._todayKey();
        if (map[key] && map[key][dk]) {
            map[key][dk] = map[key][dk].filter(function (r) { return r.id !== id; });
            CT()._saveAll(map);
        }
        CT().renderWidget("calorie-tracker-widget");
    }

    // ── 활동량 계수 선택 모달 ──────────────────────────────────
    function openFactor() {
        var pet = CT()._activePet();
        if (!pet) return;
        closeModal();
        var cur = CT()._factorFor(pet.id);
        var esc = CT()._esc;

        var opts = CT()._FACTORS.map(function (f) {
            var sel = f.v === cur;
            return '<button type="button" onclick="CalorieTracker.setFactor(' + f.v + ')" ' +
                'class="w-full flex items-center gap-3 px-4 py-3 rounded-xl border transition-all ' +
                (sel ? "border-brand-500 bg-brand-50" : "border-gray-200 hover:border-brand-300") + '">' +
                '<span class="text-lg">' + f.emoji + '</span>' +
                '<span class="flex-1 text-left text-sm font-bold text-gray-700">' + esc(f.label) + '</span>' +
                '<span class="text-xs text-gray-400">×' + f.v + '</span>' +
                (sel ? '<span class="text-brand-500">✓</span>' : "") +
                "</button>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "cal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🐾 활동량 설정</h3>' +
            '<button onclick="CalorieTracker.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-2">' + opts + "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) closeModal(); });
        document.body.appendChild(overlay);
    }

    function setFactor(v) {
        var pet = CT()._activePet();
        if (!pet) return;
        var map = CT()._loadFactors();
        map[String(pet.id)] = v;
        CT()._saveFactors(map);
        closeModal();
        CT().renderWidget("calorie-tracker-widget");
    }

    // ── 사료 열량(kcal/100g) 설정 모달 — 권장 사료량(g) 환산용 ────────
    function openDensity() {
        var pet = CT()._activePet();
        if (!pet) return;
        closeModal();
        var cur = CT()._densityFor(pet.id);

        var overlay = document.createElement("div");
        overlay.id = "cal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🥣 사료 열량 설정</h3>' +
            '<button onclick="CalorieTracker.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-2">' +
            '<label class="text-xs font-bold text-gray-500">100g당 kcal</label>' +
            '<input id="cal-density-input" type="number" min="100" max="600" step="10" value="' + cur + '" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">사료 포장지의 100g당 열량을 입력하세요 (건사료 평균 약 350).</p>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100">' +
            '<button onclick="CalorieTracker.setDensity()" class="w-full rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) closeModal(); });
        document.body.appendChild(overlay);
    }

    function setDensity() {
        var pet = CT()._activePet();
        if (!pet) return;
        var v = parseFloat((document.getElementById("cal-density-input") || {}).value);
        if (!(v > 0)) { toast("100g당 kcal을 입력해 주세요"); return; }
        var map = CT()._loadDensities();
        map[String(pet.id)] = v;
        CT()._saveDensities(map);
        closeModal();
        CT().renderWidget("calorie-tracker-widget");
    }

    function closeModal() { var el = document.getElementById("cal-overlay"); if (el) el.remove(); }

    Object.assign(window.CalorieTracker, {
        openAdd: openAdd,
        submit: submit,
        remove: remove,
        openFactor: openFactor,
        setFactor: setFactor,
        openDensity: openDensity,
        setDensity: setDensity,
        close: closeModal,
    });
})();
