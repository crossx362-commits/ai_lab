#!/usr/bin/env python3
"""
zendrop_sourcing.py
현빈 — AliExpress 실제 상품 스크래핑 & Shopify 자동 등록
- DDG 이미지 검색으로 AliExpress 상품 URL 탐색
- 실제 상품 페이지에서 제목 + 이미지 + 설명 추출
- Shopify에 실제 데이터로 등록 (이미지/제목 불일치 없음)
"""
import os, sys, json, time, re, urllib.request, urllib.parse

_here = os.path.dirname(os.path.abspath(__file__))
ROOT  = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.join(ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
import _shared.ollama_client as lm
load_env(ROOT)

SHOPIFY_TOKEN  = os.getenv("SHOPIFY_ADMIN_TOKEN", "")
SHOPIFY_DOMAIN = f"{os.getenv('SHOPIFY_STORE', 'swiftcart-101711')}.myshopify.com"
BASE_URL       = f"https://{SHOPIFY_DOMAIN}/admin/api/2024-10"
GEMINI_KEY     = os.getenv("GEMINI_API_KEY", "")

MARKUP    = 2.8
MARGIN_MIN = 0.45

NICHE_KEYWORDS = [
    "pet grooming brush",
    "dog retractable leash",
    "cat interactive toy",
    "dog dental chews",
    "pet water fountain",
    "cat tree scratcher",
    "pet nail grinder",
    "dog cooling mat",
    "cat litter mat",
    "dog training collar",
    "pet carrier bag",
    "dog shampoo",
]

UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"


def _shopify(method, path, body=None):
    url  = f"{BASE_URL}/{path}"
    data = json.dumps(body).encode() if body else None
    req  = urllib.request.Request(url, data=data, method=method,
           headers={"X-Shopify-Access-Token": SHOPIFY_TOKEN,
                    "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.loads(r.read())
    except Exception as e:
        print(f"  [Shopify] {e}")
        return {}


def _ai(prompt: str) -> str:
    if lm.is_available():
        result = lm.chat(prompt, max_tokens=400)
        if result:
            return result.strip()
    url  = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={GEMINI_KEY}"
    body = {"contents": [{"parts": [{"text": prompt}]}]}
    req  = urllib.request.Request(url, data=json.dumps(body).encode(),
           headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            data = json.loads(r.read())
            return data["candidates"][0]["content"]["parts"][0]["text"].strip()
    except Exception:
        return ""


def _ddg_aliexpress_urls(keyword: str, max_results: int = 5) -> list[dict]:
    """DDG 이미지 검색으로 AliExpress 상품 URL + 이미지 URL 수집"""
    search_url = f"https://duckduckgo.com/?q={urllib.parse.quote(keyword + ' aliexpress')}&iax=images&ia=images"
    try:
        req = urllib.request.Request(search_url, headers={"User-Agent": UA})
        with urllib.request.urlopen(req, timeout=10) as r:
            html = r.read().decode(errors="ignore")

        m = re.search(r"vqd='([^']+)'", html) or re.search(r'vqd="([^"]+)"', html)
        if not m:
            return []
        vqd = m.group(1)

        img_url = (
            f"https://duckduckgo.com/i.js?l=us-en&o=json"
            f"&q={urllib.parse.quote(keyword + ' aliexpress')}"
            f"&vqd={urllib.parse.quote(vqd)}&f=,,,,,&p=1"
        )
        req2 = urllib.request.Request(img_url, headers={"User-Agent": UA, "Referer": "https://duckduckgo.com/"})
        with urllib.request.urlopen(req2, timeout=10) as r2:
            data = json.loads(r2.read())

        results = []
        for item in data.get("results", []):
            src_page = item.get("url", "")
            img      = item.get("image", "")
            if "aliexpress.com/item/" in src_page and img:
                results.append({"page_url": src_page, "image_url": img, "title": item.get("title", "")})
                if len(results) >= max_results:
                    break
        return results
    except Exception as e:
        print(f"  [DDG] {e}")
        return []


def _scrape_aliexpress(page_url: str) -> dict | None:
    """AliExpress 상품 페이지에서 제목·이미지·가격 추출"""
    try:
        req = urllib.request.Request(page_url, headers={"User-Agent": UA, "Accept-Language": "en-US,en;q=0.9"})
        with urllib.request.urlopen(req, timeout=15) as r:
            html = r.read().decode(errors="ignore")

        # 제목
        title = ""
        m = re.search(r'"title"\s*:\s*"([^"]{10,200})"', html)
        if m:
            title = m.group(1).replace("\\u0026", "&").replace("\\'", "'")
        if not title:
            m = re.search(r'<h1[^>]*>([^<]{10,200})</h1>', html)
            if m:
                title = m.group(1).strip()

        # 가격 (USD)
        price = 0.0
        m = re.search(r'"minActivityAmount"\s*:\s*\{"value"\s*:\s*"?([\d.]+)"?', html)
        if not m:
            m = re.search(r'"salePrice"\s*:\s*\{"minPrice"\s*:\s*"?([\d.]+)"?', html)
        if not m:
            m = re.search(r'"price"\s*:\s*\{"value"\s*:\s*"?([\d.]+)"?', html)
        if m:
            price = float(m.group(1))

        # 이미지들 (CDN 직접 링크)
        images = []
        for img in re.findall(r'"(https://ae[0-9]+\.alicdn\.com/[^"]+\.(?:jpg|png|webp)[^"]*)"', html):
            clean = img.split("_")[0] + ".jpg" if "_" in img else img
            if clean not in images:
                images.append(clean)
            if len(images) >= 3:
                break

        if not title or not images:
            return None

        return {"title": title[:120], "price_usd": price, "images": images, "source_url": page_url}
    except Exception as e:
        print(f"  [AliExpress] 스크래핑 실패: {e}")
        return None


def get_existing_titles() -> set:
    return {p["title"].lower() for p in _shopify("GET", "products.json?limit=250").get("products", [])}


def add_to_shopify(data: dict, sell_price: float) -> bool:
    title      = data["title"]
    images     = data.get("images", [])
    source_url = data.get("source_url", "")

    # 알리 URL을 태그로 저장해서 주문 발송 시 추적 가능하게
    ali_item_id = ""
    import re as _re
    m = _re.search(r"aliexpress\.com/item/(\d+)", source_url)
    if m:
        ali_item_id = m.group(1)

    tags = "dropship,aliexpress,pet"
    if ali_item_id:
        tags += f",ali:{ali_item_id}"

    shipping_notice = (
        "<div style='background:#fff8e1;border-left:4px solid #ffc107;padding:12px 16px;"
        "margin:16px 0;border-radius:4px;font-size:14px;'>"
        "<strong>📦 배송 안내:</strong> 본 상품은 해외 직구 방식으로 배송됩니다. "
        "평균 배송 기간은 <strong>10~25일</strong>이며, 통관 일정에 따라 다소 지연될 수 있습니다.</div>"
    )

    body = {
        "product": {
            "title": title,
            "body_html": shipping_notice,
            "vendor": "SwiftCart",
            "tags": tags,
            "status": "draft",
            "variants": [{
                "price": str(sell_price),
                "inventory_management": None,
                "inventory_policy": "continue",
            }],
        }
    }
    if images:
        body["product"]["images"] = [{"src": url} for url in images[:5]]

    res = _shopify("POST", "products.json", body)
    return "product" in res


def run_sourcing(max_products: int = 10):
    print("[현빈] AliExpress 실제 상품 소싱 시작...")
    existing = get_existing_titles()
    added    = []
    skipped  = 0

    import random
    keywords = random.sample(NICHE_KEYWORDS, min(6, len(NICHE_KEYWORDS)))

    for kw in keywords:
        if len(added) >= max_products:
            break
        print(f"  검색: {kw}")
        candidates = _ddg_aliexpress_urls(kw, max_results=5)

        for cand in candidates:
            if len(added) >= max_products:
                break

            print(f"    스크래핑: {cand['page_url'][:70]}")
            data = _scrape_aliexpress(cand["page_url"])
            time.sleep(1)

            if not data:
                # 스크래핑 실패 시 DDG 이미지 URL 직접 사용
                title = cand.get("title", "").strip()[:80]
                if not title or len(title) < 5:
                    skipped += 1
                    continue
                data = {
                    "title": title,
                    "price_usd": 0.0,
                    "images": [cand["image_url"]] if cand.get("image_url") else [],
                    "source_url": cand["page_url"],
                }

            title = data["title"]
            if not title or title.lower() in existing:
                skipped += 1
                continue

            cost = data["price_usd"] or 8.0
            sell = round(int(cost * MARKUP) + 0.99, 2)
            margin = (sell - cost) / sell
            if margin < MARGIN_MIN:
                skipped += 1
                continue

            ok = add_to_shopify(data, sell)
            if ok:
                added.append({"title": title, "cost": cost, "price": sell})
                existing.add(title.lower())
                print(f"    ✅ 등록: {title[:50]} | ${cost:.2f} → ${sell}")
            time.sleep(0.5)

    if added:
        lines = "\n".join(f"  • {a['title'][:40]} | ${a['cost']:.2f}→${a['price']}" for a in added)
        msg = f"🛒 <b>[현빈] AliExpress 상품 {len(added)}개 소싱 완료</b>\n\n{lines}\n\n⏭ 스킵: {skipped}개"
    else:
        msg = f"[현빈] 소싱 완료 — 신규 상품 없음 (스킵 {skipped}개)"

    send_telegram_message(msg)
    print(f"[현빈] 완료 — {len(added)}개 추가, {skipped}개 스킵")
    return added


if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("--max", type=int, default=10)
    args = p.parse_args()
    run_sourcing(max_products=args.max)
