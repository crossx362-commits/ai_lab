"""펫과나 마케팅 문구의 규제 안전성 + AI 엔드포인트 남용 방지 회귀 (2026-08-12).

배경
----
사진 기반 '질병 탐지'는 한국에서 동물용의료기기 품목허가 대상이다
(에이아이포펫=안구 AI, 십일리터=치주·슬개골, 티티케어=국내 최초 AI 동물용
의료기기 SW 모두 허가 후 출시). 펫과나는 허가가 없다.

앱 **안**에는 면책 문구가 있었지만(js/ai-health.js AI_HEALTH_DISCLAIMER),
로그인 랜딩·title·OG 설명 같은 **앱 밖에 노출되는 마케팅 문구**는
"AI가 건강 이상을 먼저 알려주고", "사진 한 장으로 이상 신호"라고 말하고 있었다.
규제 판단은 내부 면책이 아니라 바깥에 내건 문구를 본다.

왜 문구를 '못 박지' 않는가
--------------------------
카피를 리터럴로 단언하는 테스트는 회귀를 잡는 게 아니라 개선을 막는다
(2026-07-28 교훈: 웰니스 배지 2줄 개선이 카피 단언 3개에 막혀 보류로 밀려났다).
그래서 여기서는 **어떤 문구를 써야 하는지**가 아니라 **하면 안 되는 주장이
없는지**만 본다 — 표현은 자유롭게 바꿔도 되고, 진단·탐지를 표방하는 순간만 막힌다.
"""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PETNNA = ROOT / "projects/petnna"
INDEX = PETNNA / "index.html"
MANIFEST = PETNNA / "manifest.json"
API = PETNNA / "api/ai-health.js"

# 허가 없이 표방하면 안 되는 주장. 좁게 유지한다 — 넓히면 정상 카피까지 막는다.
FORBIDDEN = [
    (r"이상\s*신호", "질병 신호 탐지 표방"),
    (r"이상을\s*(먼저\s*)?(알려|찾아|감지|발견)", "질병 탐지 표방"),
    (r"(질병|질환|병)\s*(을|를)?\s*(진단|탐지|판별|발견)", "진단·탐지 표방"),
    (r"AI\s*진단", "AI 진단 표방"),
]
# 면책 문장은 '진단'이라는 낱말을 쓸 수밖에 없다 — 부정 표현이 함께 있으면 통과.
NEGATIONS = ("아닙니다", "아니에요", "아니며", "참고용", "대신하지")


def _marketing_surfaces() -> dict[str, str]:
    """앱 밖에 노출되는 문구만 모은다(검색결과·공유카드·가입 전 화면)."""
    html = INDEX.read_text(encoding="utf-8")
    out = {}

    m = re.search(r"<title>(.*?)</title>", html, re.S)
    if m:
        out["<title>"] = m.group(1)
    for name in ("description", "og:description", "twitter:description",
                 "og:title", "twitter:title"):
        m = re.search(
            rf'<meta\s+(?:name|property)="{re.escape(name)}"\s+content="([^"]*)"', html)
        if m:
            out[f"meta[{name}]"] = m.group(1)

    # 로그인 히어로(가입 전에 보는 화면) 본문 — 태그를 걷어낸 텍스트만.
    hero = re.search(r'id="login-hero-panel".*?<!-- 우: 인증 -->', html, re.S)
    if hero:
        body = re.sub(r"<!--.*?-->", " ", hero.group(0), flags=re.S)   # 주석 제외
        out["로그인 히어로"] = re.sub(r"<[^>]+>", " ", body)

    out["manifest.description"] = json.loads(
        MANIFEST.read_text(encoding="utf-8")).get("description", "")
    return out


class RegulatoryCopyTests(unittest.TestCase):
    def test_marketing_surfaces_claim_no_diagnosis(self):
        offenses = []
        for where, text in _marketing_surfaces().items():
            for pattern, why in FORBIDDEN:
                for m in re.finditer(pattern, text):
                    around = text[max(0, m.start() - 40):m.end() + 40]
                    if any(n in around for n in NEGATIONS):
                        continue          # 면책 문장 안의 언급은 허용
                    offenses.append(f"{where}: …{around.strip()}… ({why})")
        self.assertEqual(
            [], offenses,
            "허가 없이 진단·탐지를 표방하는 마케팅 문구가 있다 "
            "(동물용의료기기 품목허가 대상). 기록·정리 표현으로 바꿔라:\n  "
            + "\n  ".join(offenses),
        )

    def test_login_landing_carries_a_disclaimer(self):
        html = INDEX.read_text(encoding="utf-8")
        self.assertIn(
            'id="login-hero-disclaimer"', html,
            "가입 전 화면에 면책 고지가 없다 — 앱 내부 면책만으로는 부족하다",
        )
        m = re.search(r'id="login-hero-disclaimer"[^>]*>(.*?)</p>', html, re.S)
        self.assertIsNotNone(m, "면책 문구 요소를 파싱하지 못했다")
        self.assertTrue(
            any(n in m.group(1) for n in NEGATIONS),
            "면책 요소는 있는데 '진단이 아니다/참고용'이라는 부정이 없다",
        )


class AiEndpointAbuseGuardTests(unittest.TestCase):
    """AI_HEALTH_ENABLED를 켠 뒤로 /api/ai-health 는 공개 Gemini 프록시가 된다.

    인증이 없으므로 최소한의 문턱(출처 확인·레이트리밋)이 사라지면
    오너의 Gemini 크레딧이 그대로 노출된다.
    """

    def setUp(self):
        self.src = API.read_text(encoding="utf-8")

    def test_kill_switch_still_gates_the_handler(self):
        self.assertIn("AI_HEALTH_ENABLED", self.src,
                      "비상 차단 스위치가 사라졌다")

    def test_origin_and_rate_guards_are_wired(self):
        for fn in ("originAllowed", "rateLimited"):
            self.assertIn(f"function {fn}", self.src, f"{fn} 정의가 없다")
            self.assertRegex(
                self.src, rf"if\s*\(\s*!?\s*{fn}\(",
                f"{fn}가 정의만 되고 핸들러에서 호출되지 않는다",
            )

    def test_image_size_cap_survives(self):
        self.assertIn("MAX_IMAGE_BASE64_CHARS", self.src,
                      "이미지 크기 상한이 사라지면 한 요청으로 크레딧을 태울 수 있다")


class RecordOnlyOutputTests(unittest.TestCase):
    """출력 자체가 기록형이어야 한다(2026-08-12 오너 지시 "출력도 기록형으로").

    카피만 바꾸고 출력이 "정상|주의|이상"·urgent·needsVet 그대로면 위험이 사라진 게
    아니라 안 보이게 된 것이다 — 프롬프트가 판정을 요구하지 않는지 본다.
    """

    def setUp(self):
        self.src = API.read_text(encoding="utf-8")
        # 주석은 제외한다 — 왜 없앴는지 설명하는 주석에 옛 필드명이 등장한다.
        self.code = re.sub(r"//.*", "", self.src)
        self.code = re.sub(r"/\*.*?\*/", "", self.code, flags=re.S)

    def test_record_only_constraint_is_prepended_to_every_health_prompt(self):
        self.assertIn("RECORD_ONLY", self.code, "역할 제한 상수가 없다")
        # photo·symptom·vet-chat 세 갈래 전부에 주입돼야 한다.
        self.assertGreaterEqual(
            self.code.count("${RECORD_ONLY}"), 3,
            "건강 관련 프롬프트 갈래(photo·symptom·vet-chat)마다 역할 제한이 붙어야 한다",
        )

    def test_prompt_asks_for_no_verdicts(self):
        banned = {
            '"정상|주의|이상': "부위별 상태 판정 스케일",
            '"score"': "건강 점수(등급)",
            '"urgent"': "긴급도 판정",
            '"needsVet"': "수의사 필요 여부 판정",
            '"urgency"': "중증도 등급",
            '"possibleCauses"': "원인 추정(감별진단)",
        }
        found = [f"{k} ({why})" for k, why in banned.items() if k in self.code]
        self.assertEqual(
            [], found,
            "프롬프트가 아직 판정형 출력을 요구한다 — 기록형으로 바꿔라: " + ", ".join(found),
        )

    def test_no_claim_of_being_a_veterinarian(self):
        self.assertNotIn(
            "당신은 10년 경력의 친절한 수의사", self.code,
            "AI가 수의사를 사칭하는 시스템 프롬프트가 남아 있다",
        )


if __name__ == "__main__":
    unittest.main()
