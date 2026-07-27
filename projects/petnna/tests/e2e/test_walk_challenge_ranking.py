"""펫나 E2E — 산책 탭 주간 챌린지·동네 익명 리더보드 화면 검증.

로그인 판정은 클라이언트 localStorage(petna_is_logged_in)로만 이뤄지므로
외부 네트워크(수파베이스) 없이 로그인 레이아웃을 재현한다. 헤더 탭으로
'산책' 탭에 진입했을 때 WALK_TEMPLATE의 '내 주간 산책 목표' 카드 안에서
renderWalkChallenge()가 챌린지 진척·주간 목표 버튼(3/5/7회)·주간 배지 안내·
동네 익명 리더보드(샘플)와 '우리 아이 (나)' 행을 실제로 채우는지, 목표 버튼
클릭이 치명적 예외 없이 재렌더되어 선택 상태로 반영되는지 구조·가시성 위주로
검증한다. 리더보드는 앱 내부 샘플 데이터라 네트워크 성공에 의존하지 않는다.
"""

NAME = "산책 탭 주간 챌린지·동네 랭킹"


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

    # 로그인 레이아웃: 본문 노출, 로그인 오버레이 숨김.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"

    # 헤더 탭으로 '산책' 전환 → 산책 탭 표시.
    page.locator("header .tab-btn[data-tab='walk']").click()
    page.wait_for_selector("#tab-walk:not(.hidden)", timeout=10000)
    assert page.locator("#tab-walk").is_visible(), "산책 탭으로 전환되지 않음"

    # 주간 챌린지 카드 제목과 챌린지 섹션 컨테이너가 존재해야 한다.
    assert page.get_by_text("내 주간 산책 목표", exact=False).first.is_visible(), \
        "주간 산책 목표 카드 제목이 표시되지 않음"
    section = page.locator("#walk-challenge-section")
    assert section.count() == 1, "챌린지 섹션 컨테이너(#walk-challenge-section)가 없음"

    # renderWalkChallenge()가 컨테이너를 실제로 채웠는지 — 자식 요소가 생성될 때까지 대기.
    page.wait_for_function(
        "() => { const b = document.getElementById('walk-challenge-section');"
        " return b && b.children.length > 0; }",
        timeout=10000,
    )
    assert section.is_visible(), "챌린지 섹션이 렌더되었으나 보이지 않음"

    # 진척 요약·주간 목표 버튼(3/5/7회)·배지 안내가 렌더되어야 한다.
    assert section.get_by_text("이번 주 산책", exact=False).first.is_visible(), \
        "주간 산책 진척 요약이 표시되지 않음"
    assert section.get_by_text("주간 챌린지 배지", exact=False).first.is_visible(), \
        "주간 챌린지 배지 안내가 표시되지 않음"
    for label in ("3회", "5회", "7회"):
        assert section.get_by_role("button", name=label).count() >= 1, \
            f"주간 목표 버튼 '{label}'이 없음"

    # 동네 익명 리더보드(샘플)와 '우리 아이 (나)' 본인 행이 있어야 한다.
    assert section.get_by_text("동네 익명 리더보드", exact=False).first.is_visible(), \
        "동네 익명 리더보드 라벨이 표시되지 않음"
    assert section.get_by_text("우리 아이 (나)", exact=False).first.is_visible(), \
        "리더보드에 본인(우리 아이) 행이 없음"

    # 상호작용: 목표를 7회로 변경 → 재렌더되어도 챌린지 구조가 유지되고 7회 버튼이 활성 상태.
    seven = section.get_by_role("button", name="7회").first
    seven.click()
    page.wait_for_function(
        "() => { const btns = document.querySelectorAll('#walk-challenge-section button');"
        " for (const b of btns) { if (b.textContent.trim() === '7회')"
        " return b.className.includes('bg-brand-500'); } return false; }",
        timeout=10000,
    )
    assert section.get_by_text("우리 아이 (나)", exact=False).first.is_visible(), \
        "목표 변경 재렌더 후 리더보드 본인 행이 사라짐"

    # 전 과정에서 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"산책 챌린지 렌더 중 미처리 JS 예외 발생: {js_errors[:3]}"
