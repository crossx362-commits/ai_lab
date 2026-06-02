"""
agent_council.py — 자율 에이전트 회의 엔진

내부 문제 발생 시 에이전트들이 회의를 열고 합의된 패치를 적용한 뒤
예원 CEO 최종 승인을 받아 실행하고, 영숙이 텔레그램으로 보고한다.

회의 구조:
  경수 (원인 분석) → 코다리 (패치 제안) → 가희 (검수) → 예원 CEO (최종 승인)
"""
import os
import sys
import json
import re
import datetime
import traceback

_here = os.path.dirname(os.path.abspath(__file__))
if _here not in sys.path:
    sys.path.insert(0, _here)

try:
    from ollama_client import chat as lm_chat, is_available as lm_available
    from telegram_notifier import send_telegram_message
    from env_loader import find_project_root, load_env
except ImportError:
    from _shared.ollama_client import chat as lm_chat, is_available as lm_available
    from _shared.telegram_notifier import send_telegram_message
    from _shared.env_loader import find_project_root, load_env

_PROJECT_ROOT = find_project_root(_here)
load_env(_PROJECT_ROOT)

_COUNCIL_LOG = os.path.join(_PROJECT_ROOT, "reports", "learning", "council_log.jsonl")

# ─── 에이전트 페르소나 ────────────────────────────────────────────────────────

_GYEONGSU = """당신은 경수(수사관)입니다. 에러와 버그를 날카롭게 분석하는 전문가입니다.
주어진 오류 정보를 보고 다음을 짧고 명확하게 말하세요:
1. 근본 원인 (한 줄)
2. 의심되는 파일/함수
3. 재현 조건
4. 웹 서치가 필요한 기술 키워드 (있으면)
기술적으로 정확하고 추측하지 마세요. JSON 응답 금지, 일반 텍스트로 답하세요."""

_KODARI = """당신은 코다리(개발자)입니다. Python 코드 수정을 담당합니다.
경수의 분석을 받아 다음 JSON을 반드시 반환하세요:
{
  "target_file": "수정할 파일의 절대 경로 또는 null",
  "patch_type": "replace" 또는 "append" 또는 "none",
  "old_code": "교체할 기존 코드 (replace인 경우)",
  "new_code": "새 코드",
  "reason": "수정 이유 한 줄"
}
코드 수정이 불필요하면 patch_type을 "none"으로. 반드시 유효한 JSON만 반환."""

_GAHEE = """당신은 가희(검수관)입니다. 코다리의 패치를 검토합니다.
다음 JSON을 반드시 반환하세요:
{
  "approved": true 또는 false,
  "issues": ["문제점 목록, 없으면 빈 배열"],
  "comment": "한 줄 검수 의견"
}
패치가 안전하고 올바르면 approved: true. 반드시 유효한 JSON만 반환."""

_YEWON = """당신은 예원 CEO입니다. 팀의 최종 결재권자입니다.
가희의 검수 결과와 전체 회의 내용을 보고 최종 결정을 내립니다.
다음 JSON을 반드시 반환하세요:
{
  "final_approval": true 또는 false,
  "execute": true 또는 false,
  "ceo_comment": "결재 의견 한 줄"
}
승인 시 execute: true. 불확실하거나 위험하면 execute: false. 반드시 유효한 JSON만 반환."""


def _update_skill_knowledge(agent: str, topic: str, content: str):
    """웹 서치 결과를 에이전트 지식 파일에 자동 저장."""
    safe_topic = re.sub(r'[^\w가-힣]', '_', topic)[:40]
    now_str = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    kb_dir = os.path.join(_PROJECT_ROOT, "reports", "knowledge_base")
    os.makedirs(kb_dir, exist_ok=True)
    fname = os.path.join(kb_dir, f"{agent}_{safe_topic}_{now_str}.md")
    try:
        with open(fname, "w", encoding="utf-8") as f:
            f.write(f"# [{agent}] {topic}\n\n")
            f.write(f"_자동 학습: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M')}_\n\n")
            f.write(content)
        print(f"  [지식화] 저장: {os.path.basename(fname)}")
    except Exception as e:
        print(f"  [지식화] 실패: {e}")


def _parse_json_safe(text: str) -> dict | None:
    """LLM 응답에서 JSON 추출."""
    if not text:
        return None
    text = text.strip()
    match = re.search(r'\{.*\}', text, re.DOTALL)
    if match:
        try:
            return json.loads(match.group())
        except Exception:
            pass
    try:
        return json.loads(text)
    except Exception:
        return None


def _apply_patch(patch: dict) -> tuple[bool, str]:
    """코다리 패치를 실제 파일에 적용."""
    ptype = patch.get("patch_type", "none")
    if ptype == "none":
        return True, "패치 불필요"

    target = patch.get("target_file")
    if not target or not os.path.isfile(target):
        return False, f"파일 없음: {target}"

    try:
        with open(target, "r", encoding="utf-8") as f:
            content = f.read()

        if ptype == "replace":
            old = patch.get("old_code", "")
            new = patch.get("new_code", "")
            if not old:
                return False, "old_code 비어 있음"
            if old not in content:
                return False, "old_code를 파일에서 찾을 수 없음"
            content = content.replace(old, new, 1)
        elif ptype == "append":
            content = content + "\n" + patch.get("new_code", "")
        else:
            return False, f"알 수 없는 patch_type: {ptype}"

        with open(target, "w", encoding="utf-8") as f:
            f.write(content)
        return True, f"패치 적용: {os.path.basename(target)}"

    except Exception as e:
        return False, f"패치 실패: {e}"


def _log_council(record: dict):
    """회의 결과를 jsonl에 기록."""
    os.makedirs(os.path.dirname(_COUNCIL_LOG), exist_ok=True)
    try:
        with open(_COUNCIL_LOG, "a", encoding="utf-8") as f:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")
    except Exception:
        pass


def convene(
    problem_summary: str,
    error_traceback: str = "",
    context_file: str = "",
    caller_agent: str = "시스템",
) -> dict:
    """
    에이전트 회의를 소집하고 결과를 반환한다.

    Args:
        problem_summary: 발생한 문제 요약
        error_traceback: 스택 트레이스 (선택)
        context_file: 문제가 발생한 파일 경로 (선택)
        caller_agent: 회의를 소집한 에이전트 이름

    Returns:
        {
            "success": bool,
            "executed": bool,
            "summary": str,
            "patch": dict or None,
        }
    """
    if not lm_available():
        msg = "⚠️ [에이전트 회의] Ollama 오프라인 — 회의 불가. 수동 확인 필요."
        send_telegram_message(msg)
        return {"success": False, "executed": False, "summary": msg, "patch": None}

    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    print(f"\n{'='*55}")
    print(f"  [에이전트 회의] {now} — 소집: {caller_agent}")
    print(f"  문제: {problem_summary[:80]}")
    print(f"{'='*55}")

    send_telegram_message(
        f"🚨 [에이전트 회의 소집]\n"
        f"소집자: {caller_agent}\n"
        f"문제: {problem_summary[:200]}"
    )

    problem_context = f"문제: {problem_summary}"
    if error_traceback:
        problem_context += f"\n\n에러 트레이스:\n{error_traceback[:1500]}"
    if context_file:
        problem_context += f"\n\n관련 파일: {context_file}"

    # ── Round 1: 경수 원인 분석 ───────────────────────────────────────────────
    print("\n🔍 [경수] 원인 분석 중...")
    gyeongsu_resp = lm_chat(
        problem_context,
        system=_GYEONGSU,
        max_tokens=400,
        temperature=0.3,
    ) or "분석 실패"
    print(f"  경수: {gyeongsu_resp[:150]}...")

    # ── Round 1.5: 웹 서치 (필요 시) + 지식화 ───────────────────────────────
    web_knowledge = ""
    try:
        from gemini_client import web_search
    except ImportError:
        try:
            from _shared.gemini_client import web_search
        except ImportError:
            web_search = None

    if web_search:
        # 경수 분석에서 키워드 추출 후 서치
        search_query = f"Python {problem_summary[:80]} 해결 방법 best practice 2025"
        print(f"\n🌐 [코다리] 웹 서치 중: {search_query[:60]}...")
        search_result = web_search(search_query, max_tokens=800)
        if search_result:
            web_knowledge = search_result
            print(f"  웹 서치 결과: {search_result[:120]}...")
            # 지식 파일 자동 업데이트
            _update_skill_knowledge(
                agent="코다리",
                topic=problem_summary[:60],
                content=f"문제: {problem_summary}\n\n웹 서치 결과:\n{search_result}",
            )

    # ── Round 2: 코다리 패치 제안 ─────────────────────────────────────────────
    print("\n🛠️ [코다리] 패치 제안 중...")
    kodari_input = (
        f"{problem_context}\n\n"
        f"경수 분석:\n{gyeongsu_resp}\n\n"
        + (f"웹 서치 참고 자료:\n{web_knowledge}\n\n" if web_knowledge else "")
        + f"프로젝트 루트: {_PROJECT_ROOT}"
    )
    kodari_resp = lm_chat(
        kodari_input,
        system=_KODARI,
        json_mode=True,
        max_tokens=600,
        temperature=0.2,
    ) or "{}"
    patch = _parse_json_safe(kodari_resp) or {"patch_type": "none", "reason": "파싱 실패"}
    print(f"  코다리: patch_type={patch.get('patch_type')}, reason={patch.get('reason','?')[:80]}")

    # ── Round 3: 가희 검수 ────────────────────────────────────────────────────
    print("\n🧐 [가희] 패치 검수 중...")
    gahee_input = (
        f"경수 분석:\n{gyeongsu_resp}\n\n"
        f"코다리 패치:\n{json.dumps(patch, ensure_ascii=False)}"
    )
    gahee_resp = lm_chat(
        gahee_input,
        system=_GAHEE,
        json_mode=True,
        max_tokens=300,
        temperature=0.2,
    ) or "{}"
    review = _parse_json_safe(gahee_resp) or {"approved": False, "issues": ["파싱 실패"], "comment": ""}
    print(f"  가희: approved={review.get('approved')}, {review.get('comment','')[:80]}")

    # ── Round 4: 예원 CEO 최종 결재 ───────────────────────────────────────────
    print("\n👑 [예원 CEO] 최종 결재 중...")
    yewon_input = (
        f"문제: {problem_summary}\n\n"
        f"경수 분석: {gyeongsu_resp[:300]}\n\n"
        f"코다리 패치: {json.dumps(patch, ensure_ascii=False)[:400]}\n\n"
        f"가희 검수: approved={review.get('approved')}, {review.get('comment','')}\n"
        f"issues: {review.get('issues', [])}"
    )
    yewon_resp = lm_chat(
        yewon_input,
        system=_YEWON,
        json_mode=True,
        max_tokens=200,
        temperature=0.1,
    ) or "{}"
    decision = _parse_json_safe(yewon_resp) or {
        "final_approval": False,
        "execute": False,
        "ceo_comment": "파싱 실패 — 수동 확인 필요",
    }
    print(f"  예원: final_approval={decision.get('final_approval')}, execute={decision.get('execute')}")
    print(f"  예원 코멘트: {decision.get('ceo_comment','')}")

    # ── 패치 실행 ─────────────────────────────────────────────────────────────
    executed = False
    exec_result = "실행 안 함"

    if decision.get("execute") and decision.get("final_approval"):
        ok, exec_result = _apply_patch(patch)
        executed = ok
        if ok:
            print(f"\n✅ 패치 적용 완료: {exec_result}")
        else:
            print(f"\n❌ 패치 실패: {exec_result}")

    # ── 회의록 저장 ───────────────────────────────────────────────────────────
    record = {
        "timestamp": now,
        "caller": caller_agent,
        "problem": problem_summary[:300],
        "gyeongsu": gyeongsu_resp[:400],
        "patch": patch,
        "review": review,
        "decision": decision,
        "executed": executed,
        "exec_result": exec_result,
    }
    _log_council(record)

    # ── 텔레그램 보고 ─────────────────────────────────────────────────────────
    status_icon = "✅" if executed else ("🟡" if decision.get("final_approval") else "❌")
    issues_str = "\n".join(f"  • {i}" for i in review.get("issues", [])) or "  없음"
    report = (
        f"{status_icon} [에이전트 회의 결과]\n\n"
        f"📋 문제: {problem_summary[:150]}\n\n"
        f"🔍 경수 분석: {gyeongsu_resp[:200]}\n\n"
        f"🛠️ 코다리 패치: {patch.get('reason', '없음')}\n"
        f"🧐 가희 검수: {review.get('comment', '없음')}\n"
        f"  지적 사항:\n{issues_str}\n\n"
        f"👑 예원 결재: {decision.get('ceo_comment', '없음')}\n\n"
        f"{'✅ 패치 자동 적용 완료' if executed else ('🔧 패치 준비됨 (미실행)' if decision.get('final_approval') else '⚠️ 수동 확인 필요')}: {exec_result}"
    )
    send_telegram_message(report)

    summary = f"회의완료|executed={executed}|{decision.get('ceo_comment','')[:80]}"
    print(f"\n{'='*55}\n  [회의 종료] {summary}\n{'='*55}\n")

    return {
        "success": True,
        "executed": executed,
        "summary": summary,
        "patch": patch,
        "decision": decision,
    }


def convene_from_exception(
    exc: Exception,
    context_file: str = "",
    caller_agent: str = "시스템",
) -> dict:
    """예외 객체로부터 바로 회의 소집 (파이프라인 except 블록에서 사용)."""
    tb = traceback.format_exc()
    problem = f"{type(exc).__name__}: {str(exc)[:200]}"
    return convene(
        problem_summary=problem,
        error_traceback=tb,
        context_file=context_file,
        caller_agent=caller_agent,
    )


if __name__ == "__main__":
    # 직접 실행 테스트
    result = convene(
        problem_summary="테스트: agent_council 자가진단",
        error_traceback="",
        caller_agent="코다리",
    )
    print("결과:", result)
