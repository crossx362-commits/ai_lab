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
