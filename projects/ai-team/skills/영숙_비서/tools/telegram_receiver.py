"""
영숙 텔레그램 봇 - 최종 최적화 및 에러 복원 버전
"""
import os, sys, json, time, traceback
from datetime import datetime, timezone, timedelta

_early_here = os.path.dirname(os.path.abspath(__file__))
try:
    _log_path = os.path.join(_early_here, "telegram_receiver.log")
    if sys.stdout is None or sys.stderr is None or "pythonw" in os.path.basename(sys.executable).lower():
        _log = open(_log_path, "a", encoding="utf-8", buffering=1)
        sys.stdout = _log
        sys.stderr = _log
except Exception:
    pass

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except: pass

_here = _early_here
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

try:
    from google import genai
    from google.genai import types
except ModuleNotFoundError:
    genai = None
    types = None

API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
client = genai.Client(api_key=API_KEY) if genai and API_KEY else None

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
try:
    import yewon_dispatcher
except:
    yewon_dispatcher = None

SYSTEM = "영숙(비서). 규칙: 짧게 핵심만 2줄 이내 답변. 단, 현황 보고(get_agent_status) 도구를 호출하는 경우에는 예외적으로 모든 에이전트의 내용을 줄이거나 생략하지 말고 상세하게 다 답변해야 함. 일반적인 질문이나 상태 분석 요청은 도구를 쓰지 말고 제미니가 직접 아는 선에서 즉시 답변하세요. 파이썬 스크립트 실행이나 특정 에이전트 구동 명령인 경우에만 dispatch 도구를 호출하세요."

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
    """에이전트 현황. Args: agent ('예원'/'영숙'/'루나'/'아린'/'코다리'/'케빈'/'티모'/'현빈'/'경수'/'로율'/'데이브'/'전체')"""
    from _shared.agent_status import get_status_report
    return get_status_report(agent, PROJECT_ROOT)

def list_calendar(days: int = 7):
    """캘린더 일정. Args: days (조회 일수)"""
    cache = os.path.join(PROJECT_ROOT, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return f"📅 일정:\n{f.read()[:800]}"
    return "📅 캘린더 미설정"

def generate_content_with_retry(model_name, contents, config, max_retries=5):
    """구글 제미니 API 호출을 수행하며, 429 등의 할당량 초과 시 자동 재시도 및 사용자 알림 제공"""
    if not client:
        return None

    for attempt in range(max_retries):
        try:
            return client.models.generate_content(
                model=model_name,
                contents=contents,
                config=config
            )
        except Exception as e:
            err = str(e)
            if "429" in err or "RESOURCE_EXHAUSTED" in err or "quota" in err.lower() or "limit" in err.lower():
                if attempt < max_retries - 1:
                    send_msg("⚠️ 구글 서버가 바쁩니다. 5초 뒤 자동 재시도합니다.")
                    time.sleep(5)
                    continue
            raise e
    return None

def dispatch(cmd: str):
    """에이전트 스크립트를 실제로 구동/실행(run/execute)하는 명시적 명령에만 호출하세요. 단순 분석/설명 질문에는 절대 호출 금지. Args: cmd (실행 명령)"""
    if not yewon_dispatcher: return "❌ 디스패처 없음"
    import threading
    def _run_bg():
        try:
            print(f"🎯 [비동기 시작] {cmd}")
            send_msg(f"⚙️ 에이전트 작업을 시작합니다: '{cmd}'")
            result = yewon_dispatcher.dispatch_and_execute(cmd)
            if not result:
                send_msg("⚠️ CEO 분석 대기 중")
                return
            if len(result) > 500:
                try:
                    s = generate_content_with_retry(
                        model_name="gemini-2.5-flash",
                        contents=[types.Content(role="user", parts=[types.Part.from_text(text=f"2줄 요약:\n{result[:600]}")]),],
                        config=types.GenerateContentConfig(max_output_tokens=100)
                    )
                    if s and s.text:
                        send_msg(f"✅ 작업 완료 요약:\n{s.text.strip()}")
                        return
                except:
                    pass
            send_msg(f"✅ 작업 결과:\n{result}")
        except Exception as e:
            send_msg(f"❌ 작업 수행 실패: {str(e)[:200]}")
            
    threading.Thread(target=_run_bg, daemon=True).start()
    return "🚀 에이전트 구동 지시를 비동기로 시작했습니다. 완료되면 알려드리겠습니다."

TOOLS = [get_agent_status, list_calendar, dispatch]
TOOL_MAP = {"get_agent_status": get_agent_status, "list_calendar": list_calendar, "dispatch": dispatch}

def process(msg):
    print(f"\n📩 {msg}")

    # 1. 자주 사용하는 시스템 명령은 즉시 반환하여 딜레이/토큰 제로화
    msg_clean = msg.strip().replace(" ", "").lower()
    if any(k in msg_clean for k in ["현황", "상태", "다들뭐해"]):
        try:
            status_report = get_agent_status("전체")
            send_msg(status_report)
            return
        except Exception as se:
            print(f"❌ 현황 조회 실패: {se}")

    if any(k in msg_clean for k in ["일정", "캘린더"]):
        try:
            cal_report = list_calendar()
            send_msg(cal_report)
            return
        except Exception as ce:
            print(f"❌ 일정 조회 실패: {ce}")

    direct_dispatch_keywords = ["구동", "실행", "시작", "켜", "동작", "가동", "데몬", "자동매매", "실거래", "live"]
    if not client:
        if any(k in msg for k in direct_dispatch_keywords):
            send_msg(dispatch(msg))
            return
        try:
            from _shared.ollama_client import chat as lm_chat
            answer = lm_chat(msg, system=SYSTEM, max_tokens=150, temperature=0.7)
            send_msg(answer.strip() if answer else "Gemini 패키지가 없어 로컬 응답도 실패했습니다.")
            return
        except Exception as oe:
            print(f"❌ 로컬 Ollama 폴백 실패: {oe}")
            send_msg("Gemini 패키지가 없어 일반 답변은 제한됩니다. 현황/일정/에이전트 구동 명령은 가능합니다.")
            return

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    # 대화 누적 방지: 이전 대화 컨텍스트(HISTORY) 없이 지금 보낸 단 한 개의 메시지만 독립적으로 전송
    contents = [types.Content(role="user", parts=[types.Part.from_text(text=msg)])]

    model_name = "gemini-2.5-flash"
    try:
        try:
            resp = generate_content_with_retry(
                model_name=model_name,
                contents=contents,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM + time_ctx,
                    tools=TOOLS,
                    max_output_tokens=120,
                    temperature=0.7
                )
            )
        except Exception as e:
            # flash 할당량 초과 시 pro로 전환 시도
            err = str(e)
            if "429" in err or "RESOURCE_EXHAUSTED" in err or "quota" in err.lower():
                print("🔄 Gemini Flash 할당량 초과. Gemini Pro로 전환 시도...")
                model_name = "gemini-2.5-pro"
                resp = generate_content_with_retry(
                    model_name=model_name,
                    contents=contents,
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

        if resp and resp.candidates and resp.candidates[0].content.parts:
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
            is_direct = any(tr["name"] in ["get_agent_status", "get_status_report", "list_calendar"] for tr in tool_results)
            if is_direct:
                answer = "\n\n".join([tr["result"] for tr in tool_results])
            else:
                fn_parts = [types.Part.from_function_response(name=tr["name"], response={"result": tr["result"]}) for tr in tool_results]
                contents.append(types.Content(role="model", parts=[types.Part.from_function_call(name=tool_results[0]["name"], args={})]))
                contents.append(types.Content(role="user", parts=fn_parts))

                try:
                    final = generate_content_with_retry(
                        model_name=model_name,
                        contents=contents,
                        config=types.GenerateContentConfig(
                            system_instruction=SYSTEM + time_ctx + "\n도구 결과로 간결히 답변",
                            max_output_tokens=150,
                            temperature=0.7
                        )
                    )
                except Exception as fe:
                    ferr = str(fe)
                    if ("429" in ferr or "RESOURCE_EXHAUSTED" in ferr or "quota" in ferr.lower()) and model_name == "gemini-2.5-flash":
                        print("🔄 Gemini Flash 할당량 초과 (최종 답변). Gemini Pro로 전환 시도...")
                        model_name = "gemini-2.5-pro"
                        final = generate_content_with_retry(
                            model_name=model_name,
                            contents=contents,
                            config=types.GenerateContentConfig(
                                system_instruction=SYSTEM + time_ctx + "\n도구 결과로 간결히 답변",
                                max_output_tokens=150,
                                temperature=0.7
                            )
                        )
                    else:
                        raise fe
                answer = final.text.strip() if final and final.text else "\n\n".join([tr["result"] for tr in tool_results])

        if not answer:
            answer = "네"

        send_msg(answer)

    except Exception as e:
        print(f"❌ {model_name} 최종 오류: {e}")
        try:
            print("🔄 Gemini 오류 감지 → 로컬 Ollama 최종 폴백 진행...")
            from _shared.ollama_client import chat as lm_chat
            ollama_ans = lm_chat(msg, system=SYSTEM, max_tokens=150, temperature=0.7)
            if ollama_ans:
                answer = ollama_ans.strip()
                send_msg(answer)
                return
        except Exception as oe:
            print(f"❌ 로컬 Ollama 폴백 실패: {oe}")
        send_msg("일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.")

def main():
    # 중복 실행 방지
    try:
        import psutil
        current_pid = os.getpid()
        for p in psutil.process_iter(['pid', 'name', 'cmdline']):
            try:
                if p.info['pid'] == current_pid:
                    continue
                cmd = p.info['cmdline']
                if cmd and any('telegram_receiver.py' in str(arg) for arg in cmd):
                    print(f"⚠️ 이미 다른 telegram_receiver.py 프로세스가 실행 중입니다 (PID: {p.info['pid']}). 실행을 종료합니다.")
                    sys.exit(0)
            except (psutil.AccessDenied, psutil.NoSuchProcess):
                continue
    except Exception as pe:
        print(f"중복 실행 확인 중 오류 발생: {pe}")

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
                    time.sleep(2)  # 구글 API 요청 속도 조절을 위한 2초 딜레이 추가
            time.sleep(1)
        except KeyboardInterrupt:
            print("\n👋 종료")
            break
        except Exception as e:
            print(f"❌ 루프: {e}")
            time.sleep(5)

if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        raise
