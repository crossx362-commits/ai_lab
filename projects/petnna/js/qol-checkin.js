// 💛 몸무게/삶의질(QOL) 주간 체크인 — 백로그 나무_20260709_3(오너 승인 2026-07-10)
// ─────────────────────────────────────────────────────────────
// 주 1회 5문항 설문(식욕·활력·배변·편안함·행복도)으로 QOL 점수를 기록하고 추이를
// 라인차트로 보여준다. 점수가 낮으면 AI 수의사 상담으로 유도한다.
// 프론트 전용 — localStorage에만 저장(신규 DB 테이블 없음, 회의 가드와 동일 원칙).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_qol_checkins"; // { [petId]: [{date, scores:{...}, total, weight}] }
    var WEEK_MS = 6 * 24 * 3600 * 1000; // 6일 이상 지나면 다시 물어봄(주 1회, 여유 하루)
    var LOW_SCORE_THRESHOLD = 3.0; // 5점 만점 평균 기준

    var QUESTIONS = [
        { key: "appetite", label: "식욕", emoji: "🍖", desc: "밥을 잘 먹나요?" },
        { key: "energy", label: "활력", emoji: "🏃", desc: "평소만큼 활발한가요?" },
        { key: "elimination", label: "배변·배뇨", emoji: "🚽", desc: "배변·배뇨가 원활한가요?" },
        { key: "comfort", label: "편안함", emoji: "😌", desc: "아파하거나 불편해 보이지 않나요?" },
        { key: "happiness", label: "행복도", emoji: "💛", desc: "전반적으로 행복해 보이나요?" },
    ];

    var SCALE = [
        { v: 1, emoji: "😟", label: "많이 나쁨" },
        { v: 2, emoji: "🙁", label: "나쁨" },
        { v: 3, emoji: "😐", label: "보통" },
        { v: 4, emoji: "🙂", label: "좋음" },
        { v: 5, emoji: "😄", label: "아주 좋음" },
    ];

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

    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || {}; } catch (e) { return {}; } }
    function saveAll(map) { localStorage.setItem(LS_KEY, JSON.stringify(map)); }
    function history(petId) { return loadAll()[String(petId)] || []; }

    function isDue(petId) {
        var h = history(petId);
        if (!h.length) return true;
        var last = h[h.length - 1];
        return Date.now() - new Date(last.date).getTime() >= WEEK_MS;
    }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }

        var h = history(pet.id);
        var due = isDue(pet.id);
        var last = h[h.length - 1];

        var trendHtml = h.length
            ? '<canvas id="qol-trend-chart" height="90"></canvas>'
            : '<p class="text-xs text-gray-400 text-center py-3">첫 체크인을 하면 추이가 여기 쌓여요</p>';

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">💛</span>주간 QOL 체크인</h3>' +
            (due
                ? '<button onclick="QolCheckin.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">이번 주 체크인</button>'
                : '<span class="text-[11px] text-gray-400">다음 체크인까지 대기 중</span>') +
            "</div>" +
            (last
                ? '<div class="flex items-center gap-3 mb-3 text-xs text-gray-500">' +
                  '<span>최근 점수 <b class="text-brand-600 text-sm">' + last.total.toFixed(1) + '</b>/5.0</span>' +
                  '<span>' + esc(new Date(last.date).toLocaleDateString("ko-KR")) + '</span>' +
                  (last.weight ? '<span>체중 ' + esc(last.weight) + 'kg</span>' : "") +
                  "</div>"
                : "") +
            '<div id="qol-trend-wrap">' + trendHtml + "</div>" +
            '<div id="qol-nudge"></div>' +
            "</div>";

        if (h.length) renderChart(h);
        if (last && last.total < LOW_SCORE_THRESHOLD) renderNudge(last);
    }

    function renderChart(h) {
        var canvas = document.getElementById("qol-trend-chart");
        if (!canvas || typeof Chart === "undefined") return;
        if (window.qolTrendChart) window.qolTrendChart.destroy();
        var recent = h.slice(-12); // 최근 12회
        window.qolTrendChart = new Chart(canvas.getContext("2d"), {
            type: "line",
            data: {
                labels: recent.map(function (r) {
                    var d = new Date(r.date);
                    return (d.getMonth() + 1) + "/" + d.getDate();
                }),
                datasets: [{
                    label: "QOL 점수",
                    data: recent.map(function (r) { return r.total; }),
                    borderColor: "#cc785c",
                    backgroundColor: "rgba(204, 120, 92, 0.1)",
                    tension: 0.3,
                    fill: true,
                }],
            },
            options: {
                responsive: true,
                plugins: { legend: { display: false } },
                scales: { y: { min: 1, max: 5, ticks: { stepSize: 1 } } },
            },
        });
    }

    function renderNudge(last) {
        var el = document.getElementById("qol-nudge");
        if (!el) return;
        el.innerHTML =
            '<div class="mt-3 rounded-xl bg-amber-50 border border-amber-100 px-3 py-2.5 flex items-center justify-between gap-2">' +
            '<p class="text-xs text-amber-700 leading-relaxed">최근 QOL 점수가 낮게 나왔어요. 컨디션이 걱정되면 상담해보세요.</p>' +
            (typeof openVetChatModal === "function"
                ? '<button onclick="openVetChatModal()" class="shrink-0 text-xs font-bold text-white bg-amber-500 hover:bg-amber-600 px-3 py-1.5 rounded-full whitespace-nowrap">AI 수의사</button>'
                : "") +
            "</div>";
    }

    // ── 설문 모달 ──────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        close();

        var qHtml = QUESTIONS.map(function (q) {
            var scale = SCALE.map(function (s) {
                return '<button type="button" class="qol-scale-btn flex-1 flex flex-col items-center gap-0.5 py-2 rounded-xl border border-gray-200 hover:border-brand-300 hover:bg-brand-50 transition-all" data-q="' + q.key + '" data-v="' + s.v + '">' +
                    '<span class="text-lg">' + s.emoji + '</span><span class="text-[9px] text-gray-500">' + s.label + '</span></button>';
            }).join("");
            return '<div class="space-y-1.5">' +
                '<p class="text-xs font-bold text-gray-700">' + q.emoji + ' ' + esc(q.label) + ' <span class="font-normal text-gray-400">— ' + esc(q.desc) + '</span></p>' +
                '<div class="flex gap-1.5" data-question="' + q.key + '">' + scale + "</div></div>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "qol-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">💛 ' + esc(pet.name) + ' 주간 QOL 체크인</h3>' +
            '<button onclick="QolCheckin.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' + qHtml +
            '<div><label class="text-xs font-bold text-gray-500">현재 체중(선택, kg)</label>' +
            '<input id="qol-weight" type="number" step="0.1" min="0" placeholder="' + esc(pet.weight || "") + '" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 sticky bottom-0 bg-white">' +
            '<button onclick="QolCheckin.submit()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">저장</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        overlay.querySelectorAll(".qol-scale-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var group = overlay.querySelector('[data-question="' + btn.dataset.q + '"]');
                group.querySelectorAll(".qol-scale-btn").forEach(function (b) {
                    b.classList.remove("bg-brand-500", "border-brand-500");
                    b.querySelectorAll("span").forEach(function (s) { s.classList.remove("text-white"); });
                });
                btn.classList.add("bg-brand-500", "border-brand-500");
                btn.querySelectorAll("span").forEach(function (s) { s.classList.add("text-white"); });
            });
        });
    }

    function submit() {
        var pet = activePet();
        if (!pet) return;
        var overlay = document.getElementById("qol-overlay");
        if (!overlay) return;

        var scores = {};
        var missing = [];
        QUESTIONS.forEach(function (q) {
            var picked = overlay.querySelector('.qol-scale-btn.bg-brand-500[data-q="' + q.key + '"]');
            if (picked) scores[q.key] = parseInt(picked.dataset.v, 10);
            else missing.push(q.label);
        });
        if (missing.length) { toast("응답 안 한 항목이 있어요: " + missing.join(", ")); return; }

        var total = QUESTIONS.reduce(function (sum, q) { return sum + scores[q.key]; }, 0) / QUESTIONS.length;
        var weightInput = (document.getElementById("qol-weight") || {}).value;
        var weight = weightInput ? parseFloat(weightInput) : null;

        var map = loadAll();
        var key = String(pet.id);
        if (!map[key]) map[key] = [];
        map[key].push({ date: new Date().toISOString(), scores: scores, total: total, weight: weight });
        saveAll(map);

        close();
        toast("체크인 완료! QOL 점수 " + total.toFixed(1) + "/5.0 📝");
        renderWidget("qol-checkin-widget");
    }

    function close() { var el = document.getElementById("qol-overlay"); if (el) el.remove(); }

    window.QolCheckin = { open: open, submit: submit, close: close, renderWidget: renderWidget, _isDue: isDue };
})();
