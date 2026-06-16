#!/usr/bin/env python3
"""
shopify_products.py
아린 — Shopify 상품 이미지 & 콘텐츠 자율 관리
- DuckDuckGo 이미지 검색으로 실제 공급사 이미지 수집 (AI 생성 금지)
- Shopify 상품 이미지 업로드
- 상품 설명(body_html) AI 작성 & 업데이트
"""
import os, sys, json, time, urllib.request, urllib.error, urllib.parse

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
        print(f"  [Shopify {method}] {path}: {e}")
        return {}


def _ai_text(prompt: str) -> str:
    """Ollama 우선, 실패 시 Gemini 폴백"""
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
    except Exception as e:
        print(f"  [AI] 텍스트 생성 실패: {e}")
        return ""


def _ddg_image_url(query: str) -> str | None:
    """DuckDuckGo 이미지 검색으로 실제 상품 이미지 URL 획득"""
    # 1단계: vqd 토큰 획득
    search_url = f"https://duckduckgo.com/?q={urllib.parse.quote(query)}&iax=images&ia=images"
    try:
        req = urllib.request.Request(
            search_url,
            headers={"User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36"}
        )
        with urllib.request.urlopen(req, timeout=10) as r:
            html = r.read().decode(errors="ignore")

        vqd = ""
        for line in html.split("\n"):
            if "vqd=" in line:
                start = line.find("vqd=") + 4
                vqd   = line[start:].split("'")[0].split('"')[0].split("&")[0]
                if vqd:
                    break

        if not vqd:
            return None

        # 2단계: 이미지 검색 API
        img_url = (
            f"https://duckduckgo.com/i.js?l=us-en&o=json&q={urllib.parse.quote(query)}"
            f"&vqd={urllib.parse.quote(vqd)}&f=,,,,,&p=1"
        )
        req2 = urllib.request.Request(
            img_url,
            headers={
                "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                "Referer": "https://duckduckgo.com/",
            }
        )
        with urllib.request.urlopen(req2, timeout=10) as r2:
            data = json.loads(r2.read())

        results = data.get("results", [])
        for item in results[:5]:
            url = item.get("image", "")
            # AliExpress / 신뢰할 수 있는 CDN 우선
            if url and any(domain in url for domain in ["aliexpress", "alicdn", "ae01", "ae02", "ae03"]):
                return url
        # AliExpress 없으면 첫 번째 결과
        if results:
            return results[0].get("image", "")
    except Exception as e:
        print(f"  [DDG] 이미지 검색 실패: {e}")
    return None


def fetch_and_upload_image(product_id: int, product_title: str) -> bool:
    """DDG로 실제 상품 이미지 찾아 Shopify에 업로드"""
    query = f"{product_title} product photo"
    print(f"  DDG 검색: {query}")

    image_url = _ddg_image_url(query)
    if not image_url:
        # 키워드 단순화 후 재시도
        words = product_title.split()[:4]
        image_url = _ddg_image_url(" ".join(words) + " product")

    if not image_url:
        print(f"  ❌ 이미지 URL 없음: {product_title}")
        return False

    print(f"  이미지 URL: {image_url[:80]}")
    res = _shopify("POST", f"products/{product_id}/images.json",
                   {"image": {"src": image_url}})
    if "image" in res:
        print(f"  ✅ 이미지 업로드 완료: {product_title}")
        return True
    else:
        print(f"  ❌ 업로드 실패: {res}")
        return False


def write_product_description(product_id: int, product_title: str, existing_desc: str = "") -> bool:
    """AI로 상품 설명 작성 후 업데이트"""
    if existing_desc and len(existing_desc.strip()) > 80:
        # 이미 충분한 설명이 있으면 배송 고지만 추가
        shipping_notice = (
            "<div style='background:#fff8e1;border-left:4px solid #ffc107;padding:12px 16px;"
            "margin:16px 0;border-radius:4px;'>"
            "<strong>📦 배송 안내:</strong> 본 상품은 해외 직구 방식으로 배송됩니다. "
            "평균 배송 기간은 <strong>10~25일</strong>이며, 통관 일정에 따라 다소 지연될 수 있습니다.</div>"
        )
        if "배송 안내" not in existing_desc:
            new_html = existing_desc + shipping_notice
            res = _shopify("PUT", f"products/{product_id}.json",
                           {"product": {"id": product_id, "body_html": new_html}})
            return "product" in res
        return True

    desc = _ai_text(
        f"Write a compelling Shopify product description for: '{product_title}'\n"
        f"Requirements: 3-4 sentences, benefits-focused, SEO-friendly, English.\n"
        f"Format as simple HTML with <p> tags only. No headers. Output HTML only."
    )
    if not desc:
        return False

    shipping_notice = (
        "<div style='background:#fff8e1;border-left:4px solid #ffc107;padding:12px 16px;"
        "margin:16px 0;border-radius:4px;'>"
        "<strong>📦 Shipping Notice:</strong> This item ships internationally. "
        "Estimated delivery: <strong>10–25 business days</strong>. Customs delays may apply.</div>"
    )
    full_html = desc + shipping_notice

    res = _shopify("PUT", f"products/{product_id}.json",
                   {"product": {"id": product_id, "body_html": full_html}})
    if "product" in res:
        print(f"  ✅ 상품 설명 작성 완료: {product_title}")
        return True
    return False


def run_full_product_refresh():
    """전체 상품 이미지 + 설명 일괄 갱신"""
    print("[아린] Shopify 상품 콘텐츠 갱신 시작...")
    products = _shopify("GET", "products.json?limit=50").get("products", [])
    updated = []

    for p in products:
        pid   = p["id"]
        title = p["title"]
        print(f"\n  [{title}]")

        # 이미지 없으면 DDG로 실제 이미지 수집
        if len(p.get("images", [])) < 1:
            fetch_and_upload_image(pid, title)
            time.sleep(2)

        # 설명이 비어있거나 짧으면 작성
        existing = p.get("body_html", "")
        if not existing or len(existing.strip()) < 30:
            write_product_description(pid, title, existing)
            time.sleep(1)

        updated.append(title)

    msg = (
        f"📦 <b>[아린] Shopify 상품 콘텐츠 갱신 완료</b>\n\n"
        f"처리 상품 {len(updated)}개:\n"
        + "\n".join(f"  • {t}" for t in updated)
    )
    send_telegram_message(msg)
    print("\n[아린] 완료 — 텔레그램 보고 전송")


if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument("action", nargs="?", default="refresh",
                   choices=["refresh", "image", "desc"])
    p.add_argument("--id",    type=int, help="상품 ID")
    p.add_argument("--title", type=str, help="상품명")
    args = p.parse_args()

    if args.action == "refresh":
        run_full_product_refresh()
    elif args.action == "image" and args.id and args.title:
        fetch_and_upload_image(args.id, args.title)
    elif args.action == "desc" and args.id and args.title:
        write_product_description(args.id, args.title)
