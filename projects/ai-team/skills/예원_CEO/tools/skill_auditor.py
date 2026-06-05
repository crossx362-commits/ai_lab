"""
skill_auditor.py — 예원(CEO): 에이전트 스킬 관리 감독·분석·검토

Ollama로 각 에이전트 SKILL.md를 읽어 분석:
  - 완성도 / 전문성 점수
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
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.telegram_notifier import send_telegram_message
from _shared.env_loader import find_project_root
_root = find_project_root(_here)

SKILLS_DIR = os.path.join(_root, "projects", "ai-team", "skills")
DRY_RUN    = "--check" in sys.argv

AGENT_FOLDER_MAP = {
    "루나": "루나_디렉터",
    "아린": "아린_관리자",
    "가희": "가희_검수관",
    "현빈": "현빈_전략가",
    "영숙": "영숙_비서",
    "경수": "경수_수사관",
    "코다리": "코다리_개발자",
    "티모": "티모_디자이너",
    "로율": "로율_변호사",
    "케빈": "케빈_인프라",
}
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
        f"당신은 AI 에이전트 팀을 관리하는 CEO 예원입니다.\n"
        f"다음은 에이전트 [{agent}]의 SKILL.md 내용입니다:\n\n"
        f"---\n{snippet}\n---\n\n"
        f"이 스킬을 다음 기준으로 평가하고 JSON으로만 반환하세요:\n"
        f'{{"completeness": 점수(1-10), "expertise": 점수(1-10), '
        f'"strengths": ["강점1", "강점2"], '
        f'"improvements": ["개선사항1", "개선사항2"], '
        f'"one_line": "한 줄 요약"}}'
    )

    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=400, temperature=0.5)
        if raw:
            try:
                return json.loads(raw.strip())
            except Exception:
                pass

    return {
        "completeness": None,
        "expertise":    None,
        "strengths":    [],
        "improvements": ["Ollama 미응답 — 분석 불가"],
        "one_line":     "분석 실패 (Ollama 미응답)",
    }


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
        result = lm_chat(prompt, max_tokens=200, temperature=0.5)
        if result:
            return [result.strip()]
    return []


def _build_report(results: dict[str, dict], overlaps: list[str], timestamp: str) -> str:
    """텔레그램 보고 메시지 생성."""
    lines = [f"📋 [CEO 예원] 주간 에이전트 스킬 감사 보고 ({timestamp})\n"]

    # 에이전트별 평가
    lines.append("에이전트별 평가:")
    for agent, r in results.items():
        c = r.get("completeness")
        e = r.get("expertise")
        if c is not None and e is not None:
            avg = (c + e) / 2
            stars = "⭐" * min(int(avg / 2), 5)
            score_str = f"{stars}({avg:.0f}/10)"
        else:
            score_str = "❓(분석불가)"
        one_line = r.get("one_line", "")
        imps = r.get("improvements", [])
        imp_str = f" → {imps[0]}" if imps and imps[0] != "Ollama 미응답 — 분석 불가" else ""
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
        print(f"     완성도={r.get('completeness')}/10 전문성={r.get('expertise')}/10 — {r.get('one_line','')}")

    # 3. 중복 감지
    print("  🔗 역할 중복 감지 중...")
    overlaps = _detect_overlaps(skills)

    # 4. 보고서 생성
    report = _build_report(results, overlaps, timestamp)
    print("\n" + report)

    # 5. 텔레그램 전송
    if not DRY_RUN:
        send_telegram_message(report)
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
