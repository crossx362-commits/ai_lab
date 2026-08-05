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
    var BUDGET_KEY = "petna_expense_budgets"; // { cat: 월예산(원) }

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
    function loadBudgets() { try { return JSON.parse(localStorage.getItem(BUDGET_KEY)) || {}; } catch (e) { return {}; } }
    function saveBudgets(b) { localStorage.setItem(BUDGET_KEY, JSON.stringify(b)); }

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

    // month(YYYY-MM)에서 delta개월 이동한 YYYY-MM
    function shiftMonth(month, delta) {
        var y = parseInt(month.slice(0, 4), 10);
        var m = parseInt(month.slice(5, 7), 10) - 1 + delta;
        return new Date(y, m, 1).toISOString().slice(0, 7);
    }

    // month에서 끝나는 최근 n개월 월별 총지출 [{month,total}]
    function monthlyTotals(month, n) {
        var recs = loadAll();
        var out = [];
        for (var i = n - 1; i >= 0; i--) {
            var mm = shiftMonth(month, -i);
            var t = recs.filter(function (r) { return (r.date || "").slice(0, 7) === mm; })
                .reduce(function (a, r) { return a + (r.amount || 0); }, 0);
            out.push({ month: mm, total: t });
        }
        return out;
    }

    // 값 배열을 미니 SVG 스파크라인으로
    function sparkline(vals) {
        if (!vals.length) return "";
        var w = 120, h = 28, pad = 3;
        var max = Math.max.apply(null, vals), min = Math.min.apply(null, vals);
        var range = (max - min) || 1;
        var step = vals.length > 1 ? (w - 2 * pad) / (vals.length - 1) : 0;
        var pts = vals.map(function (v, i) {
            var x = pad + i * step;
            var y = h - pad - ((v - min) / range) * (h - 2 * pad);
            return x.toFixed(1) + "," + y.toFixed(1);
        });
        var last = pts[pts.length - 1].split(",");
        return '<svg viewBox="0 0 ' + w + " " + h + '" width="100%" height="' + h + '" preserveAspectRatio="none" aria-hidden="true">' +
            '<polyline fill="none" stroke="#f0a05a" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" points="' + pts.join(" ") + '"></polyline>' +
            '<circle cx="' + last[0] + '" cy="' + last[1] + '" r="2.5" fill="#e07b2f"></circle></svg>';
    }

    // 예산 준수·지출 추세로 0~100 지출 건강점수
    function healthScore(month) {
        var sum = summarize(month);
        var budgets = loadBudgets();
        var score = 100, budgeted = 0, over = 0;
        sum.byCat.forEach(function (c) {
            var b = budgets[c.key];
            if (b > 0) {
                budgeted++;
                if (c.sum > b) { over++; score -= Math.min(30, Math.round(((c.sum - b) / b) * 30)); }
            }
        });
        var totals = monthlyTotals(month, 2);
        if (totals[0].total > 0 && totals[1].total > totals[0].total * 1.2) score -= 10;
        return { score: Math.max(0, Math.min(100, score)), budgeted: budgeted, over: over };
    }

    var _selMonth = null;

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var month = _selMonth || currentMonth();
        var sum = summarize(month);
        var budgets = loadBudgets();

        var monthOpts = months().map(function (m) {
            return '<option value="' + m + '"' + (m === month ? " selected" : "") + ">" + esc(m) + "</option>";
        }).join("");

        var bars = sum.byCat.map(function (c) {
            var pct = sum.total > 0 ? Math.round((c.sum / sum.total) * 100) : 0;
            var b = budgets[c.key] || 0;
            var over = b > 0 && c.sum > b;
            var budgetPct = b > 0 ? Math.round((c.sum / b) * 100) : 0;
            var badge = b > 0
                ? (over
                    ? '<span class="ml-1 text-[9px] font-bold text-red-500 bg-red-50 px-1.5 py-0.5 rounded-full">예산초과 ' + budgetPct + "%</span>"
                    : '<span class="ml-1 text-[9px] font-bold text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded-full">' + budgetPct + "%</span>")
                : "";
            return '<div class="py-1.5">' +
                '<div class="flex items-center justify-between text-xs mb-1">' +
                '<span class="text-gray-700">' + c.emoji + " " + esc(c.label) + badge + "</span>" +
                '<span class="' + (over ? "text-red-500" : "text-brand-600") + ' font-bold">' + fmt(c.sum) + "원" +
                (b > 0 ? ' <span class="text-gray-300 font-normal">/ ' + fmt(b) + "</span>" : "") + "</span></div>" +
                '<div class="h-2 rounded-full bg-gray-100 overflow-hidden">' +
                '<div class="h-full rounded-full ' + (over ? "bg-red-400" : "bg-brand-400") + '" style="width:' + pct + '%"></div></div></div>';
        }).join("");

        var insight = "";
        if (sum.count || Object.keys(budgets).length) {
            var hs = healthScore(month);
            var totals = monthlyTotals(month, 6);
            var prev = totals.length >= 2 ? totals[totals.length - 2].total : 0;
            var delta = prev > 0 ? Math.round(((sum.total - prev) / prev) * 100) : 0;
            var deltaLabel = prev > 0 ? (delta > 0 ? "▲ " + delta + "%" : delta < 0 ? "▼ " + Math.abs(delta) + "%" : "─ 0%") : "—";
            var deltaColor = delta > 0 ? "text-red-500" : delta < 0 ? "text-emerald-600" : "text-gray-400";
            var scoreColor = hs.score >= 80 ? "text-emerald-600" : hs.score >= 60 ? "text-amber-500" : "text-red-500";
            insight =
                '<div class="mb-3 rounded-xl bg-gray-50 px-3 py-2.5">' +
                '<div class="flex items-center justify-between mb-1.5">' +
                '<span class="text-[11px] font-bold text-gray-500">지출 건강점수' +
                (hs.over ? ' <span class="text-red-500">· 예산초과 ' + hs.over + "건</span>" : "") + "</span>" +
                '<span class="text-sm font-extrabold ' + scoreColor + '">' + hs.score + '<span class="text-[10px] text-gray-400 font-normal">/100</span></span></div>' +
                '<div class="flex items-end justify-between gap-2">' +
                '<div class="flex-1 min-w-0">' + sparkline(totals.map(function (t) { return t.total; })) + "</div>" +
                '<div class="text-right shrink-0">' +
                '<div class="text-[10px] text-gray-400">전월 대비</div>' +
                '<div class="text-xs font-bold ' + deltaColor + '">' + deltaLabel + "</div></div></div></div>";
        }

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">💰</span>반려동물 가계부' +
            '<span class="text-[10px] font-bold text-gray-400">내 지출 기록</span></h3>' +
            '<button onclick="ExpenseTracker.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">지출 기록</button>' +
            "</div>" +
            '<div class="flex items-center justify-between mb-3">' +
            '<select onchange="ExpenseTracker.selectMonth(this.value)" class="rounded-lg border border-gray-200 px-2 py-1 text-xs focus:border-brand-400 focus:outline-none">' + monthOpts + "</select>" +
            '<div class="flex items-center gap-2">' +
            '<button onclick="ExpenseTracker.openBudget()" class="text-xs font-bold text-brand-600 hover:text-brand-700">🎯 예산 설정</button>' +
            '<span class="text-sm font-extrabold text-gray-900">합계 ' + fmt(sum.total) + "원</span></div></div>" +
            insight +
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
            '<select id="expense-cat" onchange="ExpenseTracker._toggleVetReport()" class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + catOptions + "</select></div>" +
            '<div><label class="text-xs font-bold text-gray-500">금액(원)</label>' +
            '<input id="expense-amount" type="number" min="0" step="100" placeholder="예: 30000" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">날짜</label>' +
            '<input id="expense-date" type="date" value="' + new Date().toISOString().slice(0, 10) + '" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            '<div><label class="text-xs font-bold text-gray-500">메모(선택)</label>' +
            '<input id="expense-memo" type="text" maxlength="30" placeholder="예: 정기 예방접종" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            // 병원 지출이면 시세 보드에 익명 제보할지 물어본다 — 같은 병원비를 두 폼에
            // 각각 입력하던 이중 입력 제거(2026-07-26 회의). 넘어가는 건 금액·항목뿐이고
            // 메모·날짜 등 개인 정보는 보내지 않는다(보드의 익명성 원칙 유지).
            '<div id="expense-vetreport-wrap" class="hidden rounded-xl bg-brand-50/60 border border-brand-100 px-3 py-2.5">' +
            '<label class="flex items-start gap-2 cursor-pointer">' +
            '<input id="expense-vetreport" type="checkbox" class="mt-0.5 accent-brand-500">' +
            '<span class="min-w-0"><span class="block text-xs font-bold text-brand-800">병원비 시세 보드에 익명 제보</span>' +
            '<span class="block text-[10px] text-gray-500 leading-relaxed mt-0.5">금액과 진료 항목만 보냅니다. 병원명·메모는 보내지 않아요.</span></span>' +
            '</label>' +
            '<select id="expense-vetitem" class="mt-2 w-full rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs focus:border-brand-400 focus:outline-none">' +
            vetItemOptions() + '</select></div>' +
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

        // 병원 지출 + 제보 동의 시에만, 금액·항목만 익명 보드로 넘긴다.
        var repCb = document.getElementById("expense-vetreport");
        if (cat === "medical" && repCb && repCb.checked && window.VetCostBoard && VetCostBoard.report) {
            var vItem = (document.getElementById("expense-vetitem") || {}).value;
            if (VetCostBoard.report(vItem, amount, "")) toast("시세 보드에 익명 제보했어요 🙏");
        }

        _selMonth = date.slice(0, 7); // 방금 기록한 달로 이동
        close();
        toast(catOf(cat).label + " " + fmt(amount) + "원 기록 완료! 💰");
        renderWidget("expense-tracker-widget");
    }

    function selectMonth(m) { _selMonth = m; renderWidget("expense-tracker-widget"); }

    function close() { var el = document.getElementById("expense-overlay"); if (el) el.remove(); }

    // ── 카테고리별 월 예산 설정 모달 ──────────────────────────
    function openBudget() {
        closeBudget();
        var budgets = loadBudgets();
        var rows = CATS.map(function (c) {
            var v = budgets[c.key] || "";
            return '<div class="flex items-center justify-between gap-2">' +
                '<label class="text-sm text-gray-700">' + c.emoji + " " + esc(c.label) + "</label>" +
                '<input id="budget-' + c.key + '" type="number" min="0" step="1000" value="' + v + '" placeholder="미설정" ' +
                'class="w-28 rounded-lg border border-gray-200 px-2 py-1.5 text-sm text-right focus:border-brand-400 focus:outline-none"></div>';
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "budget-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🎯 카테고리별 월 예산</h3>' +
            '<button onclick="ExpenseTracker.closeBudget()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-2.5">' + rows +
            '<p class="text-[10px] text-gray-400 pt-1">예산을 정하면 지출이 예산을 넘을 때 경고 배지와 건강점수에 반영돼요.</p></div>' +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2">' +
            '<button onclick="ExpenseTracker.submitBudget()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) closeBudget(); });
        document.body.appendChild(overlay);
    }

    function submitBudget() {
        var budgets = {};
        CATS.forEach(function (c) {
            var el = document.getElementById("budget-" + c.key);
            var v = el ? parseFloat(el.value) : NaN;
            if (isFinite(v) && v > 0) budgets[c.key] = v;
        });
        saveBudgets(budgets);
        closeBudget();
        toast("월 예산을 저장했어요 🎯");
        renderWidget("expense-tracker-widget");
    }

    function closeBudget() { var el = document.getElementById("budget-overlay"); if (el) el.remove(); }

    // 병원 카테고리일 때만 제보 옵션을 보인다
    function _toggleVetReport() {
        var cat = (document.getElementById("expense-cat") || {}).value;
        var wrap = document.getElementById("expense-vetreport-wrap");
        if (wrap) wrap.classList.toggle("hidden", cat !== "medical");
    }

    // 시세 보드의 진료 항목 목록을 그대로 쓴다(항목 정의를 두 곳에 두지 않는다)
    function vetItemOptions() {
        var items = (window.VetCostBoard && VetCostBoard.ITEMS) ? VetCostBoard.ITEMS : [];
        if (!items.length) return '<option value="">진료 항목</option>';
        return items.map(function (it) {
            return '<option value="' + esc(it.key) + '">' + esc(it.label) + "</option>";
        }).join("");
    }

    window.ExpenseTracker = { open: open, submit: submit, close: close, selectMonth: selectMonth,
                              openBudget: openBudget, submitBudget: submitBudget, closeBudget: closeBudget,
                              _toggleVetReport: _toggleVetReport, renderWidget: renderWidget };
})();
