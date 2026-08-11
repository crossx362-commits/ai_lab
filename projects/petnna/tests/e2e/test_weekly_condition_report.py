"""펫나 E2E — 이번 주 컨디션·급여·활동 요약 카드 렌더 검증.

건강 탭 요약 카드(#weekly-condition-report-card, weekly-condition-report.js)가
기존 healthLogs.history만으로 이번 주(0~6일) 추이를 요약해 렌더하는지 본다.
weekly-report.js(지난주 대비 '변화 조기경보')와 다른 채널 — 이건 '이번 주 요약'이다.

외부 네트워크(수파베이스)에 의존하지 않는다 — healthLogs는 localStorage 기반
전역 상태라 window.healthLogs에 시드를 주입한 뒤 renderWeeklyConditionReport()로
직접 렌더한다. 로그인 게이팅은 앱이 읽는 localStorage 플래그 주입으로 우회한다.
"""

import json

NAME = "이번 주 컨디션 요약 카드"

_PET = {
    "id": 990810, "name": "요약견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def _today_iso(page, offset_days):
    return page.evaluate(
        "(d) => { const t = new Date(); t.setDate(t.getDate()-d);"
        "return t.toISOString().slice(0,10); }",
        offset_days,
    )


def _render(page, history):
    return page.evaluate(
        """(history) => {
            window.healthLogs = { today: {}, history };
            window.renderWeeklyConditionReport();
            const host = document.getElementById('weekly-condition-report-card');
            return host ? host.innerText : '';
        }""",
        history,
    )


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_weekly@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    host = page.wait_for_selector("#weekly-condition-report-card", state="attached", timeout=15000)
    assert host is not None, "요약 카드 호스트(#weekly-condition-report-card)가 없음 (건강 탭 렌더 실패 가능)"

    ready = page.evaluate("() => typeof window.renderWeeklyConditionReport === 'function'")
    assert ready, "renderWeeklyConditionReport 전역 함수가 노출되지 않음"

    # === 1) 기록 없음: 안내(빈 상태) 카드 ===
    empty = _render(page, [])
    assert "이번 주 건강 요약" in empty, f"빈 상태 카드 제목이 없음: {empty[:150]!r}"

    # === 2) 이번 주 기록 3일: 요약 통계 렌더 ===
    d0, d1, d2 = _today_iso(page, 0), _today_iso(page, 2), _today_iso(page, 4)
    history = [
        {"date": d0, "food": 100, "water": 200, "appetite": "good", "activity": "high"},
        {"date": d1, "food": 120, "water": 220, "appetite": "good", "activity": "normal"},
        {"date": d2, "food": 110, "water": 210, "appetite": "normal", "activity": "high"},
    ]
    card = _render(page, history)
    assert "이번 주 건강 요약" in card, f"요약 카드 제목이 없음: {card[:200]!r}"
    assert "3일" in card, f"기록 일수(3일)가 요약에 없음: {card[:200]!r}"
    assert "3/7일" in card, f"진행 배지(3/7일)가 없음: {card[:200]!r}"
    # 평균 급여(110g)·주로 식욕(좋음) 통계가 렌더되는지
    assert "110g" in card, f"평균 급여(110g)가 요약에 없음: {card[:200]!r}"
    assert "좋음" in card, f"주로 식욕(좋음)이 요약에 없음: {card[:200]!r}"

    # === 3) 지난주(7~13일) 기록은 이번 주 요약에서 제외 ===
    old = _today_iso(page, 10)
    only_old = _render(page, [{"date": old, "food": 999, "appetite": "low"}])
    assert "이번 주 건강 요약" in only_old, "빈 상태 카드가 아님"
    assert "999g" not in only_old, "지난주 기록이 이번 주 요약에 잘못 포함됨"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
