// Vercel 서버리스 함수: GET /api/search?q=<검색어>
// 네이버 쇼핑 API를 서버에서 호출해 실제 상품을 반환한다.
// 키가 없으면 configured:false로 응답 → 프론트는 실제 쇼핑몰 검색 링크로 자동 폴백한다.
import { searchNaverShopping } from './_naver.js';

export default async function handler(req, res) {
  const query = String((req.query && req.query.q) || '').trim();
  const clientId = process.env.NAVER_CLIENT_ID;
  const clientSecret = process.env.NAVER_CLIENT_SECRET;

  res.setHeader('Cache-Control', 's-maxage=300, stale-while-revalidate=600');

  if (!clientId || !clientSecret) {
    return res.status(200).json({ configured: false, items: [] });
  }
  if (!query) {
    return res.status(200).json({ configured: true, items: [] });
  }

  try {
    const items = await searchNaverShopping(query, { clientId, clientSecret });
    return res.status(200).json({ configured: true, items });
  } catch (error) {
    return res.status(502).json({ configured: true, items: [], error: error.message });
  }
}
