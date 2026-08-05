// 📷 사료/간식 바코드 스캔 — 성분 조회 & 알러지 대조 (나무 백로그 P3, 기획)
// ─────────────────────────────────────────────────────────────
// 제품 바코드를 카메라(브라우저 네이티브 BarcodeDetector)로 읽거나 직접 입력하면
// Open Pet Food Facts 공개 API로 성분표를 조회하고, 프로필에 등록된 알러지/기피
// 성분(pet.allergies)과 대조해 겹치는 성분을 경고로 표시한다.
//
// 가드레일:
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. 조회는 공개 read-only API(키 불필요).
//  · 새 라이브러리 없음 — 스캔은 브라우저 표준 BarcodeDetector, 미지원 시 수동 입력 폴백.
//  · 참고용 — 성분 DB가 불완전할 수 있으니 실제 제품 라벨을 반드시 함께 확인.
//  · 알러지 파싱은 FoodSafety와 동일 규칙(pet.allergies 자유입력 토큰화).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var API = "https://world.openpetfoodfacts.org/api/v2/product/";
    var FIELDS = "product_name,product_name_ko,brands,ingredients_text,ingredients_text_ko,image_front_small_url";
    var FORMATS = ["ean_13", "ean_8", "upc_a", "upc_e", "code_128"];

    var stream = null;   // 활성 카메라 스트림
    var rafId = null;    // 스캔 루프 핸들
    var detector = null; // BarcodeDetector 인스턴스

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(msg) { if (typeof showToast === "function") showToast(msg); }

    // 활성 펫의 알러지/기피 성분(pet.allergies, 자유입력) → 소문자 토큰 배열.
    function petAllergens() {
        var pet = (typeof window.getActivePet === "function") ? window.getActivePet() : null;
        var src = pet && (pet.allergies || pet.allergy);
        if (!src) return [];
        if (Array.isArray(src)) src = src.join(",");
        return String(src).toLowerCase()
            .split(/[,、/·\s]+/)
            .map(function (t) { return t.trim(); })
            .filter(function (t) { return t.length >= 1; });
    }

    // 성분 텍스트에서 등록된 알러지 토큰과 겹치는 것들을 반환(부분일치).
    function allergyHits(ingredientsText) {
        var toks = petAllergens();
        if (!toks.length || !ingredientsText) return [];
        var hay = String(ingredientsText).toLowerCase();
        return toks.filter(function (t) { return hay.indexOf(t) !== -1; });
    }

    function isValidCode(code) { return /^[0-9]{6,14}$/.test(String(code || "").trim()); }

    function open() {
        close();
        var overlay = document.createElement("div");
        overlay.id = "bs-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">📷 사료·간식 바코드 스캔</h3>' +
            '<button onclick="FoodScanner.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3 overflow-y-auto flex-1">' +
            '<div id="bs-cam-wrap" class="hidden">' +
            '<div class="relative rounded-xl overflow-hidden bg-black aspect-[4/3]">' +
            '<video id="bs-video" class="w-full h-full object-cover" playsinline muted></video>' +
            '<div class="absolute inset-x-6 top-1/2 -translate-y-1/2 h-0.5 bg-brand-400/80"></div></div>' +
            '<p class="text-[11px] text-gray-400 mt-1.5 text-center">바코드를 화면 중앙에 맞춰 주세요.</p></div>' +
            '<button id="bs-cam-btn" onclick="FoodScanner.startCamera()" ' +
            'class="w-full py-2.5 rounded-xl bg-brand-500 text-white text-sm font-bold hover:bg-brand-600 transition-colors">' +
            '📷 카메라로 스캔하기</button>' +
            '<div class="flex items-center gap-2 text-[11px] text-gray-400"><div class="flex-1 h-px bg-gray-100"></div><span>또는 직접 입력</span><div class="flex-1 h-px bg-gray-100"></div></div>' +
            '<div class="flex gap-2">' +
            '<input id="bs-code" type="text" inputmode="numeric" pattern="[0-9]*" placeholder="바코드 번호 (예: 3182550702126)" ' +
            'onkeydown="if(event.key===\'Enter\')FoodScanner.lookupManual()" ' +
            'class="flex-1 rounded-xl border border-gray-200 px-3 py-2.5 text-sm focus:outline-none focus:border-brand-400" />' +
            '<button onclick="FoodScanner.lookupManual()" class="px-4 rounded-xl bg-gray-100 text-gray-700 text-sm font-bold hover:bg-gray-200">조회</button></div>' +
            '<div id="bs-result"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100">' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 성분 정보는 Open Pet Food Facts 공개 DB 기반이라 누락·부정확할 수 있어요. 실제 제품 라벨을 반드시 함께 확인하고, 알러지 대조는 참고용입니다.</p>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function startCamera() {
        if (!("BarcodeDetector" in window)) {
            toast("이 브라우저는 카메라 스캔을 지원하지 않아요 — 번호를 직접 입력해 주세요.");
            return;
        }
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            toast("카메라를 사용할 수 없어요 — 번호를 직접 입력해 주세요.");
            return;
        }
        var wrap = document.getElementById("bs-cam-wrap");
        var btn = document.getElementById("bs-cam-btn");
        if (wrap) wrap.classList.remove("hidden");
        if (btn) { btn.disabled = true; btn.textContent = "카메라 여는 중…"; }
        try { detector = new window.BarcodeDetector({ formats: FORMATS }); }
        catch (e) { detector = new window.BarcodeDetector(); }
        navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } })
            .then(function (s) {
                stream = s;
                var video = document.getElementById("bs-video");
                if (!video) { stopCamera(); return; }
                video.srcObject = s;
                return video.play();
            })
            .then(function () { scanLoop(); })
            .catch(function () {
                stopCamera();
                if (btn) { btn.disabled = false; btn.textContent = "📷 카메라로 스캔하기"; }
                toast("카메라 접근이 거부됐어요 — 번호를 직접 입력해 주세요.");
            });
    }

    function scanLoop() {
        var video = document.getElementById("bs-video");
        if (!detector || !video || !stream) return;
        detector.detect(video)
            .then(function (codes) {
                if (codes && codes.length && isValidCode(codes[0].rawValue)) {
                    var code = String(codes[0].rawValue).trim();
                    stopCamera();
                    var wrap = document.getElementById("bs-cam-wrap");
                    if (wrap) wrap.classList.add("hidden");
                    lookup(code);
                    return;
                }
                rafId = requestAnimationFrame(scanLoop);
            })
            .catch(function () { rafId = requestAnimationFrame(scanLoop); });
    }

    function stopCamera() {
        if (rafId) { cancelAnimationFrame(rafId); rafId = null; }
        if (stream) { stream.getTracks().forEach(function (t) { t.stop(); }); stream = null; }
        detector = null;
    }

    function lookupManual() {
        var input = document.getElementById("bs-code");
        var code = input ? String(input.value || "").trim() : "";
        if (!isValidCode(code)) { toast("바코드 번호(숫자 6~14자리)를 정확히 입력해 주세요."); return; }
        lookup(code);
    }

    function setResult(html) { var el = document.getElementById("bs-result"); if (el) el.innerHTML = html; }

    function lookup(code) {
        setResult('<p class="text-center text-xs text-gray-400 py-6">🔎 성분 정보를 조회하는 중…</p>');
        fetch(API + encodeURIComponent(code) + ".json?fields=" + FIELDS)
            .then(function (r) { if (!r.ok) throw new Error("http"); return r.json(); })
            .then(function (data) {
                if (!data || data.status !== 1 || !data.product) { renderNotFound(code); return; }
                renderProduct(code, data.product);
            })
            .catch(function () {
                setResult('<div class="rounded-xl border border-gray-100 bg-gray-50 px-4 py-4 text-center">' +
                    '<p class="text-sm font-bold text-gray-700">조회에 실패했어요</p>' +
                    '<p class="text-[11px] text-gray-500 mt-1">네트워크를 확인하고 다시 시도하거나, 제품 라벨을 직접 확인해 주세요.</p></div>');
            });
    }

    function renderNotFound(code) {
        setResult('<div class="rounded-xl border border-amber-100 bg-amber-50 px-4 py-4">' +
            '<p class="text-sm font-bold text-amber-800">등록되지 않은 제품이에요</p>' +
            '<p class="text-[11px] text-amber-700 mt-1">바코드 <b>' + esc(code) + '</b>가 Open Pet Food Facts DB에 없어요. 제품 라벨의 성분표를 직접 확인해 주세요.</p></div>');
    }

    function renderProduct(code, p) {
        var name = p.product_name_ko || p.product_name || "이름 미상 제품";
        var brand = p.brands || "";
        var ingredients = p.ingredients_text_ko || p.ingredients_text || "";
        var img = p.image_front_small_url || "";
        var hits = allergyHits(ingredients);

        var head = '<div class="flex items-center gap-3 mb-2">' +
            (img ? '<img src="' + esc(img) + '" alt="" class="w-12 h-12 rounded-lg object-cover border border-gray-100" />' : '<div class="w-12 h-12 rounded-lg bg-gray-100 flex items-center justify-center text-xl">📦</div>') +
            '<div class="min-w-0"><p class="text-sm font-bold text-gray-900 truncate">' + esc(name) + '</p>' +
            (brand ? '<p class="text-[11px] text-gray-500 truncate">' + esc(brand) + '</p>' : "") + "</div></div>";

        var warn = "";
        if (hits.length) {
            warn = '<div class="rounded-xl border border-rose-200 bg-rose-50 px-3 py-2.5 mb-2">' +
                '<p class="text-xs font-bold text-rose-700">⚠️ 등록된 알러지/기피 성분과 겹쳐요</p>' +
                '<p class="text-[11px] text-rose-700 mt-1">겹치는 성분: <b>' + hits.map(esc).join(", ") + '</b> — 급여 전 반드시 확인하세요.</p></div>';
        } else if (petAllergens().length) {
            warn = '<div class="rounded-xl border border-emerald-100 bg-emerald-50 px-3 py-2 mb-2">' +
                '<p class="text-[11px] font-bold text-emerald-700">✅ 등록된 알러지/기피 성분은 발견되지 않았어요 (성분표 기준).</p></div>';
        } else {
            warn = '<div class="rounded-xl border border-gray-100 bg-gray-50 px-3 py-2 mb-2">' +
                '<p class="text-[11px] text-gray-500">프로필에 알러지/기피 성분을 등록하면 자동으로 대조해 드려요.</p></div>';
        }

        var ing = ingredients
            ? '<div class="rounded-xl border border-gray-100 px-3 py-2.5"><p class="text-[11px] font-bold text-gray-500 mb-1">성분표</p>' +
              '<p class="text-[11px] text-gray-700 leading-relaxed">' + esc(ingredients) + "</p></div>"
            : '<p class="text-[11px] text-gray-400">이 제품은 성분표 정보가 아직 등록되지 않았어요 — 제품 라벨을 확인해 주세요.</p>';

        setResult('<div class="mt-1">' + head + warn + ing + "</div>");
    }

    function close() {
        stopCamera();
        var el = document.getElementById("bs-overlay");
        if (el) el.remove();
    }

    window.FoodScanner = {
        open: open, close: close, startCamera: startCamera,
        lookupManual: lookupManual, _allergyHits: allergyHits, _isValidCode: isValidCode
    };
})();
