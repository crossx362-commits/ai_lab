# AI Model & Cost Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** LLM API를 쓰는 개발자가 월 사용량을 입력하면 주요 모델의 예상 비용을 비교하고 최적 모델을 추천받는 정적 웹 도구를 만들어 Vercel에 배포하고, PayPal 일회성 결제로 발급한 라이선스 키로 프리미엄 기능을 잠금 해제한다.

**Architecture:** 순수 ES 모듈로 작성한 계산 코어(`src/`)를 Node 내장 테스트 러너로 TDD하고, 그 위에 의존성 없는 바닐라 JS UI(`public/`)를 얹는다. 결제와 라이선스만 Vercel 서버리스 함수 3개(`create-order`·`capture-order`·`verify-license`)가 PayPal Orders API를 다룬다. 라이선스 키는 주문ID를 HMAC으로 서명한 문자열이라 **구매 기록을 저장할 DB가 없다**. 빌드 단계 없음 — 브라우저가 ES 모듈을 직접 로드한다.

**Tech Stack:** Vanilla JS (ES modules), Node 26 내장 `node --test`, Vercel (정적 호스팅 + serverless functions), PayPal Orders API v2

## Global Constraints

- 프로젝트 루트: `projects/ai-model-picker/` — 기존 `projects/ai-team/`, `projects/petnna/`와 완전히 독립. 그 두 폴더의 파일은 어떤 태스크에서도 수정하지 않는다.
- 런타임 의존성 0개. `package.json`에 `dependencies`를 추가하지 않는다 (`devDependencies`도 불필요 — 테스트는 Node 내장 러너).
- 빌드 단계 없음. 브라우저가 `<script type="module">`로 `src/`의 파일을 그대로 로드한다. 따라서 `src/`의 모든 모듈은 브라우저와 Node 양쪽에서 동작해야 한다 (Node 전용 API 사용 금지).
- 모든 UI 텍스트는 **영어**. 코드 주석과 커밋 메시지는 한국어 허용.
- 통화는 USD, 소수점 둘째 자리까지 표시.
- 가격 단위는 내부적으로 **USD per 1M tokens**로 통일한다.
- 파일당 하나의 책임. 300줄을 넘으면 분리를 검토한다.
- **테스트는 `npm test`(= `node --test`, 자동 탐색)로 돌린다.** Node 26에서 `node --test test/`는 디렉터리를 모듈 진입점으로 해석해 `MODULE_NOT_FOUND`로 죽는다(2026-08-05 실측). 파일을 직접 지정하는 `node --test test/foo.test.js`는 정상이다.
- 결제·라이선스 관련 코드는 **샌드박스 자격증명으로만** 검증한다. 라이브 키로 결제 흐름을 테스트하면 실제 청구가 발생한다.

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
    "test": "node --test"
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

Run: `cd projects/ai-model-picker && npm test`
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

Run: `cd projects/ai-model-picker && npm test`
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

Run: `cd projects/ai-model-picker && npm test`
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

Run: `cd projects/ai-model-picker && npm test`
Expected: PASS — 20 tests

- [ ] **Step 5: 커밋**

```bash
git add projects/ai-model-picker/src/format.js projects/ai-model-picker/test/format.test.js
git commit -m "feat(picker): 통화 포맷과 Markdown/CSV 내보내기"
```

---

### Task 4: 웹 UI (무료 기능)

**Files:**
- Create: `projects/ai-model-picker/index.html` ← **프로젝트 루트에 둔다** (아래 이유 참고)
- Create: `projects/ai-model-picker/public/styles.css`
- Create: `projects/ai-model-picker/public/app.js`
- Modify: `projects/ai-model-picker/package.json` (`scripts`에 `serve` 추가)

**Interfaces:**
- Consumes: `MODELS`, `PRICING_LAST_VERIFIED` (`src/pricing.js`), `rankModels`, `fitsContext` (`src/recommend.js`), `formatUSD` (`src/format.js`).
- Produces: 브라우저에서 동작하는 페이지. 이후 Task 5가 `app.js`에 프리미엄 훅을 추가한다. 그래서 `app.js`는 다음 두 함수를 모듈 스코프에 정의하고 export 한다:
  - `getUsage()`: 폼에서 읽은 `{ requestsPerMonth, inputTokens, outputTokens, cacheHitRate }`
  - `getRanked()`: 현재 화면에 표시 중인 `rankModels` 결과 배열

**`index.html`을 `public/`이 아니라 프로젝트 루트에 두는 이유:** `app.js`가 `../src/`를 import 하므로 `public/`을 도큐먼트 루트로 서빙할 수 없다. 그렇다고 `index.html`을 `public/`에 두고 Vercel에서 `/` → `/public/index.html`로 rewrite 하면, 브라우저 주소는 `/`인 채라 상대경로 `styles.css`가 `/styles.css`(존재하지 않음)로 풀려 **CSS와 JS가 통째로 404가 난다**. `index.html`이 루트에 있으면 로컬(`http://localhost:4321/`)과 배포본이 같은 경로 규칙을 쓰고 rewrite 자체가 필요 없다.

- [ ] **Step 1: package.json에 로컬 서버 스크립트 추가**

`projects/ai-model-picker/package.json`의 `scripts`를 다음으로 교체한다:

```json
  "scripts": {
    "test": "node --test",
    "serve": "python3 -m http.server 4321"
  },
```

`python3 -m http.server`는 실행한 폴더를 루트로 서빙한다. 즉 `projects/ai-model-picker/`에서 실행하면 `http://localhost:4321/`에서 바로 열린다. 외부 의존성 0개 제약을 지키기 위해 이 방식을 쓴다.

- [ ] **Step 2: HTML 작성**

`projects/ai-model-picker/index.html`:

```html
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>LLM Cost Picker — compare AI model pricing</title>
  <meta name="description" content="Estimate your monthly LLM API bill across Claude, GPT and Gemini, and find the model that fits your budget.">
  <link rel="stylesheet" href="public/styles.css">
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

  <script type="module" src="public/app.js"></script>
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
`http://localhost:4321/projects/ai-model-picker/`로 이동한다.
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
git add projects/ai-model-picker/index.html projects/ai-model-picker/public/ projects/ai-model-picker/package.json .claude/launch.json
git commit -m "feat(picker): 무료 비용 비교 UI"
```

---

### Task 5: PayPal 결제와 라이선스 게이트

> **2026-08-05 개정** — 당초 Gumroad 안에서 PayPal Orders API(일회성 결제)로 변경(오너 지시).
> Gumroad는 결제·키발급·검증을 모두 대행했지만 PayPal은 결제만 하므로, 키 발급과 검증을 직접 만든다.

**Files:**
- Create: `projects/ai-model-picker/src/license.js`
- Create: `projects/ai-model-picker/src/paypal.js`
- Create: `projects/ai-model-picker/api/create-order.js`
- Create: `projects/ai-model-picker/api/capture-order.js`
- Create: `projects/ai-model-picker/api/verify-license.js`
- Create: `projects/ai-model-picker/public/premium.js`
- Test: `projects/ai-model-picker/test/license.test.js`
- Test: `projects/ai-model-picker/test/paypal.test.js`
- Modify: `projects/ai-model-picker/index.html` (프리미엄 섹션 + 스크립트 태그)
- Modify: `projects/ai-model-picker/public/styles.css` (섹션 스타일 추가)

**Interfaces:**
- Consumes: `getUsage()`, `getRanked()` (`public/app.js`, Task 4), `toMarkdown`, `toCSV`, `formatUSD` (`src/format.js`, Task 3), `MODELS` (`src/pricing.js`), `rankModels` (`src/recommend.js`), `monthlyCost` (`src/cost.js`).
- Produces:
  - `signLicense(orderId, secret)` (from `src/license.js`): `'<ORDERID>-<SIG>'` 문자열. `SIG`는 HMAC-SHA256(secret, orderId)의 hex 앞 10자리 대문자.
  - `verifyLicense(key, secret)` (from `src/license.js`): `{ valid: boolean, reason: string, orderId: string }`.
  - `paypalConfig(env)` (from `src/paypal.js`): `{ mode: 'live'|'sandbox', host: string, clientId: string, secret: string }`.
  - `accessToken(cfg)`, `createOrder(cfg, opts)`, `captureOrder(cfg, orderId)` (from `src/paypal.js`).

**Global Constraints의 명시적 예외:** `src/license.js`와 `src/paypal.js`는 **서버 전용**이다(`node:crypto`·시크릿 사용). 브라우저 코드에서 import 하지 않는다. 나머지 `src/` 모듈은 기존 규칙(브라우저·Node 양쪽 동작)을 그대로 지킨다.

**결제 흐름 (리다이렉트 방식):** PayPal JS SDK를 쓰지 않는다 — 외부 스크립트 의존을 0으로 유지하기 위해서다.

1. 사용자가 Buy 클릭 → `POST /api/create-order`
2. 서버가 PayPal 주문(`intent: CAPTURE`) 생성 → 승인 URL 반환
3. 브라우저가 PayPal로 이동해 결제
4. PayPal이 `return_url`(`/api/capture-order?token=<주문ID>`)로 리다이렉트
5. 서버가 캡처 → 성공 시 라이선스 키 생성 → `/?license=<KEY>`로 리다이렉트
6. `premium.js`가 URL의 `license`를 읽어 자동 unlock + 키를 화면에 크게 표시(사용자가 저장하도록)

**환경변수** (Vercel 프로젝트에 설정):

| 이름 | 용도 |
| --- | --- |
| `PAYPAL_ENV` | `live` 또는 `sandbox`. **미설정 시 sandbox** — 오설정이 실제 결제를 받는 사고를 막는 기본값이다. |
| `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET` | 라이브 자격증명 |
| `PAYPAL_SANDBOX_CLIENT_ID` / `PAYPAL_SANDBOX_CLIENT_SECRET` | 샌드박스 자격증명 |
| `PICKER_LICENSE_SECRET` | 키 서명용 랜덤 시크릿. **바꾸면 발급된 모든 키가 무효화된다** — 로테이션 금지. |
| `PICKER_PRICE_USD` | 판매가. 미설정 시 `'9.00'` |
| `PICKER_SITE_URL` | 배포 사이트 origin (예: `https://llm-cost-picker.vercel.app`) — PayPal 리다이렉트 URL 조립용 |

- [ ] **Step 1: 실패하는 테스트 작성**

`projects/ai-model-picker/test/license.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { signLicense, verifyLicense } from '../src/license.js';

const SECRET = 'test-secret-do-not-use-in-production';
const ORDER = '5O190127TN364715T';

test('signLicense returns the order id joined to a signature', () => {
  const key = signLicense(ORDER, SECRET);
  assert.ok(key.startsWith(`${ORDER}-`));
  assert.equal(key.slice(ORDER.length + 1).length, 10);
});

test('signLicense is deterministic', () => {
  assert.equal(signLicense(ORDER, SECRET), signLicense(ORDER, SECRET));
});

test('signLicense produces a different signature for a different secret', () => {
  assert.notEqual(signLicense(ORDER, SECRET), signLicense(ORDER, 'other-secret'));
});

test('verifyLicense accepts a key it just signed', () => {
  const result = verifyLicense(signLicense(ORDER, SECRET), SECRET);
  assert.equal(result.valid, true);
  assert.equal(result.orderId, ORDER);
});

test('verifyLicense is case and whitespace tolerant', () => {
  const key = signLicense(ORDER, SECRET);
  assert.equal(verifyLicense(`  ${key.toLowerCase()}  `, SECRET).valid, true);
});

test('verifyLicense rejects a tampered signature', () => {
  const key = signLicense(ORDER, SECRET);
  const tampered = `${key.slice(0, -1)}${key.at(-1) === 'A' ? 'B' : 'A'}`;
  assert.equal(verifyLicense(tampered, SECRET).valid, false);
});

test('verifyLicense rejects a tampered order id', () => {
  const key = signLicense(ORDER, SECRET);
  assert.equal(verifyLicense(`X${key.slice(1)}`, SECRET).valid, false);
});

test('verifyLicense rejects a key signed with a different secret', () => {
  assert.equal(verifyLicense(signLicense(ORDER, 'other-secret'), SECRET).valid, false);
});

test('verifyLicense rejects malformed input rather than throwing', () => {
  for (const bad of [null, undefined, '', '   ', 'nodash', '-', 'ABC-', 42, {}]) {
    const result = verifyLicense(bad, SECRET);
    assert.equal(result.valid, false, `expected ${JSON.stringify(bad)} to be invalid`);
    assert.ok(result.reason.length > 0);
  }
});

test('verifyLicense rejects a signature of the wrong length', () => {
  assert.equal(verifyLicense(`${ORDER}-ABC`, SECRET).valid, false);
});
```

`projects/ai-model-picker/test/paypal.test.js`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { paypalConfig } from '../src/paypal.js';

test('paypalConfig defaults to sandbox when PAYPAL_ENV is unset', () => {
  const cfg = paypalConfig({
    PAYPAL_SANDBOX_CLIENT_ID: 'sb-id',
    PAYPAL_SANDBOX_CLIENT_SECRET: 'sb-secret',
  });
  assert.equal(cfg.mode, 'sandbox');
  assert.equal(cfg.host, 'https://api-m.sandbox.paypal.com');
  assert.equal(cfg.clientId, 'sb-id');
});

test('paypalConfig only selects live on an exact match', () => {
  for (const value of ['LIVE', 'production', 'true', '']) {
    assert.equal(paypalConfig({ PAYPAL_ENV: value }).mode, 'sandbox', `${value} must not go live`);
  }
  assert.equal(paypalConfig({ PAYPAL_ENV: 'live' }).mode, 'live');
});

test('paypalConfig picks the credentials matching the mode', () => {
  const env = {
    PAYPAL_ENV: 'live',
    PAYPAL_CLIENT_ID: 'live-id',
    PAYPAL_CLIENT_SECRET: 'live-secret',
    PAYPAL_SANDBOX_CLIENT_ID: 'sb-id',
    PAYPAL_SANDBOX_CLIENT_SECRET: 'sb-secret',
  };
  const cfg = paypalConfig(env);
  assert.equal(cfg.clientId, 'live-id');
  assert.equal(cfg.secret, 'live-secret');
  assert.equal(cfg.host, 'https://api-m.paypal.com');
});
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `cd projects/ai-model-picker && node --test test/license.test.js test/paypal.test.js`
Expected: FAIL — `Cannot find module '../src/license.js'`

- [ ] **Step 3: 라이선스 서명·검증 구현**

`projects/ai-model-picker/src/license.js`:

```js
// 서버 전용 모듈 — node:crypto와 서명 시크릿을 쓴다. 브라우저에서 import 하지 않는다.
//
// 라이선스 키 = "<PayPal 주문ID>-<HMAC 앞 10자리>".
// 서버가 시크릿으로 서명을 재계산해 대조하므로 구매 기록을 저장할 필요가 없다.
// 트레이드오프: 환불된 구매의 키는 자동으로 죽지 않는다(설계 문서 §4 참고).
import { createHmac, timingSafeEqual } from 'node:crypto';

const SIG_LENGTH = 10;
const INVALID = 'That license key is not valid.';

function signature(orderId, secret) {
  return createHmac('sha256', secret).update(orderId).digest('hex').slice(0, SIG_LENGTH).toUpperCase();
}

export function signLicense(orderId, secret) {
  const id = String(orderId ?? '').trim().toUpperCase();
  if (!id || !secret) throw new Error('signLicense requires an order id and a secret');
  return `${id}-${signature(id, secret)}`;
}

export function verifyLicense(key, secret) {
  const fail = (reason = INVALID) => ({ valid: false, reason, orderId: '' });
  if (typeof key !== 'string' || !secret) return fail();

  const normalized = key.trim().toUpperCase();
  const split = normalized.lastIndexOf('-');
  if (split <= 0 || split === normalized.length - 1) return fail();

  const orderId = normalized.slice(0, split);
  const provided = normalized.slice(split + 1);
  const expected = signature(orderId, secret);
  // timingSafeEqual은 길이가 다르면 던진다 — 먼저 길이를 본다.
  if (provided.length !== expected.length) return fail();
  if (!timingSafeEqual(Buffer.from(provided), Buffer.from(expected))) return fail();

  return { valid: true, reason: '', orderId };
}
```

- [ ] **Step 4: PayPal 클라이언트 구현**

`projects/ai-model-picker/src/paypal.js`:

```js
// 서버 전용 모듈 — PayPal 시크릿을 쓴다. 브라우저에서 import 하지 않는다.
const HOSTS = {
  live: 'https://api-m.paypal.com',
  sandbox: 'https://api-m.sandbox.paypal.com',
};

/**
 * 환경변수에서 PayPal 설정을 읽는다.
 * PAYPAL_ENV가 정확히 'live'일 때만 라이브다 — 오타·미설정은 전부 샌드박스로 떨어진다.
 * 잘못된 설정이 실제 결제를 받는 것보다 결제가 안 되는 쪽이 안전하다.
 */
export function paypalConfig(env = process.env) {
  const mode = env.PAYPAL_ENV === 'live' ? 'live' : 'sandbox';
  return {
    mode,
    host: HOSTS[mode],
    clientId: mode === 'live' ? env.PAYPAL_CLIENT_ID : env.PAYPAL_SANDBOX_CLIENT_ID,
    secret: mode === 'live' ? env.PAYPAL_CLIENT_SECRET : env.PAYPAL_SANDBOX_CLIENT_SECRET,
  };
}

async function call(cfg, path, { method = 'GET', token, body } = {}) {
  const response = await fetch(`${cfg.host}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: token
        ? `Bearer ${token}`
        : `Basic ${Buffer.from(`${cfg.clientId}:${cfg.secret}`).toString('base64')}`,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const json = await response.json().catch(() => null);
  if (!response.ok) {
    const detail = json?.message || json?.error_description || `HTTP ${response.status}`;
    throw new Error(`PayPal ${path} failed: ${detail}`);
  }
  return json;
}

export async function accessToken(cfg) {
  const response = await fetch(`${cfg.host}/v1/oauth2/token`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
      Authorization: `Basic ${Buffer.from(`${cfg.clientId}:${cfg.secret}`).toString('base64')}`,
    },
    body: 'grant_type=client_credentials',
  });
  const json = await response.json().catch(() => null);
  if (!response.ok || !json?.access_token) {
    throw new Error(`PayPal auth failed: ${json?.error_description || response.status}`);
  }
  return json.access_token;
}

export async function createOrder(cfg, { amount, currency = 'USD', returnUrl, cancelUrl }) {
  const token = await accessToken(cfg);
  const order = await call(cfg, '/v2/checkout/orders', {
    method: 'POST',
    token,
    body: {
      intent: 'CAPTURE',
      purchase_units: [
        {
          amount: { currency_code: currency, value: amount },
          description: 'LLM Cost Picker — Pro license',
        },
      ],
      application_context: {
        brand_name: 'LLM Cost Picker',
        user_action: 'PAY_NOW',
        shipping_preference: 'NO_SHIPPING',
        return_url: returnUrl,
        cancel_url: cancelUrl,
      },
    },
  });
  const approve = (order.links || []).find((l) => l.rel === 'approve' || l.rel === 'payer-action');
  if (!approve) throw new Error('PayPal did not return an approval link');
  return { id: order.id, approveUrl: approve.href };
}

export async function captureOrder(cfg, orderId) {
  const token = await accessToken(cfg);
  const result = await call(cfg, `/v2/checkout/orders/${encodeURIComponent(orderId)}/capture`, {
    method: 'POST',
    token,
    body: {},
  });
  return { id: result.id, status: result.status };
}
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `cd projects/ai-model-picker && npm test`
Expected: PASS — 33 tests (Task 1~3의 20개 + 이번 13개)

- [ ] **Step 6: 서버리스 함수 3개 작성**

`projects/ai-model-picker/api/create-order.js`:

```js
import { paypalConfig, createOrder } from '../src/paypal.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed.' });
    return;
  }
  const cfg = paypalConfig();
  if (!cfg.clientId || !cfg.secret) {
    res.status(500).json({ error: 'Checkout is not configured yet.' });
    return;
  }
  const site = process.env.PICKER_SITE_URL;
  if (!site) {
    res.status(500).json({ error: 'Checkout is not configured yet.' });
    return;
  }
  try {
    const { approveUrl } = await createOrder(cfg, {
      amount: process.env.PICKER_PRICE_USD || '9.00',
      returnUrl: `${site}/api/capture-order`,
      cancelUrl: `${site}/?checkout=cancelled`,
    });
    res.status(200).json({ approveUrl });
  } catch (error) {
    console.error('create-order failed:', error.message);
    res.status(502).json({ error: 'Could not start checkout. Try again.' });
  }
}
```

`projects/ai-model-picker/api/capture-order.js`:

```js
import { paypalConfig, captureOrder } from '../src/paypal.js';
import { signLicense } from '../src/license.js';

// PayPal은 승인 후 return_url로 ?token=<주문ID>를 붙여 리다이렉트한다.
export default async function handler(req, res) {
  const site = process.env.PICKER_SITE_URL || '';
  const orderId = typeof req.query?.token === 'string' ? req.query.token : '';
  const back = (params) => {
    res.setHeader('Location', `${site}/?${new URLSearchParams(params)}`);
    res.status(302).end();
  };

  if (!orderId) return back({ checkout: 'failed' });

  const secret = process.env.PICKER_LICENSE_SECRET;
  const cfg = paypalConfig();
  if (!secret || !cfg.clientId) return back({ checkout: 'failed' });

  try {
    const result = await captureOrder(cfg, orderId);
    if (result.status !== 'COMPLETED') return back({ checkout: 'failed' });
    return back({ license: signLicense(result.id, secret) });
  } catch (error) {
    console.error('capture-order failed:', error.message);
    return back({ checkout: 'failed' });
  }
}
```

`projects/ai-model-picker/api/verify-license.js`:

```js
import { verifyLicense } from '../src/license.js';

export default async function handler(req, res) {
  if (req.method !== 'POST') {
    res.status(405).json({ valid: false, reason: 'Method not allowed.' });
    return;
  }
  const secret = process.env.PICKER_LICENSE_SECRET;
  if (!secret) {
    res.status(500).json({ valid: false, reason: 'Server is not configured yet.' });
    return;
  }
  const key = typeof req.body?.licenseKey === 'string' ? req.body.licenseKey : '';
  if (!key || key.length > 200) {
    res.status(400).json({ valid: false, reason: 'Enter your license key.' });
    return;
  }
  const { valid, reason } = verifyLicense(key, secret);
  res.status(200).json({ valid, reason });
}
```

- [ ] **Step 7: 프리미엄 UI 섹션을 HTML에 추가**

`index.html`의 `</main>` 바로 앞에 다음을 삽입한다:

```html
    <section class="panel" aria-labelledby="premium-heading">
      <h2 id="premium-heading">Pro features</h2>

      <div id="license-gate">
        <p class="muted">Compare growth scenarios side by side, see your prompt-caching savings, and export the full report.</p>
        <p id="checkout-message" role="status"></p>
        <p><button type="button" id="buy-button">Buy a license</button> <span class="muted" id="price-note"></span></p>
        <form id="license-form">
          <label>Already bought? Enter your license key
            <input type="text" id="license-key" placeholder="XXXXXXXXXXXXXXXXX-XXXXXXXXXX" autocomplete="off">
          </label>
          <button type="submit" id="license-submit">Unlock</button>
        </form>
        <p id="license-message" role="status"></p>
      </div>

      <div id="premium-content" hidden>
        <p id="license-receipt" class="receipt" hidden></p>

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

`</body>` 앞의 스크립트 태그 다음 줄에 추가한다:

```html
  <script type="module" src="public/premium.js"></script>
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
button:disabled { opacity: .6; cursor: default; }

#license-form { display: flex; gap: .75rem; align-items: flex-end; flex-wrap: wrap; }
#license-form label { flex: 1 1 22rem; }
#license-message:empty, #checkout-message:empty { display: none; }
#license-message, #checkout-message { margin: .75rem 0 0; font-size: .9rem; }
#license-message.error, #checkout-message.error { color: #c8372d; }
#license-message.ok, #checkout-message.ok { color: #1c7c4a; }
#premium-content h3 { font-size: .95rem; margin: 1.5rem 0 .5rem; }

.receipt {
  background: var(--accent-soft);
  border-radius: 8px;
  padding: .75rem 1rem;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: .85rem;
  word-break: break-all;
}
```

- [ ] **Step 8: 프리미엄 로직 작성**

`projects/ai-model-picker/public/premium.js`:

```js
import { getUsage, getRanked } from './app.js';
import { MODELS } from '../src/pricing.js';
import { rankModels } from '../src/recommend.js';
import { monthlyCost } from '../src/cost.js';
import { toMarkdown, toCSV, formatUSD } from '../src/format.js';

const STORAGE_KEY = 'picker_license_key';

const gate = document.getElementById('license-gate');
const content = document.getElementById('premium-content');
const message = document.getElementById('license-message');
const checkoutMessage = document.getElementById('checkout-message');
const receipt = document.getElementById('license-receipt');

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

  const best = getRanked()[0];
  const savingsEl = document.getElementById('cache-savings');
  if (!best) {
    savingsEl.textContent = '—';
  } else if (best.model.cachedInputPer1M === null) {
    savingsEl.textContent = `${best.model.name} does not offer prompt caching.`;
  } else {
    const noCache = monthlyCost(best.model, { ...usage, cacheHitRate: 0 });
    const full = monthlyCost(best.model, { ...usage, cacheHitRate: 0.9 });
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

document.getElementById('buy-button').addEventListener('click', async (event) => {
  const button = event.currentTarget;
  button.disabled = true;
  checkoutMessage.className = '';
  checkoutMessage.textContent = 'Opening PayPal…';
  try {
    const response = await fetch('/api/create-order', { method: 'POST' });
    const json = await response.json();
    if (json.approveUrl) {
      window.location.href = json.approveUrl;
      return;
    }
    checkoutMessage.className = 'error';
    checkoutMessage.textContent = json.error || 'Could not start checkout.';
  } catch {
    checkoutMessage.className = 'error';
    checkoutMessage.textContent = 'Could not start checkout. Try again.';
  }
  button.disabled = false;
});

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

// 결제 복귀 처리 — PayPal에서 돌아오면 URL에 license 또는 checkout이 붙어 있다.
const params = new URLSearchParams(window.location.search);
const fresh = params.get('license');
const checkoutState = params.get('checkout');

if (fresh) {
  localStorage.setItem(STORAGE_KEY, fresh);
  receipt.hidden = false;
  receipt.textContent = `Your license key: ${fresh} — save it. You'll need it on other devices.`;
  unlock();
  window.history.replaceState({}, '', window.location.pathname);
} else if (checkoutState === 'cancelled') {
  checkoutMessage.textContent = 'Checkout cancelled — nothing was charged.';
  window.history.replaceState({}, '', window.location.pathname);
} else if (checkoutState === 'failed') {
  checkoutMessage.className = 'error';
  checkoutMessage.textContent = 'We could not complete that payment. If you were charged, email us and we will sort it out.';
  window.history.replaceState({}, '', window.location.pathname);
} else {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored) {
    verify(stored)
      .then((result) => {
        if (result.valid) unlock();
        else localStorage.removeItem(STORAGE_KEY);
      })
      .catch(() => {});
  }
}
```

- [ ] **Step 9: 로컬 정적 서버에서 UI 검증**

`preview_start` 후 페이지를 다시 연다. 로컬 정적 서버에는 `/api/*`가 없으므로 404가 나는 것이 **정상**이다. 확인 항목:
1. 콘솔에 모듈 로딩 에러가 없다.
2. Pro features 섹션이 보이고 `#premium-content`는 숨겨져 있다.
3. Buy a license를 누르면 "Could not start checkout." 메시지가 뜬다(로컬엔 API 없음).
4. 아무 키나 넣고 Unlock을 누르면 잠금이 풀리지 않는다.
5. 콘솔에서 `document.getElementById('premium-content').hidden = false; document.dispatchEvent(new Event('picker:rendered'))`를 실행하면 시나리오 표 4행과 캐싱 절감 문구가 렌더된다.
6. 같은 상태에서 Download CSV를 누르면 파일이 받아진다.
7. 검증이 끝나면 `#premium-content`를 다시 `hidden = true`로 되돌린다.

- [ ] **Step 10: 샌드박스로 결제 흐름 end-to-end 검증**

**라이브 자격증명으로 이 검증을 하지 마라 — 실제 청구가 발생한다.**

`projects/ai-model-picker/.env.local`(gitignore 대상)에 저장소 루트 `.env`의 샌드박스 값을 옮겨 적는다:

```
PAYPAL_ENV=sandbox
PAYPAL_SANDBOX_CLIENT_ID=<루트 .env의 값>
PAYPAL_SANDBOX_CLIENT_SECRET=<루트 .env의 값>
PICKER_LICENSE_SECRET=<`openssl rand -hex 32`로 생성>
PICKER_PRICE_USD=9.00
PICKER_SITE_URL=http://localhost:3000
```

`npx vercel dev`로 서버리스 함수까지 띄운 뒤 확인한다:
1. Buy a license → PayPal 샌드박스 로그인 화면으로 이동한다.
2. PayPal 개발자 대시보드의 **Sandbox 개인 테스트 계정**으로 결제한다.
3. 사이트로 돌아오면 프리미엄이 열리고 라이선스 키가 화면에 표시된다.
4. 그 키를 복사해 시크릿 창에서 Unlock에 넣으면 잠금이 풀린다.
5. 키의 마지막 글자를 바꾸면 거부된다.
6. Cancel로 빠져나오면 "Checkout cancelled — nothing was charged."가 뜬다.

- [ ] **Step 11: 커밋**

```bash
git add projects/ai-model-picker/api/ projects/ai-model-picker/src/license.js projects/ai-model-picker/src/paypal.js projects/ai-model-picker/index.html projects/ai-model-picker/public/ projects/ai-model-picker/test/license.test.js projects/ai-model-picker/test/paypal.test.js
git commit -m "feat(picker): PayPal 일회성 결제와 서명 기반 라이선스 게이트"
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
  "cleanUrls": true
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
python3 -m http.server 4321 # http://localhost:4321/
```

로컬 정적 서버에는 `/api/*`가 없다. 결제·라이선스까지 확인하려면 `.env.local`에
샌드박스 값을 채우고 `npx vercel dev`를 쓴다.

## Deploying (오너 작업)

1. Vercel에 이 저장소를 연결하고 **Root Directory**를 `projects/ai-model-picker`로 지정한다.
2. 환경변수를 설정한다:

   | 이름 | 값 |
   | --- | --- |
   | `PAYPAL_ENV` | 검증 중엔 `sandbox`, 판매 개시 시 `live` |
   | `PAYPAL_CLIENT_ID` / `PAYPAL_CLIENT_SECRET` | PayPal 라이브 앱 자격증명 |
   | `PAYPAL_SANDBOX_CLIENT_ID` / `PAYPAL_SANDBOX_CLIENT_SECRET` | PayPal 샌드박스 앱 자격증명 |
   | `PICKER_LICENSE_SECRET` | `openssl rand -hex 32` 결과 |
   | `PICKER_PRICE_USD` | 예: `9.00` |
   | `PICKER_SITE_URL` | 배포 도메인 origin (끝에 `/` 없이) |

3. **`PICKER_LICENSE_SECRET`은 절대 바꾸지 않는다** — 바꾸면 이미 판매한 모든 라이선스 키가 무효화된다.
4. 샌드박스로 결제 왕복을 확인한 뒤에만 `PAYPAL_ENV=live`로 바꾼다.

## 환불 처리 (수동)

라이선스 키에 구매 기록이 없으므로 환불해도 키는 계속 동작한다. $9 상품에서
DB를 두는 비용보다 이 손실이 작다는 판단이다(설계 문서 §4). 문제가 될 만큼
환불이 늘면 `api/verify-license.js`에 PayPal 주문 상태 조회를 덧붙인다 —
`verifyLicense`가 이미 `orderId`를 돌려주므로 코드 변경만으로 가능하다.

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
| `src/license.js` | 라이선스 키 서명·검증 (**서버 전용**) |
| `src/paypal.js` | PayPal Orders API 클라이언트 (**서버 전용**) |
| `api/create-order.js` | 주문 생성 → PayPal 승인 URL 반환 |
| `api/capture-order.js` | 결제 캡처 → 라이선스 키 발급 후 리다이렉트 |
| `api/verify-license.js` | 라이선스 키 검증 |
| `public/` | UI |
| `test/` | 단위 테스트 |
```

- [ ] **Step 4: 전체 테스트 통과 확인**

Run: `cd projects/ai-model-picker && npm test`
Expected: PASS — 33 tests, 0 실패

- [ ] **Step 5: 커밋**

```bash
git add projects/ai-model-picker/vercel.json projects/ai-model-picker/README.md projects/ai-model-picker/public/robots.txt
git commit -m "feat(picker): 배포 설정과 README"
```

---

## 배포 후 (오너 승인 필요)

구현이 끝나면 다음은 **오너가 직접** 해야 한다 (클로드는 대신할 수 없음):

1. Vercel 계정 연결 + 환경변수 설정 + `PAYPAL_ENV=live` 전환
2. 디지털 상품 판매에 따른 세무 확인 — PayPal은 merchant of record가 아니라 결제 대행일 뿐이라,
   부가세(EU VAT 등) 의무가 판매자에게 있다. Gumroad·Paddle 같은 MoR을 쓰면 이 부담이 없어지므로
   해외 매출이 늘면 재검토할 사안이다.
3. 홍보 게시물 실제 발행 (클로드는 초안만 작성)

클로드는 위 각 단계에 필요한 문구·설정값·체크리스트를 제공한다.
