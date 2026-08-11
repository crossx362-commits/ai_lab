NAME = "로그인 split 레이아웃 앵커·캡처 시 랜딩 오버레이 숨김"


def _overflow_x(page):
    return page.evaluate(
        "() => document.documentElement.scrollWidth - document.documentElement.clientWidth"
    )


def _wait_login_ready(page):
    page.wait_for_selector("#login-landing-overlay", timeout=15000)
    page.wait_for_selector("#login-shell", timeout=15000)
    # 좌측 히어로 패널·폼 섹션은 클래스 조합이 아니라 고유 ID로 앵커링(2026-07-25 교훈)
    page.wait_for_selector("#login-hero-panel", timeout=15000)
    page.wait_for_selector("#login-form-section", timeout=15000)


def run(page, base_url):
    # ---- 데스크톱 뷰포트: split 레이아웃(좌 히어로 / 우 폼) 앵커·오버플로 ----
    page.set_viewport_size({"width": 1440, "height": 900})
    page.goto(base_url)
    page.wait_for_timeout(2500)
    _wait_login_ready(page)

    overlay = page.locator("#login-landing-overlay")
    assert overlay.is_visible(), "미로그인 상태에서 로그인 랜딩 오버레이가 보여야 한다"

    hero = page.locator("#login-hero-panel")
    form = page.locator("#login-form-section")
    features = page.locator("#login-hero-features")
    assert hero.is_visible(), "데스크톱에서 좌측 히어로 패널이 보여야 한다"
    assert form.is_visible(), "데스크톱에서 로그인 폼 섹션이 보여야 한다"
    assert features.count() == 1, "히어로 가치제안 피처 그리드 앵커가 존재해야 한다"

    hero_box = hero.bounding_box()
    form_box = form.bounding_box()
    assert hero_box and form_box, "히어로/폼 바운딩박스를 얻어야 한다"
    # lg:flex-row 이므로 히어로가 폼보다 왼쪽에 위치(카피 텍스트가 아니라 배치로 단언)
    assert hero_box["x"] < form_box["x"], (
        "데스크톱 split에서 히어로 패널이 폼 섹션보다 좌측이어야 한다 "
        f"(hero.x={hero_box['x']}, form.x={form_box['x']})"
    )

    # 로그인 입력 요소 도달성(리터럴 카피 단언 없이 구조만)
    for sel in ("#login-email-input", "#login-password-input", "#login-submit-btn"):
        assert page.locator(sel).is_visible(), f"{sel} 이(가) 데스크톱에서 보여야 한다"

    assert _overflow_x(page) <= 1, "데스크톱 로그인 화면에 가로 스크롤이 없어야 한다"

    # ---- 모바일 뷰포트: 스택 레이아웃·오버플로·폼 도달성 ----
    page.set_viewport_size({"width": 390, "height": 844})
    page.goto(base_url)
    page.wait_for_timeout(2500)
    _wait_login_ready(page)

    assert page.locator("#login-landing-overlay").is_visible(), "모바일에서도 로그인 오버레이가 보여야 한다"
    assert page.locator("#login-hero-panel").is_visible(), "모바일에서 히어로 패널이 스택되어 보여야 한다"
    for sel in ("#login-email-input", "#login-password-input", "#login-submit-btn"):
        assert page.locator(sel).is_visible(), f"{sel} 이(가) 모바일에서 도달 가능해야 한다"
    assert _overflow_x(page) <= 1, "모바일 로그인 화면에 가로 스크롤이 없어야 한다"

    # 회귀(2026-08-11): 히어로 패널 **본체**에 overflow-hidden을 걸면 flex 아이템의
    # min-height:auto 가 0으로 바뀐다. 모바일(flex-col, min-h-full)에서 패널이 제
    # 콘텐츠보다 작게 찌그러지고 그 내용을 스스로 잘라냈다(실측 422px→195px).
    # 장식(그라디언트 블러)을 클리핑할 땐 패널이 아니라 별도 레이어에만 걸 것.
    # is_visible()은 이 결함을 못 잡는다 — 잘려도 요소는 '보인다'.
    clip = page.evaluate(
        """() => { const el = document.getElementById('login-hero-panel');
                   return { s: el.scrollHeight, c: el.clientHeight }; }"""
    )
    assert clip["s"] <= clip["c"] + 1, (
        "모바일에서 히어로 패널이 제 콘텐츠를 잘라내면 안 된다 "
        f"(scrollHeight={clip['s']}, clientHeight={clip['c']})"
    )

    # ---- 회귀: 공유(care-share) 캡처 시 랜딩 오버레이가 실제로 숨는지 ----
    page.set_viewport_size({"width": 1440, "height": 900})
    page.goto(base_url)
    page.wait_for_timeout(2500)
    _wait_login_ready(page)
    assert page.locator("#login-landing-overlay").is_visible(), "캡처 전 오버레이는 보여야 한다"
    page.evaluate(
        """
        () => {
          const obj = { v: 1, p: '테스트', d: '2026-08-08',
                        t: [{ ti: '09:00', x: '산책', ty: 'walk' }] };
          const b = btoa(unescape(encodeURIComponent(JSON.stringify(obj))))
                      .replace(/\\+/g, '-').replace(/\\//g, '_').replace(/=+$/, '');
          history.replaceState({}, '', '/care?care=' + b);
          try { window.CareShare && window.CareShare._route(); } catch (e) {}
        }
        """
    )
    page.wait_for_function(
        "() => document.documentElement.classList.contains('cs-share-active')",
        timeout=10000,
    )
    assert not page.locator("#login-landing-overlay").is_visible(), (
        "공유 캡처(cs-share-active) 시 랜딩 오버레이가 CSS로 숨겨져야 한다"
    )

    # ---- 회귀: 실종 QR(public-profile) 캡처 시 랜딩 오버레이가 실제로 숨는지 ----
    page.goto(base_url)
    page.wait_for_timeout(2500)
    _wait_login_ready(page)
    assert page.locator("#login-landing-overlay").is_visible(), "QR 캡처 전 오버레이는 보여야 한다"
    page.evaluate(
        """
        () => {
          history.replaceState({}, '', '/p/ABCDEFGH12');
          try { window.PublicProfile && window.PublicProfile._route(); } catch (e) {}
        }
        """
    )
    page.wait_for_function(
        "() => document.documentElement.classList.contains('pp-finder-active')",
        timeout=10000,
    )
    assert not page.locator("#login-landing-overlay").is_visible(), (
        "실종 QR 캡처(pp-finder-active) 시 랜딩 오버레이가 CSS로 숨겨져야 한다"
    )


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
