"""펫나 E2E — 모바일 '더보기' 시트를 통한 추가 탭 도달성 회귀.

모바일 하단바(390x844)에는 자리가 없어 앨범·상점·조화도·설정 탭이 '더보기'
시트(#mobile-more-sheet)로 밀려 있다. 과거 "렌더는 되지만 진입점이 없어
도달 불가"였던 사고(설정 탭 모바일 도달 불가)와 같은 계열을 막기 위해,
사용자가 실제로 더보기 버튼을 눌러 시트를 열고 그 안의 탭으로 이동할 수
있는지 화면 구조·가시성·접근성 속성 위주로 검증한다. 외부 네트워크에는
의존하지 않는다(로그인 판정은 클라이언트 localStorage 플래그로 재현).
"""

NAME = "모바일 더보기 시트 추가 탭 도달성"


def run(page, base_url):
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    # 모바일 뷰포트로 고정해야 하단바·더보기 버튼이 노출된다.
    page.set_viewport_size({"width": 390, "height": 844})

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

    # 로그인 레이아웃: 본문·하단 네비 노출.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert page.locator("#mobile-navbar").count() == 1, "모바일 하단 네비가 없음"

    # 더보기 버튼이 모바일에서 실제로 보이고 접근성 속성이 닫힘 상태여야 한다.
    more_btn = page.wait_for_selector("#mobile-more-btn", state="visible", timeout=10000)
    assert more_btn is not None, "모바일 더보기 버튼이 표시되지 않음"
    assert (
        page.locator("#mobile-more-btn").get_attribute("aria-expanded") == "false"
    ), "초기 더보기 버튼 aria-expanded가 false가 아님"

    # 시트는 초기에 숨겨져 있어야 한다.
    assert not page.locator("#mobile-more-sheet").is_visible(), "더보기 시트가 초기에 노출됨"

    # 시트 안에는 추가 탭(앨범·상점·조화도·설정)이 실제로 존재해야 한다.
    for tab in ("album", "shop", "saju", "settings"):
        assert (
            page.locator(f"#mobile-more-sheet [data-tab='{tab}']").count() >= 1
        ), f"더보기 시트에 '{tab}' 진입점이 없음"

    # 더보기 버튼을 눌러 시트를 연다 → 시트 노출 + aria-expanded=true.
    page.locator("#mobile-more-btn").click()
    page.wait_for_selector("#mobile-more-sheet:not(.hidden)", timeout=10000)
    assert page.locator("#mobile-more-sheet").is_visible(), "더보기 버튼 클릭 후 시트가 열리지 않음"
    assert (
        page.locator("#mobile-more-btn").get_attribute("aria-expanded") == "true"
    ), "시트 열림 후 aria-expanded가 true로 갱신되지 않음"

    # 시트 안 '설정' 탭으로 이동 → 설정 탭 표시 + 시트 자동 닫힘 + aria 복귀.
    page.locator("#mobile-more-sheet [data-tab='settings']").click()
    page.wait_for_selector("#tab-settings:not(.hidden)", timeout=10000)
    assert page.locator("#tab-settings").is_visible(), "더보기 시트에서 설정 탭으로 이동되지 않음"
    assert not page.locator("#mobile-more-sheet").is_visible(), "탭 이동 후 더보기 시트가 닫히지 않음"
    assert (
        page.locator("#mobile-more-btn").get_attribute("aria-expanded") == "false"
    ), "탭 이동 후 aria-expanded가 false로 복귀되지 않음"
    assert not page.locator("#tab-mypet").is_visible(), "설정 이동 후 이전 마이펫 탭이 여전히 노출됨"

    # 전 과정에서 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"더보기 시트 이동 중 미처리 JS 예외 발생: {js_errors[:3]}"
