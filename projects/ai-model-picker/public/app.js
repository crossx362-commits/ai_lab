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
