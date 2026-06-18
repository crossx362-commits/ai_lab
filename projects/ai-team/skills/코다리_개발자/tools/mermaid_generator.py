"""
mermaid_generator.py — 코다리: Mermaid 다이어그램 자동 생성 도구

사용법:
  python mermaid_generator.py "예약 처리 흐름" --type flowchart
  python mermaid_generator.py "API 인증 시퀀스" --type sequence
  python mermaid_generator.py "데이터베이스 스키마" --type erd
  python mermaid_generator.py "시스템 아키텍처" --type c4
  python mermaid_generator.py --interactive

지원 타입:
  flowchart, sequence, erd, class, state, gantt, c4, journey
"""
import os
import sys
import argparse

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
from _shared.llm import ollama as lm_chat, is_available as lm_available
from _shared.env import load_env

load_env()

# ── 다이어그램 타입별 프롬프트 템플릿 ─────────────────────────────────────────

DIAGRAM_PROMPTS = {
    "flowchart": """
다음 내용을 Mermaid flowchart 다이어그램으로 만들어줘.

규칙:
- `flowchart TD` 방향 사용 (복잡하면 LR)
- 노드 타입: [사각형], ([둥근]), {{{다이아몬드}}}, [/평행사변형/], [(DB)]
- 모든 경로(yes/no 분기 포함) 표현
- 시작/끝 명확히 표시
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "sequence": """
다음 내용을 Mermaid sequenceDiagram으로 만들어줘.

규칙:
- actor(사용자), participant(시스템), database 구분
- 동기 요청: ->>, 비동기 응답: -->>
- 분기는 alt/else 블록 사용
- 반복은 loop 블록 사용
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "erd": """
다음 내용을 Mermaid ERD(erDiagram)로 만들어줘.

규칙:
- 엔티티별 필드 타입 명시 (uuid, string, int, decimal, enum, timestamp)
- PK, FK, UK 표시
- 관계 카디널리티: ||--o{{ (1:N), }}o--o{{ (M:N), ||--|| (1:1)
- 관계 레이블 포함
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "class": """
다음 내용을 Mermaid classDiagram으로 만들어줘.

규칙:
- 클래스별 속성(+public, -private, #protected)과 메서드 포함
- 상속: <|--,  구성: *--, 집합: o--, 의존: ..>
- 인터페이스/추상 클래스 구분
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "state": """
다음 내용을 Mermaid stateDiagram-v2로 만들어줘.

규칙:
- [*]로 시작/끝 표시
- 전환 조건 레이블 포함
- 중첩 상태(state ... {{ }}) 활용
- 주요 상태에 note 추가
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "c4": """
다음 내용을 Mermaid C4Context 또는 C4Container 다이어그램으로 만들어줘.

규칙:
- Context: Person, System, System_Ext, Rel 사용
- Container: Container, ContainerDb, Rel 사용
- title 포함
- 외부 시스템 명확히 구분
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "journey": """
다음 내용을 Mermaid journey(User Journey) 다이어그램으로 만들어줘.

규칙:
- section으로 단계 구분
- 각 태스크 점수(1-5)와 담당자 포함
- 사용자 경험 흐름 순서대로
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",

    "gantt": """
다음 내용을 Mermaid gantt 차트로 만들어줘.

규칙:
- dateFormat YYYY-MM-DD 사용
- section으로 단계 구분
- 태스크별 기간 및 의존성 표시
- 중요 마일스톤 포함
- 오직 mermaid 코드 블록만 출력 (설명 없이)

내용: {description}
""",
}

# 타입 자동 감지용 키워드
TYPE_KEYWORDS = {
    "flowchart":  ["흐름", "프로세스", "flow", "process", "결정", "분기", "알고리즘"],
    "sequence":   ["시퀀스", "sequence", "api", "인증", "요청", "응답", "호출", "메시지"],
    "erd":        ["erd", "데이터베이스", "db", "테이블", "스키마", "엔티티", "관계"],
    "class":      ["클래스", "class", "객체", "oop", "상속", "인터페이스"],
    "state":      ["상태", "state", "lifecycle", "라이프사이클", "전환"],
    "c4":         ["아키텍처", "architecture", "c4", "시스템", "컨테이너", "컴포넌트"],
    "journey":    ["사용자 여정", "user journey", "ux", "경험"],
    "gantt":      ["일정", "gantt", "타임라인", "schedule", "마일스톤"],
}


def _auto_detect_type(description: str) -> str:
    """설명에서 다이어그램 타입 자동 감지."""
    desc_lower = description.lower()
    scores = {t: 0 for t in TYPE_KEYWORDS}
    for dtype, keywords in TYPE_KEYWORDS.items():
        for kw in keywords:
            if kw in desc_lower:
                scores[dtype] += 1
    best = max(scores, key=scores.get)
    return best if scores[best] > 0 else "flowchart"


def _extract_mermaid(raw: str) -> str:
    """Ollama 응답에서 mermaid 코드 블록 추출."""
    lines = raw.strip().split("\n")
    in_block = False
    result = []
    for line in lines:
        if line.strip().startswith("```mermaid"):
            in_block = True
            continue
        if in_block and line.strip() == "```":
            break
        if in_block:
            result.append(line)
    if result:
        return "\n".join(result)
    # 코드 블록 없으면 그대로 반환
    return raw.strip()


def generate_diagram(description: str, diagram_type: str = None) -> str:
    """설명으로 Mermaid 다이어그램 생성."""
    if not lm_available():
        return "❌ Ollama 미실행 — 코드 생성 불가"

    if not diagram_type:
        diagram_type = _auto_detect_type(description)
        print(f"  [자동 감지] 다이어그램 타입: {diagram_type}")

    template = DIAGRAM_PROMPTS.get(diagram_type, DIAGRAM_PROMPTS["flowchart"])
    prompt = template.replace("{description}", description)

    print(f"  [생성 중] {diagram_type} 다이어그램...")
    raw = lm_chat(prompt, task="coding", max_tokens=1200, temperature=0.2)
    if not raw:
        return "❌ 다이어그램 생성 실패"

    diagram = _extract_mermaid(raw)
    return diagram


def save_diagram(diagram: str, output_path: str, description: str):
    """Markdown 파일로 저장."""
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(f"# {description}\n\n")
        f.write("```mermaid\n")
        f.write(diagram)
        f.write("\n```\n")
    print(f"\n💾 저장: {output_path}")


def interactive_mode():
    """대화형 모드."""
    print("\n🎨 Mermaid 다이어그램 생성기 (코다리)")
    print("=" * 50)
    print("지원 타입:", ", ".join(DIAGRAM_PROMPTS.keys()))
    print("타입 미입력 시 자동 감지\n")

    description = input("설명을 입력하세요: ").strip()
    dtype_input = input(f"다이어그램 타입 (Enter=자동): ").strip() or None

    if dtype_input and dtype_input not in DIAGRAM_PROMPTS:
        print(f"⚠️  알 수 없는 타입 '{dtype_input}' — 자동 감지로 전환")
        dtype_input = None

    diagram = generate_diagram(description, dtype_input)
    print("\n" + "=" * 50)
    print("```mermaid")
    print(diagram)
    print("```")

    save = input("\n저장할 파일명 (Enter=저장 안 함): ").strip()
    if save:
        if not save.endswith(".md"):
            save += ".md"
        save_diagram(diagram, save, description)


def main():
    parser = argparse.ArgumentParser(description="Mermaid 다이어그램 자동 생성")
    parser.add_argument("description", nargs="?", help="다이어그램 설명")
    parser.add_argument("--type", "-t", choices=list(DIAGRAM_PROMPTS.keys()),
                        help="다이어그램 타입 (미입력 시 자동 감지)")
    parser.add_argument("--output", "-o", help="출력 Markdown 파일 경로")
    parser.add_argument("--interactive", "-i", action="store_true", help="대화형 모드")
    args = parser.parse_args()

    if args.interactive or not args.description:
        interactive_mode()
        return

    diagram = generate_diagram(args.description, args.type)

    print("\n```mermaid")
    print(diagram)
    print("```")

    if args.output:
        save_diagram(diagram, args.output, args.description)


if __name__ == "__main__":
    main()
