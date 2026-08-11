"""펫나 E2E — 원탭 배변 기록·저장·건강카드 접이식 (2026-07-28 회의 배정 과제).

건강 탭 '오늘의 기록' 카드 안 '원탭 컨디션' 위젯(daily-condition.js)에서
배변·배변색을 원탭 이모지로 기록하면 healthLogs.today에 즉시 저장돼
wellness-anomaly 이상감지의 입력이 된다. 또 긴 건강 탭의 각 카드는
section-collapse.js가 헤더 바를 씌워 클릭으로 접고(펼침/접힘) 그 상태를
localStorage(petna_collapsed_sections)에 남긴다.

검증 결정(게이트):
  결정1 — 원탭 기록: 배변·배변색 버튼을 누르면 그 버튼이 선택(aria-pressed=true)되고
          healthLogs.today에 값이 담긴다. 새로고침해도 선택과 값이 유지된다.
  결정2 — 접이식: 위젯을 품은 카드의 헤더를 누르면 본문이 접히고(hidden,
          aria-expanded=false), 다시 누르면 펼쳐진다.

셀렉터는 고유 ID/데이터-속성만 쓴다 — 2026-07-25 교훈(클래스 조합 앵커는
카드가 하나 늘면 조용히 깨진다). 원탭 버튼은 위젯 고유 host(#daily-condition-widget)
아래에서 onclick 속성으로 정확히 지목하고, 접이식은 data-collapse-* 속성으로 잡는다.
외부 네트워크(수파베이스) 성공에 의존하지 않는다 — 로컬 상태·화면 구조만 본다.
"""

NAME = "원탭 배변 기록·건강카드 접이식"

_EMAIL = "e2e_onetap@petna.co.kr"

_PET = {
    "id": 970728, "name": "원탭", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.2", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}

# host 고유 id 아래에서 onclick 속성으로 정확히 지목(클래스 조합 금지).
_WIDGET = "#daily-condition-widget"
_POOP_BTN = _WIDGET + " button[onclick=\"DailyCondition.set('poop','normal')\"]"
_COLOR_BTN = _WIDGET + " button[onclick=\"DailyCondition.set('poopColor','black')\"]"


def run(page, base_url):
    import json

    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email',%s);"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %s);"
        % (json.dumps(_EMAIL), json.dumps(json.dumps([_PET])))
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-health", state="attached", timeout=15000)
    page.wait_for_timeout(2500)   # 위젯 렌더 대기(1회 허용)

    # 원탭 위젯이 실제로 배변 버튼을 그렸는지 확인 — 없으면 배선 회귀.
    page.wait_for_selector(_POOP_BTN, state="visible", timeout=15000)
    page.wait_for_selector(_COLOR_BTN, state="attached", timeout=15000)

    # ── 결정1: 원탭 기록 → 선택·저장 ──────────────────────────────
    page.click(_POOP_BTN)
    # 배변 색은 2026-07-28 회의 결정으로 기본 접힘이 됐다(이상 있을 때만 보는 항목).
    # 펼침 버튼을 먼저 눌러야 클릭할 수 있다 — 이 버튼이 사라지면 그 입력은
    # 도달 불가가 되므로, 여기서 못 찾고 실패하는 것 자체가 도달성 회귀 신호다.
    page.wait_for_selector("#condition-detail-toggle", state="visible", timeout=10000)
    if not page.evaluate(
        "() => { const p = document.getElementById('condition-detail-panel');"
        "        return !!(p && p.offsetParent !== null); }"
    ):
        page.click("#condition-detail-toggle")
        page.wait_for_selector(_COLOR_BTN, state="visible", timeout=10000)
    page.click(_COLOR_BTN)

    # 위젯 재렌더 후 두 버튼이 선택 상태로 그려져야 한다.
    page.wait_for_selector(_POOP_BTN + "[aria-pressed='true']", timeout=10000)
    page.wait_for_selector(_COLOR_BTN + "[aria-pressed='true']", timeout=10000)

    saved = page.evaluate(
        "() => (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today)"
        " ? { poop: healthLogs.today.poop, color: healthLogs.today.poopColor } : null"
    )
    assert saved is not None, "healthLogs.today가 없음 — 원탭 저장 대상 상태가 초기화되지 않음"
    assert saved["poop"] == "normal", \
        f"원탭 배변이 healthLogs.today.poop에 안 담김: {saved['poop']!r}"
    assert saved["color"] == "black", \
        f"원탭 배변색이 healthLogs.today.poopColor에 안 담김: {saved['color']!r}"

    # ── 결정1(계속): 새로고침 생존 ────────────────────────────────
    page.reload()
    page.wait_for_selector("#tab-health", state="attached", timeout=15000)
    page.wait_for_selector(_POOP_BTN, state="visible", timeout=15000)

    reloaded = page.evaluate(
        "() => (typeof healthLogs !== 'undefined' && healthLogs && healthLogs.today)"
        " ? { poop: healthLogs.today.poop, color: healthLogs.today.poopColor } : null"
    )
    assert reloaded is not None and reloaded["poop"] == "normal" and reloaded["color"] == "black", \
        f"새로고침 후 원탭 기록이 유실됨: {reloaded!r} (localStorage 저장/복원 회귀)"
    # 복원된 상태가 버튼 선택으로도 반영돼야 한다.
    page.wait_for_selector(_POOP_BTN + "[aria-pressed='true']", timeout=10000)
    page.wait_for_selector(_COLOR_BTN + "[aria-pressed='true']", timeout=10000)

    # ── 결정2: 건강카드 접이식(펼침/접힘) ──────────────────────────
    # 위젯을 품은 카드가 section-collapse로 감싸질 때까지 대기(setTimeout(0)).
    page.wait_for_function(
        "() => !!(document.getElementById('daily-condition-widget')"
        " && document.getElementById('daily-condition-widget').closest('[data-collapse-key]'))",
        timeout=10000,
    )

    def _fold_state():
        return page.evaluate(
            "() => {"
            "  const w = document.getElementById('daily-condition-widget');"
            "  const sec = w && w.closest('[data-collapse-key]');"
            "  if (!sec) return null;"
            "  const body = sec.querySelector('[data-collapse-body]');"
            "  return {"
            "    expanded: sec.getAttribute('aria-expanded'),"
            "    hidden: body ? !!body.hidden : null,"
            "    widgetVisible: !!(w.offsetWidth || w.offsetHeight || w.getClientRects().length)"
            "  };"
            "}"
        )

    before = _fold_state()
    assert before is not None, "위젯이 접이식 섹션(data-collapse-key)으로 감싸지지 않음"
    assert before["expanded"] == "true" and before["hidden"] is False, \
        f"초기 상태는 펼침이어야 함: {before}"

    # 우리 위젯을 품은 바로 그 섹션의 헤더만 지목(다른 카드의 접기 헤더와 섞이지 않게).
    header = "[data-collapse-key]:has(#daily-condition-widget) [data-collapse-btn]"

    # 헤더 클릭 → 접힘
    page.click(header)
    collapsed = _fold_state()
    assert collapsed["expanded"] == "false" and collapsed["hidden"] is True, \
        f"헤더 클릭 후 접혀야 함(aria-expanded=false, body hidden): {collapsed}"
    assert collapsed["widgetVisible"] is False, "접힌 뒤에도 원탭 위젯이 보임 — 접기 미적용"

    # 다시 클릭 → 펼침
    page.click(header)
    expanded = _fold_state()
    assert expanded["expanded"] == "true" and expanded["hidden"] is False, \
        f"재클릭 후 다시 펼쳐져야 함: {expanded}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
