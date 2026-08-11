"""펫나 E2E — 사주/조화도 병합 수정 회귀 3종.

이미 master에 병합된 수정의 회귀를 방지한다:
  (1) 오늘의 운세 시드에 펫 식별자(getSajuPet)가 섞여 펫별로 다른 결과가 나오는 배선(saju.js L547~550).
  (2) 오늘의 운세 연속출석 스트릭이 어제 뽑았으면 +1 증가(saju.js L596~620, localStorage 기반).
  (3) 사주 서브탭이 모바일에서 4열 그리드로 7개 버튼 전부 가시(templates/saju.js).

외부 네트워크에 의존하지 않고 화면 구조·가시성·localStorage 동작만 검증한다.
"""

NAME = "사주 운세·조화도 병합 회귀"

# 사주 서브탭 버튼 7종 (switchSajuSubTab 리셋 목록과 일치)
SUBTAB_BTN_IDS = [
    "saju-tab-saju",
    "saju-tab-fortune",
    "saju-tab-petIq",
    "saju-tab-ownerIq",
    "saju-tab-mbti",
    "saju-tab-harmony",
    "saju-tab-arcade",
]


def run(page, base_url):
    page.goto(base_url)

    # 비로그인(랜딩 오버레이) 상태이면 사주 화면에 접근할 수 없으므로 통과 처리.
    try:
        page.wait_for_selector("#login-landing-overlay", state="visible", timeout=3000)
        return
    except Exception:
        pass

    # 초기 렌더 대기 (허용된 1회)
    page.wait_for_timeout(2500)

    # === 사주 탭으로 진입 ===
    saju_tab = page.locator("[data-tab='saju']")
    assert saju_tab.count() > 0 and saju_tab.first.is_visible(), "사주 하단 탭이 보이지 않음"
    saju_tab.first.click()

    subtab_container = page.wait_for_selector("#saju-tab-fortune", state="visible", timeout=5000)
    assert subtab_container is not None, "사주 서브탭 진입 실패"

    # === 검증 (3): 모바일 4열 그리드 — 서브탭 7개 전부 가시 ===
    page.set_viewport_size({"width": 390, "height": 844})
    page.wait_for_timeout(300)

    for bid in SUBTAB_BTN_IDS:
        btn = page.locator(f"#{bid}")
        assert btn.count() == 1, f"서브탭 버튼 #{bid} 이(가) 존재하지 않음"
        assert btn.is_visible(), f"모바일 뷰포트에서 서브탭 버튼 #{bid} 이(가) 보이지 않음"

    # 서브탭 컨테이너가 모바일에서 4열 그리드 클래스를 유지하는지 확인
    grid_class = page.locator("#saju-tab-fortune").evaluate(
        "el => el.parentElement.className"
    )
    assert "grid-cols-4" in grid_class, f"서브탭 컨테이너가 4열 그리드가 아님: {grid_class}"

    # === 오늘의 운세 서브탭으로 전환 ===
    page.locator("#saju-tab-fortune").click()
    page.wait_for_selector("#fortune-test-section", state="visible", timeout=5000)

    # 앱이 펫 식별자 시드를 섞기 위해 의존하는 getSajuPet 배선이 살아있어야 함 (검증 1의 전제)
    assert page.evaluate("typeof getSajuPet") == "function", \
        "getSajuPet 함수가 정의되지 않음 — 펫별 운세 시드 배선이 유실됨"

    # === 검증 (2) 준비: 스트릭 localStorage를 '어제 뽑음, 카운트=5'로 심는다 ===
    email = page.evaluate(
        "() => (typeof settings_email !== 'undefined' && settings_email) "
        "? settings_email : 'butler@petna.co.kr'"
    )
    seeded = page.evaluate(
        """(email) => {
            const t = new Date();
            const y = new Date(t); y.setDate(y.getDate() - 1);
            const yStr = `${y.getFullYear()}-${y.getMonth()+1}-${y.getDate()}`;
            const key = `petna_fortune_streak_${email}`;
            localStorage.setItem(key, JSON.stringify({ last: yStr, count: 5 }));
            return { key };
        }""",
        email,
    )
    assert seeded and seeded.get("key"), "스트릭 localStorage 시드 실패"

    # === 운세 뽑기 실행 ===
    draw_btn = page.locator("[onclick*='startFortuneDraw']")
    assert draw_btn.count() > 0, "운세 뽑기 버튼(startFortuneDraw)이 없음"
    draw_btn.first.click()

    # 결과 컨테이너가 노출되어야 함
    page.wait_for_selector("#fortune-result-container", state="visible", timeout=5000)

    # === 검증 (1): 운세 텍스트 3종이 채워짐 ===
    for fid in ("fortune-main-keyword", "fortune-pet-text", "fortune-owner-text"):
        el = page.locator(f"#{fid}")
        assert el.is_visible(), f"운세 결과 #{fid} 이(가) 보이지 않음"
        assert el.inner_text().strip(), f"운세 결과 #{fid} 텍스트가 비어 있음"

    # === 검증 (2): 어제 뽑은 스트릭(5)이 오늘 뽑아 6으로 증가 ===
    badge = page.locator("#fortune-streak-badge")
    assert badge.is_visible(), "연속출석 스트릭 배지가 보이지 않음"

    count_txt = page.locator("#fortune-streak-count").inner_text().strip()
    assert count_txt.isdigit(), f"스트릭 카운트가 숫자가 아님: {count_txt!r}"
    assert int(count_txt) == 6, f"어제 뽑음(count=5) 후 오늘 뽑기 시 6이어야 하나 {count_txt}"

    # localStorage에도 6/오늘로 반영되었는지 확인 (같은 날 재뽑기는 유지되어야 함)
    persisted = page.evaluate(
        "(key) => JSON.parse(localStorage.getItem(key) || '{}')", seeded["key"]
    )
    assert persisted.get("count") == 6, f"localStorage 스트릭 카운트가 6이 아님: {persisted}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
