"""펫나 E2E — 건강 탭이 '활성 펫'을 따르는지 + 무펫 empty state (2026-07-26 2차 회의).

발견 경위: 회의에서 테오가 `health.js`의 물 섭취 목표가 `getActivePet()`이 아니라
`pets[0]`을 읽는 것을 지목했다. 다중 펫 사용자가 펫을 바꿔도 물 목표가 첫 번째 펫
체중에 고정된다 — 펫이 하나뿐인 환경에서는 절대 드러나지 않아 QA·E2E를 통째로
빠져나가는 유형이다.

같은 회의에서 '펫 0마리일 때 건강 탭이 `--g`·`0회` 카드만 늘어놓고 등록 유도가
없다'는 지적도 나와 empty state를 넣었다. 둘 다 여기서 함께 지킨다.

주의: `pets[0]`이 전부 나쁜 건 아니다. 대부분의 위젯은
`getActivePet가 있으면 그걸, 없으면 pets[0]` 형태의 **정상 폴백**이다.
문제는 getActivePet을 아예 안 보는 단독 사용이었다.
"""

NAME = "건강 탭 활성 펫 반영·무펫 안내"

# 체중이 뚜렷하게 다른 두 마리 — 물 목표(50ml/kg)가 확실히 갈리게 한다
_PETS = [
    {"id": 960001, "name": "가벼움", "breed": "믹스", "type": "dog", "imageUrl": "",
     "age": "3살", "weight": "4", "gender": "남아", "personality": "온순",
     "hunger": 70, "happy": 80},
    {"id": 960002, "name": "무거움", "breed": "믹스", "type": "dog", "imageUrl": "",
     "age": "5살", "weight": "12", "gender": "여아", "personality": "활발",
     "hunger": 70, "happy": 80},
]


def run(page, base_url):
    import json

    # petna_demo_mode를 켜야 주입한 펫이 살아남는다 — 이걸 안 켜면 Supabase
    # 동기화(syncPets)가 로컬 상태를 DB 펫으로 통째로 덮어써서, 여기서 만든
    # 체중 4kg/12kg 대신 실제 DB 펫이 들어온다(2026-07-26 이 테스트 작성 중 실제로
    # 겪음 — 체중이 'ㅇ'인 DB 펫이 들어와 NaN이 났다).
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_activepet@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps(_PETS))
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-health", state="attached", timeout=15000)
    page.wait_for_timeout(2500)

    # === A. 펫을 바꾸면 물 목표가 그 펫 체중을 따라야 한다 ===
    goals = page.evaluate(
        """(pets) => {
            const out = [];
            for (let i = 0; i < pets.length; i++) {
                if (typeof setActivePet === 'function') setActivePet(i);
                else if (typeof AppStore !== 'undefined') AppStore.setState('activePetIndex', i);
                if (typeof renderHealthTab === 'function') renderHealthTab();
                if (typeof switchMealTab === 'function') switchMealTab('water');
                const el = document.getElementById('water-goal-max');
                const active = (typeof getActivePet === 'function') ? getActivePet() : null;
                out.push({
                    name: active ? active.name : null,
                    weight: active ? parseFloat(active.weight) : null,
                    goal: parseInt((el?.textContent || '').replace(/[^0-9]/g, ''), 10) || null,
                });
            }
            return out;
        }""",
        _PETS,
    )
    assert len(goals) == len(_PETS), f"펫 전환 측정 실패: {goals}"
    for g in goals:
        assert g["name"], f"활성 펫을 못 읽음: {g}"
        expected = round(g["weight"] * 50)
        assert g["goal"] == expected, (
            f"'{g['name']}'(체중 {g['weight']}kg) 물 목표가 {g['goal']}ml — "
            f"{expected}ml이어야 한다. health.js가 활성 펫 대신 pets[0]을 보는 회귀."
        )
    # 두 펫의 목표가 실제로 달라야 한다(둘 다 같으면 고정된 것)
    assert goals[0]["goal"] != goals[1]["goal"], \
        f"펫을 바꿨는데 물 목표가 그대로 {goals[0]['goal']}ml — pets[0] 고정 회귀"

    # === B. 펫이 0마리면 등록 유도만 보이고 위젯 본문은 숨는다 ===
    empty = page.evaluate(
        """() => {
            const keep = JSON.parse(JSON.stringify(AppStore.getState('pets') || []));
            AppStore.setState('pets', []);
            if (typeof renderHealthTab === 'function') renderHealthTab();
            const es = document.getElementById('health-empty-state');
            const gr = document.getElementById('health-main-grid');
            const r = {
                hasEmpty: !!es, hasGrid: !!gr,
                emptyShown: !!es && !es.classList.contains('hidden'),
                gridHidden: !!gr && gr.classList.contains('hidden'),
                cta: !!es && es.querySelectorAll('button').length > 0,
            };
            AppStore.setState('pets', keep);
            if (typeof renderHealthTab === 'function') renderHealthTab();
            r.restoredGrid = !!gr && !gr.classList.contains('hidden');
            r.restoredEmptyHidden = !!es && es.classList.contains('hidden');
            return r;
        }"""
    )
    assert empty["hasEmpty"] and empty["hasGrid"], \
        "건강 탭에 #health-empty-state / #health-main-grid가 없음(구조 회귀)"
    assert empty["emptyShown"], "펫 0마리인데 등록 유도가 안 보임"
    assert empty["gridHidden"], "펫 0마리인데 위젯 본문이 그대로 노출됨(--g·0회 카드 문제)"
    assert empty["cta"], "등록 유도에 이동 버튼이 없음"
    assert empty["restoredGrid"] and empty["restoredEmptyHidden"], \
        "펫이 다시 생겼는데 본문이 복구되지 않음 — empty state가 영구히 남는 회귀"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
