"""펫나 E2E — QR 미아방지 공개 프로필 생성·습득자 열람·공개 중지 회귀.

오늘 완성된 공개 프로필(`js/public-profile.js`) 흐름의 회귀를 막는다.
  ① 마이펫 탭 QR 버튼 → 설정 모달에서 연락처 입력·'QR 만들기' →
     결과 패널에 QR svg·복사/인쇄/미리보기 버튼이 노출된다.
  ② /p/<token> 습득자 페이지가 로그인 없이 이름·마스킹 연락처를 렌더한다.
  ③ '공개 중지' 후 /p/<token> 이 '프로필을 찾을 수 없어요'를 보여준다.

외부 네트워크(Supabase) 성공에 의존하지 않는다 — 공개 프로필은 localStorage
미러(`petna_public_profiles`)에 항상 저장되고 finder도 localGet을 우선하므로
미연결 상태에서 화면 구조로 전부 검증 가능하다. 오히려 Supabase가 '연결에
성공'하면 클라우드의 실제 반려동물 목록이 주입 펫을 덮어써(비결정적 레이스)
활성 펫이 매번 달라지므로, 원점(127.0.0.1) 외 모든 요청을 차단해 앱을
완전 오프라인으로 고정한다 → 주입한 펫이 항상 활성 펫이 되어 결정적이다.

로그인 게이팅은 앱이 읽는 localStorage 플래그(petna_is_logged_in)를 주입해
우회한다. 단 습득자 페이지 방문(②③)에서는 '로그인 없이'가 요구사항이므로
init 스크립트가 /p/ 경로에서는 로그인 플래그를 세팅하지 않도록 분기한다.

라우팅: 정적 서버는 /p/<token> 을 서빙하지 못하므로(404), 그 문서 요청만
index.html 로 대체 응답한다. index.html 은 <base href="/"> 라 에셋은 원점에서
정상 로드되고, public-profile.js 의 route()가 pathname 을 보고 finder 를 그린다.
"""

import json
from urllib.parse import urlparse

NAME = "QR 미아방지 공개 프로필 생성·습득자 열람·공개 중지"

_PET = {
    "id": 970707, "name": "찾아줘개", "breed": "진돗개", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "12", "gender": "남아",
    "personality": "활발하고 겁이 많아요", "hunger": 70, "happy": 80,
}
_CONTACT = "010-1234-5678"      # 마스킹되어 010-****-5678 로 저장·표시
_MASKED = "010-****-5678"
_MIDDLE = "1234"                # 원문 가운데 자리 — finder 에 노출되면 안 됨


def run(page, base_url):
    origin = base_url.rsplit("/", 1)[0]

    # 앱 경로에서만 로그인/펫 주입. /p/ 습득자 경로는 '로그인 없이' 검증 대상이라 제외.
    page.add_init_script(
        "if (!location.pathname.startsWith('/p/')) {"
        "  localStorage.setItem('petna_is_logged_in','true');"
        "  localStorage.setItem('petna_user_email','e2e_qr@petna.co.kr');"
        "  localStorage.setItem('petna_active_tab','mypet');"
        "  localStorage.setItem('petna_pets', %s);"
        "}" % json.dumps(json.dumps([_PET]))
    )

    # 단일 라우터: ①/p/<token> 문서 요청은 index.html 로 대체(SPA; 에셋은 <base href='/'>
    # 라 원점 로드) ②원점 외 호스트(Supabase·CDN·이미지)는 차단해 앱을 오프라인 고정.
    def _router(route):
        u = route.request.url
        if urlparse(u).path.startswith("/p/"):
            route.fulfill(response=route.fetch(url=base_url))
            return
        if urlparse(u).hostname not in ("127.0.0.1", "localhost"):
            route.abort()
            return
        route.continue_()
    page.route("**/*", _router)

    page.goto(base_url)

    # === ① 마이펫 탭 QR 버튼 → 설정 모달 ===
    qr_btn = page.wait_for_selector(
        "button[onclick=\"PublicProfile.open()\"]", state="visible", timeout=15000,
    )
    assert qr_btn is not None, "마이펫 탭 미아방지 QR 버튼이 보이지 않음 (로그인 우회/탭 렌더 실패 가능)"
    qr_btn.click()

    overlay = page.wait_for_selector("#pp-modal-overlay", state="visible", timeout=5000)
    assert overlay is not None, "공개 프로필 설정 모달이 열리지 않음"
    assert "미아방지 QR" in page.locator("#pp-modal-overlay h3").inner_text(), \
        "모달 제목에 '미아방지 QR' 표시가 없음"

    # 기본 공개 필드가 하나 이상 체크되어 저장 가능한 상태여야 한다.
    checked = page.eval_on_selector_all(".pp-field", "els => els.filter(e => e.checked).length")
    assert checked >= 1, "공개 필드 기본 체크가 하나도 없음 — 저장 불가 상태"

    # 연락처 입력 후 'QR 만들기'
    page.fill("#pp-contact", _CONTACT)
    page.get_by_role("button", name="QR 만들기", exact=True).click()

    # 결과 패널: QR svg + 복사/인쇄/미리보기 버튼
    page.wait_for_selector("#pp-result:not(.hidden)", state="visible", timeout=8000)
    page.wait_for_selector("#pp-qr svg", state="visible", timeout=8000)
    result_text = page.locator("#pp-result").inner_text()
    for label in ("복사", "인쇄", "미리보기"):
        assert label in result_text, f"결과 패널에 '{label}' 버튼이 없음"
    assert "공개 중" in result_text, "결과 패널이 '공개 중' 상태를 표시하지 않음"

    # 생성된 토큰 추출(로컬 미러 기준)
    token = page.evaluate(
        "(pid) => localStorage.getItem('petna_pubprofile_pet_' + pid)", str(_PET["id"]),
    )
    assert token and len(token) >= 8, f"공개 프로필 토큰이 생성되지 않음: {token!r}"

    # === ② 습득자 페이지: 로그인 없이 이름·마스킹 연락처 렌더 ===
    finder_url = origin + "/p/" + token
    page.goto(finder_url)

    root = page.wait_for_selector("#pp-finder-root", state="visible", timeout=10000)
    assert root is not None, "습득자 페이지 finder 루트가 렌더되지 않음"
    # 로딩 플레이스홀더가 걷히고 실제 프로필(또는 결과)이 그려질 때까지 대기.
    page.wait_for_function(
        "(name) => {"
        "  const el = document.getElementById('pp-finder-root');"
        "  if (!el) return false;"
        "  const t = el.innerText || '';"
        "  return t.includes(name) || t.includes('찾을 수 없어요');"
        "}",
        arg=_PET["name"], timeout=10000,
    )
    ftext = page.locator("#pp-finder-root").inner_text()
    assert "찾을 수 없어요" not in ftext, "공개 중인 프로필인데 '찾을 수 없어요'가 표시됨"
    assert _PET["name"] in ftext, "습득자 페이지에 반려동물 이름이 노출되지 않음"
    assert _MASKED in ftext, "습득자 페이지에 마스킹된 연락처가 노출되지 않음"
    assert _MIDDLE not in ftext, "연락처 원문 가운데 자리가 마스킹되지 않고 노출됨(개인정보 유출)"

    # === ③ 공개 중지 후 /p/<token> 이 '프로필을 찾을 수 없어요' ===
    page.goto(base_url)
    page.wait_for_selector(
        "button[onclick=\"PublicProfile.open()\"]", state="visible", timeout=15000,
    ).click()
    page.wait_for_selector("#pp-modal-overlay", state="visible", timeout=5000)
    # 기존 프로필이 있으므로 '공개 중지' 버튼이 노출된다.
    page.get_by_role("button", name="공개 중지", exact=True).click()

    # 로컬 미러의 is_public 이 false 로 전환될 때까지 대기(동기 localStorage 기록).
    page.wait_for_function(
        "(tok) => {"
        "  let m = {};"
        "  try { m = JSON.parse(localStorage.getItem('petna_public_profiles') || '{}'); } catch (e) {}"
        "  return m[tok] && m[tok].is_public === false;"
        "}",
        arg=token, timeout=5000,
    )

    page.goto(finder_url)
    page.wait_for_selector("#pp-finder-root", state="visible", timeout=10000)
    page.wait_for_function(
        "() => {"
        "  const el = document.getElementById('pp-finder-root');"
        "  return el && (el.innerText || '').includes('찾을 수 없어요');"
        "}",
        timeout=10000,
    )
    not_found_text = page.locator("#pp-finder-root").inner_text()
    assert "프로필을 찾을 수 없어요" in not_found_text, \
        "공개 중지 후에도 습득자 페이지가 프로필을 계속 노출함"
    assert _MASKED not in not_found_text, "공개 중지 후에도 연락처가 노출됨"
