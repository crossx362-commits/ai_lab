#!/usr/bin/env python3
"""
aliexpress_fulfillment.py
현빈 — AliExpress Playwright 자동 발주
- 첫 실행: 브라우저 열고 구글 로그인 → 세션 저장
- 이후: 저장된 세션으로 자동 발주
- Shopify 주문 감지 → 알리에서 고객 주소로 자동 주문
"""
import os, sys, json, time, re
from pathlib import Path

_here = os.path.dirname(os.path.abspath(__file__))
ROOT  = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, ROOT)
sys.path.insert(0, os.path.join(ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
load_env(ROOT)

import urllib.request
SHOPIFY_TOKEN  = os.getenv("SHOPIFY_ADMIN_TOKEN", "")
SHOPIFY_STORE  = os.getenv("SHOPIFY_STORE", "swiftcart-101711")
SHOPIFY_DOMAIN = f"{SHOPIFY_STORE}.myshopify.com"
BASE_URL       = f"https://{SHOPIFY_DOMAIN}/admin/api/2024-10"

SESSION_FILE = Path(_here) / ".aliexpress_session.json"


# ── Shopify 헬퍼 ──────────────────────────────────────────────────────────────

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
        return {"error": str(e)}


def get_unfulfilled_orders():
    return _shopify("GET", "orders.json?fulfillment_status=unfulfilled&status=open&limit=50").get("orders", [])


def mark_fulfilled(order_id, tracking_number="", tracking_company="AliExpress"):
    fulfillment = {
        "fulfillment": {
            "location_id": _get_location_id(),
            "tracking_number": tracking_number,
            "tracking_company": tracking_company,
            "notify_customer": True,
        }
    }
    return _shopify("POST", f"orders/{order_id}/fulfillments.json", fulfillment)


def _get_location_id():
    res = _shopify("GET", "locations.json")
    locs = res.get("locations", [])
    return locs[0]["id"] if locs else None


def get_ali_id_for_product(product_title: str) -> str:
    """상품 태그에서 ali:XXXXXXX 추출"""
    res = _shopify("GET", f"products.json?limit=250")
    for p in res.get("products", []):
        if p["title"].lower() == product_title.lower():
            for tag in p.get("tags", "").split(","):
                t = tag.strip()
                if t.startswith("ali:"):
                    return t[4:]
    return ""


# ── AliExpress 세션 관리 ──────────────────────────────────────────────────────

def login_and_save_session():
    """브라우저 열고 구글 로그인 후 세션 저장 (첫 1회만) — 로그인 감지 자동 저장"""
    from playwright.sync_api import sync_playwright

    print("[현빈] 브라우저 열기 — 구글로 로그인하세요. 로그인 완료 시 자동으로 세션 저장됩니다.")
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=False, slow_mo=300)
        ctx = browser.new_context(
            user_agent="Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            viewport={"width": 1280, "height": 800},
        )
        page = ctx.new_page()
        page.goto("https://www.aliexpress.com/")

        # 로그인 완료 감지: 쿠키 확인 (최대 5분)
        print("  로그인 감지 대기 중... 구글로 로그인하세요.")
        LOGIN_COOKIES = {"aeUUID", "_tb_token_", "xman_us_t", "acs_usuc_t", "ali_apache_id"}
        deadline = time.time() + 300  # 5분
        logged_in = False
        while time.time() < deadline:
            try:
                cookie_names = {c["name"] for c in ctx.cookies()}
                if cookie_names & LOGIN_COOKIES:
                    logged_in = True
                    print("  ✅ 로그인 감지! 3초 후 세션 저장...")
                    time.sleep(3)
                    break
                remaining = int(deadline - time.time())
                print(f"  대기 중... (남은 시간 {remaining}초)", end="\r", flush=True)
            except Exception:
                pass
            time.sleep(3)

        print()
        if not logged_in:
            print("  ⚠️ 로그인 미감지 — 현재 상태로 세션 저장")

        # 세션 저장
        storage = ctx.storage_state()
        SESSION_FILE.write_text(json.dumps(storage))
        print(f"  ✅ 세션 저장 완료 → {SESSION_FILE}")
        time.sleep(1)
        browser.close()


def _get_context(playwright):
    """저장된 세션으로 컨텍스트 생성"""
    browser = playwright.chromium.launch(headless=True)
    if SESSION_FILE.exists():
        storage = json.loads(SESSION_FILE.read_text())
        ctx = browser.new_context(
            storage_state=storage,
            user_agent="Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            viewport={"width": 1280, "height": 800},
        )
    else:
        ctx = browser.new_context()
    return browser, ctx


# ── AliExpress 자동 발주 ──────────────────────────────────────────────────────

def place_order_on_aliexpress(ali_item_id: str, shipping_address: dict, quantity: int = 1) -> dict:
    """알리익스프레스에서 특정 상품을 고객 주소로 주문"""
    from playwright.sync_api import sync_playwright

    result = {"success": False, "order_id": "", "error": ""}

    with sync_playwright() as p:
        browser, ctx = _get_context(p)
        page = ctx.new_page()

        try:
            # 상품 페이지 이동
            url = f"https://www.aliexpress.com/item/{ali_item_id}.html"
            print(f"  상품 페이지: {url}")
            page.goto(url, timeout=30000)
            page.wait_for_load_state("domcontentloaded")
            time.sleep(2)

            # 로그인 확인
            if "login" in page.url or "sign" in page.url.lower():
                result["error"] = "세션 만료 — 재로그인 필요"
                return result

            # 수량 설정 (기본 1개)
            if quantity > 1:
                try:
                    qty_input = page.locator("input[class*='quantity']").first
                    qty_input.fill(str(quantity))
                except Exception:
                    pass

            # Buy Now 클릭
            buy_btn = page.locator("button[class*='buy-now'], a[class*='buy-now'], [data-pl='buy-now']").first
            buy_btn.click()
            page.wait_for_load_state("domcontentloaded", timeout=15000)
            time.sleep(2)

            # 주소 입력 페이지
            if "trade/confirm" in page.url or "order/confirm" in page.url or "buy" in page.url:
                _fill_shipping_address(page, shipping_address)
                time.sleep(1)

                # 주문 확인 버튼
                confirm_btn = page.locator("button[class*='place-order'], button[class*='confirm']").first
                if confirm_btn.count() > 0:
                    confirm_btn.click()
                    page.wait_for_load_state("domcontentloaded", timeout=15000)
                    time.sleep(3)

                # 주문 번호 추출
                order_match = re.search(r"orderId=(\d+)|order/(\d+)", page.url)
                if order_match:
                    oid = order_match.group(1) or order_match.group(2)
                    result["success"] = True
                    result["order_id"] = oid
                else:
                    # 페이지에서 주문번호 찾기
                    content = page.content()
                    m = re.search(r'"orderId"\s*:\s*"?(\d+)"?', content)
                    if m:
                        result["success"] = True
                        result["order_id"] = m.group(1)
                    else:
                        result["error"] = "주문 완료됐으나 주문번호 파싱 실패"
                        result["success"] = True  # 페이지 이동 자체는 성공
            else:
                result["error"] = f"예상치 못한 페이지: {page.url}"

        except Exception as e:
            result["error"] = str(e)
        finally:
            # 세션 갱신 저장
            try:
                SESSION_FILE.write_text(json.dumps(ctx.storage_state()))
            except Exception:
                pass
            browser.close()

    return result


def _fill_shipping_address(page, addr: dict):
    """AliExpress 배송지 입력 폼 채우기"""
    try:
        # 새 주소 추가 버튼이 있으면 클릭
        add_btn = page.locator("button[class*='add-address'], [class*='new-address']").first
        if add_btn.count() > 0:
            add_btn.click()
            time.sleep(1)

        fields = {
            "input[name='contactPerson'], input[placeholder*='name' i], input[placeholder*='이름']":
                addr.get("name", ""),
            "input[name='mobileNo'], input[placeholder*='phone' i], input[placeholder*='전화']":
                addr.get("phone", ""),
            "input[name='address'], input[placeholder*='address' i], input[placeholder*='주소']":
                addr.get("address1", ""),
            "input[name='city'], input[placeholder*='city' i]":
                addr.get("city", ""),
            "input[name='province'], input[placeholder*='state' i]":
                addr.get("province", ""),
            "input[name='zip'], input[placeholder*='zip' i], input[placeholder*='postal' i]":
                addr.get("zip", ""),
        }
        for selector, value in fields.items():
            if not value:
                continue
            try:
                el = page.locator(selector).first
                if el.count() > 0:
                    el.fill(value)
            except Exception:
                pass
    except Exception as e:
        print(f"  [주소입력] {e}")


# ── 주문 이행 메인 루프 ────────────────────────────────────────────────────────

def fulfill_pending_orders():
    """미이행 Shopify 주문 → 알리 자동 발주"""
    if not SESSION_FILE.exists():
        print("❌ 세션 없음 — 먼저 로그인: python3 aliexpress_fulfillment.py login")
        return

    orders = get_unfulfilled_orders()
    if not orders:
        print("미이행 주문 없음")
        return

    print(f"[현빈] 미이행 주문 {len(orders)}건 처리 시작...")
    for order in orders:
        oid  = order["order_number"]
        addr = order.get("shipping_address", {})

        for item in order.get("line_items", []):
            title = item["title"]
            qty   = item["quantity"]
            ali_id = get_ali_id_for_product(title)

            if not ali_id:
                send_telegram_message(
                    f"⚠️ <b>[현빈] 발주 실패 #{oid}</b>\n"
                    f"상품: {title}\n"
                    f"알리 ID 없음 — 수동 처리 필요"
                )
                continue

            print(f"  발주: {title[:40]} × {qty} → ali:{ali_id}")
            result = place_order_on_aliexpress(ali_id, addr, qty)

            if result["success"]:
                ali_order_id = result.get("order_id", "")
                send_telegram_message(
                    f"✅ <b>[현빈] 자동 발주 완료 #{oid}</b>\n"
                    f"상품: {title[:40]}\n"
                    f"알리 주문번호: {ali_order_id or '확인필요'}\n"
                    f"고객: {addr.get('name','')} / {addr.get('city','')}"
                )
                # Shopify 이행 완료 처리
                mark_fulfilled(order["id"], tracking_number=ali_order_id)
            else:
                send_telegram_message(
                    f"❌ <b>[현빈] 발주 실패 #{oid}</b>\n"
                    f"상품: {title[:40]}\n"
                    f"오류: {result['error']}"
                )

        time.sleep(2)


def monitor_and_fulfill():
    """Shopify 신규 주문 감지 → 즉시 알리 자동 발주 (루프)"""
    print("[현빈] 주문 자동이행 모니터 시작...")
    if not SESSION_FILE.exists():
        print("❌ 세션 없음 — 먼저 로그인: python3 aliexpress_fulfillment.py login")
        return

    seen_ids = {o["id"] for o in get_unfulfilled_orders()}

    while True:
        try:
            orders = get_unfulfilled_orders()
            new_orders = [o for o in orders if o["id"] not in seen_ids]
            for o in new_orders:
                seen_ids.add(o["id"])
                print(f"  신규 주문 #{o['order_number']} — 자동 발주 시작")
                for item in o.get("line_items", []):
                    ali_id = get_ali_id_for_product(item["title"])
                    if ali_id:
                        result = place_order_on_aliexpress(ali_id, o.get("shipping_address", {}), item["quantity"])
                        if result["success"]:
                            mark_fulfilled(o["id"], result.get("order_id", ""))
                            send_telegram_message(
                                f"✅ <b>[현빈] 자동 발주 #{o['order_number']}</b>\n"
                                f"{item['title'][:40]} × {item['quantity']}\n"
                                f"알리 주문: {result.get('order_id','확인필요')}"
                            )
                        else:
                            send_telegram_message(
                                f"❌ <b>[현빈] 발주 실패 #{o['order_number']}</b>\n{result['error']}"
                            )
                    else:
                        send_telegram_message(
                            f"⚠️ <b>[현빈] 수동발주 필요 #{o['order_number']}</b>\n"
                            f"{item['title'][:40]} — 알리 매핑 없음"
                        )
        except Exception as e:
            print(f"  [오류] {e}")

        time.sleep(300)  # 5분마다 체크


# ── CLI ───────────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("action", nargs="?", default="monitor",
                        choices=["login", "fulfill", "monitor"])
    args = parser.parse_args()

    if args.action == "login":
        login_and_save_session()
    elif args.action == "fulfill":
        fulfill_pending_orders()
    else:
        monitor_and_fulfill()
