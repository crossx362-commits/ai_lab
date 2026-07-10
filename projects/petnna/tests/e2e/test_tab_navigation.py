"""펫나 E2E — 상단 탭 네비게이션 전환 검증.

로그인 판정은 클라이언트 측 localStorage(petna_is_logged_in)로만 이뤄지므로
외부 네트워크(수파베이스) 없이 로그인 레이아웃을 재현한다. 로그인 상태에서
헤더 탭 버튼을 눌렀을 때 대상 탭 콘텐츠가 보이고 이전 탭은 숨겨지는지,
그 과정에서 치명적 JS 예외가 없는지 화면 구조 위주로 검증한다.
"""

NAME = "상단 탭 네비게이션 전환"


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

    # 로그인 레이아웃: 본문·하단 네비 노출, 로그인 오버레이 숨김.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"
    assert page.locator("#mobile-navbar").count() == 1, "하단 네비 요소가 없음"

    # 기본 탭(마이펫) 콘텐츠가 렌더되어 보여야 한다.
    mypet = page.wait_for_selector("#tab-mypet", state="visible", timeout=10000)
    assert mypet is not None, "기본 마이펫 탭이 표시되지 않음"

    # 헤더 탭 버튼으로 '건강' 전환 → 건강 탭 표시, 마이펫 숨김.
    page.locator("header .tab-btn[data-tab='health']").click()
    page.wait_for_selector("#tab-health:not(.hidden)", timeout=10000)
    assert page.locator("#tab-health").is_visible(), "건강 탭으로 전환되지 않음"
    assert not page.locator("#tab-mypet").is_visible(), "탭 전환 후 이전 마이펫 탭이 여전히 노출됨"

    # '설정' 전환 → 설정 탭 표시, 건강 탭 숨김.
    page.locator("header .tab-btn[data-tab='settings']").click()
    page.wait_for_selector("#tab-settings:not(.hidden)", timeout=10000)
    assert page.locator("#tab-settings").is_visible(), "설정 탭으로 전환되지 않음"
    assert not page.locator("#tab-health").is_visible(), "설정 전환 후 건강 탭이 여전히 노출됨"

    # 다시 '마이펫'으로 복귀 → 마이펫 표시, 설정 숨김.
    page.locator("header .tab-btn[data-tab='mypet']").click()
    page.wait_for_selector("#tab-mypet:not(.hidden)", timeout=10000)
    assert page.locator("#tab-mypet").is_visible(), "마이펫 탭으로 복귀되지 않음"
    assert not page.locator("#tab-settings").is_visible(), "복귀 후 설정 탭이 여전히 노출됨"

    # 탭 전환 전 과정에서 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"탭 전환 중 미처리 JS 예외 발생: {js_errors[:3]}"
