"""펫나 E2E — 설정의 데모 데이터가 새로고침 후에도 살아남는지.

2026-07-25 사고: 설정 > '일주일 데모 데이터 주입'을 눌러도 아무것도 반영되지 않았다.
주입 자체는 정상이었고, 진짜 원인은 **주입 직후 새로고침에서 클라우드 동기화가
로컬 상태를 통째로 덮어쓴 것**이었다 — `SupabaseService.sync*()`는 DB에 행이 하나라도
있으면 pets/posts/albums/healthLogs를 DB 내용으로 교체하고 `saveState()`까지 불러
localStorage에 확정 저장한다. 데모 데이터는 1초도 못 버티고 사라졌다.

수리: 주입 시 `petna_demo_mode='1'`을 켜고, 원격 → 로컬 방향 동기화 함수 전부가
이 플래그에서 즉시 반환한다(호출부가 아니라 각 함수 안에 가드 — 실제로 첫 수정은
`startSync()`에만 걸었다가 DOMContentLoaded의 두 번째 동기화 트리거에 그대로 뚫렸다).

이 테스트가 지키는 계약:
  A) 주입하면 `petna_demo_mode` 플래그가 켜진다.
  B) 주입 후 새로고침해도 데모 펫(초코·나비·솜이)이 그대로 남는다
     — 동기화 가드가 하나라도 빠지면 여기서 DB 펫으로 바뀌며 깨진다.
  C) AppStore 스키마 밖에서 자기 localStorage 키를 직접 읽는 모듈들
     (건강수첩·QOL·AI 건강분석)도 데모 데이터를 받는다 — 빠지면 그 탭만 빈 화면.
  D) 데모 모드를 끄면 플래그가 지워지고 배너가 사라진다(복귀 경로가 막히지 않았는지).

주의: 이 테스트는 끝나면 반드시 데모 모드를 끈다 — 켜둔 채 끝나면 같은 브라우저
프로필을 쓰는 다른 테스트가 전부 데모 데이터를 보게 된다.
"""

NAME = "데모 데이터 새로고침 생존"

_DEMO_PETS = ["초코", "나비", "솜이"]

# 데모 주입이 반드시 채워야 하는, AppStore 스키마 밖 모듈 키
# (값은 '이메일 접미사 필요' 여부)
_EXTRA_KEYS = [
    ("petna_medical_records_", True),
    ("petna_qol_checkins", False),
    ("petna_health_analyses", False),
]


def _confirm_dialog(page):
    """showCustomDialog의 '확인' 버튼을 누른다."""
    page.wait_for_selector("#dialog-actions button", state="visible", timeout=10000)
    page.evaluate(
        """() => {
            const btn = [...document.querySelectorAll('#dialog-actions button')]
                .find(b => b.textContent.trim() === '확인');
            if (!btn) throw new Error('확인 버튼 없음');
            btn.click();
        }"""
    )


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_demo@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','settings');"
    )
    page.goto(base_url)
    page.wait_for_selector("#tab-settings", state="attached", timeout=15000)
    page.wait_for_function("() => typeof resetToRichDemoData === 'function'", timeout=15000)

    # === A. 주입 → 데모 모드 플래그 ===
    page.evaluate("() => resetToRichDemoData()")
    _confirm_dialog(page)

    page.wait_for_function(
        "() => localStorage.getItem('petna_demo_mode') === '1'", timeout=10000
    )

    # 주입 함수가 1초 뒤 스스로 새로고침한다 — 그 새로고침이 끝날 때까지 기다린다.
    page.wait_for_timeout(3000)
    page.wait_for_selector("#tab-settings", state="attached", timeout=15000)
    page.wait_for_function("() => typeof AppStore !== 'undefined'", timeout=15000)

    # === B. 새로고침 후에도 데모 펫이 살아있는가 (동기화 클로버 회귀 검출) ===
    # 동기화는 비동기라 즉시 덮어쓰지 않는다 — 넉넉히 기다린 뒤 확인해야 회귀를 잡는다.
    page.wait_for_timeout(4000)
    names = page.evaluate("() => (AppStore.getState('pets') || []).map(p => p.name)")
    assert names == _DEMO_PETS, (
        f"새로고침 후 데모 펫이 사라짐 — 실제: {names}. "
        "원격→로컬 동기화 함수(sync*) 중 isDemoMode() 가드가 빠진 것이 있는지 확인할 것."
    )

    posts = page.evaluate("() => (AppStore.getState('posts') || []).length")
    assert posts == 2, f"데모 게시글이 DB 피드로 덮어써짐 — 개수 {posts} (기대 2)"

    # === C. AppStore 밖 모듈 키도 채워졌는가 ===
    empty = page.evaluate(
        """(specs) => {
            const email = localStorage.getItem('petna_user_email') || '';
            const bad = [];
            for (const [key, needsEmail] of specs) {
                const k = needsEmail ? key + email : key;
                let v;
                try { v = JSON.parse(localStorage.getItem(k) || 'null'); } catch (e) { v = null; }
                const n = Array.isArray(v) ? v.length : (v && typeof v === 'object' ? Object.keys(v).length : 0);
                if (!n) bad.push(k);
            }
            return bad;
        }""",
        _EXTRA_KEYS,
    )
    assert empty == [], f"데모 주입이 비워둔 모듈 키: {empty} — 해당 탭이 빈 화면으로 남는다"

    # === D. 복귀 경로 — 데모 모드 종료 ===
    banner_visible = page.evaluate(
        "() => { const b = document.getElementById('demo-mode-banner');"
        " return !!b && !b.classList.contains('hidden'); }"
    )
    assert banner_visible, "데모 모드인데 설정에 '데모 모드 켜짐' 배너가 안 보임 — 종료 버튼에 도달할 수 없음"

    page.evaluate("() => exitDemoMode()")
    _confirm_dialog(page)
    page.wait_for_function(
        "() => localStorage.getItem('petna_demo_mode') === null", timeout=10000
    )

    # 종료도 스스로 새로고침한다 — 끝나기 전에 테스트가 종료되면 다음 테스트가
    # 반쯤 지워진 상태를 물려받는다.
    page.wait_for_timeout(2500)
