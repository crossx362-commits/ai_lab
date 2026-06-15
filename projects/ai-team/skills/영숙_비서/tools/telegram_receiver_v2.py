"""
영숙 텔레그램 봇 - Gemini Function Calling 최적화 버전

사용자가 대충 명령해도 알아듣고 자동으로 적절한 도구를 실행합니다.
"""
import os
import sys
import json
import time
import traceback
from datetime import datetime, timezone, timedelta

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

from google import genai
from google.genai import types

# Gemini 클라이언트 초기화
API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
client = genai.Client(api_key=API_KEY) if API_KEY else None

# 텔레그램 설정
TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# CEO Dispatcher 임포트
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
try:
    import yewon_dispatcher
except ImportError:
    yewon_dispatcher = None
    print("  [Warning] CEO Dispatcher 로드 실패")

# ══════════════════════════════════════════════════════════════════════════════
# 영숙 페르소나 및 시스템 프롬프트
# ══════════════════════════════════════════════════════════════════════════════

YEONGSUK_PERSONA = """당신은 영숙입니다. 사장님의 개인 비서입니다.

성격:
- 밝고 똑똑하며, 사장님을 최우선으로 생각합니다
- 핵심만 짧고 명확하게 답합니다
- 불필요한 인사나 미사여구는 생략합니다

업무 처리 원칙:
1. 사용자가 "현황 보고", "상태 확인", "어떻게 돼가?" 등 → get_agent_status 도구 사용
2. 사용자가 "일정", "스케줄", "캘린더" 언급 → 캘린더 관련 도구 사용
3. 사용자가 "루나 영상 만들어", "인스타 올려", "포스팅 해" 등 실행 명령 → dispatch_to_agents 도구 사용
4. 도구 실행 결과를 받으면 그 내용을 바탕으로 간단히 요약하여 사장님께 보고

중요:
- 임의로 정보를 만들어내지 마세요
- 도구를 사용할 수 있으면 반드시 사용하세요
- 도구가 없으면 솔직하게 말하세요
"""

CHAT_HISTORY = []  # 대화 히스토리


# ══════════════════════════════════════════════════════════════════════════════
# 텔레그램 API
# ══════════════════════════════════════════════════════════════════════════════

def telegram_api(method: str, payload: dict, timeout: int = 20) -> dict:
    """텔레그램 API 호출"""
    import urllib.request
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"  [Telegram API Error] {method}: {e}")
        return {"ok": False, "error": str(e)}


def send_telegram_message(text: str):
    """텔레그램 메시지 전송"""
    print(f"  📤 [영숙 → 사장님] {text[:80]}...")
    return telegram_api("sendMessage", {
        "chat_id": CHAT_ID,
        "text": text,
        "parse_mode": "HTML"
    })


def get_telegram_updates(offset: int) -> list:
    """텔레그램 업데이트 가져오기"""
    res = telegram_api("getUpdates", {
        "offset": offset,
        "timeout": 30,
        "allowed_updates": ["message"]
    }, timeout=40)
    return res.get("result", [])


# ══════════════════════════════════════════════════════════════════════════════
# 도구 함수 정의 (Gemini Function Calling)
# ══════════════════════════════════════════════════════════════════════════════

def get_agent_status(agent: str = "전체") -> str:
    """에이전트들의 최근 작업 현황 및 로그를 조회합니다.

    이 도구는 다음과 같은 요청 시 사용하세요:
    - "루나 어떻게 돼가?", "아린 상태 확인", "데이브 뭐해?"
    - "현황 보고해줘", "상태 알려줘", "다들 뭐하고 있어?"
    - "전체 현황", "에이전트 상태"

    Args:
        agent: 조회할 에이전트 ('루나' / '아린' / '데이브' / '전체')
               - '루나': YouTube 영상 제작 담당
               - '아린': Instagram 포스팅 담당
               - '데이브': 가상자산/주식 분석 담당
               - '전체': 모든 에이전트 현황

    Returns:
        str: 에이전트 현황 보고 (최근 업로드, 로그, 작업 상태 등)
    """
    from _shared.agent_status import get_status_report
    return get_status_report(agent, PROJECT_ROOT)


def list_calendar_events(days_ahead: int = 7) -> str:
    """구글 캘린더 일정을 조회합니다.

    이 도구는 다음과 같은 요청 시 사용하세요:
    - "일정 보여줘", "이번주 일정", "다음주 스케줄"
    - "언제 약속 있어?", "캘린더 확인"

    Args:
        days_ahead: 조회할 미래 일수 (기본 7일, 최대 30일)

    Returns:
        str: 일정 목록 (날짜, 시간, 제목, 장소 등)
    """
    cache = os.path.join(PROJECT_ROOT, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            content = f.read()
        return f"📅 일정 목록:\n\n{content[:1500]}"
    return "📅 캘린더 연동이 아직 설정되지 않았어요."


def create_calendar_event(
    title: str,
    start_datetime: str,
    duration_minutes: int = 60,
    description: str = "",
    location: str = ""
) -> str:
    """구글 캘린더에 새 일정을 등록합니다.

    이 도구는 다음과 같은 요청 시 사용하세요:
    - "내일 오후 3시 회의 잡아줘"
    - "다음주 월요일 10시 약속 등록"
    - "6월 20일 14시 점심약속 캘린더에 추가"

    Args:
        title: 일정 제목 (예: "팀 회의", "점심 약속")
        start_datetime: 시작 일시 (ISO 형식: "2026-06-20T14:00:00")
        duration_minutes: 지속 시간(분) (기본 60분)
        description: 일정 설명 (선택사항)
        location: 장소 (선택사항)

    Returns:
        str: 등록 성공/실패 메시지
    """
    return "⚠️ 캘린더 등록 기능은 현재 설정 중입니다."


def dispatch_to_agents(command: str) -> str:
    """에이전트에게 작업을 지시하여 실행합니다.

    이 도구는 다음과 같은 실행 명령 시 사용하세요:
    - "루나 영상 만들어줘", "YouTube 영상 하나 올려"
    - "인스타 포스팅 해줘", "아린 사진 올려"
    - "데이브 분석 돌려", "비트코인 분석해줘"

    중요: 이 도구는 실제로 작업을 실행하므로 신중하게 사용하세요.

    Args:
        command: 실행할 명령 (예: "루나 영상 제작", "인스타 포스팅")

    Returns:
        str: 작업 실행 결과
    """
    if not yewon_dispatcher:
        return "❌ CEO 디스패처가 로드되지 않아서 실행할 수 없어요."

    try:
        print(f"  🎯 [CEO 디스패처 호출] {command}")
        result = yewon_dispatcher.dispatch_and_execute(command)

        if result is None:
            return "⚠️ CEO가 복구 대기 중이라 실행되지 않았습니다."

        # 결과 요약
        if len(result) > 500:
            summary_prompt = f"다음 결과를 2-3줄로 요약해줘. 성공/실패 여부와 핵심 정보만:\n\n{result[:1000]}"
            try:
                summary_res = client.models.generate_content(
                    model="gemini-1.5-flash",
                    contents=summary_prompt,
                    config=types.GenerateContentConfig(max_output_tokens=150)
                )
                if summary_res.text:
                    return f"✅ {summary_res.text.strip()}"
            except Exception:
                pass

        return result

    except Exception as e:
        return f"❌ 실행 오류: {str(e)[:200]}"


# ══════════════════════════════════════════════════════════════════════════════
# Gemini Function Calling 핸들러
# ══════════════════════════════════════════════════════════════════════════════

# 도구 매핑
TOOLS = [
    get_agent_status,
    list_calendar_events,
    create_calendar_event,
    dispatch_to_agents
]

TOOL_MAP = {
    "get_agent_status": get_agent_status,
    "list_calendar_events": list_calendar_events,
    "create_calendar_event": create_calendar_event,
    "dispatch_to_agents": dispatch_to_agents
}


def process_message(user_message: str):
    """사용자 메시지 처리 (Gemini Function Calling)"""
    print(f"\n📩 [사장님 → 영숙] {user_message}")
    global CHAT_HISTORY

    # API 키 확인
    if not client:
        send_telegram_message("영숙이에요! Gemini API 키가 설정되지 않아서 대화할 수 없어요 😢")
        return

    # 현재 시간 컨텍스트 추가
    kst = timezone(timedelta(hours=9))
    now_kst = datetime.now(kst)
    time_context = f"\n\n[현재 시각: {now_kst.strftime('%Y-%m-%d %H:%M:%S %A')} (KST)]"

    system_instruction = YEONGSUK_PERSONA + time_context

    # 사용자 메시지를 히스토리에 추가
    CHAT_HISTORY.append(types.Content(
        role="user",
        parts=[types.Part.from_text(text=user_message)]
    ))

    # 최근 6개 메시지만 유지 (3턴)
    if len(CHAT_HISTORY) > 6:
        CHAT_HISTORY = CHAT_HISTORY[-6:]

    try:
        # Gemini API 호출 (Function Calling 활성화)
        response = client.models.generate_content(
            model="gemini-1.5-flash",
            contents=CHAT_HISTORY,
            config=types.GenerateContentConfig(
                system_instruction=system_instruction,
                tools=TOOLS,
                max_output_tokens=500,
                temperature=0.7
            )
        )

        final_answer = ""
        tool_results = []

        # Function Call 실행
        if response.candidates and response.candidates[0].content.parts:
            for part in response.candidates[0].content.parts:
                # 텍스트 응답
                if part.text:
                    final_answer += part.text

                # Function Call
                elif part.function_call:
                    fn_name = part.function_call.name
                    fn_args = dict(part.function_call.args) if part.function_call.args else {}

                    print(f"  🔧 [도구 호출] {fn_name}({fn_args})")

                    if fn_name in TOOL_MAP:
                        try:
                            tool_result = TOOL_MAP[fn_name](**fn_args)
                            tool_results.append({
                                "name": fn_name,
                                "result": tool_result
                            })
                            print(f"  ✅ [도구 결과] {tool_result[:100]}...")
                        except Exception as e:
                            error_msg = f"도구 실행 오류: {str(e)[:100]}"
                            tool_results.append({
                                "name": fn_name,
                                "result": f"❌ {error_msg}"
                            })
                            print(f"  ❌ [도구 오류] {error_msg}")

        # 도구 실행 결과가 있으면, 다시 Gemini에게 전달하여 자연스러운 답변 생성
        if tool_results:
            # 도구 실행 결과를 히스토리에 추가
            function_response_parts = []
            for tr in tool_results:
                function_response_parts.append(types.Part.from_function_response(
                    name=tr["name"],
                    response={"result": tr["result"]}
                ))

            CHAT_HISTORY.append(types.Content(
                role="model",
                parts=[types.Part.from_function_call(
                    name=tool_results[0]["name"],
                    args={}
                )]
            ))
            CHAT_HISTORY.append(types.Content(
                role="user",
                parts=function_response_parts
            ))

            # 도구 결과를 바탕으로 최종 답변 생성
            final_response = client.models.generate_content(
                model="gemini-1.5-flash",
                contents=CHAT_HISTORY,
                config=types.GenerateContentConfig(
                    system_instruction=system_instruction + "\n\n도구 실행 결과를 바탕으로 간결하게 답변하세요.",
                    max_output_tokens=300,
                    temperature=0.7
                )
            )

            if final_response.text:
                final_answer = final_response.text.strip()
            else:
                # 도구 결과를 직접 전달
                final_answer = "\n\n".join([tr["result"] for tr in tool_results])

        # 답변이 없으면 기본 응답
        if not final_answer:
            final_answer = "네, 알겠습니다!"

        # 텔레그램 전송
        send_telegram_message(final_answer)

        # 최종 답변을 히스토리에 추가
        CHAT_HISTORY.append(types.Content(
            role="model",
            parts=[types.Part.from_text(text=final_answer)]
        ))

        # 히스토리 정리
        if len(CHAT_HISTORY) > 6:
            CHAT_HISTORY = CHAT_HISTORY[-6:]

    except Exception as e:
        error_str = str(e)
        print(f"  ❌ [오류] {error_str}")
        traceback.print_exc()

        # Gemini 할당량 초과 시 Claude 폴백
        if "429" in error_str or "RESOURCE_EXHAUSTED" in error_str or "quota" in error_str.lower():
            print("  🔄 [Fallback] Claude API로 전환")
            try:
                from _shared.claude_client import chat as claude_chat

                history_text = "\n".join([
                    f"{'사용자' if c.role == 'user' else '영숙'}: {c.parts[0].text if c.parts and hasattr(c.parts[0], 'text') else ''}"
                    for c in CHAT_HISTORY[-4:]
                ])

                claude_response = claude_chat(
                    prompt=f"{history_text}\n사용자: {user_message}",
                    system=YEONGSUK_PERSONA + time_context,
                    max_tokens=300
                )

                if claude_response:
                    send_telegram_message(f"🤖 [Claude 모드]\n{claude_response}")
                    return
            except Exception as claude_err:
                print(f"  ❌ [Claude 실패] {claude_err}")

        send_telegram_message("죄송해요, 일시적인 오류가 발생했어요. 잠시 후 다시 시도해주세요.")


# ══════════════════════════════════════════════════════════════════════════════
# 메인 루프
# ══════════════════════════════════════════════════════════════════════════════

def main():
    """텔레그램 봇 메인 루프"""
    print("=" * 70)
    print("🤖 영숙 텔레그램 봇 시작")
    print("=" * 70)
    print(f"  Gemini API: {'✅' if client else '❌ API 키 없음'}")
    print(f"  Telegram: {'✅' if TOKEN and CHAT_ID else '❌ 설정 필요'}")
    print(f"  CEO Dispatcher: {'✅' if yewon_dispatcher else '❌'}")
    print("=" * 70)

    if not TOKEN or not CHAT_ID:
        print("❌ TELEGRAM_BOT_TOKEN 또는 TELEGRAM_CHAT_ID가 설정되지 않았습니다.")
        return

    # Webhook 삭제
    telegram_api("deleteWebhook", {"drop_pending_updates": True})

    # 시작 메시지
    send_telegram_message(
        "🤖 영숙이 출근했어요!\n\n"
        "명령 예시:\n"
        "• 현황 보고해줘\n"
        "• 루나 영상 만들어\n"
        "• 일정 알려줘"
    )

    offset = 0

    while True:
        try:
            updates = get_telegram_updates(offset)

            for update in updates:
                offset = update["update_id"] + 1
                message = update.get("message", {})
                text = message.get("text", "").strip()

                if text:
                    process_message(text)

            time.sleep(1)

        except KeyboardInterrupt:
            print("\n👋 종료합니다.")
            break
        except Exception as e:
            print(f"❌ 루프 오류: {e}")
            time.sleep(5)


if __name__ == "__main__":
    main()
