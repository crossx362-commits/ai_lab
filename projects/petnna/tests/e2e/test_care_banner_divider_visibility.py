"""펫나 E2E — 상단 케어 배너 divider 가시성 안전망.

마이펫 홈의 '오늘' 통합카드(templates/mypet.js의 `#home-today-card`)는
상단에 조건부 배너들을 품는다(그중 아래 둘을 검증):
  1) #memory-flashback-banner (memory-flashback.js — 추억 회고, 있을 때만 노출)
     ※ 원래 여기 있던 #care-check-banner는 2026-07-26 건강 탭으로 이동했다.
  2) #care-nudge-banner  (care-nudge.js — 오늘 챙길 것, 있을 때만 노출)

두 스크립트는 노출할 내용이 없으면 `host.innerHTML=''; host.hidden=true`로
배너를 접는다. Tailwind `divide-y`가 자식 사이에 구분선을 그리는데, 그 규칙은
`> :not([hidden]) ~ :not([hidden])`이라 **hidden 배너는 구분선을 만들지 않는다**.

회귀 위험: 배너가 비었는데도 `hidden`을 걸지 않으면(빈 채 노출), 높이 0짜리
유령 요소가 divide-y의 '보이는 선행 형제'로 취급돼 아래 블록 위에 유령 구분선
+ 빈 간격이 생긴다. 특히 **상단 2블록이 동시에 빈 경우** 그 아래 첫 상시 블록
위에 붕 뜬 선이 나타나는 회귀를 이 테스트가 잡는다.

셀렉터 주의(2026-07-25 수정): `.card-modern.divide-y.divide-gray-100` 클래스
조합은 마이펫 탭의 여러 카드(#home-today-card·#home-challenge-card)가
공유하므로 클래스가 아니라 **#home-today-card ID**로 잡는다 — 첫 매치가 hidden
카드면 visible 대기가 영영 안 끝나는 함정이 있었다. (같은 날 알림 카드
#home-alerts-card와 #home-summary-card가 이 카드로 통합됐다.)
또한 날짜/날씨 블록은 2026-07-24 상단 헤더로 이동해 카드 안에 없다 — 배너 아래
'첫 번째 hidden 아닌 형제'를 측정 대상으로 삼는다.

외부 네트워크(수파베이스)에 의존하지 않는다 — 로그인 게이팅은 앱이 읽는
localStorage 플래그로 우회하고, 두 배너의 데이터 소스 함수를 테스트에서
스텁으로 교체해 4가지 표시 조합을 결정론적으로 재현한 뒤 실제 렌더 결과의
가시성·구분선(border-top)만 측정한다. 앱 코드(index.html·js·css)는 변경하지 않는다.
"""

import json

NAME = "상단 케어 배너 divider 가시성"

_PET = {
    "id": 990721, "name": "구분선", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "2살", "weight": "8", "gender": "여아",
    "personality": "차분", "hunger": 60, "happy": 70,
}

# 한 조합의 상태를 구동(데이터 소스 스텁)하고, 통합카드 상단 3요소를 측정하는 JS.
#   check / nudge : 각 배너를 채울지 여부(True→내용 있음, False→비움)
# 반환: 추억배너 / care-nudge / 그 아래 첫 상시블록의 가시성·구분선 폭.
_APPLY_COMBO = r"""
([check, nudge]) => {
    // --- 데이터 소스 스텁: 렌더 함수가 typeof로 참조하는 전역만 교체 ---
    // 2026-07-26 care-check 배너는 건강 탭으로 이동했다. 이 카드에 남은 조건부 배너 중
    // 테스트가 통제할 수 있는 건 care-nudge 하나뿐이라, 'check' 축은 추억 배너로 옮겼다.
    window.analyzeStool = () => nudge ? { emoji: '💩', label: '무름', days: 3 } : null;
    window.analyzeWellness = () => [];

    // --- 실제 앱 렌더 함수 호출(진짜 innerHTML/hidden 토글 + divide-y CSS 적용) ---
    if (typeof window.renderCareNudgeBanner === 'function') window.renderCareNudgeBanner();

    // 이 테스트가 통제하는 건 care-nudge 하나다. 앞선 조건부 배너들은 앱과 같은 규약
    // (빈 내용 + hidden)으로 접어 통제 대상을 '앞에 보이는 형제가 없는' 상태로 만든 뒤
    // 구분선을 측정한다. check 축은 추억 배너를 직접 채워 재현한다.
    ['health-digest-banner', 'memorial-banner'].forEach((id) => {
        const el = document.getElementById(id);
        if (el) { el.innerHTML = ''; el.hidden = true; }
    });
    const mf = document.getElementById('memory-flashback-banner');
    if (mf) {
        if (check) { mf.innerHTML = '<div class="p-3 text-xs">E2E 추억</div>'; mf.hidden = false; }
        else { mf.innerHTML = ''; mf.hidden = true; }
    }

    const card = document.getElementById('home-today-card');
    const cc = document.getElementById('memory-flashback-banner');
    const cn = document.getElementById('care-nudge-banner');
    if (!card || !cc || !cn) return { ok: false };
    // care-nudge 아래 첫 '펼쳐진' 형제(항상 보이는 첫 콘텐츠 셀). 유령 구분선의 표적.
    // (날짜/날씨 블록은 2026-07-24 헤더로 이동 — 특정 블록이 아니라 구조로 찾는다)
    let dateBlock = cn.nextElementSibling;
    while (dateBlock && (dateBlock.hidden || dateBlock.innerHTML.trim() === '')) {
        dateBlock = dateBlock.nextElementSibling;
    }

    const measure = (el) => {
        const cs = getComputedStyle(el);
        return {
            hidden: el.hidden === true,
            offsetH: el.offsetHeight,               // 접힌(hidden/빈) 요소는 0
            borderTop: parseFloat(cs.borderTopWidth) || 0,  // divide-y 구분선 폭(px)
            empty: el.innerHTML.trim().length === 0,
            display: cs.display,
        };
    };

    return {
        ok: true,
        check: measure(cc),
        nudge: measure(cn),
        date: dateBlock ? measure(dateBlock) : null,
        // 통제 배너 앞에 '보이는' 형제가 없어야 top-border가 0이다. 그 배너 자신이
        // 비어 hidden인 조합도 있으므로 "첫 보이는 자식"이 아니라 "앞에 보이는 형제 없음"
        // 으로 판정한다(전자는 빈 조합에서 항상 거짓이 된다).
        checkIsFirst: [...card.children]
            .slice(0, [...card.children].indexOf(cc))
            .every(el => el.hidden === true),
    };
}
"""


def run(page, base_url):
    # 로그인 우회 + 활성 반려동물 주입(앱 스크립트보다 먼저, 매 내비게이션 재적용).
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_divider@petna.co.kr');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )

    page.goto(base_url)
    page.wait_for_timeout(2500)  # 초기 렌더 대기(1회 허용)

    # 통합카드가 마이펫 탭에 렌더될 때까지 대기.
    card = page.wait_for_selector(
        "#home-today-card",
        state="visible", timeout=15000,
    )
    assert card is not None, "마이펫 탭에 '오늘 요약' 통합카드(#home-today-card)가 렌더되지 않음"

    # 배너 두 개가 카드 안에 실재하는지 확인.
    banners = page.evaluate(
        """() => {
            const card = document.getElementById('home-today-card');
            const cc = document.getElementById('memory-flashback-banner');
            const cn = document.getElementById('care-nudge-banner');
            const strip = document.getElementById('today-care-strip');
            // 2026-07-25 알림 카드가 이 카드로 통합돼 첫 자식은 건강 다이제스트다.
            // 검증할 계약은 "특정 배너가 첫 자식"이 아니라 "조건부 배너들이 모두
            // 상시 블록(케어 요약)보다 앞에 있다" — 그래야 배너가 비었을 때
            // divide-y 유령 구분선이 상시 블록 위에 생기는 회귀를 잡을 수 있다.
            const before = (a, b) => !!a && !!b
                && (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING) > 0;
            return {
                cc: !!cc && card.contains(cc),
                cn: !!cn && card.contains(cn),
                bannersBeforeStrip: before(cc, strip) && before(cn, strip),
                order: before(cc, cn),
            };
        }"""
    )
    assert banners["cc"], "#memory-flashback-banner가 통합카드 안에 없음(구조 회귀)"
    assert banners["cn"], "#care-nudge-banner가 통합카드 안에 없음(구조 회귀)"
    assert banners["bannersBeforeStrip"], \
        "조건부 배너가 상시 블록(#today-care-strip)보다 뒤에 있음 — divide-y 순서 회귀"
    assert banners["order"], "care-nudge가 추억 배너 뒤에 오지 않음 — 상단 배너 순서 회귀"

    # 4가지 표시 조합 × 기대치. (check, nudge) → 각 배너/날짜블록의 가시성·구분선.
    #   - 빈 배너: hidden=True, 높이 0, (아래 유령 구분선 없음)
    #   - 채운 배너: 보임, 높이>0
    #   - date_border: 날짜블록 위 구분선 존재 여부(=상단에 '보이는' 배너가 하나라도 있으면 True)
    combos = [
        # (check, nudge, check_filled_expected, nudge_filled_expected,
        #  nudge_border_expected, date_border_expected, 설명)
        (False, False, False, False, False, False, "상단 2블록 동시 빈 경우 — 유령 divider·간격 없어야 함"),
        (True,  False, True,  False, None,  True,  "케어체크만 노출 — 아래에 divider 1줄"),
        (False, True,  False, True,  False, True,  "넛지만 노출 — 넛지는 유일 상단블록이라 위 divider 없음"),
        (True,  True,  True,  True,  True,  True,  "둘 다 노출 — 사이에 divider, 아래에도 divider"),
    ]

    for check, nudge, cc_fill, cn_fill, cn_border, date_border, desc in combos:
        r = page.evaluate(_APPLY_COMBO, [check, nudge])
        assert r.get("ok"), f"[{desc}] 카드/배너 요소를 찾지 못함"
        c, n, d = r["check"], r["nudge"], r["date"]
        assert d is not None, f"[{desc}] care-nudge 아래 날짜 블록을 찾지 못함(구조 회귀)"

        # 통제 배너 앞에 보이는 형제가 없어야 → top-border 0.
        assert r["checkIsFirst"], f"[{desc}] 통제 배너 앞에 보이는 형제가 있음 — divide-y 순서 회귀"
        assert c["borderTop"] == 0, \
            f"[{desc}] 앞에 보이는 형제가 없는데 유령 top-border {c['borderTop']}px 발생"

        # --- 통제 배너(추억) 상태 ---
        if cc_fill:
            assert not c["hidden"] and not c["empty"] and c["offsetH"] > 0, \
                f"[{desc}] 추억 배너가 채워졌는데 노출 안 됨: {c}"
        else:
            assert c["hidden"] and c["empty"] and c["offsetH"] == 0, \
                f"[{desc}] 추억 배너가 비었는데 접히지 않음(hidden 미설정) — 유령 배너: {c}"

        # --- care-nudge 상태 ---
        if cn_fill:
            assert not n["hidden"] and not n["empty"] and n["offsetH"] > 0, \
                f"[{desc}] care-nudge가 채워졌는데 노출 안 됨: {n}"
        else:
            assert n["hidden"] and n["empty"] and n["offsetH"] == 0, \
                f"[{desc}] care-nudge가 비었는데 접히지 않음(hidden 미설정) — 유령 배너: {n}"

        # --- 핵심: 유령 구분선 방지 ---
        # 빈(접힌) 배너는 divide-y '보이는 선행 형제'로 취급되면 안 되므로
        # 그 바로 위/아래 블록에 유령 구분선을 만들어선 안 된다.
        if cn_border is not None:
            if cn_border:
                assert n["borderTop"] > 0, \
                    f"[{desc}] care-nudge 위 구분선이 있어야 하나 없음(borderTop=0)"
            else:
                assert n["borderTop"] == 0, \
                    f"[{desc}] care-nudge 위에 유령 구분선 {n['borderTop']}px 발생"

        # 날짜/날씨 블록: 상단에 '보이는' 배너가 없으면 위 구분선(붕 뜬 선)도 없어야 한다.
        if date_border:
            assert d["borderTop"] > 0, \
                f"[{desc}] 날짜 블록 위 구분선이 있어야 하나 없음(borderTop=0)"
        else:
            assert d["borderTop"] == 0, \
                f"[{desc}] 상단 2블록이 모두 비었는데 날짜 블록 위에 유령 구분선 " \
                f"{d['borderTop']}px 발생 — divide-y가 hidden 배너를 형제로 오인"

        # 접힌 배너는 어떤 조합에서도 화면 높이를 차지하지 않아야 한다(유령 간격 방지).
        if not cc_fill:
            assert c["display"] == "none", \
                f"[{desc}] 빈 추억 배너가 display:{c['display']} — 접히지 않아 간격 차지"
        if not cn_fill:
            assert n["display"] == "none", \
                f"[{desc}] 빈 care-nudge가 display:{n['display']} — 접히지 않아 간격 차지"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
