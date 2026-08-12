"""펫나 E2E — 모바일에서 모든 탭이 숨김 메뉴 없이 도달 가능한지 회귀.

이 파일의 전신은 test_mobile_more_sheet_nav.py 였다. 2026-08-05에 하단바를 5칸으로
줄이고 앨범·상점·조화도·설정을 '더보기' 시트에 접었는데, 2026-08-12 오너 지시
("메뉴 더보기로 하지말고 다 볼수 있어야")로 시트를 없애고 콘텐츠 탭 7개를 한 줄에
모두 노출하도록 되돌렸다. 설정만 헤더 기어(#mobile-settings-btn)로 옮겼다 —
8칸을 넣으면 320px에서 칸이 40px로 줄어 권장 탭 타깃 44px을 못 맞추기 때문이다.

지키려는 것은 그때나 지금이나 같다: **"렌더는 되는데 갈 수가 없다"를 막는 것**
(2026-07-25 사고 — 설정 탭이 앱 어디에서도 진입 불가였는데, 순회 점검이
switchTab()을 직접 호출하는 방식이라 아무도 못 잡았다).

그래서 구현 방식(시트냐 한 줄이냐)을 못 박지 않고 **결과**만 단언한다:
전 탭이 실제 클릭으로 도달 가능하고, 탭 타깃이 44px 이상이며, 숨김 메뉴가 없다.
"""

NAME = "모바일 전 탭 숨김 메뉴 없이 도달 가능"

# 하단바에 노출되는 콘텐츠 탭(순서 포함). 설정은 헤더 기어라 따로 본다.
_BAR_TABS = ["mypet", "health", "walk", "social", "album", "shop", "saju"]
_MIN_TAP = 44


def _login(page, base_url):
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
    page.wait_for_selector("main", state="visible", timeout=15000)


def run(page, base_url):
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    page.set_viewport_size({"width": 390, "height": 844})
    _login(page, base_url)

    assert page.locator("#mobile-navbar").count() == 1, "모바일 하단 네비가 없음"

    # ---- 1. 숨김 메뉴가 없어야 한다(다시 접히는 회귀 차단) ----
    for sel in ("#mobile-more-btn", "#mobile-more-sheet"):
        assert page.locator(sel).count() == 0, (
            f"{sel} 가 부활했다 — 탭을 숨김 메뉴 뒤로 접지 않기로 했다(오너 지시 2026-08-12)"
        )

    # ---- 2. 콘텐츠 탭 7개가 하단바에 실제로 보인다 ----
    bar_tabs = page.eval_on_selector_all(
        "#mobile-navbar .mobile-tab-btn",
        "els => els.filter(e => e.offsetParent !== null).map(e => e.dataset.tab)",
    )
    assert bar_tabs == _BAR_TABS, f"하단바 탭 구성이 다르다: {bar_tabs}"

    # ---- 3. 설정은 헤더 기어로 도달 가능 ----
    gear = page.locator("#mobile-settings-btn")
    assert gear.count() == 1 and gear.is_visible(), (
        "모바일 설정 진입점(헤더 기어)이 없다 — 하단바에서 뺐으므로 이게 유일한 경로다"
    )

    # ---- 4. 탭 타깃 44px (rem 유틸리티는 이 앱에서 86.5%로 줄어든다 — px로 확인) ----
    small = page.evaluate(
        """(min) => {
            const out = [];
            document.querySelectorAll('#mobile-navbar .mobile-tab-btn').forEach(b => {
                const r = b.getBoundingClientRect();
                if (r.width < min || r.height < min) out.push(b.dataset.tab + ':' + Math.round(r.width) + 'x' + Math.round(r.height));
            });
            const g = document.getElementById('mobile-settings-btn').getBoundingClientRect();
            if (g.width < min || g.height < min) out.push('settings(gear):' + Math.round(g.width) + 'x' + Math.round(g.height));
            return out;
        }""",
        _MIN_TAP,
    )
    assert not small, f"탭 타깃이 {_MIN_TAP}px 미만인 항목: {small}"

    # ---- 5. 실제로 눌러서 이동되는가(핵심 — 존재만으로는 도달 가능이 아니다) ----
    for tab in _BAR_TABS[1:]:            # mypet은 초기 탭
        page.locator(f"#mobile-navbar .mobile-tab-btn[data-tab='{tab}']").click()
        page.wait_for_selector(f"#tab-{tab}:not(.hidden)", timeout=10000)
        assert page.locator(f"#tab-{tab}").is_visible(), f"'{tab}' 탭으로 이동되지 않음"

    gear.click()
    page.wait_for_selector("#tab-settings:not(.hidden)", timeout=10000)
    assert page.locator("#tab-settings").is_visible(), "헤더 기어로 설정 탭에 못 들어감"
    assert not page.locator("#tab-saju").is_visible(), "설정 이동 후 이전 탭이 여전히 노출됨"

    # ---- 6. 좁은 화면(320px)에서도 접히거나 넘치지 않는다 ----
    page.set_viewport_size({"width": 320, "height": 640})
    page.wait_for_timeout(300)
    narrow = page.evaluate(
        """(min) => {
            const btns = [...document.querySelectorAll('#mobile-navbar .mobile-tab-btn')];
            return {
                visible: btns.filter(b => b.offsetParent !== null).length,
                tooSmall: btns.filter(b => b.getBoundingClientRect().width < min).length,
                overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
            };
        }""",
        _MIN_TAP,
    )
    assert narrow["visible"] == len(_BAR_TABS), f"320px에서 탭이 사라졌다: {narrow}"
    assert narrow["tooSmall"] == 0, f"320px에서 탭 타깃이 {_MIN_TAP}px 미만: {narrow}"
    assert narrow["overflow"] <= 1, f"320px에서 가로 스크롤 발생: {narrow}"

    assert not js_errors, f"탭 이동 중 미처리 JS 예외: {js_errors[:3]}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
