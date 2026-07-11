"""펫나 E2E — 예측 웰니스 이상감지 회귀 검증.

건강 탭의 이상감지 카드(#wellness-anomaly-card, wellness-anomaly.js)가
건강 기록 이력(healthLogs.history)의 음수·식사량 z-score 분석 결과에 따라
올바르게 노출/억제되는지 회귀 검증한다. 세 시나리오를 다룬다:
  1) 비정상 시드 → 경고 카드('평소와 다른 변화가 감지됐어요') + 토스트 노출
  2) 정상 시드   → 경고 미노출('이상 없음' 안정 카드)
  3) N<14 시드   → 판정 억제 안내 카드(표본 부족)

추가로 z-score 순수 함수(window.analyzeWellness)를 결정적 입력으로 단위 검증한다
(mean 200 / std 10 기준선에 recent 170 → z=-3.0, pct=-15%, 급감).

외부 네트워크(수파베이스) 성공에 의존하지 않는다 — healthLogs는 localStorage
기반 전역 상태(AppStore 브릿지)라 window.healthLogs에 시드를 주입한 뒤
renderWellnessCard()로 카드를 직접 렌더해 화면 구조를 검증한다.
로그인 게이팅은 앱이 읽는 localStorage 플래그를 주입해 우회한다(실제 인증 없음).
"""

NAME = "예측 웰니스 이상감지 회귀"

_TODAY = "2026-07-15"          # 토스트 억제 플래그(petna_wellness_alerted_<date>) 대상 날짜

_PET = {
    "id": 990707, "name": "감지견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입 + 건강 탭 활성 (앱 스크립트보다 먼저 실행)
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_wellness@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %r);" % __import__("json").dumps([_PET])
    )

    page.goto(base_url)

    # 건강 탭 렌더 완료 → 실제 이상감지 카드 호스트와 wellness-anomaly.js 로딩 확인.
    host = page.wait_for_selector("#wellness-anomaly-card", state="attached", timeout=15000)
    assert host is not None, "이상감지 카드 호스트(#wellness-anomaly-card)가 없음 (건강 탭 렌더 실패 가능)"

    fns_ready = page.evaluate(
        "() => typeof window.analyzeWellness === 'function'"
        " && typeof window.renderWellnessCard === 'function'"
    )
    assert fns_ready, "analyzeWellness/renderWellnessCard 전역 함수가 노출되지 않음"

    # === Part A. z-score 순수 함수 단위 검증 (결정적 입력) ===
    unit = page.evaluate(
        """() => {
            const mk = arr => arr.map(v => ({ water: v }));
            // 기준선 14개: 190×7 + 210×7 → mean 200, std 10
            const baseline = [190,190,190,190,190,190,190, 210,210,210,210,210,210,210];

            // (1) 비정상 급감: recent 170×3 → z = (170-200)/10 = -3.0
            const abnormal = mk([...baseline, 170, 170, 170]);
            // (2) 정상: recent 205/200/195 → mean 200 → z = 0
            const normal = mk([...baseline, 205, 200, 195]);
            // (3) 표본 부족: 유효 값 10개(<14+3) → 억제
            const few = mk([200,205,195,200,210,190,200,205,195,200]);

            return {
                abnormal: window.analyzeWellness(abnormal),
                normal: window.analyzeWellness(normal),
                few: window.analyzeWellness(few),
            };
        }"""
    )

    a = unit["abnormal"]
    assert len(a) == 1, f"비정상 시드에서 이상 소견 1건을 기대했으나 {len(a)}건"
    f = a[0]
    assert f["metric"] == "water", f"이상 지표가 water가 아님: {f['metric']}"
    assert f["direction"] == "down", f"급감(down)을 기대했으나 {f['direction']}"
    assert f["z"] == -3.0, f"z-score -3.0을 기대했으나 {f['z']}"
    assert f["baselineAvg"] == 200, f"기준 평균 200을 기대했으나 {f['baselineAvg']}"
    assert f["recentAvg"] == 170, f"최근 평균 170을 기대했으나 {f['recentAvg']}"
    assert f["pct"] == -15, f"변화율 -15%를 기대했으나 {f['pct']}"
    assert f["samples"] == 14, f"기준 표본 14를 기대했으나 {f['samples']}"

    assert unit["normal"] == [], f"정상 시드는 이상 소견이 없어야 하나 {unit['normal']}"
    assert unit["few"] == [], f"표본 부족(N<14) 시드는 억제되어 이상 소견이 없어야 하나 {unit['few']}"

    # === Part B. 렌더링 회귀: 비정상 시드 → 경고 카드 + 토스트 ===
    abnormal_card = page.evaluate(
        """(today) => {
            const mk = arr => arr.map(v => ({ water: v }));
            const baseline = [190,190,190,190,190,190,190, 210,210,210,210,210,210,210];
            window.healthLogs = { today: { date: today }, history: mk([...baseline, 170, 170, 170]) };
            try { localStorage.removeItem('petna_wellness_alerted_' + today); } catch (e) {}
            const t = document.getElementById('toast-text');
            if (t) t.innerText = '';
            window.renderWellnessCard();
            const host = document.getElementById('wellness-anomaly-card');
            return { card: host ? host.innerText : '', toast: t ? t.innerText : null };
        }""",
        _TODAY,
    )
    assert "평소와 다른 변화가 감지됐어요" in abnormal_card["card"], \
        f"비정상 시드에서 경고 카드가 노출되지 않음: {abnormal_card['card'][:120]!r}"
    assert abnormal_card["toast"] is not None and "🔮" in abnormal_card["toast"], \
        f"비정상 시드에서 이상 토스트가 노출되지 않음: {abnormal_card['toast']!r}"

    # === Part B. 정상 시드 → 경고 미노출('이상 없음' 안정 카드) ===
    normal_card = page.evaluate(
        """(today) => {
            const mk = arr => arr.map(v => ({ water: v }));
            const baseline = [190,190,190,190,190,190,190, 210,210,210,210,210,210,210];
            window.healthLogs = { today: { date: today }, history: mk([...baseline, 205, 200, 195]) };
            window.renderWellnessCard();
            const host = document.getElementById('wellness-anomaly-card');
            return host ? host.innerText : '';
        }""",
        _TODAY,
    )
    assert "이상 없음" in normal_card, f"정상 시드에서 '이상 없음' 안정 카드가 없음: {normal_card[:120]!r}"
    assert "평소와 다른 변화가 감지됐어요" not in normal_card, \
        "정상 시드인데 경고 카드가 잘못 노출됨"

    # === Part B. N<14 시드 → 판정 억제 안내 카드 ===
    few_card = page.evaluate(
        """(today) => {
            const mk = arr => arr.map(v => ({ water: v }));
            window.healthLogs = { today: { date: today },
                history: mk([200,205,195,200,210,190,200,205,195,200]) };
            window.renderWellnessCard();
            const host = document.getElementById('wellness-anomaly-card');
            return host ? host.innerText : '';
        }""",
        _TODAY,
    )
    assert "예측 웰니스" in few_card and "현재" in few_card, \
        f"표본 부족 시 억제 안내 카드가 노출되지 않음: {few_card[:120]!r}"
    assert "평소와 다른 변화가 감지됐어요" not in few_card, \
        "표본 부족(N<14)인데 경고 카드가 잘못 노출됨"
