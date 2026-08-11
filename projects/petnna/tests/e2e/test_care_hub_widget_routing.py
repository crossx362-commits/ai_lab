"""펫나 E2E — 케어 허브(케어 위젯 그룹) 버튼 도달성·타깃 위젯 스크롤 도착 회귀.

배경(회의 배정 과제): 건강 탭 케어 허브(#care-widgets-group)에는 각 케어 위젯이
자기 액션 버튼으로 모달을 여는데(병원비 '금액 제보'→VetCostBoard.open,
QOL '이번 주 체크인'→QolCheckin.open), 이 버튼들이 실제로 화면에 도달 가능한지·
클릭이 대상 모달로 라우팅되는지·위젯이 탭 안에서 스크롤로 도달되는지를 확인하는
E2E가 없었다("현재 라우팅 도달성 검증 공백"). 이 테스트가 그 공백을 메운다.

검증 골격(카피가 아니라 DOM 구조·라우팅 결과로만 단언):
  • 케어 허브 컨테이너(#care-widgets-group)와 두 위젯 호스트가 건강 탭에 렌더된다.
  • 두 위젯 호스트를 스크롤로 뷰포트에 도달시킬 수 있다(앵커 스크롤 도착).
  • 병원비 위젯의 VetCostBoard.open 버튼 클릭 → #vetcost-overlay 모달이 뜬다(라우팅).
  • QOL 위젯의 QolCheckin.open 버튼 클릭 → #qol-overlay 모달이 뜬다(라우팅).
  • 두 모달의 close 라우트가 실제로 오버레이를 제거한다.

외부 네트워크(수파베이스)에 의존하지 않는다 — 케어 위젯은 localStorage 기반이라
로그인 게이팅은 앱이 읽는 플래그 주입으로 우회하고(실제 인증 없음), 활성 반려동물을
petna_pets로 주입해 QOL 위젯(활성 펫 필요·첫 주라 체크인 due)이 버튼을 렌더하게 한다.
"""

import json

NAME = "케어 허브 위젯 버튼 도달성·스크롤 도착"

_PET = {
    "id": 880314, "name": "허브견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "5살", "weight": "9", "gender": "여아",
    "personality": "온순", "hunger": 60, "happy": 75,
}

# 요소를 뷰포트로 스크롤한 뒤 실제로 세로 방향으로 뷰포트와 겹치는지(=도착)를 반환.
_SCROLL_ARRIVE = """(sel) => {
    const el = document.querySelector(sel);
    if (!el) return { found: false };
    el.scrollIntoView({ block: 'center' });
    const r = el.getBoundingClientRect();
    const vh = window.innerHeight || document.documentElement.clientHeight;
    return {
        found: true,
        // 요소가 세로로 뷰포트 안에 겹쳐 있으면 스크롤 도착으로 본다.
        inViewport: r.bottom > 0 && r.top < vh,
        top: Math.round(r.top), bottom: Math.round(r.bottom), vh: Math.round(vh),
    };
}"""


def _scroll_and_assert(page, sel):
    res = page.evaluate(_SCROLL_ARRIVE, sel)
    assert res.get("found"), f"위젯 호스트를 찾지 못함(케어 허브 렌더 실패 가능): {sel}"
    assert res.get("inViewport"), \
        f"스크롤해도 위젯이 뷰포트에 도달하지 못함({sel}): {res}"


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 + 건강 탭 활성 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_carehub@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    # 초기 SPA 렌더 대기(1회 허용) — 건강 탭 템플릿 주입 + 위젯 렌더가 끝나길 기다린다.
    page.wait_for_timeout(2500)

    # === 1) 케어 허브 컨테이너·위젯 호스트가 건강 탭에 붙는다 ===
    page.wait_for_selector("#care-widgets-group", state="attached", timeout=15000)
    page.wait_for_selector("#vet-cost-board-widget", state="attached", timeout=15000)
    page.wait_for_selector("#qol-checkin-widget", state="attached", timeout=15000)

    # 라우팅 대상 전역 API가 노출됐는지(버튼 onclick이 부르는 핸들러) 확인.
    for expr in (
        "typeof window.VetCostBoard === 'object' && typeof window.VetCostBoard.open === 'function'",
        "typeof window.VetCostBoard.close === 'function'",
        "typeof window.QolCheckin === 'object' && typeof window.QolCheckin.open === 'function'",
        "typeof window.QolCheckin.close === 'function'",
    ):
        assert page.evaluate("() => " + expr), f"케어 허브 라우팅 API 미노출: {expr}"

    # === 2) 병원비 위젯: 스크롤 도착 → 버튼 클릭 → 모달 라우팅 ===
    _scroll_and_assert(page, "#vet-cost-board-widget")

    vet_btn = page.locator("#vet-cost-board-widget button[onclick*='VetCostBoard.open']")
    assert vet_btn.count() >= 1, "병원비 위젯에 VetCostBoard.open 버튼이 없음(도달 불가)"
    vet_btn.first.click()
    page.wait_for_selector("#vetcost-overlay", state="visible", timeout=8000)

    # close 라우트가 실제로 오버레이를 걷어내는지 확인(모달 라이프사이클 도달성).
    page.evaluate("() => window.VetCostBoard.close()")
    page.wait_for_selector("#vetcost-overlay", state="detached", timeout=5000)

    # === 3) QOL 위젯: 스크롤 도착 → 버튼 클릭 → 모달 라우팅 ===
    _scroll_and_assert(page, "#qol-checkin-widget")

    qol_btn = page.locator("#qol-checkin-widget button[onclick*='QolCheckin.open']")
    assert qol_btn.count() >= 1, \
        "QOL 위젯에 QolCheckin.open 버튼이 없음(활성 펫 주입 실패 또는 도달 불가)"
    qol_btn.first.click()
    page.wait_for_selector("#qol-overlay", state="visible", timeout=8000)

    page.evaluate("() => window.QolCheckin.close()")
    page.wait_for_selector("#qol-overlay", state="detached", timeout=5000)


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
