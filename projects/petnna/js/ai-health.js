// ai-health.js — Gemini 2.5 Flash Vision 기반 10항목 건강 분석 + 음성 문진
const AI_HEALTH_DISCLAIMER = "※ 이 분석은 참고용이며 의학적 진단이 아닙니다. 이상 소견 시 반드시 수의사와 상담하세요.";

async function callPetnnaAiProxy(payload) {
    const enabled = (typeof isAiHealthEnabled === "function") ? isAiHealthEnabled() : false;
    if (!enabled) {
        return {
            error: true,
            locked: true,
            message: (typeof notifyPetnnaServiceLocked === "function")
                ? notifyPetnnaServiceLocked("AI 건강 분석")
                : "AI 건강 분석은 현재 준비 중입니다.",
            disclaimer: AI_HEALTH_DISCLAIMER
        };
    }

    const endpoint = (typeof getAiHealthProxyPath === "function") ? getAiHealthProxyPath() : "/api/ai-health";
    const res = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
        return {
            error: true,
            locked: !!data.locked,
            message: data.message || `AI API ${res.status}`,
            disclaimer: AI_HEALTH_DISCLAIMER
        };
    }
    return data;
}

async function analyzeHealthFromPhoto(imageBase64, petName = "펫") {
    try {
        const result = await callPetnnaAiProxy({ type: "photo", imageBase64, petName });
        result.disclaimer = AI_HEALTH_DISCLAIMER;
        result.petName = petName;
        result.analyzedAt = new Date().toISOString();
        return result;
    } catch (e) {
        return { error: true, message: `분석 실패: ${e.message}` };
    }
}

// 음성 문진 — 증상을 말하면 Gemini가 분석
async function analyzeSymptomByVoice(transcript, petName = "펫") {
    try {
        const result = await callPetnnaAiProxy({ type: "symptom", transcript, petName });
        result.disclaimer = AI_HEALTH_DISCLAIMER;
        return result;
    } catch (e) {
        return { error: true, message: `분석 실패: ${e.message}` };
    }
}

function imageFileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (e) => resolve(e.target.result.split(",")[1]);
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function saveHealthAnalysis(result) {
    if (!result || result.error) return;
    const key = "petna_health_analyses";
    const history = JSON.parse(localStorage.getItem(key) || "[]");
    history.unshift(result);
    if (history.length > 30) history.splice(30);
    localStorage.setItem(key, JSON.stringify(history));
}

function getHealthAnalyses() {
    return JSON.parse(localStorage.getItem("petna_health_analyses") || "[]");
}

// 건강 기록 스트릭 계산 (하루 유예 프리즈 포함)
function calcHealthStreak() {
    const today = new Date().toISOString().split('T')[0];
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    if (!history.length) return 0;

    const hasRecord = (dateStr) => {
        const e = history.find(h => h.date === dateStr);
        return e && (e.food > 0 || e.water > 0 || e.poop !== null);
    };

    let streak = 0;
    let checkDate = new Date();
    const hasToday = hasRecord(today);
    // 오늘 기록 없으면 어제부터 체크 (오늘치는 아직 입력 중일 수 있음)
    if (!hasToday) checkDate.setDate(checkDate.getDate() - 1);

    for (let i = 0; i < 365; i++) {
        const d = checkDate.toISOString().split('T')[0];
        if (hasRecord(d)) {
            streak++;
            checkDate.setDate(checkDate.getDate() - 1);
        } else {
            // 스트릭 프리즈: 3회 누적 사용 가능. 여기선 하루 gap 허용 (Duolingo 방식)
            const freezeKey = "petna_streak_freeze_used_" + d;
            const freezeUsed = parseInt(localStorage.getItem(freezeKey) || "0");
            if (freezeUsed < 1 && streak > 0) {
                localStorage.setItem(freezeKey, "1");
                checkDate.setDate(checkDate.getDate() - 1);
            } else break;
        }
    }
    return streak;
}

// 마일스톤 달성 여부 확인 후 축하 팝업
function checkStreakMilestone(streak) {
    const milestones = { 7: "🥉 7일 연속!", 30: "🥈 30일 연속!", 100: "🥇 100일 연속!" };
    if (milestones[streak]) {
        const key = "petna_milestone_shown_" + streak;
        if (!localStorage.getItem(key)) {
            localStorage.setItem(key, "1");
            if (typeof showToast === 'function') {
                showToast(`${milestones[streak]} 건강 기록 달인이에요 🎉`);
            }
        }
    }
}
