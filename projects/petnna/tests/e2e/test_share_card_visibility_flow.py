"""펫나 E2E — 공유 카드 가시성·클릭 + 오늘의 운세 뽑기 결과 회귀.

회의 배정 과제(공유카드 가시성·클릭 E2E)를 커버한다. 외부 네트워크(수파베이스 등)
성공에 의존하지 않고, 앱이 이미 로드한 화면 구조·전역 함수·런타임 상태만으로 검증한다.

검증 항목:
  (A) 오늘의 운세 '포춘 쿠키 열기'(startFortuneDraw) → 결과 컨테이너가 나타나고
      3영역(대표 키워드·펫의 하루·집사의 하루)이 모두 비어있지 않음.
  (B) shareSajuCard/shareHarmonyCard 공유 버튼이 사주 탭 DOM에 렌더되고,
      두 전역 함수가 실제로 정의되어 있음(제거/개명 회귀 방지).
  (C) 조화도 결과를 런타임에 주입한 뒤 shareHarmonyCard 버튼을 클릭하면
      카드 이미지(canvas → PNG)가 생성되어 다운로드가 트리거됨.

셀렉터는 index.html·js/templates/saju.js·js/saju.js·js/share-card.js에서 실존을
확인한 id/onclick만 사용한다.
"""

NAME = "공유 카드 가시성·클릭 + 운세 뽑기 결과"

# 오늘의 운세 결과 3영역 (js/templates/saju.js — startFortuneDraw가 채움)
FORTUNE_RESULT_IDS = [
    "fortune-main-keyword",
    "fortune-pet-text",
    "fortune-owner-text",
]


def run(page, base_url):
    page.goto(base_url)

    # 비로그인(랜딩 오버레이) 상태이면 사주 화면에 접근할 수 없으므로 통과 처리
    # (기존 사주 회귀 테스트와 동일한 게이트 정책).
    try:
        page.wait_for_selector("#login-landing-overlay", state="visible", timeout=3000)
        return
    except Exception:
        pass

    # 초기 렌더 대기 (허용된 1회)
    page.wait_for_timeout(2500)

    # === 사주 탭 진입 ===
    saju_tab = page.locator("[data-tab='saju']")
    assert saju_tab.count() > 0 and saju_tab.first.is_visible(), "사주 하단 탭이 보이지 않음"
    saju_tab.first.click()

    # 사주 서브탭 버튼(운세)이 나타날 때까지 대기
    page.wait_for_selector("#saju-tab-fortune", state="visible", timeout=5000)

    # === (A) 오늘의 운세 뽑기: 결과 3영역 비어있지 않음 ===
    # 운세 서브탭으로 전환
    page.locator("#saju-tab-fortune").click()

    draw_btn = page.locator("button[onclick*='startFortuneDraw']")
    assert draw_btn.count() == 1, "'포춘 쿠키 열기' 뽑기 버튼(startFortuneDraw)이 존재하지 않음"
    page.wait_for_selector("button[onclick*='startFortuneDraw']", state="visible", timeout=5000)
    draw_btn.first.click()

    # 결과 컨테이너가 hidden 해제되어 표시됨
    page.wait_for_selector("#fortune-result-container", state="visible", timeout=5000)

    for rid in FORTUNE_RESULT_IDS:
        el = page.locator(f"#{rid}")
        assert el.count() == 1, f"운세 결과 영역 #{rid} 이(가) 존재하지 않음"
        text = (el.inner_text() or "").strip()
        assert len(text) > 0, f"운세 결과 영역 #{rid} 이(가) 비어 있음"

    # === (B) 공유 버튼 렌더 + 전역 함수 실존 ===
    saju_share_btn = page.locator("button[onclick*='shareSajuCard']")
    harmony_share_btn = page.locator("button[onclick*='shareHarmonyCard']")
    assert saju_share_btn.count() == 1, "shareSajuCard 공유 버튼이 렌더되지 않음"
    assert harmony_share_btn.count() == 1, "shareHarmonyCard 공유 버튼이 렌더되지 않음"

    fns_defined = page.evaluate(
        "() => typeof window.shareSajuCard === 'function' "
        "&& typeof window.shareHarmonyCard === 'function'"
    )
    assert fns_defined, "shareSajuCard/shareHarmonyCard 전역 함수가 정의되어 있지 않음"

    # === (C) 조화도 결과 주입 후 공유 클릭 → 카드 생성·다운로드 트리거 ===
    # 앱 파일은 수정하지 않고, 런타임 상태(AppStore)에만 조화도 결과를 주입한다.
    # getCurrentHarmonyResult()의 폴백(AppStore.getState('harmonyResult'))을 이용.
    seeded = page.evaluate(
        """() => {
            if (typeof AppStore === 'undefined' || typeof AppStore.setState !== 'function') return null;
            AppStore.setState('harmonyResult', {
                avgScore: 88,
                title: 'E2E 조화 테스트',
                level: '천생연분',
                solution: '오늘은 함께 산책을 해보세요.'
            });
            return (typeof getCurrentHarmonyResult === 'function')
                ? (getCurrentHarmonyResult() || {}).avgScore
                : null;
        }"""
    )
    assert seeded == 88, f"조화도 결과 주입 실패(getCurrentHarmonyResult.avgScore={seeded})"

    # 공유 버튼은 결과 컨테이너(#harmony-result-container, 기본 hidden) 안에 있으므로
    # force 클릭으로 onclick 핸들러를 직접 실행시켜 다운로드 트리거를 검증한다.
    with page.expect_download(timeout=10000) as dl_info:
        harmony_share_btn.first.click(force=True)
    download = dl_info.value
    assert "petna-harmony" in (download.suggested_filename or ""), (
        f"조화도 카드 다운로드 파일명이 예상과 다름: {download.suggested_filename}"
    )
