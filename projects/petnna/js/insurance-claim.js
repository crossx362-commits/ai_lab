// 🧾 보험 청구 패키지 내보내기 — 진료기록·비용·반려동물 기본정보를 한 장의 카드로 (나무 백로그 P3)
// ─────────────────────────────────────────────────────────────
// 국내 펫보험 청구 페인포인트(진료명세·영수증·개체정보를 매번 따로 챙겨야 함)를 완화한다.
// 선택한 진료 기록을 반려동물 기본정보와 함께 한 장의 인쇄/공유용 카드로 묶어준다.
//
// 가드레일:
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. 기존 건강수첩 데이터(getMedicalRecordsForActivePet) 재사용.
//  · 인쇄는 기존 exportMedicalRecordsPDF와 동일한 window.open+print 패턴 재사용(신규 라이브러리 없음).
//  · 청구 자체는 각 보험사 절차를 따라야 하며 본 카드는 제출용 자료 묶음일 뿐(면책 명시).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var CAT = {
        visit: "🏥 진료/방문", vaccine: "💉 예방접종", checkup: "🩺 정기검진",
        surgery: "🔪 수술", medication: "💊 투약/처방", other: "📋 기타",
    };

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(m) { if (typeof showToast === "function") showToast(m); }
    function catLabel(k) { return CAT[k] || CAT.other; }
    function won(n) { return (parseFloat(n) || 0).toLocaleString("ko-KR") + "원"; }

    function activePet() { return (typeof getActivePet === "function") ? getActivePet() : null; }
    function records() {
        return (typeof getMedicalRecordsForActivePet === "function")
            ? (getMedicalRecordsForActivePet() || []) : [];
    }

    function open() {
        close();
        var recs = records();
        var pet = activePet();
        if (!recs.length) { toast("⚠️ 청구에 묶을 진료 기록이 먼저 필요해요."); return; }

        var rows = recs.map(function (r, i) {
            var cost = parseFloat(r.cost) || 0;
            return '<label class="flex items-start gap-2.5 rounded-xl border border-gray-100 px-3 py-2.5 cursor-pointer hover:bg-gray-50">' +
                '<input type="checkbox" class="ic-pick mt-0.5 accent-brand-500" data-idx="' + i + '" checked>' +
                '<span class="min-w-0 flex-1">' +
                '<span class="block text-sm font-bold text-gray-900 truncate">' + esc(r.visitDate || "-") + " · " + esc(catLabel(r.category)) + "</span>" +
                '<span class="block text-[11px] text-gray-500 truncate">' + esc(r.hospital || "병원 미기재") + " · " + esc(r.diagnosis || "진단 미기재") + "</span></span>" +
                '<span class="shrink-0 text-sm font-bold text-brand-600">' + (cost > 0 ? won(cost) : "-") + "</span></label>";
        }).join("");

        var overlay = document.createElement("div");
        overlay.id = "ic-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white z-10">' +
            '<h3 class="text-base font-extrabold text-gray-900">🧾 보험 청구 패키지</h3>' +
            '<button onclick="InsuranceClaim.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3">' +
            '<div class="rounded-xl bg-brand-50 px-3 py-2.5 text-xs text-brand-700 leading-relaxed">' +
            esc((pet && pet.name) || "우리 아이") + '의 진료 기록을 골라 <b>기본정보 + 진료명세</b>를 한 장으로 묶어요. 보험사 청구 시 제출 자료로 쓰세요.</div>' +
            '<p class="text-xs font-bold text-gray-500">묶을 진료 선택</p>' +
            '<div class="space-y-1.5">' + rows + "</div>" +
            '<div class="flex gap-2 pt-1">' +
            '<button onclick="InsuranceClaim.copySummary()" class="flex-1 rounded-xl border border-brand-200 hover:bg-brand-50 text-brand-600 py-2.5 text-sm font-bold">📋 요약 복사</button>' +
            '<button onclick="InsuranceClaim.build()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">🧾 청구 카드 만들기</button></div>' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 본 카드는 청구용 자료를 한 장으로 모아줄 뿐이며, 실제 청구·심사는 각 보험사 절차와 진료 명세서·영수증 원본을 따라야 해요.</p>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function close() { var el = document.getElementById("ic-overlay"); if (el) el.remove(); }

    function selected() {
        var recs = records();
        var picks = [];
        document.querySelectorAll(".ic-pick:checked").forEach(function (cb) {
            var r = recs[parseInt(cb.getAttribute("data-idx"), 10)];
            if (r) picks.push(r);
        });
        return picks;
    }

    function petLine(pet) {
        if (!pet) return "-";
        return [pet.type === "cat" ? "고양이" : pet.type === "dog" ? "강아지" : pet.type, pet.breed]
            .filter(Boolean).join(" / ") || "-";
    }

    function copySummary() {
        var picks = selected();
        if (!picks.length) { toast("묶을 진료를 하나 이상 선택해 주세요"); return; }
        var pet = activePet();
        var total = picks.reduce(function (s, r) { return s + (parseFloat(r.cost) || 0); }, 0);
        var lines = [];
        lines.push("[보험 청구 자료] " + ((pet && pet.name) || "우리 아이"));
        lines.push("· 기본정보: " + petLine(pet) +
            (pet && pet.gender ? " · " + pet.gender : "") + (pet && pet.age ? " · " + pet.age : ""));
        lines.push("· 진료 " + picks.length + "건 / 합계 " + won(total));
        picks.forEach(function (r) {
            lines.push("  - " + (r.visitDate || "-") + " " + catLabel(r.category) +
                " / " + (r.hospital || "병원 미기재") + " / " + (r.diagnosis || "진단 미기재") +
                " / " + ((parseFloat(r.cost) || 0) > 0 ? won(r.cost) : "-"));
        });
        var text = lines.join("\n");
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () { toast("청구 요약을 복사했어요 📋"); },
                function () { toast("복사에 실패했어요"); });
        } else {
            toast("이 브라우저에서는 복사를 지원하지 않아요");
        }
    }

    function build() {
        var picks = selected();
        if (!picks.length) { toast("묶을 진료를 하나 이상 선택해 주세요"); return; }
        var pet = activePet() || {};
        var total = picks.reduce(function (s, r) { return s + (parseFloat(r.cost) || 0); }, 0);
        var today = new Date().toISOString().slice(0, 10);

        var rows = picks.map(function (r) {
            var cost = parseFloat(r.cost) || 0;
            return "<tr>" +
                "<td>" + esc(r.visitDate || "-") + "</td>" +
                "<td>" + esc(catLabel(r.category)) + "</td>" +
                "<td>" + esc(r.hospital || "-") + "</td>" +
                "<td>" + esc(r.diagnosis || "-") + "</td>" +
                '<td class="num">' + (cost > 0 ? cost.toLocaleString("ko-KR") + "원" : "-") + "</td>" +
                "</tr>";
        }).join("");

        var w = window.open("", "_blank", "width=720,height=900");
        if (!w) { toast("팝업이 차단됐어요 — 팝업을 허용해 주세요"); return; }
        w.document.write(
            '<!doctype html><html lang="ko"><head><meta charset="utf-8">' +
            "<title>보험 청구 패키지 — " + esc(pet.name || "") + "</title>" +
            "<style>@media print{@page{margin:14mm}}" +
            'body{font-family:-apple-system,"Apple SD Gothic Neo","Malgun Gothic",sans-serif;margin:0;padding:28px;color:#1f2937}' +
            ".hd{color:#6366f1;font-weight:800;font-size:16px;margin:0 0 2px}" +
            ".nm{font-weight:800;font-size:24px;margin:0 0 2px}" +
            ".sub{color:#9ca3af;font-size:12px;margin:0 0 16px}" +
            ".grid{display:grid;grid-template-columns:1fr 1fr;gap:6px 12px;margin-bottom:16px;font-size:13px}" +
            ".grid b{color:#6b7280;font-weight:700;font-size:11px;display:block}" +
            ".cards{display:flex;gap:10px;margin-bottom:16px}" +
            ".stat{flex:1;border:1px solid #e0e7ff;background:#eef2ff;border-radius:12px;padding:10px 12px}" +
            ".stat b{color:#6366f1;font-weight:700;font-size:11px;display:block}" +
            ".stat .v{font-weight:800;font-size:18px}" +
            "h4{margin:0 0 6px;font-size:13px;color:#6366f1}" +
            "table{width:100%;border-collapse:collapse;font-size:12px}" +
            "th,td{text-align:left;padding:7px 8px;border-bottom:1px solid #f0f0f3}" +
            "th{color:#6b7280;font-size:11px;background:#fafafa}" +
            "td.num,th.num{text-align:right;white-space:nowrap}" +
            ".foot{color:#9ca3af;font-size:10px;text-align:center;margin-top:18px;line-height:1.6}</style></head>" +
            '<body onload="window.focus();window.print()">' +
            '<p class="hd">🧾 펫과나 보험 청구 패키지</p>' +
            '<p class="nm">' + esc(pet.name || "우리 아이") + "</p>" +
            '<p class="sub">발급일 ' + today + "</p>" +
            '<div class="grid">' +
            "<div><b>종/품종</b>" + esc(petLine(pet)) + "</div>" +
            "<div><b>나이</b>" + esc(pet.age != null && pet.age !== "" ? pet.age : "-") + "</div>" +
            "<div><b>성별</b>" + esc(pet.gender || "-") + "</div>" +
            "<div><b>체중</b>" + esc(pet.weight != null && pet.weight !== "" ? pet.weight + " kg" : "-") + "</div>" +
            "</div>" +
            '<div class="cards">' +
            '<div class="stat"><b>청구 대상 합계</b><span class="v">' + total.toLocaleString("ko-KR") + "원</span></div>" +
            '<div class="stat"><b>진료 건수</b><span class="v">' + picks.length + "건</span></div>" +
            "</div>" +
            "<h4>🏥 진료 명세</h4>" +
            "<table><thead><tr><th>방문일</th><th>구분</th><th>병원</th><th>진단/처방</th><th class=\"num\">진료비</th></tr></thead>" +
            "<tbody>" + rows + "</tbody></table>" +
            '<p class="foot">본 자료는 펫과나 앱에서 자동 생성한 청구 참고용 묶음입니다.<br>실제 청구는 각 보험사 절차에 따라 진료 명세서·영수증 원본을 제출하세요.</p>' +
            "</body></html>"
        );
        w.document.close();
        close();
    }

    window.InsuranceClaim = { open: open, close: close, build: build, copySummary: copySummary };
})();
