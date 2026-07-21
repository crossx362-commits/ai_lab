"""펫나 E2E — 케어위젯 소제목 재그룹화 안전망.

마이펫 탭의 '챌린지·업적 통합카드'(templates/mypet.js의
`.card-modern.divide-y.divide-gray-100`)와 건강 탭의 '케어 위젯' 섹션
라벨(templates/health.js)이 재구성 착수 전 기준선대로 유지되는지 회귀 검증한다.

앞으로 소제목/그룹핑을 재구성할 때 아래 구조가 조용히 깨지는 걸 막는 선행 안전망:
  A) 마이펫: divide-y 통합카드가 정확히 1개(겉박스 하나로 합쳐진 상태)이고,
     챌린지·업적 자식 셀들(weekly-care-challenge 등)이 모두 그 한 박스의
     divide-y 자식으로 들어 있는지(분리된 개별 카드로 흩어지지 않았는지).
  B) 건강: '케어 위젯' 그룹 라벨이 실제로 보이고, 케어 위젯 호스트들
     (preventive-care-dashboard 등)이 그 라벨과 같은 그룹 컨테이너 안에 묶여 있는지.

외부 네트워크(수파베이스)에 의존하지 않는다 — 템플릿 정적 마크업 구조·가시성만
검증한다. 로그인 게이팅은 앱이 읽는 localStorage 플래그를 주입해 우회한다(실제 인증 없음).
"""

import json

NAME = "케어위젯 소제목 재그룹화 안전망"

_PET = {
    "id": 990716, "name": "안전망", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "10", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}

# 마이펫 divide-y 통합카드가 반드시 자기 divide-y 자식으로 품어야 하는 챌린지·업적 셀들
_MYPET_CARD_CHILDREN = [
    "weekly-care-challenge",
    "weekly-walk-challenge",
    "walk-streak-banner",
    "buddy-streak-card",
    "hood-challenge",
    "training-mission-card",
]

# 건강 '케어 위젯' 라벨과 한 그룹으로 묶여야 하는 케어 위젯 호스트들
# (칼로리·식단·병원비는 우측 레일 과밀 해소로 왼쪽 컬럼 이동 — 2026-07-21, 그룹 검증 대상 제외)
_HEALTH_CARE_WIDGETS = [
    "preventive-care-dashboard",
    "med-adherence-tracker",
    "qol-checkin-widget",
    "bcs-wizard-widget",
]

# 왼쪽 컬럼으로 이동한 위젯 — 그룹 밖이어도 반드시 존재·렌더돼야 한다
_MOVED_WIDGETS = ["calorie-tracker-widget", "diet-recommend-widget", "vet-cost-board-widget"]


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 (앱 스크립트보다 먼저 실행, 매 내비게이션마다 재적용).
    # active_tab은 여기서 건드리지 않는다 — 기본값이 'mypet'이라 첫 로드는 마이펫 탭,
    # Part B에서 localStorage로 'health'를 지정한 뒤 reload한다.
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_carewidget@petna.co.kr');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )

    page.goto(base_url)

    # === Part A. 마이펫 divide-y 통합카드 구조 ===
    card = page.wait_for_selector(
        "#tab-mypet .card-modern.divide-y.divide-gray-100",
        state="visible", timeout=15000,
    )
    assert card is not None, "마이펫 탭에 divide-y 통합카드가 렌더되지 않음"

    a = page.evaluate(
        """(ids) => {
            const cards = [...document.querySelectorAll(
                '#tab-mypet .card-modern.divide-y.divide-gray-100')];
            if (cards.length !== 1) return { count: cards.length };
            const box = cards[0];
            const missing = [];
            for (const id of ids) {
                const el = document.getElementById(id);
                if (!el || el.closest('.card-modern.divide-y.divide-gray-100') !== box) {
                    missing.push(id);
                }
            }
            return { count: 1, directChildren: box.children.length, missing };
        }""",
        _MYPET_CARD_CHILDREN,
    )
    assert a["count"] == 1, \
        f"챌린지·업적 통합카드는 divide-y 박스 정확히 1개여야 하나 {a['count']}개 (재그룹화로 흩어짐?)"
    assert a["missing"] == [], \
        f"다음 챌린지·업적 셀이 통합카드 밖으로 분리됨: {a['missing']}"
    assert a["directChildren"] >= len(_MYPET_CARD_CHILDREN), \
        f"통합카드 직계 자식 셀이 {a['directChildren']}개로 기준({len(_MYPET_CARD_CHILDREN)}) 미만 — 소제목/셀 유실 의심"

    # === Part B. 건강 '케어 위젯' 섹션 라벨 + 그룹 묶음 ===
    page.evaluate("() => localStorage.setItem('petna_active_tab','health')")
    page.reload()

    page.wait_for_selector("#tab-health", state="attached", timeout=15000)

    label = page.get_by_text("케어 위젯", exact=True)
    label.wait_for(state="visible", timeout=15000)
    assert label.count() >= 1, "건강 탭에 '케어 위젯' 섹션 라벨이 보이지 않음"

    b = page.evaluate(
        """(ids) => {
            const label = [...document.querySelectorAll('#tab-health span')]
                .find(s => s.textContent.trim() === '케어 위젯');
            if (!label) return { label: false };
            const group = label.closest('.space-y-2');
            if (!group) return { label: true, group: false };
            const missing = [];
            for (const id of ids) {
                const el = document.getElementById(id);
                if (!el || !group.contains(el)) missing.push(id);
            }
            return { label: true, group: true, missing };
        }""",
        _HEALTH_CARE_WIDGETS,
    )
    assert b.get("label"), "'케어 위젯' 라벨 span을 DOM에서 찾지 못함"
    assert b.get("group"), "'케어 위젯' 라벨을 감싸는 그룹 컨테이너(.space-y-2)가 없음"
    assert b["missing"] == [], \
        f"다음 케어 위젯 호스트가 '케어 위젯' 그룹 밖으로 빠짐: {b['missing']}"

    # === Part C. 왼쪽 컬럼으로 이동한 위젯(칼로리·식단·병원비)은 그룹 밖에서도 렌더돼야 한다 ===
    moved = page.evaluate(
        """(ids) => ids.map(id => {
            const el = document.getElementById(id);
            return { id, exists: !!el, filled: el ? el.innerHTML.trim().length > 0 : false };
        })""",
        _MOVED_WIDGETS,
    )
    for m in moved:
        assert m["exists"], f"이동한 위젯 {m['id']}가 DOM에서 사라짐 (재배치 회귀)"
        assert m["filled"], f"이동한 위젯 {m['id']}가 렌더되지 않음 (renderHealthTab 배선 확인)"
