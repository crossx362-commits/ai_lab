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
