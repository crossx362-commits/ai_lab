"""게스트('가입 없이 먼저 둘러보기') 진입·쓰기차단·종료 (회의 결정 2026-08-08, 오너 승인).

왜 이 테스트가 중요한가
-----------------------
게스트 모드의 안전선은 **원격 쓰기가 절대 나가지 않는다**는 것 하나다. 이게 뚫리면
로그인하지 않은 방문자의 조작이 실제 Supabase에 쓰인다.

설계 당시 실측으로 확인한 것: `isDemoMode()` 가드는 원격 → 로컬 방향(sync*) 10곳에만
있었고, upload/update/delete 16곳 중 12곳에는 인증 가드조차 없어 `isConnected`만 보고
그대로 서버로 나갔다. 그래서 가드를 CTA가 아니라 **데이터 계층**(supabase.js의
isReadOnlySession)에 한 번만 두었다 — CTA를 하나씩 감싸면 새 버튼이 생길 때마다 뚫린다
(2026-07-25 데모 동기화 사고의 교훈).

여기서 굳히는 것:
  1. 로그인 화면에 게스트 진입 버튼이 있고 실제로 앱으로 들어간다.
  2. 게스트 상태에서 쓰기 함수를 호출해도 **네트워크 요청이 0건**이다(fetch 계측).
     — 이 단언이 핵심이다. 존재 확인이 아니라 실제 호출로 검증한다.
  3. "저장되지 않는다"는 고지 배너가 보이고, 하단 네비·헤더를 가리지 않는다.
  4. 종료하면 로그인 화면으로 돌아가고 가드가 풀린다.
"""

NAME = "게스트 둘러보기 진입·쓰기차단·종료"


def _ls(page, key):
    return page.evaluate("(k) => localStorage.getItem(k)", key)


def run(page, base_url):
    page.goto(base_url)
    page.evaluate("() => localStorage.clear()")
    page.reload()
    page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)

    # 1) 진입 버튼이 로그인 화면에 실제로 도달 가능해야 한다.
    btn = page.wait_for_selector("#guest-enter-btn", state="visible", timeout=5000)
    assert btn is not None, "게스트 진입 버튼이 로그인 화면에 없다"

    btn.click()
    # 진입은 location.reload()로 이뤄진다 — 새 문서가 뜨고 스크립트가 실행될 때까지 기다린다.
    page.wait_for_load_state("load")
    page.wait_for_function("typeof SupabaseService !== 'undefined'", timeout=15000)
    page.wait_for_selector("#login-landing-overlay", state="hidden", timeout=15000)
    assert _ls(page, "petna_guest_mode") == "1", "게스트 플래그가 켜지지 않았다"
    assert _ls(page, "petna_demo_mode") == "1", (
        "데모 플래그가 없으면 새로고침 직후 sync*가 로컬을 DB로 덮어쓴다(2026-07-25 사고)")

    # 2) 핵심 — 게스트 상태에서 원격 쓰기가 네트워크를 타지 않는다.
    #    존재 확인이 아니라 실제로 호출해 fetch 발생 수를 센다.
    result = page.evaluate(
        """async () => {
            const calls = [];
            const orig = window.fetch;
            window.fetch = function (...a) { calls.push(String(a[0])); return orig.apply(this, a); };
            const S = SupabaseService;   // const 선언이라 window.에는 안 붙는다
            const probes = {
                uploadPost:     () => S.uploadPost({petName:'E2E_GUARD', petAvatar:'🔬', content:'probe'}),
                updatePet:      () => S.updatePet({id: 999999, name:'E2E_GUARD'}),
                uploadAlbum:    () => S.uploadAlbum({id: 999999, title:'E2E_GUARD'}),
                updateProfile:  () => S.updateProfile({email:'probe@example.com', nickname:'E2E_GUARD'}),
                uploadHealthLog:() => S.uploadHealthLog({date:'2026-01-01'}),
            };
            const out = {};
            for (const [name, fn] of Object.entries(probes)) {
                const before = calls.length;
                try { await fn(); } catch (e) { /* 가드에 막혀 조기 반환이면 예외도 없다 */ }
                out[name] = calls.length - before;
            }
            window.fetch = orig;
            return { readOnly: S.isReadOnlySession(), calls: out };
        }"""
    )
    assert result["readOnly"] is True, "게스트인데 isReadOnlySession()이 false다"
    leaked = {k: v for k, v in result["calls"].items() if v > 0}
    assert not leaked, (
        f"게스트 상태에서 원격 쓰기가 나갔다 — {leaked}. "
        "데이터 계층 가드(isReadOnlySession)가 뚫렸다")

    # 3) 고지 배너 — 보이고, 하단 네비/헤더를 가리지 않아야 한다.
    layout = page.evaluate(
        """() => {
            const bar = document.getElementById('guest-mode-banner');
            if (!bar) return { banner: false };
            const nav = document.getElementById('mobile-navbar');
            const out = { banner: true, visible: bar.checkVisibility ? bar.checkVisibility() : true,
                          navCovered: false };
            if (nav && (nav.checkVisibility ? nav.checkVisibility() : true)) {
                const btn = nav.querySelector('.mobile-tab-btn') || nav.querySelector('button');
                if (btn) {
                    const r = btn.getBoundingClientRect();
                    const el = document.elementFromPoint(r.right - 4, r.bottom - 4);
                    out.navCovered = !!(el && el.closest('#guest-mode-banner'));
                }
            }
            return out;
        }"""
    )
    assert layout.get("banner"), "게스트 고지 배너가 없다 — 저장되지 않는다는 사실을 숨기면 안 된다"
    assert layout.get("visible"), "게스트 배너가 보이지 않는다"
    assert not layout.get("navCovered"), "게스트 배너가 하단 네비 탭을 가린다 — 탭 이동이 막힌다"

    # 4) 종료 → 로그인 화면 복귀 + 가드 해제
    page.evaluate("() => exitGuestMode()")
    page.wait_for_load_state("load")
    page.wait_for_function("typeof SupabaseService !== 'undefined'", timeout=15000)
    page.wait_for_selector("#login-landing-overlay", state="visible", timeout=15000)
    assert _ls(page, "petna_guest_mode") is None, "종료했는데 게스트 플래그가 남았다"
    assert _ls(page, "petna_demo_mode") is None, "종료했는데 데모 플래그가 남았다"
    off = page.evaluate("() => SupabaseService.isReadOnlySession()")
    assert off is False, "종료 후에도 쓰기 가드가 켜져 있다 — 정상 로그인 사용자의 동기화가 막힌다"

    return "진입·쓰기차단(5종 네트워크 0)·배너·종료 확인"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
