"""
영숙 텔레그램 봇 - 최종 최적화 버전

[핵심 기능]
1. Gemini Flash → Pro → Claude 3단계 폴백
2. 토큰 최적화 (시스템 프롬프트 최소화, 히스토리 3턴)
3. Function Calling으로 대충 명령해도 알아듣기
"""
import os, sys, json, time, traceback
from datetime import datetime, timezone, timedelta

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except: pass

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

from google import genai
from google.genai import types

API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
client = genai.Client(api_key=API_KEY) if API_KEY else None

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
try:
    import yewon_dispatcher
except:
    yewon_dispatcher = None

SYSTEM = "영숙(비서). 규칙: 짧게 핵심만 2줄 이내 답변. 단, 현황 보고(get_agent_status) 도구를 호출하는 경우에는 예외적으로 모든 에이전트의 내용을 줄이거나 생략하지 말고 상세하게 다 답변해야 함. 필요시 도구(get_agent_status, list_calendar, dispatch) 즉시 호출."
HISTORY = []

def tg_api(method, data, timeout=20):
    import urllib.request
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    req = urllib.request.Request(url, json.dumps(data).encode(), headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except: return {}

def send_msg(text):
    print(f"📤 {text[:60]}...")
    tg_api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset):
    res = tg_api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]}, timeout=40)
    return res.get("result", [])

def get_agent_status(agent: str = "전체"):
    """에이전트 현황. Args: agent ('예원'/'영숙'/'루나'/'아린'/'가희'/'코다리'/'케빈'/'티모'/'현빈'/'경수'/'로율'/'데이브'/'전체')"""
    from _shared.agent_status import get_status_report
    return get_status_report(agent, PROJECT_ROOT)


def list_calendar(days: int = 7):
    """캘린더 일정. Args: days (조회 일수)"""
    cache = os.path.join(PROJECT_ROOT, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return f"📅 일정:\n{f.read()[:800]}"
    return "📅 캘린더 미설정"

def dispatch(cmd: str):
    """에이전트 실행. Args: cmd (명령)"""
    if not yewon_dispatcher: return "❌ 디스패처 없음"
    try:
        print(f"🎯 {cmd}")
        result = yewon_dispatcher.dispatch_and_execute(cmd)
        if not result: return "⚠️ CEO 대기"
        if len(result) > 400:
            try:
                s = client.models.generate_content(model="gemini-2.5-flash", contents=f"2줄 요약:\n{result[:600]}", config=types.GenerateContentConfig(max_output_tokens=100))
                if s.text: return f"✅ {s.text.strip()}"
            except: pass
            return result[:400] + "..."
        return result
    except Exception as e:
        return f"❌ {str(e)[:100]}"

TOOLS = [get_agent_status, list_calendar, dispatch]
TOOL_MAP = {"get_agent_status": get_agent_status, "list_calendar": list_calendar, "dispatch": dispatch}

def process(msg):
    global HISTORY
    print(f"\n📩 {msg}")

    if "현황" in msg or "상태" in msg:
        HISTORY = []

    if not client:
        send_msg("Gemini API 키 없음")
        return

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    # 대화 기록 추가
    HISTORY.append(types.Content(role="user", parts=[types.Part.from_text(text=msg)]))
    
    # 최근 3턴(6개 메시지: 3 user, 3 model)만 유지하여 토큰 사용량 최적화
    if len(HISTORY) > 6:
        HISTORY = HISTORY[-6:]

    model_name = "gemini-2.5-flash"
    try:
        try:
            resp = client.models.generate_content(
                model=model_name,
                contents=HISTORY,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM + time_ctx,
                    tools=TOOLS,
                    max_output_tokens=120,  # 답변 길이 제한으로 토큰 사용량 최소화
                    temperature=0.7
                )
            )
        except Exception as e:
            err = str(e)
            if "429" in err or "RESOURCE_EXHAUSTED" in err or "quota" in err.lower():
                print("🔄 Gemini Flash 할당량 초과. Gemini Pro로 전환 시도...")
                model_name = "gemini-2.5-pro"
                resp = client.models.generate_content(
                    model=model_name,
                    contents=HISTORY,
                    config=types.GenerateContentConfig(
                        system_instruction=SYSTEM + time_ctx,
                        tools=TOOLS,
                        max_output_tokens=120,
                        temperature=0.7
                    )
                )
            else:
                raise e

        answer = ""
        tool_results = []

        if resp.candidates and resp.candidates[0].content.parts:
            for part in resp.candidates[0].content.parts:
                if part.text:
                    answer += part.text
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

        if tool_results:
            fn_parts = [types.Part.from_function_response(name=tr["name"], response={"result": tr["result"]}) for tr in tool_results]
            HISTORY.append(types.Content(role="model", parts=[types.Part.from_function_call(name=tool_results[0]["name"], args={})]))
            HISTORY.append(types.Content(role="user", parts=fn_parts))

            # 현황 보고의 경우 줄이지 말고 모든 에이전트의 내용을 상세히 전달하도록 설정
            is_status = any(tr["name"] in ["get_agent_status", "get_status_report"] for tr in tool_results)
            max_tok = 1000 if is_status else 100
            sys_inst = SYSTEM + time_ctx + ("\n현황 보고는 모든 에이전트의 내용을 줄이지 말고 상세하게 보고하세요." if is_status else "\n도구 결과로 간결히 답변")

            try:
                final = client.models.generate_content(
                    model=model_name,
                    contents=HISTORY,
                    config=types.GenerateContentConfig(
                        system_instruction=sys_inst,
                        max_output_tokens=max_tok,
                        temperature=0.7
                    )
                )
            except Exception as fe:
                ferr = str(fe)
                if ("429" in ferr or "RESOURCE_EXHAUSTED" in ferr or "quota" in ferr.lower()) and model_name == "gemini-2.5-flash":
                    print("🔄 Gemini Flash 할당량 초과 (최종 답변). Gemini Pro로 전환 시도...")
                    model_name = "gemini-2.5-pro"
                    final = client.models.generate_content(
                        model=model_name,
                        contents=HISTORY,
                        config=types.GenerateContentConfig(
                            system_instruction=sys_inst,
                            max_output_tokens=max_tok,
                            temperature=0.7
                        )
                    )
                else:
                    raise fe
            answer = final.text.strip() if final.text else "\n\n".join([tr["result"] for tr in tool_results])

        if not answer:
            answer = "네"

        send_msg(answer)

        # 모델 응답 기록 추가 및 3턴(6개 메시지) 유지
        HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=answer)]))
        if len(HISTORY) > 6:
            HISTORY = HISTORY[-6:]

    except Exception as e:
        print(f"❌ {model_name} 최종 오류: {e}")
        send_msg("일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.")

def main():
    print("="*60)
    print("🤖 영숙 봇 (Gemini 2.5 Flash 전용 최적화 버전)")
    print("="*60)
    print(f"Gemini: {'✅' if client else '❌'}")
    print(f"Telegram: {'✅' if TOKEN and CHAT_ID else '❌'}")
    print("="*60)

    if not TOKEN or not CHAT_ID:
        print("❌ 텔레그램 설정 필요")
        return

    tg_api("deleteWebhook", {"drop_pending_updates": True})
    send_msg("🤖 영숙 출근 (최적화 모드)\n예: 현황/루나 영상/일정")

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
