"""펫나 E2E — 상점(펫 라이프) 화면 진입 검증.

로그인 판정은 클라이언트 측 localStorage(petna_is_logged_in)로만 이뤄지므로
외부 네트워크(수파베이스) 없이 로그인 레이아웃을 재현한다. 로그인 상태에서
상점(shop) 탭으로 전환했을 때 펫 라이프 지도 화면(#tab-shop 내부 템플릿)이
렌더·노출되고, 그 과정에서 치명적 JS 예외가 없는지 화면 구조 위주로 검증한다.
"""

NAME = "상점(펫 라이프) 화면 진입"


def run(page, base_url):
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    # 최초 진입(오리진 확보) → 클라이언트 로그인 플래그 주입 → 재로딩.
    page.goto(base_url)
    page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    page.evaluate(
        """() => {
            localStorage.setItem('petna_is_logged_in', 'true');
            localStorage.setItem('petna_user_email', 'e2e@petna.test');
            localStorage.setItem('petna_active_tab', 'mypet');
        }"""
    )
    page.reload()

    # 로그인 레이아웃 확보: 본문 노출, 로그인 오버레이 숨김.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"

    # 상점 탭 버튼으로 전환 → #tab-shop 이 표시되어야 한다.
    page.locator("header .tab-btn[data-tab='shop']").click()
    page.wait_for_selector("#tab-shop:not(.hidden)", timeout=10000)
    assert page.locator("#tab-shop").is_visible(), "상점 탭으로 전환되지 않음"

    # 상점 템플릿(펫 라이프 지도) 본문이 렌더되어 보여야 한다.
    world = page.wait_for_selector("#tab-shop .island-world-wrap", state="visible", timeout=10000)
    assert world is not None, "상점 템플릿(.island-world-wrap)이 렌더되지 않음"

    # 상점 헤더 타이틀이 노출되어야 한다.
    title = page.locator("#tab-shop .iw-title")
    assert title.count() >= 1, "상점 헤더 타이틀 요소가 없음"
    assert title.first.is_visible(), "상점 헤더 타이틀이 표시되지 않음"

    # 이전 마이펫 탭은 숨겨져야 한다.
    assert not page.locator("#tab-mypet").is_visible(), "상점 전환 후 이전 마이펫 탭이 여전히 노출됨"

    # 진입 전 과정에서 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"상점 화면 진입 중 미처리 JS 예외 발생: {js_errors[:3]}"
