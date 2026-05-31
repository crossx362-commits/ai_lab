# 펫과나 전체 개선 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** AI 건강 분석 + 건강 대시보드 + 멀티펫 UX + 소셜 강화 + Freemium 모델을 순차 구현해 TTcare 대항마 수준으로 펫과나를 완성한다.

**Architecture:** 순수 프론트엔드(Vanilla JS) + Supabase(localStorage 폴백) 구조 유지. Gemini 2.5 Flash Vision API를 `window._env_.GEMINI_API_KEY`로 직접 호출. 신규 기능은 독립 JS 파일로 분리해 기존 코드 오염 최소화.

**Tech Stack:** Vanilla JS, Tailwind CSS, Chart.js(기탑재), Supabase, Gemini 2.5 Flash API

---

## 파일 구조

| 파일 | 역할 |
|------|------|
| `js/ai-health.js` | Gemini Vision 건강 분석 신규 모듈 |
| `js/health-dashboard.js` | 7일/30일 트렌드 차트, 건강 점수 계산 |
| `js/share-card.js` | 사주/건강 결과 공유 카드 생성기 |
| `js/freemium.js` | AI 분석 월 사용 횟수 추적, 프리미엄 게이트 |
| `js/mypet.js` | AI 분석 버튼 연결, 멀티펫 스와이프 UI 교체 |
| `js/templates/mypet.js` | 건강 대시보드 섹션, AI 분석 결과 패널 HTML |
| `js/social.js` | AI 캡션 생성 버튼 추가 |
| `css/style.css` | 스와이프 카드, AI 패널, 공유 카드 스타일 |
| `index.html` | GEMINI_API_KEY env 추가 |

---

## Phase 1: AI 건강 분석 MVP

### Task 1: GEMINI_API_KEY 환경 변수 추가

**Files:**
- Modify: `index.html` (window._env_ 블록)

- [ ] **Step 1: `index.html`의 `window._env_` 블록에 GEMINI_API_KEY 추가**

`index.html` 에서 `window._env_` 를 정의하는 `<script>` 블록을 찾아 다음과 같이 추가:

```html
window._env_ = {
    SUPABASE_URL: "",
    SUPABASE_ANON_KEY: "",
    GEMINI_API_KEY: ""   // ← 추가: Google AI Studio에서 발급
};
```

- [ ] **Step 2: 브라우저 콘솔에서 키 존재 확인**

```javascript
console.log(window._env_.GEMINI_API_KEY); // 빈 문자열 또는 실제 키 출력되면 OK
```

- [ ] **Step 3: Commit**

```bash
git add index.html
git commit -m "feat: add GEMINI_API_KEY to env config"
```

---

### Task 2: AI 건강 분석 모듈 생성 (`js/ai-health.js`)

**Files:**
- Create: `js/ai-health.js`

- [ ] **Step 1: `js/ai-health.js` 파일 생성**

```javascript
// ai-health.js — Gemini 2.5 Flash Vision으로 펫 사진 건강 분석
const AI_HEALTH_DISCLAIMER = "※ 이 분석은 참고용이며 의학적 진단이 아닙니다. 이상 소견 시 반드시 수의사와 상담하세요.";

async function analyzeHealthFromPhoto(imageBase64, petName = "펫") {
    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) {
        return { error: true, message: "GEMINI_API_KEY가 설정되지 않았습니다." };
    }

    const prompt = `이 반려동물 사진을 보고 건강 상태를 분석해줘.
다음 항목을 확인하고 JSON으로만 반환:
- eyes: 눈 이상 여부 (정상/주의/이상)
- skin: 피부·털 상태 (정상/주의/이상)  
- body: 체형·자세 (정상/주의/이상)
- score: 종합 건강 점수 0~100
- summary: 한국어 2문장 요약 (심각한 이상 없으면 긍정적으로)
- advice: 권고 사항 1줄 (이상 없으면 "정기 검진을 유지하세요")
형식: {"eyes":"정상","skin":"주의","body":"정상","score":78,"summary":"...","advice":"..."}`;

    try {
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    contents: [{
                        parts: [
                            { text: prompt },
                            { inline_data: { mime_type: "image/jpeg", data: imageBase64 } }
                        ]
                    }],
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
        return { error: true, message: `분석 실��: ${e.message}` };
    }
}

function imageFileToBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (e) => {
            const base64 = e.target.result.split(",")[1];
            resolve(base64);
        };
        reader.onerror = reject;
        reader.readAsDataURL(file);
    });
}

function saveHealthAnalysis(result) {
    if (!result || result.error) return;
    const key = "petna_health_analyses";
    const history = JSON.parse(localStorage.getItem(key) || "[]");
    history.unshift(result);
    if (history.length > 30) history.splice(30); // 최대 30개
    localStorage.setItem(key, JSON.stringify(history));
}

function getHealthAnalyses() {
    return JSON.parse(localStorage.getItem("petna_health_analyses") || "[]");
}
```

- [ ] **Step 2: `index.html`에 스크립트 태그 추가**

기존 `<script src="js/mypet.js">` 앞에 삽입:

```html
<script src="js/ai-health.js"></script>
```

- [ ] **Step 3: 브라우저 콘솔에서 함수 존재 확인**

```javascript
typeof analyzeHealthFromPhoto // "function" 이면 OK
```

- [ ] **Step 4: Commit**

```bash
git add js/ai-health.js index.html
git commit -m "feat: add AI health analysis module (Gemini Vision)"
```

---

### Task 3: mypet 탭에 AI 분석 버튼 + 결과 패널 추가

**Files:**
- Modify: `js/templates/mypet.js` (pet-stage 섹션 이후)
- Modify: `js/mypet.js` (분석 실행 함수)
- Modify: `css/style.css`

- [ ] **Step 1: `js/templates/mypet.js` 에 AI 분석 섹션 추가**

`pet-dday-bubble` div 뒤에 삽입:

```html
<!-- AI 건강 분석 -->
<div class="bg-gradient-to-br from-violet-50 to-purple-50/60 border border-violet-100 rounded-2xl p-4 space-y-3">
    <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
            <span class="text-lg">🏥</span>
            <span class="text-xs font-black text-violet-700">AI 건강 분석</span>
        </div>
        <button id="ai-health-analyze-btn"
            onclick="triggerAiHealthAnalysis()"
            class="flex items-center gap-1.5 px-3 py-1.5 bg-violet-500 hover:bg-violet-600 text-white font-black text-[11px] rounded-xl transition-all shadow-sm">
            <i class="fa-solid fa-camera text-xs"></i> 사진 분석
        </button>
    </div>
    <input type="file" id="ai-health-photo-input" accept="image/*" class="hidden"
        onchange="runAiHealthAnalysis(event)">
    <div id="ai-health-result" class="hidden space-y-2"></div>
    <p class="text-[10px] text-violet-400 font-medium">
        ※ 참고용 AI 분석 · 의학적 진단 아님 · 이상 시 수의사 상담
    </p>
</div>
```

- [ ] **Step 2: `js/mypet.js` 에 분석 함수 추가 (파일 끝에 추가)**

```javascript
function triggerAiHealthAnalysis() {
    const usageKey = "petna_ai_health_count_" + new Date().toISOString().slice(0,7); // YYYY-MM
    const used = parseInt(localStorage.getItem(usageKey) || "0");
    if (used >= 3 && !localStorage.getItem("petna_premium")) {
        showToast("이번 달 무료 분석 3회를 모두 사용했습니다. 프리미엄에서 무제한 사용 가능합니다 🐾");
        return;
    }
    document.getElementById("ai-health-photo-input")?.click();
}

async function runAiHealthAnalysis(event) {
    const file = event.target.files[0];
    if (!file) return;
    const btn = document.getElementById("ai-health-analyze-btn");
    const resultEl = document.getElementById("ai-health-result");
    if (!resultEl) return;

    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin text-xs"></i> 분석 중...';
    resultEl.classList.remove("hidden");
    resultEl.innerHTML = '<p class="text-xs text-violet-500 text-center py-4">AI가 사진을 분석하고 있습니다... 🔍</p>';

    const pet = getActivePet();
    const base64 = await imageFileToBase64(file);
    const result = await analyzeHealthFromPhoto(base64, pet?.name || "펫");

    if (result.error) {
        resultEl.innerHTML = `<p class="text-xs text-red-500">${result.message}</p>`;
    } else {
        saveHealthAnalysis(result);
        // 사용 횟수 증가
        const usageKey = "petna_ai_health_count_" + new Date().toISOString().slice(0,7);
        localStorage.setItem(usageKey, String(parseInt(localStorage.getItem(usageKey) || "0") + 1));

        const scoreColor = result.score >= 80 ? "text-emerald-600" : result.score >= 60 ? "text-amber-500" : "text-red-500";
        const badge = (val) => {
            const c = val === "정상" ? "bg-emerald-100 text-emerald-700" : val === "주의" ? "bg-amber-100 text-amber-700" : "bg-red-100 text-red-700";
            return `<span class="text-[10px] font-black px-2 py-0.5 rounded-full ${c}">${val}</span>`;
        };
        resultEl.innerHTML = `
            <div class="flex items-center gap-3 bg-white rounded-xl p-3 border border-violet-100">
                <div class="text-center">
                    <span class="block text-2xl font-black ${scoreColor}">${result.score}</span>
                    <span class="text-[9px] text-gray-400 font-bold">건강점수</span>
                </div>
                <div class="flex-1 space-y-1">
                    <div class="flex gap-1.5 flex-wrap">
                        <span class="text-[10px] font-bold text-gray-500">눈</span>${badge(result.eyes)}
                        <span class="text-[10px] font-bold text-gray-500">피부</span>${badge(result.skin)}
                        <span class="text-[10px] font-bold text-gray-500">체형</span>${badge(result.body)}
                    </div>
                    <p class="text-[11px] text-gray-600 leading-snug">${result.summary}</p>
                    <p class="text-[10px] text-violet-500 font-medium">${result.advice}</p>
                </div>
            </div>`;
    }
    btn.disabled = false;
    btn.innerHTML = '<i class="fa-solid fa-camera text-xs"></i> 다시 분석';
    event.target.value = "";
}
```

- [ ] **Step 3: 브라우저에서 동작 확인**

1. mypet 탭 진입
2. "사진 분석" 버튼 클릭 → 파일 선택창 열리는지 확인
3. `GEMINI_API_KEY` 있으면 실제 분석, 없으면 에러 메시지 표시 확인

- [ ] **Step 4: Commit**

```bash
git add js/templates/mypet.js js/mypet.js
git commit -m "feat: add AI health analysis button and result panel to mypet tab"
```

---

## Phase 2: 건강 대시보드 고도화

### Task 4: 건강 이력 자동 저장 + 7일 트렌드 차트

**Files:**
- Create: `js/health-dashboard.js`
- Modify: `js/templates/mypet.js`
- Modify: `js/mypet.js`

- [ ] **Step 1: `js/health-dashboard.js` 생성**

```javascript
// health-dashboard.js — 건강 이력 저장 및 차트 렌더링

function saveHealthHistoryToday() {
    if (typeof healthLogs === 'undefined' || !healthLogs.today) return;
    const today = new Date().toISOString().split('T')[0];
    if (!healthLogs.history) healthLogs.history = [];
    const existing = healthLogs.history.findIndex(h => h.date === today);
    const entry = { ...healthLogs.today, date: today };
    if (existing >= 0) {
        healthLogs.history[existing] = entry;
    } else {
        healthLogs.history.unshift(entry);
        if (healthLogs.history.length > 90) healthLogs.history.splice(90);
    }
    if (typeof saveState === 'function') saveState();
}

function getLast7DaysHealthData() {
    const history = (typeof healthLogs !== 'undefined' && healthLogs.history) ? healthLogs.history : [];
    const days = Array.from({ length: 7 }, (_, i) => {
        const d = new Date();
        d.setDate(d.getDate() - (6 - i));
        return d.toISOString().split('T')[0];
    });
    return days.map(date => {
        const entry = history.find(h => h.date === date) || {};
        return {
            date: date.slice(5),      // MM-DD
            food: entry.food || 0,
            water: entry.water || 0,
            poop: entry.poop ? 1 : 0,
            condition: entry.condition || null
        };
    });
}

function calcHealthScore() {
    const data = getLast7DaysHealthData();
    let score = 70;
    const avgFood = data.reduce((s, d) => s + d.food, 0) / 7;
    const avgWater = data.reduce((s, d) => s + d.water, 0) / 7;
    const poopDays = data.filter(d => d.poop).length;
    if (avgFood > 50)  score += 10;
    if (avgWater > 200) score += 10;
    if (poopDays >= 5) score += 10;
    return Math.min(100, Math.round(score));
}

let healthTrendChart = null;

function renderHealthTrendChart(period = 7) {
    const ctx = document.getElementById('health-trend-chart');
    if (!ctx || typeof Chart === 'undefined') return;
    const data = getLast7DaysHealthData().slice(-period);
    if (healthTrendChart) { healthTrendChart.destroy(); healthTrendChart = null; }

    const isDark = document.body.classList.contains('theme-dark');
    const textColor = isDark ? '#d1d1e0' : '#6b7280';
    const gridColor = isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)';

    healthTrendChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => d.date),
            datasets: [
                {
                    label: '음식(g)',
                    data: data.map(d => d.food),
                    backgroundColor: 'rgba(245,158,11,0.6)',
                    borderRadius: 4
                },
                {
                    label: '물(ml)',
                    data: data.map(d => d.water),
                    backgroundColor: 'rgba(56,189,248,0.6)',
                    borderRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { labels: { color: textColor, font: { size: 10 } } }
            },
            scales: {
                x: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor } },
                y: { ticks: { color: textColor, font: { size: 10 } }, grid: { color: gridColor } }
            }
        }
    });

    // 건강 점수 업데이트
    const scoreEl = document.getElementById('health-score-value');
    if (scoreEl) {
        const score = calcHealthScore();
        scoreEl.textContent = score;
        scoreEl.className = score >= 80 ? 'text-2xl font-black text-emerald-500'
                          : score >= 60 ? 'text-2xl font-black text-amber-500'
                          : 'text-2xl font-black text-red-500';
    }
}
```

- [ ] **Step 2: `js/templates/mypet.js` 에 건강 대시보드 섹션 추가**

컨디션 2칸 div 위에 삽입:

```html
<!-- 건강 트렌드 대시보드 -->
<div class="bg-white border border-gray-100 rounded-2xl p-4 space-y-3">
    <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
            <span class="text-base">📊</span>
            <span class="text-xs font-black text-gray-700">7일 건강 트렌드</span>
        </div>
        <div class="flex items-center gap-2">
            <span class="text-[10px] text-gray-400 font-bold">건강점수</span>
            <span id="health-score-value" class="text-2xl font-black text-emerald-500">--</span>
        </div>
    </div>
    <div style="height:110px">
        <canvas id="health-trend-chart"></canvas>
    </div>
</div>
```

- [ ] **Step 3: `js/mypet.js`의 `renderMyPets()` 함수 끝에 차트 호출 추가**

```javascript
// renderMyPets() 함수 내 마지막 부분에 추가
if (typeof saveHealthHistoryToday === 'function') saveHealthHistoryToday();
if (typeof renderHealthTrendChart === 'function') setTimeout(renderHealthTrendChart, 300);
```

- [ ] **Step 4: `index.html`에 스크립트 추가**

```html
<script src="js/health-dashboard.js"></script>
```

- [ ] **Step 5: 브라우저에서 차트 표시 확인**

mypet 탭 진입 → 7일 트렌드 바 차트와 건강 점수가 표시되면 OK

- [ ] **Step 6: Commit**

```bash
git add js/health-dashboard.js js/templates/mypet.js js/mypet.js index.html
git commit -m "feat: add 7-day health trend chart and health score to mypet dashboard"
```

---

## Phase 3: 멀티펫 스와이프 UI

### Task 5: 멀티펫 가로 스와이프 카드로 교체

**Files:**
- Modify: `js/mypet.js` (`renderPetStageList` 함수)
- Modify: `css/style.css`

- [ ] **Step 1: `css/style.css` 에 스와이프 스타일 추가**

```css
/* 멀티펫 스와이프 카드 */
#pet-stage-list {
    flex-direction: row !important;
    gap: 10px !important;
    overflow-x: auto;
    overflow-y: hidden;
    scroll-snap-type: x mandatory;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
    padding: 4px 2px;
    max-width: 100%;
}
#pet-stage-list::-webkit-scrollbar { display: none; }
.pet-swipe-card {
    scroll-snap-align: center;
    flex-shrink: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    cursor: pointer;
}
```

- [ ] **Step 2: `js/mypet.js`의 `renderPetStageList()`에서 wrapper className 수정**

```javascript
// 기존:
wrapper.className = 'flex flex-row items-center gap-2 cursor-pointer group';
// 변경:
wrapper.className = 'pet-swipe-card';
```

- [ ] **Step 3: 활성 펫 카드 강조 스타일 — circle 클래스에 ring 추가**

```javascript
// isActive === true 분기에서 border-amber-400 뒤에 ring-2 ring-amber-300/50 추가
? `${sz.circle} ${sz.border} border-amber-400 ring-2 ring-amber-300/50 shadow-lg bg-amber-50 cursor-pointer hover:border-amber-500`
```

- [ ] **Step 4: 브라우저에서 확인**

펫 2마리 이상 등록 후 mypet 탭 → 가로 스크롤로 펫 선택 가능한지 확인

- [ ] **Step 5: Commit**

```bash
git add js/mypet.js css/style.css
git commit -m "feat: replace multi-pet vertical list with horizontal swipe cards"
```

---

### Task 6: 소셜 게시물 AI 캡션 자동 생성

**Files:**
- Modify: `js/social.js`
- Modify: `js/templates/social.js` (게시물 작성 모달)

- [ ] **Step 1: `js/social.js` 끝에 AI 캡션 생성 함수 추가**

```javascript
async function generateSocialCaption(imageBase64OrNull) {
    const apiKey = window._env_?.GEMINI_API_KEY || "";
    if (!apiKey) { showToast("AI 캡션 생성에 GEMINI_API_KEY가 필요합니다."); return; }

    const textArea = document.getElementById('post-content');
    if (!textArea) return;
    const prevVal = textArea.value;
    textArea.value = "AI 캡션 생성 중... ✍️";
    textArea.disabled = true;

    const pet = getActivePet();
    const prompt = imageBase64OrNull
        ? `이 반려동물 사진을 보고 진짜 집사가 인스타에 올릴 것처럼 자연스러운 한국어 캡션을 써줘. 짧고 감성적으로. 이모지 1개. 해시태그 5개 포함. 캡션만 출력.`
        : `${pet?.name || "댕이"} 사진을 올리는 인스타 캡션을 써줘. 진짜 집사처럼 자연스럽게. 이모지 1개. 해시태그 5개. 캡션만.`;

    try {
        const parts = [{ text: prompt }];
        if (imageBase64OrNull) parts.push({ inline_data: { mime_type: "image/jpeg", data: imageBase64OrNull } });
        const res = await fetch(
            `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=${apiKey}`,
            { method: "POST", headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ contents: [{ parts }] }) }
        );
        const data = await res.json();
        const caption = data?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || prevVal;
        textArea.value = caption;
    } catch (e) {
        textArea.value = prevVal;
        showToast("AI 캡션 생성 실패: " + e.message);
    }
    textArea.disabled = false;
}
```

- [ ] **Step 2: `js/templates/social.js` 에서 `id="post-content"` textarea를 검색 후 바로 위에 버튼 삽입**

`js/templates/social.js` 에서 `post-content` textarea 태그 바로 앞에:

```html
<div class="flex justify-end mb-1">
    <button type="button" onclick="generateSocialCaption(null)"
        class="flex items-center gap-1 text-[11px] font-black text-violet-500 hover:text-violet-700 transition-colors">
        <i class="fa-solid fa-wand-magic-sparkles text-xs"></i> AI 캡션 생���
    </button>
</div>
```

- [ ] **Step 3: 브라우저에서 확인**

소셜 탭 → 게시물 작성 → "AI 캡션 생성" 클릭 → textarea에 자연스러운 캡션 채워지면 OK

- [ ] **Step 4: Commit**

```bash
git add js/social.js js/templates/social.js
git commit -m "feat: add AI caption generation button to social post composer"
```

---

### Task 7: 사주/건강 결과 공유 카드

**Files:**
- Create: `js/share-card.js`
- Modify: `js/mypet.js`

- [ ] **Step 1: `js/share-card.js` 생성**

```javascript
// share-card.js — 건강/사주 결과 이미지 카드 생성 및 공유

function generateShareCard(type = "health") {
    const pet = getActivePet();
    const petName = pet?.name || "댕이";
    const analyses = typeof getHealthAnalyses === 'function' ? getHealthAnalyses() : [];
    const latest = analyses[0];

    const canvas = document.createElement("canvas");
    canvas.width = 800; canvas.height = 800;
    const ctx = canvas.getContext("2d");

    // 배경
    const grad = ctx.createLinearGradient(0, 0, 800, 800);
    if (type === "health") {
        grad.addColorStop(0, "#f5f3ff"); grad.addColorStop(1, "#ede9fe");
    } else {
        grad.addColorStop(0, "#fffbeb"); grad.addColorStop(1, "#fef3c7");
    }
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, 800, 800);

    // 제목
    ctx.fillStyle = type === "health" ? "#7c3aed" : "#d97706";
    ctx.font = "bold 48px 'Apple SD Gothic Neo', sans-serif";
    ctx.textAlign = "center";
    ctx.fillText(type === "health" ? "🏥 AI 건강 분석 결과" : "🔯 사주 분석 결과", 400, 100);

    // 펫 이름
    ctx.fillStyle = "#374151";
    ctx.font = "bold 36px 'Apple SD Gothic Neo', sans-serif";
    ctx.fillText(`${petName} 의 건강 점수`, 400, 200);

    if (type === "health" && latest) {
        // 점수
        ctx.font = "bold 120px 'Apple SD Gothic Neo', sans-serif";
        ctx.fillStyle = latest.score >= 80 ? "#10b981" : latest.score >= 60 ? "#f59e0b" : "#ef4444";
        ctx.fillText(String(latest.score), 400, 380);

        ctx.font = "24px 'Apple SD Gothic Neo', sans-serif";
        ctx.fillStyle = "#6b7280";
        ctx.fillText(latest.summary || "", 400, 450);
        ctx.fillText(latest.advice || "", 400, 490);
    }

    // 워터마크
    ctx.font = "20px 'Apple SD Gothic Neo', sans-serif";
    ctx.fillStyle = "rgba(0,0,0,0.2)";
    ctx.fillText("🐾 펫과나 (Pet & Na)", 400, 760);

    return canvas.toDataURL("image/png");
}

async function shareHealthCard() {
    const dataUrl = generateShareCard("health");
    const blob = await (await fetch(dataUrl)).blob();
    const file = new File([blob], "petna-health.png", { type: "image/png" });

    if (navigator.share && navigator.canShare({ files: [file] })) {
        await navigator.share({ files: [file], title: "펫 건강 분석 결과", text: "🐾 AI가 분석한 우리 펫 건강 점수 확인해요!" });
    } else {
        const a = document.createElement("a");
        a.href = dataUrl;
        a.download = "petna-health.png";
        a.click();
        showToast("건강 카드 이미지를 저장했습니다 📸");
    }
}
```

- [ ] **Step 2: AI 분석 결과 패널 아래에 공유 버튼 추가 (`js/templates/mypet.js`)**

AI 건강 분석 섹션의 disclaimer p 태그 위에:

```html
<div id="ai-health-share-btn-wrap" class="hidden flex justify-end">
    <button onclick="shareHealthCard()"
        class="flex items-center gap-1.5 px-3 py-1.5 bg-violet-100 hover:bg-violet-200 text-violet-700 font-black text-[11px] rounded-xl transition-all">
        <i class="fa-solid fa-share-nodes text-xs"></i> 공유 카드 저장
    </button>
</div>
```

- [ ] **Step 3: 분석 완료 후 공유 버튼 표시 (`js/mypet.js` `runAiHealthAnalysis` 함수 끝)**

```javascript
// resultEl.innerHTML = ... 이후에 추가
document.getElementById('ai-health-share-btn-wrap')?.classList.remove('hidden');
```

- [ ] **Step 4: `index.html`에 스크립트 추가**

```html
<script src="js/share-card.js"></script>
```

- [ ] **Step 5: 브라우저에서 확인**

AI 분석 완료 후 "공유 카드 저장" 버튼 클릭 → PNG 다운로드 또는 공유 시트 열리면 OK

- [ ] **Step 6: Commit**

```bash
git add js/share-card.js js/templates/mypet.js js/mypet.js index.html
git commit -m "feat: add health share card generator and share button"
```

---

## Phase 4: Freemium 모델

### Task 8: AI 분석 월 3회 무료 제한 + 프리미엄 업그레이드 UI

**Files:**
- Create: `js/freemium.js`
- Modify: `js/templates/mypet.js`

- [ ] **Step 1: `js/freemium.js` 생성**

```javascript
// freemium.js — 월별 AI 분석 횟수 추적 + 프리미엄 게이트

const FREE_LIMIT = 3;

function getMonthlyAiUsage() {
    const key = "petna_ai_health_count_" + new Date().toISOString().slice(0, 7);
    return parseInt(localStorage.getItem(key) || "0");
}

function isPremium() {
    return !!localStorage.getItem("petna_premium");
}

function getRemainingFreeAnalyses() {
    if (isPremium()) return Infinity;
    return Math.max(0, FREE_LIMIT - getMonthlyAiUsage());
}

function updateAiHealthUsageBadge() {
    const badge = document.getElementById("ai-health-usage-badge");
    if (!badge) return;
    if (isPremium()) {
        badge.textContent = "프리미엄 ∞";
        badge.className = "text-[10px] font-black text-amber-500 bg-amber-50 px-2 py-0.5 rounded-full";
    } else {
        const rem = getRemainingFreeAnalyses();
        badge.textContent = `이번 달 ${rem}회 남음`;
        badge.className = `text-[10px] font-black px-2 py-0.5 rounded-full ${rem > 0 ? 'text-violet-500 bg-violet-50' : 'text-red-500 bg-red-50'}`;
    }
}

function showPremiumModal() {
    const modal = document.getElementById("premium-modal");
    if (modal) modal.classList.remove("hidden");
}

function closePremiumModal() {
    const modal = document.getElementById("premium-modal");
    if (modal) modal.classList.add("hidden");
}

// 테스트용: 프리미엄 활성화 (실제 결제 연동 전 임시)
function activatePremiumDemo() {
    localStorage.setItem("petna_premium", "demo");
    closePremiumModal();
    updateAiHealthUsageBadge();
    showToast("프리미엄 활성화! AI 분석 무제한 사용 가능합니�� 🎉");
}
```

- [ ] **Step 2: `js/templates/mypet.js` AI 분석 섹션 헤더에 사용량 배지 추가**

AI 건강 분석 섹션의 버튼 왼쪽에:

```html
<span id="ai-health-usage-badge" class="text-[10px] font-black text-violet-500 bg-violet-50 px-2 py-0.5 rounded-full">
    이번 달 3회 남음
</span>
```

- [ ] **Step 3: 프리미엄 모달 HTML 추가 (`js/templates/mypet.js` 끝 또는 모달 섹션)**

```html
<!-- 프리미엄 업그레이드 모달 -->
<div id="premium-modal" class="hidden fixed inset-0 bg-black/50 z-50 flex items-end justify-center pb-8">
    <div class="bg-white rounded-3xl p-6 mx-4 max-w-sm w-full space-y-4 shadow-2xl">
        <div class="text-center space-y-2">
            <span class="text-4xl">👑</span>
            <h3 class="text-lg font-black text-gray-800">프리미엄으로 업그레이드</h3>
            <p class="text-sm text-gray-500">무료 AI 분석 3회를 모두 사용했습니다</p>
        </div>
        <div class="bg-violet-50 rounded-2xl p-4 space-y-2">
            <p class="text-xs font-black text-violet-700">프리미엄 혜택</p>
            <ul class="text-xs text-gray-600 space-y-1">
                <li>✅ AI 건강 분석 무제한</li>
                <li>✅ 월별 건강 리포트 PDF</li>
                <li>✅ 상세 건강 트렌드 분석</li>
            </ul>
        </div>
        <div class="space-y-2">
            <button onclick="activatePremiumDemo()"
                class="w-full py-3 bg-violet-500 hover:bg-violet-600 text-white font-black text-sm rounded-2xl transition-all">
                월 2,900원으로 시작하기
            </button>
            <button onclick="closePremiumModal()"
                class="w-full py-2 text-gray-400 font-bold text-sm">
                나중에
            </button>
        </div>
    </div>
</div>
```

- [ ] **Step 4: `js/mypet.js`의 `triggerAiHealthAnalysis()` 무료 한도 초과 시 모달 호출로 변경**

```javascript
// 기존 showToast 줄을 모달 호출로 교체:
if (used >= 3 && !localStorage.getItem("petna_premium")) {
    if (typeof showPremiumModal === 'function') showPremiumModal();
    else showToast("이번 달 무료 분석 3회를 모두 사용했습니다.");
    return;
}
```

- [ ] **Step 5: `renderMyPets()` 에 뱃지 업데이트 호출 추가**

```javascript
if (typeof updateAiHealthUsageBadge === 'function') updateAiHealthUsageBadge();
```

- [ ] **Step 6: `index.html`에 스크립트 추가**

```html
<script src="js/freemium.js"></script>
```

- [ ] **Step 7: 브라우저에서 확인**

1. AI 분석 3회 실행 → 4번째 클릭 시 프리미엄 모달 표시되면 OK
2. "월 2,900원으로 시작하기" 클릭 → 무제한 전환 확인

- [ ] **Step 8: Commit**

```bash
git add js/freemium.js js/templates/mypet.js js/mypet.js index.html
git commit -m "feat: add freemium gate with 3 free AI analyses/month and premium upgrade modal"
```

---

## 최종 검증

- [ ] **전체 탭 smoke test**
  - mypet: AI 분석 버튼, 트렌드 차트, 멀티펫 스와이프 정상 동작
  - social: AI 캡션 생성 버튼 표시
  - 프리미엄 모달 표시/닫기

- [ ] **다크모드 확인**: 테마 전환 시 차트 색상 올바른지 확인

- [ ] **모바일 확인**: 스와이프 카드 가로 스크롤, AI 분석 결과 패널 레이아웃

- [ ] **최종 Commit**

```bash
git add -A
git commit -m "feat: petnna AI health analysis + dashboard + swipe UI + freemium complete"
```
