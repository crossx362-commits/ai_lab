// 🆘 미아방지 공개 프로필 (QR) — public_pet_profiles
// ─────────────────────────────────────────────────────────────
// 반려동물별 '읽기전용 공개 프로필'을 만들어 QR로 인쇄/공유한다. 습득자가 QR을
// 스캔하면 로그인 없이 /p/<token> 에서 보호자가 고른 정보만 열람 → 빠른 상봉.
//
// 프라이버시 설계(migrations/add_public_pet_profiles.sql 계약을 프론트에서 준수):
//  · 토큰은 추측 불가 랜덤(순차 id 아님) → URL 열거 불가
//  · 보호자가 고른 필드만 public_fields 에 저장(안 고른 항목은 애초에 미보관)
//  · is_public=false 로 즉시 철회, 행 삭제 가능
//  · 연락처는 '마스킹된 문자열만' 저장(원문 전화번호 저장 금지) — 스키마 계약
//    → 실제 통화가 필요하면 보호자가 '공개 안내 메시지'(오픈채팅 링크 등)를 직접 넣는다.
//
// 저장: Supabase 연결 시 public_pet_profiles 테이블(교차기기 열람), 항상 localStorage
//       미러(보호자 본인 기기·오프라인·미연결 데모). 습득자 열람은 익명 SELECT(RLS).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_public_profiles";        // { token: profile }
    var LS_PET_PREFIX = "petna_pubprofile_pet_";  // pet_id → token (기존 프로필 찾기)

    // 공개 가능 필드 정의(스키마 주석의 권장 집합)
    var FIELD_DEFS = [
        { key: "name", label: "이름", icon: "fa-tag" },
        { key: "photo", label: "사진", icon: "fa-image" },
        { key: "breed", label: "견종/묘종", icon: "fa-dog" },
        { key: "traits", label: "성격·특징", icon: "fa-heart" },
        { key: "allergies", label: "알레르기", icon: "fa-triangle-exclamation" },
        { key: "meds", label: "복용약", icon: "fa-pills" },
    ];

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(msg) { if (typeof window.showToast === "function") window.showToast(msg); }

    // esc() 후 http(s) URL을 클릭 가능한 a 태그로 감싼다(오픈채팅 링크 활성화).
    function linkify(s) {
        return esc(s).replace(/https?:\/\/[^\s<]+/g, function (u) {
            return '<a href="' + u + '" target="_blank" rel="noopener noreferrer nofollow" class="text-brand-600 underline break-all">' + u + "</a>";
        });
    }

    // 전화번호 마스킹: 가운데를 가려 원문 미보관. 010-1234-5678 → 010-****-5678
    function maskPhone(raw) {
        var digits = String(raw || "").replace(/[^0-9]/g, "");
        if (digits.length < 7) return raw ? "***-****" : "";
        var last4 = digits.slice(-4);
        var head = digits.slice(0, digits.length >= 10 ? 3 : 2);
        return head + "-****-" + last4;
    }

    function genToken() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID().replace(/-/g, "");
        var a = new Uint8Array(16);
        (window.crypto || {}).getRandomValues ? crypto.getRandomValues(a)
            : a.forEach(function (_, i) { a[i] = Math.floor(Math.random() * 256); });
        return Array.from(a).map(function (b) { return b.toString(16).padStart(2, "0"); }).join("");
    }

    // ── 저장소 ────────────────────────────────────────────────
    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || {}; } catch (e) { return {}; } }
    function saveAll(map) { localStorage.setItem(LS_KEY, JSON.stringify(map)); }

    function localGet(token) { return loadAll()[token] || null; }
    function localPut(profile) {
        var map = loadAll(); map[profile.token] = profile; saveAll(map);
        if (profile.pet_id) localStorage.setItem(LS_PET_PREFIX + profile.pet_id, profile.token);
    }
    function localDelete(token) {
        var map = loadAll(); var p = map[token]; delete map[token]; saveAll(map);
        if (p && p.pet_id) localStorage.removeItem(LS_PET_PREFIX + p.pet_id);
    }
    function tokenForPet(petId) { return localStorage.getItem(LS_PET_PREFIX + petId) || null; }

    function connected() {
        return typeof SupabaseService !== "undefined" && SupabaseService.isConnected && SupabaseService.client;
    }

    // Supabase 업서트(연결 시). 로그인 사용자만 가능(RLS: auth.uid()=user_id)
    async function remoteUpsert(profile) {
        if (!connected()) return;
        try {
            var row = {
                token: profile.token,
                pet_id: profile.pet_id,
                is_public: profile.is_public,
                public_fields: profile.public_fields,
                contact_masked: profile.contact_masked || null,
            };
            var res = await SupabaseService.client.from("public_pet_profiles").upsert([row]);
            if (res.error) throw res.error;
        } catch (e) {
            (window.AppLogger ? AppLogger.warn : console.warn)("공개 프로필 원격 저장 실패(로컬은 유지)", e && e.message);
        }
    }
    async function remoteDelete(token) {
        if (!connected()) return;
        try { await SupabaseService.client.from("public_pet_profiles").delete().eq("token", token); }
        catch (e) { (window.AppLogger ? AppLogger.warn : console.warn)("공개 프로필 원격 삭제 실패", e && e.message); }
    }
    async function remoteGet(token) {
        if (!connected()) return null;
        try {
            var res = await SupabaseService.client.from("public_pet_profiles")
                .select("*").eq("token", token).eq("is_public", true).maybeSingle();
            if (res.error) throw res.error;
            return res.data || null;
        } catch (e) {
            (window.AppLogger ? AppLogger.warn : console.warn)("공개 프로필 원격 조회 실패", e && e.message);
            return null;
        }
    }

    // ── 공개 URL / QR ─────────────────────────────────────────
    function publicUrl(token) { return location.origin + "/p/" + token; }

    function renderQrInto(el, url) {
        el.innerHTML = "";
        if (typeof window.qrcode !== "function") {
            el.innerHTML = '<p class="text-xs text-gray-400">QR 라이브러리 로딩 중…</p>'; return;
        }
        try {
            var qr = window.qrcode(0, "M");   // typeNumber 0 = 자동, ECC level M
            qr.addData(url);
            qr.make();
            el.innerHTML = qr.createSvgTag(5, 2);
            var svg = el.querySelector("svg");
            if (svg) { svg.setAttribute("width", "100%"); svg.setAttribute("height", "100%");
                svg.style.maxWidth = "220px"; svg.style.height = "auto"; }
        } catch (e) {
            el.innerHTML = '<p class="text-xs text-rose-400">QR 생성 실패: ' + esc(e && e.message) + "</p>";
        }
    }

    // ── 보호자용 설정 모달 ─────────────────────────────────────
    function activePet() {
        if (typeof window.getActivePet === "function") return window.getActivePet();
        return (typeof pets !== "undefined" && pets && pets[0]) || null;
    }

    function open() {
        var pet = activePet();
        if (!pet) { toast("먼저 반려동물을 등록해 주세요 🐾"); return; }

        var existingToken = tokenForPet(pet.id);
        var existing = existingToken ? localGet(existingToken) : null;
        var pf = (existing && existing.public_fields) || {};
        // 재열기 땐 '실제 선택(_keys)'으로 복원(스냅샷 값이 비어도 선택은 유지). 최초엔 기본 체크.
        var selected = (pf && pf._keys) || null;
        var defaults = { name: true, photo: true, traits: true, breed: true };

        var checks = FIELD_DEFS.map(function (f) {
            var on = selected ? !!selected[f.key] : !!defaults[f.key];
            return '<label class="flex items-center gap-2 py-1.5 cursor-pointer">' +
                '<input type="checkbox" class="pp-field accent-brand-500 w-4 h-4" data-key="' + f.key + '"' + (on ? " checked" : "") + ">" +
                '<i class="fa-solid ' + f.icon + ' text-brand-400 text-xs w-4 text-center"></i>' +
                '<span class="text-sm text-gray-700">' + f.label + "</span></label>";
        }).join("");

        var curContact = existing ? (existing.contact_masked || "") : "";
        var curNote = existing ? (existing.note || "") : "";
        var isPublic = existing ? existing.is_public !== false : true;

        var body =
            '<div class="space-y-4">' +
            '<p class="text-xs text-gray-500 leading-relaxed">습득자가 QR을 스캔하면 <b>로그인 없이</b> 아래에서 고른 정보만 봅니다. ' +
            '전화번호는 개인정보 보호를 위해 <b>가운데를 가려</b> 저장·표시됩니다.</p>' +

            '<div><p class="text-xs font-bold text-gray-500 mb-1">공개할 정보</p>' +
            '<div class="grid grid-cols-2 gap-x-3 rounded-xl bg-gray-50 px-3 py-2">' + checks + "</div></div>" +

            '<div><label class="text-xs font-bold text-gray-500">연락처(선택) — 마스킹되어 저장</label>' +
            '<input id="pp-contact" type="tel" inputmode="tel" value="' + esc(curContact) + '" placeholder="010-1234-5678" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +

            '<div><label class="text-xs font-bold text-gray-500">공개 안내 메시지(선택)</label>' +
            '<input id="pp-note" type="text" maxlength="80" value="' + esc(curNote) + '" placeholder="예) 발견 시 카톡 오픈채팅으로 연락 주세요 🙏" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none"></div>' +

            '<div id="pp-result" class="hidden pt-2 border-t border-gray-100"></div>' +
            "</div>";

        var buttons = [
            { text: existing ? "QR 갱신" : "QR 만들기", primary: true, closeOnClick: false, onClick: function () { save(pet); } },
        ];
        if (existing) {
            buttons.push({ text: isPublic ? "공개 중지" : "다시 공개", closeOnClick: false, onClick: function () { toggle(pet, existingToken); } });
            buttons.push({ text: "삭제", danger: true, closeOnClick: false, onClick: function () { remove(pet, existingToken); } });
        }

        showModal("🆘 " + esc(pet.name) + " 미아방지 QR", body, buttons);

        if (existing) setTimeout(function () { showResult(existing); }, 30);
    }

    function collectFields() {
        var out = {};
        document.querySelectorAll(".pp-field").forEach(function (cb) { if (cb.checked) out[cb.dataset.key] = true; });
        return out;
    }

    function snapshotPet(pet, fields) {
        // 고른 필드의 '현재 값'을 스냅샷으로 저장(공개 시점 값 고정, 이후 앱 데이터 변경과 분리)
        var snap = {};
        if (fields.name) snap.name = pet.name || "";
        if (fields.photo) snap.photo = (pet.type === "custom" && pet.imageUrl) ? pet.imageUrl : "";
        if (fields.breed) snap.breed = pet.breed || "";
        if (fields.traits) snap.traits = pet.personality || "";
        if (fields.allergies) snap.allergies = pet.allergies || "";
        if (fields.meds) snap.meds = pet.meds || "";
        return snap;
    }

    async function save(pet) {
        var fields = collectFields();
        if (!Object.keys(fields).length) { toast("공개할 정보를 하나 이상 선택해 주세요"); return; }
        var contactRaw = (document.getElementById("pp-contact") || {}).value || "";
        var note = ((document.getElementById("pp-note") || {}).value || "").trim();

        var existingToken = tokenForPet(pet.id);
        var token = existingToken || genToken();
        var profile = {
            token: token,
            pet_id: String(pet.id),
            is_public: true,
            public_fields: Object.assign(snapshotPet(pet, fields), { _keys: fields }),
            contact_masked: contactRaw ? maskPhone(contactRaw) : "",
            note: note,
            updated_at: new Date().toISOString(),
        };
        localPut(profile);
        await remoteUpsert(profile);
        toast(existingToken ? "공개 프로필을 갱신했어요 ✅" : "미아방지 QR을 만들었어요 🆘");
        showResult(profile);
    }

    async function toggle(pet, token) {
        var p = localGet(token); if (!p) return;
        p.is_public = !p.is_public; p.updated_at = new Date().toISOString();
        localPut(p);
        if (connected()) {
            try { await SupabaseService.client.from("public_pet_profiles").update({ is_public: p.is_public }).eq("token", token); }
            catch (e) { (window.AppLogger ? AppLogger.warn : console.warn)("공개 토글 원격 반영 실패", e && e.message); }
        }
        toast(p.is_public ? "다시 공개되었어요" : "공개를 중지했어요(습득자 열람 차단)");
        showResult(p);
        // 버튼 라벨 갱신 위해 모달 재구성
        setTimeout(function () { open(); }, 400);
    }

    async function remove(pet, token) {
        localDelete(token);
        await remoteDelete(token);
        toast("공개 프로필을 삭제했어요");
        if (typeof window.closeModal === "function") window.closeModal();
    }

    function showResult(profile) {
        var box = document.getElementById("pp-result");
        if (!box) return;
        box.classList.remove("hidden");
        var url = publicUrl(profile.token);
        var status = profile.is_public !== false
            ? '<span class="text-emerald-600 font-bold">공개 중</span>'
            : '<span class="text-rose-500 font-bold">공개 중지됨</span>';
        box.innerHTML =
            '<div class="flex flex-col items-center gap-3 pt-1">' +
            '<div id="pp-qr" class="bg-white p-2 rounded-xl border border-gray-100 flex items-center justify-center" style="min-height:120px"></div>' +
            '<p class="text-[11px] text-gray-400">' + status + " · QR을 인쇄해 목걸이·하네스에 달아두세요</p>" +
            '<div class="w-full flex items-center gap-1.5">' +
            '<input readonly value="' + esc(url) + '" class="flex-1 rounded-lg border border-gray-200 bg-gray-50 px-2 py-1.5 text-[11px] text-gray-500">' +
            '<button onclick="PublicProfile.copy(\'' + esc(url) + '\')" class="rounded-lg bg-brand-500 hover:bg-brand-600 text-white px-3 py-1.5 text-xs font-bold whitespace-nowrap">복사</button>' +
            '<button onclick="PublicProfile.print(\'' + esc(profile.token) + '\')" class="rounded-lg border border-gray-200 hover:bg-gray-50 px-3 py-1.5 text-xs font-bold whitespace-nowrap">인쇄</button>' +
            '<button onclick="PublicProfile.preview(\'' + esc(profile.token) + '\')" class="rounded-lg border border-gray-200 hover:bg-gray-50 px-3 py-1.5 text-xs font-bold whitespace-nowrap">미리보기</button>' +
            "</div></div>";
        var qel = document.getElementById("pp-qr");
        if (qel) {
            if (profile.is_public !== false) renderQrInto(qel, url);
            else qel.innerHTML = '<p class="text-xs text-gray-400 px-4 text-center">공개 중지 상태 — 다시 공개하면 QR이 활성화됩니다</p>';
        }
    }

    function copy(url) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(url).then(function () { toast("링크를 복사했어요 📋"); },
                function () { toast("복사 실패 — 링크를 길게 눌러 복사하세요"); });
        } else { toast("이 브라우저는 자동 복사를 지원하지 않아요"); }
    }
    function preview(token) { window.open(publicUrl(token), "_blank"); }

    // 목걸이·하네스용 QR 태그를 별도 창에서 인쇄. QR SVG는 부모 창에서 생성해 주입.
    function print(token) {
        var profile = localGet(token);
        if (!profile || profile.is_public === false) { toast("공개 중인 프로필만 인쇄할 수 있어요"); return; }
        var url = publicUrl(token);
        var name = (profile.public_fields && profile.public_fields.name) || "우리 아이";
        var qrHtml = '<p style="font-size:12px;color:#999">QR 생성 실패</p>';
        if (typeof window.qrcode === "function") {
            try {
                var qr = window.qrcode(0, "M");
                qr.addData(url); qr.make();
                qrHtml = qr.createSvgTag(6, 4);
            } catch (e) { /* keep fallback */ }
        }
        var w = window.open("", "_blank", "width=420,height=560");
        if (!w) { toast("팝업이 차단됐어요 — 팝업을 허용해 주세요"); return; }
        w.document.write(
            '<!doctype html><html lang="ko"><head><meta charset="utf-8">' +
            '<title>미아방지 QR — ' + esc(name) + '</title>' +
            '<style>@media print{@page{margin:12mm}}' +
            'body{font-family:-apple-system,"Apple SD Gothic Neo","Malgun Gothic",sans-serif;' +
            'margin:0;display:flex;justify-content:center;padding:24px}' +
            '.card{width:300px;border:2px solid #f43f5e;border-radius:18px;padding:20px;text-align:center}' +
            '.hd{color:#f43f5e;font-weight:800;font-size:15px;margin:0 0 10px}' +
            '.qr{width:200px;height:200px;margin:0 auto}.qr svg{width:100%;height:100%}' +
            '.nm{font-weight:800;font-size:18px;margin:12px 0 2px}' +
            '.tip{color:#888;font-size:11px;line-height:1.5;margin-top:8px}' +
            '.url{color:#aaa;font-size:9px;word-break:break-all;margin-top:6px}</style></head>' +
            '<body onload="window.focus();window.print()">' +
            '<div class="card"><p class="hd">🆘 저를 발견하셨나요?</p>' +
            '<div class="qr">' + qrHtml + '</div>' +
            '<p class="nm">' + esc(name) + '</p>' +
            '<p class="tip">QR을 스캔하면 보호자 연락처가 보여요.<br>목걸이·하네스에 달아 주세요 🙏</p>' +
            '<p class="url">' + esc(url) + '</p></div></body></html>'
        );
        w.document.close();
    }

    // 모달 헬퍼: 앱의 showCustomDialog 우선, 없으면 자체 오버레이
    function showModal(title, bodyHtml, buttons) {
        if (typeof window.showCustomDialog === "function") {
            // showCustomDialog는 커스텀 버튼 콜백 지원이 제한적 → 자체 오버레이 사용
        }
        var overlay = document.createElement("div");
        overlay.id = "pp-modal-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-sm max-h-[88vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">' + title + "</h3>" +
            '<button onclick="PublicProfile.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4">' + bodyHtml + "</div>" +
            '<div id="pp-modal-buttons" class="px-5 py-3 border-t border-gray-100 flex gap-2 justify-end sticky bottom-0 bg-white"></div></div>';
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        var bar = overlay.querySelector("#pp-modal-buttons");
        buttons.forEach(function (b) {
            var btn = document.createElement("button");
            btn.textContent = b.text;
            btn.className = b.primary
                ? "rounded-xl bg-brand-500 hover:bg-brand-600 text-white px-4 py-2 text-sm font-bold"
                : b.danger
                    ? "rounded-xl bg-rose-50 hover:bg-rose-100 text-rose-600 px-4 py-2 text-sm font-bold"
                    : "rounded-xl border border-gray-200 hover:bg-gray-50 text-gray-600 px-4 py-2 text-sm font-bold";
            btn.addEventListener("click", function () { b.onClick && b.onClick(); if (b.closeOnClick !== false) close(); });
            bar.appendChild(btn);
        });
    }
    function close() { var el = document.getElementById("pp-modal-overlay"); if (el) el.remove(); }

    // ── 습득자용 공개 페이지 (/p/<token>) ─────────────────────
    function fieldRow(icon, label, value) {
        if (!value) return "";
        return '<div class="flex items-start gap-3 py-2.5 border-b border-gray-100 last:border-0">' +
            '<i class="fa-solid ' + icon + ' text-brand-400 w-5 text-center mt-0.5"></i>' +
            '<div><p class="text-[11px] text-gray-400 font-bold">' + label + "</p>" +
            '<p class="text-sm text-gray-800 font-medium break-words">' + esc(value) + "</p></div></div>";
    }

    // 앱 크롬을 CSS(!important)로 강제 숨김 — app.js 부트스트랩이 이후 인라인 display를
    // 다시 켜도 눌러 이긴다(JS 타이밍 비의존). <html>.pp-finder-active 로 스코프.
    function installFinderCss() {
        if (document.getElementById("pp-finder-style")) return;
        var st = document.createElement("style");
        st.id = "pp-finder-style";
        st.textContent =
            ".pp-finder-active header,.pp-finder-active main,.pp-finder-active #mobile-navbar," +
            ".pp-finder-active #login-landing-overlay,.pp-finder-active #achievement-popup-container," +
            ".pp-finder-active #toast{display:none !important;}" +
            ".pp-finder-active{overflow:auto !important;}";
        (document.head || document.documentElement).appendChild(st);
    }

    function renderFinder(token) {
        // 앱 크롬 완전 숨김 → 독립 전체화면(CSS 클래스로 timing-independent)
        installFinderCss();
        document.documentElement.classList.add("pp-finder-active");
        document.body.style.display = "block";

        if (document.getElementById("pp-finder-root")) return; // 중복 방지
        var root = document.createElement("div");
        root.id = "pp-finder-root";
        root.className = "min-h-screen bg-gradient-to-b from-brand-50 to-white flex flex-col items-center px-4 py-8";
        root.innerHTML = '<div class="w-full max-w-md"><p class="text-center text-gray-400 text-sm py-16">프로필을 불러오는 중… 🐾</p></div>';
        document.body.appendChild(root);

        loadFinder(token, 0);
    }

    async function loadFinder(token, attempt) {
        // 로컬 우선(본인 기기·데모), 없으면 Supabase(교차기기). 미연결이면 잠깐 재시도.
        var profile = localGet(token);
        if (!profile) profile = await remoteGet(token);
        if (!profile && !connected() && attempt < 8) {
            return setTimeout(function () { loadFinder(token, attempt + 1); }, 400); // 클라 초기화 대기
        }
        paintFinder(token, profile);
    }

    function paintFinder(token, profile) {
        var root = document.getElementById("pp-finder-root");
        if (!root) return;
        var wrap = root.firstElementChild;

        if (!profile || profile.is_public === false) {
            wrap.innerHTML =
                '<div class="rounded-2xl bg-white shadow-xl p-8 text-center">' +
                '<div class="text-5xl mb-3">🐾</div>' +
                '<h1 class="text-lg font-extrabold text-gray-800">프로필을 찾을 수 없어요</h1>' +
                '<p class="text-sm text-gray-500 mt-2 leading-relaxed">' +
                (profile ? "보호자가 이 프로필을 비공개로 전환했어요." : "링크가 올바르지 않거나 삭제된 프로필이에요.") +
                "</p>" +
                '<a href="/" class="inline-block mt-5 text-sm font-bold text-brand-600">펫과나 홈으로 →</a></div>';
            return;
        }

        var f = profile.public_fields || {};
        var photo = f.photo;
        var name = f.name;
        var rows =
            fieldRow("fa-dog", "견종/묘종", f.breed) +
            fieldRow("fa-heart", "성격·특징", f.traits) +
            fieldRow("fa-triangle-exclamation", "알레르기", f.allergies) +
            fieldRow("fa-pills", "복용약", f.meds);

        var contact = "";
        if (profile.contact_masked) {
            contact += '<div class="flex items-center gap-2 justify-center"><i class="fa-solid fa-phone text-brand-500"></i>' +
                '<span class="text-base font-black tracking-wide text-gray-800">' + esc(profile.contact_masked) + "</span></div>";
        }
        if (profile.note) {
            contact += '<p class="text-sm text-gray-600 mt-1.5 text-center leading-relaxed">' + linkify(profile.note) + "</p>";
        }
        if (!contact) contact = '<p class="text-sm text-gray-500 text-center">보호자 연락처가 등록되어 있지 않아요.</p>';

        wrap.innerHTML =
            '<div class="rounded-2xl bg-white shadow-xl overflow-hidden">' +
            '<div class="bg-brand-500 text-white text-center py-3">' +
            '<p class="text-sm font-bold tracking-wide">🆘 저를 발견하셨나요? 집을 찾고 있어요</p></div>' +

            (photo
                ? '<img src="' + esc(photo) + '" alt="" class="w-full h-56 object-cover">'
                : '<div class="w-full h-56 flex items-center justify-center bg-brand-50 text-6xl">🐶</div>') +

            '<div class="px-6 pt-4 pb-2 text-center">' +
            (name ? '<h1 class="text-2xl font-black text-gray-900">' + esc(name) + "</h1>" : "") +
            '<p class="text-xs text-gray-400 mt-0.5">펫과나 미아방지 프로필</p></div>' +

            (rows ? '<div class="px-6 py-2">' + rows + "</div>" : "") +

            '<div class="mx-6 my-4 rounded-xl bg-brand-50 px-4 py-4">' +
            '<p class="text-[11px] font-bold text-brand-600 text-center mb-2">보호자에게 연락해 주세요 🙏</p>' +
            contact + "</div>" +

            '<p class="px-6 pb-5 text-center text-[11px] text-gray-400 leading-relaxed">' +
            '따뜻한 관심 덕분에 아이가 집으로 돌아갈 수 있어요. 감사합니다.</p>' +
            "</div>" +
            '<p class="text-center mt-4"><a href="/" class="text-xs text-gray-400">펫과나 — 반려동물 케어 올인원</a></p>';
    }

    // ── 라우팅: /p/<token> 이면 습득자 페이지, 아니면 앱 정상 부팅 ──
    function route() {
        var m = location.pathname.match(/^\/p\/([A-Za-z0-9]{8,})\/?$/);
        if (m) { document.title = "🐾 미아방지 프로필 — 펫과나"; renderFinder(m[1]); return true; }
        return false;
    }

    // 공개 API
    window.PublicProfile = { open: open, copy: copy, preview: preview, print: print, close: close, _route: route };

    // defer 스크립트라 DOM 준비됨. 즉시 라우팅 판정.
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", route);
    else route();
})();
