"""펫나 E2E — 원탭 컨디션 접이식(배변 색·소변 색) + 배변 원탭 단독화 (2026-07-28 회의).

회의 결정 1·2의 게이트:
  1. 배변은 모달이 아니라 원탭 컨디션에서만 입력한다(모달의 4지선다는 순수 중복이라 제거).
  2. 배변 색(4버튼)·소변 색(3버튼)은 기본 접힘 — 단 **항상 보이는 펼침 버튼**이 있어야 한다.

접기는 조용히 기능을 없앨 수 있다. 패널만 숨기고 진입점을 빠뜨리면 입력이 렌더는 되지만
사용자가 영영 도달 못 한다(2026-07-25 설정 탭이 모바일에서 진입점 0이던 사고와 같은 계열).
그래서 '접혀 있다'만 보지 않고 **펼침 버튼이 보이는가 → 눌러서 펼쳐지는가 → 펼친 뒤
입력이 실제로 보이는가**까지 본다.

또 하나: 기록된 값이 있으면 자동으로 펼쳐야 한다. 사용자가 남긴 이상 소견(혈변 등)을
접어서 숨기면 '기록이 사라진 것'과 구분되지 않는다.

셀렉터는 고유 ID만 쓴다 — 클래스 조합 앵커는 카드가 하나 늘면 조용히 깨진다(2026-07-25).
"""
import json

NAME = "원탭 컨디션 접이식·배변 단독화"

_PET = {
    "id": 970742, "name": "접이식", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.4", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


_EMAIL = "e2e_collapse@petna.co.kr"


def _boot(page, base_url, logs=None):
    script = (
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','%s');"
        "localStorage.setItem('petna_active_tab','health');"
        # 주입 데이터가 원격 동기화로 덮이지 않게(2026-07-25 교훈)
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % (_EMAIL, json.dumps(json.dumps([_PET])))
    )
    if logs is not None:
        # 앱은 한 번이라도 뜬 뒤엔 **이메일 접미사 키**를 우선해 읽는다
        # (petna_health_logs_<email>). 기본 키에만 넣으면 앞선 세션이 남긴
        # 접미사 키에 밀려 주입이 조용히 무시된다 — 두 번째 goto부터는 반드시 둘 다 쓸 것.
        payload = json.dumps(json.dumps(logs))
        script += "localStorage.setItem('petna_health_logs', %s);" % payload
        script += "localStorage.setItem('petna_health_logs_%s', %s);" % (_EMAIL, payload)
    page.add_init_script(script)
    page.goto(base_url)
    page.wait_for_selector("#condition-detail-toggle", state="visible", timeout=15000)


def _visible(page, el_id):
    return page.evaluate(
        "(id) => { const e = document.getElementById(id); return !!(e && e.offsetParent !== null); }",
        el_id,
    )


def run(page, base_url):
    # === 1) 기본 접힘 + 펼침 버튼은 항상 보인다 ===
    _boot(page, base_url)

    assert _visible(page, "condition-detail-toggle"), \
        "펼침 버튼이 화면에 없다 — 접이식 패널에 도달할 방법이 사라진다"
    assert not _visible(page, "condition-detail-panel"), \
        "배변 색·소변 색 패널이 기본 접힘이어야 하는데 펼쳐져 있다"

    # 배변·식욕·활력은 접히지 않고 그대로 보여야 한다(매일 쓰는 항목)
    main_visible = page.evaluate(
        "() => {"
        "  const host = document.getElementById('daily-condition-widget');"
        "  if (!host) return 0;"
        "  const panel = document.getElementById('condition-detail-panel');"
        "  return [...host.querySelectorAll('button[onclick^=\"DailyCondition.set\"]')]"
        "    .filter(b => !(panel && panel.contains(b)) && b.offsetParent !== null).length;"
        "}"
    )
    assert main_visible >= 9, \
        f"접이식 밖 원탭 버튼이 {main_visible}개뿐 — 배변(3)·식욕(3)·활력(3)은 항상 보여야 한다"

    # === 2) 눌러서 펼치면 입력이 실제로 보인다 ===
    page.click("#condition-detail-toggle")
    page.wait_for_timeout(400)
    assert _visible(page, "condition-detail-panel"), "펼침 버튼을 눌렀는데 패널이 안 펼쳐졌다"

    detail_inputs = page.evaluate(
        "() => [...document.querySelectorAll('#condition-detail-panel button')]"
        "        .filter(b => b.offsetParent !== null).length"
    )
    assert detail_inputs >= 7, \
        f"펼친 뒤 보이는 입력이 {detail_inputs}개 — 배변 색(4)+소변 색(3)이 다 보여야 한다"

    # 다시 누르면 접힌다
    page.click("#condition-detail-toggle")
    page.wait_for_timeout(400)
    assert not _visible(page, "condition-detail-panel"), "다시 눌렀는데 접히지 않았다"

    # === 3) 배변은 원탭에서 기록되고 새로고침 후에도 남는다(모달 제거 회귀) ===
    page.evaluate("() => DailyCondition.set('poop','hard')")
    page.wait_for_timeout(500)
    assert page.evaluate("() => healthLogs.today.poop") == "hard", "원탭 배변 기록이 저장되지 않았다"

    page.reload()
    page.wait_for_selector("#condition-detail-toggle", state="visible", timeout=15000)
    assert page.evaluate("() => healthLogs.today.poop") == "hard", \
        "새로고침 후 원탭 배변 기록이 사라졌다"

    # 모달에는 배변 4지선다가 남아 있으면 안 된다(중복 제거 확인)
    poop_btns = page.evaluate("() => document.querySelectorAll('.health-poop-btn').length")
    assert poop_btns == 0, f"모달에 배변 버튼이 {poop_btns}개 남아 있다 — 원탭과 중복"

    # === 4) 색 값이 이미 있으면 자동으로 펼쳐진다 ===
    today = page.evaluate("() => healthLogs.today.date")
    logs = {
        "today": {"date": today, "food": 200, "water": 300, "poop": "normal", "poopColor": "red"},
        "history": [{"date": today, "food": 200, "water": 300, "poop": "normal", "poopColor": "red"}],
    }
    _boot(page, base_url, logs)
    assert _visible(page, "condition-detail-panel"), \
        "배변 색이 이미 기록돼 있는데 접혀 있다 — 기록한 이상 소견이 숨겨지면 안 된다"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
