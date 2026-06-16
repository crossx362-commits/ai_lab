// ai-health.js — Gemini 2.5 Flash Vision 기반 10항목 건강 분석 + 음성 문진
const AI_HEALTH_DISCLAIMER = "※ 이 분석은 참고용이며 의학적 진단이 아닙니다. 이상 소견 시 반드시 수의사와 상담하세요.";

async function analyzeHealthFromPhoto(imageBase64, petName = "펫") {
    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) return { error: true, message: "GEMINI_API_KEY가 설정되지 않았습니다." };

    const prompt = `이 반려동물 사진을 보고 건강 상태를 전문 수의사 관점으로 분석해줘.
사진에서 보이지 않는 부위는 "확인불가"로 반환.
다음 10개 항목을 JSON으로만 반환 (다른 텍스트 없이):

{
  "eyes": "정상|주의|이상|확인불가",
  "ears": "정상|주의|이상|확인불가",
  "skin": "정상|주의|이상|확인불가",
  "coat": "윤기있음|보통|칙칙함|확인불가",
  "teeth": "정상|주의|이상|확인불가",
  "nose": "촉촉함|건조함|이상|확인불가",
  "posture": "정상|주의|이상|확인불가",
  "weight": "저체중|적정|과체중|확인불가",
  "alertness": "활발|보통|무기력|확인불가",
  "paw": "정상|주의|이상|확인불가",
  "score": 0~100,
  "urgent": true|false,
  "urgentReason": "긴급 사유 (urgent=false면 빈 문자열)",
  "summary": "한국어 2문장 요약",
  "advice": "권고 사항 1줄"
}

score 산정 기준: 이상 항목 1개당 -8점, 주의 항목 1개당 -3점, 기본 85점에서 차감.
urgent=true 조건: 눈·코·피부 중 '이상' 2개 이상이거나, 자세·체형이 '이상'인 경우.`;

    try {
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    contents: [{ parts: [
                        { text: prompt },
                        { inline_data: { mime_type: "image/jpeg", data: imageBase64 } }
                    ]}],
                    generationConfig: { responseMimeType: "application/json" }
                })
            }
        );
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        const raw = data?.candidates?.[0]?.content?.parts?.[0]?.text || "{}";
        const result = JSON.parse(raw);
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
    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) return { error: true, message: "GEMINI_API_KEY가 설정되지 않았습니다." };

    const prompt = `반려동물 보호자가 "${petName}"의 증상을 이렇게 설명했어:
"${transcript}"

수의사 관점에서 아래 JSON 형식으로만 분석해줘:
{
  "possibleCauses": ["원인1", "원인2", "원인3"],
  "immediateAction": "지금 당장 할 수 있는 조치 1줄",
  "needsVet": true|false,
  "urgency": "즉시|24시간내|일주일내|관찰",
  "summary": "2문장 요약"
}`;

    try {
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    contents: [{ parts: [{ text: prompt }] }],
                    generationConfig: { responseMimeType: "application/json" }
                })
            }
        );
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        const raw = data?.candidates?.[0]?.content?.parts?.[0]?.text || "{}";
        return JSON.parse(raw);
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
