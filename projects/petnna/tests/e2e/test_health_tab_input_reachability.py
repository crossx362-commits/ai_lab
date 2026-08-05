"""펫나 E2E — 데스크톱 건강탭 입력 도달성 앵커 회귀.

건강 탭 상단의 두 입력 카드는 접이식 <details id="health-top-analysis"> 안에 있다:
  - #wellness-anomaly-card  (예측 웰니스 이상감지, wellness-anomaly.js)
  - #weekly-report-card     (주간 건강 리포트, weekly-report.js)

모바일에서는 summary를 눌러 펼치지만, 데스크톱(lg≥1024px)에서는 style.css가
  · details.health-top-analysis > summary { display: none }
  · details.health-top-analysis > .health-top-analysis-body { display: grid !important }
  · details.health-top-analysis::details-content { content-visibility: visible !important }
로 '닫힌 details'의 본문을 강제 노출한다. 이 규칙이 깨지면 데스크톱 사용자가
두 카드를 펼칠 summary도 없이 입력에 아예 도달할 수 없다(렌더는 되지만 숨겨짐).

이 테스트는 '요소가 렌더되었는가'가 아니라 '데스크톱에서 실제로 도달 가능한가'를
검증한다 — 1440x900 뷰포트에서 details를 열지 않은 기본 상태 그대로 두 카드의
offsetParent !== null(가시 레이아웃 트리에 포함)과 폭>0을 단언한다.

외부 네트워크(수파베이스)에 의존하지 않는다. 로그인 게이팅과 활성 탭은 앱이 읽는
localStorage 플래그를 주입해 우회한다(실제 인증 없음).
"""

import json

NAME = "데스크톱 건강탭 입력 도달성 앵커"

_PET = {
    "id": 880808, "name": "도달견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "4살", "weight": "10", "gender": "여아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def run(page, base_url):
    # 데스크톱 뷰포트를 goto 전에 고정 — lg(≥1024px) 미디어쿼리가 걸려야
    # 닫힌 details 본문 강제 노출 규칙이 적용된다.
    page.set_viewport_size({"width": 1440, "height": 900})

    # 로그인 우회 + 활성 반려동물 주입 + 건강 탭 활성 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_reach@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % json.dumps([_PET])
    )

    page.goto(base_url)

    # 건강 탭 템플릿 렌더 완료 → 두 입력 카드 호스트가 DOM에 붙을 때까지 대기.
    page.wait_for_selector("#wellness-anomaly-card", state="attached", timeout=15000)
    page.wait_for_selector("#weekly-report-card", state="attached", timeout=15000)

    # 측정 신뢰성: 뷰포트가 실제로 데스크톱 폭인지 확인(innerWidth 0이면 측정 불신).
    inner_w = page.evaluate("() => window.innerWidth")
    assert inner_w == 1440, f"데스크톱 뷰포트(1440px)가 적용되지 않음: innerWidth={inner_w}"

    # 건강 탭이 실제로 활성(숨김 아님)인지 — 탭 컨테이너 자체가 도달 가능해야 한다.
    tab_visible = page.evaluate(
        "() => { const t = document.getElementById('tab-health');"
        " return !!t && t.offsetParent !== null; }"
    )
    assert tab_visible, "건강 탭(#tab-health)이 활성 상태가 아님 (offsetParent=null)"

    # 핵심: 두 입력 카드가 데스크톱에서 도달 가능한가.
    # details를 열지 않은 기본 상태 그대로 — CSS 강제 노출이 작동해야만 통과한다.
    reach = page.evaluate(
        """() => {
            const details = document.getElementById('health-top-analysis');
            const pick = id => {
                const el = document.getElementById(id);
                if (!el) return { exists: false };
                const r = el.getBoundingClientRect();
                return {
                    exists: true,
                    reachable: el.offsetParent !== null,
                    width: Math.round(r.width),
                };
            };
            return {
                detailsOpen: !!(details && details.open),
                wellness: pick('wellness-anomaly-card'),
                weekly: pick('weekly-report-card'),
            };
        }"""
    )

    w = reach["wellness"]
    wr = reach["weekly"]
    assert w["exists"], "이상감지 카드 호스트(#wellness-anomaly-card)가 없음"
    assert wr["exists"], "주간 리포트 카드 호스트(#weekly-report-card)가 없음"

    assert w["reachable"], (
        "데스크톱에서 #wellness-anomaly-card가 도달 불가(offsetParent=null) — "
        "닫힌 details 본문 강제 노출 CSS가 깨졌을 수 있음 "
        f"(details.open={reach['detailsOpen']})"
    )
    assert wr["reachable"], (
        "데스크톱에서 #weekly-report-card가 도달 불가(offsetParent=null) — "
        "닫힌 details 본문 강제 노출 CSS가 깨졌을 수 있음 "
        f"(details.open={reach['detailsOpen']})"
    )

    # 도달성 보강: 실제 레이아웃 폭이 잡혀야 사용자가 볼 수 있는 입력이다.
    assert w["width"] > 0, f"#wellness-anomaly-card 레이아웃 폭이 0: {w}"
    assert wr["width"] > 0, f"#weekly-report-card 레이아웃 폭이 0: {wr}"
