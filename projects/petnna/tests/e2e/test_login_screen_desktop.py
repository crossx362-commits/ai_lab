"""펫나 E2E — 로그인 화면 데스크톱(lg) 뷰포트 레이아웃 회귀.

1440px 폭에서 로그인 랜딩 오버레이가 열렸을 때
브랜딩 패널(로고 아이콘·환영 헤딩)이 실제로 보이고,
가로 스크롤(오버플로)이 발생하지 않는지 단언한다.
데스크톱 split 회귀를 초록불이 보증하도록 하는 목적이며
외부 네트워크(수파베이스) 성공에 의존하지 않고 화면 구조만 본다.
"""

NAME = "로그인 화면 데스크톱 뷰포트 브랜드 패널·가로 스크롤 부재"


def run(page, base_url):
    page.set_viewport_size({"width": 1440, "height": 900})
    page.goto(base_url)

    # 로그인 랜딩 오버레이가 lg 뷰포트에서도 보여야 한다(비로그인 최초 진입).
    overlay = page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    assert overlay is not None, "1440px에서 로그인 오버레이가 표시되지 않음"

    # 뷰포트가 실제로 데스크톱 폭인지 확인(측정 신뢰성 담보).
    inner_w = page.evaluate("() => window.innerWidth")
    assert inner_w >= 1200, f"뷰포트 폭이 데스크톱이 아님(innerWidth={inner_w})"

    # 브랜딩 패널: 로고(paw) 아이콘과 환영 헤딩이 보여야 한다.
    paw = page.wait_for_selector("#login-landing-overlay .fa-paw", state="visible", timeout=5000)
    assert paw is not None, "브랜드 로고 아이콘이 데스크톱에서 미표시"

    heading = page.wait_for_selector("#login-landing-overlay h2", state="visible", timeout=5000)
    assert "펫과나" in (heading.inner_text() or ""), "환영 헤딩 텍스트가 예상과 다름"

    # 로그인 폼 요소도 데스크톱 폭에서 정상 노출되어야 한다.
    assert page.locator("#google-login-btn").is_visible(), "구글 로그인 버튼 미표시(lg)"
    assert page.locator("#login-email-input").is_visible(), "이메일 입력란 미표시(lg)"
    assert page.locator("#login-submit-btn").is_visible(), "로그인 제출 버튼 미표시(lg)"

    # 가로 스크롤(오버플로)이 없어야 한다 — 데스크톱 split 레이아웃 회귀 방지.
    overflow_x = page.evaluate(
        "() => document.documentElement.scrollWidth - document.documentElement.clientWidth"
    )
    assert overflow_x <= 1, f"데스크톱에서 가로 스크롤 오버플로 발생(초과폭={overflow_x}px)"

    # 오버레이 카드가 뷰포트 가로 경계 안에 완전히 들어와야 한다.
    box = page.locator("#login-landing-overlay > div").first.bounding_box()
    assert box is not None, "오버레이 카드 박스를 측정하지 못함"
    assert box["x"] >= -1, f"오버레이 카드가 좌측으로 벗어남(x={box['x']})"
    assert box["x"] + box["width"] <= inner_w + 1, (
        f"오버레이 카드가 우측 뷰포트를 벗어남(right={box['x'] + box['width']}, vw={inner_w})"
    )

    # ── 2단 split (2026-08-08 회의 결정) ────────────────────────────────
    # 데스크톱에서 좌측 히어로 패널이 보이고, 인증 카드와 **가로로 나란히** 놓인다.
    # 카피 문구는 단언하지 않는다 — 리터럴을 못 박으면 문구 개선이 회귀로 잡힌다
    # (2026-07-28 교훈: 카피를 단언하는 테스트는 회귀가 아니라 개선을 막는다).
    hero = page.wait_for_selector("#login-hero-panel", state="visible", timeout=5000)
    assert hero is not None, "데스크톱 좌측 히어로 패널이 없다 — split 레이아웃 미적용"

    hero_box = page.locator("#login-hero-panel").bounding_box()
    auth_box = page.locator("#login-auth-panel").bounding_box()
    assert hero_box and auth_box, "히어로/인증 패널 박스를 측정하지 못함"
    assert hero_box["x"] + hero_box["width"] <= auth_box["x"] + 1, (
        "데스크톱에서 히어로와 인증 패널이 가로로 나란하지 않다 "
        f"(hero right={hero_box['x'] + hero_box['width']}, auth x={auth_box['x']})")

    # split의 존재 이유: 죽은 여백을 줄이는 것. 카드가 화면 폭을 충분히 쓰는지 본다.
    shell = page.locator("#login-shell").bounding_box()
    assert shell is not None, "#login-shell 을 찾지 못함"
    assert shell["width"] >= inner_w * 0.5, (
        f"데스크톱에서 로그인 셸이 화면 폭의 절반도 안 쓴다(폭 {shell['width']} / {inner_w}) — "
        "split으로 바꾼 의미가 없다")


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
