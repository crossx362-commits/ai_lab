// handover-sheet.js — 🏨 펫시터·호텔 인수인계 시트 (나무 백로그 P3)
// ─────────────────────────────────────────────────────────────
// 위탁(펫시터·호텔) 시 필요한 정보를 한 장으로 모아 인쇄/QR로 전달한다.
//   · 급여 일정(care-scheduler type=feed) — 시간·내용·메모(급여량)
//   · 투약 일정(type=medicine) — 시간·내용·메모 + 남은 약(리필)
//   · 그 밖의 돌봄 루틴(산책·미용·놀이 등)
//   · 특이사항 — 알러지·복용약·만성질환·혈액형·성격(응급 프로필과 동일 소스)
//   · 병원 연락처 — 기존 지도 데이터(PETLIFE_REAL_LOCATIONS)의 24h/야간 병원
//   · 보호자 연락처 + QR(요약 오프라인 열람)
//
// 가드레일: 프론트 전용, 스키마/DB 무접촉. 기존 모듈만 읽어 조립한다.
//   - QR/esc/보호자·응급정보: PetPassportRender 재사용
//   - 케어 데이터: care-scheduler.js 전역 함수(getCareTypeName/Icon, getMedicationRefillInfo)
//   - 인쇄창은 pet-passport.js 패턴을 따른다(부모 창에서 QR SVG 생성 후 주입).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }
    function render() { return window.PetPassportRender; }

    function esc(s) {
        if (render() && render().esc) return render().esc(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    function activePet() {
        if (typeof window.getActivePet === "function") return window.getActivePet();
        return (typeof pets !== "undefined" && pets && pets[0]) || null;
    }

    var REPEAT_LABEL = { daily: "매일", weekly: "매주", monthly: "매월", once: "1회" };
    var DOW = ["일", "월", "화", "수", "목", "금", "토"];

    function repeatText(s) {
        if (s.repeat === "weekly" && Array.isArray(s.repeatDays) && s.repeatDays.length && s.repeatDays.length < 7) {
            return "매주 " + s.repeatDays.slice().sort().map(function (d) { return DOW[d]; }).join("·");
        }
        return REPEAT_LABEL[s.repeat] || "매일";
    }

    // 활성 펫의 케어 일정을 종류별로 분류(오늘만이 아니라 전체 루틴 — 위탁 기간 안내용)
    function petSchedules(pet) {
        var cs = (typeof AppStore !== "undefined" && AppStore.getState("careSchedules")) || { schedules: [] };
        var list = (cs.schedules || []).filter(function (s) {
            return pet == null || s.petId == null || String(s.petId) === String(pet.id);
        }).sort(function (a, b) { return String(a.time || "").localeCompare(String(b.time || "")); });
        return {
            feed: list.filter(function (s) { return s.type === "feed" || s.type === "water"; }),
            medicine: list.filter(function (s) { return s.type === "medicine"; }),
            other: list.filter(function (s) { return ["feed", "water", "medicine"].indexOf(s.type) === -1; }),
        };
    }

    // 24h/야간 동물병원(emergency-card.js와 동일 필터 — 위탁 시 급할 때 전화용)
    function emergencyHospitals() {
        var arr = (typeof PETLIFE_REAL_LOCATIONS !== "undefined" && Array.isArray(PETLIFE_REAL_LOCATIONS))
            ? PETLIFE_REAL_LOCATIONS : [];
        return arr.filter(function (loc) {
            if (loc.category !== "hospital") return false;
            var hay = String(loc.hours || "") + " " + (Array.isArray(loc.tags) ? loc.tags.join(" ") : "");
            return /24|야간|응급/.test(hay);
        }).slice(0, 4);
    }

    // 특이사항 목록 [{label, value}]
    function specialNotes(pet) {
        var em = (render() && render().getEmergency) ? render().getEmergency(pet) : {};
        var out = [];
        if (pet.allergies) out.push({ label: "알러지", value: pet.allergies });
        if (pet.meds) out.push({ label: "복용약", value: pet.meds });
        if (em.chronic) out.push({ label: "만성질환", value: em.chronic });
        if (em.blood) out.push({ label: "혈액형", value: em.blood });
        if (pet.personality) out.push({ label: "성격·특징", value: pet.personality });
        return out;
    }

    // 남은 약 정보(리필) 텍스트 — care-scheduler.js 재사용
    function refillText(s) {
        if (typeof window.getMedicationRefillInfo !== "function") return "";
        var info = window.getMedicationRefillInfo(s);
        if (!info) return "";
        if (info.remainingDays <= 0) return "약 소진";
        return "약 " + info.remainingDays + "일분 남음";
    }

    // ── QR: 서버 없이 스캔만으로 읽히는 요약 텍스트 ──
    function summaryText(pet) {
        var g = petSchedules(pet);
        var lines = ["[펫과나 인수인계]", "이름: " + (pet.name || "우리 아이")];
        var basics = [pet.type, pet.breed].filter(Boolean).join("/");
        if (basics) lines.push("종/품종: " + basics);
        if (pet.weight != null && pet.weight !== "") lines.push("체중: " + pet.weight + "kg");
        function seg(title, arr) {
            if (!arr.length) return;
            lines.push(title + ":");
            arr.forEach(function (s) {
                lines.push("  - " + (s.time ? s.time + " " : "") + (s.title || "") + (s.notes ? " (" + s.notes + ")" : ""));
            });
        }
        seg("급여", g.feed);
        seg("투약", g.medicine);
        specialNotes(pet).forEach(function (n) { lines.push(n.label + ": " + n.value); });
        var contact = (render() && render().ownerContact) ? render().ownerContact() : "";
        if (contact) lines.push("보호자: " + contact);
        return lines.join("\n");
    }

    // ── 화면용 섹션 HTML ─────────────────────────────────────────
    function schedRows(arr) {
        return arr.map(function (s) {
            var name = (typeof window.getCareTypeName === "function") ? window.getCareTypeName(s.type) : s.type;
            var icon = (typeof window.getCareTypeIcon === "function") ? window.getCareTypeIcon(s.type) : "📋";
            var meta = [repeatText(s)];
            if (s.type === "medicine") { var r = refillText(s); if (r) meta.push(r); }
            return '<div class="flex items-start gap-2 py-1.5 border-b border-gray-100 last:border-0">' +
                '<span class="text-base shrink-0">' + icon + "</span>" +
                '<span class="text-xs font-bold text-gray-400 tabular-nums shrink-0 w-10">' + esc(s.time || "-") + "</span>" +
                '<div class="min-w-0 flex-1"><p class="text-xs font-bold text-gray-800 break-words">' + esc(s.title || name) + "</p>" +
                (s.notes ? '<p class="text-[10px] text-gray-500 break-words">' + esc(s.notes) + "</p>" : "") +
                '<p class="text-[10px] text-gray-400">' + esc(meta.join(" · ")) + "</p></div></div>";
        }).join("");
    }

    function section(title, emoji, arr, empty) {
        var body = arr.length ? schedRows(arr) : '<p class="text-[11px] text-gray-400 py-1">' + esc(empty) + "</p>";
        return '<div class="rounded-2xl bg-gray-50 px-4 py-3">' +
            '<p class="text-[11px] font-black text-gray-500 mb-1">' + emoji + " " + esc(title) + "</p>" + body + "</div>";
    }

    function cardBody(pet) {
        var g = petSchedules(pet);
        var notes = specialNotes(pet);
        var notesHtml = notes.length
            ? '<div class="rounded-2xl bg-rose-50 border border-rose-100 px-4 py-3 space-y-1">' +
              '<p class="text-[11px] font-black text-rose-600">⚠️ 특이사항</p>' +
              notes.map(function (n) {
                  return '<p class="text-xs text-gray-700"><b>' + esc(n.label) + "</b> · " + esc(n.value) + "</p>";
              }).join("") + "</div>"
            : "";

        var hs = emergencyHospitals();
        var hospHtml = "";
        if (hs.length) {
            var rows = hs.map(function (h) {
                var tel = String(h.phone || "").replace(/[^0-9+]/g, "");
                return '<div class="flex items-center justify-between gap-2 py-1.5 border-b border-gray-100 last:border-0">' +
                    '<div class="min-w-0"><p class="text-xs font-bold text-gray-800 truncate">' + esc(h.name) + "</p>" +
                    '<p class="text-[10px] text-gray-500 truncate">' + esc(h.hours || "") + (h.phone ? " · " + esc(h.phone) : "") + "</p></div>" +
                    (tel ? '<a href="tel:' + esc(tel) + '" class="rounded-lg bg-rose-500 hover:bg-rose-600 text-white px-2.5 py-1 text-[11px] font-bold shrink-0">📞</a>' : "") +
                    "</div>";
            }).join("");
            hospHtml = '<div class="rounded-2xl bg-amber-50 border border-amber-100 px-4 py-3">' +
                '<p class="text-[11px] font-black text-amber-700 mb-1">🏥 병원 연락처 (24h·야간)</p>' + rows + "</div>";
        }

        var contact = (render() && render().ownerContact) ? render().ownerContact() : "";
        var contactHtml = '<div class="rounded-2xl bg-brand-50 border border-brand-100 px-4 py-3 text-center">' +
            '<p class="text-[11px] font-black text-brand-600 mb-0.5">보호자 연락처</p>' +
            '<p class="text-sm font-black text-gray-800 break-words">' +
            (contact ? esc(contact) : "미등록 — 설정에서 이메일을 등록해 주세요") + "</p></div>";

        var photo = pet.imageUrl || "";
        return '<div class="space-y-3">' +
            '<div class="flex items-center gap-3">' +
            (photo
                ? '<img src="' + esc(photo) + '" alt="" class="w-16 h-16 rounded-2xl object-cover border border-gray-200">'
                : '<div class="w-16 h-16 rounded-2xl bg-brand-50 flex items-center justify-center text-3xl">🐶</div>') +
            '<div><h2 class="text-lg font-black text-gray-900">' + esc(pet.name || "우리 아이") + "</h2>" +
            '<p class="text-[11px] text-gray-400">' +
            esc([pet.type, pet.breed].filter(Boolean).join(" / ") || "펫시터·호텔 인수인계") +
            (pet.weight != null && pet.weight !== "" ? " · " + esc(pet.weight) + "kg" : "") + "</p></div></div>" +
            section("급여 일정", "🍚", g.feed, "등록된 급여 일정이 없어요 — 케어 스케줄러에서 추가하세요") +
            section("투약 일정", "💊", g.medicine, "등록된 투약 일정이 없어요") +
            (g.other.length ? section("그 밖의 돌봄", "🐾", g.other, "") : "") +
            notesHtml + hospHtml + contactHtml +
            '<div class="flex flex-col items-center gap-2 pt-2 border-t border-gray-100">' +
            '<div id="handover-qr" class="bg-white p-2 rounded-xl border border-gray-100 flex items-center justify-center" style="min-height:120px"></div>' +
            '<p class="text-[10px] text-gray-400 text-center">QR을 스캔하면 급여·투약·특이사항 요약이 오프라인으로 열려요</p></div>' +
            "</div>";
    }

    // ── 모달 ───────────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        close();
        var overlay = document.createElement("div");
        overlay.id = "handover-modal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🏨 펫시터·호텔 인수인계</h3>' +
            '<button onclick="HandoverSheet.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4">' + cardBody(pet) + "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 justify-end sticky bottom-0 bg-white">' +
            '<button onclick="HandoverSheet.print()" class="rounded-xl bg-brand-500 hover:bg-brand-600 text-white px-4 py-2 text-sm font-bold">인쇄</button>' +
            '<button onclick="HandoverSheet.close()" class="rounded-xl border border-gray-200 hover:bg-gray-50 text-gray-600 px-4 py-2 text-sm font-bold">닫기</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        var qel = document.getElementById("handover-qr");
        if (qel && render() && render().renderQrInto) render().renderQrInto(qel, summaryText(pet));
    }

    function close() { var el = document.getElementById("handover-modal-overlay"); if (el) el.remove(); }

    // ── 인쇄 (pet-passport.js 패턴) ──────────────────────────────
    function printList(arr) {
        return arr.map(function (s) {
            var meta = [repeatText(s)];
            if (s.type === "medicine") { var r = refillText(s); if (r) meta.push(r); }
            return "<li><b>" + esc(s.time || "-") + "</b> " + esc(s.title || "") +
                (s.notes ? " — " + esc(s.notes) : "") +
                ' <span class="mt">(' + esc(meta.join(" · ")) + ")</span></li>";
        }).join("");
    }

    function print() {
        var pet = activePet();
        if (!pet) return;
        var g = petSchedules(pet);
        var notes = specialNotes(pet);
        var hs = emergencyHospitals();
        var contact = (render() && render().ownerContact) ? render().ownerContact() : "";

        var qrHtml = '<p style="font-size:12px;color:#999">QR 생성 실패</p>';
        if (typeof window.qrcode === "function") {
            try {
                var qr = window.qrcode(0, "L");
                qr.addData(summaryText(pet)); qr.make();
                qrHtml = qr.createSvgTag(5, 4);
            } catch (e) { /* keep fallback */ }
        }

        var w = window.open("", "_blank", "width=460,height=720");
        if (!w) { toast("팝업이 차단됐어요 — 팝업을 허용해 주세요"); return; }
        w.document.write(
            '<!doctype html><html lang="ko"><head><meta charset="utf-8">' +
            "<title>인수인계 — " + esc(pet.name || "") + "</title>" +
            "<style>@media print{@page{margin:12mm}}" +
            'body{font-family:-apple-system,"Apple SD Gothic Neo","Malgun Gothic",sans-serif;margin:0;padding:24px;color:#1f2937}' +
            ".card{max-width:400px;margin:0 auto;border:2px solid #cc785c;border-radius:18px;padding:20px}" +
            ".hd{color:#cc785c;font-weight:800;font-size:15px;margin:0 0 4px}" +
            ".nm{font-weight:800;font-size:22px;margin:0 0 2px}" +
            ".sub{color:#9ca3af;font-size:12px;margin:0 0 12px}" +
            ".sec{margin:10px 0}.sec h4{margin:0 0 4px;font-size:12px;color:#cc785c}" +
            ".alert{background:#fff1f2;border:1px solid #fecdd3;border-radius:10px;padding:8px 12px;font-size:12px;line-height:1.6}" +
            "ul{margin:4px 0;padding-left:18px;font-size:12px;line-height:1.7}" +
            ".mt{color:#9ca3af;font-size:10px}" +
            ".qr{width:180px;height:180px;margin:12px auto 4px}.qr svg{width:100%;height:100%}" +
            ".contact{text-align:center;background:#fdf0ea;border-radius:10px;padding:8px;margin-top:10px;font-size:13px;font-weight:800}" +
            ".tip{color:#9ca3af;font-size:10px;text-align:center;margin-top:6px}</style></head>" +
            '<body onload="window.focus();window.print()"><div class="card">' +
            '<p class="hd">🏨 펫과나 펫시터·호텔 인수인계 시트</p>' +
            '<p class="nm">' + esc(pet.name || "우리 아이") + "</p>" +
            '<p class="sub">' + esc([pet.type, pet.breed].filter(Boolean).join(" / ") || "") +
            (pet.weight != null && pet.weight !== "" ? " · " + esc(pet.weight) + "kg" : "") + "</p>" +
            (notes.length
                ? '<div class="alert">⚠️ ' + notes.map(function (n) {
                    return "<b>" + esc(n.label) + "</b> " + esc(n.value);
                }).join(" &nbsp; ") + "</div>"
                : "") +
            '<div class="sec"><h4>🍚 급여 일정</h4><ul>' +
            (g.feed.length ? printList(g.feed) : "<li>등록된 급여 일정 없음</li>") + "</ul></div>" +
            '<div class="sec"><h4>💊 투약 일정</h4><ul>' +
            (g.medicine.length ? printList(g.medicine) : "<li>등록된 투약 일정 없음</li>") + "</ul></div>" +
            (g.other.length ? '<div class="sec"><h4>🐾 그 밖의 돌봄</h4><ul>' + printList(g.other) + "</ul></div>" : "") +
            (hs.length
                ? '<div class="sec"><h4>🏥 병원 연락처 (24h·야간)</h4><ul>' +
                  hs.map(function (h) {
                      return "<li>" + esc(h.name) + (h.phone ? " · <b>" + esc(h.phone) + "</b>" : "") +
                          (h.hours ? ' <span class="mt">(' + esc(h.hours) + ")</span>" : "") + "</li>";
                  }).join("") + "</ul></div>"
                : "") +
            '<div class="qr">' + qrHtml + "</div>" +
            '<p class="tip">QR을 스캔하면 요약이 오프라인으로 열려요</p>' +
            (contact ? '<div class="contact">📞 보호자 연락: ' + esc(contact) + "</div>" : "") +
            "</div></body></html>"
        );
        w.document.close();
    }

    window.HandoverSheet = { open: open, close: close, print: print };
})();
