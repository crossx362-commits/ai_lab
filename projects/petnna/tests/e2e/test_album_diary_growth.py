"""펫나 E2E — 일기장(앨범) 탭 진입·다이어리 타임라인·성장 기록 골격 검증.

앨범 탭 콘텐츠(ALBUM_TEMPLATE)는 앱 init 시 #tab-album에 주입되므로
외부 네트워크(수파베이스) 없이도 골격이 존재한다. 로그인은 클라이언트
localStorage 플래그로만 재현하고, 헤더 '일기장' 탭 전환 후 다이어리
타임라인·통계 행·성장 기록 카드 같은 핵심 구조가 보이는지, 이전 탭이
숨겨지는지, 그 과정에서 치명적 JS 예외가 없는지 화면 구조 위주로 검증한다.
"""

NAME = "일기장 탭 진입·다이어리 타임라인·성장 기록"


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

    # 로그인 레이아웃 확보: 본문 노출, 오버레이 숨김.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"

    # 기본 탭(마이펫)이 먼저 보인 뒤 헤더 '일기장' 탭으로 전환한다.
    page.wait_for_selector("#tab-mypet", state="visible", timeout=10000)
    page.locator("header .tab-btn[data-tab='album']").click()
    page.wait_for_selector("#tab-album:not(.hidden)", timeout=10000)
    assert page.locator("#tab-album").is_visible(), "일기장 탭으로 전환되지 않음"
    assert not page.locator("#tab-mypet").is_visible(), "전환 후 이전 마이펫 탭이 여전히 노출됨"

    # 다이어리 골격: 타임라인 컨테이너·통계 행이 DOM에 존재해야 한다.
    assert page.locator("#diary-timeline-container").count() == 1, "다이어리 타임라인 컨테이너가 없음"
    assert page.locator("#diary-stats-row").count() == 1, "다이어리 통계 행이 없음"
    assert page.locator("#diary-stats-row").is_visible(), "다이어리 통계 행이 보이지 않음"

    # 성장 기록 카드 골격: 컨테이너·발행 버튼 컨테이너가 존재하고 제목이 보인다.
    assert page.locator("#growth-chart-container").count() == 1, "성장 기록 차트 컨테이너가 없음"
    assert page.locator("#album-publish-btn-container").count() == 1, "앨범 발행 버튼 컨테이너가 없음"
    growth_title = page.locator("#tab-album").get_by_text("성장 기록", exact=False).first
    assert growth_title.is_visible(), "성장 기록 제목이 보이지 않음"

    # 다시 '마이펫' 복귀 시 앨범 탭이 숨겨져야 한다(탭 상호배타).
    page.locator("header .tab-btn[data-tab='mypet']").click()
    page.wait_for_selector("#tab-mypet:not(.hidden)", timeout=10000)
    assert not page.locator("#tab-album").is_visible(), "복귀 후 일기장 탭이 여전히 노출됨"

    # 전 과정에서 치명적 JS 예외가 없어야 한다.
    assert not js_errors, f"일기장 탭 흐름 중 미처리 JS 예외 발생: {js_errors[:3]}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
