// 다나와(가격비교/스펙 사이트)에서 상품 스펙을 스크래핑한다 (서버 전용).
// 네이버 검색 API는 상세 스펙을 주지 않으므로, 실제 기능 비교 데이터를 여기서 가져온다.
// 주의: 다나와 이용약관상 스크래핑은 회색지대. 캐시로 호출을 최소화한다(specs.js의 Cache-Control).

const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36';

function stripTags(value) {
  return String(value || '')
    .replace(/<[^>]+>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/\s+/g, ' ')
    .trim();
}

// "화면:15.5cm / 램 : 8GB / A17 Pro / 3,274mAh" → { 화면:'15.5cm', 램:'8GB', 특징:'A17 Pro, 3,274mAh' }
function parseSpecTokens(raw) {
  const out = {};
  const extras = [];
  raw.split('/').map((token) => token.trim()).filter(Boolean).forEach((token) => {
    const pair = token.match(/^([^:]{1,14}):\s*(.+)$/);
    if (pair) out[pair[1].trim()] = pair[2].trim();
    else extras.push(token);
  });
  if (extras.length) out['특징'] = extras.join(', ');
  return out;
}

const NORMALIZE = (value) => String(value || '').toLowerCase().replace(/\s+/g, '');

// 제목을 비교용 토큰으로 분해(판매 노이즈·브랜드 제거).
function tokenize(value) {
  return String(value || '')
    .toLowerCase()
    .replace(/\[[^\]]*\]|\([^)]*\)/g, ' ')
    .replace(/미개봉|새상품|새제품|정품|자급제|공기계|미사용|무료배송|당일발송|특가|해외구매|병행수입|리퍼|중고|apple|삼성전자|삼성|애플/g, ' ')
    .split(/[^a-z0-9가-힣]+/)
    .filter((token) => token && (token.length >= 2 || /\d/.test(token)));
}

// 반드시 일치해야 하는 모델 식별자: 짧은 모델번호(15·16e·s24) 전부 + 긴 코드(16z90s)는 가장 긴 것 1개.
function requiredModelTokens(refTokens) {
  const nums = refTokens.filter((t) => /\d/.test(t) && !/^\d+(gb|tb)$/.test(t) && !/[가-힣]/.test(t) && !/^(19|20)\d\d$/.test(t));
  const core = nums.filter((t) => /^[a-z]?\d{1,3}[a-z]?$/.test(t));
  const longCodes = nums.filter((t) => t.length >= 5).sort((a, b) => b.length - a.length);
  return Array.from(new Set([...core, ...(longCodes[0] ? [longCodes[0]] : [])]));
}

// 후보 중 선택 상품(ref)과 가장 잘 맞는 것을 고른다. 확신이 낮으면 null(틀린 스펙 노출 방지).
export function pickBestCandidate(candidates, query, ref) {
  const queryTokens = Array.from(new Set(tokenize(query)));
  const refTokens = Array.from(new Set(tokenize(ref)));
  const required = requiredModelTokens(refTokens.length ? refTokens : queryTokens);
  const scoreTokens = Array.from(new Set([...queryTokens, ...refTokens]));

  let best = null;
  let bestScore = 0;
  for (const candidate of candidates) {
    const name = NORMALIZE(candidate.name);
    if (!required.every((token) => name.includes(token))) continue;
    const hits = scoreTokens.filter((token) => name.includes(token)).length;
    const score = scoreTokens.length ? hits / scoreTokens.length : 0;
    if (score > bestScore) { bestScore = score; best = candidate; }
  }
  return bestScore >= 0.3 ? best : null;
}

// 검색(recall) → 제목 기반 채점(precision)으로 실제 상품의 스펙을 반환한다.
export async function bestSpecFor(query, ref) {
  const candidates = await fetchDanawaSpecs(query, { max: 12 });
  const best = pickBestCandidate(candidates, query, ref || query);
  if (!best) return { matched: false, specs: {} };
  return { matched: true, name: best.name, url: best.url, specs: best.specs, source: '다나와' };
}

// 다나와 검색 후 상위 매칭 상품의 스펙을 반환한다.
export async function fetchDanawaSpecs(query, { max = 1 } = {}) {
  const url = 'https://search.danawa.com/dsearch.php?k1=' + encodeURIComponent(query);
  const res = await fetch(url, {
    headers: { 'User-Agent': UA, 'Accept-Language': 'ko-KR,ko;q=0.9' },
  });
  if (!res.ok) {
    const error = new Error(`다나와 응답 ${res.status}`);
    error.status = res.status;
    throw error;
  }

  const html = await res.text();
  const chunks = html.split('class="prod_name"').slice(1);
  const products = [];

  for (const chunk of chunks) {
    const nameMatch = chunk.match(/<a[^>]*>([\s\S]*?)<\/a>/);
    const pcodeMatch = chunk.match(/pcode=(\d+)/);
    const specMatch = chunk.match(/<div class="spec_list">([\s\S]*?)<\/div>/);
    if (!nameMatch || !specMatch) continue;

    const name = stripTags(nameMatch[1]);
    const specRaw = stripTags(specMatch[1].replace(/<em>\s*\/\s*<\/em>/g, ' / '));
    if (!specRaw) continue;

    products.push({
      name,
      pcode: pcodeMatch ? pcodeMatch[1] : '',
      url: pcodeMatch ? `https://prod.danawa.com/info/?pcode=${pcodeMatch[1]}` : '',
      specRaw,
      specs: parseSpecTokens(specRaw),
    });
    if (products.length >= max) break;
  }

  return products;
}
