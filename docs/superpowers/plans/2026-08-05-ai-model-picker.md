# AI Model & Cost Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** LLM API를 쓰는 개발자가 월 사용량을 입력하면 주요 모델의 예상 비용을 비교하고 최적 모델을 추천받는 정적 웹 도구를 만들어 Vercel에 배포하고, Gumroad 라이선스 키로 프리미엄 기능을 잠금 해제한다.

**Architecture:** 순수 ES 모듈로 작성한 계산 코어(`src/`)를 Node 내장 테스트 러너로 TDD하고, 그 위에 의존성 없는 바닐라 JS UI(`public/`)를 얹는다. 라이선스 검증만 Vercel 서버리스 함수 하나가 Gumroad API를 프록시한다. 빌드 단계 없음 — 브라우저가 ES 모듈을 직접 로드한다.

**Tech Stack:** Vanilla JS (ES modules), Node 26 내장 `node --test`, Vercel (정적 호스팅 + serverless function), Gumroad License API

## Global Constraints

- 프로젝트 루트: `projects/ai-model-picker/` — 기존 `projects/ai-team/`, `projects/petnna/`와 완전히 독립. 그 두 폴더의 파일은 어떤 태스크에서도 수정하지 않는다.
- 런타임 의존성 0개. `package.json`에 `dependencies`를 추가하지 않는다 (`devDependencies`도 불필요 — 테스트는 Node 내장 러너).
- 빌드 단계 없음. 브라우저가 `<script type="module">`로 `src/`의 파일을 그대로 로드한다. 따라서 `src/`의 모든 모듈은 브라우저와 Node 양쪽에서 동작해야 한다 (Node 전용 API 사용 금지).
- 모든 UI 텍스트는 **영어**. 코드 주석과 커밋 메시지는 한국어 허용.
- 통화는 USD, 소수점 둘째 자리까지 표시.
- 가격 단위는 내부적으로 **USD per 1M tokens**로 통일한다.
- 파일당 하나의 책임. 300줄을 넘으면 분리를 검토한다.

---

### Task 1: 가격 데이터와 비용 계산 코어

**Files:**
- Create: `projects/ai-model-picker/package.json`
- Create: `projects/ai-model-picker/src/pricing.js`
- Create: `projects/ai-model-picker/src/cost.js`
- Test: `projects/ai-model-picker/test/cost.test.js`

**Interfaces:**
- Consumes: (없음 — 첫 태스크)
- Produces:
  - `MODELS` (from `src/pricing.js`): 배열. 각 원소는
    `{ id: string, name: string, provider: string, inputPer1M: number, outputPer1M: number, cachedInputPer1M: number|null, contextWindow: number, tier: 'budget'|'balanced'|'premium', speed: 1|2|3 }`
    (`speed`: 3이 가장 빠름. `cachedInputPer1M`: 프롬프트 캐시 읽기 단가, 미지원이면 `null`.)
  - `monthlyCost(model, usage)` (from `src/cost.js`): `usage`는
    `{ requestsPerMonth: number, inputTokens: number, outputTokens: number, cacheHitRate: number }`
    (`cacheHitRate`: 0~1, 캐시 미적용이면 0). 반환값은
    `{ inputCost: number, outputCost: number, total: number }` — 모두 USD, 반올림 없는 원시 숫자.

- [ ] **Step 1: 프로젝트 폴더와 package.json 생성**

`projects/ai-model-picker/package.json`:

```json
{
  "name": "ai-model-picker",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "description": "Compare LLM API costs across providers and pick the right model.",
  "scripts": {
    "test": "node --test test/"
  }
}
```

- [ ] **Step 2: 실패하는 테스트 작성**

`projects/ai-model-picker/test/cost.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { monthlyCost } from '../src/cost.js';
import { MODELS } from '../src/pricing.js';

const fakeModel = {
  id: 'fake',
  name: 'Fake',
  provider: 'Test',
  inputPer1M: 3,
  outputPer1M: 15,
  cachedInputPer1M: 0.3,
  contextWindow: 200000,
  tier: 'balanced',
  speed: 2,
};

test('monthlyCost multiplies tokens by per-1M rates', () => {
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0,
  };
  const result = monthlyCost(fakeModel, usage);
  assert.equal(result.inputCost, 3);
  assert.equal(result.outputCost, 15);
  assert.equal(result.total, 18);
});

test('monthlyCost applies the cache hit rate to input tokens only', () => {
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0.5,
  };
  const result = monthlyCost(fakeModel, usage);
  // 절반은 정가 3, 절반은 캐시가 0.3 → 1.65
  assert.equal(result.inputCost, 1.65);
  assert.equal(result.outputCost, 15);
});

test('monthlyCost ignores the cache hit rate when the model has no cache pricing', () => {
  const noCache = { ...fakeModel, cachedInputPer1M: null };
  const usage = {
    requestsPerMonth: 1_000_000,
    inputTokens: 1,
    outputTokens: 1,
    cacheHitRate: 0.9,
  };
  assert.equal(monthlyCost(noCache, usage).inputCost, 3);
});

test('monthlyCost returns zero for zero usage', () => {
  const usage = {
    requestsPerMonth: 0,
    inputTokens: 500,
    outputTokens: 500,
    cacheHitRate: 0,
  };
  assert.equal(monthlyCost(fakeModel, usage).total, 0);
});

test('MODELS entries all carry the required fields', () => {
  assert.ok(MODELS.length >= 6, 'expected at least 6 models');
  for (const m of MODELS) {
    assert.equal(typeof m.id, 'string');
    assert.equal(typeof m.name, 'string');
    assert.equal(typeof m.provider, 'string');
    assert.equal(typeof m.inputPer1M, 'number');
    assert.equal(typeof m.outputPer1M, 'number');
    assert.equal(typeof m.contextWindow, 'number');
    assert.ok(['budget', 'balanced', 'premium'].includes(m.tier));
    assert.ok([1, 2, 3].includes(m.speed));
    assert.ok(m.cachedInputPer1M === null || typeof m.cachedInputPer1M === 'number');
  }
});

test('MODELS ids are unique', () => {
  const ids = MODELS.map((m) => m.id);
  assert.equal(new Set(ids).size, ids.length);
});
```

- [ ] **Step 3: 테스트 실패 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: FAIL — `Cannot find module '../src/cost.js'`

- [ ] **Step 4: 가격 데이터 작성**

먼저 WebSearch로 각 provider의 **현재** 공식 가격 페이지를 확인하고, 확인한 값으로 아래 파일을 채운다. 아래 값은 2026-01 시점 기준의 출발점이며, 검색 결과가 다르면 **검색 결과를 따른다**. 확인한 출처 URL을 파일 상단 주석에 남긴다.

`projects/ai-model-picker/src/pricing.js`:

```js
// LLM API 가격표 — USD per 1M tokens.
// 최종 확인일: <YYYY-MM-DD>
// 출처:
//   Anthropic: https://www.anthropic.com/pricing
//   OpenAI:    https://openai.com/api/pricing/
//   Google:    https://ai.google.dev/pricing
// 가격은 자주 바뀐다. 분기마다 위 페이지를 다시 확인하고 이 파일을 갱신할 것.

export const PRICING_LAST_VERIFIED = '<YYYY-MM-DD>';

export const MODELS = [
  // 검색으로 확인한 값으로 채운다. 각 원소는 다음 형태:
  // {
  //   id: 'claude-haiku-4-5',
  //   name: 'Claude Haiku 4.5',
  //   provider: 'Anthropic',
  //   inputPer1M: 1.0,
  //   outputPer1M: 5.0,
  //   cachedInputPer1M: 0.1,
  //   contextWindow: 200000,
  //   tier: 'budget',
  //   speed: 3,
  // },
];
```

최소 6개 모델을 포함하되, budget / balanced / premium 티어가 각각 하나 이상 있어야 한다. Anthropic·OpenAI·Google 세 provider를 모두 포함한다.

- [ ] **Step 5: 비용 계산 구현**

`projects/ai-model-picker/src/cost.js`:

```js
/**
 * 월 예상 비용을 계산한다. 모든 단가는 USD per 1M tokens.
 * cacheHitRate는 입력 토큰 중 캐시로 읽히는 비율(0~1)이며,
 * 모델이 캐시 가격을 제공하지 않으면 무시된다.
 */
export function monthlyCost(model, usage) {
  const { requestsPerMonth, inputTokens, outputTokens, cacheHitRate } = usage;
  const MILLION = 1_000_000;

  const totalInputTokens = requestsPerMonth * inputTokens;
  const totalOutputTokens = requestsPerMonth * outputTokens;

  const hitRate = model.cachedInputPer1M === null ? 0 : cacheHitRate;
  const cachedTokens = totalInputTokens * hitRate;
  const freshTokens = totalInputTokens - cachedTokens;

  const inputCost =
    (freshTokens / MILLION) * model.inputPer1M +
    (cachedTokens / MILLION) * (model.cachedInputPer1M ?? 0);
  const outputCost = (totalOutputTokens / MILLION) * model.outputPer1M;

  return { inputCost, outputCost, total: inputCost + outputCost };
}
```

- [ ] **Step 6: 테스트 통과 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: PASS — 6 tests

- [ ] **Step 7: 커밋**

```bash
git add projects/ai-model-picker/package.json projects/ai-model-picker/src/ projects/ai-model-picker/test/
git commit -m "feat(picker): 가격표와 월 비용 계산 코어"
```

---

### Task 2: 모델 추천 엔진

**Files:**
- Create: `projects/ai-model-picker/src/recommend.js`
- Test: `projects/ai-model-picker/test/recommend.test.js`

**Interfaces:**
- Consumes: `MODELS` (`src/pricing.js`), `monthlyCost(model, usage)` (`src/cost.js`) — Task 1의 시그니처 그대로.
- Produces:
  - `rankModels(models, usage, priority)` (from `src/recommend.js`): `priority`는 `'cost'|'quality'|'speed'|'context'`. 반환값은 배열, 각 원소는
    `{ model: object, cost: { inputCost, outputCost, total }, score: number }`,
    **score 내림차순 정렬**(높을수록 좋음). 동점이면 `cost.total` 오름차순.
  - `fitsContext(model, usage)` (from `src/recommend.js`): `inputTokens + outputTokens`가 모델의 `contextWindow` 이내면 `true`.

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/ai-model-picker/test/recommend.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rankModels, fitsContext } from '../src/recommend.js';

const cheap = {
  id: 'cheap', name: 'Cheap', provider: 'T',
  inputPer1M: 1, outputPer1M: 2, cachedInputPer1M: null,
  contextWindow: 8000, tier: 'budget', speed: 3,
};
const smart = {
  id: 'smart', name: 'Smart', provider: 'T',
  inputPer1M: 15, outputPer1M: 75, cachedInputPer1M: null,
  contextWindow: 200000, tier: 'premium', speed: 1,
};
const middle = {
  id: 'middle', name: 'Middle', provider: 'T',
  inputPer1M: 3, outputPer1M: 15, cachedInputPer1M: null,
  contextWindow: 128000, tier: 'balanced', speed: 2,
};
const all = [smart, cheap, middle];

const usage = {
  requestsPerMonth: 10000, inputTokens: 1000,
  outputTokens: 500, cacheHitRate: 0,
};

test('cost priority ranks the cheapest model first', () => {
  const ranked = rankModels(all, usage, 'cost');
  assert.equal(ranked[0].model.id, 'cheap');
  assert.equal(ranked.at(-1).model.id, 'smart');
});

test('quality priority ranks the premium tier first', () => {
  const ranked = rankModels(all, usage, 'quality');
  assert.equal(ranked[0].model.id, 'smart');
});

test('speed priority ranks the fastest model first', () => {
  const ranked = rankModels(all, usage, 'speed');
  assert.equal(ranked[0].model.id, 'cheap');
});

test('context priority ranks the largest context window first', () => {
  const ranked = rankModels(all, usage, 'context');
  assert.equal(ranked[0].model.id, 'smart');
});

test('rankModels attaches the computed cost to every entry', () => {
  const ranked = rankModels(all, usage, 'cost');
  assert.equal(ranked.length, 3);
  for (const entry of ranked) {
    assert.equal(typeof entry.cost.total, 'number');
    assert.ok(entry.cost.total > 0);
    assert.equal(typeof entry.score, 'number');
  }
});

test('rankModels returns scores in descending order', () => {
  const ranked = rankModels(all, usage, 'quality');
  for (let i = 1; i < ranked.length; i += 1) {
    assert.ok(ranked[i - 1].score >= ranked[i].score);
  }
});

test('rankModels does not mutate the input array', () => {
  const input = [smart, cheap, middle];
  rankModels(input, usage, 'cost');
  assert.equal(input[0].id, 'smart');
});

test('fitsContext compares combined tokens against the context window', () => {
  assert.equal(fitsContext(cheap, { inputTokens: 1000, outputTokens: 500 }), true);
  assert.equal(fitsContext(cheap, { inputTokens: 9000, outputTokens: 500 }), false);
  assert.equal(fitsContext(smart, { inputTokens: 9000, outputTokens: 500 }), true);
});
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `cd projects/ai-model-picker && node --test test/recommend.test.js`
Expected: FAIL — `Cannot find module '../src/recommend.js'`

- [ ] **Step 3: 추천 엔진 구현**

`projects/ai-model-picker/src/recommend.js`:

```js
import { monthlyCost } from './cost.js';

const TIER_SCORE = { budget: 1, balanced: 2, premium: 3 };

export function fitsContext(model, usage) {
  return usage.inputTokens + usage.outputTokens <= model.contextWindow;
}

/**
 * 우선순위에 따라 모델을 점수화해 정렬한다(높은 점수가 먼저).
 * 점수는 우선순위 축의 값을 그 축 최댓값으로 나눈 0~1 정규화 값이라
 * 축이 달라도 크기가 비교 가능하다.
 */
export function rankModels(models, usage, priority) {
  const entries = models.map((model) => ({
    model,
    cost: monthlyCost(model, usage),
  }));

  const maxCost = Math.max(...entries.map((e) => e.cost.total), 1);
  const maxContext = Math.max(...models.map((m) => m.contextWindow), 1);

  const scored = entries.map((entry) => {
    const { model, cost } = entry;
    let score;
    switch (priority) {
      case 'cost':
        // 저렴할수록 높은 점수.
        score = 1 - cost.total / maxCost;
        break;
      case 'quality':
        score = TIER_SCORE[model.tier] / 3;
        break;
      case 'speed':
        score = model.speed / 3;
        break;
      case 'context':
        score = model.contextWindow / maxContext;
        break;
      default:
        throw new Error(`unknown priority: ${priority}`);
    }
    return { ...entry, score };
  });

  return scored.sort((a, b) => b.score - a.score || a.cost.total - b.cost.total);
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: PASS — 14 tests (Task 1의 6개 + 이번 8개)

- [ ] **Step 5: 커밋**

```bash
git add projects/ai-model-picker/src/recommend.js projects/ai-model-picker/test/recommend.test.js
git commit -m "feat(picker): 우선순위 기반 모델 추천 엔진"
```

---

### Task 3: 포맷팅과 내보내기 (Markdown / CSV)

**Files:**
- Create: `projects/ai-model-picker/src/format.js`
- Test: `projects/ai-model-picker/test/format.test.js`

**Interfaces:**
- Consumes: `rankModels` 반환 배열 형태 `[{ model, cost, score }]` (Task 2).
- Produces:
  - `formatUSD(n)` (from `src/format.js`): `1234.5` → `'$1,234.50'`. 0.01 미만 양수는 `'<$0.01'`. 0은 `'$0.00'`.
  - `toMarkdown(ranked, usage)` (from `src/format.js`): 마크다운 표 문자열.
  - `toCSV(ranked)` (from `src/format.js`): CSV 문자열. 헤더 행 포함, 값에 쉼표가 있으면 큰따옴표로 감싼다.

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/ai-model-picker/test/format.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatUSD, toMarkdown, toCSV } from '../src/format.js';

const ranked = [
  {
    model: { id: 'a', name: 'Model A', provider: 'Anthropic', contextWindow: 200000 },
    cost: { inputCost: 10, outputCost: 20, total: 30 },
    score: 0.9,
  },
  {
    model: { id: 'b', name: 'Model B, Turbo', provider: 'OpenAI', contextWindow: 128000 },
    cost: { inputCost: 5, outputCost: 5, total: 10 },
    score: 0.4,
  },
];

const usage = {
  requestsPerMonth: 10000, inputTokens: 1000,
  outputTokens: 500, cacheHitRate: 0,
};

test('formatUSD renders two decimals with thousands separators', () => {
  assert.equal(formatUSD(1234.5), '$1,234.50');
  assert.equal(formatUSD(0), '$0.00');
  assert.equal(formatUSD(7), '$7.00');
});

test('formatUSD marks tiny non-zero amounts', () => {
  assert.equal(formatUSD(0.004), '<$0.01');
});

test('toMarkdown includes a header row and one row per model', () => {
  const md = toMarkdown(ranked, usage);
  assert.ok(md.includes('| Model |'));
  assert.ok(md.includes('Model A'));
  assert.ok(md.includes('Model B, Turbo'));
  assert.ok(md.includes('$30.00'));
});

test('toMarkdown states the usage assumptions', () => {
  const md = toMarkdown(ranked, usage);
  assert.ok(md.includes('10000'));
  assert.ok(md.includes('1000'));
});

test('toCSV emits a header row plus one row per model', () => {
  const lines = toCSV(ranked).trim().split('\n');
  assert.equal(lines.length, 3);
  assert.ok(lines[0].startsWith('Model,Provider'));
});

test('toCSV quotes values containing commas', () => {
  const csv = toCSV(ranked);
  assert.ok(csv.includes('"Model B, Turbo"'));
});
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `cd projects/ai-model-picker && node --test test/format.test.js`
Expected: FAIL — `Cannot find module '../src/format.js'`

- [ ] **Step 3: 구현**

`projects/ai-model-picker/src/format.js`:

```js
export function formatUSD(n) {
  if (n > 0 && n < 0.01) return '<$0.01';
  return `$${n.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export function toMarkdown(ranked, usage) {
  const header =
    `# LLM cost comparison\n\n` +
    `Assumptions: ${usage.requestsPerMonth} requests/month, ` +
    `${usage.inputTokens} input tokens, ${usage.outputTokens} output tokens, ` +
    `${Math.round(usage.cacheHitRate * 100)}% cache hit rate.\n\n`;

  const rows = ranked
    .map(
      ({ model, cost }) =>
        `| ${model.name} | ${model.provider} | ${formatUSD(cost.inputCost)} | ` +
        `${formatUSD(cost.outputCost)} | ${formatUSD(cost.total)} |`,
    )
    .join('\n');

  return (
    header +
    `| Model | Provider | Input | Output | Monthly total |\n` +
    `| --- | --- | --- | --- | --- |\n` +
    rows +
    '\n'
  );
}

function csvCell(value) {
  const s = String(value);
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export function toCSV(ranked) {
  const header = ['Model', 'Provider', 'Context window', 'Input cost', 'Output cost', 'Monthly total'];
  const rows = ranked.map(({ model, cost }) => [
    model.name,
    model.provider,
    model.contextWindow,
    cost.inputCost.toFixed(2),
    cost.outputCost.toFixed(2),
    cost.total.toFixed(2),
  ]);
  return [header, ...rows].map((r) => r.map(csvCell).join(',')).join('\n') + '\n';
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: PASS — 20 tests

- [ ] **Step 5: 커밋**

```bash
git add projects/ai-model-picker/src/format.js projects/ai-model-picker/test/format.test.js
git commit -m "feat(picker): 통화 포맷과 Markdown/CSV 내보내기"
```

---

### Task 4: 웹 UI (무료 기능)

**Files:**
- Create: `projects/ai-model-picker/public/index.html`
- Create: `projects/ai-model-picker/public/styles.css`
- Create: `projects/ai-model-picker/public/app.js`
- Modify: `projects/ai-model-picker/package.json` (`scripts`에 `serve` 추가)

**Interfaces:**
- Consumes: `MODELS`, `PRICING_LAST_VERIFIED` (`src/pricing.js`), `rankModels`, `fitsContext` (`src/recommend.js`), `formatUSD` (`src/format.js`).
- Produces: 브라우저에서 동작하는 페이지. 이후 Task 5가 `app.js`에 프리미엄 훅을 추가한다. 그래서 `app.js`는 다음 두 함수를 모듈 스코프에 정의하고 export 한다:
  - `getUsage()`: 폼에서 읽은 `{ requestsPerMonth, inputTokens, outputTokens, cacheHitRate }`
  - `getRanked()`: 현재 화면에 표시 중인 `rankModels` 결과 배열

**HTML 파일이 `../src/`를 참조하므로 `public/`을 도큐먼트 루트로 서빙하면 안 된다.** 프로젝트 폴더(`projects/ai-model-picker/`)를 루트로 서빙하고 `/public/index.html`로 접근한다. 배포 설정은 Task 6에서 다룬다.

- [ ] **Step 1: package.json에 로컬 서버 스크립트 추가**

`projects/ai-model-picker/package.json`의 `scripts`를 다음으로 교체한다:

```json
  "scripts": {
    "test": "node --test test/",
    "serve": "python3 -m http.server 4321"
  },
```

`python3 -m http.server`는 실행한 폴더를 루트로 서빙한다. 즉 `projects/ai-model-picker/`에서 실행하면 `http://localhost:4321/public/index.html`에서 열린다. 외부 의존성 0개 제약을 지키기 위해 이 방식을 쓴다.

- [ ] **Step 2: HTML 작성**

`projects/ai-model-picker/public/index.html`:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>LLM Cost Picker — compare AI model pricing</title>
  <meta name="description" content="Estimate your monthly LLM API bill across Claude, GPT and Gemini, and find the model that fits your budget.">
  <link rel="stylesheet" href="styles.css">
</head>
<body>
  <header>
    <h1>LLM Cost Picker</h1>
    <p class="tagline">Estimate your monthly API bill across providers, then pick the model that fits.</p>
  </header>

  <main>
    <section class="panel" aria-labelledby="usage-heading">
      <h2 id="usage-heading">Your usage</h2>
      <form id="usage-form">
        <label>Requests per month
          <input type="number" id="requests" value="10000" min="0" step="1000">
        </label>
        <label>Avg input tokens per request
          <input type="number" id="input-tokens" value="1000" min="0" step="100">
        </label>
        <label>Avg output tokens per request
          <input type="number" id="output-tokens" value="500" min="0" step="100">
        </label>
        <label>Cache hit rate
          <input type="range" id="cache-rate" value="0" min="0" max="90" step="10">
          <output id="cache-rate-out">0%</output>
        </label>
        <label>Optimize for
          <select id="priority">
            <option value="cost">Lowest cost</option>
            <option value="quality">Highest quality</option>
            <option value="speed">Fastest</option>
            <option value="context">Largest context</option>
          </select>
        </label>
      </form>
    </section>

    <section class="panel" aria-labelledby="results-heading">
      <h2 id="results-heading">Estimated monthly cost</h2>
      <p id="recommendation" class="recommendation"></p>
      <div class="table-wrap">
        <table id="results">
          <thead>
            <tr>
              <th>Model</th><th>Provider</th><th>Context</th>
              <th>Input</th><th>Output</th><th>Monthly total</th>
            </tr>
          </thead>
          <tbody></tbody>
        </table>
      </div>
      <p class="footnote">Prices last verified <span id="verified-date"></span>. List prices only — volume discounts and batch pricing are not included.</p>
    </section>
  </main>

  <script type="module" src="app.js"></script>
</body>
</html>
```

- [ ] **Step 3: 스타일 작성**

`projects/ai-model-picker/public/styles.css`:

```css
:root {
  --bg: #ffffff;
  --fg: #16181d;
  --muted: #5c6270;
  --line: #e3e6ec;
  --accent: #2f6fed;
  --accent-soft: #eef3fe;
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg: #14161a;
    --fg: #e8eaef;
    --muted: #99a0ae;
    --line: #2a2e37;
    --accent: #6d9bff;
    --accent-soft: #1d2534;
  }
}

* { box-sizing: border-box; }

body {
  margin: 0;
  padding: 2rem 1rem 4rem;
  background: var(--bg);
  color: var(--fg);
  font: 16px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif;
}

header, main { max-width: 60rem; margin: 0 auto; }

h1 { font-size: 1.8rem; margin: 0 0 .25rem; }
.tagline { color: var(--muted); margin: 0 0 2rem; }

.panel {
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 1.25rem;
  margin-bottom: 1.5rem;
}

.panel h2 { font-size: 1.05rem; margin: 0 0 1rem; }

form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
  gap: 1rem;
}

label { display: flex; flex-direction: column; gap: .35rem; font-size: .875rem; color: var(--muted); }

input, select {
  padding: .5rem .6rem;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--bg);
  color: var(--fg);
  font: inherit;
}

.recommendation {
  background: var(--accent-soft);
  border-radius: 8px;
  padding: .75rem 1rem;
  margin: 0 0 1rem;
  font-weight: 600;
}

.table-wrap { overflow-x: auto; }

table { width: 100%; border-collapse: collapse; font-size: .9rem; }
th, td { text-align: left; padding: .55rem .6rem; border-bottom: 1px solid var(--line); white-space: nowrap; }
th { color: var(--muted); font-weight: 600; }
tr.best td { background: var(--accent-soft); font-weight: 600; }
td.over-context { color: var(--muted); }

.footnote { color: var(--muted); font-size: .8rem; margin: 1rem 0 0; }
```

- [ ] **Step 4: 앱 로직 작성**

`projects/ai-model-picker/public/app.js`:

```js
import { MODELS, PRICING_LAST_VERIFIED } from '../src/pricing.js';
import { rankModels, fitsContext } from '../src/recommend.js';
import { formatUSD } from '../src/format.js';

const form = document.getElementById('usage-form');
const tbody = document.querySelector('#results tbody');
const recommendation = document.getElementById('recommendation');
const cacheOut = document.getElementById('cache-rate-out');

let currentRanked = [];

export function getUsage() {
  return {
    requestsPerMonth: Number(document.getElementById('requests').value) || 0,
    inputTokens: Number(document.getElementById('input-tokens').value) || 0,
    outputTokens: Number(document.getElementById('output-tokens').value) || 0,
    cacheHitRate: (Number(document.getElementById('cache-rate').value) || 0) / 100,
  };
}

export function getRanked() {
  return currentRanked;
}

function render() {
  const usage = getUsage();
  const priority = document.getElementById('priority').value;
  cacheOut.textContent = `${Math.round(usage.cacheHitRate * 100)}%`;

  currentRanked = rankModels(MODELS, usage, priority);
  tbody.innerHTML = '';

  currentRanked.forEach((entry, index) => {
    const { model, cost } = entry;
    const row = document.createElement('tr');
    if (index === 0) row.className = 'best';

    const cells = [
      model.name,
      model.provider,
      `${(model.contextWindow / 1000).toFixed(0)}K`,
      formatUSD(cost.inputCost),
      formatUSD(cost.outputCost),
      formatUSD(cost.total),
    ];
    for (const value of cells) {
      const td = document.createElement('td');
      td.textContent = value;
      row.appendChild(td);
    }
    if (!fitsContext(model, usage)) {
      row.cells[2].classList.add('over-context');
      row.cells[2].textContent += ' ⚠';
      row.cells[2].title = 'Your request exceeds this context window.';
    }
    tbody.appendChild(row);
  });

  const best = currentRanked[0];
  recommendation.textContent = best
    ? `Best match: ${best.model.name} — ${formatUSD(best.cost.total)} per month.`
    : 'No models available.';

  document.dispatchEvent(new CustomEvent('picker:rendered'));
}

document.getElementById('verified-date').textContent = PRICING_LAST_VERIFIED;
form.addEventListener('input', render);
render();
```

- [ ] **Step 5: 브라우저에서 검증**

저장소 루트의 `.claude/launch.json`이 없으면 만들고, 있으면 `configurations` 배열에 다음 항목을 추가한다:

```json
{
  "name": "ai-model-picker",
  "runtimeExecutable": "python3",
  "runtimeArgs": ["-m", "http.server", "4321"],
  "port": 4321
}
```

`preview_start`로 서버를 띄운다. 서버는 **저장소 루트**를 도큐먼트 루트로 서빙하므로
`http://localhost:4321/projects/ai-model-picker/public/index.html`로 이동한다.
(`preview_start`가 연 URL이 다르면 실제 열린 경로에 맞춰 조정한다.)

확인 항목 — 모두 통과해야 한다:
1. `read_console_messages`에 에러가 없다.
2. 표에 `MODELS` 개수만큼 행이 있다.
3. 첫 행이 `best` 클래스로 강조되어 있다.
4. Requests per month를 바꾸면 금액이 갱신된다.
5. "Optimize for"를 Highest quality로 바꾸면 첫 행이 바뀐다.
6. Input tokens를 500000으로 올리면 컨텍스트가 작은 모델의 Context 칸에 ⚠가 붙는다.
7. `resize_window`로 375px에서 가로 스크롤이 body에 생기지 않는다(표는 자체 스크롤).

- [ ] **Step 6: 커밋**

```bash
git add projects/ai-model-picker/public/ projects/ai-model-picker/package.json .claude/launch.json
git commit -m "feat(picker): 무료 비용 비교 UI"
```

---

### Task 5: 라이선스 게이트와 프리미엄 기능

**Files:**
- Create: `projects/ai-model-picker/api/verify-license.js`
- Create: `projects/ai-model-picker/src/license.js`
- Create: `projects/ai-model-picker/public/premium.js`
- Test: `projects/ai-model-picker/test/license.test.js`
- Modify: `projects/ai-model-picker/public/index.html` (프리미엄 섹션 + 스크립트 태그 추가)

**Interfaces:**
- Consumes: `getUsage()`, `getRanked()` (`public/app.js`, Task 4), `toMarkdown`, `toCSV` (`src/format.js`, Task 3), `MODELS` (`src/pricing.js`), `rankModels` (`src/recommend.js`).
- Produces:
  - `parseVerifyResponse(json)` (from `src/license.js`): Gumroad 응답 객체를 받아
    `{ valid: boolean, reason: string }` 반환. `success === true` 이고
    `purchase.refunded !== true` 이고 `purchase.chargebacked !== true` 일 때만 valid.
  - `SCENARIOS` (from `public/premium.js` 내부 상수, export 불필요)

**보안:** Gumroad product id는 공개 값이라 함수에 하드코딩해도 되지만, 환경변수 `GUMROAD_PRODUCT_ID`로 읽어 배포마다 바꿀 수 있게 한다. 시크릿은 사용하지 않는다 (이 엔드포인트는 시크릿이 필요 없다).

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/ai-model-picker/test/license.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseVerifyResponse } from '../src/license.js';

test('a successful non-refunded purchase is valid', () => {
  const result = parseVerifyResponse({
    success: true,
    purchase: { refunded: false, chargebacked: false },
  });
  assert.equal(result.valid, true);
});

test('an unsuccessful response is invalid', () => {
  const result = parseVerifyResponse({ success: false, message: 'That license does not exist' });
  assert.equal(result.valid, false);
  assert.ok(result.reason.length > 0);
});

test('a refunded purchase is invalid', () => {
  const result = parseVerifyResponse({
    success: true,
    purchase: { refunded: true, chargebacked: false },
  });
  assert.equal(result.valid, false);
  assert.match(result.reason, /refund/i);
});

test('a chargebacked purchase is invalid', () => {
  const result = parseVerifyResponse({
    success: true,
    purchase: { refunded: false, chargebacked: true },
  });
  assert.equal(result.valid, false);
});

test('a malformed response is invalid rather than throwing', () => {
  assert.equal(parseVerifyResponse(null).valid, false);
  assert.equal(parseVerifyResponse({}).valid, false);
  assert.equal(parseVerifyResponse({ success: true }).valid, false);
});
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `cd projects/ai-model-picker && node --test test/license.test.js`
Expected: FAIL — `Cannot find module '../src/license.js'`

- [ ] **Step 3: 파서 구현**

`projects/ai-model-picker/src/license.js`:

```js
/**
 * Gumroad의 라이선스 검증 응답을 판정 결과로 정규화한다.
 * 응답 형태를 신뢰하지 않는다 — 외부 API의 임의 JSON이 들어올 수 있다.
 */
export function parseVerifyResponse(json) {
  if (!json || typeof json !== 'object') {
    return { valid: false, reason: 'Could not read the verification response.' };
  }
  if (json.success !== true) {
    return { valid: false, reason: json.message || 'That license key is not valid.' };
  }
  const purchase = json.purchase;
  if (!purchase || typeof purchase !== 'object') {
    return { valid: false, reason: 'That license key is not valid.' };
  }
  if (purchase.refunded === true) {
    return { valid: false, reason: 'This purchase was refunded.' };
  }
  if (purchase.chargebacked === true) {
    return { valid: false, reason: 'This purchase was charged back.' };
  }
  return { valid: true, reason: '' };
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: PASS — 25 tests

- [ ] **Step 5: 서버리스 함수 작성**

`projects/ai-model-picker/api/verify-license.js`:

```js
import { parseVerifyResponse } from '../src/license.js';

const GUMROAD_VERIFY_URL = 'https://api.gumroad.com/v2/licenses/verify';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ valid: false, reason: 'Method not allowed.' });
    return;
  }

  const productId = process.env.GUMROAD_PRODUCT_ID;
  if (!productId) {
    res.status(500).json({ valid: false, reason: 'Server is not configured yet.' });
    return;
  }

  const licenseKey = typeof req.body?.licenseKey === 'string' ? req.body.licenseKey.trim() : '';
  if (!licenseKey || licenseKey.length > 200) {
    res.status(400).json({ valid: false, reason: 'Enter your license key.' });
    return;
  }

  try {
    const upstream = await fetch(GUMROAD_VERIFY_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        product_id: productId,
        license_key: licenseKey,
        // 검증 때마다 사용 횟수가 증가하지 않도록 한다.
        increment_uses_count: 'false',
      }),
    });
    const json = await upstream.json().catch(() => null);
    res.status(200).json(parseVerifyResponse(json));
  } catch {
    res.status(502).json({ valid: false, reason: 'Could not reach the license server. Try again.' });
  }
}
```

- [ ] **Step 6: 프리미엄 UI 섹션을 HTML에 추가**

`public/index.html`의 `</main>` 바로 앞에 다음을 삽입한다:

```html
    <section class="panel" aria-labelledby="premium-heading">
      <h2 id="premium-heading">Pro features</h2>

      <div id="license-gate">
        <p class="muted">Compare growth scenarios side by side, see your prompt-caching savings, and export the full report.</p>
        <form id="license-form">
          <label>License key
            <input type="text" id="license-key" placeholder="XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX" autocomplete="off">
          </label>
          <button type="submit" id="license-submit">Unlock</button>
        </form>
        <p id="license-message" role="status"></p>
        <p class="footnote">No license yet? <a id="buy-link" href="#" rel="noopener">Get one on Gumroad</a>.</p>
      </div>

      <div id="premium-content" hidden>
        <h3>Scenario comparison</h3>
        <div class="table-wrap">
          <table id="scenarios">
            <thead>
              <tr><th>Scenario</th><th>Requests/mo</th><th>Best model</th><th>Monthly total</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>

        <h3>Prompt caching savings</h3>
        <p id="cache-savings"></p>

        <h3>Export</h3>
        <button type="button" id="export-md">Download Markdown</button>
        <button type="button" id="export-csv">Download CSV</button>
      </div>
    </section>
```

그리고 `</body>` 앞의 스크립트 태그 다음 줄에 추가한다:

```html
  <script type="module" src="premium.js"></script>
```

`styles.css` 끝에 추가한다:

```css
.muted { color: var(--muted); }

button {
  padding: .5rem .9rem;
  border: 1px solid var(--accent);
  border-radius: 8px;
  background: var(--accent);
  color: #fff;
  font: inherit;
  cursor: pointer;
}

button:hover { filter: brightness(1.06); }

#license-form { display: flex; gap: .75rem; align-items: flex-end; flex-wrap: wrap; }
#license-form label { flex: 1 1 22rem; }
#license-message:empty { display: none; }
#license-message { margin: .75rem 0 0; font-size: .9rem; }
#license-message.error { color: #c8372d; }
#license-message.ok { color: #1c7c4a; }
#premium-content h3 { font-size: .95rem; margin: 1.5rem 0 .5rem; }
```

- [ ] **Step 7: 프리미엄 로직 작성**

`projects/ai-model-picker/public/premium.js`:

```js
import { getUsage, getRanked } from './app.js';
import { MODELS } from '../src/pricing.js';
import { rankModels } from '../src/recommend.js';
import { monthlyCost } from '../src/cost.js';
import { toMarkdown, toCSV, formatUSD } from '../src/format.js';

const STORAGE_KEY = 'picker_license_key';
const BUY_URL = 'https://gumroad.com/'; // 배포 전 실제 상품 URL로 교체한다.

const gate = document.getElementById('license-gate');
const content = document.getElementById('premium-content');
const message = document.getElementById('license-message');

document.getElementById('buy-link').href = BUY_URL;

const SCENARIOS = [
  { name: 'MVP', multiplier: 0.1 },
  { name: 'Current', multiplier: 1 },
  { name: 'Growth (10x)', multiplier: 10 },
  { name: 'Scale (100x)', multiplier: 100 },
];

async function verify(licenseKey) {
  const response = await fetch('/api/verify-license', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ licenseKey }),
  });
  return response.json();
}

function unlock() {
  gate.hidden = true;
  content.hidden = false;
  renderPremium();
}

function renderPremium() {
  if (content.hidden) return;
  const usage = getUsage();
  const priority = document.getElementById('priority').value;

  const tbody = document.querySelector('#scenarios tbody');
  tbody.innerHTML = '';
  for (const scenario of SCENARIOS) {
    const scaled = {
      ...usage,
      requestsPerMonth: Math.round(usage.requestsPerMonth * scenario.multiplier),
    };
    const best = rankModels(MODELS, scaled, priority)[0];
    const row = document.createElement('tr');
    for (const value of [
      scenario.name,
      scaled.requestsPerMonth.toLocaleString('en-US'),
      best ? best.model.name : '—',
      best ? formatUSD(best.cost.total) : '—',
    ]) {
      const td = document.createElement('td');
      td.textContent = value;
      row.appendChild(td);
    }
    tbody.appendChild(row);
  }

  const ranked = getRanked();
  const best = ranked[0];
  const savingsEl = document.getElementById('cache-savings');
  if (!best) {
    savingsEl.textContent = '—';
  } else if (best.model.cachedInputPer1M === null) {
    savingsEl.textContent = `${best.model.name} does not offer prompt caching.`;
  } else {
    const noCache = monthlyCost(best.model, { ...getUsage(), cacheHitRate: 0 });
    const full = monthlyCost(best.model, { ...getUsage(), cacheHitRate: 0.9 });
    savingsEl.textContent =
      `${best.model.name} at a 90% cache hit rate: ${formatUSD(full.total)} per month ` +
      `instead of ${formatUSD(noCache.total)} — you save ${formatUSD(noCache.total - full.total)}.`;
  }
}

function download(filename, text, mime) {
  const blob = new Blob([text], { type: mime });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

document.getElementById('license-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const key = document.getElementById('license-key').value.trim();
  if (!key) return;

  const button = document.getElementById('license-submit');
  button.disabled = true;
  message.className = '';
  message.textContent = 'Checking…';

  try {
    const result = await verify(key);
    if (result.valid) {
      localStorage.setItem(STORAGE_KEY, key);
      message.className = 'ok';
      message.textContent = 'Unlocked. Thanks for buying!';
      unlock();
    } else {
      message.className = 'error';
      message.textContent = result.reason || 'That license key is not valid.';
    }
  } catch {
    message.className = 'error';
    message.textContent = 'Could not reach the license server. Try again.';
  } finally {
    button.disabled = false;
  }
});

document.getElementById('export-md').addEventListener('click', () => {
  download('llm-cost-comparison.md', toMarkdown(getRanked(), getUsage()), 'text/markdown');
});

document.getElementById('export-csv').addEventListener('click', () => {
  download('llm-cost-comparison.csv', toCSV(getRanked()), 'text/csv');
});

document.addEventListener('picker:rendered', renderPremium);

// 이전에 검증된 키가 있으면 조용히 재검증한다.
const stored = localStorage.getItem(STORAGE_KEY);
if (stored) {
  verify(stored)
    .then((result) => {
      if (result.valid) unlock();
      else localStorage.removeItem(STORAGE_KEY);
    })
    .catch(() => {});
}
```

- [ ] **Step 8: 브라우저에서 검증**

`preview_start` 후 페이지를 다시 연다. `/api/verify-license`는 로컬 정적 서버에 없으므로 404가 나는 것이 **정상**이다. 확인 항목:
1. 콘솔에 모듈 로딩 에러가 없다.
2. Pro features 섹션이 보이고 `#premium-content`는 숨겨져 있다.
3. 아무 키나 넣고 Unlock을 누르면 "Could not reach the license server." 메시지가 뜨고 잠금이 풀리지 않는다.
4. 콘솔에서 `document.getElementById('premium-content').hidden = false; document.dispatchEvent(new Event('picker:rendered'))`를 실행하면 시나리오 표 4행과 캐싱 절감 문구가 렌더된다.
5. 같은 상태에서 Download CSV를 누르면 파일이 받아진다.
6. 검증이 끝나면 `#premium-content`를 다시 `hidden = true`로 되돌린다.

- [ ] **Step 9: 커밋**

```bash
git add projects/ai-model-picker/api/ projects/ai-model-picker/src/license.js projects/ai-model-picker/public/ projects/ai-model-picker/test/license.test.js
git commit -m "feat(picker): Gumroad 라이선스 게이트와 프리미엄 기능"
```

---

### Task 6: 배포 설정과 문서

**Files:**
- Create: `projects/ai-model-picker/vercel.json`
- Create: `projects/ai-model-picker/README.md`
- Create: `projects/ai-model-picker/public/robots.txt`

**Interfaces:**
- Consumes: 앞선 모든 태스크의 산출물.
- Produces: 배포 가능한 상태 + 오너가 따라 할 수 있는 셋업 문서.

**주의 (CLAUDE.md 사고 기록):** `vercel.json`에 `_comment_*` 같은 비표준 키를 넣지 않는다 — 스키마 검증 실패로 전체 배포가 죽는다. 설명은 README에 쓴다.

- [ ] **Step 1: vercel.json 작성**

`projects/ai-model-picker/vercel.json`:

```json
{
  "$schema": "https://openapi.vercel.sh/vercel.json",
  "cleanUrls": true,
  "rewrites": [
    { "source": "/", "destination": "/public/index.html" }
  ]
}
```

- [ ] **Step 2: robots.txt 작성**

`projects/ai-model-picker/public/robots.txt`:

```
User-agent: *
Allow: /
```

- [ ] **Step 3: README 작성**

`projects/ai-model-picker/README.md`:

```markdown
# LLM Cost Picker

Estimate your monthly LLM API bill across Anthropic, OpenAI and Google, and
pick the model that fits your budget, latency and context needs.

## Local development

```bash
cd projects/ai-model-picker
npm test                    # 순수 로직 단위 테스트 (Node 내장 러너, 의존성 0개)
python3 -m http.server 4321 # http://localhost:4321/public/index.html
```

로컬 정적 서버에는 `/api/verify-license`가 없다. 라이선스 검증까지 확인하려면
`vercel dev`를 쓴다.

## Deploying (오너 작업)

1. Vercel에 이 저장소를 연결하고 **Root Directory**를 `projects/ai-model-picker`로 지정한다.
2. Gumroad에서 상품을 만들고 라이선스 키 발급을 켠다. 상품 페이지 URL의 상품 ID를 복사한다.
3. Vercel 프로젝트 환경변수에 `GUMROAD_PRODUCT_ID`를 추가한다.
4. `public/premium.js`의 `BUY_URL`을 실제 Gumroad 상품 URL로 바꾼다.

## 가격 데이터 갱신

`src/pricing.js`의 `MODELS`와 `PRICING_LAST_VERIFIED`를 분기마다 갱신한다.
출처 URL은 파일 상단 주석에 있다.

## 구조

| 경로 | 역할 |
| --- | --- |
| `src/pricing.js` | 모델 가격표 (수동 갱신) |
| `src/cost.js` | 월 비용 계산 |
| `src/recommend.js` | 우선순위 기반 순위 산정 |
| `src/format.js` | 통화 포맷, Markdown/CSV 내보내기 |
| `src/license.js` | Gumroad 응답 판정 (브라우저·서버 공용) |
| `api/verify-license.js` | 라이선스 검증 서버리스 함수 |
| `public/` | UI |
| `test/` | 단위 테스트 |
```

- [ ] **Step 4: 전체 테스트 통과 확인**

Run: `cd projects/ai-model-picker && node --test test/`
Expected: PASS — 25 tests, 0 실패

- [ ] **Step 5: 커밋**

```bash
git add projects/ai-model-picker/vercel.json projects/ai-model-picker/README.md projects/ai-model-picker/public/robots.txt
git commit -m "feat(picker): 배포 설정과 README"
```

---

## 배포 후 (오너 승인 필요)

구현이 끝나면 다음은 **오너가 직접** 해야 한다 (클로드는 대신할 수 없음):

1. Gumroad 계정 생성 + 상품 등록 + 가격 설정
2. Vercel 계정 연결 + 환경변수 설정
3. 홍보 게시물 실제 발행 (클로드는 초안만 작성)

클로드는 위 각 단계에 필요한 문구·설정값·체크리스트를 제공한다.
