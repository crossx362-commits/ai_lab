// 네이버 쇼핑 검색 API 호출 (서버 전용 모듈).
// Vercel 서버리스 함수(api/search.js)와 Vite dev 미들웨어(vite.config.js)가 공유한다.
// 시크릿(client id/secret)은 이 모듈이 도는 서버에서만 사용되고 클라이언트 번들에 포함되지 않는다.

const NAVER_ENDPOINT = 'https://openapi.naver.com/v1/search/shop.json';

const ENTITIES = {
  '&lt;': '<',
  '&gt;': '>',
  '&amp;': '&',
  '&quot;': '"',
  '&#39;': "'",
};

export function cleanTitle(value) {
  return String(value || '')
    .replace(/<[^>]*>/g, '')
    .replace(/&lt;|&gt;|&amp;|&quot;|&#39;/g, (m) => ENTITIES[m] || m)
    .trim();
}

// "성지" 낚시 매물(제목 키워드 스터핑)을 걸러낸다. 실제 매물이지만 검색어와 무관한 미끼가 많다.
const SPAM_PATTERNS = ['성지', '싸게사는법', '싸게 사는', '최저가보장', '최저가 보장', '좌표', '시세표', '카톡문의', '카톡 문의'];

export function isSpamTitle(title) {
  const text = String(title || '');
  return SPAM_PATTERNS.some((pattern) => text.includes(pattern));
}

// 네이버 응답을 프론트가 쓰는 최소 형태로 정규화한다.
export function normalizeItem(item) {
  return {
    id: item.productId,
    title: cleanTitle(item.title),
    price: Number(item.lprice) || 0,
    image: item.image,
    mall: item.mallName || '판매처',
    link: item.link,
    brand: item.brand || '',
    maker: item.maker || '',
    category: [item.category1, item.category2, item.category3, item.category4].filter(Boolean).join(' > '),
  };
}

// query로 실제 상품 목록을 조회한다. sort: sim(정확도) | asc(가격오름) | date | dsc
export async function searchNaverShopping(query, { clientId, clientSecret, display = 20, sort = 'sim' } = {}) {
  if (!clientId || !clientSecret) {
    const error = new Error('네이버 API 자격 증명이 없습니다.');
    error.code = 'NO_CREDENTIALS';
    throw error;
  }

  const url = `${NAVER_ENDPOINT}?query=${encodeURIComponent(query)}&display=${display}&sort=${sort}`;
  const response = await fetch(url, {
    headers: {
      'X-Naver-Client-Id': clientId,
      'X-Naver-Client-Secret': clientSecret,
    },
  });

  if (!response.ok) {
    const body = await response.text().catch(() => '');
    const error = new Error(`네이버 API ${response.status}: ${body.slice(0, 200)}`);
    error.status = response.status;
    throw error;
  }

  const data = await response.json();
  const items = (data.items || []).map(normalizeItem);
  const clean = items.filter((item) => !isSpamTitle(item.title));
  // 전부 스팸으로 걸러져 빈 화면이 되는 것만 방지(그 외엔 항상 스팸 제외).
  return clean.length ? clean : items;
}
