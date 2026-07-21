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

    # === 모바일 하단 네비 순서·개수 고정 (2026-07-21 회의 결정) ===
    # 로그인 상태로 재진입해 하단 네비가 결정된 6칸 구성·순서와 일치하는지 검증.
    # 데스크톱 탭 재정렬(조화도 유틸 강등)이 모바일 6칸(조화도·설정 제외)을
    # 건드리지 않았는지 회귀로 잡는다.
    import json as _json
    _pet = {"id": 990721, "name": "네비견", "breed": "믹스", "type": "dog",
            "imageUrl": "", "age": "2살", "weight": "8", "gender": "남아",
            "personality": "온순", "hunger": 70, "happy": 80}
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_mobilenav@petna.co.kr');"
        "localStorage.setItem('petna_pets', %s);" % _json.dumps(_json.dumps([_pet]))
    )
    page.goto(base_url)
    page.wait_for_selector("#mobile-navbar", state="visible", timeout=15000)
    nav_tabs = page.evaluate(
        "() => [...document.querySelectorAll('#mobile-navbar .mobile-tab-btn')]"
        ".map(b => b.getAttribute('data-tab'))"
    )
    expected = ["mypet", "health", "walk", "social", "album", "shop"]
    assert nav_tabs == expected, \
        f"모바일 하단 네비 구성/순서 회귀: {nav_tabs} (기대: {expected})"
