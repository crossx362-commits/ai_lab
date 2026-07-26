"""펫나 E2E — 건강 탭 병합 카드 묶음 구성원 안전망 (2026-07-26 회의 결정).

2026-07-26 오너 지시로 건강 탭의 흩어진 카드를 card-merge 래퍼 3장으로 묶었다:
  · #health-passport-group  — 건강수첩 + 영양관리 + 급식칼로리 + 식단추천
  · #health-cost-group      — 가계부 + 병원비 시세 보드
  · #health-medication-group— 오늘 투약·케어 체크 + 투약·정기예방 대시보드

회귀 위험: 묶음은 '겉카드 하나 + 안쪽 호스트 여러 개' 구조라, 호스트를 옮기거나
템플릿을 손대면 **호스트만 남고 렌더가 안 되는** 상태가 조용히 생긴다(실제로
care-check 호스트를 마이펫→건강으로 옮길 때 renderCareCheckBanner 호출을 같이
안 옮겨 빈 채로 남을 뻔했다). 카드가 '있다'가 아니라 '구성원이 실제로 내용을
그렸다'까지 본다.

셀렉터는 반드시 **고유 ID**로 잡는다 — 2026-07-25에 `.card-modern.divide-y` 같은
클래스 조합을 앵커로 쓴 테스트가 카드 한 장 늘자 조용히 깨진 전례가 있다.
"""

NAME = "건강 탭 병합 묶음 구성원"

_PET = {
    "id": 970726, "name": "묶음", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.4", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}

# 묶음 ID → 그 안에 반드시 내용이 그려져야 하는 호스트들
_GROUPS = {
    "health-passport-group": [
        "medical-records-timeline",   # 건강수첩(의료 이력)
        "calorie-tracker-widget",     # 일일 급식·칼로리
        "diet-recommend-widget",      # 맞춤 식단·급여량
        # 7일 트렌드는 2026-07-26 회의 다수안 + 오너 승인으로 '오늘의 기록' 카드로
        # 되돌아갔다(입력↔조회 인접). 이 묶음의 구성원이 아니다.
    ],
    "health-cost-group": [
        "expense-tracker-widget",     # 가계부
        "vet-cost-board-widget",      # 병원비 시세 보드
    ],
    "health-medication-group": [],   # 아래 _PRESENCE_ONLY 참고
}

# 데이터가 없으면 의도적으로 비우는 위젯 — '묶음 안에 있다'까지만 본다.
# preventive-care-dashboard는 medicine/vet 일정이 있을 때만 그린다
# (medical-records.js:519 필터). 테스트 펫은 일정이 없으므로 빈 게 정상이고,
# 여기에 내용까지 요구하면 멀쩡한 앱을 실패로 만든다(첫 작성 때 실제로 그랬다).
_PRESENCE_ONLY = {
    "health-medication-group": ["care-check-banner", "preventive-care-dashboard"],
}


def run(page, base_url):
    import json

    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_groups@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-health", state="attached", timeout=15000)
    page.wait_for_timeout(3000)   # 위젯 렌더 대기(1회 허용)

    for group_id, members in _GROUPS.items():
        card = page.wait_for_selector(f"#{group_id}", state="visible", timeout=15000)
        assert card is not None, f"병합 묶음 #{group_id}가 렌더되지 않음(구조 회귀)"

        res = page.evaluate(
            """([gid, ids]) => {
                const box = document.getElementById(gid);
                if (!box) return { found: false };
                const missing = [], empty = [], outside = [];
                for (const id of ids) {
                    const el = document.getElementById(id);
                    if (!el) { missing.push(id); continue; }
                    if (!box.contains(el)) { outside.push(id); continue; }
                    // canvas는 innerHTML이 비어 있는 게 정상 — 크기로 판정한다.
                    const drawn = el.tagName === 'CANVAS'
                        ? (el.width > 0 && el.height > 0)
                        : el.innerHTML.trim().length > 30;
                    if (!drawn) empty.push(id);
                }
                return { found: true, missing, empty, outside };
            }""",
            [group_id, members],
        )
        assert res.get("found"), f"#{group_id}를 DOM에서 찾지 못함"
        assert res["missing"] == [], \
            f"#{group_id}: 구성원 호스트가 사라짐 {res['missing']} — 템플릿에서 누락됐는지 확인"
        assert res["outside"] == [], \
            f"#{group_id}: 구성원이 묶음 밖으로 빠짐 {res['outside']} — 재배치 회귀"
        assert res["empty"] == [], \
            f"#{group_id}: 호스트는 있는데 내용이 안 그려짐 {res['empty']} — " \
            f"호스트만 옮기고 렌더 호출을 안 옮겼는지 확인"

        # 데이터 의존 위젯: 묶음 안에 실재하는지만 확인(내용은 데이터에 달림)
        for host_id in _PRESENCE_ONLY.get(group_id, []):
            inside = page.evaluate(
                """([gid, hid]) => {
                    const box = document.getElementById(gid), el = document.getElementById(hid);
                    return !!(box && el && box.contains(el));
                }""",
                [group_id, host_id],
            )
            assert inside, f"#{group_id}: {host_id} 호스트가 묶음 안에 없음(재배치 회귀)"

    # 급여량 권장은 한 값이어야 한다 — calorie-tracker가 단일 산출점(2026-07-26 회의).
    # 예전엔 diet-recommend가 같은 RER/DER을 따로 계산해 한 탭에 287 vs 269가 떴다.
    kcals = page.evaluate(
        """() => {
            const grab = (id) => {
                const t = (document.getElementById(id)?.textContent || '').replace(/\\s+/g, ' ');
                const m = t.match(/(\\d+)\\s*kcal/);
                return m ? parseInt(m[1], 10) : null;
            };
            return { calorie: grab('calorie-tracker-widget'), diet: grab('diet-recommend-widget') };
        }"""
    )
    if kcals["calorie"] is not None and kcals["diet"] is not None:
        assert kcals["calorie"] == kcals["diet"], (
            f"권장 칼로리가 두 위젯에서 다름 — 급식 {kcals['calorie']}kcal vs "
            f"식단 {kcals['diet']}kcal. CalorieTracker.targetKcal 단일 산출점이 깨졌다."
        )
