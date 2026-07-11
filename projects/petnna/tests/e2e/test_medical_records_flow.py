"""펫나 E2E — 건강수첩 방문 기록 추가·월간 병원비 요약 조회 검증.

로그인된 상태(로컬스토리지 주입)에서 건강 탭의 건강수첩 카드로 진입해
'기록 추가' 모달을 열어 방문 기록(병원명·진료비)을 저장하고,
타임라인에 기록과 '누적 진료비' 요약이 렌더되는지 확인한다.
이어서 월간 병원비 요약 함수(getMonthlyHospitalSummary)가 방금 추가한
진료비를 집계하는지 검증한다.

외부 네트워크(수파베이스) 성공에 의존하지 않는다 — 건강수첩은 localStorage
우선 저장이라 Supabase 동기화 실패와 무관하게 화면 구조가 갱신된다.
로그인 게이팅은 앱이 읽는 localStorage 플래그(petna_is_logged_in)를
주입해 우회한다(실제 인증 호출 없음).
"""

import json

NAME = "건강수첩 방문 기록 추가·월간 병원비 요약"

_VISIT_DATE = "2026-07-05"          # yyyymm='2026-07'로 월간 요약 대상
_MONTH = "2026-07"
_HOSPITAL = "테스트동물병원"
_COST = 35000

_PET = {
    "id": 990101, "name": "테스트견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "2살", "weight": "10", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def run(page, base_url):
    # 로그인 게이팅 우회 + 활성 반려동물 1마리 주입 (goto 이전, 앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_medical@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"  # 건강수첩이 건강 탭으로 이동(2026-07-11 재배치)
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )

    page.goto(base_url)

    # 로그인 우회가 먹혀 메인/건강수첩 카드가 렌더되어야 한다.
    add_btn = page.wait_for_selector(
        "button[onclick=\"openMedicalRecordModal()\"]",
        state="visible", timeout=15000,
    )
    assert add_btn is not None, "건강수첩 '기록 추가' 버튼이 보이지 않음 (로그인 우회 실패 가능)"

    # 기록 추가 전 타임라인은 빈 상태 안내를 노출한다.
    timeline = page.wait_for_selector("#medical-records-timeline", state="attached", timeout=5000)
    assert "아직 건강 기록이 없어요" in (timeline.inner_text() or ""), \
        "초기 건강수첩 타임라인이 빈 상태 안내를 보여주지 않음"

    # === 방문 기록 추가 모달 열기 ===
    add_btn.click()
    modal = page.wait_for_selector("#medical-record-modal", state="visible", timeout=5000)
    assert modal is not None, "건강수첩 기록 추가 모달이 열리지 않음"

    # 폼 입력 (방문일·병원명·진료비)
    page.fill("#medical-visit-date", _VISIT_DATE)
    page.fill("#medical-hospital", _HOSPITAL)
    page.fill("#medical-cost", str(_COST))

    # 저장
    page.click("#medical-record-modal button[onclick=\"saveMedicalRecord()\"]")

    # 저장 후 모달이 닫혀야 한다.
    page.wait_for_selector("#medical-record-modal", state="hidden", timeout=5000)

    # === 타임라인에 방금 기록과 요약이 반영되어야 한다 ===
    cost_str = f"{_COST:,}"   # 35,000
    page.wait_for_function(
        "([hosp, cost]) => {"
        "  const el = document.getElementById('medical-records-timeline');"
        "  if (!el) return false;"
        "  const t = el.innerText || '';"
        "  return t.includes(hosp) && t.includes(cost) && t.includes('누적 진료비');"
        "}",
        arg=[_HOSPITAL, cost_str],
        timeout=8000,
    )
    tl_text = page.locator("#medical-records-timeline").inner_text()
    assert _HOSPITAL in tl_text, "타임라인에 저장한 병원명이 없음"
    assert cost_str in tl_text, "타임라인에 저장한 진료비가 없음"
    assert "누적 진료비" in tl_text, "누적 진료비 요약 카드가 렌더되지 않음"
    assert "1건" in tl_text, "기록 건수 요약(1건)이 표시되지 않음"

    # === 월간 병원비 요약 함수가 방금 진료비를 집계해야 한다 ===
    summary = page.evaluate(
        "(m) => (typeof getMonthlyHospitalSummary === 'function')"
        " ? getMonthlyHospitalSummary(m) : null",
        _MONTH,
    )
    assert summary is not None, "getMonthlyHospitalSummary 함수를 찾을 수 없음"
    assert summary.get("count", 0) >= 1, f"월간 병원 방문 건수 집계 오류: {summary}"
    assert float(summary.get("totalCost", 0)) >= _COST, f"월간 진료비 합계 집계 오류: {summary}"
