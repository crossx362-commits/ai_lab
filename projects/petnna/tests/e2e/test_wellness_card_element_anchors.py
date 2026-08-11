"""펫나 E2E — 예측 웰니스 카드 요소/구조 앵커 회귀.

배경(회의 배정 과제): 기존 웰니스 E2E(test_wellness_anomaly_flow / _sample_badge)는
카드의 한국어 카피 문자열('예측 웰니스', '평소와 다른 변화가 감지됐어요', '이상 없음' 등)에
앵커링돼, 정당한 문구 수정마다 오탐으로 실패했다. 이 테스트는 같은 상태 전이를
'카피 문자열'이 아니라 '요소/DOM 구조'로만 단언해 문구 변경에 견디게 한다.

wellness-anomaly.js(renderWellnessCard)는 상태별로 아래 구조를 #wellness-anomaly-card
호스트 안에 렌더한다 — 카피가 바뀌어도 이 골격은 유지된다:
  • 표본 부족  → .card-modern.border-brand-100/60, 진행 배지 span, li 없음
  • 이상 없음  → .card-modern.border-emerald-100, 둥근 배지 span 존재, li 없음
  • 경고 감지  → .card-modern.border-amber-200, 이상 소견 수만큼 <ul><li> 생성

핵심 회귀: 경고 카드의 <li> 개수가 주입한 이상 소견 수에 비례해 늘어난다(카피가 아니라
데이터로 구동되는 요소). 색상 클래스(border-*)는 상태를 구분하는 시맨틱 훅으로만 쓴다.

외부 네트워크(수파베이스)에 의존하지 않는다 — healthLogs는 localStorage 기반 전역
상태라 window.healthLogs에 시드를 주입한 뒤 renderWellnessCard()로 직접 렌더한다.
로그인 게이팅은 앱이 읽는 localStorage 플래그 주입으로 우회한다(실제 인증 없음).
"""

import json

NAME = "예측 웰니스 카드 요소 앵커 회귀"

_PET = {
    "id": 991127, "name": "앵커견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "4살", "weight": "11", "gender": "남아",
    "personality": "차분", "hunger": 70, "happy": 80,
}

# 카드 호스트 안의 상태를 '요소/구조'로만 뽑아내는 인페이지 프로브.
_PROBE = """(logs) => {
    window.healthLogs = logs;
    window.renderWellnessCard();
    const host = document.getElementById('wellness-anomaly-card');
    if (!host) return null;
    const card = host.querySelector('.card-modern');
    return {
        cardCount: host.querySelectorAll('.card-modern').length,
        liCount: host.querySelectorAll('ul li').length,
        hasBadgeSpan: !!host.querySelector('span.rounded-full'),
        hasHeading: !!host.querySelector('h3'),
        cardClass: card ? card.className : '',
    };
}"""


def _render(page, logs):
    result = page.evaluate(_PROBE, logs)
    assert result is not None, "카드 호스트(#wellness-anomaly-card)를 찾지 못함"
    return result


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 + 건강 탭 활성 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_anchor@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    # 건강 탭이 렌더되며 이상감지 카드 호스트가 붙고 렌더 함수가 노출되는지 확인.
    host = page.wait_for_selector("#wellness-anomaly-card", state="attached", timeout=15000)
    assert host is not None, "이상감지 카드 호스트가 없음 (건강 탭 렌더 실패 가능)"
    assert page.evaluate("() => typeof window.renderWellnessCard === 'function'"), \
        "renderWellnessCard 전역 함수가 노출되지 않음"

    # === 1) 표본 부족 상태: brand 색 카드 · 배지 없음 · 소견 li 없음 ===
    # water 표본 10개(<17) + 이상변 없음 → 표본 부족 안내 카드.
    insufficient = _render(page, {
        "today": {"date": "2026-07-15"},
        "history": [{"water": 200 + (i % 3)} for i in range(10)],
    })
    assert insufficient["cardCount"] == 1, \
        f"카드가 정확히 1개가 아님(표본 부족): {insufficient}"
    assert insufficient["hasHeading"], f"표본 부족 카드에 제목 요소(h3)가 없음: {insufficient}"
    assert "border-brand-100" in insufficient["cardClass"], \
        f"표본 부족 상태의 시맨틱 색상 훅(border-brand-100)이 없음: {insufficient['cardClass']!r}"
    assert insufficient["liCount"] == 0, \
        f"표본 부족 카드에 소견 li가 있으면 안 됨: {insufficient}"
    # 배지 span 유무는 더 이상 상태 구분에 쓰지 않는다 — 표본 부족 카드도 진행 배지
    # ('10/17일')를 달게 됐기 때문(미오_20260726135917_1, 2026-07-28 병합).
    # 세 상태는 이미 border-* 시맨틱 훅 + liCount로 완전히 구분되므로 판별력 손실은 없다.
    # (이 단언이 그 개선을 3회 연속 막아 보류로 밀어냈다.)

    # === 2) 이상 없음 상태: emerald 색 카드 · 배지 span 존재 · 소견 li 없음 ===
    # water 표본 20개(>=17)를 전부 동일값으로 → 표준편차 0이라 z 판정 불가 → 이상 없음.
    stable = _render(page, {
        "today": {"date": "2026-07-15"},
        "history": [{"water": 200} for _ in range(20)],
    })
    assert stable["cardCount"] == 1, f"카드가 정확히 1개가 아님(이상 없음): {stable}"
    assert "border-emerald-100" in stable["cardClass"], \
        f"이상 없음 상태의 시맨틱 색상 훅(border-emerald-100)이 없음: {stable['cardClass']!r}"
    assert stable["hasBadgeSpan"], f"이상 없음 카드에 둥근 배지 span이 있어야 함: {stable}"
    assert stable["liCount"] == 0, f"이상 없음 카드에 소견 li가 있으면 안 됨: {stable}"

    # === 3) 경고 상태(소견 1개): amber 색 카드 · li 정확히 1개 ===
    # 3일 연속 'liquid' 배변 → 배변 이상 소견 1건(표본 하한과 무관하게 조기 감지).
    warn1 = _render(page, {
        "today": {"date": "2026-07-15"},
        "history": [
            {"date": "2026-07-15", "poop": "liquid"},
            {"date": "2026-07-14", "poop": "liquid"},
            {"date": "2026-07-13", "poop": "liquid"},
        ],
    })
    assert "border-amber-200" in warn1["cardClass"], \
        f"경고 상태의 시맨틱 색상 훅(border-amber-200)이 없음: {warn1['cardClass']!r}"
    assert warn1["liCount"] == 1, \
        f"소견 1건인데 li가 1개가 아님(요소가 데이터로 구동되지 않음): {warn1}"

    # === 4) 경고 상태(소견 2개): li가 데이터 수에 비례해 2개로 증가 ===
    # 위 배변 이상 + 가장 최근 소변 'red'(혈뇨) → 소견 2건.
    warn2 = _render(page, {
        "today": {"date": "2026-07-15"},
        "history": [
            {"date": "2026-07-15", "poop": "liquid", "urine": "red"},
            {"date": "2026-07-14", "poop": "liquid"},
            {"date": "2026-07-13", "poop": "liquid"},
        ],
    })
    assert "border-amber-200" in warn2["cardClass"], \
        f"경고 상태의 색상 훅(border-amber-200)이 없음: {warn2['cardClass']!r}"
    assert warn2["liCount"] == 2, \
        f"소견 2건인데 li가 2개가 아님(li가 카피가 아니라 요소로 구동되는지 회귀): {warn2}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
