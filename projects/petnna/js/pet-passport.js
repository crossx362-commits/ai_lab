// pet-passport.js — 🪪 반려동물 응급·여행 프로필 카드 (Pet Passport) 모달/인쇄
// ─────────────────────────────────────────────────────────────
// 카드 HTML 조립과 QR 텍스트 생성은 js/pet-passport-render.js(PetPassportRender)를
// 사용한다. 이 파일은 모달 열기/닫기와 병원·펫시터용 인쇄 창만 담당한다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }

    function activePet() {
        if (typeof window.getActivePet === "function") return window.getActivePet();
        return (typeof pets !== "undefined" && pets && pets[0]) || null;
    }

    function render() { return window.PetPassportRender; }

    // ── 모달 ───────────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet || !render()) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        if (typeof window.loadMedicalRecords === "function" &&
            !(Array.isArray(window.medicalRecords) && window.medicalRecords.length)) {
            try { window.loadMedicalRecords(); } catch (e) { /* ignore */ }
        }

        var overlay = document.createElement("div");
        overlay.id = "pass-modal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🪪 응급·여행 프로필</h3>' +
            '<button onclick="PetPassport.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4">' + render().cardBody(pet) + "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 justify-end sticky bottom-0 bg-white">' +
            '<button onclick="PetPassport.print()" class="rounded-xl bg-brand-500 hover:bg-brand-600 text-white px-4 py-2 text-sm font-bold">인쇄</button>' +
            '<button onclick="PetPassport.close()" class="rounded-xl border border-gray-200 hover:bg-gray-50 text-gray-600 px-4 py-2 text-sm font-bold">닫기</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        var qel = document.getElementById("pass-qr");
        if (qel) render().renderQrInto(qel, render().passportText(pet));
    }

    function close() { var el = document.getElementById("pass-modal-overlay"); if (el) el.remove(); }

    // 병원/펫시터용 인쇄 — 부모 창에서 QR SVG 생성해 주입
    function print() {
        var pet = activePet();
        if (!pet || !render()) return;
        var esc = render().esc;
        var qrHtml = '<p style="font-size:12px;color:#999">QR 생성 실패</p>';
        if (typeof window.qrcode === "function") {
            try {
                var qr = window.qrcode(0, "L");
                qr.addData(render().passportText(pet)); qr.make();
                qrHtml = qr.createSvgTag(5, 4);
            } catch (e) { /* keep fallback */ }
        }
        var vs = render().vaccineRecords(pet).slice(0, 6).map(function (v) {
            return "<li>💉 " + esc(v.diagnosis || v.hospital || "접종") + " · " + esc(v.visitDate || "") + "</li>";
        }).join("");
        var visits = render().recentVisits(pet).map(function (r) {
            return "<li>🏥 " + esc(r.visitDate || "") + " · " + esc(r.diagnosis || r.hospital || "진료") + "</li>";
        }).join("");
        var contact = render().ownerContact();

        var w = window.open("", "_blank", "width=460,height=680");
        if (!w) { toast("팝업이 차단됐어요 — 팝업을 허용해 주세요"); return; }
        w.document.write(
            '<!doctype html><html lang="ko"><head><meta charset="utf-8">' +
            "<title>Pet Passport — " + esc(pet.name || "") + "</title>" +
            "<style>@media print{@page{margin:12mm}}" +
            'body{font-family:-apple-system,"Apple SD Gothic Neo","Malgun Gothic",sans-serif;margin:0;padding:24px;color:#1f2937}' +
            ".card{max-width:380px;margin:0 auto;border:2px solid #6366f1;border-radius:18px;padding:20px}" +
            ".hd{color:#6366f1;font-weight:800;font-size:15px;margin:0 0 4px}" +
            ".nm{font-weight:800;font-size:22px;margin:0 0 12px}" +
            ".grid{display:grid;grid-template-columns:1fr 1fr;gap:6px 12px;margin-bottom:12px;font-size:13px}" +
            ".grid b{color:#6b7280;font-weight:700;font-size:11px;display:block}" +
            ".sec{margin:10px 0}.sec h4{margin:0 0 4px;font-size:12px;color:#6366f1}" +
            ".alert{background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:8px 12px;font-size:12px}" +
            "ul{margin:4px 0;padding-left:18px;font-size:12px;line-height:1.6}" +
            ".qr{width:180px;height:180px;margin:12px auto 4px}.qr svg{width:100%;height:100%}" +
            ".contact{text-align:center;background:#eef2ff;border-radius:10px;padding:8px;margin-top:10px;font-size:13px;font-weight:800}" +
            ".tip{color:#9ca3af;font-size:10px;text-align:center;margin-top:6px}</style></head>" +
            '<body onload="window.focus();window.print()"><div class="card">' +
            '<p class="hd">🪪 펫과나 응급·여행 프로필 (Pet Passport)</p>' +
            '<p class="nm">' + esc(pet.name || "우리 아이") + "</p>" +
            '<div class="grid">' +
            "<div><b>종/품종</b>" + esc([pet.type, pet.breed].filter(Boolean).join(" / ") || "-") + "</div>" +
            "<div><b>나이</b>" + esc(pet.age != null && pet.age !== "" ? pet.age : "-") + "</div>" +
            "<div><b>성별</b>" + esc(pet.gender || "-") + "</div>" +
            "<div><b>체중</b>" + esc(pet.weight != null && pet.weight !== "" ? pet.weight + " kg" : "-") + "</div>" +
            "</div>" +
            ((pet.allergies || pet.meds)
                ? '<div class="alert">⚠️ ' +
                (pet.allergies ? "<b>알러지</b> " + esc(pet.allergies) + "  " : "") +
                (pet.meds ? "<b>복용약</b> " + esc(pet.meds) : "") + "</div>"
                : "") +
            (vs ? '<div class="sec"><h4>접종 이력</h4><ul>' + vs + "</ul></div>" : "") +
            (visits ? '<div class="sec"><h4>최근 진료</h4><ul>' + visits + "</ul></div>" : "") +
            '<div class="qr">' + qrHtml + "</div>" +
            '<p class="tip">QR을 스캔하면 정보 요약이 오프라인으로 열려요</p>' +
            (contact ? '<div class="contact">📞 보호자 연락: ' + esc(contact) + "</div>" : "") +
            "</div></body></html>"
        );
        w.document.close();
    }

    window.PetPassport = { open: open, close: close, print: print };
})();
