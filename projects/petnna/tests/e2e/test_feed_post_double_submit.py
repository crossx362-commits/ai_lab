"""펫나 E2E — 피드 발행 더블클릭 중복 발행 회귀 앵커.

소셜 피드의 '자랑 발행' 버튼을 즉시 2회 클릭했을 때, 새 글이 정확히 +1건만
등록되는지(재진입 가드 회귀 방지) 검증한다. submitFeedPost는 글 생성 직후 입력
textarea 값을 비우므로, 곧바로 이어지는 두 번째 호출은 빈 본문으로 조기 반환돼야
한다 — 이 가드가 깨지면 한 번의 의도로 두 글이 발행된다.

로그인 판정은 클라이언트 localStorage(petna_is_logged_in)로만 이뤄지므로 외부
네트워크(수파베이스) 없이 로그인 레이아웃을 재현하고, 화면 구조(피드 카드 개수)만
관찰한다. 발행 후 뜨는 완료 다이얼로그 오버레이가 두 번째 클릭을 가로채는 걸 피하기
위해, 실제 발행 버튼 엘리먼트의 네이티브 click()을 한 마이크로태스크 안에서 연속 2회
동기 호출해 '즉시 2회 클릭'의 재진입 조건을 정확히 재현한다.
"""

NAME = "피드 발행 더블클릭 중복 발행 회귀"


def run(page, base_url):
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    # 최초 진입(오리진 확보) → 로그인 플래그 주입 → 재로딩.
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

    # 로그인 레이아웃 진입 확인.
    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"

    # 초기 렌더 안정화(1회 한정).
    page.wait_for_timeout(2500)

    # 소셜 탭 → 피드 서브탭 진입.
    page.locator("header .tab-btn[data-tab='social']").click()
    page.wait_for_selector("#tab-social:not(.hidden)", timeout=10000)
    page.locator("#social-subtab-feed-btn").click()
    page.wait_for_selector("#social-subtab-feed-content:not(.hidden)", timeout=10000)

    textarea = page.wait_for_selector("#feed-input-content", state="visible", timeout=10000)
    assert textarea is not None, "피드 본문 입력창이 표시되지 않음"

    publish_btn = page.locator("#social-subtab-feed-content button", has_text="자랑 발행")
    assert publish_btn.count() >= 1, "자랑 발행 버튼을 찾지 못함"

    # 이번 발행을 유일하게 식별할 마커 본문.
    marker = f"E2E중복발행테스트-{page.evaluate('Date.now()')}"

    def marker_card_count():
        return page.evaluate(
            """(m) => Array.from(document.querySelectorAll('#feed-list > div'))
                .filter(d => (d.textContent || '').includes(m)).length""",
            marker,
        )

    # 발행 전에는 마커를 포함한 피드 카드가 없어야 한다.
    assert marker_card_count() == 0, "발행 전에 마커 글이 이미 존재함"

    textarea.fill(marker)

    # '즉시 2회 클릭' 재현: 발행 버튼의 네이티브 click()을 한 호흡에 연속 2회 동기 호출.
    publish_btn.first.evaluate("el => { el.click(); el.click(); }")

    # 렌더 후 마커 글이 정확히 1건만 존재해야 한다(재진입 가드).
    page.wait_for_function(
        """(m) => Array.from(document.querySelectorAll('#feed-list > div'))
            .filter(d => (d.textContent || '').includes(m)).length >= 1""",
        arg=marker,
        timeout=10000,
    )
    count = marker_card_count()
    assert count == 1, f"더블클릭으로 중복 발행됨(재진입 가드 회귀): 마커 글 {count}건"

    # 입력창은 발행 후 비워져 있어야 한다(가드의 근거).
    assert page.locator("#feed-input-content").input_value() == "", "발행 후 입력창이 비워지지 않음"

    assert not js_errors, f"발행 흐름 중 미처리 JS 예외 발생: {js_errors[:3]}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
