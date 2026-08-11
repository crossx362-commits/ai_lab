"""펫나 E2E — 예측 웰니스 표본부족 배지 흐름 검증.

건강 탭 이상감지 카드(#wellness-anomaly-card, wellness-anomaly.js)에서
z-score 판정 표본이 하한(MIN_SAMPLES 14 + RECENT_DAYS 3 = 17일) 미만일 때
노출되는 '표본 부족 안내 카드'의 배지를 집중 회귀한다.

기존 test_wellness_anomaly_flow.py 가 '억제 여부'(경고 미노출)를 봤다면,
여기서는 배지 자체의 구조를 본다:
  1) 표본 부족 시 안내 카드에 '예측 웰니스' 제목 + 🔮 아이콘 + 임계값 안내(17일)가 뜬다.
  2) 배지가 **실제 표본 수와 목표치**를 표시한다(형식은 '현재 N일'·'N/17일' 등 무관).
     처음엔 리터럴 '현재'를 단언했는데, 그 낱말은 표기의 일부지 기능이 아니다 —
     배지를 우측 진행 표시('N/17일')로 다듬는 개선(미오_20260726135917_1)이 이 단언
     하나에 3회 연속 막혀 보류됐다(2026-07-28에 확인·완화). 주석은 '표기 변경에 견딘다'고
     적혀 있는데 단언은 특정 표기를 못 박고 있었다 — 테스트는 숫자만 본다.
  3) 경계값: 16개(표본<17)면 배지가 뜨고, 17개(표본>=17)면 배지가 사라진다.
  4) 표본 부족 상태에서 경고 카드('평소와 다른 변화가 감지됐어요')는 절대 노출되지 않는다.

외부 네트워크(수파베이스) 성공에 의존하지 않는다 — healthLogs는 localStorage 기반
전역 상태라 window.healthLogs에 시드를 주입한 뒤 renderWellnessCard()로 직접 렌더한다.
로그인 게이팅은 앱이 읽는 localStorage 플래그 주입으로 우회한다(실제 인증 없음).
"""

import json

NAME = "예측 웰니스 표본부족 배지"

_THRESHOLD = 17  # MIN_SAMPLES(14) + RECENT_DAYS(3)

_PET = {
    "id": 990808, "name": "배지견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def _render_with_water_samples(page, n):
    """water 지표 n개만 담은 history를 주입해 카드를 렌더하고 innerText를 반환."""
    return page.evaluate(
        """(n) => {
            const history = Array.from({ length: n }, (_, i) => ({ water: 200 + (i % 3) }));
            window.healthLogs = { today: { date: '2026-07-15' }, history };
            window.renderWellnessCard();
            const host = document.getElementById('wellness-anomaly-card');
            return host ? host.innerText : '';
        }""",
        n,
    )


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 + 건강 탭 활성 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_badge@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    # 건강 탭 렌더 완료 → 이상감지 카드 호스트와 렌더 함수 로딩 확인.
    host = page.wait_for_selector("#wellness-anomaly-card", state="attached", timeout=15000)
    assert host is not None, "이상감지 카드 호스트(#wellness-anomaly-card)가 없음 (건강 탭 렌더 실패 가능)"

    fns_ready = page.evaluate(
        "() => typeof window.renderWellnessCard === 'function'"
    )
    assert fns_ready, "renderWellnessCard 전역 함수가 노출되지 않음"

    # === 1) 표본 10개(<17): 표본 부족 안내 카드 구조 검증 ===
    card10 = _render_with_water_samples(page, 10)
    assert "예측 웰니스" in card10, f"표본 부족 안내 카드 제목이 없음: {card10[:150]!r}"
    assert "🔮" in card10, f"표본 부족 안내 카드에 🔮 아이콘이 없음: {card10[:150]!r}"
    assert str(_THRESHOLD) in card10, \
        f"임계값 안내({_THRESHOLD}일)가 카드에 없음: {card10[:150]!r}"
    # 배지: 실제 표본 수 + 목표치(표기 무관 — '현재 10일'·'10/17일' 모두 허용)
    assert "10" in card10, f"배지에 실제 표본 수(10)가 반영되지 않음: {card10[:150]!r}"
    # 경고 카드는 절대 노출 금지
    assert "평소와 다른 변화가 감지됐어요" not in card10, \
        "표본 부족(N<17)인데 경고 카드가 잘못 노출됨"

    # === 2) 경계값: 16개(<17) → 배지 유지, 표본 수 갱신 ===
    card16 = _render_with_water_samples(page, 16)
    assert "예측 웰니스" in card16 and str(_THRESHOLD) in card16, \
        f"표본 16개(경계 바로 아래)에서 안내 카드가 사라짐: {card16[:150]!r}"
    assert "16" in card16, f"배지가 실제 표본 수(16)를 반영하지 않음: {card16[:150]!r}"

    # === 3) 경계값: 17개(>=17) → 표본 부족 안내 카드 사라짐 ===
    card17 = _render_with_water_samples(page, 17)
    assert "건강 기록이" not in card17, \
        f"표본 17개(임계 도달)인데 표본 부족 안내 문구가 남아 있음: {card17[:150]!r}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
