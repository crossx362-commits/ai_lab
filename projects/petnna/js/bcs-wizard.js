// 🐾 BCS(체형점수) 셀프 체크 위저드 UI — 백로그 나무_20260712143815(나무 제안, P3)
// ─────────────────────────────────────────────────────────────
// 갈비뼈 촉감·허리 라인·복부 라인 등 5문항 시각 위저드 화면. 문항 데이터와
// 저장/판정 로직은 js/bcs-wizard-data.js(BcsWizardData)를 사용한다.
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
    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }

    function data() { return window.BcsWizardData; }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el || !data()) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }

        var h = data().history(pet.id);
        var last = h[h.length - 1];
        var cat = last ? data().classify(last.score) : null;

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">🐾</span>BCS 체형 셀프체크</h3>' +
            '<button onclick="BcsWizard.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">' +
            (last ? "다시 측정" : "측정하기") + "</button>" +
            "</div>" +
            (last
                ? '<div class="flex items-center gap-3 mb-1 text-xs text-gray-500">' +
                  '<span class="inline-flex items-center gap-1 font-bold text-' + cat.color + '-600 text-sm">' + cat.emoji + " " + esc(cat.label) + "</span>" +
                  '<span>BCS <b class="text-brand-600">' + last.score.toFixed(1) + '</b>/5.0</span>' +
                  '<span>' + esc(new Date(last.date).toLocaleDateString("ko-KR")) + "</span>" +
                  "</div>"
                : '<p class="text-xs text-gray-400 leading-relaxed">갈비뼈·허리·복부 라인 5문항으로 우리 아이 체형을 확인해 보세요.</p>') +
            '<div id="bcs-nudge"></div>' +
            "</div>";

        if (cat && cat.key !== "ideal") renderNudge(cat);
    }

    function renderNudge(cat) {
        var el = document.getElementById("bcs-nudge");
        if (!el) return;
        var msg = cat.key === "over"
            ? "과체중이 의심돼요. 급여량·운동을 점검하고 필요하면 상담해 보세요."
            : "저체중이 의심돼요. 식사량·건강 상태가 걱정되면 상담해 보세요.";
        el.innerHTML =
            '<div class="mt-3 rounded-xl bg-' + cat.color + "-50 border border-" + cat.color + '-100 px-3 py-2.5 flex items-center justify-between gap-2">' +
            '<p class="text-xs text-' + cat.color + '-700 leading-relaxed">' + esc(msg) + "</p>" +
            (typeof openVetChatModal === "function"
                ? '<button onclick="openVetChatModal()" class="shrink-0 text-xs font-bold text-white bg-' + cat.color + "-500 hover:bg-" + cat.color + '-600 px-3 py-1.5 rounded-full whitespace-nowrap">AI 수의사</button>'
                : "") +
            "</div>";
    }

    // ── 위저드 모달 ────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet || !data()) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        close();

        var qHtml = data().QUESTIONS.map(function (q) {
            var opts = q.options.map(function (o) {
                return '<button type="button" class="bcs-opt-btn w-full text-left px-3 py-2 rounded-xl border border-gray-200 hover:border-brand-300 hover:bg-brand-50 transition-all text-xs text-gray-700" ' +
                    'data-q="' + q.key + '" data-v="' + o.v + '">' + esc(o.label) + "</button>";
            }).join("");
            return '<div class="space-y-1.5" data-question="' + q.key + '">' +
                '<p class="text-xs font-bold text-gray-700">' + q.emoji + " " + esc(q.label) +
                ' <span class="font-normal text-gray-400">— ' + esc(q.desc) + "</span></p>" +
                '<div class="grid gap-1.5">' + opts + "</div></div>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "bcs-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🐾 ' + esc(pet.name) + ' BCS 체형 체크</h3>' +
            '<button onclick="BcsWizard.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' + qHtml + "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 sticky bottom-0 bg-white">' +
            '<button onclick="BcsWizard.submit()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">결과 보기</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        overlay.querySelectorAll(".bcs-opt-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var group = overlay.querySelector('[data-question="' + btn.dataset.q + '"]');
                group.querySelectorAll(".bcs-opt-btn").forEach(function (b) {
                    b.classList.remove("bg-brand-500", "border-brand-500", "text-white");
                });
                btn.classList.add("bg-brand-500", "border-brand-500", "text-white");
            });
        });
    }

    function submit() {
        var pet = activePet();
        if (!pet || !data()) return;
        var overlay = document.getElementById("bcs-overlay");
        if (!overlay) return;

        var answers = {};
        var missing = [];
        data().QUESTIONS.forEach(function (q) {
            var picked = overlay.querySelector('.bcs-opt-btn.bg-brand-500[data-q="' + q.key + '"]');
            if (picked) answers[q.key] = parseInt(picked.dataset.v, 10);
            else missing.push(q.label);
        });
        if (missing.length) { toast("응답 안 한 항목이 있어요: " + missing.join(", ")); return; }

        var score = data().QUESTIONS.reduce(function (sum, q) { return sum + answers[q.key]; }, 0) / data().QUESTIONS.length;
        var cat = data().classify(score);
        data().record(pet.id, answers, score, cat.key);

        close();
        toast(cat.emoji + " BCS " + score.toFixed(1) + "/5.0 · " + cat.label + " 판정");
        renderWidget("bcs-wizard-widget");
    }

    function close() { var el = document.getElementById("bcs-overlay"); if (el) el.remove(); }

    window.BcsWizard = { open: open, submit: submit, close: close, renderWidget: renderWidget };
})();
