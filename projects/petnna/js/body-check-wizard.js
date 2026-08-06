// 🔎 부위별 자가 건강점검 위저드 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// 눈·귀·치아·피부 등 부위별 증상 체크리스트로 위험도를 산출하고,
// 결과에 따라 증상 빠른 진단(SymptomTriage)·병원비 보드(VetCostBoard)로 연결한다.
//
// 가드레일(symptom-triage와 동일 계열):
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. pet 데이터는 읽기만(비파괴).
//  · 결정론적 규칙 기반(가중치 합산) — LLM 비결정성 배제.
//  · 의료 면책 문구 필수 + 불확실하면 상향(과소경고 방지).
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

    // 부위별 증상 — w: 가중치(1=경증, 2=주의, 3=적신호). 적신호가 하나라도 있으면 최소 '내원 권장'.
    var PARTS = [
        { key: "eye", emoji: "👁️", label: "눈", items: [
            { id: "eye_red",   label: "충혈·붉어짐", w: 1 },
            { id: "eye_disc",  label: "눈물·눈곱 분비물 많음", w: 1 },
            { id: "eye_rub",   label: "자주 비비거나 긁음", w: 1 },
            { id: "eye_cloud", label: "각막 혼탁·백태", w: 2 },
            { id: "eye_shut",  label: "눈을 잘 못 뜸·통증", w: 3 },
        ] },
        { key: "ear", emoji: "👂", label: "귀", items: [
            { id: "ear_smell", label: "악취가 남", w: 2 },
            { id: "ear_wax",   label: "갈색·검은 귀지 많음", w: 1 },
            { id: "ear_shake", label: "머리를 자주 흔들거나 긁음", w: 1 },
            { id: "ear_pain",  label: "귀를 만지면 아파함", w: 2 },
            { id: "ear_swell", label: "붉게 붓거나 진물", w: 3 },
        ] },
        { key: "teeth", emoji: "🦷", label: "치아·구강", items: [
            { id: "teeth_breath", label: "입냄새가 심함", w: 1 },
            { id: "teeth_tartar", label: "치석·누런 이", w: 1 },
            { id: "teeth_gum",    label: "잇몸이 붉거나 출혈", w: 3 },
            { id: "teeth_chew",   label: "씹기 힘들어함·한쪽으로만 씹음", w: 2 },
            { id: "teeth_drool",  label: "침을 많이 흘림", w: 2 },
        ] },
        { key: "skin", emoji: "🐾", label: "피부·털", items: [
            { id: "skin_itch",  label: "심하게 가려워함", w: 2 },
            { id: "skin_rash",  label: "붉은 발진·딱지", w: 2 },
            { id: "skin_loss",  label: "부분 탈모·털빠짐", w: 1 },
            { id: "skin_dand",  label: "비듬이 많음", w: 1 },
            { id: "skin_odor",  label: "피부에서 냄새가 남", w: 1 },
        ] },
    ];

    var LEVELS = {
        ok:   { label: "특이사항 없음", emoji: "✅", cls: "bg-emerald-50 text-emerald-700 border-emerald-200",
                desc: "선택한 증상이 없어요. 평소처럼 지켜봐 주세요." },
        low:  { label: "집에서 관찰", emoji: "🏠", cls: "bg-emerald-50 text-emerald-700 border-emerald-200",
                desc: "경미한 신호예요. 며칠 관찰하고 나빠지면 병원을 찾으세요." },
        mid:  { label: "내원 권장", emoji: "🏥", cls: "bg-amber-50 text-amber-700 border-amber-200",
                desc: "가까운 시일 내 동물병원 진료를 권해요." },
        high: { label: "빠른 내원", emoji: "🚨", cls: "bg-rose-50 text-rose-700 border-rose-200",
                desc: "적신호가 보여요. 되도록 빨리 진료를 받으세요." },
    };

    // 결정론적 판정: 적신호(w=3) 존재 → high, 가중치 합 기반 low/mid.
    function assess(selected) {
        var sum = 0, hasRed = false, parts = {};
        PARTS.forEach(function (p) {
            p.items.forEach(function (it) {
                if (selected.indexOf(it.id) === -1) return;
                sum += it.w;
                if (it.w >= 3) hasRed = true;
                parts[p.key] = (parts[p.key] || 0) + it.w;
            });
        });
        var level = "ok";
        if (hasRed || sum >= 5) level = "high";
        else if (sum >= 3) level = "mid";
        else if (sum >= 1) level = "low";
        return { level: level, sum: sum, parts: parts };
    }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }
        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">🔎</span>부위별 자가 건강점검</h3>' +
            '<button onclick="BodyCheckWizard.open()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">점검하기</button>' +
            "</div>" +
            '<p class="text-xs text-gray-400 leading-relaxed">눈·귀·치아·피부 증상을 체크해 위험도를 확인하고, 필요하면 증상 진단·병원비 보드로 이어가세요.</p>' +
            "</div>";
    }

    // ── 위저드 모달 ────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        close();

        var partsHtml = PARTS.map(function (p) {
            var chips = p.items.map(function (it) {
                return '<button type="button" class="bcw-chip text-xs px-3 py-1.5 rounded-full border border-gray-200 ' +
                    'text-gray-600 hover:border-brand-300 hover:bg-brand-50 transition-all" data-id="' + it.id + '">' +
                    esc(it.label) + "</button>";
            }).join("");
            return '<div class="space-y-1.5">' +
                '<p class="text-xs font-bold text-gray-700">' + p.emoji + " " + esc(p.label) + "</p>" +
                '<div class="flex flex-wrap gap-1.5">' + chips + "</div></div>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "bcw-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🔎 ' + esc(pet.name) + ' 부위별 건강점검</h3>' +
            '<button onclick="BodyCheckWizard.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' +
            '<p class="text-xs text-gray-400">해당하는 증상을 모두 선택하세요.</p>' +
            '<div id="bcw-parts" class="space-y-3.5">' + partsHtml + "</div>" +
            '<div id="bcw-result" class="hidden"></div>' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 참고용 자가점검이며 수의학적 진단을 대체하지 않아요.</p>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 sticky bottom-0 bg-white">' +
            '<button onclick="BodyCheckWizard.run()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">위험도 확인</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        overlay.querySelectorAll(".bcw-chip").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var on = btn.classList.toggle("bcw-on");
                btn.classList.toggle("bg-brand-500", on);
                btn.classList.toggle("text-white", on);
                btn.classList.toggle("border-brand-500", on);
                btn.classList.toggle("text-gray-600", !on);
            });
        });
    }

    function run() {
        var selected = [];
        document.querySelectorAll("#bcw-parts .bcw-chip.bcw-on").forEach(function (b) { selected.push(b.dataset.id); });
        var r = assess(selected);
        var meta = LEVELS[r.level];
        var box = document.getElementById("bcw-result");
        if (!box) return;
        box.classList.remove("hidden");

        var hasTriage = (typeof window.SymptomTriage !== "undefined" && window.SymptomTriage && typeof window.SymptomTriage.open === "function");
        var hasCost = (typeof window.VetCostBoard !== "undefined" && window.VetCostBoard && typeof window.VetCostBoard.open === "function");
        // 저위험 이상일 때 후속 도구로 연결(눈·귀 자가점검이 애매하면 증상 진단으로).
        var linkHtml = "";
        if (r.level !== "ok" && (hasTriage || hasCost)) {
            linkHtml = '<div class="mt-3 flex gap-2">' +
                (hasTriage ? '<button onclick="BodyCheckWizard.close(); SymptomTriage.open();" class="flex-1 rounded-lg border border-brand-200 bg-white hover:bg-brand-50 py-2 text-xs font-bold text-brand-700 transition-colors">🩺 증상 빠른 진단</button>' : "") +
                (hasCost ? '<button onclick="BodyCheckWizard.close(); VetCostBoard.open();" class="flex-1 rounded-lg border border-brand-200 bg-white hover:bg-brand-50 py-2 text-xs font-bold text-brand-700 transition-colors">💰 병원비 보드</button>' : "") +
                "</div>";
        }

        box.innerHTML =
            '<div class="rounded-xl border px-4 py-3 ' + meta.cls + '">' +
            '<div class="flex items-center gap-2 font-extrabold text-sm">' + meta.emoji + " " + esc(meta.label) + "</div>" +
            '<p class="mt-1 text-xs leading-relaxed">' + esc(meta.desc) + "</p>" +
            (r.sum ? '<p class="mt-1 text-[11px] opacity-70">점검 점수 ' + r.sum + "점</p>" : "") +
            "</div>" + linkHtml;

        box.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function close() { var el = document.getElementById("bcw-overlay"); if (el) el.remove(); }

    window.BodyCheckWizard = { open: open, run: run, close: close, renderWidget: renderWidget, _assess: assess };
})();
