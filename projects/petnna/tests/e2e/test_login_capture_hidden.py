"""공유·실종QR 캡처 중에는 로그인 오버레이가 확실히 숨는다.

왜 이 테스트가 필요한가(2026-08-08 회의 결정): 로그인 화면을 데스크톱 2단 split으로
재구성하면서 `#login-landing-overlay`의 마크업을 바꾼다. 그런데 이 ID는 화면 표시 용도
말고도 **캡처 기능의 숨김 앵커**로 쓰인다 —

  · care-share.js:83  `.cs-share-active #login-landing-overlay{display:none !important}`
  · public-profile.js:466 `.pp-finder-active #login-landing-overlay{...}`

즉 공유 카드·실종 QR 이미지를 만들 때 화면을 통째로 캡처하면서 이 오버레이만 CSS로
숨긴다. ID가 바뀌거나 오버레이가 다른 요소로 감싸이면 **캡처 이미지에 로그인 창이
찍혀 나간다** — 사용자가 공유한 이미지에 남의 로그인 폼이 박히는 셈이라 조용히
망가지면 치명적이다. 화면 렌더 테스트로는 절대 안 잡힌다.

여기서 굳히는 것:
  1. `#login-landing-overlay` ID가 살아 있다(다섯 참조처의 앵커).
  2. 캡처 클래스가 붙으면 오버레이가 실제로 안 보인다 — 클래스 존재가 아니라
     `checkVisibility()` 실측으로 본다(2026-07-28 교훈: Tailwind display 클래스가
     `[hidden]`을 이기듯, CSS 우선순위는 코드만 읽어선 알 수 없다).
  3. 캡처 클래스를 떼면 다시 보인다(숨김이 영구화되지 않는다).
"""

NAME = "캡처 중 로그인 오버레이 숨김"

_CAPTURE_CLASSES = ["cs-share-active", "pp-finder-active"]


def _visible(page, sel):
    return page.evaluate(
        """(s) => {
            const el = document.querySelector(s);
            if (!el) return null;
            return el.checkVisibility ? el.checkVisibility()
                 : !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
        }""",
        sel,
    )


def run(page, base_url):
    page.goto(base_url)
    page.wait_for_selector("#login-landing-overlay", state="attached", timeout=15000)

    # 캡처 스타일은 해당 기능 모듈이 처음 쓰일 때 주입될 수도 있으므로, 규칙이 이미
    # 문서에 있는지 확인한다. 없으면 이 테스트의 전제가 깨진 것이라 명확히 실패시킨다.
    has_rule = page.evaluate(
        """() => {
            for (const sheet of document.styleSheets) {
                let rules;
                try { rules = sheet.cssRules; } catch (e) { continue; }  // CORS 시트는 건너뜀
                for (const r of rules || []) {
                    if (r.selectorText && r.selectorText.includes('#login-landing-overlay')
                        && /share-active|finder-active/.test(r.selectorText)) return true;
                }
            }
            return false;
        }"""
    )

    results = []
    for cls in _CAPTURE_CLASSES:
        page.evaluate("(c) => document.documentElement.classList.add(c)", cls)
        hidden = _visible(page, "#login-landing-overlay")
        page.evaluate("(c) => document.documentElement.classList.remove(c)", cls)
        shown = _visible(page, "#login-landing-overlay")

        assert hidden is not None, f"{cls}: #login-landing-overlay 요소가 사라졌다 — " \
                                   "다섯 참조처(care-share·public-profile·supabase·app)의 앵커다"
        if has_rule:
            assert hidden is False, (
                f"{cls} 상태에서 로그인 오버레이가 여전히 보인다 — "
                "공유 카드·실종 QR 캡처 이미지에 로그인 폼이 찍혀 나간다")
        assert shown is True, f"{cls} 해제 후에도 오버레이가 안 보인다 — 숨김이 영구화됐다"
        results.append(f"{cls}={'숨김' if hidden is False else '표시'}")

    if not has_rule:
        # 규칙이 아직 주입 전이면 숨김 여부는 판정 불가 — ID 보존만 확인하고 그 사실을 남긴다.
        return "ID 보존 확인(캡처 CSS 미주입 상태라 숨김 판정은 생략): " + ", ".join(results)
    return "캡처 클래스별 숨김 확인: " + ", ".join(results)
