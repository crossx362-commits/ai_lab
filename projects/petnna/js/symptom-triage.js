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

    var lastResult = null; // 마지막 진단 결과(건강수첩 저장·리마인더 생성에 사용)
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

    // 자유입력 응급 키워드(오탐보다 과소경고 방지 우선 — 단, 1글자 토큰은 "피곤"·"숨바꼭질"·
    // "피부" 같은 벤나인 단어에 부분일치로 오발동해 코드리뷰에서 실측 확인 후 복합어로 교체)
    var RED_FLAGS = ["호흡곤란", "숨을 못", "숨이 가빠", "가쁜 숨", "쌕쌕", "헐떡",
        "경련", "발작", "쓰러졌", "쓰러져", "의식이 없", "의식을 잃",
        "출혈", "피가 나", "코피", "혈변", "토혈", "피를 토",
        "중독", "초콜릿", "포도", "양파", "자일리톨", "삼켰", "삼킨",
        "배가 부풀", "복부팽만", "소변을 못", "소변이 안 나",
        "청색증", "잇몸이 하얗", "잇몸이 창백"];

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
        lastResult = { pet: pet, result: r, free: free };
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
            ((r.picks.length || String(free).trim())
                ? '<button onclick="SymptomTriage.saveToRecord()" class="mt-3 w-full rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-xs font-bold">📋 건강수첩에 저장 · 재방문 알림 받기</button>'
                + '<button onclick="SymptomTriage.openPrepCard()" class="mt-2 w-full rounded-xl border border-brand-200 hover:bg-brand-50 py-2 text-xs font-bold text-brand-600">🏥 병원 방문 준비 카드 만들기</button>'
                : "") +
            (typeof openVetChatModal === "function"
                ? '<button onclick="SymptomTriage.close(); openVetChatModal();" class="mt-3 w-full rounded-xl border border-gray-200 hover:bg-gray-50 py-2 text-xs font-bold text-gray-600">AI 수의사에게 자세히 물어보기 →</button>'
                : "");
        box.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    function close() { var el = document.getElementById("st-overlay"); if (el) el.remove(); }

    // ── 병원 방문 준비 카드 (나무_20260708 백로그) ─────────────────
    // 트리아지 결과 + 증상별 질문 체크리스트 + 최근 접종/투약을 묶어
    // 수의사에게 그대로 보여줄 수 있는 요약 카드를 만든다. 결정론적 규칙 기반.
    var VET_QUESTIONS = {
        breath:  ["헐떡임/숨찬 증상이 언제부터, 쉴 때도 나타나나요?"],
        cough:   ["기침 소리(마른/젖은)와 하루 중 심해지는 시간대는?"],
        vomit:   ["구토 횟수·내용물(음식/거품/피)과 물은 마시는지?"],
        diarr:   ["설사 횟수·색·혈액 여부와 식욕은 어떤지?"],
        noapp:   ["마지막으로 잘 먹은 시점과 물 섭취량은?"],
        limp:    ["절뚝임이 어느 다리·언제 시작됐고 만지면 아파하나요?"],
        seizure: ["발작 지속 시간·빈도와 전후 행동 변화는?"],
        blood:   ["출혈 부위·양과 색(선홍/검붉음)은?"],
        itch:    ["가려워하는 부위와 피부 발적·탈모 여부는?"],
        eye:     ["분비물 색·양과 눈/귀를 비비는지?"],
        poison:  ["섭취한 물질·추정량과 섭취 시각은?"],
    };
    var VET_QUESTIONS_GENERAL = [
        "증상이 처음 나타난 시점과 이후 변화 양상",
        "평소와 다른 식사·배변·활동량 변화",
        "현재 먹이는 사료·간식·영양제/복용 중인 약",
        "최근 환경 변화(이사·새 음식·산책지 등)",
    ];

    function recentMeds() {
        var list = (typeof getMedicalRecordsForActivePet === "function") ? getMedicalRecordsForActivePet() : [];
        if (!Array.isArray(list)) return [];
        return list.filter(function (r) {
            return r && (r.category === "vaccine" || r.category === "medication");
        }).slice(0, 4);
    }

    function buildChecklist(picks) {
        var qs = [];
        picks.forEach(function (s) {
            var extra = VET_QUESTIONS[s.id];
            if (extra) extra.forEach(function (q) { if (qs.indexOf(q) === -1) qs.push(q); });
        });
        VET_QUESTIONS_GENERAL.forEach(function (q) { if (qs.indexOf(q) === -1) qs.push(q); });
        return qs;
    }

    function medLabel(r) {
        var cat = r.category === "vaccine" ? "💉 접종" : "💊 투약";
        var name = r.diagnosis || r.hospital || (r.category === "vaccine" ? "접종" : "처방");
        return cat + " " + name + (r.visitDate ? " · " + r.visitDate : "");
    }

    // ── 건강 지표 수집(체중 추세·BCS·QoL) — 각 모듈의 localStorage에서 읽음 ──
    function _petId() { var p = activePet(); return p ? p.id : null; }

    function qolHistory() {
        var id = _petId();
        if (id == null) return [];
        try { return (JSON.parse(localStorage.getItem("petna_qol_checkins")) || {})[String(id)] || []; }
        catch (e) { return []; }
    }

    function latestQol() {
        var h = qolHistory();
        if (!h.length) return null;
        var last = h[h.length - 1];
        return { total: last.total, date: String(last.date || "").slice(0, 10) };
    }

    function latestBcs() {
        var d = window.BcsWizardData, id = _petId();
        if (!d || id == null) return null;
        var h = d.history(id);
        if (!h || !h.length) return null;
        var last = h[h.length - 1];
        return { score: last.score, cat: d.classify(last.score), date: String(last.date || "").slice(0, 10) };
    }

    function weightTrend() {
        var pts = qolHistory()
            .filter(function (r) { return r.weight != null && r.weight !== "" && !isNaN(parseFloat(r.weight)); })
            .map(function (r) { return parseFloat(r.weight); });
        if (!pts.length) {
            var pet = activePet(), pw = pet ? parseFloat(pet.weight) : NaN;
            if (!isNaN(pw)) pts.push(pw);
        }
        if (!pts.length) return null;
        return { latest: pts[pts.length - 1], delta: +(pts[pts.length - 1] - pts[0]).toFixed(1), count: pts.length };
    }

    function healthMetricLines() {
        var lines = [], w = weightTrend(), b = latestBcs(), q = latestQol();
        if (w) {
            lines.push("체중: " + w.latest + "kg"
                + (w.count > 1 ? " (이전 대비 " + (w.delta >= 0 ? "+" : "") + w.delta + "kg · " + w.count + "회 기록)" : ""));
        }
        if (b) lines.push("BCS: " + b.score.toFixed(1) + "/5.0 · " + b.cat.label + (b.date ? " (" + b.date + ")" : ""));
        if (q) lines.push("QoL(삶의 질): " + Number(q.total).toFixed(1) + "/5.0" + (q.date ? " (" + q.date + ")" : ""));
        return lines;
    }

    function cardText() {
        if (!lastResult || !lastResult.pet) return "";
        var pet = lastResult.pet, r = lastResult.result, free = String(lastResult.free || "").trim();
        var meta = LEVEL_META[r.level];
        var symptomText = r.picks.map(function (s) { return s.label; }).join(", ");
        var lines = [
            "🏥 병원 방문 준비 카드 — " + (pet.name || "우리 아이"),
            "긴급도: " + meta.emoji + " " + meta.label,
            "증상: " + (symptomText || "-") + (free ? " / " + free : ""),
        ];
        var metrics = healthMetricLines();
        if (metrics.length) {
            lines.push("");
            lines.push("[건강 지표]");
            metrics.forEach(function (m) { lines.push("· " + m); });
        }
        lines.push("");
        lines.push("[수의사에게 물어볼 것]");
        buildChecklist(r.picks).forEach(function (q, i) { lines.push((i + 1) + ". " + q); });
        var meds = recentMeds();
        if (meds.length) {
            lines.push("");
            lines.push("[최근 접종/투약]");
            meds.forEach(function (m) { lines.push("· " + medLabel(m).replace(/^[💉💊]\s*/, "").replace(/^접종\s|^투약\s/, "")); });
        }
        lines.push("");
        lines.push("※ 참고용 요약이며 수의사 진료를 대체하지 않습니다. — 펫과나");
        return lines.join("\n");
    }

    function openPrepCard() {
        if (!lastResult || !lastResult.pet) return;
        var pet = lastResult.pet, r = lastResult.result, free = String(lastResult.free || "").trim();
        var meta = LEVEL_META[r.level];
        var symptomText = r.picks.map(function (s) { return s.label; }).join(", ");
        var checklistHtml = buildChecklist(r.picks).map(function (q) {
            return '<li class="flex gap-2 text-xs text-gray-700"><span class="text-brand-400 mt-0.5">☐</span><span>' + esc(q) + "</span></li>";
        }).join("");
        var meds = recentMeds();
        var medsHtml = meds.length
            ? '<div class="flex flex-wrap gap-1.5">' + meds.map(function (m) {
                return '<span class="inline-flex items-center bg-white border border-gray-200 text-[11px] font-bold text-gray-600 px-2 py-0.5 rounded-full">' + esc(medLabel(m)) + "</span>";
            }).join("") + "</div>"
            : '<p class="text-xs text-gray-400">기록된 접종/투약 정보가 없어요.</p>';
        var metrics = healthMetricLines();
        var metricsHtml = metrics.length
            ? '<div class="flex flex-wrap gap-1.5">' + metrics.map(function (m) {
                return '<span class="inline-flex items-center bg-brand-50 border border-brand-100 text-[11px] font-bold text-brand-700 px-2 py-0.5 rounded-full">' + esc(m) + "</span>";
            }).join("") + "</div>"
            : '<p class="text-xs text-gray-400">기록된 체중·BCS·QoL 데이터가 없어요.</p>';

        var old = document.getElementById("st-prep-overlay");
        if (old) old.remove();
        var overlay = document.createElement("div");
        overlay.id = "st-prep-overlay";
        overlay.className = "fixed inset-0 z-[10000] flex items-center justify-center bg-black/40 p-4";
        overlay.innerHTML =
            '<div class="w-full max-w-md max-h-[90vh] overflow-y-auto rounded-2xl bg-white shadow-2xl">' +
            '<div class="flex items-center justify-between px-5 py-3 border-b border-gray-100 sticky top-0 bg-white">' +
            '<h3 class="text-base font-extrabold text-gray-900">🏥 병원 방문 준비 카드</h3>' +
            '<button onclick="SymptomTriage.closePrepCard()" class="text-gray-300 hover:text-gray-500 text-xl leading-none">&times;</button></div>' +
            '<div class="px-5 py-4 space-y-4">' +
            '<div class="rounded-xl border px-4 py-3 ' + meta.cls + '">' +
            '<div class="flex items-center gap-2"><span class="text-xl">' + meta.emoji + '</span>' +
            '<span class="text-sm font-black">' + esc(pet.name || "우리 아이") + ' · ' + meta.label + '</span></div>' +
            '<p class="text-xs mt-1.5">증상: ' + esc(symptomText || "-") + (free ? ' / ' + esc(free) : "") + "</p></div>" +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">건강 지표 (체중·BCS·QoL)</p>' + metricsHtml + "</div>" +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">수의사에게 물어볼 것</p>' +
            '<ul class="space-y-1.5">' + checklistHtml + "</ul></div>" +
            '<div><p class="text-xs font-bold text-gray-500 mb-2">최근 접종/투약</p>' + medsHtml + "</div>" +
            '<p class="text-[11px] text-gray-400 leading-relaxed">※ 이 카드는 참고용 요약이며 수의사의 진료·진단을 대체하지 않아요.</p>' +
            '</div>' +
            '<div class="px-5 py-3 border-t border-gray-100 flex gap-2 sticky bottom-0 bg-white">' +
            '<button onclick="SymptomTriage.printPrepCard()" class="flex-1 rounded-xl border border-brand-200 hover:bg-brand-50 text-brand-600 py-2.5 text-sm font-bold">🖨️ 인쇄</button>' +
            '<button onclick="SymptomTriage.sharePrepCard()" class="flex-1 rounded-xl bg-brand-500 hover:bg-brand-600 text-white py-2.5 text-sm font-bold">📤 공유 · 복사</button>' +
            "</div></div>";
        overlay.addEventListener("click", function (e) { if (e.target === overlay) closePrepCard(); });
        document.body.appendChild(overlay);
    }

    function closePrepCard() { var el = document.getElementById("st-prep-overlay"); if (el) el.remove(); }

    // 준비 카드를 인쇄용 문서로 열기(window.print — 건강수첩 리포트와 동일 패턴)
    function printPrepCard() {
        var txt = cardText();
        if (!txt) return;
        var pet = (lastResult && lastResult.pet) || activePet() || {};
        var html = '<!DOCTYPE html><html lang="ko"><head><meta charset="UTF-8">'
            + "<title>병원 방문 준비 카드 — " + esc(pet.name || "우리 아이") + "</title><style>"
            + "body{font-family:'Apple SD Gothic Neo','Noto Sans KR',sans-serif;margin:0;padding:32px;color:#1f2937;background:#fff}"
            + "h1{font-size:20px;font-weight:900;color:#a9583e;margin:0 0 4px}"
            + ".sub{font-size:12px;color:#6b7280;margin-bottom:16px}"
            + "pre{white-space:pre-wrap;font-family:inherit;font-size:13px;line-height:1.7;background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;padding:18px}"
            + ".footer{margin-top:24px;text-align:center;font-size:10px;color:#d1d5db}"
            + "@media print{body{padding:0 20px}button{display:none}}</style></head><body>"
            + '<h1>🏥 병원 방문 준비 카드</h1>'
            + '<p class="sub">' + esc(pet.name || "우리 아이") + " · 생성일: " + new Date().toLocaleDateString("ko-KR") + "</p>"
            + "<pre>" + esc(txt) + "</pre>"
            + '<div class="footer">🐾 펫과나 (Pet & Na) — AI 반려동물 케어 올인원</div>'
            + '<div style="text-align:center;margin-top:20px"><button onclick="window.print()" style="background:#a9583e;color:#fff;border:none;padding:10px 24px;border-radius:8px;font-size:13px;font-weight:700;cursor:pointer">🖨️ 인쇄 / PDF로 저장</button></div>'
            + "</body></html>";
        var url = URL.createObjectURL(new Blob([html], { type: "text/html; charset=utf-8" }));
        var win = window.open(url, "_blank");
        if (!win) {
            URL.revokeObjectURL(url);
            if (typeof showToast === "function") showToast("팝업 차단을 해제해주세요");
            return;
        }
        setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
    }

    function sharePrepCard() {
        var txt = cardText();
        if (!txt) return;
        if (navigator.share) {
            navigator.share({ title: "병원 방문 준비 카드", text: txt }).catch(function () {});
            return;
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(txt).then(function () {
                if (typeof showToast === "function") showToast("준비 카드를 복사했어요 📋");
            }).catch(function () {});
        } else if (typeof showToast === "function") {
            showToast("공유를 지원하지 않는 환경이에요");
        }
    }

    // 긴급도별 재방문 리마인더 계획 (once 일정 — 며칠 뒤 병원/관찰 알림)
    var REVISIT = {
        3: { days: 0, type: "vet", title: "🚨 응급 방문 후 경과 확인" },
        2: { days: 2, type: "vet", title: "🏥 병원 내원 예정" },
        1: { days: 2, type: "vet", title: "🩺 증상 경과 재확인" },
    };
    function fmtDate(d) {
        return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
    }

    // 결과를 건강수첩에 저장 + 긴급도별 재방문 리마인더 자동 생성
    function saveToRecord() {
        if (!lastResult || !lastResult.pet) return;
        var pet = lastResult.pet, r = lastResult.result, free = String(lastResult.free || "").trim();
        if (!r.picks.length && !free) {
            if (typeof showToast === "function") showToast("증상을 먼저 선택해 주세요 🐾");
            return;
        }
        var meta = LEVEL_META[r.level];
        var symptomText = r.picks.map(function (s) { return s.label; }).join(", ");
        var summary = "증상 트리아지 · " + meta.label
            + (symptomText ? " (" + symptomText + ")" : "")
            + (free ? " / " + free : "");

        // 1) 재방문 리마인더 자동 생성 (긴급도별 — care-scheduler)
        var plan = REVISIT[r.level] || REVISIT[1];
        var due = new Date();
        due.setDate(due.getDate() + plan.days);
        if (typeof addCareSchedule === "function") {
            addCareSchedule({
                petId: pet.id,
                type: plan.type,
                title: plan.title,
                time: "10:00",
                repeat: "once",
                date: fmtDate(due),
                notes: summary,
            });
        }

        // 2) 건강수첩 모달을 요약으로 채워 저장 유도 (medical-records)
        if (typeof openMedicalRecordModal === "function") {
            close();
            openMedicalRecordModal();
            var cat = document.getElementById("medical-category");
            var diag = document.getElementById("medical-diagnosis");
            var notes = document.getElementById("medical-notes");
            if (cat) cat.value = "visit";
            if (diag) diag.value = symptomText || meta.label;
            if (notes) notes.value = summary;
        }
    }

    window.SymptomTriage = { open: open, run: run, close: close, saveToRecord: saveToRecord,
        openPrepCard: openPrepCard, closePrepCard: closePrepCard, sharePrepCard: sharePrepCard,
        printPrepCard: printPrepCard, _triage: triage };
})();
