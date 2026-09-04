// LLM API 가격표 — USD per 1M tokens.
// 최종 확인일: 2026-08-06
// 출처:
//   Anthropic: https://platform.claude.com/docs/en/about-claude/pricing
//   OpenAI:    https://developers.openai.com/api/docs/pricing
//   Google:    https://ai.google.dev/gemini-api/docs/pricing
// 가격은 자주 바뀐다. 분기마다 위 페이지를 다시 확인하고 이 파일을 갱신할 것.
//
// 값을 채울 때 내린 판단 (다음 갱신자가 알아야 할 것):
// - `cachedInputPer1M`은 캐시 **읽기(hit)** 단가다. 캐시 쓰기 단가(정가의 1.25~2배)는
//   이 도구가 모델링하지 않는다 — 재사용이 잦은 워크로드를 가정하므로 읽기 단가가 지배적이다.
// - Claude Sonnet 5는 2026-08-31까지 도입가 $2/$10이지만 여기엔 **표준가 $3/$15**를 넣었다.
//   이 도구는 앞으로의 월 비용을 추정하는 물건이라, 25일 뒤 사라질 할인가를 쓰면 과소추정이 된다.
// - Gemini 3.1 Pro는 프롬프트 길이로 단가가 갈린다(≤200k / >200k). 여기엔 ≤200k 구간을 넣었다.
// - `speed`는 공식 수치가 아니라 티어 기반의 상대 등급이다(3=가장 빠름). 벤치마크가 아니다.
// - GPT-5 계열 컨텍스트 400k는 공식 문서에 GPT-5 기준으로만 명시돼 있다. 5.4·5.6도 같다고 가정했다.

export const PRICING_LAST_VERIFIED = '2026-08-06';

export const MODELS = [
  // ── Anthropic ──
  {
    id: 'claude-haiku-4-5',
    name: 'Claude Haiku 4.5',
    provider: 'Anthropic',
    inputPer1M: 1.0,
    outputPer1M: 5.0,
    cachedInputPer1M: 0.1,
    contextWindow: 200000,
    tier: 'budget',
    speed: 3,
  },
  {
    id: 'claude-sonnet-5',
    name: 'Claude Sonnet 5',
    provider: 'Anthropic',
    inputPer1M: 3.0,
    outputPer1M: 15.0,
    cachedInputPer1M: 0.3,
    contextWindow: 1000000,
    tier: 'balanced',
    speed: 2,
  },
  {
    id: 'claude-opus-5',
    name: 'Claude Opus 5',
    provider: 'Anthropic',
    inputPer1M: 5.0,
    outputPer1M: 25.0,
    cachedInputPer1M: 0.5,
    contextWindow: 1000000,
    tier: 'premium',
    speed: 1,
  },

  // ── OpenAI ──
  {
    id: 'gpt-5-6-luna',
    name: 'GPT-5.6 Luna',
    provider: 'OpenAI',
    inputPer1M: 0.2,
    outputPer1M: 1.2,
    cachedInputPer1M: 0.02,
    contextWindow: 400000,
    tier: 'budget',
    speed: 3,
  },
  {
    id: 'gpt-5-4',
    name: 'GPT-5.4',
    provider: 'OpenAI',
    inputPer1M: 2.5,
    outputPer1M: 15.0,
    cachedInputPer1M: 0.25,
    contextWindow: 400000,
    tier: 'balanced',
    speed: 2,
  },
  {
    id: 'gpt-5-6-sol',
    name: 'GPT-5.6 Sol',
    provider: 'OpenAI',
    inputPer1M: 5.0,
    outputPer1M: 30.0,
    cachedInputPer1M: 0.5,
    contextWindow: 400000,
    tier: 'premium',
    speed: 1,
  },

  // ── Google ──
  {
    id: 'gemini-3-5-flash-lite',
    name: 'Gemini 3.5 Flash-Lite',
    provider: 'Google',
    inputPer1M: 0.3,
    outputPer1M: 2.5,
    cachedInputPer1M: 0.03,
    contextWindow: 1000000,
    tier: 'budget',
    speed: 3,
  },
  {
    id: 'gemini-3-6-flash',
    name: 'Gemini 3.6 Flash',
    provider: 'Google',
    inputPer1M: 1.5,
    outputPer1M: 7.5,
    cachedInputPer1M: 0.15,
    contextWindow: 1000000,
    tier: 'balanced',
    speed: 2,
  },
  {
    id: 'gemini-3-1-pro',
    name: 'Gemini 3.1 Pro',
    provider: 'Google',
    inputPer1M: 2.0,
    outputPer1M: 12.0,
    cachedInputPer1M: 0.2,
    contextWindow: 1000000,
    tier: 'premium',
    speed: 1,
  },
];
