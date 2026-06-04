import json
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# 즉시 반려 기준 — 이 키워드가 포함되면 Ollama 검토 없이 바로 거부
_HARD_BANNED = [
    "lofi", "lo-fi", "study beats", "sleep music", "white noise",
    "neon", "네온", "spam", "스팸", "adult", "성인", "도박", "불법",
    "이재명", "윤석열", "정치", "선거",
]


def await_approval(decision: str | dict) -> bool:
    """예원 CEO가 기획안의 브랜드 적합성을 평가합니다.

    판단 우선순위:
    1. 금지 키워드 하드체크 → 즉시 반려
    2. Ollama 소프트 평가 (충분한 토큰, 기본값 승인)
    3. Ollama 미응답/오류 → 자동 승인
    """
    decision_str = json.dumps(decision, ensure_ascii=False) if isinstance(decision, dict) else str(decision)
    print(f"⏳ [예원 CEO] 결재 검토 중...")

    # 1단계: 금지 키워드 하드체크
    decision_lower = decision_str.lower()
    for keyword in _HARD_BANNED:
        if keyword in decision_lower:
            print(f"❌ [예원 CEO] 금지 키워드 '{keyword}' 감지 → 반려")
            try:
                send_telegram_message(f"❌ [예원 CEO] 금지 키워드 '{keyword}' 포함 → 반려")
            except Exception:
                pass
            return False

    # 2단계: Ollama 소프트 평가
    if lm_available():
        prompt = (
            "당신은 인스타그램 채널 CEO 예원입니다. 아래 콘텐츠 기획안이 포스팅해도 괜찮은지 판단하세요.\n\n"
            f"기획안 요약:\n{decision_str[:500]}\n\n"
            "판단 기준 (아래 중 하나라도 해당되면 거부):\n"
            "- lofi/neon/study beats 등 금지 장르·키워드 포함\n"
            "- 정치·선거·도박·불법 관련 내용\n"
            "- 브랜드 이미지에 명백히 해로운 내용\n\n"
            "위 기준에 해당하면 'REJECTED', 괜찮으면 'APPROVED'로만 답하세요."
        )
        try:
            res = lm_chat(prompt, max_tokens=20, temperature=0.1)
            res_upper = (res or "").strip().upper()
            print(f"  [예원 CEO 평가] {repr(res_upper[:30])}")
            # REJECTED가 명확히 포함되고 APPROVED가 없을 때만 반려
            if "REJECTED" in res_upper and "APPROVED" not in res_upper:
                print("❌ [예원 CEO] 기획안 반려")
                try:
                    send_telegram_message(f"❌ [예원 CEO] 기획안 반려: {res_upper[:50]}")
                except Exception:
                    pass
                return False
        except Exception as e:
            print(f"  [예원 CEO] 평가 오류 → 자동 승인: {e}")

    print("✅ [예원 CEO] 기획안 승인")
    try:
        send_telegram_message(f"✅ [예원 CEO] 기획안 승인 완료")
    except Exception:
        pass
    return True


def ceo_coaching_on_rejection(agent: str, title: str, description: str, issues: list) -> dict:
    """가희에게 반려된 콘텐츠를 예원 CEO가 교정합니다."""
    print(f"👑 [예원 CEO] {agent} 반려 사유 분석 중...")

    corrected = {
        "title": title,
        "description": description,
        "caption": description,
        "directive": "금지 단어 제거 및 자연스러운 한국어 톤으로 재작성 요망.",
    }

    if not lm_available():
        return corrected

    issues_str = ", ".join(issues)
    prompt = (
        f"CEO 예원입니다. {agent}의 콘텐츠가 다음 이유로 반려됐습니다: {issues_str}\n\n"
        f"원본 캡션: {title}\n"
        f"원본 설명: {description}\n\n"
        "교정 규칙:\n"
        "- 금지어(lofi, neon, 네온 등) 완전 제거\n"
        "- 중복 단어 제거\n"
        "- 자연스러운 감성 한국어 유지\n\n"
        '아래 JSON만 출력하세요:\n'
        '{"title": "교정 캡션", "description": "교정 설명글", "directive": "수정 지시 요약"}'
    )
    try:
        res = lm_chat(prompt, max_tokens=600, temperature=0.5, json_mode=True)
        if res and res.strip():
            data = json.loads(res.strip())
            corrected["title"] = data.get("title", title)
            corrected["description"] = data.get("description", description)
            corrected["caption"] = data.get("description", description)
            corrected["directive"] = data.get("directive", corrected["directive"])
            print(f"👑 [예원 CEO 지시] {corrected['directive']}")
            try:
                send_telegram_message(
                    f"👑 [예원 CEO 지시서]\n수정대상: {agent}\n사유: {issues_str}\n지시: {corrected['directive']}"
                )
            except Exception:
                pass
    except Exception as e:
        print(f"⚠️ CEO 코칭 생성 실패: {e}")

    return corrected
