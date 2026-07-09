"""
skill_auditor.py — 예원(CEO): 에이전트 스킬 관리 감독·분석·검토

Ollama로 각 에이전트 SKILL.md를 읽어 분석:
  - 스킬 문서 완성도 / 전문성
  - 역할 중복·충돌 감지
  - 개선 제안
  - 주간 보고 → 텔레그램 전송

실행:
  python skill_auditor.py          # 전체 감사 + 보고
  python skill_auditor.py --check  # 분석만 (텔레그램 전송 없음)
"""
import os
import sys
import re
import json
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.llm import text as lm_chat, is_available as lm_available
from _shared.telegram import send
from _shared.env import find_root
_root = find_root(_here)

SKILLS_DIR = os.path.join(_root, "projects", "ai-team", "skills")
DRY_RUN    = "--check" in sys.argv


def _agent_display_name(folder_name: str) -> str:
    return folder_name.split("_", 1)[0]


def _build_agent_folder_map() -> dict[str, str]:
    """Discover auditable agents from the current skills directory."""
    folders: dict[str, str] = {}
    for folder in os.listdir(SKILLS_DIR):
        skill_path = os.path.join(SKILLS_DIR, folder, "SKILL.md")
        if os.path.exists(skill_path):
            folders[_agent_display_name(folder)] = folder
    return dict(sorted(folders.items(), key=lambda item: item[0]))


AGENT_FOLDER_MAP = _build_agent_folder_map()
AGENTS = list(AGENT_FOLDER_MAP.keys())


def _read_skill(agent: str) -> str | None:
    """에이전트 SKILL.md 읽기."""
    folder = AGENT_FOLDER_MAP.get(agent, agent)
    path = os.path.join(SKILLS_DIR, folder, "SKILL.md")
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as f:
        return f.read()


def _analyze_skill(agent: str, skill_content: str) -> dict:
    """Ollama로 스킬 분석 — 완성도·전문성·개선점."""
    # 너무 긴 스킬은 앞 3000자만 분석
    snippet = skill_content[:3000]

    prompt = (
        f"당신은 AI 에이전트 팀 CEO 예원이며, Anthropic 스킬 작성 베스트 프랙티스로 문서를 감사합니다.\n"
        f"다음은 에이전트 [{agent}]의 SKILL.md 내용입니다:\n\n"
        f"---\n{snippet}\n---\n\n"
        "평가 기준:\n"
        "1. 완성도 — 역할·주요 도구·데이터 흐름·책임 경계가 빠짐없이 기술됐나\n"
        "2. 전문성 — 도메인 판단 기준·규칙이 구체적이고 실행 가능한가\n"
        "3. 발견성 — description이 '무엇을 언제 담당하는지'를 트리거 키워드로 명확히 하고, "
        "다른 에이전트와 역할 경계가 겹치지 않게 구분되는가(핵심 베스트 프랙티스). "
        "개선사항에는 발견성·역할경계 문제를 우선 지적하라.\n\n"
        "아래 JSON 객체만 반환하세요. 마크다운, 설명문, 코드펜스는 쓰지 마세요.\n"
        '{"completeness": 1에서 10 사이 숫자, "expertise": 1에서 10 사이 숫자, '
        '"strengths": ["짧은 강점", "짧은 강점"], '
        '"improvements": ["발견성/역할경계 개선을 우선한 짧은 개선사항"], '
        '"one_line": "한 줄 요약"}'
    )

    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=400, temperature=0.5, lm_first=True)
        if raw:
            parsed = _parse_json_object(raw)
            if parsed:
                return _normalize_analysis(parsed)

    return {
        "completeness": _heuristic_score(skill_content),
        "expertise":    _heuristic_score(skill_content),
        "strengths":    [],
        "improvements": ["LLM JSON 파싱 실패로 휴리스틱 점수 사용"],
        "one_line":     "스킬 문서 구조 기반 휴리스틱 평가",
    }


def _parse_json_object(raw: str) -> dict | None:
    """Parse a JSON object even when the model wraps it in prose or fences."""
    text = raw.strip()
    candidates = [text]
    start = text.find("{")
    end = text.rfind("}")
    if start >= 0 and end > start:
        candidates.append(text[start:end + 1])
    for candidate in candidates:
        try:
            parsed = json.loads(candidate)
            return parsed if isinstance(parsed, dict) else None
        except Exception:
            continue
    return None


def _normalize_analysis(data: dict) -> dict:
    def score(value):
        if isinstance(value, (int, float)):
            n = float(value)
            if n > 10:
                n = n / 10 if n <= 100 else 10
            return max(1, min(10, round(n)))
        try:
            return max(1, min(10, round(float(str(value).strip()))))
        except Exception:
            return None

    return {
        "completeness": score(data.get("completeness")),
        "expertise": score(data.get("expertise")),
        "strengths": data.get("strengths") if isinstance(data.get("strengths"), list) else [],
        "improvements": data.get("improvements") if isinstance(data.get("improvements"), list) else [],
        "one_line": str(data.get("one_line") or "").strip()[:160],
    }


def _heuristic_score(skill_content: str) -> int:
    score_value = 4
    checks = [
        "description:",
        "##",
        "역할",
        "실행",
        "가이드",
        "체크",
        "에이전트",
        "tools",
    ]
    score_value += sum(1 for item in checks if item in skill_content)
    return max(1, min(10, score_value))


def _detect_overlaps(skills: dict[str, str]) -> list[str]:
    """에이전트 간 역할 중복·충돌 감지."""
    prompt = (
        "다음은 AI 에이전트 팀의 스킬 요약입니다. 역할 중복이나 충돌을 찾아주세요.\n\n"
    )
    for agent, content in skills.items():
        desc_match = re.search(r'description:\s*(.+)', content or "")
        desc = desc_match.group(1) if desc_match else "(설명 없음)"
        prompt += f"[{agent}]: {desc[:100]}\n"

    prompt += "\n중복·충돌이 있으면 2~3줄로 설명. 없으면 '없음' 반환."

    if lm_available():
        result = lm_chat(prompt, max_tokens=200, temperature=0.5, lm_first=True)
        if result:
            return [result.strip()]
    return []


def _build_report(results: dict[str, dict], overlaps: list[str], timestamp: str) -> str:
    """텔레그램 보고 메시지 생성."""
    lines = [f"📋 [CEO 예원] 주간 에이전트 스킬 문서 감사 보고 ({timestamp})\n"]

    # 에이전트별 스킬 문서 평가. 투자/매수판단 점수는 소미 분석 도구만 산출한다.
    lines.append("에이전트별 스킬 문서 평가:")
    for agent, r in results.items():
        c = r.get("completeness")
        e = r.get("expertise")
        if c is not None and e is not None:
            avg = (c + e) / 2
            stars = "⭐" * min(int(avg / 2), 5)
            score_str = f"{stars}(문서 {avg:.0f}/10)"
        else:
            score_str = "❓(분석불가)"
        one_line = r.get("one_line", "")
        imps = r.get("improvements", [])
        imp_str = f" → {imps[0]}" if imps and imps[0] not in {"Ollama 미응답 — 분석 불가", "LLM 응답 JSON 파싱 실패"} else ""
        lines.append(f"  {agent} {score_str} — {one_line}{imp_str}")

    # 중복 감지
    if overlaps and overlaps[0] != "없음":
        lines.append(f"\n⚠️ 역할 중복/충돌:\n  {overlaps[0]}")

    # 종합 개선 우선순위
    needs_work = sorted(
        [(a, r) for a, r in results.items()
         if r.get("completeness") is not None and r.get("completeness", 10) < 7],
        key=lambda x: x[1].get("completeness", 0)
    )
    if needs_work:
        lines.append("\n🔧 개선 우선순위:")
        for agent, r in needs_work[:3]:
            imps = r.get("improvements", [])
            if imps:
                lines.append(f"  [{agent}] {imps[0]}")

    return "\n".join(lines)


def run_audit() -> bool:
    """전체 스킬 감사 실행."""
    kst = datetime.timezone(datetime.timedelta(hours=9))
    timestamp = datetime.datetime.now(kst).strftime("%Y-%m-%d %H:%M KST")

    if not lm_available():
        msg = f"⚠️ [CEO 예원] 스킬 감사 실패 ({timestamp}) — Ollama 미실행"
        print(msg)
        # Ollama 관련 에러는 텔레그램 메시지를 전송하지 않음
        return False

    print(f"📋 [CEO 예원] 스킬 감사 시작 ({timestamp})")

    # 1. 스킬 파일 읽기
    skills = {}
    for agent in AGENTS:
        content = _read_skill(agent)
        if content:
            skills[agent] = content
            print(f"  ✅ {agent} SKILL.md 로드")
        else:
            print(f"  ⚠️ {agent} SKILL.md 없음")

    # 2. 각 에이전트 분석
    results = {}
    for agent, content in skills.items():
        print(f"  🔍 [{agent}] 분석 중...")
        results[agent] = _analyze_skill(agent, content)
        r = results[agent]
        print(
            f"     문서완성도={r.get('completeness')}/10 "
            f"문서전문성={r.get('expertise')}/10 — {r.get('one_line','')}"
        )

    # 3. 중복 감지
    print("  🔗 역할 중복 감지 중...")
    overlaps = _detect_overlaps(skills)

    # 4. 보고서 생성
    report = _build_report(results, overlaps, timestamp)
    print("\n" + report)

    # 5. 텔레그램 전송
    if not DRY_RUN:
        send(report)
        print("\n✅ 텔레그램 보고 완료")

    return True


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    run_audit()
