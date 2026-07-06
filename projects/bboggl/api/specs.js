// Vercel 서버리스 함수: GET /api/specs?q=<모델 검색어>
// 다나와에서 실제 스펙을 스크래핑해 반환한다. 실패해도 200(matched:false)로 응답 → 프론트는 기본 필드로 폴백.
import { fetchDanawaSpecs } from './_danawa.js';

export default async function handler(req, res) {
  const query = String((req.query && req.query.q) || '').trim();

  // 스펙은 잘 안 바뀌므로 하루 캐시 + 일주일 SWR로 다나와 호출을 최소화한다.
  res.setHeader('Cache-Control', 's-maxage=86400, stale-while-revalidate=604800');

  if (!query) {
    return res.status(200).json({ matched: false, specs: {} });
  }

  try {
    const products = await fetchDanawaSpecs(query, { max: 1 });
    const product = products[0];
    if (!product) {
      return res.status(200).json({ matched: false, specs: {} });
    }
    return res.status(200).json({
      matched: true,
      name: product.name,
      url: product.url,
      specs: product.specs,
      source: '다나와',
    });
  } catch (error) {
    return res.status(200).json({ matched: false, specs: {}, error: error.message });
  }
}
