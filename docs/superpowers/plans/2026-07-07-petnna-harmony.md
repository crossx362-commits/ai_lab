# 펫과나 조화도 탭 리뉴얼 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) 문법.

**Goal:** 조화도 탭에 명리 기반 오행 리포트·일진 캘린더·펫게임 버프를 추가한다.

**Architecture:** 신규 `js/harmony/harmony-core.js`(순수 계산, UMD-lite, node 테스트) + `js/harmony/harmony-view.js`(DOM 렌더). 기존 saju.js는 훅 호출만. 게임 버프는 game-core/stage에 typeof 가드 훅.

**Tech Stack:** 바닐라 JS 전역 스크립트(ES 모듈 금지), Tailwind 유틸, node assert 테스트.

**Spec:** `docs/superpowers/specs/2026-07-07-petnna-harmony-design.md`

## Global Constraints
- ES 모듈 금지 — `window.PetHarmony` + `if (typeof module !== 'undefined') module.exports = ...`
- 기존 `pet.harmonyData` 소비자(`avgScore` 등) 무수정 호환 — 필드 추가만, 기존 필드 유지
- 오행 색: 목 #7BA05B · 화 #E05C5C · 토 #E8A55A · 금 #B9A88C · 수 #5C8FC9
- 궁합·영역·일진 산식은 스펙 §1 수치 그대로. 클램프 5~99, 반올림 정수
- 갑자 앵커(테스트 필수): 1984년=갑자년, 2026년=병오년, 1900-01-01=갑술일, 2000-01-01=무오일
- 게임 버프: 오늘지수 ≥70 → 먹이 XP ×1.2 반올림 / harmonyData 존재 → decay ×0.8. 하모니 모듈 부재 시 완전 무영향(typeof 가드)
- 수정 파일 ?v 상향(+1), 신규 파일 ?v=1. 커밋 말미 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

---

### Task 1: 명리 계산 코어 (harmony-core.js)

**Files:**
- Create: `projects/petnna/js/harmony/harmony-core.js`
- Test: `projects/petnna/tests/harmony/test_core.js`

**Interfaces (전역 PetHarmony / module.exports):**
- `yearPillar(y)` → `{stem, branch, stemEl, branchEl}` (한글 글자·오행명)
- `dayPillar(date)` → 동일 형태 (date: Date)
- `elementsOf(birthISO)` → `{vec:{목,화,토,금,수}, list:[5원소명], dominant}` — [년간,년지,월지,일간,일지] 집계, dominant=최다(동률 시 목화토금수 순 먼저)
- `harmony(petISO, ownerISO)` → `{score, pet:{vec,dominant}, owner:{vec,dominant}, relation:{type:'상생'|'상극'|'동일'|'중립', text}}`
- `areas(petISO, ownerISO)` → `[{key,name,emoji,element,score,grade,tip}]` 4종 (놀이🎾화·식사🍚토·산책🐾목·휴식💤수)
- `dayLuck(date, petDominant)` → `{walk:boolean, groom:boolean, vet:boolean, dayEl}`
- `todayIndex(harmonyScore, petDominant, date?)` → number(5~99) — 일진 보정 ±15
- `analyze(petISO, ownerISO)` → 위 전부 묶은 `{score, elements, areas, relation, measuredAt}` (harmonyData 병합용)

- [ ] **Step 1: 실패하는 테스트 작성** — `projects/petnna/tests/harmony/test_core.js`:

```js
const assert = require('assert');
const H = require('../../js/harmony/harmony-core.js');

// 갑자 앵커
assert.strictEqual(H.yearPillar(1984).stem + H.yearPillar(1984).branch, '갑자');
assert.strictEqual(H.yearPillar(2026).stem + H.yearPillar(2026).branch, '병오');
const d1900 = H.dayPillar(new Date(Date.UTC(1900, 0, 1)));
assert.strictEqual(d1900.stem + d1900.branch, '갑술');
const d2000 = H.dayPillar(new Date(Date.UTC(2000, 0, 1)));
assert.strictEqual(d2000.stem + d2000.branch, '무오');

// 오행 벡터: 5글자 집계, 합 5
const e = H.elementsOf('2024-03-15');
assert.strictEqual(Object.values(e.vec).reduce((a, b) => a + b, 0), 5);
assert.ok(['목','화','토','금','수'].includes(e.dominant));

// 궁합: 결정성·범위·대칭
const h1 = H.harmony('2024-03-15', '1990-07-07');
const h2 = H.harmony('2024-03-15', '1990-07-07');
assert.strictEqual(h1.score, h2.score);
assert.ok(h1.score >= 5 && h1.score <= 99);
const hSwap = H.harmony('1990-07-07', '2024-03-15');
assert.strictEqual(h1.score, hSwap.score); // 상생/상극 판정이 양방향이라 대칭
assert.ok(['상생','상극','동일','중립'].includes(h1.relation.type));
assert.ok(h1.relation.text.length > 5);

// 영역 4종: 키·범위·등급·팁
const ar = H.areas('2024-03-15', '1990-07-07');
assert.strictEqual(ar.length, 4);
assert.deepStrictEqual(ar.map(a => a.key), ['play','meal','walk','rest']);
for (const a of ar) {
  assert.ok(a.score >= 5 && a.score <= 99, a.key);
  assert.ok(['최고','좋음','보통','노력','주의'].includes(a.grade), a.key);
  assert.ok(a.tip.length > 5, a.key);
}

// 일진: boolean 3종 + 오늘지수 범위·보정 방향
const luck = H.dayLuck(new Date(Date.UTC(2026, 6, 7)), '수');
assert.ok(['walk','groom','vet'].every(k => typeof luck[k] === 'boolean'));
const idx = H.todayIndex(70, '수', new Date(Date.UTC(2026, 6, 7)));
assert.ok(idx >= 5 && idx <= 99);
assert.ok(Math.abs(idx - 70) <= 15);

// analyze: harmonyData 병합용 형태
const full = H.analyze('2024-03-15', '1990-07-07');
assert.ok(full.score && full.elements.pet.vec && full.areas.length === 4 && full.measuredAt);
console.log('harmony test_core OK');
```

- [ ] **Step 2: 실패 확인** — Run: `cd projects/petnna && node tests/harmony/test_core.js` → `Cannot find module`

- [ ] **Step 3: 구현** — `projects/petnna/js/harmony/harmony-core.js`:

```js
// 조화도 명리 코어 — 갑자/오행/궁합/일진 순수 계산(DOM 금지). UMD-lite.
(function (root) {
    const STEMS = ['갑','을','병','정','무','기','경','신','임','계'];
    const BRANCHES = ['자','축','인','묘','진','사','오','미','신','유','술','해'];
    const STEM_EL = ['목','목','화','화','토','토','금','금','수','수'];
    const BRANCH_EL = { 자:'수', 축:'토', 인:'목', 묘:'목', 진:'토', 사:'화', 오:'화', 미:'토', 신:'금', 유:'금', 술:'토', 해:'수' };
    const MONTH_BRANCH = ['축','인','묘','진','사','오','미','신','유','술','해','자']; // 1~12월(간이)
    const SHENG = { 목:'화', 화:'토', 토:'금', 금:'수', 수:'목' };  // 상생
    const KE = { 목:'토', 토:'수', 수:'화', 화:'금', 금:'목' };      // 상극
    const ELS = ['목','화','토','금','수'];

    function yearPillar(y) {
        const s = ((y - 4) % 10 + 10) % 10, b = ((y - 4) % 12 + 12) % 12;
        return { stem: STEMS[s], branch: BRANCHES[b], stemEl: STEM_EL[s], branchEl: BRANCH_EL[BRANCHES[b]] };
    }

    function dayPillar(date) {
        const jdn = Math.floor(date.getTime() / 86400000) + 2440588;
        const idx = ((jdn + 49) % 60 + 60) % 60;
        const s = idx % 10, b = idx % 12;
        return { stem: STEMS[s], branch: BRANCHES[b], stemEl: STEM_EL[s], branchEl: BRANCH_EL[BRANCHES[b]] };
    }

    function _parseISO(iso) {
        const [y, m, d] = String(iso).slice(0, 10).split('-').map(Number);
        return { y, m: m || 1, d: d || 1, date: new Date(Date.UTC(y, (m || 1) - 1, d || 1)) };
    }

    function elementsOf(birthISO) {
        const { y, m, date } = _parseISO(birthISO);
        const yp = yearPillar(y);
        const mb = MONTH_BRANCH[m - 1];
        const dp = dayPillar(date);
        const list = [yp.stemEl, yp.branchEl, BRANCH_EL[mb], dp.stemEl, dp.branchEl];
        const vec = { 목: 0, 화: 0, 토: 0, 금: 0, 수: 0 };
        list.forEach(e => { vec[e] += 1; });
        const dominant = ELS.reduce((best, e) => vec[e] > vec[best] ? e : best, '목');
        return { vec, list, dominant };
    }

    function _rel(a, b) {
        if (a === b) return 'same';
        if (SHENG[a] === b || SHENG[b] === a) return 'sheng';
        if (KE[a] === b || KE[b] === a) return 'ke';
        return 'neutral';
    }

    const REL_TEXT = {
        상생: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 서로를 살려주는 상생 궁합이에요. 함께할수록 기운이 자라나요!`,
        상극: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 부딪히기 쉬운 상극이지만, 그만큼 서로를 단련시키는 관계예요. 아래 가이드를 참고하세요.`,
        동일: (p) => `둘 다 ${p} 기운 — 닮은꼴 영혼이라 말하지 않아도 통해요. 다만 같은 데서 지치니 환기가 필요해요.`,
        중립: (p, o) => `${p} 기운의 펫과 ${o} 기운의 집사 — 서로의 영역을 존중하는 담백한 궁합이에요. 꾸준함이 힘이 됩니다.`,
    };

    function harmony(petISO, ownerISO) {
        const pet = elementsOf(petISO), owner = elementsOf(ownerISO);
        let sheng = 0, ke = 0, same = 0;
        for (const a of pet.list) for (const b of owner.list) {
            const r = _rel(a, b);
            if (r === 'sheng') sheng++; else if (r === 'ke') ke++; else if (r === 'same') same++;
        }
        const n = pet.list.length * owner.list.length;
        const score = Math.round(Math.max(5, Math.min(99, 50 + (sheng / n) * 40 - (ke / n) * 25 + (same / n) * 10)));
        const rd = _rel(pet.dominant, owner.dominant);
        const type = rd === 'sheng' ? '상생' : rd === 'ke' ? '상극' : rd === 'same' ? '동일' : '중립';
        const text = type === '동일' ? REL_TEXT.동일(pet.dominant) : REL_TEXT[type](pet.dominant, owner.dominant);
        return { score, pet, owner, relation: { type, text } };
    }

    const AREAS = [
        { key: 'play', name: '놀이', emoji: '🎾', element: '화' },
        { key: 'meal', name: '식사', emoji: '🍚', element: '토' },
        { key: 'walk', name: '산책', emoji: '🐾', element: '목' },
        { key: 'rest', name: '휴식', emoji: '💤', element: '수' },
    ];
    const TIPS = {
        play: { 최고: '놀이 궁합 만점! 새 장난감이 최고의 선물이에요', 좋음: '짧고 굵은 놀이가 잘 맞아요. 하루 2번 10분씩!', 보통: '펫이 좋아하는 놀이 하나를 정해 루틴으로 만들어보세요', 노력: '놀이 전 간식으로 텐션을 올려주면 훨씬 잘 놀아요', 주의: '과격한 놀이보다 노즈워크 같은 차분한 놀이가 길해요' },
        meal: { 최고: '먹복이 타고났어요. 규칙적인 식사가 건강운을 지켜줘요', 좋음: '식사 시간을 일정하게 — 토(土) 기운은 규칙에서 자라요', 보통: '간식보다 주식에 정성을. 식기를 깨끗이 하면 운이 트여요', 노력: '급하게 먹는 습관 주의 — 슬로우 식기를 써보세요', 주의: '소화가 약할 수 있어요. 소량씩 나눠 급여하는 게 좋아요' },
        walk: { 최고: '산책이 곧 보약! 새로운 길을 함께 개척해보세요', 좋음: '아침 산책이 특히 길해요 — 목(木) 기운이 아침에 자라요', 보통: '매일 같은 시간 산책이 둘의 리듬을 맞춰줘요', 노력: '산책 거리보다 냄새 맡을 시간을 충분히 주세요', 주의: '무리한 장거리보다 짧은 산책 여러 번이 좋아요' },
        rest: { 최고: '함께 쉬기만 해도 충전되는 사이! 낮잠 메이트네요', 좋음: '조용한 휴식 공간을 만들어주면 애착이 깊어져요', 보통: '휴식 중엔 건드리지 않기 — 수(水) 기운은 고요에서 회복돼요', 노력: '잠자리를 어둡고 아늑하게 바꿔보세요', 주의: '수면이 예민할 수 있어요. 소음을 줄여주는 게 우선이에요' },
    };

    function _grade(s) { return s >= 85 ? '최고' : s >= 70 ? '좋음' : s >= 55 ? '보통' : s >= 40 ? '노력' : '주의'; }

    function areas(petISO, ownerISO) {
        const h = harmony(petISO, ownerISO);
        return AREAS.map(a => {
            const strength = (h.pet.vec[a.element] + h.owner.vec[a.element]) / 10; // 0~1
            const bonus = (SHENG[h.pet.dominant] === a.element || SHENG[h.owner.dominant] === a.element) ? 10 : 0;
            const score = Math.round(Math.max(5, Math.min(99, h.score * 0.5 + strength * 40 + bonus)));
            const grade = _grade(score);
            return { ...a, score, grade, tip: TIPS[a.key][grade] };
        });
    }

    function dayLuck(date, petDominant) {
        const dp = dayPillar(date);
        const els = [dp.stemEl, dp.branchEl];
        const shengWithPet = els.some(e => SHENG[e] === petDominant || SHENG[petDominant] === e);
        return {
            walk: (els.includes('목') || els.includes('화')) && shengWithPet,
            groom: dp.stemEl === '금',
            vet: (els.includes('수') || els.includes('토')) && shengWithPet,
            dayEl: dp.stemEl,
        };
    }

    function todayIndex(harmonyScore, petDominant, date) {
        const d = date || new Date();
        const el = dayPillar(d).stemEl;
        const r = _rel(el, petDominant);
        const adj = r === 'sheng' ? 15 : r === 'ke' ? -15 : 0;
        return Math.round(Math.max(5, Math.min(99, (harmonyScore || 60) + adj)));
    }

    function analyze(petISO, ownerISO) {
        const h = harmony(petISO, ownerISO);
        return {
            score: h.score,
            elements: { pet: h.pet, owner: h.owner },
            relation: h.relation,
            areas: areas(petISO, ownerISO),
            measuredAt: new Date().toISOString().slice(0, 10),
        };
    }

    const api = { yearPillar, dayPillar, elementsOf, harmony, areas, dayLuck, todayIndex, analyze };
    root.PetHarmony = api;
    if (typeof module !== 'undefined') module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
```

- [ ] **Step 4: 테스트 통과** — `node tests/harmony/test_core.js` → `harmony test_core OK`. 기존 petgame 테스트 3종 회귀 확인.
- [ ] **Step 5: 커밋** — `git add projects/petnna/js/harmony/harmony-core.js projects/petnna/tests/harmony/test_core.js && git commit -m "feat(harmony): 명리 계산 코어 — 갑자·오행·궁합·영역·일진 (Task 1)"`

---

### Task 2: 리포트 뷰 + 조화도 탭 통합 (harmony-view.js)

**Files:**
- Create: `projects/petnna/js/harmony/harmony-view.js`
- Modify: `projects/petnna/js/templates/saju.js` (`harmony-result-container` 닫힘 div 직후에 `<div id="harmony-plus"></div>` 추가)
- Modify: `projects/petnna/js/saju.js` (측정 완료·탭 진입 훅)
- Modify: `projects/petnna/index.html` (harmony 2파일 로드 ?v=1 — state.js 이후 saju.js 이전, 수정 파일 ?v +1)

**Interfaces:**
- Consumes: `PetHarmony.analyze/todayIndex`, 기존 `getSajuPet()`, `pet.harmonyData`, 펫 생일 입력값(`#saju-pet-birth`)·집사(`#saju-owner-birth`)
- Produces (전역 `PetHarmonyView`): `renderPlus(containerId, harmonyData)` — 오행 차트+관계 요약+영역 카드+오늘 가이드 / `enrichOnMeasure()` — 측정 완료 시 pet.harmonyData에 `PetHarmony.analyze` 결과 병합 후 renderPlus

- [ ] **Step 1: harmony-view.js 구현** (전체 코드):

```js
// 조화도 리포트 뷰 — PetHarmony 계산 결과를 카드로 렌더. DOM 전용.
(function (root) {
    const EL_COLOR = { 목: '#7BA05B', 화: '#E05C5C', 토: '#E8A55A', 금: '#B9A88C', 수: '#5C8FC9' };
    const EL_HANJA = { 목: '木', 화: '火', 토: '土', 금: '金', 수: '水' };
    const ELS = ['목', '화', '토', '금', '수'];

    function bar(label, petN, ownerN) {
        const c = EL_COLOR[label];
        const w = (n) => Math.max(4, n * 20);
        return `
        <div class="flex items-center gap-2 text-[11px] font-bold">
          <span class="w-10 shrink-0" style="color:${c}">${label}(${EL_HANJA[label]})</span>
          <div class="flex-1 space-y-0.5">
            <div class="h-[7px] rounded-full" style="width:${w(petN)}%;background:${c}"></div>
            <div class="h-[7px] rounded-full opacity-45" style="width:${w(ownerN)}%;background:${c}"></div>
          </div>
          <span class="w-8 text-right text-gray-400">${petN}·${ownerN}</span>
        </div>`;
    }

    function renderPlus(containerId, hd) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (!hd || !hd.elements) { el.innerHTML = ''; return; }
        const pet = hd.elements.pet, owner = hd.elements.owner;
        const areaCard = (a) => `
          <div class="bg-white rounded-2xl border border-rose-100 p-3 text-center">
            <div class="text-[20px]">${a.emoji}</div>
            <div class="text-[11px] font-black mt-0.5">${a.name}</div>
            <div class="text-[16px] font-black" style="color:${EL_COLOR[a.element]}">${a.score}<span class="text-[10px] text-gray-400">점</span></div>
            <div class="text-[10px] font-bold text-rose-500">${a.grade}</div>
            <p class="text-[10px] text-gray-500 mt-1 leading-snug">${a.tip}</p>
          </div>`;
        const luck = (typeof PetHarmony !== 'undefined')
            ? PetHarmony.todayIndex(hd.score, pet.dominant) : null;
        el.innerHTML = `
        <div class="mt-4 space-y-4">
          <div class="bg-white rounded-3xl p-4 border border-rose-100 shadow-sm">
            <h4 class="text-xs font-black text-gray-700 mb-1">🌿 오행 분포 <span class="text-[10px] font-bold text-gray-400">진한색=펫 · 연한색=집사</span></h4>
            <div class="flex gap-2 mb-2">
              <span class="text-[10px] font-black px-2 py-0.5 rounded-full text-white" style="background:${EL_COLOR[pet.dominant]}">펫: ${pet.dominant}(${EL_HANJA[pet.dominant]}) 기운</span>
              <span class="text-[10px] font-black px-2 py-0.5 rounded-full text-white" style="background:${EL_COLOR[owner.dominant]}">집사: ${owner.dominant}(${EL_HANJA[owner.dominant]}) 기운</span>
            </div>
            <div class="space-y-1.5">${ELS.map(e => bar(e, pet.vec[e], owner.vec[e])).join('')}</div>
            <p class="text-[11px] text-gray-600 mt-3 leading-snug bg-rose-50/60 rounded-xl p-2.5">${hd.relation ? hd.relation.text : ''}</p>
          </div>
          <div>
            <h4 class="text-xs font-black text-gray-700 mb-2">💞 영역별 궁합</h4>
            <div class="grid grid-cols-2 gap-2">${(hd.areas || []).map(areaCard).join('')}</div>
          </div>
          ${luck !== null ? `<div class="bg-gradient-to-r from-rose-50 to-amber-50 rounded-2xl border border-rose-100 p-3 text-center">
            <span class="text-[11px] font-black text-gray-600">오늘의 궁합 지수</span>
            <span class="text-[18px] font-black text-rose-600 ml-2">${luck}</span>
            <p class="text-[10px] text-gray-500 mt-1">${luck >= 70 ? '💞 오늘은 함께하기 좋은 날! 펫게임 XP 1.2배 버프 발동 중' : '평온한 하루 — 꾸준한 케어가 내일의 운을 만들어요'}</p>
          </div>` : ''}
        </div>`;
    }

    function enrichOnMeasure() {
        try {
            const petB = document.getElementById('saju-pet-birth');
            const ownB = document.getElementById('saju-owner-birth');
            const pet = (typeof getSajuPet === 'function') ? getSajuPet() : null;
            const petISO = (petB && petB.value) || (pet && pet.harmonyData && pet.harmonyData.petBirth);
            const ownISO = (ownB && ownB.value) || (pet && pet.harmonyData && pet.harmonyData.ownerBirth);
            if (!pet || !petISO || !ownISO || typeof PetHarmony === 'undefined') return;
            const full = PetHarmony.analyze(petISO, ownISO);
            pet.harmonyData = Object.assign({}, pet.harmonyData, full,
                { petBirth: petISO, ownerBirth: ownISO, avgScore: (pet.harmonyData && pet.harmonyData.avgScore) || full.score });
            if (typeof saveState === 'function') saveState();
            renderPlus('harmony-plus', pet.harmonyData);
        } catch (e) { console.error('[harmony] enrich 실패', e); }
    }

    root.PetHarmonyView = { renderPlus, enrichOnMeasure };
})(typeof window !== 'undefined' ? window : globalThis);
```

- [ ] **Step 2: 템플릿·saju.js 훅**
  - `js/templates/saju.js`: `harmony-result-container` div의 닫힘 태그 직후 `<div id="harmony-plus"></div>` 삽입 (컨테이너 경계는 grep으로 확인)
  - `js/saju.js`: ① `switchSajuSubTab`의 harmony 분기 끝에:
    ```js
    if (tabName === 'harmony' && typeof PetHarmonyView !== 'undefined') {
        const p = (typeof getSajuPet === 'function') ? getSajuPet() : null;
        PetHarmonyView.renderPlus('harmony-plus', p && p.harmonyData);
    }
    ```
    ② 조화도 측정 완료 지점(결과 컨테이너 표시 직후 — `harmony-result-container` classList.remove('hidden') 하는 함수 grep)에 `if (typeof PetHarmonyView !== 'undefined') PetHarmonyView.enrichOnMeasure();`
  - `index.html`: state.js 다음에 `harmony/harmony-core.js?v=1`, `harmony/harmony-view.js?v=1` 추가, saju.js·templates/saju.js ?v +1

- [ ] **Step 3: 검증** — node --check 2파일 + preview에서 조화도 측정 → 리포트(오행 바·영역 카드·오늘 지수) 렌더 확인, 재진입 시 자동 렌더 확인. 커밋.

---

### Task 3: 일진 캘린더 (오늘의 운세 탭)

**Files:**
- Modify: `projects/petnna/js/harmony/harmony-view.js` (renderCalendar 추가·export)
- Modify: `projects/petnna/js/templates/saju.js` (`fortune-test-section` 최상단에 `<div id="fortune-calendar"></div>`)
- Modify: `projects/petnna/js/saju.js` (`switchSajuSubTab` fortune 분기에 렌더 훅)

**Interfaces:**
- Produces: `PetHarmonyView.renderCalendar(containerId)` — 내부 상태 `_calMonth`(기본 이번 달), `PetHarmonyView.calMove(delta)` 월 이동

- [ ] **Step 1: renderCalendar 구현** — harmony-view.js에 추가:

```js
    let _cal = null; // {y, m}
    function calMove(delta) {
        if (!_cal) return;
        _cal.m += delta;
        if (_cal.m < 0) { _cal.m = 11; _cal.y--; }
        if (_cal.m > 11) { _cal.m = 0; _cal.y++; }
        renderCalendar('fortune-calendar');
    }

    function renderCalendar(containerId) {
        const el = document.getElementById(containerId);
        if (!el) return;
        const pet = (typeof getSajuPet === 'function') ? getSajuPet() : null;
        const hd = pet && pet.harmonyData;
        if (!hd || !hd.elements || typeof PetHarmony === 'undefined') {
            el.innerHTML = `<div class="bg-white rounded-3xl p-5 border border-amber-100 text-center">
              <p class="text-xs font-bold text-gray-500">일진 캘린더는 조화도 측정 후 열려요</p>
              <button onclick="switchSajuSubTab('harmony')" class="mt-2 text-[11px] font-black text-white bg-rose-400 px-4 py-2 rounded-xl">💞 조화도 측정하러 가기</button></div>`;
            return;
        }
        const now = new Date();
        if (!_cal) _cal = { y: now.getFullYear(), m: now.getMonth() };
        const dom = pet.harmonyData.elements.pet.dominant;
        const first = new Date(Date.UTC(_cal.y, _cal.m, 1));
        const days = new Date(Date.UTC(_cal.y, _cal.m + 1, 0)).getUTCDate();
        const pad = first.getUTCDay();
        const today = { y: now.getFullYear(), m: now.getMonth(), d: now.getDate() };
        const idx = PetHarmony.todayIndex(hd.score, dom);
        let cells = '';
        for (let i = 0; i < pad; i++) cells += '<div></div>';
        for (let d = 1; d <= days; d++) {
            const luck = PetHarmony.dayLuck(new Date(Date.UTC(_cal.y, _cal.m, d)), dom);
            const marks = [luck.walk ? '🐾' : '', luck.groom ? '✂️' : '', luck.vet ? '🏥' : ''].join('');
            const isToday = _cal.y === today.y && _cal.m === today.m && d === today.d;
            cells += `<div class="rounded-lg py-1 ${isToday ? 'bg-rose-100 border border-rose-300' : ''}">
              <div class="text-[10px] font-bold ${isToday ? 'text-rose-600' : 'text-gray-600'}">${d}</div>
              <div class="text-[9px] leading-none h-3">${marks}</div></div>`;
        }
        el.innerHTML = `
        <div class="bg-white rounded-3xl p-4 border border-amber-100 shadow-sm mb-4">
          <div class="flex items-center justify-between mb-1">
            <button onclick="PetHarmonyView.calMove(-1)" class="text-gray-400 font-black px-2">‹</button>
            <span class="text-xs font-black">🗓️ ${_cal.y}년 ${_cal.m + 1}월 일진 캘린더</span>
            <button onclick="PetHarmonyView.calMove(1)" class="text-gray-400 font-black px-2">›</button>
          </div>
          <div class="text-center text-[10px] text-gray-400 font-bold mb-2">🐾 산책 길일 · ✂️ 미용 길일 · 🏥 병원 길일</div>
          <div class="grid grid-cols-7 gap-0.5 text-center">
            ${['일','월','화','수','목','금','토'].map(d => `<div class="text-[9px] font-black text-gray-400">${d}</div>`).join('')}
            ${cells}
          </div>
          <div class="mt-2 text-center bg-amber-50 rounded-xl py-1.5">
            <span class="text-[10px] font-bold text-gray-500">오늘의 궁합 지수</span>
            <span class="text-[14px] font-black text-rose-600 ml-1.5">${idx}</span>
          </div>
        </div>`;
    }
```
export에 `renderCalendar, calMove` 추가. saju.js fortune 분기에 `if (typeof PetHarmonyView !== 'undefined') PetHarmonyView.renderCalendar('fortune-calendar');`, 템플릿에 컨테이너 삽입.

- [ ] **Step 2: 검증·커밋** — preview: 미측정 CTA → 측정 → 캘린더 마킹·월 이동·오늘 하이라이트. node --check. 커밋.

---

### Task 4: 게임 버프 + 종합 검증

**Files:**
- Modify: `projects/petnna/js/petgame/game-core.js` (feed XP 배율·decay 완화)
- Modify: `projects/petnna/js/petgame/game-stage.js` (HUD 버프 뱃지)
- Modify: `projects/petnna/index.html` (?v 상향)

- [ ] **Step 1: game-core.feed()** — `const levelUp = addXp(...)` 직전에 XP 계산 교체:

```js
        let xpGain = f.xp;
        // 조화도 버프: 오늘 궁합 ≥70 → XP 1.2배 (하모니 모듈·측정 데이터 없으면 무영향)
        try {
            if (typeof PetHarmony !== 'undefined' && pet.harmonyData && pet.harmonyData.elements) {
                const idx = PetHarmony.todayIndex(pet.harmonyData.score || pet.harmonyData.avgScore, pet.harmonyData.elements.pet.dominant);
                if (idx >= 70) xpGain = Math.round(f.xp * 1.2);
            }
        } catch (e) {}
        const levelUp = addXp(pet, xpGain);
```
반환 msg의 `+${f.xp}XP`도 `+${xpGain}XP`로.

- [ ] **Step 2: game-core.decay()** — 조화도 측정 시 감쇠 0.8배:

```js
    function decay(pet, hours) {
        const g = ensureGame(pet);
        const soft = (pet.harmonyData && pet.harmonyData.elements) ? 0.8 : 1;
        g.hunger = Math.max(0, g.hunger - 3 * hours * soft);
        g.happy = Math.max(0, g.happy - 1 * hours * soft);
        save();
    }
```

- [ ] **Step 3: HUD 뱃지** — game-stage hudHTML 코인 span 앞에:

```js
        let buff = '';
        try {
            if (typeof PetHarmony !== 'undefined' && p.harmonyData && p.harmonyData.elements
                && PetHarmony.todayIndex(p.harmonyData.score || p.harmonyData.avgScore, p.harmonyData.elements.pet.dominant) >= 70) {
                buff = `<span class="text-[9px] font-black text-rose-500 bg-rose-50 px-1.5 py-0.5 rounded-full mr-1">💞1.2x</span>`;
            }
        } catch (e) {}
```
코인 span 앞에 `${buff}` 삽입.

- [ ] **Step 4: 단위 검증** — node로 feed 버프(가짜 PetHarmony 주입해 xp 1.2배/미측정 시 1배), decay 0.8배 확인. 기존 테스트 4종(petgame 3 + harmony 1) 전체 재실행.
- [ ] **Step 5: preview 종합** — 측정→리포트→캘린더→게임 HUD 뱃지→먹이 XP 확인. 기존 서브탭(MBTI·아케이드 등) 회귀 무. 모바일 375px.
- [ ] **Step 6: 커밋·푸시**

## Self-Review
- 스펙 커버: 코어 산식(T1)·리포트 UI(T2)·캘린더(T3)·버프(T4)·호환(avgScore 유지, T2 enrich)·테스트(T1·T4) 전부 매핑. 플레이스홀더 없음(팁 20문구·관계 4템플릿 내장). 타입 일관: PetHarmony/PetHarmonyView 시그니처 태스크 간 일치(todayIndex(score, dominant, date?) 통일).
