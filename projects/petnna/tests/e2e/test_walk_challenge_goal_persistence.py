"""펫나 E2E — 산책 주간 목표 선택의 결정론적 영속화 검증.

기존 test_walk_challenge_ranking은 목표 버튼 클릭 후 재렌더된 버튼의
`bg-brand-500` 클래스를 폴링해 선택 상태를 확인한다 — 그런데 재렌더는
setWalkChallengeGoal()이 renderWalkChallenge()를 동기 호출하는 구조라,
클래스 문자열 폴링은 타이밍 의존 flaky의 원인이 됐다(무관 diff에 무관
테스트가 게이트를 막은 3회 실패의 진짜 원인).

이 테스트는 같은 흐름을 클래스 문자열이 아니라 **영속 상태(localStorage
petna_walk_challenge_goal)**로 검증한다. setWalkChallengeGoal(n)은
값이 유효할 때 localStorage에 즉시 기록하므로(walk.js), 이 값은 재렌더
타이밍과 무관하게 결정론적이다. 3/5/7회 세 옵션을 순회하며 ①클릭 후 저장
키가 그 값이 되는지 ②선택된 버튼이 정확히 하나이고 그 텍스트가 저장값과
일치하는지 ③재로딩 후에도 마지막 선택이 유지되는지를 확인한다. 앱 내부
샘플·클라이언트 저장만 쓰므로 외부 네트워크 성공에 의존하지 않는다.
"""

NAME = "산책 주간 목표 선택 영속화"

GOAL_KEY = "petna_walk_challenge_goal"


def _enter_walk_tab(page):
    page.locator("header .tab-btn[data-tab='walk']").click()
    page.wait_for_selector("#tab-walk:not(.hidden)", timeout=10000)
    assert page.locator("#tab-walk").is_visible(), "산책 탭으로 전환되지 않음"
    # renderWalkChallenge()가 컨테이너를 실제로 채울 때까지 대기.
    page.wait_for_function(
        "() => { const b = document.getElementById('walk-challenge-section');"
        " return b && b.children.length > 0; }",
        timeout=10000,
    )


def _selected_goal_from_dom(page):
    """선택 스타일(bg-brand-500)을 가진 목표 버튼이 정확히 하나임을 확인하고 그 회수를 반환."""
    return page.evaluate(
        """() => {
            const box = document.getElementById('walk-challenge-section');
            if (!box) return { count: -1, value: null };
            const btns = [...box.querySelectorAll('button')]
                .filter(b => /^\\d+회$/.test(b.textContent.trim()));
            const sel = btns.filter(b => b.className.includes('bg-brand-500'));
            return {
                count: sel.length,
                value: sel.length === 1 ? parseInt(sel[0].textContent, 10) : null,
            };
        }"""
    )


def run(page, base_url):
    js_errors = []
    page.on("pageerror", lambda exc: js_errors.append(str(exc)))

    # 최초 진입(오리진 확보) → 클라이언트 로그인 플래그 주입 → 재로딩.
    page.goto(base_url)
    page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    page.evaluate(
        """() => {
            localStorage.setItem('petna_is_logged_in', 'true');
            localStorage.setItem('petna_user_email', 'e2e@petna.test');
            localStorage.setItem('petna_active_tab', 'mypet');
        }"""
    )
    page.reload()

    page.wait_for_selector("main", state="visible", timeout=15000)
    assert not page.locator("#login-landing-overlay").is_visible(), "로그인 후에도 오버레이가 노출됨"

    _enter_walk_tab(page)
    section = page.locator("#walk-challenge-section")

    # 세 목표 옵션을 순회하며 클릭 → 저장 키와 DOM 선택 상태가 결정론적으로 일치.
    for goal in (3, 7, 5):
        section.get_by_role("button", name=f"{goal}회").first.click()

        # 저장 키는 동기 기록이라 즉시 그 값이어야 한다(폴링으로 재렌더 완료만 흡수).
        page.wait_for_function(
            "(g) => localStorage.getItem('%s') === String(g)" % GOAL_KEY,
            arg=goal,
            timeout=10000,
        )
        stored = page.evaluate("() => localStorage.getItem('%s')" % GOAL_KEY)
        assert stored == str(goal), f"목표 {goal}회 클릭 후 저장값이 {stored}"

        # 재렌더된 DOM에서도 선택 버튼이 정확히 하나이고 저장값과 일치.
        page.wait_for_function(
            "(g) => { const box = document.getElementById('walk-challenge-section');"
            " if (!box) return false;"
            " const sel = [...box.querySelectorAll('button')]"
            "   .filter(b => /^\\d+회$/.test(b.textContent.trim()) && b.className.includes('bg-brand-500'));"
            " return sel.length === 1 && parseInt(sel[0].textContent,10) === g; }",
            arg=goal,
            timeout=10000,
        )
        dom = _selected_goal_from_dom(page)
        assert dom["count"] == 1, f"목표 {goal}회 선택 후 선택 버튼이 {dom['count']}개(정확히 1개여야 함)"
        assert dom["value"] == goal, f"DOM 선택 버튼({dom['value']}회)이 저장값({goal}회)과 불일치"

        # 리더보드 본인 행은 목표 변경 재렌더 후에도 유지되어야 한다.
        assert section.get_by_text("우리 아이 (나)", exact=False).first.is_visible(), \
            f"목표 {goal}회 재렌더 후 리더보드 본인 행이 사라짐"

    # 마지막 선택(5회)이 재로딩 이후에도 유지되어야 한다(영속성).
    page.reload()
    page.wait_for_selector("main", state="visible", timeout=15000)
    _enter_walk_tab(page)
    reloaded = page.evaluate("() => localStorage.getItem('%s')" % GOAL_KEY)
    assert reloaded == "5", f"재로딩 후 목표 저장값이 유지되지 않음(값={reloaded})"
    dom = _selected_goal_from_dom(page)
    assert dom["count"] == 1 and dom["value"] == 5, \
        f"재로딩 후 선택 버튼이 5회로 복원되지 않음(count={dom['count']}, value={dom['value']})"

    assert not js_errors, f"산책 챌린지 목표 영속화 검증 중 미처리 JS 예외 발생: {js_errors[:3]}"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 아무것도 안 하고 exit 0이 된다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
