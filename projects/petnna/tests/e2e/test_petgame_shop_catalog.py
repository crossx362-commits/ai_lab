"""펫나 E2E — 펫게임 상점이 먹이·아이템 카탈로그를 빠짐없이 렌더하는지 (회의 배정).

배경: game-items.js의 카탈로그(FOODS·ITEMS)가 확장됐는데(먹이 9종·아이템 31종),
상점 시트(game-stage.js openShop)가 그 카탈로그를 그대로 반영해 버튼을 그리는지는
아무도 화면으로 확인하지 않았다. 카탈로그 상수만 검증하는 단위 테스트(petgame/test_items.js)는
'데이터가 맞다'는 것만 보고 '그 데이터가 상점 UI에 실제로 나오는가'는 못 본다.

여기서는 실제 브라우저에서 마이펫 탭의 육성 게임을 띄우고 '🛒 상점'을 눌러,
먹이 그리드 버튼 수 == 살아있는 FOODS 수, 아이템 그리드 버튼 수 == 살아있는 ITEMS 수
임을 '상대 단언'으로 지킨다(카탈로그가 더 늘어도 자동 추종, 리터럴 개수에 안 깨짐).
현재 알려진 정상값(먹이 9·아이템 31)은 회귀 앵커로만 함께 확인한다.

외부 네트워크(Supabase) 성공에 의존하지 않는다 — 데모 세션 + 로컬 주입 펫으로
화면 구조/가시성만 본다.
"""

import json

NAME = "펫게임 상점 먹이·아이템 카탈로그 반영"

# 육성 게임은 활성 펫이 있어야 마운트된다(펫 0마리면 온보딩 카드만 뜬다).
_PETS = [
    {"id": 970001, "name": "몽이", "breed": "믹스", "type": "dog", "imageUrl": "",
     "age": "2살", "weight": "6", "gender": "남아", "personality": "활발",
     "hunger": 70, "happy": 80},
]


def run(page, base_url):
    # 데모 모드를 켜야 주입한 펫이 syncPets에 덮여 사라지지 않는다(2026-07-26 교훈).
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_pgshop@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','mypet');"
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps(_PETS))
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-mypet", state="attached", timeout=15000)
    page.wait_for_timeout(2500)

    # 육성 게임이 마운트되고 스테이지가 그려질 때까지 대기(펫이 없으면 여기서 실패 → 회귀 신호).
    page.wait_for_selector("#petgame-root", state="attached", timeout=15000)
    page.wait_for_selector("#pg-stage", state="visible", timeout=15000)

    # === A. 살아있는 카탈로그(상점이 렌더의 근거로 삼는 원천) 확인 ===
    cat = page.evaluate(
        """() => {
            const I = window.PetGameItems;
            if (!I) return null;
            const lv1 = I.ITEMS.filter(i => i.unlockLv === 1).length;
            const lv10All = I.itemsForSpace('yard', 10).length + I.itemsForSpace('room', 10).length;
            return {
                foods: I.FOODS.length,
                items: I.ITEMS.length,
                lv1,
                lv10All,
                hasKing: I.ITEMS.some(i => i.id === 'golden_doghouse' && i.unlockLv === 10),
            };
        }"""
    )
    assert cat, "window.PetGameItems 카탈로그를 못 읽음 — 스크립트 로드 회귀"
    assert cat["foods"] == 9, f"FOODS 수 {cat['foods']} — 현재 정상값 9와 불일치(회귀 앵커)"
    assert cat["items"] == 31, f"ITEMS 수 {cat['items']} — 현재 정상값 31과 불일치(회귀 앵커)"
    assert cat["lv1"] == 8, f"Lv1 기본템 {cat['lv1']}종 — 8종이어야 한다"
    assert cat["hasKing"], "golden_doghouse(Lv10 킹)가 카탈로그에 없음"
    # Lv10 해금은 전체와 같아야 한다(공간별 합 == ITEMS.length, 상대 단언)
    assert cat["lv10All"] == cat["items"], (
        f"Lv10 해금 합 {cat['lv10All']} != ITEMS {cat['items']} — itemsForSpace 회귀"
    )

    # === B. '🛒 상점'을 실제로 눌러 시트가 카탈로그를 그대로 반영하는지 ===
    shop_btn = page.locator("#petgame-root button", has_text="상점").first
    assert shop_btn.count() > 0, "육성 게임 액션바에 '상점' 버튼이 없음(구조 회귀)"
    shop_btn.click()

    page.wait_for_selector("#pg-sheet .grid", state="visible", timeout=10000)

    grids = page.evaluate(
        """() => {
            const sheet = document.getElementById('pg-sheet');
            if (!sheet) return null;
            const gs = Array.from(sheet.querySelectorAll('.grid'));
            const I = window.PetGameItems;
            const themePaid = I.THEMES.filter(t => t.price > 0).length;
            return {
                gridCount: gs.length,
                foodBtns: gs[0] ? gs[0].querySelectorAll('button').length : 0,
                itemBtns: gs[1] ? gs[1].querySelectorAll('button').length : 0,
                themeBtns: gs[2] ? gs[2].querySelectorAll('button').length : 0,
                headerShown: /상점/.test(sheet.textContent || ''),
                catFoods: I.FOODS.length,
                catItems: I.ITEMS.length,
                catThemesPaid: themePaid,
            };
        }"""
    )
    assert grids, "#pg-sheet를 못 읽음 — 상점 시트가 열리지 않음"
    assert grids["headerShown"], "상점 시트에 '상점' 헤더 텍스트가 없음"
    assert grids["gridCount"] >= 2, (
        f"상점 시트 그리드가 {grids['gridCount']}개 — 먹이/아이템 최소 2개여야 한다"
    )
    # 상대 단언: 먹이/아이템 버튼 수가 살아있는 카탈로그 수와 정확히 일치해야 한다.
    assert grids["foodBtns"] == grids["catFoods"], (
        f"먹이 버튼 {grids['foodBtns']} != FOODS {grids['catFoods']} — "
        "상점이 먹이 카탈로그를 빠짐없이 그리지 않음"
    )
    assert grids["itemBtns"] == grids["catItems"], (
        f"아이템 버튼 {grids['itemBtns']} != ITEMS {grids['catItems']} — "
        "상점이 아이템 카탈로그를 빠짐없이 그리지 않음(잠금/보유 포함 전량 렌더 기대)"
    )
    # 유료 방 테마도 그려지면(그리드 3개) 카탈로그와 일치해야 한다.
    if grids["gridCount"] >= 3:
        assert grids["themeBtns"] == grids["catThemesPaid"], (
            f"테마 버튼 {grids['themeBtns']} != 유료 THEMES {grids['catThemesPaid']}"
        )


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
