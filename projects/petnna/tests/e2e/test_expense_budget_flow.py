"""펫나 E2E — 반려동물 가계부: 지출 기록 · 월 예산 · 인사이트(회의 PR대기 재검수).

대상: 건강 탭에 삽입되는 지출 가계부 위젯(js/expense-tracker.js,
컨테이너 #expense-tracker-widget, 건강 비용 묶음 카드 #health-cost-group 안).

이 기능이 '단순 지출 로그'와 구별되는 핵심(=회의가 병합 검토를 지시한 부분)은
① 지출 기록 진입점(버튼)이 실제로 도달 가능한가
② 카테고리별 월 예산을 설정하면 지출이 예산을 넘을 때 '예산초과' 배지가 뜨는가
③ 기록이 생기면 '지출 건강점수' 인사이트가 렌더되는가
셋이다. 외부(Supabase) 성공에 의존하지 않는다 — 위젯은 localStorage 전용
(js/expense-tracker.js LS_KEY=petna_expense_records / BUDGET_KEY=petna_expense_budgets).

셀렉터 근거(스크립트 추출): 위젯 진입 버튼은 id가 없어 위젯 컨테이너로 스코프한
텍스트 로케이터로, 모달 내부는 실존 id(#expense-overlay/#expense-cat/#expense-amount/
#expense-date, #budget-overlay/#budget-food)로 고정한다.
"""

import json

NAME = "가계부 지출 기록·월 예산 초과 배지·인사이트"

# 위젯은 펫이 최소 1마리 있어야 렌더된다(renderHealthTab 안에서 호출). demo_mode로
# 주입한 펫이 Supabase 동기화에 덮이지 않게 한다(다른 건강 테스트와 동일 관례).
_PET = {
    "id": 970101, "name": "가계", "breed": "믹스", "type": "dog", "imageUrl": "",
    "age": "3살", "weight": "5", "gender": "남아", "personality": "온순",
    "hunger": 70, "happy": 80,
}


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_expense@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_demo_mode','1');"
        # 결정성 확보 — 이전 기록/예산 잔재 제거
        "localStorage.removeItem('petna_expense_records');"
        "localStorage.removeItem('petna_expense_budgets');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-health", state="attached", timeout=15000)
    page.wait_for_timeout(2500)

    # 건강 탭을 확실히 활성화·재렌더해 위젯 컨테이너가 채워지게 한다.
    page.evaluate(
        "() => { if (typeof switchTab === 'function') switchTab('health');"
        " if (typeof renderHealthTab === 'function') renderHealthTab(); }"
    )
    page.wait_for_selector("#expense-tracker-widget", state="attached", timeout=8000)

    widget = page.locator("#expense-tracker-widget")

    # === A. 진입점 도달성(재검수 핵심) ===
    assert widget.inner_text().find("반려동물 가계부") >= 0, \
        "가계부 위젯 제목이 안 보임 — 위젯이 렌더되지 않음(진입점 부재)"
    record_btn = widget.locator("button", has_text="지출 기록")
    budget_btn = widget.locator("button", has_text="예산 설정")
    record_btn.wait_for(state="visible", timeout=6000)
    budget_btn.wait_for(state="visible", timeout=6000)
    assert record_btn.count() == 1, "‘지출 기록’ 진입 버튼이 유일하지 않음"
    assert budget_btn.count() == 1, "‘예산 설정’ 진입 버튼이 유일하지 않음"

    # 초기(기록 0건)엔 안내 문구가 있어야 한다
    assert "지출 기록이 없어요" in widget.inner_text(), \
        "기록 0건 초기 안내가 없음(초기 상태 회귀)"

    # === B. 지출 기록 모달 열기 → 실존 입력 요소 확인 → 저장 ===
    record_btn.click()
    page.wait_for_selector("#expense-overlay", state="visible", timeout=6000)
    for sel in ("#expense-cat", "#expense-amount", "#expense-date"):
        page.wait_for_selector(sel, state="visible", timeout=4000)

    page.select_option("#expense-cat", "food")
    page.fill("#expense-amount", "30000")
    # 날짜는 오늘(이번 달) 기본값 — 위젯 기본 선택 달과 일치해 집계에 잡힌다.
    page.locator("#expense-overlay button", has_text="저장").click()

    page.wait_for_selector("#expense-overlay", state="detached", timeout=6000)

    # 저장 후 위젯 재렌더: 합계·카테고리 막대·인사이트가 나와야 한다.
    page.wait_for_function(
        "() => { const w = document.getElementById('expense-tracker-widget');"
        " return w && w.innerText.indexOf('30,000') >= 0; }",
        timeout=6000,
    )
    wtext = widget.inner_text()
    assert "합계" in wtext and "30,000" in wtext, f"합계 반영 실패: {wtext[:200]}"
    assert "사료·간식" in wtext, "기록한 카테고리(사료·간식) 막대가 없음"
    assert "지출 건강점수" in wtext, \
        "기록이 생겼는데 '지출 건강점수' 인사이트가 안 뜸 — 인사이트 회귀"

    # === C. 월 예산 설정 → 예산 미만 지출이면 '예산초과' 배지 ===
    budget_btn.click()
    page.wait_for_selector("#budget-overlay", state="visible", timeout=6000)
    page.wait_for_selector("#budget-food", state="visible", timeout=4000)
    # 사료 예산 10,000원 < 지출 30,000원 → 반드시 초과로 판정돼야 한다.
    page.fill("#budget-food", "10000")
    page.locator("#budget-overlay button", has_text="저장").click()
    page.wait_for_selector("#budget-overlay", state="detached", timeout=6000)

    page.wait_for_function(
        "() => { const w = document.getElementById('expense-tracker-widget');"
        " return w && w.innerText.indexOf('예산초과') >= 0; }",
        timeout=6000,
    )
    final_text = widget.inner_text()
    assert "예산초과" in final_text, \
        "예산(1만) < 지출(3만)인데 '예산초과' 배지가 없음 — 예산 판정 회귀"
    # 예산 대비 표기(/ 10,000)도 함께 노출돼야 한다
    assert "10,000" in final_text, "예산 금액(10,000)이 막대에 표기되지 않음"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
