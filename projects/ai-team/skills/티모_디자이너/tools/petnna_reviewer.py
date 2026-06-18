"""
petnna_reviewer.py — 티모: petnna 프로젝트 UI/UX 지속 검토 및 개선 보고
SKILL.md Section 4(AI Slop 타파) + Section 5(7대 심사 기준 + 출력 템플릿) 반영
"""
import os
import sys
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
_project_root = os.path.abspath(os.path.join(_ai_team_root, "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env import load_env
from _shared.llm import ollama as lm_chat, is_available as lm_available
from _shared.notify import send

load_env()

PETNNA_ROOT = os.path.join(_project_root, "projects", "petnna")

REVIEW_TARGETS = [
    ("templates/mypet.js",  "마이펫 하루방 · 날씨 · 운세 화면"),
    ("templates/walk.js",   "산책 지도 · 산책기록 · 나만의 산책로"),
    ("templates/shop.js",   "펫샵 · 굿즈 제작 · 힐링스페이스 · 돌보미"),
    ("templates/album.js",  "일기장 · 친구 공유"),
    ("templates/social.js", "소셜 피드"),
    ("css/style.css",       "전체 스타일시트"),
]

# ─── SKILL.md Section 4 + Section 5 통합 프롬프트 ────────────────────────────
REVIEW_PROMPT = """당신은 15년 경력 수석 UI/UX 디자이너 티모입니다.
아래 petnna 앱의 {name} 코드를 분석하세요.

# 7대 심사 기준 (SKILL.md Section 5)
1. 시각적 계층 구조 — 정보 우선순위 및 시선 흐름
2. 텍스트 가독성 — 폰트 토큰 규격, 명도 대비 4.5:1 만족
3. 터치 타겟 — 모바일 최소 44×44px
4. 빈 상태 처리 — 데이터 누락 시 친절한 유도 UX
5. 반응형 레이아웃 — 모바일 퍼스트
6. 일관성 — 동일 도메인 내 인터랙션 패턴 일치
7. 웹 접근성(WCAG) — 키보드 탭 네비게이션, 스크린 리더 라벨링

# AI Slop 체크 (SKILL.md Section 4)
- 금지 폰트: Inter, Roboto, Open Sans, Lato, Montserrat, Arial, Helvetica → 발견 시 즉시 지적
- 금지 패턴: 흰 배경 위 보라색/블루 그라디언트, 어중간한 파스텔 톤 남발
- 금지 레이아웃: 상단 햄버거 메뉴(→ 하단 바로 교체 권고)
- 애니메이션 300ms 초과 시 지적, prefers-reduced-motion 누락 시 지적

코드:
```
{code}
```

아래 형식으로 정확히 답하세요 (한국어):

## 🎯 Verdict
[종합 한 줄 평 및 UX 위험도: Critical/High/Medium/Low]

## 🔍 Critical Issues
- **[이슈명]** — 무엇이 문제인가
  - **데이터 근거**: [NN Group / WCAG / 오피셜 출처]
  - **Fix 코드**: (CSS/JS 스니펫)
  - **우선순위**: Critical / High / Medium

## 🎨 Aesthetic Assessment
- Typography:
- Color & Atmosphere:
- Visual Hierarchy:

## ✅ What's Working
- 잘 설계된 강점 1~2개

## 🚀 Implementation Priority
1. Critical: 즉시 수정 항목
2. High: 개선 권장 항목
3. Medium: 폴리싱 항목

## 💡 One Big Win
- 이 피드백 적용 시 가장 핵심적인 지표 상승 가치 1가지
"""


def read_file(rel_path: str) -> str | None:
    for base in [
        os.path.join(PETNNA_ROOT, "js"),
        PETNNA_ROOT,
    ]:
        full = os.path.join(base, rel_path)
        if os.path.exists(full):
            with open(full, "r", encoding="utf-8") as f:
                return f.read()[:8000]
    return None


def check_structure() -> list[str]:
    """index.html 구조 정합성 검사 — 템플릿 로드, 변수 참조 등."""
    issues = []
    idx = os.path.join(PETNNA_ROOT, "index.html")
    if not os.path.exists(idx):
        return ["❌ index.html 없음"]

    html = open(idx, encoding="utf-8").read()

    # 1. templates/*.js 로드 여부 확인
    required_templates = [
        "templates/mypet.js", "templates/walk.js", "templates/saju.js",
        "templates/social.js", "templates/album.js", "templates/shop.js",
        "templates/settings.js", "templates/modals.js", "templates/cart.js",
    ]
    for t in required_templates:
        if t not in html:
            issues.append(f"🚨 {t} 미로드 — 로그인 후 빈 화면 발생")

    # 2. window._env_ SUPABASE 주입 여부
    import re
    m = re.search(r'"SUPABASE_URL":\s*"([^"]*)"', html)
    if not m or not m.group(1).startswith("https://"):
        issues.append("🚨 window._env_ SUPABASE_URL 미주입 — 로그인 불가")

    # 3. app.js가 templates보다 뒤에 로드되는지 확인
    tmpl_pos = html.find("templates/mypet.js")
    app_pos  = html.find("js/app.js")
    if tmpl_pos > 0 and app_pos > 0 and tmpl_pos > app_pos:
        issues.append("🚨 templates/mypet.js가 app.js 이후에 로드됨 — TEMPLATE 변수 미정의")

    return issues


def run_review() -> str:
    if not os.path.isdir(PETNNA_ROOT):
        return f"❌ petnna 프로젝트 경로를 찾을 수 없습니다: {PETNNA_ROOT}"

    if not lm_available():
        return "❌ [티모] Ollama 미실행 — petnna 검토 불가."

    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    lines = [f"🎨 <b>[티모] petnna UI/UX 검토 보고</b>\n📅 {now}\n"]
    critical_count = 0

    # 구조 정합성 먼저 체크
    struct_issues = check_structure()
    if struct_issues:
        critical_count += len(struct_issues)
        lines.append("🔍 <b>구조 정합성 검사</b>")
        for iss in struct_issues:
            lines.append(f"  {iss}")
        lines.append("")

    for rel_path, name in REVIEW_TARGETS:
        code = read_file(rel_path)
        if not code:
            lines.append(f"⚠️ <b>{name}</b>: 파일 없음 ({rel_path})")
            continue

        result = lm_chat(REVIEW_PROMPT.format(name=name, code=code), max_tokens=700, temperature=0.3)
        if not result:
            lines.append(f"⚠️ <b>{name}</b>: Ollama 응답 없음")
            continue

        if "Critical" in result:
            critical_count += 1

        lines.append(f"\n📌 <b>{name}</b>\n{result.strip()}")

    summary = f"Critical 항목 {critical_count}건 — 코다리에게 즉시 수정 요청 권장." if critical_count else "Critical 없음 ✅"
    lines.append(f"\n─────────────────────\n📊 검토 완료. {summary}")

    return "\n".join(lines)


if __name__ == "__main__":
    print("🎨 [티모] petnna UI/UX 검토 시작...\n")
    report = run_review()
    print(report.replace("<b>", "").replace("</b>", ""))
    send(report)
    print("\n✅ 텔레그램 보고 완료")
