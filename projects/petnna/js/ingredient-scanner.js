// 🧪 사료·간식 성분 안전 스캐너 — 성분표 텍스트 붙여넣기 → 알러지·기저질환 교차검증 (나무 백로그 P3)
// ─────────────────────────────────────────────────────────────
// 제품 라벨의 성분표를 그대로 붙여넣으면(또는 직접 입력) 개별 성분으로 나눠
//  ① 반려동물 독성 성분(양파·초콜릿·자일리톨 등)
//  ② 프로필에 등록된 알러지/기피 성분(pet.allergies)
//  ③ 응급정보에 등록된 기저질환(신장·췌장·당뇨·심장)과 충돌하는 성분
// 을 대조해 위험/주의 성분을 표시하고 종합 안전 등급(안전·주의·위험)을 준다.
//
// 가드레일:
//  · 프론트 전용 — 백엔드/DB/신규 테이블/키 무접촉. 대조는 결정론적 정적 규칙.
//  · 새 라이브러리 없음. 안전 판정은 재현 가능해야 하므로 LLM 비결정성을 배제하고
//    FoodSafety·FoodScanner와 동일한 알러지/질환 파싱 규칙을 재사용한다.
//  · 참고용 — 성분 DB가 불완전할 수 있으니 실제 제품 라벨·수의사 판단을 우선한다.
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    function esc(s) {
        if (typeof window.escapeHtml === "function") return window.escapeHtml(s);
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }
    function toast(msg) { if (typeof showToast === "function") showToast(msg); }

    // 반려동물 독성/위험 성분(성분표에 있으면 안 되는 것) — 부분일치용 소문자 패턴.
    var TOXIC = [
        { re: /양파|대파|쪽파|onion|양파분말/, label: "양파", note: "적혈구 파괴(빈혈) — 익혀도 위험" },
        { re: /마늘|garlic/, label: "마늘", note: "양파 계열 독성 — 소량 반복도 누적 위험" },
        { re: /초콜릿|코코아|카카오|chocolate|cocoa/, label: "초콜릿/코코아", note: "테오브로민·카페인 중독" },
        { re: /자일리톨|xylitol|자일리톨/, label: "자일리톨", note: "개 저혈당·간부전 — 무설탕 감미료 확인" },
        { re: /포도|건포도|raisin|grape/, label: "포도/건포도", note: "급성 신부전 유발" },
        { re: /마카다미아|macadamia/, label: "마카다미아", note: "무기력·구토·발열·후지 마비" },
        { re: /카페인|커피|caffeine|coffee/, label: "카페인", note: "심장·신경 자극" },
        { re: /알코올|alcohol|에탄올/, label: "알코올", note: "소량도 중독" },
        { re: /호두|walnut/, label: "호두", note: "곰팡이 독소·질식 위험" }
    ];

    // 논란 있는 첨가물 — 있으면 '주의'(금지는 아님).
    var ADDITIVE = [
        { re: /\bbha\b|부틸히드록시아니솔/, label: "BHA" },
        { re: /\bbht\b|부틸히드록시톨루엔/, label: "BHT" },
        { re: /에톡시퀸|ethoxyquin/, label: "에톡시퀸" },
        { re: /프로필렌\s?글리콜|propylene\s?glycol/, label: "프로필렌글리콜(고양이 주의)" },
        { re: /인공\s?색소|착색료|적색\s?\d+|황색\s?\d+|청색\s?\d+|red\s?\d+|yellow\s?\d+|blue\s?\d+/, label: "인공색소" },
        { re: /합성\s?향료|인공\s?향료|착향료/, label: "합성향료" },
        { re: /아질산나트륨|sodium\s?nitrite/, label: "아질산나트륨(발색제)" }
    ];

    // 기저질환 → 식이 위험 성분 규칙(FoodSafety와 동형).
    var CONDITION_RULES = [
        { cond: /신장|신부전|콩팥|kidney/i, risk: /소금|짠|나트륨|염분|고염|인산|phosph/i, warn: "나트륨·인이 높으면 신장 부담" },
        { cond: /췌장|이자염|pancrea/i, risk: /지방|기름|비계|고지방|버터|우지|돈지|fat|lard/i, warn: "고지방은 췌장염을 악화" },
        { cond: /당뇨|혈당|diabet/i, risk: /당|설탕|시럽|꿀|과당|포도당|sugar|syrup|글루코스/i, warn: "당분이 혈당을 급격히 올림" },
        { cond: /심장|심부전|heart/i, risk: /소금|짠|나트륨|염분|고염/i, warn: "나트륨 제한이 필요" }
    ];

    // 활성 펫의 알러지/기피 성분 → 소문자 토큰 배열(FoodScanner와 동일 규칙).
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

    // 활성 펫 기저질환 텍스트(응급정보 chronic, 자유입력).
    function petConditionText() {
        var pet = (typeof window.getActivePet === "function") ? window.getActivePet() : null;
        if (!pet) return "";
        var em = (window.PetPassportRender && typeof window.PetPassportRender.getEmergency === "function")
            ? window.PetPassportRender.getEmergency(pet) : null;
        return em && em.chronic ? String(em.chronic) : "";
    }

    // 성분표 텍스트 → 종합 판정. 순수 함수(테스트 가능).
    function analyze(text) {
        var raw = String(text || "");
        var hay = raw.toLowerCase();
        var findings = [];   // { level:"danger"|"caution", label, note }

        TOXIC.forEach(function (t) {
            if (t.re.test(hay)) findings.push({ level: "danger", label: t.label, note: t.note, kind: "toxic" });
        });

        // 알러지: 등록된 성분이 성분표에 등장하면 그 펫에겐 위험.
        petAllergens().forEach(function (tok) {
            if (hay.indexOf(tok) !== -1) {
                findings.push({ level: "danger", label: tok, note: "프로필에 등록된 알러지/기피 성분", kind: "allergy" });
            }
        });

        // 기저질환 충돌.
        var condTxt = petConditionText();
        if (condTxt) {
            CONDITION_RULES.forEach(function (r) {
                if (r.cond.test(condTxt) && r.risk.test(raw)) {
                    findings.push({ level: "caution", label: "기저질환 주의", note: r.warn, kind: "condition" });
                }
            });
        }

        ADDITIVE.forEach(function (a) {
            if (a.re.test(hay)) findings.push({ level: "caution", label: a.label, note: "논란 있는 첨가물", kind: "additive" });
        });

        var hasDanger = findings.some(function (f) { return f.level === "danger"; });
        var hasCaution = findings.some(function (f) { return f.level === "caution"; });
        var grade = hasDanger ? "danger" : (hasCaution ? "caution" : "safe");
        return { grade: grade, findings: findings, empty: raw.trim().length === 0 };
    }

    var GRADES = {
        safe: { label: "안전", cls: "text-emerald-700 bg-emerald-50 border-emerald-200", dot: "🟢", msg: "확인된 독성·알러지·질환 충돌 성분이 없어요." },
        caution: { label: "주의", cls: "text-amber-700 bg-amber-50 border-amber-200", dot: "🟡", msg: "주의가 필요한 성분이 있어요 — 아래를 확인하세요." },
        danger: { label: "위험", cls: "text-rose-700 bg-rose-50 border-rose-200", dot: "🔴", msg: "위험 성분이 감지됐어요 — 급여 전 반드시 확인하세요." }
    };

    function findingRow(f) {
        var isDanger = f.level === "danger";
        var cls = isDanger ? "border-rose-100 bg-rose-50/50" : "border-amber-100 bg-amber-50/40";
        var tag = isDanger ? "🔴" : "🟡";
        return '<div class="rounded-xl border px-3 py-2 ' + cls + '">' +
            '<p class="text-xs font-bold ' + (isDanger ? "text-rose-700" : "text-amber-700") + '">' + tag + " " + esc(f.label) + "</p>" +
            '<p class="text-[11px] text-gray-600 mt-0.5">' + esc(f.note) + "</p></div>";
    }

    function setResult(html) { var el = document.getElementById("is-result"); if (el) el.innerHTML = html; }

    function scan() {
        var input = document.getElementById("is-input");
        var text = input ? String(input.value || "") : "";
        var res = analyze(text);
        if (res.empty) { toast("성분표 텍스트를 입력해 주세요."); return; }

        var G = GRADES[res.grade];
        var head = '<div class="rounded-xl border px-4 py-3 ' + G.cls + '">' +
            '<p class="text-sm font-extrabold">' + G.dot + " 안전 등급: " + G.label + "</p>" +
            '<p class="text-[11px] mt-1 leading-relaxed">' + esc(G.msg) + "</p></div>";

        var body = "";
        if (res.findings.length) {
            body = '<div class="space-y-1.5 mt-2">' + res.findings.map(findingRow).join("") + "</div>";
        } else {
            body = '<p class="text-[11px] text-gray-500 mt-2">프로필에 알러지·기저질환을 등록하면 우리 아이 맞춤으로 더 정확히 대조해 드려요.</p>';
        }
        setResult(head + body);
    }

    function open() {
        close();
        var overlay = document.createElement("div");
        overlay.id = "is-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100">' +
            '<h3 class="text-base font-extrabold text-gray-900">🧪 성분 안전 스캐너</h3>' +
            '<button onclick="IngredientScanner.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-3 overflow-y-auto flex-1">' +
            '<p class="text-[11px] text-gray-500 leading-relaxed">제품 뒷면의 <b>성분표를 그대로 붙여넣으면</b> 독성 성분과 우리 아이의 알러지·기저질환을 대조해 드려요.</p>' +
            '<textarea id="is-input" rows="4" placeholder="예: 닭고기, 현미, 완두콩, 양파분말, 어유, 비타민E, BHA…" ' +
            'class="w-full rounded-xl border border-gray-200 px-3 py-2.5 text-sm resize-none focus:outline-none focus:border-brand-400"></textarea>' +
            '<button onclick="IngredientScanner.scan()" ' +
            'class="w-full py-2.5 rounded-xl bg-brand-500 text-white text-sm font-bold hover:bg-brand-600 transition-colors">🔎 성분 검사하기</button>' +
            '<div id="is-result"></div>' +
            "</div>" +
            '<div class="px-5 py-3 border-t border-gray-100">' +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 결정론적 성분 규칙 기반 참고용이에요. 성분 표기·개체 상태에 따라 다를 수 있으니 실제 제품 라벨과 수의사 판단을 우선하세요.</p>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);
    }

    function close() { var el = document.getElementById("is-overlay"); if (el) el.remove(); }

    window.IngredientScanner = { open: open, close: close, scan: scan, _analyze: analyze };
})();
