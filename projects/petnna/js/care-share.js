// care-share.js — 가족 공동돌봄 읽기전용 공유 (오늘 할 일)
//
// public-profile.js 의 "로그인 없는 공개 페이지" 패턴을 케어 스케줄에 확장.
// 백엔드/DB 없이 오늘 일정을 URL 자체에 인코딩(?care=<b64>)해 링크를 공유한다.
//  · /care?care=<data> 를 열면 앱 로그인 없이 오늘 할 일 목록을 읽기전용으로 열람
//  · 가족이 항목을 체크할 수 있으나 체크는 열람 기기 로컬(localStorage)에만 저장 → 원본 스케줄은 불변(읽기전용)
//  · 스키마·인증 변경 없음: 공유 데이터는 링크에만 담긴다(교차기기 = 링크 전달)
(function () {
    "use strict";

    function esc(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

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

    var TYPE_ICON = {
        feed: "🍚", water: "💧", walk: "🐾", medicine: "💊",
        vet: "🏥", groom: "✂️", play: "🎾"
    };

    // 오늘 할 일 페이로드 생성
    function buildPayload() {
        var list = (typeof getTodaySchedules === "function") ? getTodaySchedules() : [];
        var pet = (typeof getActivePet === "function") ? getActivePet() : null;
        return {
            v: 1,
            p: (pet && pet.name) || "우리 아이",
            d: new Date().toISOString().split("T")[0],
            t: list.map(function (s) {
                return { ti: s.time || "", x: s.title || "", ty: s.type || "" };
            })
        };
    }

    function shareUrl() {
        return location.origin + "/care?care=" + encode(buildPayload());
    }

    // 공유 버튼 핸들러: Web Share → 클립보드 폴백
    function share() {
        var payload = buildPayload();
        if (!payload.t.length) {
            if (typeof showToast === "function") showToast("오늘 공유할 일정이 없어요 📅");
            return;
        }
        var url = shareUrl();
        var text = payload.p + " 오늘 돌봄 할 일 " + payload.t.length + "개";
        if (navigator.share) {
            navigator.share({ title: "펫과나 가족 공동돌봄", text: text, url: url }).catch(function () {});
            return;
        }
        var done = function () {
            if (typeof showToast === "function") showToast("가족 공유 링크를 복사했어요! 👨‍👩‍👧 붙여넣어 전달하세요");
        };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(url).then(done, function () { window.prompt("공유 링크", url); });
        } else {
            window.prompt("공유 링크", url);
        }
    }

    // ── 읽기전용 공유 페이지 (/care?care=<data>) ──────────────
    function installShareCss() {
        if (document.getElementById("care-share-style")) return;
        var st = document.createElement("style");
        st.id = "care-share-style";
        st.textContent =
            ".cs-share-active header,.cs-share-active main,.cs-share-active #mobile-navbar," +
            ".cs-share-active #login-landing-overlay,.cs-share-active #achievement-popup-container," +
            ".cs-share-active #toast{display:none !important;}" +
            ".cs-share-active{overflow:auto !important;}";
        (document.head || document.documentElement).appendChild(st);
    }

    function checkKey(data) { return "petna_care_share_check_" + data.d + "_" + data.p; }

    function loadChecks(data) {
        try { return JSON.parse(localStorage.getItem(checkKey(data))) || {}; }
        catch (e) { return {}; }
    }
    function toggleCheck(idx) {
        var data = window.__careShareData;
        if (!data) return;
        var checks = loadChecks(data);
        checks[idx] = !checks[idx];
        try { localStorage.setItem(checkKey(data), JSON.stringify(checks)); } catch (e) {}
        paint(data);
    }

    function paint(data) {
        var root = document.getElementById("care-share-root");
        if (!root) return;
        var wrap = root.firstElementChild;

        if (!data || !data.t || !data.t.length) {
            wrap.innerHTML =
                '<div class="rounded-2xl bg-white shadow-xl p-8 text-center">' +
                '<div class="text-5xl mb-3">📅</div>' +
                '<h1 class="text-lg font-extrabold text-gray-800">공유된 할 일을 찾을 수 없어요</h1>' +
                '<p class="text-sm text-gray-500 mt-2 leading-relaxed">링크가 올바르지 않거나 오래된 링크일 수 있어요.</p>' +
                '<a href="/" class="inline-block mt-5 text-sm font-bold text-brand-600">펫과나 홈으로 →</a></div>';
            return;
        }

        var checks = loadChecks(data);
        var doneCount = data.t.reduce(function (n, _, i) { return n + (checks[i] ? 1 : 0); }, 0);

        var rows = data.t.map(function (item, i) {
            var on = !!checks[i];
            return '<button type="button" onclick="CareShare._toggle(' + i + ')" ' +
                'class="w-full flex items-center gap-3 py-3 px-3 border-b border-gray-100 last:border-0 text-left ' +
                (on ? "bg-emerald-50" : "bg-white") + '">' +
                '<span class="w-6 h-6 flex items-center justify-center rounded-full border-2 ' +
                (on ? "border-emerald-500 bg-emerald-500 text-white" : "border-gray-300 text-transparent") +
                ' text-xs font-black shrink-0">✓</span>' +
                '<span class="text-lg shrink-0">' + esc(TYPE_ICON[item.ty] || "📌") + "</span>" +
                '<span class="flex-1 ' + (on ? "line-through text-gray-400" : "text-gray-800") + ' font-medium break-words">' +
                esc(item.x) + "</span>" +
                (item.ti ? '<span class="text-xs font-bold text-gray-400 shrink-0">' + esc(item.ti) + "</span>" : "") +
                "</button>";
        }).join("");

        wrap.innerHTML =
            '<div class="rounded-2xl bg-white shadow-xl overflow-hidden">' +
            '<div class="bg-brand-500 text-white text-center py-3">' +
            '<p class="text-sm font-bold tracking-wide">👨‍👩‍👧 가족 공동돌봄 — 오늘 할 일</p></div>' +
            '<div class="px-6 pt-4 pb-1 text-center">' +
            '<h1 class="text-xl font-black text-gray-900">' + esc(data.p) + "</h1>" +
            '<p class="text-xs text-gray-400 mt-0.5">' + esc(data.d) + " · " + doneCount + "/" + data.t.length + " 완료</p></div>" +
            '<div class="px-4 py-2">' + rows + "</div>" +
            '<p class="px-6 py-4 text-center text-[11px] text-gray-400 leading-relaxed">' +
            "체크는 이 기기에만 저장돼요. 보호자의 원본 일정은 바뀌지 않아요.</p>" +
            "</div>" +
            '<p class="text-center mt-4"><a href="/" class="text-xs text-gray-400">펫과나 — 반려동물 케어 올인원</a></p>';
    }

    function renderShare(data) {
        installShareCss();
        document.documentElement.classList.add("cs-share-active");
        document.body.style.display = "block";
        if (document.getElementById("care-share-root")) return;
        var root = document.createElement("div");
        root.id = "care-share-root";
        root.className = "min-h-screen bg-gradient-to-b from-brand-50 to-white flex flex-col items-center px-4 py-8";
        root.innerHTML = '<div class="w-full max-w-md"></div>';
        document.body.appendChild(root);
        window.__careShareData = data;
        paint(data);
    }

    // ── 라우팅: /care?care=<data> 이면 공유 페이지, 아니면 앱 정상 부팅 ──
    function route() {
        if (!/^\/care\/?$/.test(location.pathname)) return false;
        var raw = new URLSearchParams(location.search).get("care");
        if (!raw) return false;
        document.title = "👨‍👩‍👧 가족 공동돌봄 — 펫과나";
        renderShare(decode(raw));
        return true;
    }

    window.CareShare = { share: share, url: shareUrl, _route: route, _toggle: toggleCheck };

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", route);
    else route();
})();
