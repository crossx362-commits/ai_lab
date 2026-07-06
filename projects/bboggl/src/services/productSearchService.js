// 실제 상품 검색: 동일 출처의 /api/search 프록시를 호출한다.
// 응답 형태: { configured: boolean, items: Array, error?: string }
// - configured=false → 네이버 API 키 미설정. 프론트는 쇼핑몰 검색 링크로 폴백한다.
export async function searchProducts(query) {
  const q = String(query || '').trim();
  if (!q) return { configured: true, items: [] };

  try {
    const response = await fetch(`/api/search?q=${encodeURIComponent(q)}`);
    const data = await response.json().catch(() => ({ configured: true, items: [], error: `HTTP ${response.status}` }));
    return {
      configured: data.configured !== false,
      items: Array.isArray(data.items) ? data.items : [],
      error: data.error || '',
    };
  } catch (error) {
    return { configured: true, items: [], error: error.message };
  }
}

// 다나와에서 실제 스펙을 가져온다. 응답: { matched, name?, url?, specs, source? }
export async function fetchSpecs(query) {
  const q = String(query || '').trim();
  if (!q) return { matched: false, specs: {} };
  try {
    const response = await fetch(`/api/specs?q=${encodeURIComponent(q)}`);
    const data = await response.json().catch(() => ({ matched: false, specs: {} }));
    return { matched: Boolean(data.matched), name: data.name || '', url: data.url || '', specs: data.specs || {}, source: data.source || '' };
  } catch (error) {
    return { matched: false, specs: {}, error: error.message };
  }
}
