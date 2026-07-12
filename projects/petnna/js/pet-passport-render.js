// pet-passport-render.js — 🪪 응급·여행 프로필 카드(Pet Passport) 데이터 취합/카드 HTML
// ─────────────────────────────────────────────────────────────
// 마이펫 기본정보 + 건강수첩(접종·최근 진료) + 알러지/복용약 + 보호자 연락처를
// 모달용 카드 HTML로 조립하고, 오프라인 스캔용 QR 텍스트를 만든다. 모달 열기/
// 인쇄 등 화면 동작은 js/pet-passport.js(PetPassport, 이 모듈을 사용)에서 처리한다.
// 저장/스키마 변경 없음 — 기존 로컬 데이터만 읽어 렌더한다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    function ownerContact() {
        return (typeof settings_email !== "undefined" && settings_email)
            || localStorage.getItem("petna_user_email") || "";
    }

    // 활성 펫의 접종 이력(건강수첩 category=vaccine) — 최신순
    function vaccineRecords(pet) {
        var list = Array.isArray(window.medicalRecords) ? window.medicalRecords : [];
        return list
            .filter(function (r) { return r.category === "vaccine" && (!pet || String(r.petId) === String(pet.id)); })
            .sort(function (a, b) { return (b.visitDate || "").localeCompare(a.visitDate || ""); });
    }

    // 활성 펫의 최근 진료 이력(접종 제외) — 최신순 상위 3건
    function recentVisits(pet) {
        var list = Array.isArray(window.medicalRecords) ? window.medicalRecords : [];
        return list
            .filter(function (r) { return r.category !== "vaccine" && (!pet || String(r.petId) === String(pet.id)); })
            .sort(function (a, b) { return (b.visitDate || "").localeCompare(a.visitDate || ""); })
            .slice(0, 3);
    }

    // ── QR: 서버 없이 스캔만으로 읽히도록 요약 텍스트를 직접 인코딩 ──
    function passportText(pet) {
        var vs = vaccineRecords(pet).slice(0, 4).map(function (v) {
            return "  - " + (v.diagnosis || v.hospital || "접종") + " (" + (v.visitDate || "") + ")";
        });
        var lines = [
            "[펫과나 Pet Passport]",
            "이름: " + (pet.name || "-"),
            "종/품종: " + [pet.type, pet.breed].filter(Boolean).join(" / "),
            "나이: " + (pet.age != null ? pet.age : "-") + " · 성별: " + (pet.gender || "-") + " · 체중: " + (pet.weight != null ? pet.weight + "kg" : "-"),
        ];
        if (pet.allergies) lines.push("알러지: " + pet.allergies);
        if (pet.meds) lines.push("복용약: " + pet.meds);
        if (vs.length) { lines.push("접종:"); lines = lines.concat(vs); }
        var contact = ownerContact();
        if (contact) lines.push("보호자 연락: " + contact);
        return lines.join("\n");
    }

    function renderQrInto(el, text) {
        el.innerHTML = "";
        if (typeof window.qrcode !== "function") {
            el.innerHTML = '<p class="text-xs text-gray-400">QR 라이브러리 로딩 중…</p>'; return;
        }
        try {
            // 텍스트가 길 수 있어 용량 여유가 큰 ECC level L 사용, typeNumber 0=자동
            var qr = window.qrcode(0, "L");
            qr.addData(text);
            qr.make();
            el.innerHTML = qr.createSvgTag(4, 2);
            var svg = el.querySelector("svg");
            if (svg) { svg.setAttribute("width", "100%"); svg.setAttribute("height", "100%");
                svg.style.maxWidth = "200px"; svg.style.height = "auto"; }
        } catch (e) {
            el.innerHTML = '<p class="text-xs text-rose-400">QR 생성 실패(정보가 너무 많아요)</p>';
        }
    }

    // ── 카드 본문 HTML ─────────────────────────────────────────
    function infoRow(label, value) {
        if (value == null || value === "") return "";
        return '<div class="flex flex-col"><span class="text-[10px] font-bold text-gray-400">' + esc(label) +
            '</span><span class="text-sm font-black text-gray-800 break-words">' + esc(value) + "</span></div>";
    }

    function cardBody(pet) {
        var photo = (pet.imageUrl) ? pet.imageUrl : "";
        var basics =
            '<div class="grid grid-cols-2 gap-x-3 gap-y-2 rounded-2xl bg-gray-50 px-4 py-3">' +
            infoRow("종/품종", [pet.type, pet.breed].filter(Boolean).join(" / ")) +
            infoRow("나이", pet.age != null && pet.age !== "" ? pet.age : "-") +
            infoRow("성별", pet.gender) +
            infoRow("체중", pet.weight != null && pet.weight !== "" ? pet.weight + " kg" : "-") +
            (pet.personality ? '<div class="col-span-2">' + infoRow("성격·특징", pet.personality) + "</div>" : "") +
            "</div>";

        var alerts = "";
        if (pet.allergies || pet.meds) {
            alerts =
                '<div class="rounded-2xl bg-rose-50 border border-rose-100 px-4 py-3 space-y-1.5">' +
                '<p class="text-[11px] font-black text-rose-600">⚠️ 응급 주의</p>' +
                (pet.allergies ? '<p class="text-xs text-gray-700"><b>알러지</b> · ' + esc(pet.allergies) + "</p>" : "") +
                (pet.meds ? '<p class="text-xs text-gray-700"><b>복용약</b> · ' + esc(pet.meds) + "</p>" : "") +
                "</div>";
        }

        var vs = vaccineRecords(pet);
        var vaccineHtml = "";
        if (vs.length) {
            var items = vs.slice(0, 5).map(function (v) {
                return '<span class="inline-flex items-center gap-1 bg-white text-emerald-700 border border-emerald-200/70 text-[10px] font-bold px-2 py-0.5 rounded-full">💉 ' +
                    esc(v.diagnosis || v.hospital || "접종") + " · " + esc(v.visitDate || "") + "</span>";
            }).join("");
            vaccineHtml =
                '<div class="rounded-2xl bg-emerald-50/60 border border-emerald-100 px-4 py-3">' +
                '<p class="text-[11px] font-black text-emerald-700 mb-1.5">💉 접종 이력 · ' + vs.length + "건</p>" +
                '<div class="flex flex-wrap gap-1.5">' + items + "</div></div>";
        }

        var visits = recentVisits(pet);
        var visitHtml = "";
        if (visits.length) {
            var rows = visits.map(function (r) {
                return '<div class="flex items-start gap-2 py-1 border-b border-gray-100 last:border-0">' +
                    '<span class="text-[11px] font-bold text-gray-400 tabular-nums shrink-0">' + esc(r.visitDate || "") + "</span>" +
                    '<span class="text-xs text-gray-700 break-words">' + esc(r.diagnosis || r.hospital || "진료") + "</span></div>";
            }).join("");
            visitHtml =
                '<div class="rounded-2xl bg-gray-50 px-4 py-3">' +
                '<p class="text-[11px] font-black text-gray-500 mb-1">🏥 최근 진료</p>' + rows + "</div>";
        }

        var contact = ownerContact();
        var contactHtml =
            '<div class="rounded-2xl bg-brand-50 border border-brand-100 px-4 py-3 text-center">' +
            '<p class="text-[11px] font-black text-brand-600 mb-0.5">보호자 연락처</p>' +
            '<p class="text-sm font-black text-gray-800 break-words">' + (contact ? esc(contact) : "미등록 — 설정에서 이메일을 등록해 주세요") + "</p></div>";

        return '<div class="space-y-3">' +
            '<div class="flex items-center gap-3">' +
            (photo
                ? '<img src="' + esc(photo) + '" alt="" class="w-16 h-16 rounded-2xl object-cover border border-gray-200">'
                : '<div class="w-16 h-16 rounded-2xl bg-brand-50 flex items-center justify-center text-3xl">🐶</div>') +
            '<div><h2 class="text-lg font-black text-gray-900">' + esc(pet.name || "우리 아이") + "</h2>" +
            '<p class="text-[11px] text-gray-400">펫과나 응급·여행 프로필</p></div></div>' +
            basics + alerts + vaccineHtml + visitHtml + contactHtml +
            '<div class="flex flex-col items-center gap-2 pt-2 border-t border-gray-100">' +
            '<div id="pass-qr" class="bg-white p-2 rounded-xl border border-gray-100 flex items-center justify-center" style="min-height:120px"></div>' +
            '<p class="text-[10px] text-gray-400 text-center">QR을 스캔하면 위 정보 요약이 오프라인으로 열려요</p></div>' +
            "</div>";
    }

    window.PetPassportRender = {
        esc: esc,
        ownerContact: ownerContact,
        vaccineRecords: vaccineRecords,
        recentVisits: recentVisits,
        passportText: passportText,
        renderQrInto: renderQrInto,
        cardBody: cardBody,
    };
})();
