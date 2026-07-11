// 🩺 증상 빠른 진단 (트리아지) — 회의 202607111333 채택안(나무_20260708_4)
// ─────────────────────────────────────────────────────────────
// 반려동물 증상을 고르면 '이 아이 기준' 긴급도(집관찰/내원권장/응급)를 즉시 안내한다.
// 견종·나이·체중을 반영해 개인화(예: 노령·초소형견·단두종은 상향).
//
// 회의 가드레일(minutes_20260711_1333) 준수:
//  · 프론트 전용 — 백엔드/DB/신규 테이블 무접촉. pet 데이터는 읽기만(비파괴).
//  · 결정론적 규칙 기반 — LLM 비결정성 배제(테오 우려 수용).
//  · 의료 면책 문구 필수 + 응급 과소경고 방지(불확실하면 상향).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var L = { home: 1, vet: 2, emerg: 3 };
    var LEVEL_META = {
        1: { key: "home",  label: "집에서 관찰", emoji: "🏠", cls: "bg-emerald-50 text-emerald-700 border-emerald-200",
             desc: "당장 위급해 보이진 않아요. 아래 관찰 포인트를 지키며 지켜봐 주세요." },
        2: { key: "vet",   label: "내원 권장",   emoji: "🏥", cls: "bg-amber-50 text-amber-700 border-amber-200",
             desc: "가까운 시일 내 동물병원 진료를 권해요. 증상이 악화되면 바로 내원하세요." },
        3: { key: "emerg", label: "응급 · 즉시 병원", emoji: "🚨", cls: "bg-rose-50 text-rose-700 border-rose-200",
             desc: "지체하지 말고 지금 바로 동물병원/응급실로 가세요." },
    };

    // 증상 칩: base=기본 긴급도, brachy/gi/resp=개인화 플래그
    var SYMPTOMS = [
        { id: "breath",  label: "호흡곤란·헐떡임", base: L.emerg, resp: true },
        { id: "seizure", label: "경련·발작",       base: L.emerg },
        { id: "collapse",label: "쓰러짐·의식저하", base: L.emerg },
        { id: "bloat",   label: "배가 부풂·헛구역", base: L.emerg },
        { id: "blood",   label: "피 토함·혈변",     base: L.emerg },
        { id: "nourine", label: "소변을 못 봄",     base: L.emerg },
        { id: "poison",  label: "이물·중독 의심 섭취", base: L.emerg },
        { id: "vomit",   label: "구토",   base: L.vet, gi: true },
        { id: "diarr",   label: "설사",   base: L.vet, gi: true },
        { id: "noapp",   label: "식욕부진", base: L.vet, gi: true },
        { id: "limp",    label: "절뚝임·통증", base: L.vet },
        { id: "cough",   label: "기침 지속", base: L.vet, resp: true },
        { id: "itch",    label: "심한 가려움·피부", base: L.vet },
        { id: "eye",     label: "눈·귀 분비물", base: L.vet },
        { id: "lethar",  label: "무기력·처짐", base: L.vet },
        { id: "sneeze",  label: "가벼운 재채기", base: L.home },
        { id: "softst",  label: "무른 변 1회", base: L.home },
    ];

    // 자유입력 응급 키워드(오탐보다 과소경고 방지 우선)
    var RED_FLAGS = ["호흡", "숨", "헐떡", "경련", "발작", "쓰러", "의식", "피", "혈변", "토혈",
        "중독", "초콜릿", "포도", "양파", "자일리톨", "삼켰", "부풀", "팽만", "소변", "청색", "보라색 잇몸"];

    // 단두종(호흡기 취약) 견·묘종
    var BRACHY = ["시츄", "시추", "퍼그", "불독", "불도그", "페키니즈", "보스턴", "복서", "보스톤",
        "페르시안", "히말라얀", "엑조틱"];

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
    function parseAgeYears(age) {
        if (!age) return null;
        var s = String(age);
        if (/개월|month/i.test(s)) return (parseFloat(s) || 0) / 12;
        var m = s.match(/[\d.]+/);
        return m ? parseFloat(m[0]) : null;
    }
    function parseWeightKg(w) {
        if (w == null) return null;
        var m = String(w).match(/[\d.]+/);
        return m ? parseFloat(m[0]) : null;
    }
    function isBrachy(breed) {
        var b = String(breed || "");
        return BRACHY.some(function (k) { return b.indexOf(k) !== -1; });
    }

    // 핵심: 선택 증상 + 자유입력 + 펫 특성 → 최종 긴급도와 개인화 사유
    function triage(pet, selectedIds, freeText) {
        var reasons = [];
        var level = L.home;
        var picks = SYMPTOMS.filter(function (s) { return selectedIds.indexOf(s.id) !== -1; });

        picks.forEach(function (s) { if (s.base > level) level = s.base; });

        // 자유입력 응급 키워드 → 상향(과소경고 방지)
        var ft = String(freeText || "");
        if (ft && RED_FLAGS.some(function (k) { return ft.indexOf(k) !== -1; })) {
            if (level < L.emerg) reasons.push("입력하신 내용에 응급 징후로 볼 만한 표현이 있어 안전하게 상향했어요.");
            level = L.emerg;
        }

        var ageY = parseAgeYears(pet && pet.age);
        var wKg = parseWeightKg(pet && pet.weight);
        var brachy = isBrachy(pet && pet.breed);
        var hasGI = picks.some(function (s) { return s.gi; });
        var hasResp = picks.some(function (s) { return s.resp; });

        // 개인화 상향 규칙 (내원권장 이하일 때만 — 이미 응급이면 그대로)
        if (level < L.emerg) {
            if (brachy && hasResp) {
                level = L.emerg;
                reasons.push((esc(pet.breed) || "단두종") + "은(는) 호흡기가 취약한 단두종이라 호흡 증상은 응급으로 봐야 해요.");
            }
            if (ageY != null && ageY >= 8 && level < L.vet && picks.length) {
                level = L.vet;
                reasons.push("노령(추정 " + ageY.toFixed(0) + "살)이라 같은 증상도 더 주의가 필요해 내원 권장으로 올렸어요.");
            }
            if (wKg != null && wKg > 0 && wKg < 5 && hasGI && level < L.emerg) {
                level = L.emerg;
                reasons.push("체중 " + wKg + "kg의 초소형견/묘는 구토·설사 시 탈수·저혈당 위험이 커 빠른 진료가 안전해요.");
            }
            if (ageY != null && ageY < 1 && hasGI && level < L.emerg) {
                level = L.emerg;
                reasons.push("아직 어려(1살 미만) 소화기 증상에 취약해 응급으로 안내해요.");
            }
        }
        if (!picks.length && !ft) reasons.push("증상을 하나 이상 선택하거나 적어 주세요.");
        return { level: level, reasons: reasons, picks: picks };
    }

    // ── UI ────────────────────────────────────────────────────
    function open() {
        var pet = activePet();
        if (!pet) { if (typeof showToast === "function") showToast("먼저 반려동물을 등록해 주세요 🐾"); return; }
        close();

        var chips = SYMPTOMS.map(function (s) {
            return '<button type="button" class="st-chip text-xs px-3 py-1.5 rounded-full border border-gray-200 ' +
                'text-gray-600 hover:border-brand-300 hover:bg-brand-50 transition-all" data-id="' + s.id + '">' +
                esc(s.label) + "</button>";
        }).join("");

        // age가 이미 "1살"처럼 단위를 품을 수 있어 숫자만일 때만 '살'을 붙인다(이중 '살살' 방지).
        var ageStr = pet.age ? (/^[\d.]+$/.test(String(pet.age).trim()) ? pet.age + "살" : String(pet.age)) : null;
        var petLine = [pet.breed, ageStr, pet.weight ? pet.weight + "kg" : null]
            .filter(Boolean).join(" · ") || "정보 미등록";

        var overlay = document.createElement("div");
        overlay.id = "st-overlay";
        overlay.className = "fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🩺 증상 빠른 진단</h3>' +
            '<button onclick="SymptomTriage.close()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' +
            '<div class="rounded-xl bg-brand-50 px-3 py-2 text-xs text-brand-700"><b>' + esc(pet.name) + '</b> · ' + esc(petLine) +
            ' <span class="text-brand-400">기준으로 판단해요</span></div>' +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">해당하는 증상을 모두 선택</p>' +
            '<div id="st-chips" class="flex flex-wrap gap-1.5">' + chips + "</div></div>" +
            '<div><label class="text-xs font-bold text-gray-500">추가 설명(선택)</label>' +
            '<textarea id="st-free" rows="2" placeholder="예) 어제 저녁부터 밥을 안 먹고 축 처져 있어요" ' +
            'class="mt-1 w-full rounded-xl border border-gray-200 px-3 py-2 text-sm focus:border-brand-400 focus:outline-none resize-none"></textarea></div>' +
            '<div id="st-result" class="hidden"></div>' +
            '</div>' +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 sticky bottom-0 bg-white">' +
            '<button onclick="SymptomTriage.run()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">긴급도 확인</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) close(); });
        document.body.appendChild(overlay);

        overlay.querySelectorAll(".st-chip").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var on = btn.classList.toggle("st-on");
                btn.classList.toggle("bg-brand-500", on);
                btn.classList.toggle("text-white", on);
                btn.classList.toggle("border-brand-500", on);
                btn.classList.toggle("text-gray-600", !on);
            });
        });
    }

    function run() {
        var pet = activePet();
        if (!pet) return;
        var selected = [];
        document.querySelectorAll("#st-chips .st-chip.st-on").forEach(function (b) { selected.push(b.dataset.id); });
        var free = (document.getElementById("st-free") || {}).value || "";
        var r = triage(pet, selected, free);
        var meta = LEVEL_META[r.level];
        var box = document.getElementById("st-result");
        if (!box) return;
        box.classList.remove("hidden");

        var reasonsHtml = r.reasons.length
            ? '<ul class="mt-2 space-y-1">' + r.reasons.map(function (x) {
                return '<li class="text-xs text-gray-600 flex gap-1.5"><span class="text-brand-400">•</span><span>' + esc(x) + "</span></li>";
            }).join("") + "</ul>" : "";

        box.innerHTML =
            '<div class="rounded-2xl border px-4 py-4 ' + meta.cls + '">' +
            '<div class="flex items-center gap-2"><span class="text-2xl">' + meta.emoji + "</span>" +
            '<span class="text-base font-black">' + meta.label + "</span></div>" +
            '<p class="text-xs mt-1.5 leading-relaxed">' + meta.desc + "</p>" +
            reasonsHtml + "</div>" +
            '<div class="mt-3 rounded-xl bg-gray-50 px-3 py-2.5">' +
            '<p class="text-[11px] text-gray-500 leading-relaxed">⚠️ 이 안내는 보호자의 초기 판단을 돕는 <b>참고용</b>이며 ' +
            '수의사의 진료·진단을 대체하지 않아요. 조금이라도 걱정되면 병원에 문의하세요.</p></div>' +
            (typeof openVetChatModal === "function"
                ? '<button onclick="SymptomTriage.close(); openVetChatModal();" class="mt-3 w-full rounded-xl border border-gray-200 hover:bg-gray-50 py-2 text-xs font-bold text-gray-600">AI 수의사에게 자세히 물어보기 →</button>'
                : "");
        box.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function close() { var el = document.getElementById("st-overlay"); if (el) el.remove(); }

    window.SymptomTriage = { open: open, run: run, close: close, _triage: triage };
})();
