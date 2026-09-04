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
