// care-handoff.js — 상시 돌봄 인수인계 프로필 시트 (나무 백로그 P2)
//
// 펫시터·가족에게 넘길 "상시 케어 가이드"(급여 스케줄·투약·알러지·응급 연락처)를
// 읽기전용 링크 + QR로 생성한다. care-share.js 의 "로그인 없는 URL 인코딩 공유" 패턴을
// 재사용하되, '오늘 할 일'이 아니라 '상시 케어 정보'를 담는다.
//  · /handoff?h=<b64> 를 열면 앱 로그인 없이 인수인계 시트를 읽기전용으로 열람
//  · 손으로 적는 정보(알러지·응급연락처·메모)는 펫별 localStorage에 저장 → 다음에도 상시 유지
//  · 스키마·인증 변경 없음: 공유 데이터는 링크에만 담긴다(교차기기 = 링크 전달)
(function () {
    "use strict";

    function esc(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function nl2br(s) { return esc(s).replace(/\n/g, "<br>"); }

    // 유니코드 안전 base64 (URL-safe)
    function encode(obj) {
        var b = btoa(unescape(encodeURIComponent(JSON.stringify(obj))));
        return b.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }
    function decode(s) {
        try {
            var b = s.replace(/-/g, "+").replace(/_/g, "/");
            while (b.length % 4) b += "=";
            return JSON.parse(decodeURIComponent(escape(atob(b))));
        } catch (e) { return null; }
    }

    function toast(m) { if (typeof showToast === "function") showToast(m); }

    function activePet() {
        if (typeof window.getActivePet === "function") return window.getActivePet();
        return (typeof pets !== "undefined" && pets && pets[0]) || null;
    }

    // ── 상시 저장(펫별): 손으로 적는 알러지·응급연락처·메모 ──────────
    function storeKey(pet) { return "petna_care_handoff_" + ((pet && pet.id) || "default"); }
    function loadStored(pet) {
        try { return JSON.parse(localStorage.getItem(storeKey(pet))) || {}; }
        catch (e) { return {}; }
    }
    function saveStored(pet, data) {
        try { localStorage.setItem(storeKey(pet), JSON.stringify(data)); } catch (e) {}
    }

    // 급여/투약 스케줄을 careSchedules 에서 자동 추출(상시 반복 항목)
    function scheduleLines(types) {
        var cs = (typeof AppStore !== "undefined" && AppStore.getState("careSchedules")) || { schedules: [] };
        return (cs.schedules || [])
            .filter(function (s) { return types.indexOf(s.type) !== -1; })
            .sort(function (a, b) { return String(a.time || "").localeCompare(String(b.time || "")); })
            .map(function (s) { return (s.time ? s.time + " · " : "") + (s.title || ""); });
    }

    // 공유 페이로드 생성(모달 입력값 + 자동 추출 스케줄)
    function buildPayload(pet, stored) {
        return {
            v: 1,
            p: (pet && pet.name) || "우리 아이",
            b: (pet && pet.breed) || "",
            feed: scheduleLines(["feed", "water"]),
            med: scheduleLines(["medicine"]),
            allergy: (stored.allergy || "").trim(),
            emergency: (stored.emergency || "").trim(),
            note: (stored.note || "").trim()
        };
    }

    function shareUrl(payload) {
        return location.origin + "/handoff?h=" + encode(payload);
    }

    // ── 보호자용 생성 모달 ─────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        if (typeof showModal !== "function") { toast("잠시 후 다시 시도해 주세요"); return; }

        var stored = loadStored(pet);
        // 알러지·투약 메모는 펫 프로필 값이 있으면 최초 기본값으로 채움
        var allergy = stored.allergy != null ? stored.allergy : (pet.allergies || "");
        var emergency = stored.emergency != null ? stored.emergency : "";
        var note = stored.note != null ? stored.note : (pet.meds || "");

        var feed = scheduleLines(["feed", "water"]);
        var med = scheduleLines(["medicine"]);

        var autoBlock =
            '<div class="rounded-xl bg-gray-50 px-3 py-2 text-xs text-gray-600 leading-relaxed">' +
            '<p class="font-bold text-gray-500 mb-1">🍚 급여·💧 음수 (케어 일정에서 자동)</p>' +
            (feed.length ? esc(feed.join(" / ")) : '<span class="text-gray-400">등록된 급여·음수 일정이 없어요</span>') +
            '<p class="font-bold text-gray-500 mt-2 mb-1">💊 투약 (케어 일정에서 자동)</p>' +
            (med.length ? esc(med.join(" / ")) : '<span class="text-gray-400">등록된 투약 일정이 없어요</span>') +
            "</div>";

        var body =
            '<div class="space-y-4">' +
            '<p class="text-xs text-gray-500 leading-relaxed">펫시터·가족에게 넘길 <b>상시 케어 가이드</b>를 만들어요. ' +
            '링크를 받은 사람은 <b>로그인 없이</b> 읽기전용으로 봅니다. 급여·투약은 케어 일정에서 자동으로 채워져요.</p>' +
            autoBlock +
            '<div><label class="text-xs font-bold text-gray-500">🌿 알러지·주의 음식</label>' +
            '<textarea id="ch-allergy" rows="2" placeholder="예) 닭고기 알러지, 사람 음식 금지" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + esc(allergy) + "</textarea></div>" +
            '<div><label class="text-xs font-bold text-gray-500">📞 응급 연락처</label>' +
            '<textarea id="ch-emergency" rows="2" placeholder="예) 보호자 010-1234-5678 / 단골병원 02-123-4567" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + esc(emergency) + "</textarea></div>" +
            '<div><label class="text-xs font-bold text-gray-500">📝 그 밖의 당부</label>' +
            '<textarea id="ch-note" rows="2" placeholder="예) 산책은 아침·저녁 20분, 낯선 사람에게 겁이 많아요" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none">' + esc(note) + "</textarea></div>" +
            '<div id="ch-result" class="hidden pt-2 border-t border-gray-100"></div>' +
            "</div>";

        showModal("🩺 " + esc(pet.name) + " 인수인계 시트", body, [
            { text: "링크·QR 만들기", primary: true, closeOnClick: false, onClick: function () { generate(pet); } }
        ]);
    }

    function generate(pet) {
        var stored = {
            allergy: ((document.getElementById("ch-allergy") || {}).value || "").trim(),
            emergency: ((document.getElementById("ch-emergency") || {}).value || "").trim(),
            note: ((document.getElementById("ch-note") || {}).value || "").trim()
        };
        saveStored(pet, stored); // 상시 유지
        var payload = buildPayload(pet, stored);
        var url = shareUrl(payload);

        var box = document.getElementById("ch-result");
        if (!box) return;
        box.classList.remove("hidden");
        box.innerHTML =
            '<p class="text-xs font-bold text-gray-500 mb-2">✅ 읽기전용 인수인계 링크</p>' +
            '<div id="ch-qr" class="flex justify-center py-2"></div>' +
            '<div class="flex gap-2 mt-2">' +
            '<button type="button" onclick="CareHandoff._copy()" class="flex-1 py-2 bg-brand-500 hover:bg-brand-600 text-white text-sm font-bold rounded-xl">🔗 링크 복사</button>' +
            '<button type="button" onclick="CareHandoff._shareNative()" class="flex-1 py-2 bg-brand-50 hover:bg-brand-100 border border-brand-200 text-brand-700 text-sm font-bold rounded-xl">📤 공유</button>' +
            "</div>" +
            '<p class="text-[11px] text-gray-400 mt-2 leading-relaxed">QR을 보여주거나 링크를 전달하세요. 받는 사람은 앱 로그인 없이 이 시트만 봅니다.</p>';

        window.__careHandoffUrl = url;
        renderQrInto(document.getElementById("ch-qr"), url);
    }

    function renderQrInto(el, url) {
        if (!el) return;
        el.innerHTML = "";
        if (typeof window.qrcode !== "function") {
            el.innerHTML = '<p class="text-xs text-gray-400">QR 라이브러리 로딩 중…</p>'; return;
        }
        try {
            var qr = window.qrcode(0, "M");
            qr.addData(url);
            qr.make();
            el.innerHTML = qr.createSvgTag(5, 2);
            var svg = el.querySelector("svg");
            if (svg) { svg.setAttribute("width", "100%"); svg.setAttribute("height", "auto");
                svg.style.maxWidth = "200px"; svg.style.height = "auto"; }
        } catch (e) {
            el.innerHTML = '<p class="text-xs text-rose-400">QR 생성 실패</p>';
        }
    }

    function copyLink() {
        var url = window.__careHandoffUrl;
        if (!url) return;
        var done = function () { toast("인수인계 링크를 복사했어요! 📋"); };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(url).then(done, function () { window.prompt("인수인계 링크", url); });
        } else { window.prompt("인수인계 링크", url); }
    }
    function shareNative() {
        var url = window.__careHandoffUrl;
        if (!url) return;
        if (navigator.share) { navigator.share({ title: "펫과나 인수인계 시트", url: url }).catch(function () {}); }
        else copyLink();
    }

    // ── 읽기전용 열람 페이지 (/handoff?h=<data>) ────────────────
    function installCss() {
        if (document.getElementById("care-handoff-style")) return;
        var st = document.createElement("style");
        st.id = "care-handoff-style";
        st.textContent =
            ".ch-page-active header,.ch-page-active main,.ch-page-active #mobile-navbar," +
            ".ch-page-active #login-landing-overlay,.ch-page-active #achievement-popup-container," +
            ".ch-page-active #toast{display:none !important;}" +
            ".ch-page-active{overflow:auto !important;}";
        (document.head || document.documentElement).appendChild(st);
    }

    function section(icon, title, contentHtml) {
        return '<div class="px-6 py-3 border-b border-gray-100 last:border-0">' +
            '<p class="text-xs font-bold text-brand-600 mb-1">' + icon + " " + esc(title) + "</p>" +
            '<div class="text-sm text-gray-800 leading-relaxed break-words">' + contentHtml + "</div></div>";
    }
    function listOrEmpty(arr) {
        if (!arr || !arr.length) return '<span class="text-gray-400">기록 없음</span>';
        return arr.map(function (x) { return esc(x); }).join("<br>");
    }

    function renderPage(data) {
        installCss();
        document.documentElement.classList.add("ch-page-active");
        document.body.style.display = "block";
        if (document.getElementById("care-handoff-root")) return;

        var root = document.createElement("div");
        root.id = "care-handoff-root";
        root.className = "min-h-screen bg-gradient-to-b from-brand-50 to-white flex flex-col items-center px-4 py-8";

        var inner;
        if (!data || !data.p) {
            inner =
                '<div class="rounded-2xl bg-white shadow-xl p-8 text-center">' +
                '<div class="text-5xl mb-3">🩺</div>' +
                '<h1 class="text-lg font-extrabold text-gray-800">인수인계 시트를 찾을 수 없어요</h1>' +
                '<p class="text-sm text-gray-500 mt-2 leading-relaxed">링크가 올바르지 않거나 오래된 링크일 수 있어요.</p>' +
                '<a href="/" class="inline-block mt-5 text-sm font-bold text-brand-600">펫과나 홈으로 →</a></div>';
        } else {
            inner =
                '<div class="rounded-2xl bg-white shadow-xl overflow-hidden">' +
                '<div class="bg-brand-500 text-white text-center py-3">' +
                '<p class="text-sm font-bold tracking-wide">🩺 돌봄 인수인계 시트</p></div>' +
                '<div class="px-6 pt-4 pb-2 text-center border-b border-gray-100">' +
                '<h1 class="text-xl font-black text-gray-900">' + esc(data.p) + "</h1>" +
                (data.b ? '<p class="text-xs text-gray-400 mt-0.5">' + esc(data.b) + "</p>" : "") + "</div>" +
                section("🍚", "급여·음수", listOrEmpty(data.feed)) +
                section("💊", "투약", listOrEmpty(data.med)) +
                section("🌿", "알러지·주의 음식", data.allergy ? nl2br(data.allergy) : '<span class="text-gray-400">기록 없음</span>') +
                section("📞", "응급 연락처", data.emergency ? nl2br(data.emergency) : '<span class="text-gray-400">기록 없음</span>') +
                (data.note ? section("📝", "당부", nl2br(data.note)) : "") +
                '<p class="px-6 py-4 text-center text-[11px] text-gray-400 leading-relaxed">' +
                "보호자가 공유한 읽기전용 정보예요. 위급하면 먼저 응급 연락처로 전화하세요.</p>" +
                "</div>" +
                '<p class="text-center mt-4"><a href="/" class="text-xs text-gray-400">펫과나 — 반려동물 케어 올인원</a></p>';
        }
        root.innerHTML = '<div class="w-full max-w-md">' + inner + "</div>";
        document.body.appendChild(root);
    }

    // ── 라우팅: /handoff?h=<data> 이면 열람 페이지, 아니면 앱 정상 부팅 ──
    function route() {
        if (!/^\/handoff\/?$/.test(location.pathname)) return false;
        var raw = new URLSearchParams(location.search).get("h");
        if (!raw) return false;
        document.title = "🩺 돌봄 인수인계 시트 — 펫과나";
        renderPage(decode(raw));
        return true;
    }

    window.CareHandoff = {
        open: open, _route: route,
        _copy: copyLink, _shareNative: shareNative
    };

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", route);
    else route();
})();
