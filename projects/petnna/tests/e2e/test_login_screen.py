"""펫나 E2E — 로그인 화면 진입·폼 요소 표시 검증.

비로그인 상태 최초 진입 시 로그인 랜딩 오버레이가 노출되고
소셜/이메일 로그인 폼 요소가 정상적으로 보이는지 확인한다.
외부 네트워크(수파베이스) 성공에 의존하지 않고 화면 구조만 본다.
"""

NAME = "로그인 화면 진입·폼 요소 표시"


def run(page, base_url):
    page.goto(base_url)

    # 로그인 랜딩 오버레이가 보여야 한다(비로그인 최초 진입).
    overlay = page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    assert overlay is not None, "로그인 오버레이가 표시되지 않음"

    # 환영 헤딩 텍스트 확인.
    heading = page.wait_for_selector("#login-landing-overlay h2", state="visible", timeout=5000)
    assert "펫과나" in (heading.inner_text() or ""), "환영 헤딩 텍스트가 예상과 다름"

    # 소셜 로그인 버튼 2종이 보여야 한다.
    assert page.locator("#google-login-btn").is_visible(), "구글 로그인 버튼 미표시"
    assert page.locator("#kakao-login-btn").is_visible(), "카카오 로그인 버튼 미표시"

    # 이메일 로그인 폼 입력·제출 요소가 보여야 한다.
    assert page.locator("#login-email-input").is_visible(), "이메일 입력란 미표시"
    assert page.locator("#login-password-input").is_visible(), "비밀번호 입력란 미표시"
    assert page.locator("#login-submit-btn").is_visible(), "로그인 제출 버튼 미표시"

    # 비로그인 상태에선 메인 콘텐츠·하단 네비가 숨겨져 있어야 한다.
    assert not page.locator("main").is_visible(), "비로그인인데 메인 콘텐츠가 노출됨"
    assert not page.locator("#mobile-navbar").is_visible(), "비로그인인데 하단 네비가 노출됨"

    # 회원가입 토글 시 회원가입 폼이 나타나는지 확인.
    page.locator("#login-form-section button:has-text('회원가입')").click()
    signup = page.wait_for_selector("#signup-form-section", state="visible", timeout=5000)
    assert signup is not None, "회원가입 폼 토글이 동작하지 않음"
    assert page.locator("#signup-nickname-input").is_visible(), "회원가입 닉네임 입력란 미표시"
