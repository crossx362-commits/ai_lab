// 💰 반려동물 가계부 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// 사료·병원·미용·용품 등 카테고리별 지출을 기록하고 선택한 달의
// 카테고리별 집계를 막대로 보여준다. 첫걸음으로 프론트 전용(localStorage)
// 구현 — 신규 DB 테이블 없음(vet-cost-board·qol-checkin과 동일 원칙).
// 서버 동기화는 후속 과제(Supabase 계약 필요)로 남긴다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_expense_records"; // [{ cat, amount, memo, date(YYYY-MM-DD) }]

    var CATS = [
        { key: "food", label: "사료·간식", emoji: "🍚" },
        { key: "medical", label: "병원", emoji: "🏥" },
        { key: "grooming", label: "미용", emoji: "✂️" },
        { key: "supplies", label: "용품", emoji: "🧸" },
        { key: "etc", label: "기타", emoji: "🐾" },
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

    function catOf(key) {
        for (var i = 0; i < CATS.length; i++) if (CATS[i].key === key) return CATS[i];
        return { key: key, label: key, emoji: "🐾" };
    }

    function currentMonth() { return new Date().toISOString().slice(0, 7); } // YYYY-MM

    // 선택 달(month=YYYY-MM)의 카테고리별 합계·총액
    function summarize(month) {
        var recs = loadAll().filter(function (r) { return (r.date || "").slice(0, 7) === month; });
        var byCat = CATS.map(function (c) {
            var sum = recs.filter(function (r) { return r.cat === c.key; })
                .reduce(function (a, r) { return a + (r.amount || 0); }, 0);
            return { key: c.key, label: c.label, emoji: c.emoji, sum: sum };
        });
        var total = byCat.reduce(function (a, c) { return a + c.sum; }, 0);
        return { byCat: byCat, total: total, count: recs.length };
    }

    // 기록이 있는 달 목록(최신순, 없으면 이번 달만)
    function months() {
        var set = {};
        loadAll().forEach(function (r) { var m = (r.date || "").slice(0, 7); if (m) set[m] = 1; });
        var list = Object.keys(set).sort().reverse();
        if (list.indexOf(currentMonth()) === -1) list.unshift(currentMonth());
        return list;
    }

    var _selMonth = null;

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var month = _selMonth || currentMonth();
        var sum = summarize(month);

        var monthOpts = months().map(function (m) {
            return '<option value="' + m + '"' + (m === month ? " selected" : "") + ">" + esc(m) + "</option>";
        }).join("");

        var bars = sum.byCat.map(function (c) {
            var pct = sum.total > 0 ? Math.round((c.sum / sum.total) * 100) : 0;
            return '<div class="py-1.5">' +
                '<div class="flex items-center justify-between text-xs mb-1">' +
                '<span class="text-gray-700">' + c.emoji + " " + esc(c.label) + "</span>" +
                '<span class="text-brand-600 font-bold">' + fmt(c.sum) + "원</span></div>" +
                '<div class="h-2 rounded-full bg-gray-100 overflow-hidden">' +
                '<div class="h-full rounded-full bg-brand-400" style="width:' + pct + '%"></div></div></div>';
        }).join("");

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">💰</span>반려동물 가계부</h3>' +
            '<button onclick="ExpenseTracker.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">지출 기록</button>' +
            "</div>" +
            '<div class="flex items-center justify-between mb-3">' +
            '<select onchange="ExpenseTracker.selectMonth(this.value)" class="rounded-lg border border-gray-200 px-2 py-1 text-xs focus:border-brand-400 focus:outline-none">' + monthOpts + "</select>" +
            '<span class="text-sm font-extrabold text-gray-900">합계 ' + fmt(sum.total) + "원</span></div>" +
            (sum.count ? '<div>' + bars + "</div>"
                : '<p class="text-[11px] text-gray-300 py-4 text-center">이 달 지출 기록이 없어요. 지출을 추가해 보세요.</p>') +
            "</div>";
    }

    // ── 기록 모달 ──────────────────────────────────────────────
    function open() {
        close();
        var catOptions = CATS.map(function (c) {
            return '<option value="' + c.key + '">' + c.emoji + " " + esc(c.label) + "</option>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "expense-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">💰 지출 기록</h3>' +
            '<button onclick="ExpenseTracker.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3">' +
            '<div><label class="text-xs font-bold text-gray-500">카테고리</label>' +
            '<select id="expense-cat" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + catOptions + "</select></div>" +
            '<div><label class="text-xs font-bold text-gray-500">금액(원)</label>' +
            '<input id="expense-amount" type="number" min="0" step="100" placeholder="예: 30000" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">날짜</label>' +
            '<input id="expense-date" type="date" value="' + new Date().toISOString().slice(0, 10) + '" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">메모(선택)</label>' +
            '<input id="expense-memo" type="text" maxlength="30" placeholder="예: 정기 예방접종" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2">' +
            '<button onclick="ExpenseTracker.submit()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function submit() {
        var overlay = document.getElementById("expense-overlay");
        if (!overlay) return;
        var cat = (document.getElementById("expense-cat") || {}).value;
        var amountRaw = (document.getElementById("expense-amount") || {}).value;
        var date = (document.getElementById("expense-date") || {}).value || new Date().toISOString().slice(0, 10);
        var memo = ((document.getElementById("expense-memo") || {}).value || "").trim();

        var amount = amountRaw ? parseFloat(amountRaw) : NaN;
        if (!cat || !isFinite(amount) || amount <= 0) { toast("올바른 금액을 입력해 주세요"); return; }

        var list = loadAll();
        list.push({ cat: cat, amount: amount, memo: memo.slice(0, 30), date: date });
        saveAll(list);

        _selMonth = date.slice(0, 7); // 방금 기록한 달로 이동
        close();
        toast(catOf(cat).label + " " + fmt(amount) + "원 기록 완료! 💰");
        renderWidget("expense-tracker-widget");
    }

    function selectMonth(m) { _selMonth = m; renderWidget("expense-tracker-widget"); }

    function close() { var el = document.getElementById("expense-overlay"); if (el) el.remove(); }

    window.ExpenseTracker = { open: open, submit: submit, close: close, selectMonth: selectMonth, renderWidget: renderWidget };
})();
