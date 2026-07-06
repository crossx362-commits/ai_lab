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
