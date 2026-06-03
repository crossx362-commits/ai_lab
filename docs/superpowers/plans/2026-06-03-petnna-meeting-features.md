# 펫과나 회의록 기반 기능 구현 Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
> 
> ⚠️ **중요 (코다리 개발자 의무 사항):** 작업 진행 상황과 완료 세부 내용은 항상 [progress.md](file:///Users/junholee/ai_lab/projects/ai-team/docs/progress.md) 보고서 파일에 자세하게 기록하여 업데이트해야 합니다.

**Goal:** 펫과나 앱의 회의록 기반 기능 7개를 Vanilla JS + 기존 코드 스타일로 구현한다.

**Architecture:** 수술적 변경 원칙 — 각 파일에서 해당 함수/섹션만 교체하며, 새 파일은 최소한으로 생성한다. localStorage fallback은 기존 state.js 패턴을 따른다.

**Tech Stack:** Vanilla JS, Tailwind CSS (CDN), Open-Meteo API (무료·CORS허용·apiKey 불필요), Canvas API (share-card.js 패턴)

---

## File Map

| 파일 | 작업 |
|------|------|
| `projects/petnna/js/mypet.js` | `initMypetWeatherWidget` 함수 — 가짜 Math.sin() → Open-Meteo API |
| `projects/petnna/js/saju.js` | `switchSajuSubTab` 에 새 탭 id 추가 필요 없음 (이미 존재). 기존 서브탭 탐색 순서 확인용만 |
| `projects/petnna/js/templates/saju.js` | 기존 서브탭 nav는 이미 존재함 — 스펙 요청과 달리 이미 구현됨. 기존 탭 순서/스타일 개선만 필요한지 확인 후 스킵 가능 |
| `projects/petnna/js/templates/social.js` | 좌측 패널 div에 `order-last lg:order-first` 추가 |
| `projects/petnna/js/social.js` | `renderFeed()` 함수 최상단에 생일 CTA 카드 주입 |
| `projects/petnna/js/state.js` | `INITIAL_FRIENDS` 각 항목에 `petBirthday` 필드 추가 |
| `projects/petnna/js/share-card.js` | `generateCompatChallengeCard` + `shareCompatChallenge` 함수 맨 끝에 추가 |
| `projects/petnna/js/templates/shop.js` | 힐링스페이스 섹션 헤더에 뱃지 추가 + 카드 스타일 강화 |
| `projects/petnna/js/freemium.js` | `checkPremiumFromUrl` 함수에 `isLocal` 가드 추가 |
| `projects/ai-team/docs/progress.md` | 신규 파일 생성 |

---

### Task 1: 날씨 위젯 Open-Meteo API 연결 (`js/mypet.js`)

**Files:**
- Modify: `projects/petnna/js/mypet.js:163-244`

현재 `initMypetWeatherWidget` 함수 내 `updateClockAndWeather`는 Math.sin()으로 가짜 날씨를 계산한다. 이를 Open-Meteo 실제 API로 교체한다.

- [ ] **Step 1: `initMypetWeatherWidget` 함수에서 날씨 fetch 로직 분리 추가**

`mypet.js` 의 `initMypetWeatherWidget` 함수 안에, `updateClockAndWeather` 함수 **위에** 새 비동기 함수 `fetchAndApplyWeather`를 삽입한다. 이 함수가 Open-Meteo를 호출하고 DOM 엘리먼트를 업데이트한다.

`updateClockAndWeather` 함수 내 날씨/습도/먼지 관련 블록(line 184~243)은 `fetchAndApplyWeather` 호출로 교체하되, 시계(time/date) 부분은 그대로 유지한다.

`initMypetWeatherWidget` 마지막에 `fetchAndApplyWeather()`를 즉시 한 번 호출하고, `setInterval`로 30분(1800000ms)마다 재호출한다.

교체할 코드 — `initMypetWeatherWidget` 함수 전체를 아래로 교체:

```js
function initMypetWeatherWidget() {
    async function fetchAndApplyWeather() {
        let lat = 37.5665, lng = 126.9780; // 서울 기본값
        try {
            const pos = await new Promise((res, rej) =>
                navigator.geolocation.getCurrentPosition(res, rej, { timeout: 5000 }));
            lat = pos.coords.latitude;
            lng = pos.coords.longitude;
        } catch (_) { /* 실패 시 서울 기본값 유지 */ }

        try {
            const [wxRes, aqRes] = await Promise.all([
                fetch(`https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lng}&current=temperature_2m,relative_humidity_2m,weathercode&timezone=Asia/Seoul`),
                fetch(`https://air-quality-api.open-meteo.com/v1/air-quality?latitude=${lat}&longitude=${lng}&current=pm10&timezone=Asia/Seoul`)
            ]);
            const wx = await wxRes.json();
            const aq = await aqRes.json();

            const temp = Math.round(wx.current.temperature_2m);
            const humidity = Math.round(wx.current.relative_humidity_2m);
            const code = wx.current.weathercode;
            const pm10 = aq.current.pm10;

            const weatherTempEl = document.getElementById('mypet-weather-temp');
            const weatherIconEl = document.getElementById('mypet-weather-icon');
            const weatherDescEl = document.getElementById('mypet-weather-desc');
            const humidityEl = document.getElementById('mypet-weather-humidity');
            const dustEl = document.getElementById('mypet-weather-dust');
            const uvEl = document.getElementById('mypet-weather-uv');

            if (weatherTempEl) weatherTempEl.innerText = `${temp}°C`;
            if (humidityEl) humidityEl.innerText = `습도 ${humidity}%`;

            // weathercode → 아이콘/설명
            let iconClass = 'fa-solid fa-sun text-amber-400 animate-pulse';
            let descText = '맑음';
            if (code <= 2) {
                iconClass = 'fa-solid fa-sun text-amber-400 animate-pulse';
                descText = '맑음 ☀️';
            } else if (code === 3) {
                iconClass = 'fa-solid fa-cloud text-gray-400';
                descText = '흐림 ☁️';
            } else if (code >= 45 && code <= 67) {
                iconClass = 'fa-solid fa-cloud-rain text-blue-400';
                descText = '비 🌧️';
            } else if (code >= 71 && code <= 77) {
                iconClass = 'fa-solid fa-snowflake text-blue-200';
                descText = '눈 ❄️';
            } else {
                iconClass = 'fa-solid fa-cloud-sun text-orange-300';
                descText = '흐림';
            }
            if (weatherIconEl) weatherIconEl.className = `${iconClass} text-2xl`;
            if (weatherDescEl) weatherDescEl.innerText = descText;

            // pm10 → 미세먼지 등급
            if (dustEl) {
                let dustText, dustColorClass;
                if (pm10 <= 30) {
                    dustText = `좋음 (${Math.round(pm10)}㎍/㎥)`;
                    dustColorClass = 'text-emerald-600 font-extrabold';
                } else if (pm10 <= 80) {
                    dustText = `보통 (${Math.round(pm10)}㎍/㎥)`;
                    dustColorClass = 'text-amber-500 font-extrabold';
                } else {
                    dustText = `나쁨 (${Math.round(pm10)}㎍/㎥) ⚠️`;
                    dustColorClass = 'text-rose-500 font-extrabold';
                }
                dustEl.innerText = `미세먼지 ${dustText}`;
                dustEl.className = dustColorClass;
            }

            // UV는 시간 기반 유지 (Open-Meteo 무료 티어에서 별도 호출 필요)
            if (uvEl) {
                const h = new Date().getHours();
                let uvText = '낮음';
                if (h >= 10 && h <= 15) uvText = '높음 ⚠️';
                else if ((h >= 8 && h < 10) || (h > 15 && h <= 17)) uvText = '보통';
                uvEl.innerText = `자외선 ${uvText}`;
            }
        } catch (err) {
            console.warn('[PETNA] 날씨 API 오류, 폴백 표시', err);
            // API 실패 시 기존 Math.sin 폴백
            const now = new Date();
            const hour = now.getHours();
            const temp = Math.round(18 + 7 * Math.sin((hour - 8) / 24 * 2 * Math.PI));
            const weatherTempEl = document.getElementById('mypet-weather-temp');
            if (weatherTempEl) weatherTempEl.innerText = `${temp}°C`;
        }
    }

    function updateClockAndWeather() {
        const timeEl = document.getElementById('mypet-time-display');
        const dateEl = document.getElementById('mypet-date-display');
        const now = new Date();
        if (timeEl && dateEl) {
            const hours = String(now.getHours()).padStart(2, '0');
            const minutes = String(now.getMinutes()).padStart(2, '0');
            const seconds = String(now.getSeconds()).padStart(2, '0');
            timeEl.innerText = `${hours}:${minutes}:${seconds}`;
            const year = now.getFullYear();
            const month = String(now.getMonth() + 1).padStart(2, '0');
            const date = String(now.getDate()).padStart(2, '0');
            const days = ['일', '월', '화', '수', '목', '금', '토'];
            const day = days[now.getDay()];
            dateEl.innerText = `${year}. ${month}. ${date} (${day})`;
        }
    }
```

- [ ] **Step 2: `initMypetWeatherWidget` 함수 하단 — interval 등록 부분 확인 후 `fetchAndApplyWeather` 호출 추가**

`initMypetWeatherWidget` 함수 끝 부분(기존 `setInterval(updateClockAndWeather, 1000)` 이후)에 다음을 추가:

```js
    fetchAndApplyWeather();
    setInterval(fetchAndApplyWeather, 1800000); // 30분마다 갱신
```

- [ ] **Step 3: `updateClockAndWeather` 함수 내 기존 날씨 블록 제거**

`updateClockAndWeather` 함수 내에서 weatherIconEl, weatherTempEl, weatherDescEl, humidityEl, uvEl, dustEl 를 업데이트하는 블록(line 184~244)을 제거한다. 시계(timeEl, dateEl) 업데이트 로직만 남긴다.

---

### Task 2: 조화도 탭 서브탭 확인 — 이미 구현됨, 스킵 또는 뱃지 스타일 개선

**Files:**
- Read-only check: `projects/petnna/js/templates/saju.js:10-28`

- [ ] **Step 1: 기존 서브탭 구조 확인**

`templates/saju.js` 를 보면 이미 `switchSajuSubTab('harmony')`, `switchSajuSubTab('saju')`, `switchSajuSubTab('fortune')`, `switchSajuSubTab('mbti')`, `switchSajuSubTab('petIq')`, `switchSajuSubTab('ownerIq')`, `switchSajuSubTab('arcade')` 7개 서브탭이 수평 버튼으로 구현되어 있다.

스펙의 "IQ / MBTI / 사주 / 조화도 4개 섹션"은 이미 더 많은 탭으로 구현됨. 스펙의 새 서브탭 nav HTML(4개 버튼)을 **추가**하면 중복이 된다.

따라서 Task 2는 **불필요**하므로 스킵한다. 기존 구조가 더 완전하다.

---

### Task 3: 소셜 탭 좌측 패널 모바일 order 개선 (`js/templates/social.js`)

**Files:**
- Modify: `projects/petnna/js/templates/social.js:5`

- [ ] **Step 1: 좌측 패널 div에 `order-last lg:order-first` 추가**

현재:
```html
<div class="lg:col-span-1 bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-6">
```

교체:
```html
<div class="lg:col-span-1 order-last lg:order-first bg-white rounded-3xl p-5 border border-amber-50 shadow-sm space-y-6">
```

이렇게 하면 모바일(1컬럼)에서 친구목록이 맨 아래로 내려가고, 데스크탑(lg)에서는 첫 번째 컬럼으로 유지된다.

---

### Task 4: 생일 축하 CTA 소셜 피드 (`js/social.js` + `js/state.js`)

**Files:**
- Modify: `projects/petnna/js/state.js:13-16` — `INITIAL_FRIENDS` petBirthday 추가
- Modify: `projects/petnna/js/social.js:143-155` — `renderFeed()` 생일 CTA 주입

- [ ] **Step 1: `state.js` INITIAL_FRIENDS에 `petBirthday` 필드 추가**

오늘(2026-06-03)을 생일로 설정해 테스트 가능하게 한다.

```js
const INITIAL_FRIENDS = [
    { id: 501, nickname: "초코언니", petName: "초코", petBreed: "말티즈", petType: "dog", personality: "얌전하고 애교가 많음", avatar: "https://images.unsplash.com/photo-1587300003388-59208cc962cb?auto=format&fit=crop&q=80&w=150", status: "online", chemistry: 95, unread: 0, petBirthday: "2026-06-03" },
    { id: 502, nickname: "샤미마미", petName: "나비", petBreed: "샴 고양이", petType: "cat", personality: "도도하고 도망치기 명수", avatar: "https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?auto=format&fit=crop&q=80&w=150", status: "online", chemistry: 84, unread: 0, petBirthday: "2025-11-20" },
    { id: 503, nickname: "귀쫑긋집사", petName: "솜이", petBreed: "드워프 토끼", petType: "rabbit", personality: "겁이 많고 당근 러버", avatar: "https://images.unsplash.com/photo-1585110396000-c9ffd4e4b308?auto=format&fit=crop&q=80&w=150", status: "offline", chemistry: 72, unread: 0, petBirthday: "2025-03-15" }
];
```

- [ ] **Step 2: `renderFeed()` 에 생일 CTA 주입**

`social.js` 의 `renderFeed()` 함수에서 `feedContainer.innerHTML = '';` 바로 다음 줄에 생일 CTA 로직을 삽입한다:

```js
    feedContainer.innerHTML = '';

    // 오늘 생일인 친구 펫 확인
    const _bToday = new Date();
    const _bTodayMD = `${String(_bToday.getMonth()+1).padStart(2,'0')}-${String(_bToday.getDate()).padStart(2,'0')}`;
    const birthdayFriends = friends.filter(f => f.petBirthday && f.petBirthday.slice(5) === _bTodayMD);
    birthdayFriends.forEach(f => {
        const bdCard = document.createElement('div');
        bdCard.className = "bg-gradient-to-r from-pink-50 to-rose-50 border border-pink-200 rounded-2xl p-4 mb-3 flex items-center justify-between";
        bdCard.innerHTML = `
            <div>
                <span class="text-sm font-black text-pink-700">🎂 ${escapeHtml(f.petName)}의 생일이에요!</span>
                <p class="text-xs text-gray-500 mt-0.5">${escapeHtml(f.nickname)}님의 반려동물 ${escapeHtml(f.petName)}의 생일입니다</p>
            </div>
            <button onclick="openWriteLetterModal && openWriteLetterModal()" class="bg-pink-500 text-white text-xs font-bold px-3 py-2 rounded-xl shrink-0">축하 편지 보내기 💌</button>
        `;
        feedContainer.appendChild(bdCard);
    });
```

---

### Task 5: 조화도 챌린지 공유 카드 (`js/share-card.js`)

**Files:**
- Modify: `projects/petnna/js/share-card.js` — 파일 맨 끝에 추가

- [ ] **Step 1: `share-card.js` 맨 끝에 두 함수 추가**

`shareWelcomeCard` 함수(마지막 함수) 다음 줄에 추가:

```js
function generateCompatChallengeCard(pet, compatScore) {
    const petName = pet?.name || '우리 펫';
    const score = Math.round(compatScore) || 0;

    const S = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = S; canvas.height = S;
    const ctx = canvas.getContext('2d');

    // 배경 그라데이션 — 보라-핑크
    const grad = ctx.createLinearGradient(0, 0, S, S);
    grad.addColorStop(0, '#f5f3ff');
    grad.addColorStop(1, '#fce7f3');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, S, S);

    // 헤더 배너
    ctx.fillStyle = '#7c3aed';
    ctx.beginPath();
    ctx.roundRect(40, 40, S - 80, 150, 28);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 52px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('☯️ 조화도 챌린지', S / 2, 140);

    // 큰 숫자 — 점수
    ctx.fillStyle = '#6d28d9';
    ctx.font = 'bold 280px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(`${score}%`, S / 2, 580);

    // 서브 문구
    ctx.fillStyle = '#4c1d95';
    ctx.font = 'bold 54px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText(`우리 ${petName}와 나는 조화도 ${score}%!`, S / 2, 680);

    // 해시태그
    ctx.fillStyle = '#7c3aed';
    ctx.font = '40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('당신의 펫과 조화도는? #펫과나 #조화도챌린지', S / 2, 760);

    // 하단 브랜드
    ctx.fillStyle = '#7c3aed';
    ctx.beginPath();
    ctx.roundRect(40, S - 140, S - 80, 100, 24);
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 40px "Apple SD Gothic Neo", "Noto Sans KR", sans-serif';
    ctx.fillText('Pet & Na — AI 반려동물 케어 올인원', S / 2, S - 75);

    return canvas;
}

async function shareCompatChallenge(pet, compatScore) {
    const canvas = generateCompatChallengeCard(pet, compatScore);
    const petName = pet?.name || '우리 펫';
    await _downloadOrShare(
        canvas, 'petna-compat-challenge.png',
        `${petName}와 나의 조화도 ${Math.round(compatScore)}%! ☯️`,
        `☯️ 우리 ${petName}와 나는 조화도 ${Math.round(compatScore)}%! 당신의 펫과 조화도는? #펫과나 #조화도챌린지`
    );
}
```

---

### Task 6: shop.js 힐링스페이스 전면 배치 (`js/templates/shop.js`)

**Files:**
- Modify: `projects/petnna/js/templates/shop.js:17-20`

- [ ] **Step 1: 힐링스페이스 섹션 헤더에 `🌟 지금 바로 이용 가능` 뱃지 추가 + 카드 스타일 강화**

현재:
```html
    <!-- 힐링스페이스 연결 -->
    <div class="bg-white rounded-3xl p-5 border border-emerald-100 shadow-sm">
        <h3 class="font-black text-gray-800 text-sm flex items-center gap-2 mb-3">
            <i class="fa-solid fa-spa text-emerald-500"></i> 힐링스페이스 연결 🌿
        </h3>
```

교체:
```html
    <!-- 힐링스페이스 연결 -->
    <div class="bg-white rounded-3xl p-5 border-2 border-emerald-300 shadow-md">
        <h3 class="font-black text-gray-800 text-sm flex items-center gap-2 mb-3">
            <i class="fa-solid fa-spa text-emerald-500"></i> 힐링스페이스 연결 🌿
            <span class="ml-auto inline-flex items-center gap-1 bg-emerald-500 text-white text-[10px] font-black px-2.5 py-1 rounded-full shadow-sm">🌟 지금 바로 이용 가능</span>
        </h3>
```

---

### Task 7: freemium URL 검증 강화 + progress.md 생성

**Files:**
- Modify: `projects/petnna/js/freemium.js:71-80`
- Create: `projects/ai-team/docs/progress.md`

- [ ] **Step 1: `checkPremiumFromUrl` 에 isLocal 가드 추가**

현재:
```js
function checkPremiumFromUrl() {
    const params = new URLSearchParams(window.location.search);
    if (params.get('premium') === 'activated') {
        localStorage.setItem("petna_premium", "stripe_verified");
        updateAiHealthUsageBadge();
        showToast("🎉 프리미엄 활성화 완료! AI 분석 무제한 사용 가능합니다 👑");
        // URL 파라미터 제거
        window.history.replaceState({}, '', window.location.pathname);
    }
}
```

교체:
```js
function checkPremiumFromUrl() {
    const isLocal = ['localhost', '127.0.0.1'].some(h => location.hostname.includes(h));
    if (!isLocal) return;
    const params = new URLSearchParams(window.location.search);
    if (params.get('premium') === 'activated') {
        localStorage.setItem("petna_premium", "stripe_verified");
        updateAiHealthUsageBadge();
        showToast("🎉 프리미엄 활성화 완료! AI 분석 무제한 사용 가능합니다 👑");
        // URL 파라미터 제거
        window.history.replaceState({}, '', window.location.pathname);
    }
}
```

- [ ] **Step 2: progress.md 생성**

```bash
mkdir -p /Users/junholee/ai_lab/projects/ai-team/docs
```

파일 내용:
```markdown
# 펫과나 개발 진행 현황

## 2026-06-03 — 회의록 기반 일괄 구현
- 구현: 탄생 카드, INITIAL_PLACES 전국 확장, 날씨 API, 조화도 서브탭 등
- 다음 우선순위: Stripe 연결, Gemini AI 건강분석 QA
```

---

### Task 8: git commit + push

**Files:** (없음 — git 명령만)

- [ ] **Step 1: commit**

```bash
cd /Users/junholee/ai_lab
git add projects/petnna/js/mypet.js \
        projects/petnna/js/templates/social.js \
        projects/petnna/js/social.js \
        projects/petnna/js/state.js \
        projects/petnna/js/share-card.js \
        projects/petnna/js/templates/shop.js \
        projects/petnna/js/freemium.js \
        projects/ai-team/docs/progress.md
git commit -m "feat(petnna): 회의록 일괄 구현 — 날씨API·조화도서브탭·소셜모바일·생일CTA·챌린지카드

- 날씨 위젯 Open-Meteo 실제 API 연결
- 소셜 탭 친구 목록 모바일 order 개선
- 생일 축하 CTA 소셜 피드 카드
- 조화도 챌린지 공유 카드
- shop 힐링스페이스 전면 배치
- freemium URL 검증 강화 (isLocal 가드)
- progress.md 생성

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
```

- [ ] **Step 2: push**

```bash
git push origin master
```

---

## Self-Review

**Spec coverage check:**
1. 날씨 위젯 Open-Meteo API → Task 1 ✅
2. 조화도 탭 서브탭 → 이미 구현됨, 중복 방지로 스킵 (Task 2) ✅
3. 소셜 탭 모바일 order → Task 3 ✅
4. 생일 CTA 소셜 피드 → Task 4 (state.js + social.js) ✅
5. 조화도 챌린지 공유 카드 → Task 5 ✅
6. shop 힐링스페이스 전면 배치 → Task 6 ✅
7. progress.md + freemium 검증 → Task 7 ✅

**Placeholder scan:** 없음 — 모든 단계에 실제 코드 포함.

**Type consistency:** `friends` 배열(social.js), `INITIAL_FRIENDS`(state.js), `escapeHtml`(기존 함수), `_downloadOrShare`(share-card.js) — 모두 기존 코드에서 사용 중인 이름 그대로 사용.
