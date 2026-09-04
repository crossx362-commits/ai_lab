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
