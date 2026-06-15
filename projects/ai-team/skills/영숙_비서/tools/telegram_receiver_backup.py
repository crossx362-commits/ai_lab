import os
import sys
import json
import time
import urllib.request
import urllib.error
import traceback

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

from google import genai
from google.genai import types

# Initialize google-genai client
client = genai.Client()

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# Import CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

YEONGSUK_PERSONA = """당신은 영숙입니다. 사장님(User)의 개인 비서이며, 사장님의 지시를 받아 작업을 수행합니다.
규칙:
- 핵심만 짧고 직관적으로 답하십시오. 미사여구와 불필요한 인사는 생략하십시오.
- 작업을 지시받으면 알맞은 도구(Tool)를 실행하여 작업을 처리하십시오.
- 제공된 도구 외에 임의로 상태를 완료 처리하거나 정보를 만들어내지 마십시오."""

CHAT_HISTORY = [] # 대화 기록 (GenAI SDK Content 구조 저장)

def _api(method: str, payload: dict, timeout: int = 15) -> dict:
    url  = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req  = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"  [API Error] {method}: {e}")
        return {}

def send_message(text: str):
    print(f"  [영숙 발신] {text[:50]}...")
    _api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset: int) -> list:
    res = _api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]}, timeout=40)
    return res.get("result", [])

def format_ceo_report(ceo_result: str) -> str:
    if ceo_result.startswith("❌") or "실패" in ceo_result or "오류" in ceo_result:
        return ceo_result
    prompt = (
        "아래 업무 결과를 2~3줄로 요약해줘. "
        "성공/실패 여부와 핵심 수치(제목·URL·포스트ID 등)만 포함. "
        "이모지 1개만, 인사말·칭찬·감사 문구 없이 결과만.\n\n"
        f"결과:\n{ceo_result}"
    )
    try:
        res = client.models.generate_content(
            model="gemini-1.5-flash",
            contents=prompt,
            config=types.GenerateContentConfig(
                max_output_tokens=100,
                temperature=0.7
            )
        )
        if res.text:
            return res.text.strip()
    except Exception as e:
        print(f"  [Gemini 요약 실패] {e}")
    return ceo_result

def _handle_status_query(agent: str) -> str:
    """에이전트 현황을 실제 데이터에서 읽어 반환."""
    PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))
    lines = []

    def _read_history(agent_name: str):
        hist_path = os.path.join(PROJECT_ROOT, "reports", "history", "upload_history.json")
        if not os.path.exists(hist_path):
            return []
        try:
            with open(hist_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return [d for d in data if d.get("agent") == agent_name] if isinstance(data, list) else []
        except Exception:
            return []

    _NOISE = ("[로컬 AI", "[Ollama]", "자동 감지:", "reconfigure", "[AI →")

    def _read_log_meaningful(log_path: str, n: int = 8) -> list:
        if not os.path.exists(log_path):
            return []
        try:
            with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
                rows = f.readlines()
            clean = [r.strip() for r in rows if r.strip() and not any(r.strip().startswith(x) for x in _NOISE)]
            return clean[-n:]
        except Exception:
            return []

    # 루나 현황
    if "루나" in agent or "전체" in agent:
        luna_hist = _read_history("루나")
        if luna_hist:
            last = luna_hist[-1]
            meta = last.get("metadata", {})
            title = meta.get("youtube_title") or meta.get("title", "?")
            vid = meta.get("video_id", "")
            url = f"https://youtu.be/{vid}" if vid else "없음"
            date = last.get("uploaded_at", "")[:16].replace("T", " ")
            status = last.get("status", "?")
            lines.append(f"🎬 <b>루나</b> 최근 업로드 ({status})")
            lines.append(f"  {date}")
            lines.append(f"  {title[:50]}")
            lines.append(f"  {url}")
            lines.append(f"  누적 {len(luna_hist)}개")
        else:
            lines.append("🎬 루나: 업로드 기록 없음")

        log_rows = _read_log_meaningful(os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "pipeline.log"))
        if log_rows:
            lines.append("  📋 최근 로그:")
            for row in log_rows[-4:]:
                lines.append(f"    {row[:90]}")

    # 아린 현황
    if "아린" in agent or "전체" in agent:
        arin_hist = _read_history("아린")
        if arin_hist:
            last = arin_hist[-1]
            meta = last.get("metadata", {})
            caption = meta.get("caption", "?")[:50].replace("\n", " ")
            date = last.get("uploaded_at", "")[:16].replace("T", " ")
            status = last.get("status", "?")
            post_id = meta.get("post_id", "")
            lines.append(f"📸 <b>아린</b> 최근 포스팅 ({status})")
            lines.append(f"  {date}")
            lines.append(f"  {caption}")
            if post_id:
                lines.append(f"  ID: {post_id}")
            lines.append(f"  누적 {len(arin_hist)}개")
        else:
            lines.append("📸 아린: 포스팅 기록 없음")

        log_rows = _read_log_meaningful(os.path.join(PROJECT_ROOT, "reports", "uploads", "arin", "pipeline.log"))
        if log_rows:
            lines.append("  📋 최근 로그:")
            for row in log_rows[-4:]:
                lines.append(f"    {row[:90]}")

    # 데이브 현황
    if "데이브" in agent or "전체" in agent:
        import datetime
        dave_path = os.path.join(PROJECT_ROOT, "reports", "research", "dave_upbit_analysis.md")
        if os.path.exists(dave_path):
            try:
                with open(dave_path, "r", encoding="utf-8") as f:
                    content = f.read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "최종 결정 (Decision):" in line:
                        decision = line.split("최종 결정 (Decision):")[1].strip()
                        break
                mtime = os.path.getmtime(dave_path)
                date = datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M")
                lines.append(f"📈 <b>데이브 (가상자산)</b> 최근 분석 ({date})")
                lines.append(f"  최종 결정: {decision}")
            except Exception as e:
                lines.append(f"📈 데이브 (가상자산): 로드 오류 ({e})")
        else:
            lines.append("📈 데이브 (가상자산): 분석 기록 없음")

        dave_stock_path = os.path.join(PROJECT_ROOT, "reports", "research", "dave_stock_analysis.md")
        if os.path.exists(dave_stock_path):
            try:
                with open(dave_stock_path, "r", encoding="utf-8") as f:
                    content = f.read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "결론:" in line:
                        decision = line.replace("## 결론:", "").strip()
                        break
                mtime = os.path.getmtime(dave_stock_path)
                date = datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M")
                lines.append(f"📉 <b>데이브 (주식)</b> 최근 분석 ({date})")
                lines.append(f"  결론: {decision}")
            except Exception as e:
                lines.append(f"📉 데이브 (주식): 로드 오류 ({e})")
        else:
            lines.append("📉 데이브 (주식): 분석 기록 없음")

    if not lines:
        lines.append("⚠️ 조회할 에이전트를 특정해주세요 (루나 / 아린 / 데이브 / 전체)")

    return "📊 <b>[현황 보고]</b>\n\n" + "\n".join(lines)

def _gcal_service():
    import importlib.util
    if not importlib.util.find_spec("googleapiclient"):
        return None
    try:
        from google.oauth2.credentials import Credentials
        from googleapiclient.discovery import build
        cfg_path = os.path.join(_here, "google_calendar_write.json")
        if not os.path.exists(cfg_path):
            return None
        with open(cfg_path, "r", encoding="utf-8") as f:
            cfg = json.load(f)
        creds = Credentials(
            token=None,
            refresh_token=cfg.get("REFRESH_TOKEN", ""),
            client_id=cfg.get("CLIENT_ID", ""),
            client_secret=cfg.get("CLIENT_SECRET", ""),
            token_uri="https://oauth2.googleapis.com/token",
            scopes=["https://www.googleapis.com/auth/calendar.events"],
        )
        return build("calendar", "v3", credentials=creds, cache_discovery=False)
    except Exception as e:
        print(f"  [캘린더 인증] 실패: {e}")
        return None

def _handle_calendar_list(days_ahead: int = 7):
    cache = os.path.join(os.path.dirname(_here), "..", "..", "..", "_shared", "calendar_cache.md")
    cache = os.path.abspath(cache)
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            content = f.read()
        return f"📅 <b>[일정 조회]</b>\n\n{content[:1500]}"
    try:
        import subprocess
        cal_py = os.path.join(_here, "google_calendar.py")
        result = subprocess.run([sys.executable, cal_py], capture_output=True, text=True, timeout=20)
        out = (result.stdout or "일정 없음").strip()
        return f"📅 <b>[일정 조회]</b>\n\n{out[:1000]}"
    except Exception as e:
        return f"📅 캘린더 조회 실패: {e}"

def _handle_calendar_create(event: dict):
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동이 설정되지 않았어요."
    try:
        start = event.get("start", "")
        dur   = int(event.get("duration_minutes", 60))
        from datetime import datetime, timedelta
        dt_start = datetime.fromisoformat(start)
        dt_end   = dt_start + timedelta(minutes=dur)
        body = {
            "summary":     event.get("title", "일정"),
            "description": event.get("description", ""),
            "location":    event.get("location", ""),
            "start": {"dateTime": dt_start.isoformat(), "timeZone": "Asia/Seoul"},
            "end":   {"dateTime": dt_end.isoformat(),   "timeZone": "Asia/Seoul"},
            "reminders": {"useDefault": False, "overrides": [
                {"method": "popup", "minutes": 60},
                {"method": "popup", "minutes": 5},
            ]},
        }
        svc.events().insert(calendarId="primary", body=body).execute()
        return f"✅ 📅 <b>{event.get('title', '일정')}</b> 등록 완료!\n{dt_start.strftime('%m/%d %H:%M')} ({dur}분)"
    except Exception as e:
        return f"❌ 일정 생성 실패: {e}"

def _handle_calendar_delete(query: str, days_ahead: int, delete_all: bool):
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동 설정 필요."
    try:
        from datetime import datetime, timedelta, timezone
        now = datetime.now(timezone.utc)
        end = now + timedelta(days=days_ahead)
        result = svc.events().list(
            calendarId="primary", timeMin=now.isoformat(), timeMax=end.isoformat(),
            q=query, maxResults=10, singleEvents=True, orderBy="startTime"
        ).execute()
        items = result.get("items", [])
        if not items:
            return f"📅 '{query}' 관련 일정을 찾지 못했어요."
        targets = items if delete_all else items[:1]
        for ev in targets:
            svc.events().delete(calendarId="primary", eventId=ev["id"]).execute()
        names = ", ".join(ev.get("summary", "?") for ev in targets)
        return f"✅ 📅 일정 취소 완료: {names}"
    except Exception as e:
        return f"❌ 일정 취소 실패: {e}"

def _handle_calendar_update(query: str, days_ahead: int, patch: dict):
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동 설정 필요."
    try:
        from datetime import datetime, timedelta, timezone
        now = datetime.now(timezone.utc)
        end = now + timedelta(days=days_ahead)
        result = svc.events().list(
            calendarId="primary", timeMin=now.isoformat(), timeMax=end.isoformat(),
            q=query, maxResults=5, singleEvents=True, orderBy="startTime"
        ).execute()
        items = result.get("items", [])
        if not items:
            return f"📅 '{query}' 관련 일정을 찾지 못했어요."
        ev = items[0]
        if "start" in patch:
            dt = datetime.fromisoformat(patch["start"])
            dur = int(patch.get("duration_minutes", 60))
            ev["start"] = {"dateTime": dt.isoformat(), "timeZone": "Asia/Seoul"}
            ev["end"]   = {"dateTime": (dt + timedelta(minutes=dur)).isoformat(), "timeZone": "Asia/Seoul"}
        if "title" in patch:
            ev["summary"] = patch["title"]
        svc.events().update(calendarId="primary", eventId=ev["id"], body=ev).execute()
        return f"✅ 📅 일정 수정 완료: {ev.get('summary', '?')}"
    except Exception as e:
        return f"❌ 일정 수정 실패: {e}"


# ─── 구글 제미니 전용 Tool 정의 (Function Calling) ───────────────────────────

def get_agent_status(agent: str) -> str:
    """에이전트의 현재 작업 현황 및 최근 로그를 조회합니다.
    Args:
        agent: 조회 대상 에이전트 ('루나', '아린', '데이브', '전체')
    """
    return _handle_status_query(agent)

def list_calendar_events(days_ahead: int = 7) -> str:
    """구글 캘린더 일정을 조회합니다.
    Args:
        days_ahead: 조회할 미래의 일수 (기본 7일)
    """
    return _handle_calendar_list(days_ahead)

def create_calendar_event(title: str, start_iso: str, duration_minutes: int = 60, description: str = "", location: str = "") -> str:
    """구글 캘린더에 일정을 생성/등록합니다.
    Args:
        title: 일정 제목
        start_iso: 시작 일시 (KST 기준, YYYY-MM-DDTHH:MM:SS 형식)
        duration_minutes: 일정 지속 시간(분) (기본 60분)
        description: 일정 상세 설명 (선택)
        location: 일정 장소 (선택)
    """
    event = {
        "title": title,
        "start": start_iso,
        "duration_minutes": duration_minutes,
        "description": description,
        "location": location
    }
    return _handle_calendar_create(event)

def delete_calendar_event(query: str, days_ahead: int = 7, delete_all: bool = False) -> str:
    """키워드로 구글 캘린더 일정을 검색해서 삭제합니다.
    Args:
        query: 삭제할 일정의 검색 키워드
        days_ahead: 검색 대상 미래 일수 (기본 7일)
        delete_all: 검색된 일정을 모두 삭제할지 여부 (기본 False: 첫 번째 항목만 삭제)
    """
    return _handle_calendar_delete(query, days_ahead, delete_all)

def update_calendar_event(query: str, start_iso: str = None, duration_minutes: int = None, title: str = None, days_ahead: int = 7) -> str:
    """키워드로 구글 캘린더 일정을 검색해서 수정합니다.
    Args:
        query: 수정할 일정의 검색 키워드
        start_iso: 새 시작 일시 (KST 기준, YYYY-MM-DDTHH:MM:SS 형식)
        duration_minutes: 새 지속 시간(분)
        title: 새 일정 제목
        days_ahead: 검색 대상 미래 일수 (기본 7일)
    """
    patch = {}
    if start_iso:
        patch["start"] = start_iso
    if duration_minutes is not None:
        patch["duration_minutes"] = duration_minutes
    if title:
        patch["title"] = title
    return _handle_calendar_update(query, days_ahead, patch)

def dispatch_to_agents(instruction: str) -> str:
    """에이전트 실행 지시 및 동사형 요청(예: 루나 영상 만들기, 인스타 포스팅 등)을 예원 CEO에게 전달하여 실행합니다.
    Args:
        instruction: 사장님이 지시하신 구체적인 실행 명령 구문
    """
    try:
        ceo_result = yewon_dispatcher.dispatch_and_execute(instruction)
        if ceo_result is None:
            return "⚠️ CEO가 복구 대기 중이라 실행되지 않았습니다."
    except Exception as dispatch_err:
        try:
            from _shared.agent_council import convene_from_exception
            convene_from_exception(dispatch_err, caller_agent="영숙_디스패처")
        except Exception:
            pass
        ceo_result = f"❌ 디스패치 오류: {dispatch_err}"

    if ceo_result and ceo_result.startswith("❌"):
        try:
            from _shared.agent_council import convene
            convene(problem_summary=f"파이프라인 실패: {ceo_result[:300]}", caller_agent="영숙_디스패처")
        except Exception:
            pass

    return format_ceo_report(ceo_result)


# Tool mapping dictionary
TOOLS_MAP = {
    "get_agent_status": get_agent_status,
    "list_calendar_events": list_calendar_events,
    "create_calendar_event": create_calendar_event,
    "delete_calendar_event": delete_calendar_event,
    "update_calendar_event": update_calendar_event,
    "dispatch_to_agents": dispatch_to_agents
}


def process_message(text: str):
    print(f"\n📩 [영숙 수신] {text}")
    global CHAT_HISTORY

    # 1. API 키 확인
    if not os.getenv("GEMINI_API_KEY", "").strip():
        send_message("영숙이에요! 지금 Gemini API 키가 설정되지 않아서 처리가 안 돼요 😭")
        return

    # 2. 한국 표준시 컨텍스트 추가
    import datetime
    kst = datetime.timezone(datetime.timedelta(hours=9))
    now_kst = datetime.datetime.now(kst)
    current_time_context = f"\n\n[현재 한국 표준시 (KST) 정보 - 일정 조율 시 반드시 기준 날짜/요일로 사용]\n- 현재 일시: {now_kst.strftime('%Y-%m-%d %H:%M:%S %A')}\n"
    system_prompt = YEONGSUK_PERSONA + current_time_context

    # 3. 신규 사용자 메시지를 히스토리에 누적
    CHAT_HISTORY.append(types.Content(role="user", parts=[types.Part.from_text(text=text)]))

    # 최근 3개 턴(6개 Content)만 유지하여 토큰 최적화
    if len(CHAT_HISTORY) > 6:
        CHAT_HISTORY = CHAT_HISTORY[-6:]

    try:
        # 4. 제미니 API 호출 (Function Calling 구성)
        response = client.models.generate_content(
            model="gemini-1.5-flash",
            contents=CHAT_HISTORY,
            config=types.GenerateContentConfig(
                system_instruction=system_prompt,
                tools=list(TOOLS_MAP.values()),
                max_output_tokens=300,
                temperature=0.7
            )
        )

        final_reply = ""

        # 5. Function Calling 발생 시 실행
        if response.function_calls:
            for call in response.function_calls:
                name = call.name
                args = call.args
                print(f"  [Gemini Tool Call] {name}({args})")

                if name in TOOLS_MAP:
                    try:
                        # 도구 실행
                        tool_result = TOOLS_MAP[name](**args)
                        final_reply += f"{tool_result}\n"
                    except Exception as e:
                        final_reply += f"❌ 도구 실행 오류 ({name}): {e}\n"
                else:
                    final_reply += f"⚠️ 지원하지 않는 도구입니다: {name}\n"
        else:
            final_reply = response.text or "네 알겠습니다!"

        # 6. 최종 텔레그램 메시지 발송
        send_message(final_reply.strip())

        # 7. 어시스턴트의 최종 응답을 히스토리에 누적
        CHAT_HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=final_reply)]))
        if len(CHAT_HISTORY) > 6:
            CHAT_HISTORY = CHAT_HISTORY[-6:]

    except Exception as e:
        error_str = str(e)
        print(f"  [오류] 영숙 메시지 처리 실패: {e}")

        # Gemini 할당량 초과 시 Claude로 폴백
        if "429" in error_str or "RESOURCE_EXHAUSTED" in error_str or "quota" in error_str.lower():
            print("  [Fallback] Gemini 할당량 초과 - Claude API로 전환")
            try:
                from _shared.claude_client import chat as claude_chat

                # 히스토리를 간단한 텍스트로 변환
                history_text = ""
                for content in CHAT_HISTORY[-4:]:  # 최근 2턴만
                    role = "사용자" if content.role == "user" else "영숙"
                    for part in content.parts:
                        if hasattr(part, 'text'):
                            history_text += f"{role}: {part.text}\n"

                full_prompt = f"{history_text}\n사용자: {text}"

                claude_response = claude_chat(
                    prompt=full_prompt,
                    system=system_prompt + "\n\n주의: Tool 사용은 지원하지 않으므로 직접 답변하세요.",
                    max_tokens=300,
                    temperature=0.7
                )

                if claude_response:
                    send_message(f"[Claude 모드] {claude_response}")
                    CHAT_HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=claude_response)]))
                    return
            except Exception as claude_err:
                print(f"  [Claude Fallback 실패] {claude_err}")

        traceback.print_exc()
        send_message("죄송해요, 일시적인 오류가 발생했어요. 잠시 후 다시 시도해주세요.")

        try:
            from _shared.agent_council import convene_from_exception
            convene_from_exception(e, caller_agent="영숙_리시버")
        except Exception:
            pass

def main_loop():
    print("🚀 영숙 전용 텔레그램 리시버가 시작되었습니다!")
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
