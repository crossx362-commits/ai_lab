"""펫나 E2E — 증상 빠른 진단(트리아지) UI 배선 회귀 검증.

회의 202607111333 채택안(나무_20260708_4)으로 붙은 `js/symptom-triage.js`의
UI 배선이 깨지지 않는지 가드한다. 검증 범위는 회의 가드레일대로 **결정론적인
부분만** — 즉 ①활성 펫 컨텍스트가 모달에 주입되는지, ②증상 선택 → '긴급도 확인'
→ 긴급도 배지가 렌더되는지의 '배선'만 본다. LLM/비결정 출력은 없고(규칙 기반),
긴급도 판정 규칙 자체가 결정적이므로 라벨/배지까지 회귀 가드가 가능하다.

외부 네트워크(수파베이스) 성공에 의존하지 않는다 — 트리아지는 프론트 전용이라
활성 펫만 있으면 오프라인에서 전부 렌더된다. 활성 펫은 앱 부팅이 클라우드에서
덮어쓰는 레이스를 피하려고, 모달을 열기 직전 window.pets/activePetIndex에 직접
주입해 결정적으로 고정한다(이 흐름이 요구하는 '펫 컨텍스트 주입' 자체를 재현).
로그인 게이팅은 앱이 읽는 localStorage 플래그를 주입해 우회한다(실제 인증 없음).
"""

import json

NAME = "증상 빠른 진단(트리아지) UI 배선 회귀"

_PET = {
    "id": 960707, "name": "진단이", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}
# 단두종+호흡 증상 개인화(응급 상향) 검증용 펫
_BRACHY_PET = {
    "id": 960708, "name": "숨찬이", "breed": "퍼그", "type": "dog",
    "imageUrl": "", "age": "2살", "weight": "8", "gender": "여아",
    "personality": "느긋", "hunger": 60, "happy": 70,
}


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_triage@petna.co.kr');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )

    page.goto(base_url)
    page.wait_for_timeout(2500)  # 초기 렌더 1회 대기

    ready = page.wait_for_function(
        "() => window.SymptomTriage"
        " && typeof window.SymptomTriage.open === 'function'"
        " && typeof window.SymptomTriage.run === 'function'"
        " && typeof window.SymptomTriage._triage === 'function'",
        timeout=15000,
    )
    assert ready, "SymptomTriage 전역(open/run/_triage)이 노출되지 않음 (symptom-triage.js 로딩 실패)"

    # === Part A. 순수 규칙 함수 결정성 (_triage) — 배지 레벨 매핑의 기반 ===
    unit = page.evaluate(
        """(pets) => {
            const t = window.SymptomTriage._triage;
            const std = pets.std, brachy = pets.brachy;
            return {
                emerg: t(std, ['seizure'], ''),      // 경련 → 응급(3)
                vet:   t(std, ['vomit'], ''),        // 구토(12kg·3살) → 내원권장(2)
                home:  t(std, ['sneeze'], ''),       // 가벼운 재채기 → 집관찰(1)
                empty: t(std, [], ''),               // 미선택 → 안내 사유
                brachyResp: t(brachy, ['cough'], ''),// 퍼그+기침 → 개인화 응급 상향(3)
                redflag: t(std, [], '초콜릿을 삼켰어요'), // 자유입력 응급 키워드 → 상향(3)
            };
        }""",
        {"std": _PET, "brachy": _BRACHY_PET},
    )
    assert unit["emerg"]["level"] == 3, f"경련은 응급(3)이어야 하나 {unit['emerg']['level']}"
    assert unit["vet"]["level"] == 2, f"구토(표준 성견)는 내원권장(2)이어야 하나 {unit['vet']['level']}"
    assert unit["home"]["level"] == 1, f"가벼운 재채기는 집관찰(1)이어야 하나 {unit['home']['level']}"
    assert unit["empty"]["level"] == 1 and unit["empty"]["reasons"], \
        "증상 미선택 시 안내 사유가 있어야 함"
    assert unit["brachyResp"]["level"] == 3, \
        f"단두종(퍼그)+호흡 증상은 응급 상향(3)이어야 하나 {unit['brachyResp']['level']}"
    assert any("단두" in r or "퍼그" in r for r in unit["brachyResp"]["reasons"]), \
        f"단두종 개인화 사유가 없음: {unit['brachyResp']['reasons']}"
    assert unit["redflag"]["level"] == 3, \
        f"자유입력 응급 키워드는 상향(3)이어야 하나 {unit['redflag']['level']}"

    # === Part B. UI 배선: 활성 펫 주입 → 모달 오픈 → 컨텍스트 렌더 ===
    page.evaluate(
        """(pet) => { window.pets = [pet]; window.activePetIndex = 0; }""",
        _PET,
    )

    page.evaluate("() => window.SymptomTriage.open()")
    overlay = page.wait_for_selector("#st-overlay", state="visible", timeout=5000)
    assert overlay is not None, "트리아지 모달(#st-overlay)이 열리지 않음"

    # 펫 컨텍스트 주입 확인: 모달 상단에 활성 펫 이름/기준 안내가 렌더됨
    overlay_text = page.inner_text("#st-overlay")
    assert _PET["name"] in overlay_text, \
        f"모달에 활성 펫 이름('{_PET['name']}')이 주입되지 않음: {overlay_text[:120]!r}"
    assert "기준으로 판단" in overlay_text, \
        f"'이 아이 기준' 컨텍스트 문구가 없음: {overlay_text[:120]!r}"

    chips = page.query_selector_all("#st-chips .st-chip")
    assert len(chips) >= 10, f"증상 칩이 충분히 렌더되지 않음(len={len(chips)})"

    # === Part B. 증상 선택 → 긴급도 확인 → 응급 배지 렌더 (배선 핵심) ===
    seizure = page.query_selector('#st-chips .st-chip[data-id="seizure"]')
    assert seizure is not None, "경련 증상 칩(data-id=seizure)이 없음"
    seizure.click()
    assert "st-on" in (seizure.get_attribute("class") or ""), "증상 칩 선택 토글(st-on)이 반영되지 않음"

    page.click('#st-overlay button:has-text("긴급도 확인")')
    result = page.wait_for_selector("#st-result:not(.hidden)", timeout=5000)
    assert result is not None, "'긴급도 확인' 후 결과 패널(#st-result)이 노출되지 않음"

    result_text = page.inner_text("#st-result")
    assert "응급" in result_text, f"경련 선택 시 '응급' 긴급도 배지가 없음: {result_text[:160]!r}"
    assert "🚨" in result_text, f"응급 배지 이모지(🚨)가 없음: {result_text[:160]!r}"
    # 회의 가드레일: 의료 면책 문구 필수
    assert "참고용" in result_text and "수의사" in result_text, \
        f"의료 면책 문구가 배지와 함께 렌더되지 않음: {result_text[:200]!r}"

    # === Part B. 재선택으로 다른 긴급도 배지 매핑 확인(내원 권장) ===
    page.evaluate("() => window.SymptomTriage.close()")
    page.evaluate("() => window.SymptomTriage.open()")
    page.wait_for_selector("#st-overlay", state="visible", timeout=5000)
    vomit = page.query_selector('#st-chips .st-chip[data-id="vomit"]')
    assert vomit is not None, "구토 증상 칩(data-id=vomit)이 없음"
    vomit.click()
    page.click('#st-overlay button:has-text("긴급도 확인")')
    page.wait_for_selector("#st-result:not(.hidden)", timeout=5000)
    vet_text = page.inner_text("#st-result")
    assert "내원 권장" in vet_text, f"구토 선택 시 '내원 권장' 배지가 없음: {vet_text[:160]!r}"
    assert "응급" not in vet_text.replace("응급실", ""), \
        f"내원 권장이어야 하는데 응급 배지가 잘못 노출됨: {vet_text[:160]!r}"

    page.evaluate("() => window.SymptomTriage.close()")
