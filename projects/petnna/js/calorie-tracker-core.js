// 🍽️ 일일 급식·칼로리 트래커 — 계산/저장/위젯 렌더링 — 백로그 나무(P3, 기획)
// ─────────────────────────────────────────────────────────────
// 체중·활동량 기반 일일 권장 칼로리(DER)를 자동 계산하고, 오늘 급여 기록과
// 목표 대비 진행바를 렌더링한다. 급여 기록/활동량 설정 모달은
// js/calorie-tracker-modals.js(같은 CalorieTracker 객체를 확장)에서 처리한다.
// 표준 수의학 공식: RER = 70 × (체중kg)^0.75, DER = RER × 활동계수
// 프론트 전용 — localStorage에만 저장(신규 DB 테이블 없음, qol-checkin과 동일 원칙).
// ─────────────────────────────────────────────────────────────
(function () {
    "use strict";

    var LS_KEY = "petna_calorie_logs"; // { [petId]: { [YYYY-MM-DD]: [{id, label, kind, kcal}] } }
    var LS_FACTOR = "petna_calorie_factor"; // { [petId]: factorValue }
    var LS_DENSITY = "petna_calorie_density"; // { [petId]: kcal per 100g } — 건강 탭의 별도
    // "일일 권장 사료량 계산기" 카드와 계산식(RER×활동계수)이 완전히 중복이라 여기로 흡수
    // (2026-07-13 오너 지시 "건강탭 정리, 합칠건 합치고"). 그램 환산만 이 위젯에 추가.
    var DEFAULT_DENSITY = 350;

    // 활동/생애 단계별 계수 (개·고양이 공통 근사; 표준 DER 계수)
    var FACTORS = [
        { v: 1.2, label: "비만/체중감량", emoji: "🩺" },
        { v: 1.4, label: "중성화·저활동", emoji: "😴" },
        { v: 1.6, label: "보통 활동", emoji: "🐾" },
        { v: 2.0, label: "활발함", emoji: "🏃" },
        { v: 2.5, label: "성장기/임신·수유", emoji: "🌱" },
    ];
    var DEFAULT_FACTOR = 1.6;

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

    function loadAll() { try { return JSON.parse(localStorage.getItem(LS_KEY)) || {}; } catch (e) { return {}; } }
    function saveAll(map) { localStorage.setItem(LS_KEY, JSON.stringify(map)); }
    function loadFactors() { try { return JSON.parse(localStorage.getItem(LS_FACTOR)) || {}; } catch (e) { return {}; } }
    function saveFactors(map) { localStorage.setItem(LS_FACTOR, JSON.stringify(map)); }
    function loadDensities() { try { return JSON.parse(localStorage.getItem(LS_DENSITY)) || {}; } catch (e) { return {}; } }
    function saveDensities(map) { localStorage.setItem(LS_DENSITY, JSON.stringify(map)); }
    function densityFor(petId) {
        var d = loadDensities()[String(petId)];
        return typeof d === "number" && d > 0 ? d : DEFAULT_DENSITY;
    }

    function todayKey() {
        var d = new Date();
        return d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0") + "-" + String(d.getDate()).padStart(2, "0");
    }
    function todayLogs(petId) {
        var byDate = loadAll()[String(petId)] || {};
        return byDate[todayKey()] || [];
    }
    function factorFor(petId) {
        var f = loadFactors()[String(petId)];
        return typeof f === "number" ? f : DEFAULT_FACTOR;
    }

    // 중성화 여부: pet.gender 문자열에 "중성화"가 있으면 true (state.js 표기 규칙)
    function neuteredFor(pet) {
        return !!(pet && typeof pet.gender === "string" && pet.gender.indexOf("중성화") !== -1);
    }
    // 최근 BCS 판정 키(under/ideal/over) — 기록 없으면 null
    function latestBcsKey(petId) {
        try {
            var hist = window.BcsWizardData && window.BcsWizardData.history(petId);
            if (!hist || !hist.length) return null;
            var last = hist[hist.length - 1];
            return window.BcsWizardData.classify(last.score).key;
        } catch (e) { return null; }
    }
    // 체중감량 우선순위: BCS 과체중→감량(1.2), 저체중→증량(2.0),
    // 정상/미기록이면 중성화 여부로 저활동(1.4)·보통활동(1.6) 추천.
    function recommendFactor(pet) {
        if (!pet) return null;
        var bcs = latestBcsKey(pet.id);
        var reasons = [];
        var v;
        if (bcs === "over") { v = 1.2; reasons.push("BCS 과체중"); }
        else if (bcs === "under") { v = 2.0; reasons.push("BCS 저체중"); }
        else {
            if (bcs === "ideal") reasons.push("BCS 정상");
            if (neuteredFor(pet)) { v = 1.4; reasons.push("중성화 완료"); }
            else { v = 1.6; reasons.push("중성화 안 함"); }
        }
        return { v: v, reason: reasons.join(" · ") };
    }

    // DER(kcal/일) 계산: 체중 없으면 null
    function targetKcal(pet) {
        var w = parseFloat(pet && pet.weight);
        if (!w || w <= 0) return null;
        var rer = 70 * Math.pow(w, 0.75);
        return Math.round(rer * factorFor(pet.id));
    }

    function sumToday(petId) {
        return todayLogs(petId).reduce(function (s, r) { return s + (parseFloat(r.kcal) || 0); }, 0);
    }

    // ── 위젯(건강 탭 삽입) ─────────────────────────────────────
    function renderWidget(containerId) {
        var el = document.getElementById(containerId);
        if (!el) return;
        var pet = activePet();
        if (!pet) { el.innerHTML = ""; return; }

        var target = targetKcal(pet);
        var eaten = sumToday(pet.id);
        var pct = target ? Math.min(100, Math.round((eaten / target) * 100)) : 0;
        var over = target && eaten > target;
        var barColor = over ? "bg-red-400" : pct >= 80 ? "bg-emerald-400" : "bg-brand-500";

        var factor = factorFor(pet.id);
        var factorLabel = (FACTORS.filter(function (f) { return f.v === factor; })[0] || {}).label || "보통 활동";
        var density = densityFor(pet.id);
        var grams = target ? Math.round((target / density) * 100) : null;

        var logs = todayLogs(pet.id);
        var listHtml = logs.length
            ? logs.map(function (r) {
                return '<div class="flex items-center gap-2 text-xs py-1">' +
                    '<span>' + (r.kind === "treat" ? "🍖" : "🥣") + '</span>' +
                    '<span class="flex-1 min-w-0 truncate text-gray-600">' + esc(r.label || (r.kind === "treat" ? "간식" : "사료")) + '</span>' +
                    '<span class="font-bold text-gray-700">' + Math.round(r.kcal) + ' kcal</span>' +
                    '<button onclick="CalorieTracker.remove(' + r.id + ')" class="text-gray-300 hover:text-red-400 ml-1">&times;</button>' +
                    "</div>";
            }).join("")
            : '<p class="text-[11px] text-gray-400 text-center py-2">오늘 급여 기록이 없어요</p>';

        el.innerHTML =
            '<div class="card-modern p-5">' +
            '<div class="flex items-center justify-between mb-3">' +
            '<h3 class="text-base font-bold text-gray-900 flex items-center gap-2"><span class="text-xl">🍽️</span>일일 급식·칼로리</h3>' +
            '<button onclick="CalorieTracker.openAdd()" class="text-xs font-bold text-white bg-brand-500 hover:bg-brand-600 px-3 py-1.5 rounded-full transition-all shadow-soft">급여 기록</button>' +
            "</div>" +
            (target
                ? '<div class="mb-2 flex items-end justify-between">' +
                  '<span class="text-2xl font-extrabold ' + (over ? "text-red-500" : "text-brand-600") + '">' + Math.round(eaten) + '</span>' +
                  '<span class="text-xs text-gray-400">/ 권장 ' + target + ' kcal</span>' +
                  "</div>" +
                  '<div class="w-full h-2.5 rounded-full bg-gray-100 overflow-hidden mb-1">' +
                  '<div class="h-full rounded-full ' + barColor + ' transition-all" style="width:' + pct + '%"></div>' +
                  "</div>" +
                  '<div class="flex items-center justify-between text-[11px] text-gray-400 mb-2">' +
                  '<button onclick="CalorieTracker.openFactor()" class="hover:text-brand-500 underline decoration-dotted">활동량: ' + esc(factorLabel) + '</button>' +
                  '<span>' + (over ? "권장량 초과 ⚠️" : pct + "% 급여") + '</span>' +
                  "</div>" +
                  '<div class="flex items-center justify-between text-[11px] bg-amber-50/60 rounded-lg px-2.5 py-1.5 mb-3">' +
                  '<span class="text-gray-500">권장 사료량(건사료 기준)</span>' +
                  '<span class="font-bold text-amber-600">' + grams + 'g' +
                  ' <button onclick="CalorieTracker.openDensity()" class="text-gray-400 hover:text-amber-500 underline decoration-dotted font-normal ml-1">(' + density + 'kcal/100g)</button></span>' +
                  "</div>"
                : '<p class="text-xs text-amber-600 bg-amber-50 rounded-xl px-3 py-2 mb-3">권장 칼로리를 계산하려면 마이펫에서 체중을 입력해 주세요 ⚖️</p>') +
            '<div class="border-t border-gray-100 pt-2">' + listHtml + "</div>" +
            "</div>";
    }

    window.CalorieTracker = {
        _esc: esc,
        _activePet: activePet,
        _FACTORS: FACTORS,
        _loadAll: loadAll,
        _saveAll: saveAll,
        _loadFactors: loadFactors,
        _saveFactors: saveFactors,
        _loadDensities: loadDensities,
        _saveDensities: saveDensities,
        _densityFor: densityFor,
        _todayKey: todayKey,
        _todayLogs: todayLogs,
        _factorFor: factorFor,
        _neuteredFor: neuteredFor,
        _recommendFactor: recommendFactor,
        renderWidget: renderWidget,
    };
})();
