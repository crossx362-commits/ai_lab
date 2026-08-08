// 📦 사료·용품 재고 소진 D-day 추적기 — 백로그 나무(P2, 기획)
// ─────────────────────────────────────────────────────────────
// 개봉일·총 용량·일 급여량으로 소진 예상일(D-day)과 진행바를 표시하고,
// 임박하면 재구매 넛지를 준다. 첫걸음으로 프론트 전용(localStorage) —
// 신규 DB 테이블 없음(expense-tracker·vet-cost-board와 동일 원칙).
// 서버 동기화는 후속 과제(Supabase 계약 필요)로 남긴다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_inventory_items"; // [{ id, name, unit, total, daily, openDate(YYYY-MM-DD) }]
    var NOTIFIED_KEY = "petna_inventory_notified"; // { itemId: "YYYY-MM-DD" } 하루 1회 알림 중복 방지
    var NUDGE_DAYS = 7;     // 남은 일수가 이 값 이하면 재구매 넛지
    var CRITICAL_DAYS = 3;  // 3일치 이하면 긴급 배지 + 브라우저 알림

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }
    function fmt(n) { return Math.round(n).toLocaleString("ko-KR"); }
    function today() { return new Date().toISOString().slice(0, 10); }

    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || []; } catch (e) { return []; } }
    function saveAll(list) { localStorage.setItem(LS_KEY, JSON.stringify(list)); }

    // openDate로부터 오늘까지 지난 일수(음수 방지)
    function daysSince(dateStr) {
        var start = new Date(dateStr + "T00:00:00");
        if (isNaN(start.getTime())) return 0;
        var now = new Date(today() + "T00:00:00");
        var d = Math.floor((now - start) / 86400000);
        return d > 0 ? d : 0;
    }

    // 재고 계산: 소비량·잔량·남은 일수·소진 예상일(YYYY-MM-DD)
    function compute(item) {
        var total = Number(item.total) || 0;
        var daily = Number(item.daily) || 0;
        var used = daily * daysSince(item.openDate);
        var remaining = total - used;
        if (remaining < 0) remaining = 0;
        var pct = total > 0 ? Math.round((remaining / total) * 100) : 0;
        var daysLeft = daily > 0 ? Math.floor(remaining / daily) : null;
        var depletion = null;
        if (daysLeft != null) {
            var d = new Date(today() + "T00:00:00");
            d.setDate(d.getDate() + daysLeft);
            depletion = d.toISOString().slice(0, 10);
        }
        return { remaining: remaining, pct: pct, daysLeft: daysLeft, depletion: depletion };
    }

    // shop 탭으로 원터치 이동(재구매)
    function goShop() {
        if (typeof window.switchTab === "function") window.switchTab("shop");
    }

    // 3일치 이하 품목을 브라우저 알림으로 1일 1회 통지(권한 있을 때만, 중복 방지)
    function notifyCritical(items) {
        if (!items || !items.length) return;
        if (!("Notification" in window) || Notification.permission !== "granted") return;
        var notified;
        try { notified = JSON.parse(localStorage.getItem(NOTIFIED_KEY)) || {}; } catch (e) { notified = {}; }
        var day = today();
        var fresh = items.filter(function (it) { return notified[it.id] !== day; });
        if (!fresh.length) return;
        var names = fresh.map(function (it) { return it.name; }).join(", ");
        try {
            var n = new Notification("🛒 재구매가 필요해요", {
                body: names + " 소진이 3일 내로 임박했어요. 탭하면 스토어로 이동해요.",
                tag: "petna-inventory-restock"
            });
            n.onclick = function () { try { window.focus(); } catch (e) {} goShop(); n.close(); };
        } catch (e) { /* 알림 실패는 무해 — 위젯 배지로 이미 노출됨 */ }
        fresh.forEach(function (it) { notified[it.id] = day; });
        try { localStorage.setItem(NOTIFIED_KEY, JSON.stringify(notified)); } catch (e) {}
    }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var list = loadAll();

        var critical = [];
        var rows = list.map(function (it) {
            var c = compute(it);
            var barColor = c.pct > 50 ? "bg-brand-400" : (c.pct > 20 ? "bg-amber-400" : "bg-red-400");
            var low = c.daysLeft != null && c.daysLeft <= NUDGE_DAYS;
            var crit = c.daysLeft != null && c.daysLeft <= CRITICAL_DAYS;
            if (crit) critical.push(it);
            var ddText = c.daysLeft == null ? "—"
                : (c.daysLeft <= 0 ? "소진됨" : "D-" + c.daysLeft);
            var ddColor = c.daysLeft != null && c.daysLeft <= 0 ? "text-red-500"
                : (crit ? "text-rose-600" : (low ? "text-amber-600" : "text-gray-500"));
            return '<div class="py-2 border-t border-gray-50 first:border-t-0">' +
                '<div class="flex items-center justify-between mb-1">' +
                '<span class="text-xs font-bold text-gray-800 truncate">' + esc(it.name) +
                (crit ? ' <span class="ml-1 align-middle text-[9px] font-extrabold text-rose-700 bg-rose-100 rounded-full px-1.5 py-0.5">임박</span>' : "") + "</span>" +
                '<span class="flex items-center gap-2">' +
                '<span class="text-xs font-extrabold ' + ddColor + '">' + ddText + "</span>" +
                '<button onclick="InventoryTracker.remove(\'' + esc(it.id) + '\')" class="text-gray-300 hover:text-red-400 text-sm leading-none" aria-label="삭제">&times;</button>' +
                "</span></div>" +
                '<div class="h-2 rounded-full bg-gray-100 overflow-hidden">' +
                '<div class="h-full rounded-full ' + barColor + '" style="width:' + c.pct + '%"></div></div>' +
                '<div class="flex items-center justify-between text-[10px] text-gray-400 mt-1">' +
                '<span>잔량 약 ' + fmt(c.remaining) + esc(it.unit || "") + " / " + fmt(it.total) + esc(it.unit || "") + "</span>" +
                '<span>' + (c.depletion ? "소진 " + esc(c.depletion) : "") + "</span></div>" +
                (low ? '<div class="mt-1 flex items-center justify-between gap-2 text-[10px] font-bold ' +
                    (crit ? "text-rose-700 bg-rose-50" : "text-amber-700 bg-amber-50") + ' rounded-lg px-2 py-1">' +
                    '<span>🛒 ' + (crit ? "3일 내 소진돼요. 지금 재구매하세요!" : "곧 소진돼요. 재구매를 준비하세요!") + "</span>" +
                    '<button onclick="InventoryTracker.goShop()" class="shrink-0 text-white bg-brand-500 hover:bg-brand-600 rounded-full px-2 py-0.5">재구매</button>' +
                    "</div>" : "");
        }).join("");

        notifyCritical(critical);

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">📦</span>재고 소진 추적기' +
            '<span class="text-[10px] font-bold text-gray-400">사료·용품 잔량</span></h3>' +
            '<button onclick="InventoryTracker.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">품목 추가</button>' +
            "</div>" +
            (list.length ? '<div>' + rows + "</div>"
                : '<p class="text-[11px] text-gray-300 py-4 text-center">등록된 재고가 없어요. 사료·용품을 추가하면 소진 예상일을 알려드려요.</p>') +
            "</div>";
    }

    // ── 등록 모달 ──────────────────────────────────────────────
    function open() {
        close();
        var overlay = document.createElement("div");
        overlay.id = "inventory-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">📦 재고 품목 추가</h3>' +
            '<button onclick="InventoryTracker.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3">' +
            '<div><label class="text-xs font-bold text-gray-500">품목명</label>' +
            '<input id="inv-name" type="text" maxlength="24" placeholder="예: 연어 사료 2kg" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div class="flex gap-2">' +
            '<div class="flex-1"><label class="text-xs font-bold text-gray-500">총 용량</label>' +
            '<input id="inv-total" type="number" min="0" step="1" placeholder="예: 2000" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div class="w-24"><label class="text-xs font-bold text-gray-500">단위</label>' +
            '<input id="inv-unit" type="text" maxlength="4" value="g" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div></div>' +
            '<div><label class="text-xs font-bold text-gray-500">일 급여·사용량</label>' +
            '<input id="inv-daily" type="number" min="0" step="1" placeholder="예: 80" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">개봉일</label>' +
            '<input id="inv-date" type="date" value="' + today() + '" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2">' +
            '<button onclick="InventoryTracker.submit()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function submit() {
        if (!document.getElementById("inventory-overlay")) return;
        var name = ((document.getElementById("inv-name") || {}).value || "").trim();
        var total = parseFloat((document.getElementById("inv-total") || {}).value);
        var daily = parseFloat((document.getElementById("inv-daily") || {}).value);
        var unit = ((document.getElementById("inv-unit") || {}).value || "").trim().slice(0, 4);
        var openDate = (document.getElementById("inv-date") || {}).value || today();

        if (!name) { toast("품목명을 입력해 주세요"); return; }
        if (!isFinite(total) || total <= 0) { toast("올바른 총 용량을 입력해 주세요"); return; }
        if (!isFinite(daily) || daily <= 0) { toast("올바른 일 급여·사용량을 입력해 주세요"); return; }

        var list = loadAll();
        list.push({
            id: "inv_" + Date.now() + "_" + Math.floor(Math.random() * 1000),
            name: name.slice(0, 24), unit: unit, total: total, daily: daily, openDate: openDate
        });
        saveAll(list);
        close();
        toast("재고 품목을 추가했어요 📦");
        renderWidget("inventory-tracker-widget");
    }

    function remove(id) {
        var list = loadAll().filter(function (it) { return it.id !== id; });
        saveAll(list);
        renderWidget("inventory-tracker-widget");
    }

    function close() { var el = document.getElementById("inventory-overlay"); if (el) el.remove(); }

    window.InventoryTracker = { open: open, submit: submit, close: close, remove: remove, renderWidget: renderWidget, goShop: goShop };
})();
