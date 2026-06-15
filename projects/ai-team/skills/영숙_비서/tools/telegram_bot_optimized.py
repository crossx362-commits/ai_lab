"""
영숙 텔레그램 봇 - Gemini Function Calling 토큰 최적화 버전

[최적화 전략]
1. 시스템 프롬프트 최소화 (핵심만)
2. 대화 기록 최근 3턴만 유지
3. max_output_tokens 제한
4. 스트리밍 미사용
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

# 클라이언트 초기화
API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
client = genai.Client(api_key=API_KEY) if API_KEY else None

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
try:
    import yewon_dispatcher
except ImportError:
    yewon_dispatcher = None

# ══════════════════════════════════════════════════════════════════════════════
# 시스템 프롬프트 - 최소화 (핵심만)
# ══════════════════════════════════════════════════════════════════════════════

SYSTEM = """영숙(사장님 비서). 규칙:
1. 짧게 답변(2-3줄)
2. 도구 사용 가능하면 무조건 사용
3. 미사여구 금지"""

HISTORY = []  # 최근 3턴만 유지


# ══════════════════════════════════════════════════════════════════════════════
# 텔레그램 API
# ══════════════════════════════════════════════════════════════════════════════

def tg_api(method: str, data: dict, timeout: int = 20) -> dict:
    import urllib.request
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    req = urllib.request.Request(
        url,
        data=json.dumps(data).encode(),
        headers={"Content-Type": "application/json"}
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"[TG Error] {method}: {e}")
        return {}


def send_msg(text: str):
    print(f"📤 {text[:60]}...")
    tg_api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})


def get_updates(offset: int) -> list:
    res = tg_api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]}, timeout=40)
    return res.get("result", [])


# ══════════════════════════════════════════════════════════════════════════════
# 도구 함수 (Gemini Function Calling)
# ══════════════════════════════════════════════════════════════════════════════

def get_agent_status(agent: str = "전체") -> str:
    """에이전트 현황 조회.
    Args:
        agent: '루나'(YouTube) / '아린'(Instagram) / '데이브'(주식) / '전체'
    """
    from _shared.agent_status import get_status_report
    return get_status_report(agent, PROJECT_ROOT)



def list_calendar(days: int = 7) -> str:
    """캘린더 일정 조회.
    Args:
        days: 조회할 미래 일수
    """
    cache = os.path.join(PROJECT_ROOT, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return f"📅 일정:\n{f.read()[:800]}"
    return "📅 캘린더 미설정"


def create_calendar(title: str, start: str, mins: int = 60, desc: str = "", loc: str = "") -> str:
    """캘린더 일정 등록.
    Args:
        title: 제목
        start: 시작 일시 (ISO: 2026-06-20T14:00:00)
        mins: 지속시간(분)
        desc: 설명
        loc: 장소
    """
    return "⚠️ 캘린더 등록 기능 준비 중"


def dispatch(cmd: str) -> str:
    """에이전트 작업 실행.
    Args:
        cmd: 명령 ("루나 영상", "인스타 포스팅", "비트코인 분석")
    """
    if not yewon_dispatcher:
        return "❌ 디스패처 없음"

    try:
        print(f"🎯 실행: {cmd}")
        result = yewon_dispatcher.dispatch_and_execute(cmd)
        if not result:
            return "⚠️ CEO 복구 대기"

        # 긴 결과는 요약
        if len(result) > 400:
            try:
                s = client.models.generate_content(
                    model="gemini-2.5-flash",
                    contents=f"2줄 요약:\n{result[:600]}",
                    config=types.GenerateContentConfig(max_output_tokens=100)
                )
                if s.text:
                    return f"✅ {s.text.strip()}"
            except Exception:
                pass
            return result[:400] + "..."

        return result
    except Exception as e:
        return f"❌ 오류: {str(e)[:100]}"


# 도구 매핑
TOOLS = [get_agent_status, list_calendar, create_calendar, dispatch]
TOOL_MAP = {
    "get_agent_status": get_agent_status,
    "list_calendar": list_calendar,
    "create_calendar": create_calendar,
    "dispatch": dispatch
}


# ══════════════════════════════════════════════════════════════════════════════
# 메시지 처리 (토큰 최적화)
# ══════════════════════════════════════════════════════════════════════════════

def process(msg: str):
    """메시지 처리 - 토큰 최적화"""
    print(f"\n📩 {msg}")
    global HISTORY

    if not client:
        send_msg("Gemini API 키 없음")
        return

    # 현재 시각
    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    # 사용자 메시지 추가
    HISTORY.append(types.Content(role="user", parts=[types.Part.from_text(text=msg)]))

    # 최근 3턴(6개 메시지)만 유지
    if len(HISTORY) > 6:
        HISTORY = HISTORY[-6:]

    try:
        # Gemini 호출 (토큰 최적화)
        resp = client.models.generate_content(
            model="gemini-2.5-flash",
            contents=HISTORY,
            config=types.GenerateContentConfig(
                system_instruction=SYSTEM + time_ctx,
                tools=TOOLS,
                max_output_tokens=200,  # 짧게 제한
                temperature=0.7
            )
        )

        answer = ""
        tool_results = []

        # 응답 파싱
        if resp.candidates and resp.candidates[0].content.parts:
            for part in resp.candidates[0].content.parts:
                # 텍스트
                if part.text:
                    answer += part.text

                # Function Call
                elif part.function_call:
                    fn = part.function_call.name
                    args = dict(part.function_call.args) if part.function_call.args else {}
                    print(f"🔧 {fn}({args})")

                    if fn in TOOL_MAP:
                        try:
                            res = TOOL_MAP[fn](**args)
                            tool_results.append({"name": fn, "result": res})
                            print(f"✅ {res[:80]}...")
                        except Exception as e:
                            err = f"❌ {str(e)[:80]}"
                            tool_results.append({"name": fn, "result": err})
                            print(err)

        # 도구 실행 결과가 있으면 재호출
        if tool_results:
            # 도구 응답 추가
            fn_parts = []
            for tr in tool_results:
                fn_parts.append(types.Part.from_function_response(
                    name=tr["name"],
                    response={"result": tr["result"]}
                ))

            HISTORY.append(types.Content(
                role="model",
                parts=[types.Part.from_function_call(name=tool_results[0]["name"], args={})]
            ))
            HISTORY.append(types.Content(role="user", parts=fn_parts))

            # 최종 답변 생성
            final = client.models.generate_content(
                model="gemini-2.5-flash",
                contents=HISTORY,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM + time_ctx + "\n도구 결과 바탕으로 간결히 답변",
                    max_output_tokens=150,
                    temperature=0.7
                )
            )

            answer = final.text.strip() if final.text else "\n\n".join([tr["result"] for tr in tool_results])

        if not answer:
            answer = "네"

        # 전송
        send_msg(answer)

        # 히스토리 추가
        HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=answer)]))

        # 다시 정리
        if len(HISTORY) > 6:
            HISTORY = HISTORY[-6:]

    except Exception as e:
        err = str(e)
        print(f"❌ {err}")
        traceback.print_exc()

        # Claude 폴백 (할당량 초과 시)
        if "429" in err or "RESOURCE_EXHAUSTED" in err or "quota" in err.lower():
            print("🔄 Claude 전환")
            try:
                from _shared.claude_client import chat as claude_chat

                hist_txt = "\n".join([
                    f"{'사용자' if c.role == 'user' else '영숙'}: {c.parts[0].text if c.parts and hasattr(c.parts[0], 'text') else ''}"
                    for c in HISTORY[-4:]
                ])

                claude_resp = claude_chat(
                    prompt=f"{hist_txt}\n사용자: {msg}",
                    system=SYSTEM + time_ctx,
                    max_tokens=200
                )

                if claude_resp:
                    send_msg(f"🤖 [Claude]\n{claude_resp}")
                    HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=claude_resp)]))
                    return
            except Exception as ce:
                print(f"❌ Claude 실패: {ce}")

        send_msg("일시 오류. 잠시 후 재시도하세요")


# ══════════════════════════════════════════════════════════════════════════════
# 메인
# ══════════════════════════════════════════════════════════════════════════════

def main():
    print("=" * 60)
    print("🤖 영숙 봇 (토큰 최적화)")
    print("=" * 60)
    print(f"Gemini: {'✅' if client else '❌'}")
    print(f"Telegram: {'✅' if TOKEN and CHAT_ID else '❌'}")
    print(f"CEO: {'✅' if yewon_dispatcher else '❌'}")
    print("=" * 60)

    if not TOKEN or not CHAT_ID:
        print("❌ 텔레그램 설정 필요")
        return

    tg_api("deleteWebhook", {"drop_pending_updates": True})
    send_msg("🤖 영숙 출근\n\n예시:\n• 현황\n• 루나 영상\n• 일정")

    offset = 0
    while True:
        try:
            for upd in get_updates(offset):
                offset = upd["update_id"] + 1
                text = upd.get("message", {}).get("text", "").strip()
                if text:
                    process(text)
            time.sleep(1)
        except KeyboardInterrupt:
            print("\n👋 종료")
            break
        except Exception as e:
            print(f"❌ 루프: {e}")
            time.sleep(5)


if __name__ == "__main__":
    main()
