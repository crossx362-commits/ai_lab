import os
import sys
import json
import time
import urllib.request
import urllib.error
import datetime
import traceback

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "ai-team"))

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

load_env(PROJECT_ROOT)
TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# Import CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, "ai-team", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

YEONGSUK_PERSONA = """
당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 개인 비서입니다.
사장님의 텔레그램 메시지를 가장 먼저 받고 응답합니다.

# 업무 판단 기준
다음 중 하나라도 해당되면 **반드시 dispatch 모드**를 사용하세요:
- "~해줘", "~만들어줘", "~해", "~분석해줘", "~작성해줘" 등 작업 요청 동사
- YouTube/Instagram 콘텐츠 제작·업로드 요청
- 리서치·분석·검수·평가 요청
- 일정·스케줄·자동화 설정 요청
- 에이전트 이름 언급 (루나, 아린, 예원, 가희, 코다리 등)
- 파일 작성·수정·삭제 요청

# 일반 대화 기준
다음은 reply 모드로 직접 답변하세요:
- 인사·안부 ("안녕", "잘 지내?", "뭐해?")
- 단순 질문 ("현재 시간", "날씨", "상태 확인")
- 감사·칭찬 ("고마워", "잘했어", "수고했어")

# 응답 형식 (JSON만 반환)

옵션 A) 일반 대화:
{"mode": "reply", "text": "사장님께 드릴 다정하고 간결한 답변"}

옵션 B) 업무 지시:
{"mode": "dispatch", "text": "네, 예원 CEO님께 전달해서 바로 처리할게요! 🚀", "dispatch_to_ceo": "예원 대표님, 사장님께서 [구체적 요청 내용]을 요청하셨습니다. 적절한 에이전트에게 배분해주세요."}

# 예시
User: "루나 영상 제작해줘"
→ {"mode": "dispatch", "text": "네, 루나에게 영상 제작 지시할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 루나 영상 제작을 요청하셨습니다."}

User: "오늘 뭐했어?"
→ {"mode": "reply", "text": "오늘도 열심히 일했어요! 텔레그램 메시지 확인하고, 일정 체크하고 있었답니다 😊"}

**중요**: 작업 요청은 무조건 dispatch! 망설이지 말고 CEO에게 전달하세요.
"""

CHAT_HISTORY = []

def _api(method: str, payload: dict) -> dict:
    url  = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req  = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"  [API Error] {method}: {e}")
        return {}

def send_message(text: str):
    print(f"  [영숙 발신] {text[:50]}...")
    _api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset: int) -> list:
    res = _api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]})
    return res.get("result", [])

def format_ceo_report(ceo_result: str) -> str:
    prompt = f"당신은 영숙 비서입니다. 아래 CEO의 업무 처리 결과를 사장님께 보고할 다정한 텍스트로 요약해주세요.\n\n결과:\n{ceo_result}"
    if lm_available():
        res = lm_chat(prompt, max_tokens=500)
        if res:
            return res.strip()
    return ceo_result

def _web_search_analyze(query: str) -> str:
    """메시지 이해 실패 시 웹 서치로 맥락 분석."""
    try:
        # Gemini API로 웹 검색 (간단한 구글 검색 시뮬레이션)
        from _shared import gemini_client as _gc

        search_prompt = f"""
다음 사용자 메시지를 분석해서 의도를 파악하고, 어떤 작업을 요청하는지 명확히 설명해줘:

사용자 메시지: "{query}"

분석 결과를 다음 형식으로 반환:
1. 핵심 의도: (한 줄)
2. 요청 작업: (구체적으로)
3. 관련 에이전트: (루나/아린/예원/코다리 등)
"""

        result = _gc.text(search_prompt, lm_first=True, max_tokens=300)
        return result if result else "분석 실패"
    except Exception as e:
        print(f"  [웹 서치 실패] {e}")
        return "분석 실패"


def process_message(text: str):
    print(f"\n📩 [영숙 수신] {text}")

    if not lm_available():
        send_message("영숙이에요! 지금 언어 모델 서버가 꺼져 있어서 처리가 안 돼요 😭")
        return

    history_text = ""
    for h in CHAT_HISTORY[-6:]:
        history_text += f"{h['role']}: {h['text']}\n"
    history_text += f"User: {text}\n"

    try:
        raw_resp = lm_chat(history_text, system=YEONGSUK_PERSONA, json_mode=True, max_tokens=500)

        # JSON 파싱 실패 시 웹 서치 분석
        if not raw_resp or not raw_resp.strip().startswith("{"):
            print(f"  [이해 실패] JSON 아님, 웹 서치 분석 시작...")
            send_message("잠깐만요, 정확히 이해하기 위해 분석 중이에요... 🔍")

            # 웹 서치로 맥락 분석
            analysis = _web_search_analyze(text)
            print(f"  [분석 결과]\n{analysis}")

            # 분석 결과를 바탕으로 재시도
            enhanced_prompt = f"{history_text}\n\n[분석 결과]\n{analysis}\n\n위 분석을 참고해서 응답해줘."
            raw_resp = lm_chat(enhanced_prompt, system=YEONGSUK_PERSONA, json_mode=True, max_tokens=500)

        if not raw_resp:
            send_message("영숙이에요! 여러 번 시도했지만 잘 이해가 안 돼요 😅\n좀 더 구체적으로 말씀해주실 수 있을까요?")
            return

        decision = json.loads(raw_resp.strip())
        mode = decision.get("mode", "reply")
        reply_text = decision.get("text", "네 알겠습니다!")

        # 1. 텔레그램 1차 응답 (영숙 -> 사용자)
        send_message(reply_text)
        
        CHAT_HISTORY.append({"role": "User", "text": text})
        CHAT_HISTORY.append({"role": "Assistant", "text": reply_text})
        
        # 2. 업무 분배 요청 시 (영숙 -> CEO 예원 -> 서브 에이전트)
        if mode == "dispatch" and "dispatch_to_ceo" in decision:
            ceo_msg = decision["dispatch_to_ceo"]
            # Synchronously (or could be threaded) call CEO
            ceo_result = yewon_dispatcher.dispatch_and_execute(ceo_msg)
            
            # 3. 결과 수신 후 최종 포매팅 및 발신 (CEO 예원 -> 영숙 -> 사용자)
            final_report = format_ceo_report(ceo_result)
            send_message(f"🔔 <b>[영숙이의 업무 보고]</b>\n\n{final_report}")
            
    except Exception as e:
        print(f"  [오류] 영숙 메시지 처리 실패: {e}")
        traceback.print_exc()

def main_loop():
    print("🚀 영숙 전용 텔레그램 리시버가 시작되었습니다!")
    # Clear webhook
    _api("deleteWebhook", {"drop_pending_updates": True})
    
    offset = 0
    while True:
        try:
            updates = get_updates(offset)
            for u in updates:
                offset = u["update_id"] + 1
                msg = u.get("message", {})
                text = msg.get("text", "").strip()
                if text:
                    process_message(text)
        except KeyboardInterrupt:
            print("종료합니다.")
            break
        except Exception as e:
            print(f"루프 오류: {e}")
            time.sleep(5)

if __name__ == "__main__":
    main_loop()
