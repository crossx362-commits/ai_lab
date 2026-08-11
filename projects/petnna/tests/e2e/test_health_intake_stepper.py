"""펫나 E2E — 식사·음수 인라인 스테퍼 (2026-07-28 회의 결정 3).

값 표시만 하던 타일이 −/+ 로 입력을 겸한다. 회의가 못 박은 조건 두 개를 지킨다:
  · 저장 형식·경로는 모달과 같아야 한다(healthLogs.today → history). 여기서 형식이
    갈라지면 wellness-anomaly·주간리포트가 읽는 이력이 둘로 나뉜다(백호 지적).
  · 연타 저장이 원격 동기화와 경합하지 않게 디바운스한다 — 다만 디바운스가 데이터를
    삼키면 안 되므로 **결국 반영되는지**를 본다(2026-07-25 sync 되감기 교훈).

정확한 수치 입력은 값 자체를 눌러 모달로 간다(모달은 마이펫·퀘스트도 부르므로 존치).
셀렉터는 고유 ID만 쓴다(2026-07-25 클래스 앵커 붕괴 교훈).
"""
import json

NAME = "식사·음수 인라인 스테퍼"

_PET = {
    "id": 970743, "name": "스테퍼", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.4", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_stepper@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_selector("#food-step-plus", state="visible", timeout=15000)

    for el in ("food-step-minus", "food-step-plus", "water-step-minus", "water-step-plus",
               "health-today-food", "health-today-water"):
        assert page.evaluate(
            "(id) => { const e = document.getElementById(id); return !!(e && e.offsetParent !== null); }", el
        ), f"#{el} 이 보이지 않는다 — 인라인 입력에 도달할 수 없다"

    # === 증가: 화면은 즉시, 저장은 디바운스 후 ===
    page.evaluate("() => { healthLogs.today = { date: new Date().toISOString().split('T')[0] }; }")
    page.click("#food-step-plus")
    page.click("#food-step-plus")
    page.click("#water-step-plus")
    page.wait_for_timeout(200)

    assert page.evaluate("() => document.getElementById('health-today-food').textContent") == "20g", \
        "타일 표시가 즉시 갱신되지 않았다(디바운스는 저장에만 걸려야 한다)"

    page.wait_for_timeout(1400)   # 디바운스(700ms) 통과
    state = page.evaluate(
        "() => ({ food: healthLogs.today.food, water: healthLogs.today.water,"
        "         hist: (healthLogs.history || []).find(h => h.date === healthLogs.today.date) || null })"
    )
    assert state["food"] == 20 and state["water"] == 50, \
        f"스테퍼 값이 healthLogs.today에 안 들어갔다: {state}"
    assert state["hist"] and state["hist"]["food"] == 20, \
        f"history에 반영되지 않았다 — 모달과 같은 저장 경로(saveHealthHistoryToday)를 써야 한다: {state}"

    # === 감소 + 0 미만으로 내려가지 않는다 ===
    for _ in range(4):
        page.click("#food-step-minus")
    page.wait_for_timeout(1400)
    assert page.evaluate("() => healthLogs.today.food") == 0, \
        "0 아래로 내려갔거나 감소가 반영되지 않았다"

    # === 새로고침 후에도 남는다 ===
    page.click("#water-step-plus")
    page.wait_for_timeout(1400)
    before = page.evaluate("() => healthLogs.today.water")
    page.reload()
    page.wait_for_selector("#food-step-plus", state="visible", timeout=15000)
    assert page.evaluate("() => healthLogs.today.water") == before, \
        "새로고침 후 스테퍼로 넣은 음수량이 사라졌다"

    # === 값을 누르면 정확 입력 모달이 열린다(모달 존치 확인) ===
    page.click("#health-today-food")
    page.wait_for_timeout(500)
    assert page.evaluate(
        "() => { const m = document.getElementById('health-log-modal');"
        "        return !!m && !m.classList.contains('hidden'); }"
    ), "값을 눌렀는데 정확 입력 모달이 열리지 않았다"
    # 모달이 오늘 값을 그대로 들고 있어야 한다(스테퍼 ↔ 모달 왕복 시 값 유실 방지)
    assert page.evaluate("() => parseInt(document.getElementById('water-amount-slider').value,10)") == before, \
        "모달 슬라이더가 스테퍼로 넣은 값을 못 받았다"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
