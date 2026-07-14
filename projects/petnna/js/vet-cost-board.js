// 🏥 병원비 제보·비교 보드 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// 사용자가 진료 항목별 실제 지불 금액을 익명 제보하고, 모인 제보의 평균가를
// 노출해 병원비 투명성을 높인다. 첫걸음으로 프론트 전용(localStorage) 구현 —
// 신규 DB 테이블 없음(회의 가드/qol-checkin과 동일 원칙). 서버 공유 집계는
// 후속 과제(Supabase 계약 필요)로 남긴다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_vet_cost_reports"; // [{ item, amount, region, date }]

    // 대표 진료 항목(익명 제보 단위)
    var ITEMS = [
        { key: "checkup", label: "기본 진찰료" },
        { key: "vaccine", label: "예방접종(1회)" },
        { key: "heartworm", label: "심장사상충 예방(1개월)" },
        { key: "blood", label: "혈액검사" },
        { key: "xray", label: "엑스레이" },
        { key: "ultrasound", label: "초음파" },
        { key: "scaling", label: "스케일링" },
        { key: "neuter", label: "중성화 수술" },
    ];

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }
    function fmt(n) { return Math.round(n).toLocaleString("ko-KR"); }

    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || []; } catch (e) { return []; } }
    function saveAll(list) { localStorage.setItem(LS_KEY, JSON.stringify(list)); }

    function labelOf(key) {
        for (var i = 0; i < ITEMS.length; i++) if (ITEMS[i].key === key) return ITEMS[i].label;
        return key;
    }

    // 항목별 집계(제보 건수·평균·최저·최고)
    function summarize() {
        var reports = loadAll();
        return ITEMS.map(function (it) {
            var vals = reports.filter(function (r) { return r.item === it.key; })
                .map(function (r) { return r.amount; });
            if (!vals.length) return { key: it.key, label: it.label, count: 0 };
            var sum = vals.reduce(function (a, b) { return a + b; }, 0);
            return {
                key: it.key, label: it.label, count: vals.length,
                avg: sum / vals.length, min: Math.min.apply(null, vals), max: Math.max.apply(null, vals),
            };
        });
    }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var rows = summarize();
        var total = loadAll().length;

        var tableHtml = rows.map(function (r) {
            var right = r.count
                ? '<span class="text-brand-600 font-bold">' + fmt(r.avg) + '원</span>' +
                  '<span class="text-[10px] text-gray-400 ml-1">(' + r.count + '건)</span>'
                : '<span class="text-[11px] text-gray-300">제보 없음</span>';
            var range = r.count
                ? '<div class="text-[10px] text-gray-400">' + fmt(r.min) + '~' + fmt(r.max) + '원</div>'
                : "";
            return '<div class="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">' +
                '<span class="text-xs text-gray-700">' + esc(r.label) + '</span>' +
                '<div class="text-right text-xs">' + right + range + '</div></div>';
        }).join("");

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">🏥</span>병원비 제보·비교 보드</h3>' +
            '<button onclick="VetCostBoard.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">금액 제보</button>' +
            "</div>" +
            '<p class="text-[11px] text-gray-400 mb-2">진료 항목별 실제 지불 금액을 익명으로 모아 평균가를 보여줘요' +
            (total ? ' (누적 ' + total + '건)' : "") + '.</p>' +
            '<div>' + tableHtml + "</div>" +
            "</div>";
    }

    // ── 제보 모달 ──────────────────────────────────────────────
    function open() {
        close();
        var itemOptions = ITEMS.map(function (it) {
            return '<option value="' + it.key + '">' + esc(it.label) + "</option>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "vetcost-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🏥 병원비 익명 제보</h3>' +
            '<button onclick="VetCostBoard.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3">' +
            '<div><label class="text-xs font-bold text-gray-500">진료 항목</label>' +
            '<select id="vetcost-item" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + itemOptions + "</select></div>" +
            '<div><label class="text-xs font-bold text-gray-500">지불 금액(원)</label>' +
            '<input id="vetcost-amount" type="number" min="0" step="100" placeholder="예: 15000" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">지역(선택)</label>' +
            '<input id="vetcost-region" type="text" maxlength="20" placeholder="예: 서울 강남구" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<p class="text-[10px] text-gray-400 leading-relaxed">병원명·개인정보는 저장하지 않아요. 익명 제보만 집계됩니다.</p>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2">' +
            '<button onclick="VetCostBoard.submit()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">제보하기</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function submit() {
        var overlay = document.getElementById("vetcost-overlay");
        if (!overlay) return;
        var item = (document.getElementById("vetcost-item") || {}).value;
        var amountRaw = (document.getElementById("vetcost-amount") || {}).value;
        var region = ((document.getElementById("vetcost-region") || {}).value || "").trim();

        var amount = amountRaw ? parseFloat(amountRaw) : NaN;
        if (!item || !isFinite(amount) || amount <= 0) { toast("올바른 금액을 입력해 주세요"); return; }

        var list = loadAll();
        list.push({ item: item, amount: amount, region: region.slice(0, 20), date: new Date().toISOString() });
        saveAll(list);

        close();
        toast(labelOf(item) + " " + fmt(amount) + "원 제보 완료! 🙏");
        renderWidget("vet-cost-board-widget");
    }

    function close() { var el = document.getElementById("vetcost-overlay"); if (el) el.remove(); }

    window.VetCostBoard = { open: open, submit: submit, close: close, renderWidget: renderWidget };
})();
