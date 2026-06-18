"""
영숙 텔레그램 봇 - 최종 최적화 및 에러 복원 버전
"""
import os, sys, json, time, traceback, subprocess
from datetime import datetime, timezone, timedelta

CREATE_NO_WINDOW = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0

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
    except Exception as e:
        print(f"❌ Telegram API 실패 ({method}): {e}")
        return {}

def send_msg(text):
    text = str(text or "")
    print(f"📤 {text[:60]}...")
    chunks = [text[i:i + 3500] for i in range(0, len(text), 3500)] or [""]
    for chunk in chunks:
        res = tg_api("sendMessage", {"chat_id": CHAT_ID, "text": chunk, "parse_mode": "HTML"})
        if res.get("ok"):
            continue
        print(f"⚠️ HTML 전송 실패 → 일반 텍스트 재전송: {res.get('description', 'unknown')}")
        tg_api("sendMessage", {"chat_id": CHAT_ID, "text": chunk})

def get_updates(offset):
    payload = {"timeout": 30, "allowed_updates": ["message"]}
    if offset:
        payload["offset"] = offset

    try:
        res = tg_api("getUpdates", payload, timeout=40)
        return res.get("result", [])
    except Exception as e:
        error_str = str(e)
        if "409" in error_str or "Conflict" in error_str:
            print(f"⚠️ Telegram API 409 Conflict - 다른 클라이언트가 봇을 사용 중입니다")
            print(f"   원인: Telegram Desktop, 웹, 또는 다른 봇 인스턴스")
            print(f"   해결: 다른 클라이언트를 종료하거나 Webhook 모드로 전환하세요")
            print(f"   대기 10초 후 재시도...")
            time.sleep(10)
            # 한 번만 재시도
            try:
                res = tg_api("getUpdates", payload, timeout=40)
                return res.get("result", [])
            except:
                pass
        return []

def get_agent_status(agent: str = "전체"):
    """에이전트 현황. Args: agent ('예원'/'영숙'/'코다리'/'케빈'/'티모'/'현빈'/'경수'/'로율'/'데이브'/'전체')"""
    from _shared.agent_status import get_status_report
    return get_status_report(agent, PROJECT_ROOT)

def list_calendar(days: int = 7):
    """캘린더 일정. Args: days (조회 일수)"""
    cache = os.path.join(PROJECT_ROOT, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return f"📅 일정:\n{f.read()[:800]}"
    return "📅 캘린더 미설정"

def is_google_busy_error(err: Exception | str) -> bool:
    text = str(err).lower()
    markers = [
        "429",
        "503",
        "resource_exhausted",
        "quota",
        "rate limit",
        "rate_limit",
        "temporarily unavailable",
        "service unavailable",
        "overloaded",
        "deadline",
    ]
    return any(marker in text for marker in markers)

def generate_content_with_retry(model_name, contents, config, max_retries=2):
    """Gemini 호출. 바쁨/쿼터 오류는 사용자에게 떠넘기지 않고 상위 fallback으로 넘긴다."""
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
            if is_google_busy_error(e):
                if attempt < max_retries - 1:
                    time.sleep(2 + attempt * 2)
                    continue
            raise e
    return None

def local_fallback_answer(msg: str) -> str | None:
    """Ollama 로컬 LLM 폴백."""
    try:
        from _shared.ollama_client import chat as lm_chat
        answer = lm_chat(msg, system=SYSTEM, max_tokens=300, temperature=0.7)
        return answer.strip() if answer else None
    except Exception as oe:
        print(f"❌ 로컬 Ollama 폴백 실패: {oe}")
        return None


def _gpt_fallback(msg: str) -> str | None:
    """Gemini 실패 시 GPT-4o mini 폴백."""
    import urllib.request, json as _json
    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return None
    try:
        payload = {
            "model": "gpt-4o-mini",
            "messages": [
                {"role": "system", "content": SYSTEM},
                {"role": "user", "content": msg},
            ],
            "max_tokens": 400,
            "temperature": 0.7,
        }
        req = urllib.request.Request(
            "https://api.openai.com/v1/chat/completions",
            data=_json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"},
        )
        with urllib.request.urlopen(req, timeout=30) as r:
            res = _json.loads(r.read())
        text = res["choices"][0]["message"]["content"].strip()
        print(f"  [GPT-4o mini 폴백] 답변 완료")
        return text
    except Exception as e:
        print(f"❌ GPT 폴백 실패: {e}")
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
                local_summary = local_fallback_answer(f"다음 작업 결과를 2줄로 요약:\n{result[:800]}")
                if local_summary:
                    send_msg(f"✅ 작업 완료 요약:\n{local_summary}")
                    return
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

    # 1. 자주 사용하는 시스템 명령은 즉시 반환하여 딜레이/토큰 제로화 (Gemini API 절약)
    msg_clean = msg.strip().replace(" ", "").lower()

    # 1-1. 종료/제어 명령은 "상태" 같은 일반 현황보다 먼저 처리한다.
    if any(k in msg_clean for k in [
        "봇전체종료", "봇모두종료", "killallbots", "stopallbots",
        "에이전트전체종료", "전체에이전트종료", "killallagents", "stopallagents",
    ]):
        try:
            import remote_bot_controller
            result = remote_bot_controller.emergency_stop_all_no_restart(skip_current=True)
            send_msg("🛑 에이전트 전체 종료 명령 처리 완료 (영숙 유지, 재시작 없음)\n" + result)
        except Exception as e:
            send_msg(f"❌ 전체 종료 실패: {e}")
        print("🛑 사용자 명령: 에이전트 전체 종료 (영숙 유지, 재시작 금지)")
        return

    bot_control_keywords = ["봇상태", "봇종료", "봇시작", "봇재시작",
                            "봇전체시작", "봇모두시작",
                            "맥북봇종료", "맥북봇시작",
                            "botstatus", "botstop", "botstart", "botrestart",
                            "startallbots", "macbookstop", "macbookstart"]
    if any(k in msg_clean for k in bot_control_keywords):
        try:
            import remote_bot_controller
            result = remote_bot_controller.handle_bot_command(msg)
            send_msg(result)

            if any(k in msg_clean for k in ["봇종료", "botstop", "봇재시작", "botrestart"]):
                import time
                time.sleep(2)
                print("🛑 사용자 명령으로 봇 종료")
                import sys
                sys.exit(0)
            return
        except Exception as e:
            send_msg(f"❌ 봇 제어 실패: {e}")
            return

    # 1-2. 개별 에이전트 제어 명령
    agent_control_keywords = ["데이브", "레오", "현빈", "영숙", "dave", "leo", "hyunbin", "youngsuk",
                              "에이전트상태", "agentstatus"]
    action_keywords = ["시작", "종료", "재시작", "상태", "start", "stop", "restart", "status", "켜", "꺼"]

    # "데이브 시작", "레오 종료" 같은 패턴 체크
    has_agent = any(k in msg_clean for k in agent_control_keywords)
    has_action = any(k in msg_clean for k in action_keywords)

    if (has_agent and has_action) or "에이전트상태" in msg_clean:
        try:
            import agent_controller
            result = agent_controller.handle_agent_command(msg)
            send_msg(result)
            return
        except Exception as e:
            send_msg(f"❌ 에이전트 제어 실패: {e}")
            return

    # 1-3. 에이전트 현황 (가장 많이 사용)
    if any(k in msg_clean for k in ["현황", "상태", "다들뭐해", "뭐하니", "진행"]):
        try:
            status_report = get_agent_status("전체")
            send_msg(status_report)
            return
        except Exception as se:
            print(f"❌ 현황 조회 실패: {se}")

    # 1-4. 일정 조회
    if any(k in msg_clean for k in ["일정", "캘린더", "calendar", "schedule"]):
        try:
            cal_report = list_calendar()
            send_msg(cal_report)
            return
        except Exception as ce:
            print(f"❌ 일정 조회 실패: {ce}")

    # 1-5. 명시적 에이전트 구동/실행 명령만 dispatch로 전달
    # ※ 에이전트 이름(데이브/레오/현빈)만으로는 dispatch 하지 않음 → 현황 질문과 구분
    direct_dispatch_keywords = ["구동", "실행해", "시작해", "켜줘", "가동", "데몬", "자동매매", "실거래시작", "거래시작"]
    if any(k in msg_clean for k in direct_dispatch_keywords):
        try:
            result = dispatch(msg)
            send_msg(result)
            return
        except Exception as de:
            print(f"❌ dispatch 실패: {de}")

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    # Gemini 클라이언트 없으면 GPT → Ollama 폴백
    if not client:
        answer = _gpt_fallback(msg)
        if not answer:
            answer = local_fallback_answer(msg)
        send_msg(answer if answer else "Gemini 패키지가 없어 일반 답변은 제한됩니다. 현황/일정/구동 명령은 가능합니다.")
        return

    # 대화 누적 방지: 지금 보낸 메시지만 독립적으로 전송
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
                    max_output_tokens=800,
                    temperature=0.7
                )
            )
        except Exception as e:
            # Gemini 혼잡/쿼터 오류 → GPT → Ollama 순서로 폴백
            if is_google_busy_error(e):
                print("🔄 Gemini 일시 제한/혼잡 감지 → GPT 폴백 시도")
                fallback = _gpt_fallback(msg) or local_fallback_answer(msg)
                send_msg(fallback if fallback else "일반 답변 모델이 잠시 불안정합니다. 현황/일정/구동 명령은 처리 가능합니다.")
                return
            raise e

        answer = ""
        tool_results = []
        tool_calls_made = []  # function_call args 보존용

        if resp and resp.candidates and resp.candidates[0].content.parts:
            for part in resp.candidates[0].content.parts:
                if part.text:
                    answer += part.text
                elif part.function_call:
                    fn = part.function_call.name
                    args = dict(part.function_call.args) if part.function_call.args else {}
                    print(f"🔧 {fn}({args})")
                    tool_calls_made.append({"name": fn, "args": args})
                    if fn in TOOL_MAP:
                        try:
                            res = TOOL_MAP[fn](**args)
                            tool_results.append({"name": fn, "result": str(res)})
                            print(f"✅ {str(res)[:80]}...")
                        except Exception as e:
                            err = f"❌ {str(e)[:100]}"
                            tool_results.append({"name": fn, "result": err})
                            print(err)

        if tool_results:
            # get_agent_status / list_calendar는 결과를 그대로 전송
            is_direct = any(tr["name"] in ["get_agent_status", "list_calendar"] for tr in tool_results)
            if is_direct:
                answer = "\n\n".join([tr["result"] for tr in tool_results])
            else:
                # dispatch 등 → Gemini에게 결과를 주고 최종 답변 생성
                model_parts = []
                for tc in tool_calls_made:
                    model_parts.append(types.Part.from_function_call(name=tc["name"], args=tc["args"]))
                contents.append(types.Content(role="model", parts=model_parts))

                fn_parts = [
                    types.Part.from_function_response(name=tr["name"], response={"result": tr["result"]})
                    for tr in tool_results
                ]
                contents.append(types.Content(role="user", parts=fn_parts))

                try:
                    final = generate_content_with_retry(
                        model_name=model_name,
                        contents=contents,
                        config=types.GenerateContentConfig(
                            system_instruction=SYSTEM + time_ctx,
                            max_output_tokens=500,
                            temperature=0.7
                        )
                    )
                except Exception as fe:
                    if is_google_busy_error(fe):
                        print("🔄 Gemini 최종 답변 제한/혼잡 → 도구 결과 직접 반환")
                        answer = "\n\n".join([tr["result"] for tr in tool_results])
                        send_msg(answer)
                        return
                    raise fe
                answer = final.text.strip() if final and final.text else "\n\n".join([tr["result"] for tr in tool_results])

        if not answer:
            answer = "네"

        send_msg(answer)

    except Exception as e:
        print(f"❌ {model_name} 최종 오류: {e}")
        print("🔄 Gemini 오류 → GPT 폴백 시도...")
        fallback = _gpt_fallback(msg) or local_fallback_answer(msg)
        if fallback:
            send_msg(fallback)
            return
        send_msg("일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.")

def _watch_traders():
    """데이브/레오 프로세스 감시 및 자동 재시작 (60초 주기)"""
    import subprocess, threading
    manual_stop_dir = os.path.join(PROJECT_ROOT, "projects", "ai-team", "scripts")
    global_manual_stop = os.path.join(manual_stop_dir, ".manual_stop")

    TRADERS = {
        "hyunbin": {
            "script": "projects/ai-team/skills/현빈_전략가/tools/crypto_market_intelligence.py",
            "args": ["--daemon"],
            "keyword": "crypto_market_intelligence",
            "lock": "/tmp/ailab_locks/hyunbin.lock",
        },
        "dave": {
            "script": "projects/ai-team/skills/데이브_주식/tools/upbit_auto_trader.py",
            "args": ["--daemon"],
            "keyword": "upbit_auto_trader",
            "lock": "/tmp/ailab_locks/dave.lock",
        },
        "leo": {
            "script": "projects/ai-team/skills/레오_트레이더/tools/leo_aggressive_trader.py",
            "args": ["--daemon", "--live"],
            "keyword": "leo_aggressive_trader",
            "lock": "/tmp/ailab_locks/leo.lock",
        },
    }

    def is_running(keyword):
        needle = keyword.lower()
        root_marker = PROJECT_ROOT.replace("\\", "/").lower()
        try:
            import psutil
            for proc in psutil.process_iter(["pid", "name", "cmdline"]):
                try:
                    cmd = " ".join(proc.info.get("cmdline") or []).replace("\\", "/").lower()
                    name = str(proc.info.get("name") or "").lower()
                    if name.startswith("python") and needle in cmd and root_marker in cmd:
                        return True
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue
        except Exception as e:
            print(f"[trader_watch] psutil 조회 실패, PowerShell fallback 사용: {e}")
            try:
                command = (
                    "Get-CimInstance Win32_Process | "
                    "Where-Object { $_.Name -match '^python' } | "
                    "Select-Object -ExpandProperty CommandLine"
                )
                result = subprocess.run(
                    ["powershell", "-NoProfile", "-Command", command],
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=15,
                    creationflags=CREATE_NO_WINDOW,
                )
                if result.returncode != 0:
                    print(f"[trader_watch] PowerShell 조회 실패: {result.stderr.strip()}")
                    return False
                for line in result.stdout.splitlines():
                    cmd = line.replace("\\", "/").lower()
                    if needle in cmd and root_marker in cmd:
                        return True
            except Exception as pe:
                print(f"[trader_watch] PowerShell fallback 실패: {pe}")
                return False
        return False

    def restart(name, info):
        try:
            agent_manual_stop = os.path.join(manual_stop_dir, f".manual_stop_{name}")
            if os.path.exists(global_manual_stop) or os.path.exists(agent_manual_stop):
                print(f"[trader_watch] {name} 수동 종료 플래그 감지 - 자동 재시작 생략")
                return
            lock = info["lock"]
            if os.path.exists(lock):
                os.remove(lock)
            root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", ".."))
            script = os.path.join(root, info["script"])
            subprocess.Popen([sys.executable, "-u", script] + info["args"], cwd=root, creationflags=CREATE_NO_WINDOW)
            send_msg(f"🔄 [영숙] {name} 종료 감지 → 자동 재시작")
        except Exception as e:
            send_msg(f"⚠️ [영숙] {name} 재시작 실패: {e}")

    def loop():
        time.sleep(30)
        while True:
            try:
                if os.path.exists(global_manual_stop):
                    print("[trader_watch] 전체 수동 종료 플래그 감지 - 감시 재시작 대기")
                    time.sleep(60)
                    continue
                for name, info in TRADERS.items():
                    if not is_running(info["keyword"]):
                        restart(name, info)
            except Exception as e:
                print(f"[trader_watch] {e}")
            time.sleep(60)

    threading.Thread(target=loop, daemon=True, name="trader_watch").start()
    print("✅ 데이브/레오 감시 스레드 시작")


def _watch_cleanup():
    """중복 프로세스 실시간 감시 — 중복 감지 즉시 제거"""
    import threading

    BOT_KEYWORDS = {
        "데이브": "upbit_auto_trader",
        "레오": "leo_aggressive_trader",
        "현빈": "crypto_market_intelligence",
        "영숙": "telegram_receiver",
    }
    ROOT_MARKER = PROJECT_ROOT.replace("\\", "/").lower()

    def get_pids(keyword):
        needle = keyword.lower()
        pids = []
        try:
            import psutil
            for proc in psutil.process_iter(["pid", "name", "cmdline", "create_time"]):
                try:
                    name = str(proc.info.get("name") or "").lower()
                    cmd = " ".join(proc.info.get("cmdline") or []).replace("\\", "/").lower()
                    if name.startswith("python") and needle in cmd and ROOT_MARKER in cmd:
                        pids.append((proc.info["create_time"], proc.info["pid"]))
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue
        except Exception as e:
            print(f"[cleanup_watch] psutil 오류: {e}")
        return sorted(pids)  # 오래된 순 정렬

    def kill_pid(pid):
        try:
            import psutil
            p = psutil.Process(pid)
            p.terminate()
            p.wait(timeout=5)
        except Exception:
            try:
                import signal
                os.kill(pid, signal.SIGKILL)
            except Exception:
                pass

    def loop():
        time.sleep(30)
        while True:
            try:
                for name, keyword in BOT_KEYWORDS.items():
                    procs = get_pids(keyword)
                    if len(procs) > 1:
                        extras = procs[:-1]  # 최신 1개 남기고 나머지 제거
                        killed = []
                        for _, pid in extras:
                            kill_pid(pid)
                            killed.append(pid)
                        msg = f"🧹 [영숙] {name} 중복 {len(killed)}개 제거 (PID: {', '.join(map(str, killed))})"
                        print(msg)
                        send_msg(msg)
            except Exception as e:
                print(f"[cleanup_watch] {e}")
            time.sleep(30)

    threading.Thread(target=loop, daemon=True, name="cleanup_watch").start()
    print("✅ 중복 프로세스 실시간 감시 스레드 시작")


def _hold_extension_telegram_lock():
    """Keep the IDE extension from polling Telegram while Youngsuk owns getUpdates."""
    import threading

    lock_paths = [
        os.path.join(os.path.expanduser("~"), ".ai-team-brain", ".telegram_poll.lock"),
        os.path.join(os.path.expanduser("~"), ".connect-ai-brain", ".telegram_poll.lock"),
    ]

    def write_lock():
        payload = {"pid": os.getpid(), "owner": "youngsuk-python", "heartbeat": int(time.time() * 1000)}
        for lock_path in lock_paths:
            os.makedirs(os.path.dirname(lock_path), exist_ok=True)
            with open(lock_path, "w", encoding="utf-8") as f:
                json.dump(payload, f)

    def loop():
        while True:
            try:
                write_lock()
            except Exception as e:
                print(f"[telegram_lock] {e}")
            time.sleep(15)

    try:
        write_lock()
    except Exception as e:
        print(f"[telegram_lock] {e}")
    threading.Thread(target=loop, daemon=True, name="telegram_lock").start()
    print("[telegram_lock] Youngsuk owns Telegram getUpdates")


def _check_telegram_conflicts():
    """Telegram API 충돌 감지 및 해결"""
    print("🔍 Telegram API 충돌 확인 중...")

    # 1. Telegram Desktop 프로세스 확인
    try:
        import psutil
        telegram_processes = []
        for p in psutil.process_iter(['pid', 'name']):
            try:
                if p.info['name'] and 'telegram' in p.info['name'].lower():
                    telegram_processes.append((p.info['pid'], p.info['name']))
            except:
                pass

        if telegram_processes:
            print("⚠️ Telegram Desktop 감지:")
            for pid, name in telegram_processes:
                print(f"   PID {pid}: {name}")
            print("   → 이 프로세스들이 봇과 충돌할 수 있습니다")
            print("   → Telegram Desktop을 종료하거나 다른 계정으로 사용하세요")
            # 자동 종료는 하지 않음 (사용자 데이터 손실 방지)
    except ImportError:
        print("⚠️ psutil 모듈 없음 - 충돌 감지 건너뜀")
    except Exception as e:
        print(f"⚠️ 충돌 감지 실패: {e}")

    # 2. 테스트 getUpdates 호출
    try:
        test_res = tg_api("getUpdates", {"timeout": 1, "offset": 0}, timeout=5)
        if test_res.get("ok"):
            print("✅ Telegram API 연결 정상")
            return True
        else:
            error_desc = test_res.get("description", "")
            if "conflict" in error_desc.lower():
                print(f"❌ Telegram API Conflict: {error_desc}")
                print("   → 다른 클라이언트가 이미 연결 중입니다")
                print("   → Telegram Desktop, 웹, 또는 다른 봇 인스턴스를 종료하세요")
                return False
            else:
                print(f"⚠️ Telegram API 오류: {error_desc}")
                return False
    except Exception as e:
        error_str = str(e)
        if "409" in error_str or "Conflict" in error_str:
            print(f"❌ 409 Conflict 감지: {e}")
            print("   → Telegram Desktop 또는 다른 봇 인스턴스를 종료하세요")
            return False
        else:
            print(f"⚠️ 연결 테스트 실패: {e}")
            return False

def main():
    # 중복 실행 방지
    try:
        import psutil
        current_pid = os.getpid()
        for p in psutil.process_iter(['pid', 'name', 'cmdline']):
            try:
                if p.info['pid'] == current_pid:
                    continue
                name = str(p.info.get('name') or '').lower()
                cmd = p.info['cmdline']
                if name.startswith("python") and cmd and any(('telegram_receiver.py' in str(arg) or 'run_youngsuk_daemon.py' in str(arg)) for arg in cmd):
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

    # Keep pending updates so messages sent while the bot was restarting are not lost.
    tg_api("deleteWebhook", {"drop_pending_updates": False})

    # Telegram API 충돌 확인
    if not _check_telegram_conflicts():
        print("=" * 60)
        print("⚠️ Telegram API 충돌이 감지되었습니다")
        print("   계속 실행하면 409 Conflict 오류가 반복됩니다")
        print("   Telegram Desktop을 종료하고 다시 시작하세요")
        print("=" * 60)
        # 충돌이 있어도 계속 실행 (사용자가 수동으로 해결할 수 있도록)

    send_msg("🤖 영숙 출근 (최적화 모드)\n예: 현황/일정")
    _hold_extension_telegram_lock()
    _watch_traders()
    _watch_cleanup()

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
