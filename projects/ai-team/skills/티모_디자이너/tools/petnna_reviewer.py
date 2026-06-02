"""
petnna_reviewer.py — 티모: petnna 프로젝트 UI/UX 지속 검토 및 개선 보고

Ollama로 petnna 각 모듈을 분석하여 개선이 필요한 부분을 텔레그램으로 보고.
"""
import os
import sys
import json
import datetime
import glob

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(8):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.telegram_notifier import send_telegram_message

load_env()

# petnna 프로젝트 경로
PETNNA_ROOT = os.path.join(os.path.dirname(_root), "petnna")

REVIEW_TARGETS = [
    ("templates/mypet.js",  "마이펫 하루방 · 날씨 · 운세 화면"),
    ("templates/walk.js",   "산책 지도 · 산책기록 · 나만의 산책로"),
    ("templates/shop.js",   "펫샵 · 굿즈 제작 · 힐링스페이스 · 돌보미"),
    ("templates/album.js",  "일기장 · 친구 공유"),
    ("templates/social.js", "소셜 피드"),
    ("css/style.css",       "전체 스타일시트"),
]

REVIEW_PROMPT = """당신은 15년 경력의 시니어 UI/UX 디자이너 티모입니다.
아래 petnna 앱의 {name} 코드를 분석하고, 실제 사용자 경험 관점에서 개선이 필요한 부분을 찾아주세요.

평가 기준:
1. 시각적 계층 구조 (정보 우선순위가 명확한가)
2. 텍스트 가독성 (폰트 크기·대비·줄간격)
3. 터치 타겟 크기 (모바일 44px 이상)
4. 빈 상태 처리 (데이터 없을 때 안내)
5. 반응형 레이아웃 (모바일 우선)
6. 일관성 (같은 액션에 같은 UI 패턴)
7. 접근성 (색상 대비, 의미있는 라벨)

코드:
```
{code}
```

다음 형식으로 간결하게 답해주세요 (한국어):
🔴 즉시 수정: (심각한 문제 1~2개)
🟡 개선 권장: (중요 개선사항 2~3개)
🟢 잘 된 점: (유지해야 할 강점 1개)
"""


def read_file(rel_path: str) -> str | None:
    full = os.path.join(PETNNA_ROOT, "js", rel_path)
    if not os.path.exists(full):
        full = os.path.join(PETNNA_ROOT, rel_path)
    if not os.path.exists(full):
        return None
    with open(full, "r", encoding="utf-8") as f:
        return f.read()[:8000]  # Ollama 컨텍스트 제한


def run_review() -> str:
    if not os.path.isdir(PETNNA_ROOT):
        return f"❌ petnna 프로젝트 경로를 찾을 수 없습니다: {PETNNA_ROOT}"

    if not lm_available():
        return "❌ [티모] Ollama 미실행 — petnna 검토 불가. Ollama를 먼저 실행해주세요."

    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    report_lines = [f"🎨 <b>[티모] petnna UI/UX 검토 보고</b>\n📅 {now}\n"]

    issues_found = False

    for rel_path, name in REVIEW_TARGETS:
        code = read_file(rel_path)
        if not code:
            report_lines.append(f"⚠️ <b>{name}</b>: 파일 없음 ({rel_path})")
            continue

        prompt = REVIEW_PROMPT.format(name=name, code=code)
        result = lm_chat(prompt, max_tokens=600, temperature=0.3)

        if not result:
            report_lines.append(f"⚠️ <b>{name}</b>: Ollama 응답 없음")
            continue

        if "🔴" in result:
            issues_found = True

        report_lines.append(f"\n📌 <b>{name}</b>\n{result.strip()}")

    report_lines.append(
        "\n─────────────────────\n"
        "📊 검토 완료. 즉시 수정(🔴) 항목이 있으면 코다리에게 수정 요청 권장."
    )

    return "\n".join(report_lines)


if __name__ == "__main__":
    print("🎨 [티모] petnna UI/UX 검토 시작...\n")
    report = run_review()
    print(report.replace("<b>", "").replace("</b>", ""))
    send_telegram_message(report)
    print("\n✅ 텔레그램 보고 완료")
