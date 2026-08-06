// care-score.js — 케어 스코어 & Proof-of-Care 공유 카드 (나무 백로그 P2)
//
// 기존에 흩어져 있던 케어 지표(산책 스트릭·건강기록 연속성·주간 케어 완료율·
// 병원기록·이상탐지 상태)를 가중합해 0~100점의 '케어 스코어'로 요약하고,
// 등급 배지와 공유용 이미지 카드를 만든다. 새 데이터·스키마 없이 전부
// 기존 전역 헬퍼만 읽는다(읽기전용).
(function () {
    "use strict";

    // ── 지표별 가중치(합 100) ──────────────────────────────────────
    // 각 항목은 0~1로 정규화한 뒤 max 점수를 곱한다.
    var WEIGHTS = {
        careRate: 30,   // 주간 케어 완료율
        walk: 20,       // 산책 연속일
        health: 20,     // 건강기록 연속성
        vet: 15,        // 병원기록(꾸준한 진료)
        wellness: 15,   // 이상탐지 상태(정상일수록 만점)
    };

    function _clamp01(x) { return x < 0 ? 0 : x > 1 ? 1 : x; }

    function _medicalCount() {
        if (typeof getMedicalRecordsForActivePet === "function") {
            try { var r = getMedicalRecordsForActivePet(); if (Array.isArray(r)) return r.length; } catch (e) {}
        }
        return Array.isArray(window.medicalRecords) ? window.medicalRecords.length : 0;
    }

    // 이상탐지: 소견이 하나도 없으면 1.0, 소견이 있으면 개수에 따라 감점.
    function _wellnessRatio() {
        var history = (typeof healthLogs !== "undefined" && healthLogs) ? healthLogs.history : [];
        var weightHistory = (typeof getWeightHistory === "function") ? getWeightHistory() : [];
        var count = 0;
        try {
            if (typeof analyzeWellness === "function") count += (analyzeWellness(history) || []).length;
            if (typeof analyzeStool === "function" && analyzeStool(history)) count += 1;
            if (typeof analyzeStoolColor === "function" && analyzeStoolColor(history)) count += 1;
            if (typeof analyzeUrine === "function" && analyzeUrine(history)) count += 1;
            if (typeof analyzeCondition === "function") count += (analyzeCondition(history) || []).length;
            if (typeof analyzeWeight === "function" && analyzeWeight(weightHistory)) count += 1;
        } catch (e) { /* 헬퍼 부재/오류는 만점 처리(억제) */ }
        return _clamp01(1 - count * 0.25);   // 소견 4개면 0점
    }

    // 케어 스코어 산출: { score, grade, components:[{key,label,pts,max,detail}] }
    function computeCareScore() {
        var careRate = (typeof getWeeklyCareCompletionRate === "function")
            ? getWeeklyCareCompletionRate() : 0;
        var walkStreak = (typeof calcWalkStreak === "function") ? calcWalkStreak() : 0;
        var healthStreak = (typeof calcHealthStreak === "function") ? calcHealthStreak() : 0;
        var vetCount = _medicalCount();
        var wellnessR = _wellnessRatio();

        var comp = [
            { key: "careRate", label: "케어 완료율", ratio: _clamp01(careRate / 100),
              max: WEIGHTS.careRate, detail: careRate + "%" },
            { key: "walk", label: "산책 스트릭", ratio: _clamp01(walkStreak / 14),
              max: WEIGHTS.walk, detail: walkStreak + "일" },
            { key: "health", label: "건강기록 연속", ratio: _clamp01(healthStreak / 14),
              max: WEIGHTS.health, detail: healthStreak + "일" },
            { key: "vet", label: "병원기록", ratio: _clamp01(vetCount / 3),
              max: WEIGHTS.vet, detail: vetCount + "건" },
            { key: "wellness", label: "이상탐지 상태", ratio: wellnessR,
              max: WEIGHTS.wellness, detail: wellnessR >= 1 ? "정상" : "주의" },
        ];

        var score = 0;
        comp.forEach(function (c) { c.pts = Math.round(c.ratio * c.max); score += c.pts; });
        score = Math.min(100, score);

        return { score: score, grade: gradeFor(score), components: comp };
    }

    // 등급 배지: 점수 → { g, emoji, color }
    function gradeFor(score) {
        if (score >= 90) return { g: "S", emoji: "👑", color: "#f59e0b" };
        if (score >= 80) return { g: "A", emoji: "🏆", color: "#10b981" };
        if (score >= 70) return { g: "B", emoji: "🥇", color: "#3b82f6" };
        if (score >= 55) return { g: "C", emoji: "🐾", color: "#8b5cf6" };
        return { g: "D", emoji: "🌱", color: "#9ca3af" };
    }

    function _esc(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    // 건강 탭 상단 카드 렌더
    function renderCareScoreCard() {
        var host = document.getElementById("care-score-card");
        if (!host) return;
        var pet = (typeof getActivePet === "function") ? getActivePet() : null;
        if (!pet) { host.innerHTML = ""; return; }

        var r = computeCareScore();
        var g = r.grade;
        var rows = r.components.map(function (c) {
            var pct = Math.round((c.pts / c.max) * 100);
            return '<div class="flex items-center gap-2 text-[11px]">' +
                '<span class="w-20 shrink-0 text-gray-500">' + _esc(c.label) + '</span>' +
                '<div class="flex-1 h-1.5 rounded-full bg-gray-100 overflow-hidden">' +
                '<div class="h-full rounded-full" style="width:' + pct + '%;background:' + g.color + '"></div></div>' +
                '<span class="w-14 text-right font-bold text-gray-700">' + _esc(c.detail) + '</span></div>';
        }).join("");

        host.innerHTML =
            '<div class="card-modern p-5 border border-brand-100/60 mt-3 lg:col-span-2">' +
              '<div class="flex items-center gap-3">' +
                '<div class="flex flex-col items-center justify-center w-16 h-16 rounded-2xl shrink-0" style="background:' + g.color + '1a">' +
                  '<span class="text-2xl leading-none">' + g.emoji + '</span>' +
                  '<span class="text-xs font-black" style="color:' + g.color + '">' + g.g + '등급</span>' +
                '</div>' +
                '<div class="min-w-0 flex-1">' +
                  '<h3 class="text-sm font-bold text-gray-900">' + _esc(pet.name || "우리 아이") + ' 케어 스코어</h3>' +
                  '<p class="text-2xl font-black leading-tight" style="color:' + g.color + '">' + r.score + '<span class="text-sm text-gray-400 font-bold">/100</span></p>' +
                '</div>' +
                '<button onclick="shareCareScoreCard()" class="btn-modern bg-brand-500 hover:bg-brand-600 text-white px-3 py-2 text-xs shrink-0">' +
                  '<i class="fa-solid fa-share-nodes mr-1"></i>공유' +
                '</button>' +
              '</div>' +
              '<div class="mt-3 space-y-1.5">' + rows + '</div>' +
              '<p class="text-[10px] text-gray-400 mt-2 text-center">기존 케어 기록을 종합한 관리 성과 지표예요 · 매일 갱신</p>' +
            '</div>';
    }

    // ── Proof-of-Care 공유 이미지 카드 (1080×1080) ─────────────────
    function generateCareScoreCard() {
        var pet = (typeof getActivePet === "function") ? getActivePet() : null;
        var petName = (pet && pet.name) || "댕이";
        var r = computeCareScore();
        var g = r.grade;

        var S = 1080;
        var canvas = document.createElement("canvas");
        canvas.width = S; canvas.height = S;
        var ctx = canvas.getContext("2d");

        var grad = ctx.createLinearGradient(0, 0, S, S);
        grad.addColorStop(0, "#faf3ef"); grad.addColorStop(1, "#f4e2d9");
        ctx.fillStyle = grad; ctx.fillRect(0, 0, S, S);

        ctx.textAlign = "center";
        ctx.fillStyle = "#7c4a2d";
        ctx.font = "bold 44px sans-serif";
        ctx.fillText("🐾 Proof of Care", S / 2, 130);

        ctx.fillStyle = "#1f2937";
        ctx.font = "bold 60px sans-serif";
        ctx.fillText(petName + " 케어 성적표", S / 2, 220);

        // 원형 점수 링
        var cx = S / 2, cy = 430, rad = 150;
        ctx.lineWidth = 26;
        ctx.strokeStyle = "#e7d8ce";
        ctx.beginPath(); ctx.arc(cx, cy, rad, 0, Math.PI * 2); ctx.stroke();
        ctx.strokeStyle = g.color;
        ctx.beginPath();
        ctx.arc(cx, cy, rad, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * (r.score / 100));
        ctx.stroke();

        ctx.fillStyle = g.color;
        ctx.font = "bold 130px sans-serif";
        ctx.fillText(String(r.score), cx, cy + 30);
        ctx.fillStyle = "#9ca3af";
        ctx.font = "bold 34px sans-serif";
        ctx.fillText("/ 100", cx, cy + 85);

        // 등급 배지
        ctx.fillStyle = g.color;
        ctx.font = "bold 52px sans-serif";
        ctx.fillText(g.emoji + " " + g.g + "등급", cx, cy + 200);

        // 지표 세부 (좌측 라벨 · 우측 값)
        var y = 730;
        ctx.font = "bold 34px sans-serif";
        r.components.forEach(function (c) {
            ctx.textAlign = "left";
            ctx.fillStyle = "#4b5563";
            ctx.fillText(c.label, 180, y);
            ctx.textAlign = "right";
            ctx.fillStyle = "#1f2937";
            ctx.fillText(c.detail, S - 180, y);
            y += 58;
        });

        ctx.textAlign = "center";
        ctx.fillStyle = "#b08968";
        ctx.font = "28px sans-serif";
        var today = new Date().toISOString().split("T")[0];
        ctx.fillText(today + " · 펫나 Petnna", S / 2, S - 50);

        return canvas;
    }

    function shareCareScoreCard() {
        if (typeof getActivePet === "function" && !getActivePet()) {
            if (typeof showToast === "function") showToast("반려동물을 먼저 등록해 주세요 🐾");
            return;
        }
        var canvas = generateCareScoreCard();
        var r = computeCareScore();
        var petName = (typeof getActivePet === "function" && getActivePet()) ? getActivePet().name : "우리 아이";
        var text = petName + "의 케어 스코어 " + r.score + "점 (" + r.grade.g + "등급)! #펫나 #ProofOfCare";
        if (typeof _downloadOrShare === "function") {
            _downloadOrShare(canvas, "care-score.png", "케어 스코어", text);
        }
    }

    window.computeCareScore = computeCareScore;
    window.gradeFor = gradeFor;
    window.renderCareScoreCard = renderCareScoreCard;
    window.generateCareScoreCard = generateCareScoreCard;
    window.shareCareScoreCard = shareCareScoreCard;
})();
