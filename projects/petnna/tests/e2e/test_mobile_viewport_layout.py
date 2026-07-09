"""펫나 E2E — 모바일 뷰포트(390x844) 초기 홈 레이아웃 검증.

모바일 화면 크기에서 앱이 부팅되면 로그인 랜딩 오버레이가 뷰포트를
넘치지 않고 정상 노출되고, 비로그인 상태이므로 헤더·본문·하단 네비가
숨겨져 있으며, 부트스트랩 중 치명적 JS 예외가 발생하지 않는지 본다.
외부 네트워크(수파베이스) 성공에 의존하지 않고 화면 구조만 검증한다.
"""

NAME = "모바일 뷰포트(390x844) 홈 레이아웃"


def run(page, base_url):
    # 부트스트랩 중 발생한 미처리 JS 예외를 수집(수파베이스 호출은 통상 try로 감싸짐).
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    page.set_viewport_size({"width": 390, "height": 844})
    page.goto(base_url)

    # 모바일에서도 로그인 랜딩 오버레이가 보여야 한다(앱 부팅 신호).
    overlay = page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    assert overlay is not None, "모바일 뷰포트에서 로그인 오버레이 미표시"

    # 오버레이 내부 카드가 뷰포트 가로폭(390)을 넘치지 않아야 한다(가로스크롤 방지).
    box = overlay.bounding_box()
    assert box is not None, "오버레이 바운딩 박스를 얻지 못함"
    assert box["width"] <= 390 + 1, f"오버레이가 뷰포트 가로폭 초과: {box['width']}"

    # 문서 전체가 가로로 넘치지 않아야 한다.
    overflow_x = page.evaluate(
        "() => document.documentElement.scrollWidth - document.documentElement.clientWidth"
    )
    assert overflow_x <= 1, f"모바일에서 가로 오버플로 발생: {overflow_x}px"

    # 로그인 폼 핵심 요소가 모바일에서도 보여야 한다.
    assert page.locator("#login-email-input").is_visible(), "모바일에서 이메일 입력란 미표시"
    assert page.locator("#login-submit-btn").is_visible(), "모바일에서 로그인 버튼 미표시"

    # 비로그인 상태이므로 헤더·본문·하단 네비는 숨겨져 있어야 한다.
    assert not page.locator("main").is_visible(), "비로그인인데 본문이 노출됨"
    assert not page.locator("#mobile-navbar").is_visible(), "비로그인인데 하단 네비가 노출됨"
    assert not page.locator("#mobile-header").is_visible(), "비로그인인데 모바일 헤더가 노출됨"

    # 부트스트랩 중 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"초기 로딩 중 미처리 JS 예외 발생: {js_errors[:3]}"
