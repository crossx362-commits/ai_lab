import os
import json
import re
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

def await_approval(decision: str | dict) -> bool:
    """예원 CEO가 기획안(제목, 설명, 플랫폼 등)의 브랜드 적합성을 평가하고 결재합니다."""
    decision_str = json.dumps(decision, ensure_ascii=False, indent=2) if isinstance(decision, dict) else str(decision)
    print(f"⏳ [예원 CEO] 결재 검토 중...\n{decision_str}")
    
    # 텔레그램으로 승인 요청 알림 전송
    try:
        send_telegram_message(f"🔔 [예원 CEO 결재 대기]\n{decision_str[:300]}")
    except Exception as e:
        print(f"⚠️ 승인 알림 전송 실패: {e}")
        
    # Ollama를 통해 실제 CEO 평가 진행
    if lm_available():
        prompt = (
            f"당신은 최고경영자(CEO) 예원입니다. 아래 에이전트의 콘텐츠 기획안을 평가해주세요.\n\n"
            f"기획안:\n{decision_str}\n\n"
            f"평가 기준:\n"
            f"1. 상업적 가치 및 대중성 여부\n"
            f"2. 브랜드 이미지 적합성 및 안정성\n"
            f"3. 가희(검수관)의 규칙 위반 가능성 ('neon/네온', 'lofi' 등 금지 키워드 포함 여부)\n\n"
            f"이 기획안을 승인한다면 'APPROVED', 승인할 수 없다면 'REJECTED' 단어 하나만 반환하세요."
        )
        try:
            res = lm_chat(prompt, max_tokens=10, temperature=0.2)
            if res and "REJECTED" in res.upper():
                print("❌ [예원 CEO] 기획안 결재 반려")
                send_telegram_message(f"❌ [예원 CEO] 기획안이 브랜드 부적합 또는 정책 위반 가능성으로 결재 반려되었습니다.")
                return False
        except Exception as e:
            print(f"⚠️ CEO 평가 중 오류 발생 (자동 통과 처리): {e}")

    print("✅ [예원 CEO] 기획안 최종 승인 완료")
    return True


def ceo_coaching_on_rejection(agent: str, title: str, description: str, issues: list) -> dict:
    """가희에게 반려된 콘텐츠의 원인을 예원 CEO가 분석하여 교정본과 수정 지시서를 제공합니다."""
    print(f"👑 [예원 CEO] {agent}의 반려 사유 분석 및 피드백 생성 중...")
    
    issues_str = ", ".join(issues)
    
    # 기본 폴백값 세팅
    corrected = {
        "title": title,
        "description": description,
        "caption": description, # 인스타 캡션용
        "directive": "금지 단어 및 중복 단어 자제 요망."
    }
    
    if lm_available():
        prompt = (
            f"당신은 최고경영자(CEO) 예원입니다. {agent} 에이전트가 만든 콘텐츠가 품질 검수에서 반려되었습니다.\n"
            f"반려 사유(피드백): {issues_str}\n"
            f"이전 작성 내용:\n"
            f"- 제목/캡션: {title}\n"
            f"- 설명글: {description}\n\n"
            f"반려 사유를 정밀 분석하여, 가희(검수관)의 검수를 확실히 통과할 수 있도록 교정하고 수정 지시를 내려주세요.\n"
            f"교정 규칙:\n"
            f"- 'neon/네온', 'lofi' 등 모든 금지어 완벽 제거 및 대체\n"
            f"- 동일 텍스트 내 2자 이상의 의미 단어 중복 사용 완전 제거\n"
            f"- 자연스럽고 감성적인 한국어 톤앤매너 유지\n\n"
            f"아래 JSON 포맷으로만 응답하세요. (설명이나 다른 말 일체 금지):\n"
            f'{{"title": "교정된 제목 또는 캡션", "description": "교정된 설명글(인스타 캡션 포함)", "directive": "CEO의 구체적 수정 지시 요약"}}'
        )
        try:
            res = lm_chat(prompt, max_tokens=600, temperature=0.5, json_mode=True)
            if res and res.strip():
                data = json.loads(res.strip())
                corrected["title"] = data.get("title", title)
                corrected["description"] = data.get("description", description)
                corrected["caption"] = data.get("description", description)
                corrected["directive"] = data.get("directive", corrected["directive"])
                
                print(f"👑 [예원 CEO 지시서 발행] 지시: {corrected['directive']}")
                send_telegram_message(
                    f"👑 <b>[예원 CEO 지시서 발행]</b>\n"
                    f"수정 대상: {agent} | 문제: {issues_str}\n"
                    f"<b>지시사항</b>: {corrected['directive']}\n"
                    f"→ 교정본이 담당 에이전트의 수정 루프에 자동으로 주입됩니다."
                )
        except Exception as e:
            print(f"⚠️ CEO 코칭 생성 실패: {e}")
            
    return corrected
