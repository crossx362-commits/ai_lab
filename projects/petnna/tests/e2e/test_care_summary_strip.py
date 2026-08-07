"""펫나 E2E — 오늘의 케어 통합 요약 스트립(여러 데이터원 집계 + 0건 empty-state).

배경(회의 배정 과제 "통합 케어 타임라인 E2E 선행 작성"): 여러 데이터원(산책·급여·
케어·기분·이상신호)의 항목을 한 화면에 모아 보여주고, 기록이 0건일 때의 empty-state를
검증하는 게 목표다. 저장소를 실측한 결과 "각 데이터원을 시간순으로 펼치는 별도 타임라인
컴포넌트"는 아직 존재하지 않고(care-timeline 류 id 없음), 여러 데이터원을 실제로 집계해
렌더하는 실존 컴포넌트는 today-care-card.js의 '오늘의 케어 요약' 스트립(#today-care-strip)
하나다. 계약(실존하는 것만 사용·화면 구조/가시성 위주)에 따라 이 실존 집계 컴포넌트를
대상으로, ID 앵커(#today-care-strip) + 안정적 라벨 텍스트로만 단언한다(클래스 조합 금지).

today-care-card.js(renderTodayCareCard)는 #today-care-strip 안에:
  • 산책(walks) · 급여(meals) · 케어(getTodaySchedules) · 기분(healthLogs.today)
    · 이상신호(wellness 분석) 를 각각 한 칸(button)으로 집계 렌더한다.
  • 각 칸은 오늘 기록이 있으면 값(예: '1회 · 2.5km'), 없으면 empty 마커('아직'/'기록 전')를
    표시한다 — 즉 데이터원별 0건 empty-state가 칸 단위로 드러난다.

외부 네트워크(수파베이스)에 의존하지 않는다 — walks/meals는 암묵적 전역(window),
healthLogs도 전역이라 인페이지에서 시드를 주입한 뒤 renderTodayCareCard()로 직접 렌더한다.
로그인 게이팅과 활성 반려동물은 앱이 읽는 localStorage 키 주입으로 우회한다(실제 인증 없음).
"""

import json

NAME = "오늘의 케어 통합 요약 스트립"

_PET = {
    "id": 770101, "name": "요약견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "차분", "hunger": 70, "happy": 80,
}

# 전역 시드를 주입하고 재렌더한 뒤 스트립 구조를 요소 단위로 읽는 인페이지 프로브.
# seed=None 이면 오늘 기록이 전부 비어 있는(0건) empty-state를 만든다.
_PROBE = """(seed) => {
    if (seed && seed.populate) {
        const now = Date.now();
        window.walks = [{ id: now, distance: '2.5' }];
        window.meals = [{ id: now }];
        window.healthLogs = { today: { activity: 'high' }, history: [] };
    } else {
        window.walks = [];
        window.meals = [];
        window.healthLogs = { today: {}, history: [] };
    }
    if (typeof window.renderTodayCareCard !== 'function') return null;
    window.renderTodayCareCard();
    const host = document.getElementById('today-care-strip');
    if (!host) return null;
    const btns = Array.from(host.querySelectorAll('button'));
    return {
        hidden: !!host.hidden,
        htmlLen: host.innerHTML.length,
        text: host.textContent.replace(/\\s+/g, ' ').trim(),
        buttonCount: btns.length,
        cells: btns.map(b => b.textContent.replace(/\\s+/g, ' ').trim()),
    };
}"""


def _cell_for(res, label):
    """집계 칸(button) 중 주어진 데이터원 라벨을 포함하는 칸 텍스트를 돌려준다."""
    for c in res["cells"]:
        if label in c:
            return c
    return None


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 + 홈(mypet) 탭 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_care@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','mypet');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    # 홈 탭이 렌더되며 통합 요약 스트립 호스트가 붙고 렌더 함수가 노출되는지 확인.
    host = page.wait_for_selector("#today-care-strip", state="attached", timeout=15000)
    assert host is not None, "통합 케어 요약 스트립 호스트(#today-care-strip)가 없음 (홈 탭 렌더 실패 가능)"
    assert page.evaluate("() => typeof window.renderTodayCareCard === 'function'"), \
        "renderTodayCareCard 전역 함수가 노출되지 않음"

    # === 1) 0건 empty-state: 모든 데이터원이 오늘 기록 0 → 칸별 empty 마커 노출 ===
    empty = page.evaluate(_PROBE, None)
    assert empty is not None, "empty 상태 렌더 결과를 읽지 못함"
    assert not empty["hidden"] and empty["htmlLen"] > 0, \
        f"활성 펫이 있는데 스트립이 숨겨짐/비어 있음(가시성 회귀): {empty}"
    assert "오늘의 케어 요약" in empty["text"], \
        f"통합 요약 헤더 텍스트가 없음: {empty['text']!r}"
    # 여러 데이터원이 각각 한 칸으로 집계되는지(산책·급여는 항상, 기분·이상신호도 항상 렌더).
    assert empty["buttonCount"] >= 4, f"집계 칸이 4개 미만(데이터원 집계 누락): {empty}"
    for src in ("산책", "급여", "기분", "이상신호"):
        assert _cell_for(empty, src) is not None, \
            f"데이터원 '{src}' 집계 칸이 없음: {empty['cells']}"
    # 0건 empty-state 마커: 산책·급여는 '아직', 기분은 '기록 전'.
    assert "아직" in _cell_for(empty, "산책"), f"산책 0건 empty 마커('아직')가 없음: {empty['cells']}"
    assert "아직" in _cell_for(empty, "급여"), f"급여 0건 empty 마커('아직')가 없음: {empty['cells']}"
    assert "기록 전" in _cell_for(empty, "기분"), f"기분 미기록 마커('기록 전')가 없음: {empty['cells']}"

    # === 2) 데이터원 항목 집계: 오늘 산책·급여·기분 기록 주입 → 각 칸이 값으로 채워짐 ===
    populated = page.evaluate(_PROBE, {"populate": True})
    assert populated is not None, "populate 상태 렌더 결과를 읽지 못함"
    assert not populated["hidden"] and populated["htmlLen"] > 0, \
        f"populate 상태에서 스트립이 숨겨짐/비어 있음: {populated}"

    walk_cell = _cell_for(populated, "산책")
    assert "km" in walk_cell and "아직" not in walk_cell, \
        f"산책 데이터원 항목이 집계되지 않음(값 대신 empty): {walk_cell!r}"
    meal_cell = _cell_for(populated, "급여")
    assert "회" in meal_cell and "아직" not in meal_cell, \
        f"급여 데이터원 항목이 집계되지 않음(값 대신 empty): {meal_cell!r}"
    mood_cell = _cell_for(populated, "기분")
    assert "활발" in mood_cell, \
        f"기분 데이터원(활력=high) 항목이 집계되지 않음: {mood_cell!r}"
