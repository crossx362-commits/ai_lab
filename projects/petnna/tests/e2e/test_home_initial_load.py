"""펫나 E2E — 홈 초기 로딩 검증.

페이지 초기 로드 시 메인 콘텐츠가 정상 렌더링되고,
브라우저 콘솔에 심각 오류가 없으며,
화면 구조와 가시성이 예상을 만족하는지 확인한다.
외부 네트워크(수파베이스, AI 서비스) 성공에 의존하지 않고
HTML 구조·레이아웃·로깅 상태만 검증한다.
"""

NAME = "홈 초기 로딩"


def run(page, base_url):
    """E2E 플로우:
    1. 페이지 초기 로드
    2. 로그인 상태 판정 (로그인/비로그인 모두 대응)
    3. 메인 콘텐츠 또는 로그인 오버레이 표시 여부
    4. 심각 콘솔 오류 검증
    5. 초기 탭(마이펫) 렌더링 확인
    """
    # 페이지 진입 (초기 2.5초 렌더 대기)
    page.goto(base_url)
    page.wait_for_timeout(2500)

    # === Step 1: 로그인 상태 판정 ===
    # 로그인 오버레이 또는 메인 콘텐츠 중 하나가 보여야 한다.
    login_overlay = page.locator("#login-landing-overlay")
    main_content = page.locator("main")
    mobile_navbar = page.locator("#mobile-navbar")

    overlay_visible = login_overlay.is_visible()
    main_visible = main_content.is_visible()

    assert overlay_visible or main_visible, \
        "로그인 오버레이도, 메인 콘텐츠도 보이지 않음 — 페이지 렌더링 실패"

    # === Step 2: 로그인 상태별 UI 검증 ===
    if overlay_visible:
        # 비로그인 상태: 로그인 오버레이만 보이고 메인/네비는 숨김
        assert not main_visible, "비로그인인데 메인 콘텐츠가 노출됨"
        assert not mobile_navbar.is_visible(), "비로그인인데 모바일 네비가 노출됨"

        # 로그인 오버레이 구조 검증
        assert page.locator("#login-landing-overlay h2").is_visible(), \
            "로그인 오버레이 헤딩이 표시되지 않음"
        assert page.locator("#login-email-input").is_visible(), \
            "이메일 입력란이 보이지 않음"
        assert page.locator("#login-submit-btn").is_visible(), \
            "로그인 제출 버튼이 보이지 않음"
    else:
        # 로그인 상태: 메인 콘텐츠와 하단 네비 표시
        assert main_visible, "로그인 상태인데 메인 콘텐츠가 숨겨짐"
        assert mobile_navbar.is_visible(), "로그인 상태인데 모바일 네비가 숨겨짐"

        # 기본 탭 콘텐츠 확인 (마이펫 탭 기본 활성)
        mypet_tab_content = page.locator("#tab-mypet")
        assert mypet_tab_content.is_visible(), "마이펫 탭 콘텐츠가 숨겨짐"

        # 하단 네비 버튼들이 보이는지 확인
        nav_buttons = page.locator(".mobile-tab-btn")
        assert nav_buttons.count() > 0, "하단 네비 버튼이 없음"

    # === Step 3: 콘솔 심각 오류 검증 ===
    # page.console 이벤트를 수집해 error/exception 수준 메시지 확인
    console_errors = []

    def on_console_message(msg):
        if msg.type in ("error", "exception"):
            console_errors.append(msg.text)

    page.on("console", on_console_message)
    page.wait_for_timeout(1000)  # 지연 스크립트 완료 대기
    page.remove_listener("console", on_console_message)

    # 허용 목록: 특정 외부 리소스 미로드는 무시
    allowed_errors = [
        "manifest.json",  # PWA manifest 선택사항
        "favicon.ico",    # favicon 선택사항
        "bot/uploads",    # 일부 리소스 조건부 로드
    ]

    critical_errors = [
        e for e in console_errors
        if not any(allowed in e for allowed in allowed_errors)
    ]

    assert len(critical_errors) == 0, \
        f"콘솔 오류 감지됨: {'; '.join(critical_errors[:3])}"

    # === Step 4: 모바일 헤더 (로그인 상태) 검증 ===
    if not overlay_visible:
        mobile_header = page.locator("#mobile-header")
        if mobile_header.is_visible():
            # 모바일 헤더가 보이면 페이지 제목이 있어야 함
            page_title = page.locator("#mobile-page-title")
            assert page_title.is_visible(), "모바일 헤더 페이지 제목이 표시되지 않음"

    # === Step 5: 레이아웃 기본 구조 검증 ===
    # 로그인된 경우, 메인 콘텐츠 영역이 실제 내용을 가지고 있는지 확인
    if main_visible:
        # 탭 콘텐츠 중 적어도 하나는 렌더링되어야 함
        tab_contents = page.locator(".tab-content")
        assert tab_contents.count() > 0, "탭 콘텐츠 컨테이너가 없음"

        # 첫 번째 탭 콘텐츠(마이펫)가 활성 상태여야 함
        first_tab = tab_contents.first
        assert first_tab.is_visible(), "첫 번째 탭 콘텐츠가 숨겨짐 (마이펫 탭)"
