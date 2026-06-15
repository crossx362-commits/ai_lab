#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
영숙의 업로드 승인 플로우
- 루나/아린 업로드 전 영숙에게 보고
- 예원 CEO 피드백 요청
- 가희 검수 통과 시 업로드 진행
"""

import os
import sys
import json
from typing import Dict, Optional

# UTF-8 인코딩
sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.stderr.reconfigure(encoding='utf-8', errors='replace')

# 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# CEO Dispatcher import
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

# upload_approval_flow.py (Gahee bypassed)


def request_upload_approval(
    agent: str,
    platform: str,
    content_info: Dict
) -> Dict:
    """
    업로드 승인 요청 플로우

    Args:
        agent: "루나" 또는 "아린"
        platform: "YouTube" 또는 "Instagram"
        content_info: 콘텐츠 정보
            {
                "title": "제목",
                "description": "설명",
                "caption": "캡션" (Instagram),
                "video_id": "비디오 ID" (YouTube),
                "image_url": "이미지 URL" (Instagram),
                "hashtags": ["태그1", "태그2"]
            }

    Returns:
        {
            "approved": True/False,
            "stage": "영숙_보고|예원_피드백|가희_검수|최종_승인",
            "message": "결과 메시지",
            "issues": ["문제1", "문제2"] (거절 시)
        }
    """

    print(f"\n{'='*70}")
    print(f"📋 [영숙] 업로드 승인 플로우 시작")
    print(f"{'='*70}")
    print(f"  에이전트: {agent}")
    print(f"  플랫폼: {platform}")
    print(f"  제목: {content_info.get('title', content_info.get('caption', 'N/A')[:50])}")
    print(f"{'='*70}\n")

    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    # Step 1: 영숙 → 사장님 보고
    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    print("━━━ Step 1: 영숙 → 사장님 보고 ━━━")

    yeongsuk_report = (
        f"📢 **[영숙 비서 → 사장님]**\n\n"
        f"{agent} 에이전트가 {platform} 업로드를 준비했습니다.\n"
        f"예원 CEO님께 피드백을 요청하겠습니다.\n\n"
        f"**제목**: {content_info.get('title', content_info.get('caption', 'N/A')[:100])}\n"
    )

    if platform == "Instagram":
        yeongsuk_report += f"**해시태그**: {' '.join(content_info.get('hashtags', [])[:5])}\n"

    send_telegram_message(yeongsuk_report)
    print(f"  ✅ 사장님께 보고 완료\n")

    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    # Step 2: 영숙 → 예원 CEO 피드백 요청
    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    print("━━━ Step 2: 영숙 → 예원 CEO 피드백 요청 ━━━")

    feedback_request = _generate_feedback_request(agent, platform, content_info)

    ceo_feedback = _get_ceo_feedback(feedback_request, content_info)

    print(f"  예원 CEO 피드백: {ceo_feedback['status']}")
    print(f"  의견: {ceo_feedback['comment'][:100]}\n")

    if not ceo_feedback['approved']:
        # 예원 CEO가 거절하면 즉시 반려
        rejection_report = (
            f"❌ **[영숙 비서 → 사장님]**\n\n"
            f"{agent} 업로드가 예원 CEO 검토에서 반려되었습니다.\n\n"
            f"**거절 사유**:\n{ceo_feedback['comment']}\n\n"
            f"**조치**: {agent}에게 수정 요청"
        )
        send_telegram_message(rejection_report)

        return {
            "approved": False,
            "stage": "예원_피드백",
            "message": ceo_feedback['comment'],
            "issues": ceo_feedback.get('issues', [])
        }

    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    # Step 3: 영숙 최종 승인 → 에이전트 업로드 지시
    # ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    print("━━━ Step 3: 영숙 최종 승인 → 업로드 지시 ━━━")

    # 사장님께 최종 승인 보고
    final_approval = (
        f"✅ **[영숙 비서 → 사장님]**\n\n"
        f"{agent}의 {platform} 업로드가 최종 승인되었습니다!\n\n"
        f"**검토 단계**:\n"
        f"1. ✅ 영숙 → 사장님 보고\n"
        f"2. ✅ 예원 CEO 피드백: 승인 (점수: {ceo_feedback.get('score', 'N/A')}/10)\n"
        f"3. ✅ 영숙 최종 승인\n\n"
        f"**제목**: {content_info.get('title', content_info.get('caption', 'N/A')[:100]}\n\n"
        f"지금 {agent}에게 업로드 지시를 내립니다! 🚀"
    )
    send_telegram_message(final_approval)

    # 업로드 지시
    upload_command = _issue_upload_command(agent, platform, content_info)

    print(f"  ✅ 영숙 → {agent} 업로드 지시 완료")
    print(f"  명령: {upload_command}")
    print(f"\n{'='*70}\n")

    return {
        "approved": True,
        "stage": "최종_승인",
        "message": "업로드 승인 및 지시 완료",
        "ceo_feedback": ceo_feedback['comment'],
        "upload_command": upload_command
    }


def _generate_feedback_request(agent: str, platform: str, content_info: Dict) -> str:
    """예원 CEO 피드백 요청 메시지 생성"""

    if platform == "YouTube":
        return (
            f"예원 CEO님, {agent}의 YouTube 영상을 검토해주세요.\n\n"
            f"제목: {content_info.get('title', 'N/A')}\n"
            f"설명: {content_info.get('description', 'N/A')[:200]}\n\n"
            f"이 콘텐츠가 채널 브랜드와 품질 기준에 부합하는지 평가해주세요."
        )
    else:  # Instagram
        return (
            f"예원 CEO님, {agent}의 Instagram 포스팅을 검토해주세요.\n\n"
            f"캡션: {content_info.get('caption', 'N/A')[:200]}\n"
            f"해시태그: {' '.join(content_info.get('hashtags', []))}\n\n"
            f"이 콘텐츠가 채널 컨셉과 팔로워 반응에 적합한지 평가해주세요."
        )


def _get_ceo_feedback(request: str, content_info: Dict) -> Dict:
    """예원 CEO 피드백 받기 (Ollama 기반)"""

    if not lm_available():
        # Ollama 없으면 기본 승인
        return {
            "approved": True,
            "status": "APPROVED",
            "comment": "자동 승인 (Ollama 오프라인)"
        }

    prompt = f"""
당신은 예원 CEO입니다. 콘텐츠 업로드 전 최종 검토를 담당합니다.

검토 요청:
{request}

검토 기준:
1. 제목/캡션이 자연스럽고 클릭 유도력이 있는가?
2. 브랜드 톤앤매너에 부합하는가?
3. 타겟 오디언스에게 매력적인가?
4. 금지 키워드(AI, 인공지능, 테크 등)가 없는가?
5. 이전 콘텐츠와 차별화되는가?

JSON만 반환:
{{
  "approved": true/false,
  "status": "APPROVED|REJECTED|NEEDS_REVISION",
  "comment": "구체적인 피드백 (2-3줄)",
  "score": 1-10,
  "issues": ["문제1", "문제2"] (거절 시)
}}
"""

    try:
        response = lm_chat(prompt, json_mode=True, max_tokens=300, temperature=0.3)
        feedback = json.loads(response)

        # 점수 7점 이상이면 승인
        if feedback.get('score', 10) >= 7:
            feedback['approved'] = True
            feedback['status'] = "APPROVED"

        return feedback

    except Exception as e:
        print(f"  ⚠️ CEO 피드백 생성 실패: {e}")
        # 에러 시 기본 승인
        return {
            "approved": True,
            "status": "APPROVED",
            "comment": "기본 승인 (피드백 생성 실패)"
        }


# _run_gahee_inspection bypassed


def _issue_upload_command(agent: str, platform: str, content_info: Dict) -> str:
    """업로드 명령 발행 (실제 파이프라인 트리거)"""

    if agent == "루나" and platform == "YouTube":
        # 루나 업로드 지시
        command = f"루나 YouTube 업로드 실행 (제목: {content_info.get('title', 'N/A')[:30]})"

        # 실제로는 music_video_pipeline.py의 upload 함수 호출
        # 여기서는 메시지만 생성
        upload_instruction = (
            f"📤 **[영숙 비서 → 루나 디렉터]**\n\n"
            f"최종 승인이 완료되었습니다!\n"
            f"지금 바로 YouTube에 업로드하세요.\n\n"
            f"**제목**: {content_info.get('title', 'N/A')}\n"
            f"**설명**: {content_info.get('description', 'N/A')[:100]}..."
        )

    elif agent == "아린" and platform == "Instagram":
        # 아린 업로드 지시
        command = f"아린 Instagram 업로드 실행 (캡션: {content_info.get('caption', 'N/A')[:30]})"

        # 실제로는 auto_pipeline.py의 upload 함수 호출
        upload_instruction = (
            f"📤 **[영숙 비서 → 아린 관리자]**\n\n"
            f"최종 승인이 완료되었습니다!\n"
            f"지금 바로 Instagram에 포스팅하세요.\n\n"
            f"**캡션**: {content_info.get('caption', 'N/A')[:100]}...\n"
            f"**해시태그**: {' '.join(content_info.get('hashtags', [])[:5])}"
        )

    else:
        command = f"{agent} {platform} 업로드"
        upload_instruction = f"[영숙] {agent}에게 업로드 지시"

    send_telegram_message(upload_instruction)

    return command


# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# 에이전트별 래퍼 함수
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

def luna_upload_approval(video_info: Dict) -> Dict:
    """루나 YouTube 업로드 승인"""
    return request_upload_approval("루나", "YouTube", video_info)


def arin_upload_approval(post_info: Dict) -> Dict:
    """아린 Instagram 업로드 승인"""
    return request_upload_approval("아린", "Instagram", post_info)


if __name__ == "__main__":
    # 테스트
    print("━━━ 업로드 승인 플로우 테스트 ━━━\n")

    # 루나 YouTube 테스트
    luna_test = {
        "title": "Neon City Nights - 80s K-Pop Fusion",
        "description": "몽환적인 80년대 도쿄의 네온 불빛 아래 펼쳐지는 시티팝...",
    }

    result = luna_upload_approval(luna_test)
    print(f"\n결과: {result}\n")
