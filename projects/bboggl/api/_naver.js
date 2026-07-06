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
  return (data.items || []).map(normalizeItem);
}
